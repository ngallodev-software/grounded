#!/usr/bin/env python3

from __future__ import annotations

import json
import os
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
ARTIFACTS_DIR = ROOT / "eval" / "artifacts"
BENCHMARK_PATH = ROOT / "eval" / "benchmark_cases.jsonl"
PROMPTS_DIR = ROOT / "prompts"
IMAGE = "llm-integration-demo-api:latest"
NETWORK = "llm-integration-demo_default"
PORT_BASE = 5401


@dataclass(frozen=True)
class RunSpec:
    provider: str
    prompt_version: str
    planner_model: str
    synthesizer_model: str

    @property
    def slug(self) -> str:
        return f"{self.provider}_{self.prompt_version}"

    @property
    def container_name(self) -> str:
        return f"grounded-eval-{self.provider}-{self.prompt_version}"


def run_command(args: list[str], capture: bool = True, check: bool = True) -> str:
    result = subprocess.run(
        args,
        cwd=ROOT,
        text=True,
        capture_output=capture,
        check=False,
    )
    if check and result.returncode != 0:
        raise RuntimeError(
            f"command failed ({result.returncode}): {' '.join(args)}\n"
            f"stdout:\n{result.stdout}\n"
            f"stderr:\n{result.stderr}"
        )
    return result.stdout.strip()


def load_container_env(container_name: str) -> dict[str, str]:
    payload = run_command(
        ["docker", "inspect", container_name, "--format", "{{json .Config.Env}}"]
    )
    values = json.loads(payload)
    env: dict[str, str] = {}
    for item in values:
        key, _, value = item.partition("=")
        env[key] = value
    return env


def load_benchmark_cases() -> dict[str, dict[str, Any]]:
    cases: dict[str, dict[str, Any]] = {}
    with BENCHMARK_PATH.open("r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            case = json.loads(line)
            cases[case["caseId"]] = case
    return cases


def find_free_port(start: int) -> int:
    port = start
    while True:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            if sock.connect_ex(("127.0.0.1", port)) != 0:
                return port
        port += 1


def wait_for_ready(port: int, timeout_seconds: float = 60.0) -> None:
    deadline = time.time() + timeout_seconds
    url = f"http://127.0.0.1:{port}/openapi/v1.json"
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=2) as response:
                if response.status == 200:
                    return
        except Exception:
            time.sleep(1)
    raise RuntimeError(f"timed out waiting for API on port {port}")


def post_eval(port: int) -> dict[str, Any]:
    url = f"http://127.0.0.1:{port}/analytics/eval"
    request = urllib.request.Request(url, method="POST", data=b"")
    request.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(request, timeout=1800) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"eval request failed: {error.code}\n{body}") from error


def ensure_removed(container_name: str) -> None:
    subprocess.run(
        ["docker", "rm", "-f", container_name],
        cwd=ROOT,
        text=True,
        capture_output=True,
        check=False,
    )


def start_container(spec: RunSpec, base_env: dict[str, str], port: int) -> None:
    ensure_removed(spec.container_name)

    args = [
        "docker",
        "run",
        "-d",
        "--name",
        spec.container_name,
        "--network",
        NETWORK,
        "-p",
        f"127.0.0.1:{port}:5252",
        "-v",
        f"{PROMPTS_DIR}:/app/prompts:ro",
        "-v",
        f"{ROOT / 'eval'}:/app/eval:ro",
        "-e",
        f"ConnectionStrings__AnalyticsDatabase={base_env['ConnectionStrings__AnalyticsDatabase']}",
        "-e",
        f"OPENAI_API_KEY={base_env.get('OPENAI_API_KEY', '')}",
        "-e",
        f"ANTHROPIC_API_KEY={base_env.get('ANTHROPIC_API_KEY', '')}",
        "-e",
        f"GROUNDED_OPENAI_BASE_URL={base_env.get('GROUNDED_OPENAI_BASE_URL', 'https://api.openai.com/v1/')}",
        "-e",
        f"GROUNDED_OPENAI_TIMEOUT_SECONDS={base_env.get('GROUNDED_OPENAI_TIMEOUT_SECONDS', '15')}",
        "-e",
        f"GROUNDED_ANTHROPIC_BASE_URL={base_env.get('GROUNDED_ANTHROPIC_BASE_URL', 'https://api.anthropic.com/v1/')}",
        "-e",
        f"GROUNDED_ANTHROPIC_VERSION={base_env.get('GROUNDED_ANTHROPIC_VERSION', '2023-06-01')}",
        "-e",
        f"GROUNDED_ANTHROPIC_TIMEOUT_SECONDS={base_env.get('GROUNDED_ANTHROPIC_TIMEOUT_SECONDS', '15')}",
        "-e",
        f"GROUNDED_PLANNER_TIMEOUT_SECONDS={base_env.get('GROUNDED_PLANNER_TIMEOUT_SECONDS', '15')}",
        "-e",
        "GROUNDED_REPLAY_MODE=false",
        "-e",
        "Eval__HistoryPath=/tmp/regression_history.json",
        "-e",
        f"GROUNDED_PLANNER_PROVIDER={spec.provider}",
        "-e",
        f"GROUNDED_SYNTHESIS_PROVIDER={spec.provider}",
        "-e",
        f"GROUNDED_PLANNER_MODEL={spec.planner_model}",
        "-e",
        f"GROUNDED_SYNTHESIS_MODEL={spec.synthesizer_model}",
        "-e",
        f"GROUNDED_PLANNER_PROMPT_VERSION={spec.prompt_version}",
        "-e",
        "ASPNETCORE_ENVIRONMENT=Production",
        "-e",
        "ASPNETCORE_URLS=http://+:5252",
        IMAGE,
    ]
    run_command(args)


def adjust_eval(raw: dict[str, Any], cases: dict[str, dict[str, Any]], spec: RunSpec) -> dict[str, Any]:
    adjusted_results: list[dict[str, Any]] = []
    adjusted_scores: list[float] = []
    adjusted_passes = 0
    expected_failure_total = 0
    expected_failure_passes = 0

    for result in raw["run"]["caseResults"]:
        case = cases[result["caseId"]]
        expected_failure = case.get("expectedOutcomeType") == "failure"
        if expected_failure:
            expected_failure_total += 1
        adjusted_pass = (not result["executionSuccess"]) if expected_failure else bool(result["passed"])
        adjusted_score = 1.0 if expected_failure and adjusted_pass else (0.0 if expected_failure else float(result["score"]))
        if adjusted_pass:
            adjusted_passes += 1
        if expected_failure and adjusted_pass:
            expected_failure_passes += 1

        enriched = dict(result)
        enriched["expectedOutcomeType"] = case.get("expectedOutcomeType")
        enriched["expectedFailureCategory"] = case.get("expectedFailureCategory")
        enriched["adjustedPassed"] = adjusted_pass
        enriched["adjustedScore"] = round(adjusted_score, 3)
        adjusted_results.append(enriched)
        adjusted_scores.append(adjusted_score)

    adjusted_overall = round(sum(adjusted_scores) / len(adjusted_scores), 3) if adjusted_scores else 0.0
    adjusted_pass_rate = round(adjusted_passes / len(adjusted_results), 3) if adjusted_results else 0.0

    payload = {
        "provider": spec.provider,
        "plannerPromptVersion": spec.prompt_version,
        "plannerModel": spec.planner_model,
        "synthesizerModel": spec.synthesizer_model,
        "raw": raw,
        "adjusted": {
            "overallScore": adjusted_overall,
            "passRate": adjusted_pass_rate,
            "passCount": adjusted_passes,
            "caseCount": len(adjusted_results),
            "expectedFailureCount": expected_failure_total,
            "expectedFailurePassCount": expected_failure_passes,
            "results": adjusted_results,
        },
    }
    return payload


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def compare_provider_runs(provider: str, runs: list[dict[str, Any]]) -> str:
    header = [
        f"# {provider.title()} Planner Prompt Comparison",
        "",
        "| Version | Adjusted score | Adjusted pass rate | Raw score | Raw execution success | Raw grounding | Expected-failure passes |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for run in runs:
        raw_run = run["raw"]["run"]
        adjusted = run["adjusted"]
        header.append(
            f"| `{run['plannerPromptVersion']}` | "
            f"{adjusted['overallScore']:.3f} | "
            f"{adjusted['passRate']:.3%} | "
            f"{float(raw_run['score']):.3f} | "
            f"{float(raw_run['summary']['executionSuccessRate']):.3%} | "
            f"{float(raw_run['summary']['groundingRate']):.3%} | "
            f"{adjusted['expectedFailurePassCount']}/{adjusted['expectedFailureCount']} |"
        )

    best = max(runs, key=lambda item: item["adjusted"]["overallScore"])
    body = [
        "",
        f"Best adjusted score: `{best['plannerPromptVersion']}` at `{best['adjusted']['overallScore']:.3f}`.",
        "",
        "## Cases That Changed Across Versions",
        "",
    ]

    case_ids = [result["caseId"] for result in runs[0]["adjusted"]["results"]]
    for case_id in case_ids:
        statuses = []
        for run in runs:
            result = next(item for item in run["adjusted"]["results"] if item["caseId"] == case_id)
            statuses.append((run["plannerPromptVersion"], result["adjustedPassed"], result["failureCategory"]))
        if len({status[1] for status in statuses}) > 1:
            formatted = ", ".join(
                f"{version}={'pass' if passed else 'fail'} ({failure or 'none'})"
                for version, passed, failure in statuses
            )
            body.append(f"- `{case_id}`: {formatted}")

    if body[-1] == "":
        body.append("- No case-level pass/fail deltas.")

    return "\n".join(header + body) + "\n"


def compare_cross_provider(openai_runs: list[dict[str, Any]], anthropic_runs: list[dict[str, Any]]) -> str:
    lines = [
        "# OpenAI vs Anthropic by Prompt Version",
        "",
        "| Version | OpenAI adjusted score | Anthropic adjusted score | Delta (Anthropic - OpenAI) | OpenAI pass rate | Anthropic pass rate |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    openai_by_version = {run["plannerPromptVersion"]: run for run in openai_runs}
    anthropic_by_version = {run["plannerPromptVersion"]: run for run in anthropic_runs}

    for version in ["v1", "v2", "v3"]:
        openai_run = openai_by_version[version]
        anthropic_run = anthropic_by_version[version]
        delta = anthropic_run["adjusted"]["overallScore"] - openai_run["adjusted"]["overallScore"]
        lines.append(
            f"| `{version}` | "
            f"{openai_run['adjusted']['overallScore']:.3f} | "
            f"{anthropic_run['adjusted']['overallScore']:.3f} | "
            f"{delta:+.3f} | "
            f"{openai_run['adjusted']['passRate']:.3%} | "
            f"{anthropic_run['adjusted']['passRate']:.3%} |"
        )

    lines.extend(["", "## Per-Version Case Deltas", ""])
    for version in ["v1", "v2", "v3"]:
        openai_run = openai_by_version[version]
        anthropic_run = anthropic_by_version[version]
        lines.append(f"### `{version}`")
        differences = []
        for openai_result in openai_run["adjusted"]["results"]:
            anthropic_result = next(item for item in anthropic_run["adjusted"]["results"] if item["caseId"] == openai_result["caseId"])
            if (
                openai_result["adjustedPassed"] != anthropic_result["adjustedPassed"]
                or openai_result["failureCategory"] != anthropic_result["failureCategory"]
            ):
                differences.append(
                    f"- `{openai_result['caseId']}`: "
                    f"openai={'pass' if openai_result['adjustedPassed'] else 'fail'} ({openai_result['failureCategory'] or 'none'}), "
                    f"anthropic={'pass' if anthropic_result['adjustedPassed'] else 'fail'} ({anthropic_result['failureCategory'] or 'none'})"
                )
        if differences:
            lines.extend(differences)
        else:
            lines.append("- No case-level differences.")
        lines.append("")

    return "\n".join(lines)


def main() -> int:
    ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)
    cases = load_benchmark_cases()
    base_env = load_container_env("grounded-api")

    openai_model = base_env.get("GROUNDED_PLANNER_MODEL", "gpt-4o-mini")
    anthropic_model = "claude-haiku-4-5-20251001"
    specs = [
        RunSpec("openai", "v1", openai_model, base_env.get("GROUNDED_SYNTHESIS_MODEL", openai_model)),
        RunSpec("openai", "v2", openai_model, base_env.get("GROUNDED_SYNTHESIS_MODEL", openai_model)),
        RunSpec("openai", "v3", openai_model, base_env.get("GROUNDED_SYNTHESIS_MODEL", openai_model)),
        RunSpec("anthropic", "v1", anthropic_model, anthropic_model),
        RunSpec("anthropic", "v2", anthropic_model, anthropic_model),
        RunSpec("anthropic", "v3", anthropic_model, anthropic_model),
    ]

    completed_runs: list[dict[str, Any]] = []
    for index, spec in enumerate(specs):
        port = find_free_port(PORT_BASE + index)
        print(f"[run] provider={spec.provider} prompt={spec.prompt_version} port={port}", flush=True)
        start_container(spec, base_env, port)
        try:
            wait_for_ready(port)
            raw = post_eval(port)
        finally:
            ensure_removed(spec.container_name)

        raw_path = ARTIFACTS_DIR / f"eval_run_{spec.slug}_raw.json"
        adjusted = adjust_eval(raw, cases, spec)
        adjusted_path = ARTIFACTS_DIR / f"eval_run_{spec.slug}.json"
        write_json(raw_path, raw)
        write_json(adjusted_path, adjusted)
        if spec.provider == "openai":
            write_json(ARTIFACTS_DIR / f"eval_run_{spec.prompt_version}.json", adjusted)
        completed_runs.append(adjusted)
        print(
            f"[done] provider={spec.provider} prompt={spec.prompt_version} "
            f"adjusted_score={adjusted['adjusted']['overallScore']:.3f} "
            f"pass_rate={adjusted['adjusted']['passRate']:.3%}",
            flush=True,
        )

    openai_runs = [run for run in completed_runs if run["provider"] == "openai"]
    anthropic_runs = [run for run in completed_runs if run["provider"] == "anthropic"]
    openai_runs.sort(key=lambda item: item["plannerPromptVersion"])
    anthropic_runs.sort(key=lambda item: item["plannerPromptVersion"])

    (ARTIFACTS_DIR / "eval_comparison_openai_v1_v2_v3.md").write_text(
        compare_provider_runs("openai", openai_runs),
        encoding="utf-8",
    )
    (ARTIFACTS_DIR / "eval_comparison_anthropic_v1_v2_v3.md").write_text(
        compare_provider_runs("anthropic", anthropic_runs),
        encoding="utf-8",
    )
    (ARTIFACTS_DIR / "eval_comparison_openai_vs_anthropic.md").write_text(
        compare_cross_provider(openai_runs, anthropic_runs),
        encoding="utf-8",
    )

    summary = {
        "generatedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "assumptions": {
            "expectedFailureCases": [
                case_id
                for case_id, case in cases.items()
                if case.get("expectedOutcomeType") == "failure"
            ],
            "expectedFailureScoringRule": "expected failures count as pass when executionSuccess is false; otherwise fail",
        },
        "runs": completed_runs,
    }
    write_json(ARTIFACTS_DIR / "eval_matrix_summary.json", summary)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise

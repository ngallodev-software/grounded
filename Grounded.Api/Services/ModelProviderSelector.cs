using Grounded.Api.Models;

namespace Grounded.Api.Services;

public static class ModelProviderSelector
{
    public static string ResolveInvokerName(string stage)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("GROUNDED_REPLAY_MODE"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return "replay";
        }

        return ResolveProvider(stage) switch
        {
            ModelProvider.Anthropic => "anthropic",
            _ => "openai_compatible"
        };
    }

    public static ModelProvider ResolveProvider(string stage)
    {
        var envKey = stage switch
        {
            "planner" => "GROUNDED_PLANNER_PROVIDER",
            "synthesizer" => "GROUNDED_SYNTHESIS_PROVIDER",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "unknown model stage")
        };

        var raw = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ModelProvider.OpenAiCompatible;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "openai" or "openai_compatible" => ModelProvider.OpenAiCompatible,
            "anthropic" => ModelProvider.Anthropic,
            _ => throw new InvalidOperationException($"Unsupported provider '{raw}' for stage '{stage}'.")
        };
    }
}

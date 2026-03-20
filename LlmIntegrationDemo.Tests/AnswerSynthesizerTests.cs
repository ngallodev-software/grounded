using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LlmIntegrationDemo.Api.Models;
using LlmIntegrationDemo.Api.Services;

namespace LlmIntegrationDemo.Tests;

public sealed class AnswerSynthesizerTests
{
    [Fact]
    public async Task SynthesizeAsync_ReturnsSummaryAndKeyPoints()
    {
        var promptStore = new PromptStore();
        var engine = new DeterministicAnswerSynthesizerEngine();
        var validator = new AnswerOutputValidator();
        var gateway = new DeterministicLlmGateway(engine);
        var synthesizer = new AnswerSynthesizer(promptStore, gateway, validator);

        var plan = new QueryPlan(
            "1.0",
            "ranking",
            "product_name",
            Array.Empty<FilterSpec>(),
            "revenue",
            new TimeRangeSpec("last_month", null, null),
            null,
            new SortSpec("metric", "desc"),
            3,
            false);

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?>
            {
                ["product_name"] = "Alpha",
                ["metric"] = 320m
            },
            new Dictionary<string, object?>
            {
                ["product_name"] = "Beta",
                ["metric"] = 190m
            }
        };

        var (answer, trace) = await synthesizer.SynthesizeAsync(
            "Which product sold the most units last month?",
            plan,
            rows,
            metadata: null,
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(answer.Summary));
        Assert.True(answer.TableIncluded);
        Assert.NotEmpty(answer.KeyPoints);
        Assert.NotNull(trace);
        Assert.Null(trace.ErrorMessage);
    }
}

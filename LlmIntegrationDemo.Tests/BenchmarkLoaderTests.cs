using Microsoft.Extensions.Configuration;
using LlmIntegrationDemo.Api.Services;

namespace LlmIntegrationDemo.Tests;

public sealed class BenchmarkLoaderTests
{
    [Fact]
    public void LoadCases_ReadsBenchmarkFile()
    {
        var configuration = new ConfigurationBuilder().Build();
        var loader = new BenchmarkLoader(configuration);

        var cases = loader.LoadCases();

        Assert.NotEmpty(cases);
        Assert.Contains(cases, benchmarkCase => benchmarkCase.CaseId == "agg_revenue_last_month");
    }
}

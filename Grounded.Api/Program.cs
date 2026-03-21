using Grounded.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new()
        {
            Title = "Grounded Analytics API",
            Version = "1.0.0",
            Description = "Fixed-scope e-commerce analytics API. Converts natural-language questions into validated, parameterized SQL via a structured QueryPlan — no LLM-generated SQL ever reaches the database.",
            Contact = new() { Name = "Grounded" },
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddSingleton<IUtcClock, SystemUtcClock>();
builder.Services.AddSingleton<SqlFragmentRegistry>();
builder.Services.AddSingleton<TimeRangeResolver>();
builder.Services.AddSingleton<QueryPlanValidator>();
builder.Services.AddSingleton<QueryPlanCompiler>();
builder.Services.AddSingleton<SqlSafetyGuard>();
builder.Services.AddSingleton<PromptStore>();
builder.Services.AddSingleton<PlannerContextBuilder>();
builder.Services.AddSingleton<PlannerPromptRenderer>();
builder.Services.AddSingleton<PlannerResponseParser>();
builder.Services.AddSingleton<PlannerResponseRepairService>();
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddSingleton<DeterministicAnswerSynthesizerEngine>();
builder.Services.AddSingleton<ModelInvokerResolver>();
builder.Services.AddSingleton<IModelInvoker, DeterministicModelInvoker>();
builder.Services.AddSingleton<IModelInvoker, ReplayModelInvoker>();
builder.Services.AddSingleton<AnswerOutputValidator>();
builder.Services.AddSingleton<ILlmGateway, OpenAiCompatibleAnswerGateway>();
builder.Services.AddHttpClient<OpenAiCompatibleModelInvoker>(client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("GROUNDED_PLANNER_BASE_URL")
        ?? "https://api.openai.com/v1/";
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    var timeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("GROUNDED_PLANNER_TIMEOUT_SECONDS"), out var configuredTimeout)
        ? configuredTimeout
        : 15;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddSingleton<IModelInvoker>(services => services.GetRequiredService<OpenAiCompatibleModelInvoker>());
builder.Services.AddSingleton<ILlmPlannerGateway, OpenAiCompatiblePlannerGateway>();
builder.Services.AddSingleton<AnswerSynthesizer>();
builder.Services.AddSingleton<BenchmarkLoader>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<RegressionComparer>();
builder.Services.AddScoped<EvalRunner>();
builder.Services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<ITraceRepository, NpgsqlTraceRepository>();
builder.Services.AddScoped<IEvalRepository, NpgsqlEvalRepository>();
builder.Services.AddScoped<IConversationStateRepository, NpgsqlConversationStateRepository>();
builder.Services.AddHostedService<SchemaInitializer>();
builder.Services.AddScoped<IAnalyticsQueryExecutor, AnalyticsQueryExecutor>();
builder.Services.AddScoped<AnalyticsQueryPlanService>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Grounded Analytics API";
    options.Theme = ScalarTheme.DeepSpace;
});
app.MapControllers();

app.Run();

public partial class Program;

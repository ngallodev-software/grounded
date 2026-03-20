using Grounded.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<IUtcClock, SystemUtcClock>();
builder.Services.AddSingleton<SqlFragmentRegistry>();
builder.Services.AddSingleton<TimeRangeResolver>();
builder.Services.AddSingleton<QueryPlanValidator>();
builder.Services.AddSingleton<QueryPlanCompiler>();
builder.Services.AddSingleton<SqlSafetyGuard>();
builder.Services.AddSingleton<PromptStore>();
builder.Services.AddSingleton<DeterministicAnswerSynthesizerEngine>();
builder.Services.AddSingleton<AnswerOutputValidator>();
builder.Services.AddSingleton<ILlmGateway, DeterministicLlmGateway>();
builder.Services.AddSingleton<ILlmPlannerGateway, DeterministicLlmPlannerGateway>();
builder.Services.AddSingleton<AnswerSynthesizer>();
builder.Services.AddSingleton<BenchmarkLoader>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<RegressionComparer>();
builder.Services.AddScoped<EvalRunner>();
builder.Services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<IAnalyticsQueryExecutor, AnalyticsQueryExecutor>();
builder.Services.AddScoped<AnalyticsQueryPlanService>();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;

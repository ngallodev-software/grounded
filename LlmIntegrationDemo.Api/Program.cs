using LlmIntegrationDemo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<IUtcClock, SystemUtcClock>();
builder.Services.AddSingleton<SqlFragmentRegistry>();
builder.Services.AddSingleton<TimeRangeResolver>();
builder.Services.AddSingleton<QueryPlanValidator>();
builder.Services.AddSingleton<QueryPlanCompiler>();
builder.Services.AddSingleton<SqlSafetyGuard>();
builder.Services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<IAnalyticsQueryExecutor, AnalyticsQueryExecutor>();
builder.Services.AddScoped<AnalyticsQueryPlanService>();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;

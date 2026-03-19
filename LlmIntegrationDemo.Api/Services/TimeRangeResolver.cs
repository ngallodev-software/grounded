using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

public interface IUtcClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemUtcClock : IUtcClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class TimeRangeResolver
{
    private readonly IUtcClock _clock;

    public TimeRangeResolver(IUtcClock clock)
    {
        _clock = clock;
    }

    public ResolvedTimeRange Resolve(TimeRangeSpec timeRange)
    {
        var todayUtc = _clock.UtcNow.UtcDateTime.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var monthStart = new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return timeRange.Preset switch
        {
            "last_7_days" => new(ToDateTimeOffset(todayUtc.AddDays(-7)), ToDateTimeOffset(tomorrowUtc)),
            "last_30_days" => new(ToDateTimeOffset(todayUtc.AddDays(-30)), ToDateTimeOffset(tomorrowUtc)),
            "last_90_days" => new(ToDateTimeOffset(todayUtc.AddDays(-90)), ToDateTimeOffset(tomorrowUtc)),
            "last_6_months" => new(ToDateTimeOffset(monthStart.AddMonths(-5)), ToDateTimeOffset(monthStart.AddMonths(1))),
            "last_12_months" => new(ToDateTimeOffset(monthStart.AddMonths(-11)), ToDateTimeOffset(monthStart.AddMonths(1))),
            "month_to_date" => new(ToDateTimeOffset(monthStart), ToDateTimeOffset(tomorrowUtc)),
            "quarter_to_date" => new(ToDateTimeOffset(GetQuarterStart(todayUtc)), ToDateTimeOffset(tomorrowUtc)),
            "year_to_date" => new(ToDateTimeOffset(new DateTime(todayUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc)), ToDateTimeOffset(tomorrowUtc)),
            "last_month" => new(ToDateTimeOffset(monthStart.AddMonths(-1)), ToDateTimeOffset(monthStart)),
            "last_quarter" => ResolveLastQuarter(todayUtc),
            "last_year" => new(ToDateTimeOffset(new DateTime(todayUtc.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)), ToDateTimeOffset(new DateTime(todayUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc))),
            "all_time" => new(null, null),
            "custom_range" => ResolveCustomRange(timeRange),
            _ => throw new InvalidOperationException($"Unsupported time preset '{timeRange.Preset}'.")
        };
    }

    private static ResolvedTimeRange ResolveLastQuarter(DateTime todayUtc)
    {
        var currentQuarterStart = GetQuarterStart(todayUtc);
        return new(ToDateTimeOffset(currentQuarterStart.AddMonths(-3)), ToDateTimeOffset(currentQuarterStart));
    }

    private static ResolvedTimeRange ResolveCustomRange(TimeRangeSpec timeRange)
    {
        var startDate = DateOnly.ParseExact(timeRange.StartDate!, "yyyy-MM-dd");
        var endDate = DateOnly.ParseExact(timeRange.EndDate!, "yyyy-MM-dd");
        return new(
            ToDateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            ToDateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));
    }

    private static DateTime GetQuarterStart(DateTime dateUtc)
    {
        var quarterMonth = ((dateUtc.Month - 1) / 3) * 3 + 1;
        return new DateTime(dateUtc.Year, quarterMonth, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime utcDateTime) => new(utcDateTime, TimeSpan.Zero);
}

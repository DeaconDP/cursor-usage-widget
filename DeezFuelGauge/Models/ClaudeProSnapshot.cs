using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class ClaudeProSnapshot
{
    public double SessionPercentUsed { get; init; }
    public double WeeklyPercentUsed { get; init; }
    public DateTimeOffset? SessionResetsAt { get; init; }
    public DateTimeOffset? WeeklyResetsAt { get; init; }
    public bool ExtraUsageIsAvailable { get; init; }
    public double ExtraUsagePercentRaw { get; init; }
    public double ExtraUsagePercentUsed => Math.Clamp(ExtraUsagePercentRaw, 0, 100);
    public decimal? ExtraUsageUsedUsd { get; init; }
    public decimal? ExtraUsageLimitUsd { get; init; }
    public bool ExtraUsageOutOfCredits { get; init; }
    public DateTimeOffset? ExtraUsageDisabledUntil { get; init; }
    public DateTimeOffset? ExtraUsageResetsAt { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static ClaudeProSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static ClaudeProSnapshot FromUsage(
        double sessionPercent,
        double weeklyPercent,
        DateTimeOffset? sessionResetsAt,
        DateTimeOffset? weeklyResetsAt,
        bool extraUsageIsAvailable = false,
        double extraUsagePercentRaw = 0,
        decimal? extraUsageUsedUsd = null,
        decimal? extraUsageLimitUsd = null,
        bool extraUsageOutOfCredits = false,
        DateTimeOffset? extraUsageDisabledUntil = null,
        DateTimeOffset? extraUsageResetsAt = null)
    {
        var session = Math.Clamp(sessionPercent, 0, 100);
        var weekly = Math.Clamp(weeklyPercent, 0, 100);
        var parts = new List<string>
        {
            $"5h {session.ToString("F0", CultureInfo.InvariantCulture)}%",
            $"wk {weekly.ToString("F0", CultureInfo.InvariantCulture)}%"
        };

        if (extraUsageIsAvailable)
        {
            if (extraUsageUsedUsd is { } used && extraUsageLimitUsd is { } limit && limit > 0)
            {
                parts.Add(
                    $"${used.ToString("F2", CultureInfo.InvariantCulture)} / ${limit.ToString("F0", CultureInfo.InvariantCulture)} mo");
            }
            else
            {
                var mo = Math.Round(extraUsagePercentRaw);
                parts.Add($"mo {mo.ToString(CultureInfo.InvariantCulture)}%");
            }
        }

        var statusMessage = extraUsageOutOfCredits ? "Monthly spend limit reached" : null;

        return new ClaudeProSnapshot
        {
            IsAvailable = true,
            SessionPercentUsed = session,
            WeeklyPercentUsed = weekly,
            SessionResetsAt = sessionResetsAt,
            WeeklyResetsAt = weeklyResetsAt,
            ExtraUsageIsAvailable = extraUsageIsAvailable,
            ExtraUsagePercentRaw = extraUsagePercentRaw,
            ExtraUsageUsedUsd = extraUsageUsedUsd,
            ExtraUsageLimitUsd = extraUsageLimitUsd,
            ExtraUsageOutOfCredits = extraUsageOutOfCredits,
            ExtraUsageDisabledUntil = extraUsageDisabledUntil,
            ExtraUsageResetsAt = extraUsageResetsAt,
            StatusMessage = statusMessage,
            DetailLabel = string.Join(" · ", parts)
        };
    }

    public ClaudeProSnapshot WithMergedExtraUsage(
        bool isAvailable,
        double percentRaw,
        decimal? usedUsd,
        decimal? limitUsd,
        bool outOfCredits,
        DateTimeOffset? disabledUntil,
        DateTimeOffset? resetsAt)
    {
        if (!isAvailable && !ExtraUsageIsAvailable)
            return this;

        return FromUsage(
            SessionPercentUsed,
            WeeklyPercentUsed,
            SessionResetsAt,
            WeeklyResetsAt,
            isAvailable || ExtraUsageIsAvailable,
            percentRaw,
            usedUsd ?? ExtraUsageUsedUsd,
            limitUsd ?? ExtraUsageLimitUsd,
            outOfCredits || ExtraUsageOutOfCredits,
            disabledUntil ?? ExtraUsageDisabledUntil,
            resetsAt ?? ExtraUsageResetsAt);
    }
}

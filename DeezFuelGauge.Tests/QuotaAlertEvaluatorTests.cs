using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class QuotaAlertEvaluatorTests
{
    private static readonly WidgetSettings DefaultSettings = new();

    [Fact]
    public void Evaluate_cursor_plan_alerts_when_usage_low_and_period_ending_soon()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 50,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Single(alerts);
        Assert.Equal("cursor-plan", alerts[0].SourceId);
        Assert.Equal("Cursor plan", alerts[0].Label);
        Assert.Contains("50% used", alerts[0].Message);
        Assert.Contains("3 days", alerts[0].Message);
    }

    [Fact]
    public void Evaluate_cursor_plan_skips_when_usage_above_threshold()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 90,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_cursor_plan_skips_when_outside_alert_window()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-10);
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 50,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_respects_source_disabled_in_settings()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var settings = new WidgetSettings
        {
            QuotaAlerts = new QuotaAlertSettings { CursorPlan = false }
        };
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 50,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_skips_direct_api_when_budget_not_set()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings
            {
                ShowDirectSource = true,
                MonthlyBudgetUsd = 0
            }
        };
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 10,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds(),
            OpenAiDirect = DirectProviderSnapshot.FromBilling(5, 100, 0, 0)
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.DoesNotContain(alerts, a => a.SourceId == "openai-platform");
    }

    [Fact]
    public void Evaluate_openai_platform_alerts_when_budget_configured()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-2);
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings
            {
                ShowDirectSource = true,
                MonthlyBudgetUsd = 100
            }
        };
        var snapshot = new UsageSnapshot
        {
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds(),
            OpenAiDirect = DirectProviderSnapshot.FromBilling(20, 100, 0, 0)
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.Contains(alerts, a => a.SourceId == "openai-platform");
    }

    [Fact]
    public void Evaluate_opencode_go_uses_resets_at_for_period_end()
    {
        var resetsAt = new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        var now = resetsAt.AddDays(-2);
        var settings = new WidgetSettings
        {
            QuotaAlerts = new QuotaAlertSettings { CursorPlan = false },
            OpenCode = new ProviderBillingSettings { ShowProLimits = true }
        };
        var snapshot = new UsageSnapshot
        {
            OpenCode = OpenCodeSnapshot.FromData(
                null,
                null,
                null,
                null,
                null,
                OpenCodeWindowSnapshot.FromUsage(30, resetsAt),
                hasGoSubscription: true)
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        var alert = Assert.Single(alerts);
        Assert.Equal("opencode-go-monthly", alert.SourceId);
        Assert.Equal(2, alert.DaysRemaining);
    }

    [Fact]
    public void Evaluate_claude_extra_usage_monthly_alerts_near_calendar_month_end()
    {
        var now = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var monthEnd = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var settings = new WidgetSettings
        {
            QuotaAlerts = new QuotaAlertSettings { CursorPlan = false },
            Claude = new ProviderBillingSettings { ShowProLimits = true }
        };
        var snapshot = new UsageSnapshot
        {
            ClaudePro = ClaudeProSnapshot.FromUsage(
                10,
                20,
                null,
                null,
                extraUsageIsAvailable: true,
                extraUsagePercentRaw: 50,
                extraUsageUsedUsd: 10m,
                extraUsageLimitUsd: 20m,
                extraUsageResetsAt: monthEnd)
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        var alert = Assert.Single(alerts);
        Assert.Equal("claude-extra-usage-monthly", alert.SourceId);
        Assert.Equal("Claude monthly spend", alert.Label);
    }

    [Fact]
    public void Evaluate_uses_utc_month_fallback_when_billing_cycle_missing()
    {
        var now = new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new UsageSnapshot { PercentUsed = 40 };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Single(alerts);
        Assert.Equal("cursor-plan", alerts[0].SourceId);
    }

    [Fact]
    public void Evaluate_returns_empty_when_master_toggle_disabled()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-1);
        var settings = new WidgetSettings
        {
            QuotaAlerts = new QuotaAlertSettings { Enabled = false }
        };
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 10,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_skips_error_snapshot()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-1);
        var snapshot = UsageSnapshot.Error("failed");

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_fal_balance_alerts_when_percent_at_or_above_threshold()
    {
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            Fal = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CreditBaselineUsd = 12,
                LastObservedBalanceUsd = 12
            },
            QuotaAlerts = new QuotaAlertSettings { MaxPercentUsed = 75, FalBalance = true }
        };
        var snapshot = new UsageSnapshot
        {
            Fal = FalSnapshot.FromBalance(
                3,
                percentUsed: PrepaidCreditBaselineTracker.Update(settings.Fal, 3))
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings);

        Assert.Single(alerts);
        Assert.Equal("fal-balance", alerts[0].SourceId);
        Assert.Equal("fal", alerts[0].ProviderKey);
        Assert.Contains("$3.00 left", alerts[0].Message);
    }

    [Fact]
    public void Evaluate_xai_balance_alerts_when_percent_at_or_above_threshold()
    {
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            Xai = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CreditBaselineUsd = 12,
                LastObservedBalanceUsd = 12
            },
            QuotaAlerts = new QuotaAlertSettings { MaxPercentUsed = 75, XaiBalance = true }
        };
        var snapshot = new UsageSnapshot
        {
            Xai = XaiSnapshot.FromBalance(
                3,
                percentUsed: PrepaidCreditBaselineTracker.Update(settings.Xai, 3))
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings);

        Assert.Single(alerts);
        Assert.Equal("xai-balance", alerts[0].SourceId);
        Assert.Equal("xai", alerts[0].ProviderKey);
        Assert.Contains("$3.00 left", alerts[0].Message);
    }

    [Fact]
    public void Evaluate_fal_balance_skips_when_balance_healthy()
    {
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            Fal = new ProviderBillingSettings { ShowProLimits = true },
            QuotaAlerts = new QuotaAlertSettings { MaxPercentUsed = 75, FalBalance = true }
        };
        var snapshot = new UsageSnapshot
        {
            Fal = FalSnapshot.FromBalance(
                50,
                percentUsed: PrepaidCreditBaselineTracker.Update(settings.Fal, 50))
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings);

        Assert.Empty(alerts);
    }

    [Theory]
    [InlineData(7, 3, true)]
    [InlineData(7, 8, false)]
    [InlineData(7, 0, false)]
    public void IsWithinAlertWindow_respects_days_before_end(int daysBefore, int daysUntilEnd, bool expected)
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-daysUntilEnd);

        var result = QuotaAlertEvaluator.IsWithinAlertWindow(periodEnd, now, daysBefore);

        Assert.Equal(expected, result);
    }
}

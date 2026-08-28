using System.Globalization;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class QuotaAlertEvaluator
{
    public static IReadOnlyList<QuotaAlert> Evaluate(
        UsageSnapshot snapshot,
        WidgetSettings settings,
        DateTimeOffset? now = null)
    {
        var alerts = settings.QuotaAlerts;
        if (!alerts.Enabled || snapshot.IsError)
            return Array.Empty<QuotaAlert>();

        var evaluatedAt = now ?? DateTimeOffset.UtcNow;
        var results = new List<QuotaAlert>();
        var cursorCycleEnd = BillingPeriodHelper.ResolveCursorCycleEnd(snapshot.BillingCycleEndMs, evaluatedAt);
        var utcMonthEnd = BillingPeriodHelper.CurrentCalendarMonthEndUtc(evaluatedAt);

        TryAdd(
            results,
            alerts.CursorPlan,
            settings.Cursor.ShowCursorSource,
            snapshot.PercentUsed,
            cursorCycleEnd,
            evaluatedAt,
            alerts,
            "cursor-plan",
            "cursor",
            "Cursor plan");

        TryAdd(
            results,
            alerts.OpenAiCursor,
            settings.OpenAi.ShowCursorSource && snapshot.OpenAi.IsAvailable,
            snapshot.OpenAi.PercentUsed,
            cursorCycleEnd,
            evaluatedAt,
            alerts,
            "openai-cursor",
            "cursor",
            "OpenAI (Cursor plan)");

        TryAdd(
            results,
            alerts.OpenAiPlatform,
            settings.OpenAi.ShowDirectSource
            && snapshot.OpenAiDirect.IsAvailable
            && settings.OpenAi.MonthlyBudgetUsd is > 0,
            snapshot.OpenAiDirect.PercentUsed,
            cursorCycleEnd,
            evaluatedAt,
            alerts,
            "openai-platform",
            "openai",
            "OpenAI API");

        TryAdd(
            results,
            alerts.ClaudeCursor,
            settings.Claude.ShowCursorSource && snapshot.Claude.IsAvailable,
            snapshot.Claude.PercentUsed,
            cursorCycleEnd,
            evaluatedAt,
            alerts,
            "claude-cursor",
            "cursor",
            "Claude (Cursor plan)");

        TryAdd(
            results,
            alerts.ClaudeApi,
            settings.Claude.ShowApiConsoleBilling
            && snapshot.ClaudeDirect.IsAvailable
            && settings.Claude.MonthlyBudgetUsd is > 0,
            snapshot.ClaudeDirect.PercentUsed,
            cursorCycleEnd,
            evaluatedAt,
            alerts,
            "claude-api",
            "claude",
            "Claude API");

        TryAdd(
            results,
            alerts.GeminiCursor,
            settings.Gemini.ShowCursorSource && snapshot.Gemini.IsAvailable,
            snapshot.Gemini.PercentUsed,
            cursorCycleEnd,
            evaluatedAt,
            alerts,
            "gemini-cursor",
            "cursor",
            "Gemini (Cursor plan)");

        if (alerts.OpenRouterKeyLimit
            && settings.OpenRouter.ShowProLimits
            && snapshot.OpenRouter.IsAvailable
            && snapshot.OpenRouter.KeyLimitPercentUsed is { } keyLimitPercent)
        {
            TryAdd(
                results,
                true,
                true,
                keyLimitPercent,
                utcMonthEnd,
                evaluatedAt,
                alerts,
                "openrouter-key",
                "openrouter",
                "OpenRouter key limit");
        }

        if (alerts.OpenCodeZenMonthly
            && settings.OpenCode.ShowDirectSource
            && snapshot.OpenCode.ZenIsAvailable
            && snapshot.OpenCode.ZenMonthlyPercentUsed is { } zenMonthlyPercent
            && snapshot.OpenCode.ZenMonthlyCapUsd is > 0)
        {
            TryAdd(
                results,
                true,
                true,
                zenMonthlyPercent,
                utcMonthEnd,
                evaluatedAt,
                alerts,
                "opencode-zen-monthly",
                "opencode",
                "OpenCode Zen monthly");
        }

        if (alerts.OpenCodeGoMonthly
            && settings.OpenCode.ShowProLimits
            && snapshot.OpenCode.HasGoSubscription
            && snapshot.OpenCode.GoMonthly.IsAvailable)
        {
            var goMonthlyEnd = snapshot.OpenCode.GoMonthly.ResetsAt ?? utcMonthEnd;
            TryAdd(
                results,
                true,
                true,
                snapshot.OpenCode.GoMonthly.PercentUsed,
                goMonthlyEnd,
                evaluatedAt,
                alerts,
                "opencode-go-monthly",
                "opencode",
                "OpenCode Go monthly");
        }

        if (alerts.ClaudeExtraUsageMonthly
            && settings.Claude.ShowProLimits
            && snapshot.ClaudePro.IsAvailable
            && snapshot.ClaudePro.ExtraUsageIsAvailable)
        {
            var claudeMonthlyEnd = snapshot.ClaudePro.ExtraUsageResetsAt ?? utcMonthEnd;
            TryAdd(
                results,
                true,
                true,
                snapshot.ClaudePro.ExtraUsagePercentUsed,
                claudeMonthlyEnd,
                evaluatedAt,
                alerts,
                "claude-extra-usage-monthly",
                "claude",
                "Claude monthly spend");
        }

        if (alerts.FalBalance
            && settings.Fal.ShowProLimits
            && snapshot.Fal.IsAvailable
            && snapshot.Fal.HeadlinePercentUsed >= alerts.MaxPercentUsed)
        {
            var balanceLabel = snapshot.Fal.DetailLabel;
            var percentLabel = Math.Round(snapshot.Fal.HeadlinePercentUsed).ToString(CultureInfo.InvariantCulture);
            results.Add(new QuotaAlert(
                "fal-balance",
                "fal",
                "fal.ai credits",
                snapshot.Fal.HeadlinePercentUsed,
                0,
                $"fal.ai: {balanceLabel} · {percentLabel}%"));
        }

        if (alerts.XaiBalance
            && settings.Xai.ShowProLimits
            && snapshot.Xai.IsAvailable
            && snapshot.Xai.HeadlinePercentUsed >= alerts.MaxPercentUsed)
        {
            var balanceLabel = snapshot.Xai.DetailLabel;
            var percentLabel = Math.Round(snapshot.Xai.HeadlinePercentUsed).ToString(CultureInfo.InvariantCulture);
            results.Add(new QuotaAlert(
                "xai-balance",
                "xai",
                "xAI credits",
                snapshot.Xai.HeadlinePercentUsed,
                0,
                $"xAI: {balanceLabel} · {percentLabel}%"));
        }

        if (alerts.GrokBotWeekly
            && settings.GrokBot.ShowProLimits
            && snapshot.GrokBot.IsAvailable
            && snapshot.GrokBot.ResetsAt is { } grokReset)
        {
            TryAdd(
                results,
                true,
                true,
                snapshot.GrokBot.PercentUsed,
                grokReset,
                evaluatedAt,
                alerts,
                "grokbot-weekly",
                "grokbot",
                "Grok Bot");
        }

        return results;
    }

    private static void TryAdd(
        List<QuotaAlert> results,
        bool sourceEnabled,
        bool monitorActive,
        double percentUsed,
        DateTimeOffset periodEnd,
        DateTimeOffset evaluatedAt,
        QuotaAlertSettings alerts,
        string sourceId,
        string providerKey,
        string label)
    {
        if (!sourceEnabled || !monitorActive)
            return;

        if (!IsWithinAlertWindow(periodEnd, evaluatedAt, alerts.DaysBeforePeriodEnd))
            return;

        if (percentUsed >= alerts.MaxPercentUsed)
            return;

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((periodEnd - evaluatedAt).TotalDays));
        var percentLabel = Math.Round(percentUsed).ToString(CultureInfo.InvariantCulture);
        var dayLabel = daysRemaining == 1 ? "1 day" : $"{daysRemaining.ToString(CultureInfo.InvariantCulture)} days";
        var message = $"{label}: {percentLabel}% used · resets in {dayLabel}";

        results.Add(new QuotaAlert(
            sourceId,
            providerKey,
            label,
            percentUsed,
            daysRemaining,
            message));
    }

    internal static bool IsWithinAlertWindow(
        DateTimeOffset periodEnd,
        DateTimeOffset evaluatedAt,
        int daysBeforePeriodEnd)
    {
        if (daysBeforePeriodEnd <= 0)
            return evaluatedAt < periodEnd;

        var windowStart = periodEnd.AddDays(-daysBeforePeriodEnd);
        return evaluatedAt >= windowStart && evaluatedAt < periodEnd;
    }
}

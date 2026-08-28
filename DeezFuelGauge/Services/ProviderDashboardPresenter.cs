using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class ProviderDashboardPresenter
{
    public static bool IsCursorDashboardVisible(WidgetSettings settings) =>
        settings.Cursor.ShowCursorSource
        || settings.OpenAi.ShowCursorSource
        || settings.Claude.ShowCursorSource
        || settings.Gemini.ShowCursorSource
        || settings.GrokBot.ShowProLimits;

    public static bool IsOpenAiDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowDirectSource || settings.ShowProLimits;

    public static bool IsClaudeDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowProLimits || settings.ShowApiConsoleBilling;

    public static bool IsGeminiDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowProLimits;

    public static bool IsOpenRouterDashboardVisible(ProviderBillingSettings settings) =>
        ProviderFeatureFlags.OpenRouterEnabled && settings.ShowProLimits;

    public static bool IsOpenCodeDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowDirectSource || settings.ShowProLimits;

    public static bool IsFalDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowProLimits;

    public static bool IsXaiDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowProLimits;

    public static double ComputeCursorHeadline(UsageSnapshot snapshot, WidgetSettings settings)
    {
        // Cursor plan usage has separate Auto and API pools. When breakdown is on, surface the
        // hotter pool on the collapsed bar so Auto exhaustion is not hidden behind a low total %.
        if (settings.Cursor.ShowCursorSource)
        {
            var values = new List<double> { snapshot.PercentUsed };
            if (settings.ShowBreakdown && snapshot.HasBreakdown)
            {
                if (snapshot.AutoPercentUsed is { } auto)
                    values.Add(auto);
                if (snapshot.ApiPercentUsed is { } api)
                    values.Add(api);
            }

            if (settings.ShowBreakdown
                && settings.GrokBot.ShowProLimits
                && snapshot.GrokBot.IsAvailable)
            {
                values.Add(snapshot.GrokBot.PercentUsed);
            }

            return values.Max();
        }

        // Fallback when only per-model Cursor sources / Grok Bot are enabled.
        var valuesFallback = new List<double>();
        if (settings.ShowBreakdown
            && settings.GrokBot.ShowProLimits
            && snapshot.GrokBot.IsAvailable)
        {
            valuesFallback.Add(snapshot.GrokBot.PercentUsed);
        }
        if (settings.OpenAi.ShowCursorSource && snapshot.OpenAi.IsAvailable)
            valuesFallback.Add(snapshot.OpenAi.PercentUsed);
        if (settings.Claude.ShowCursorSource && snapshot.Claude.IsAvailable)
            valuesFallback.Add(snapshot.Claude.PercentUsed);
        if (settings.Gemini.ShowCursorSource && snapshot.Gemini.IsAvailable)
            valuesFallback.Add(snapshot.Gemini.PercentUsed);

        return valuesFallback.Count > 0 ? valuesFallback.Max() : 0;
    }

    public static double ComputeOpenAiHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (HasFailedEnabledApiSource(settings.ShowDirectSource, snapshot.OpenAiDirect.IsAvailable))
            return 0;

        var values = new List<double>();

        if (settings.ShowDirectSource && snapshot.OpenAiDirect.IsAvailable)
            values.Add(snapshot.OpenAiDirect.PercentUsed);

        if (settings.ShowProLimits && snapshot.Codex.IsAvailable)
            values.Add(ProviderLimitsPresenter.HeadlinePercent(snapshot.Codex.SessionPercentUsed, snapshot.Codex.WeeklyPercentUsed));

        return values.Count > 0 ? values.Max() : 0;
    }

    public static double ComputeClaudeHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (HasFailedEnabledApiSource(settings.ShowApiConsoleBilling, snapshot.ClaudeDirect.IsAvailable))
            return 0;

        var values = new List<double>();

        if (settings.ShowProLimits && snapshot.ClaudePro.IsAvailable)
            values.Add(ProviderLimitsPresenter.ComputeClaudeProHeadline(snapshot.ClaudePro));

        if (settings.ShowApiConsoleBilling && snapshot.ClaudeDirect.IsAvailable)
            values.Add(snapshot.ClaudeDirect.PercentUsed);

        return values.Count > 0 ? values.Max() : 0;
    }

    public static double ComputeGeminiHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (!settings.ShowProLimits || !snapshot.Antigravity.IsAvailable)
            return 0;

        return ProviderLimitsPresenter.AntigravityHeadlinePercent(snapshot.Antigravity);
    }

    public static double ComputeOpenRouterHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (!settings.ShowProLimits || !snapshot.OpenRouter.IsAvailable)
            return 0;

        return snapshot.OpenRouter.HeadlinePercentUsed;
    }

    public static double ComputeOpenCodeHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        var values = new List<double>();

        if (settings.ShowDirectSource && snapshot.OpenCode.ZenIsAvailable)
        {
            if (snapshot.OpenCode.ZenMonthlyPercentUsed is { } zenMonthly)
                values.Add(zenMonthly);
            else if (snapshot.OpenCode.ZenBalanceUsd is { } balance)
            {
                if (balance <= 1)
                    values.Add(95);
                else if (balance <= 5)
                    values.Add(75);
                else if (balance <= 10)
                    values.Add(50);
            }
        }

        if (settings.ShowProLimits && snapshot.OpenCode.HasGoSubscription)
        {
            values.Add(ProviderLimitsPresenter.HeadlinePercent3(
                snapshot.OpenCode.GoRolling.PercentUsed,
                snapshot.OpenCode.GoWeekly.PercentUsed,
                snapshot.OpenCode.GoMonthly.PercentUsed));
        }

        return values.Count > 0 ? values.Max() : 0;
    }

    public static double ComputeFalHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (!settings.ShowProLimits || !snapshot.Fal.IsAvailable)
            return 0;

        return snapshot.Fal.HeadlinePercentUsed;
    }

    public static double ComputeXaiHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (!settings.ShowProLimits || !snapshot.Xai.IsAvailable)
            return 0;

        return snapshot.Xai.HeadlinePercentUsed;
    }

    public static double ComputeGrokBotHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (!settings.ShowProLimits || !snapshot.GrokBot.IsAvailable)
            return 0;

        return snapshot.GrokBot.PercentUsed;
    }

    public static bool IsCursorHeadlineConnected(UsageSnapshot snapshot, WidgetSettings settings) =>
        (!snapshot.IsError && settings.Cursor.ShowCursorSource)
        || (settings.OpenAi.ShowCursorSource && snapshot.OpenAi.IsAvailable)
        || (settings.Claude.ShowCursorSource && snapshot.Claude.IsAvailable)
        || (settings.Gemini.ShowCursorSource && snapshot.Gemini.IsAvailable)
        || (settings.GrokBot.ShowProLimits && snapshot.GrokBot.IsAvailable);

    public static bool IsOpenAiHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (HasFailedEnabledApiSource(settings.ShowDirectSource, snapshot.OpenAiDirect.IsAvailable))
            return false;

        return (settings.ShowDirectSource && snapshot.OpenAiDirect.IsAvailable)
               || (settings.ShowProLimits && snapshot.Codex.IsAvailable);
    }

    public static bool IsClaudeHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (HasFailedEnabledApiSource(settings.ShowApiConsoleBilling, snapshot.ClaudeDirect.IsAvailable))
            return false;

        return (settings.ShowProLimits && snapshot.ClaudePro.IsAvailable)
               || (settings.ShowApiConsoleBilling && snapshot.ClaudeDirect.IsAvailable);
    }

    public static bool IsGeminiHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings) =>
        settings.ShowProLimits && snapshot.Antigravity.IsAvailable;

    public static bool IsOpenRouterHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings) =>
        ProviderFeatureFlags.OpenRouterEnabled && settings.ShowProLimits && snapshot.OpenRouter.IsAvailable;

    public static bool IsOpenCodeHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings) =>
        (settings.ShowDirectSource && snapshot.OpenCode.ZenIsAvailable)
        || (settings.ShowProLimits && snapshot.OpenCode.HasGoSubscription);

    public static bool IsFalHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings) =>
        settings.ShowProLimits && snapshot.Fal.IsAvailable;

    public static bool IsXaiHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings) =>
        settings.ShowProLimits && snapshot.Xai.IsAvailable;

    public static bool IsGrokBotHeadlineConnected(UsageSnapshot snapshot, ProviderBillingSettings settings) =>
        settings.ShowProLimits && snapshot.GrokBot.IsAvailable;

    private static bool HasFailedEnabledApiSource(bool enabled, bool available) =>
        enabled && !available;
}

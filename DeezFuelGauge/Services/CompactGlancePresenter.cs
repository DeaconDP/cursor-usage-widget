using System.Globalization;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed record CompactGlance(
    IReadOnlyList<CompactGlanceRow> Rows,
    bool IsError,
    string? ErrorMessage);

public sealed record CompactGlanceRow(
    string Abbrev,
    string Text,
    double? PercentUsed,
    bool HasAlert,
    bool IsConnected);

public static class CompactGlancePresenter
{
    public static CompactGlance FromSnapshot(UsageSnapshot? snapshot, WidgetSettings settings)
    {
        if (snapshot is null)
            return new([Row("", "...", null, false, false)], false, null);

        if (snapshot.IsError)
            return new(
                [Row("", snapshot.ErrorMessage ?? "Error", null, false, false)],
                true,
                snapshot.ErrorMessage);

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings);
        var rows = new List<CompactGlanceRow>();

        AddCursorRows(rows, snapshot, settings, alerts);
        AddConnected(
            rows,
            settings.OpenAi.ShowCursorSource,
            "CO",
            snapshot.OpenAi.IsAvailable,
            snapshot.OpenAi.PercentUsed,
            HasAlert(alerts, "openai-cursor"));
        AddConnected(
            rows,
            settings.Claude.ShowCursorSource,
            "CC",
            snapshot.Claude.IsAvailable,
            snapshot.Claude.PercentUsed,
            HasAlert(alerts, "claude-cursor"));
        AddConnected(
            rows,
            settings.Gemini.ShowCursorSource,
            "CG",
            snapshot.Gemini.IsAvailable,
            snapshot.Gemini.PercentUsed,
            HasAlert(alerts, "gemini-cursor"));
        AddConnected(
            rows,
            settings.OpenAi.ShowProLimits,
            "CX",
            snapshot.Codex.IsAvailable,
            snapshot.Codex.IsAvailable
                ? ProviderLimitsPresenter.HeadlinePercent(
                    snapshot.Codex.SessionPercentUsed,
                    snapshot.Codex.WeeklyPercentUsed)
                : null,
            hasAlert: false);
        AddConnected(
            rows,
            settings.OpenAi.ShowDirectSource,
            "OA",
            snapshot.OpenAiDirect.IsAvailable,
            snapshot.OpenAiDirect.PercentUsed,
            HasAlert(alerts, "openai-platform"));
        AddConnected(
            rows,
            settings.Claude.ShowProLimits,
            "CL",
            snapshot.ClaudePro.IsAvailable,
            snapshot.ClaudePro.IsAvailable
                ? ProviderLimitsPresenter.ComputeClaudeProHeadline(snapshot.ClaudePro)
                : null,
            HasAlert(alerts, "claude-extra-usage-monthly"));
        AddConnected(
            rows,
            settings.Claude.ShowApiConsoleBilling,
            "AC",
            snapshot.ClaudeDirect.IsAvailable,
            snapshot.ClaudeDirect.PercentUsed,
            HasAlert(alerts, "claude-api"));
        AddConnected(
            rows,
            settings.Gemini.ShowProLimits,
            "GM",
            snapshot.Antigravity.IsAvailable,
            snapshot.Antigravity.IsAvailable
                ? ProviderLimitsPresenter.AntigravityHeadlinePercent(snapshot.Antigravity)
                : null,
            hasAlert: false);
        if (ProviderFeatureFlags.OpenRouterEnabled)
        {
            AddConnected(
                rows,
                settings.OpenRouter.ShowProLimits,
                "OR",
                snapshot.OpenRouter.IsAvailable,
                snapshot.OpenRouter.HeadlinePercentUsed,
                HasAlert(alerts, "openrouter-key"));
        }

        AddConnected(
            rows,
            settings.OpenCode.ShowDirectSource,
            "OZ",
            snapshot.OpenCode.ZenIsAvailable,
            TryZenPercent(snapshot.OpenCode),
            HasAlert(alerts, "opencode-zen-monthly"));
        AddConnected(
            rows,
            settings.OpenCode.ShowProLimits,
            "OG",
            snapshot.OpenCode.HasGoSubscription,
            snapshot.OpenCode.HasGoSubscription
                ? ProviderLimitsPresenter.HeadlinePercent3(
                    snapshot.OpenCode.GoRolling.PercentUsed,
                    snapshot.OpenCode.GoWeekly.PercentUsed,
                    snapshot.OpenCode.GoMonthly.PercentUsed)
                : null,
            HasAlert(alerts, "opencode-go-monthly"));
        AddConnected(
            rows,
            settings.Fal.ShowProLimits,
            "FA",
            snapshot.Fal.IsAvailable,
            snapshot.Fal.IsAvailable ? snapshot.Fal.HeadlinePercentUsed : null,
            HasAlert(alerts, "fal-balance"));
        AddConnected(
            rows,
            settings.Xai.ShowProLimits,
            "XA",
            snapshot.Xai.IsAvailable,
            snapshot.Xai.IsAvailable ? snapshot.Xai.HeadlinePercentUsed : null,
            HasAlert(alerts, "xai-balance"));
        AddConnected(
            rows,
            settings.GrokBot.ShowProLimits,
            "GB",
            snapshot.GrokBot.IsAvailable,
            snapshot.GrokBot.PercentUsed,
            HasAlert(alerts, "grokbot-weekly"));

        if (rows.Count == 0)
            return new([Row("", "—", null, false, false)], false, null);

        return new(rows, false, null);
    }

    private static void AddCursorRows(
        List<CompactGlanceRow> rows,
        UsageSnapshot snapshot,
        WidgetSettings settings,
        IReadOnlyList<QuotaAlert> alerts)
    {
        if (!settings.Cursor.ShowCursorSource)
            return;

        var cursorAlert = HasAlert(alerts, "cursor-plan");
        if (settings.ShowBreakdown && snapshot.HasBreakdown)
        {
            if (snapshot.AutoPercentUsed is { } auto)
                AddConnected(rows, enabled: true, "CM", connected: true, auto, cursorAlert);
            if (snapshot.ApiPercentUsed is { } api)
                AddConnected(rows, enabled: true, "CA", connected: true, api, cursorAlert);
            if (snapshot.AutoPercentUsed is null && snapshot.ApiPercentUsed is null)
                AddConnected(rows, enabled: true, "C", connected: !snapshot.IsError, snapshot.PercentUsed, cursorAlert);
            return;
        }

        AddConnected(rows, enabled: true, "C", connected: !snapshot.IsError, snapshot.PercentUsed, cursorAlert);
    }

    private static void AddConnected(
        List<CompactGlanceRow> rows,
        bool enabled,
        string abbrev,
        bool connected,
        double? percentUsed,
        bool hasAlert)
    {
        if (!enabled)
            return;

        rows.Add(connected && percentUsed is { } percent
            ? ConnectedRow(abbrev, percent, hasAlert)
            : Row(abbrev, $"{abbrev} —", null, false, false));
    }

    private static CompactGlanceRow ConnectedRow(string abbrev, double percentUsed, bool hasAlert)
    {
        var rounded = Math.Round(percentUsed).ToString(CultureInfo.InvariantCulture);
        var text = $"{abbrev} {rounded}%{QuotaAlertPresenter.FormatHeadlineBadge(hasAlert)}";
        return Row(abbrev, text, percentUsed, hasAlert, true);
    }

    private static CompactGlanceRow Row(
        string abbrev,
        string text,
        double? percentUsed,
        bool hasAlert,
        bool isConnected) =>
        new(abbrev, text, percentUsed, hasAlert, isConnected);

    private static bool HasAlert(IReadOnlyList<QuotaAlert> alerts, string sourceId) =>
        alerts.Any(a => a.SourceId == sourceId);

    internal static double? TryZenPercent(OpenCodeSnapshot openCode)
    {
        if (!openCode.ZenIsAvailable)
            return null;

        if (openCode.ZenMonthlyPercentUsed is { } zenMonthly)
            return zenMonthly;

        if (openCode.ZenBalanceUsd is { } balance)
        {
            if (balance <= 1)
                return 95;
            if (balance <= 5)
                return 75;
            if (balance <= 10)
                return 50;
        }

        return 0;
    }
}

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class ProviderLimitsPresenter
{
    private static readonly Color ErrorColor = Color.FromRgb(0xFF, 0x98, 0x00);

    public static double HeadlinePercent(double sessionPercentUsed, double weeklyPercentUsed) =>
        Math.Max(sessionPercentUsed, weeklyPercentUsed);

    public static double HeadlinePercent3(double rollingPercentUsed, double weeklyPercentUsed, double monthlyPercentUsed) =>
        Math.Max(rollingPercentUsed, Math.Max(weeklyPercentUsed, monthlyPercentUsed));

    public static double AntigravityHeadlinePercent(AntigravitySnapshot snapshot)
    {
        var values = new List<double>();
        if (snapshot.Gemini.IsAvailable)
        {
            values.Add(snapshot.Gemini.SessionPercentUsed);
            values.Add(snapshot.Gemini.WeeklyPercentUsed);
        }

        if (snapshot.ThirdParty.IsAvailable)
        {
            values.Add(snapshot.ThirdParty.SessionPercentUsed);
            values.Add(snapshot.ThirdParty.WeeklyPercentUsed);
        }

        return values.Count > 0 ? values.Max() : 0;
    }

    public static string FormatSessionWeeklySummary(double sessionPercentUsed, double weeklyPercentUsed) =>
        FormatSessionWeeklySummary(sessionPercentUsed, weeklyPercentUsed, hasSessionWindow: true, hasWeeklyWindow: true);

    public static string FormatSessionWeeklySummary(
        double sessionPercentUsed,
        double weeklyPercentUsed,
        bool hasSessionWindow,
        bool hasWeeklyWindow)
    {
        var parts = new List<string>();
        if (hasSessionWindow)
        {
            var session = Math.Round(sessionPercentUsed);
            parts.Add($"{session.ToString(CultureInfo.InvariantCulture)}% 5-hour");
        }

        if (hasWeeklyWindow)
        {
            var weekly = Math.Round(weeklyPercentUsed);
            parts.Add($"{weekly.ToString(CultureInfo.InvariantCulture)}% weekly");
        }

        if (parts.Count == 0)
            return "No rate-limit windows";

        return parts.Count == 1
            ? $"{parts[0]} used"
            : $"{parts[0]} and {parts[1]} used";
    }

    public static string FormatThreeWindowSummary(double rollingPercentUsed, double weeklyPercentUsed, double monthlyPercentUsed)
    {
        var rolling = Math.Round(rollingPercentUsed);
        var weekly = Math.Round(weeklyPercentUsed);
        var monthly = Math.Round(monthlyPercentUsed);
        return $"{rolling.ToString(CultureInfo.InvariantCulture)}% 5h · {weekly.ToString(CultureInfo.InvariantCulture)}% wk · {monthly.ToString(CultureInfo.InvariantCulture)}% mo used";
    }

    public static string FormatAntigravitySummary(AntigravitySnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
            return snapshot.StatusMessage ?? snapshot.DetailLabel;

        var gemini5h = Math.Round(snapshot.Gemini.SessionPercentUsed);
        var geminiWeekly = Math.Round(snapshot.Gemini.WeeklyPercentUsed);
        var thirdParty5h = Math.Round(snapshot.ThirdParty.SessionPercentUsed);
        var thirdPartyWeekly = Math.Round(snapshot.ThirdParty.WeeklyPercentUsed);
        return $"{gemini5h.ToString(CultureInfo.InvariantCulture)}% Gemini 5h · {geminiWeekly.ToString(CultureInfo.InvariantCulture)}% Gemini wk · " +
               $"{thirdParty5h.ToString(CultureInfo.InvariantCulture)}% 3P 5h · {thirdPartyWeekly.ToString(CultureInfo.InvariantCulture)}% 3P wk used";
    }

    public static string FormatOpenCodeGoFooter(OpenCodeSnapshot openCode)
    {
        if (!openCode.HasGoSubscription)
            return "";

        // Reset times are shown inline beside each window bar when breakdown is visible.
        return "";
    }

    public static string FormatResetLabel(DateTimeOffset? resetsAt)
    {
        if (resetsAt is not { } reset)
            return "";

        return $"⟲ {FormatResetTime(reset)}";
    }

    public static string FormatResetTimes(DateTimeOffset? sessionResetsAt, DateTimeOffset? weeklyResetsAt)
    {
        var parts = new List<string>();
        if (sessionResetsAt is { } sessionReset)
            parts.Add($"5h resets {FormatResetTime(sessionReset)}");

        if (weeklyResetsAt is { } weeklyReset)
            parts.Add($"weekly resets {FormatResetTime(weeklyReset)}");

        return string.Join(" · ", parts);
    }

    public static string FormatOpenCodeGoResetTimes(OpenCodeSnapshot openCode)
    {
        if (!openCode.HasGoSubscription)
            return "";

        var parts = new List<string>();
        if (openCode.GoRolling.ResetsAt is { } rollingReset)
            parts.Add($"5h resets {FormatResetTime(rollingReset)}");
        if (openCode.GoWeekly.ResetsAt is { } weeklyReset)
            parts.Add($"weekly resets {FormatResetTime(weeklyReset)}");
        if (openCode.GoMonthly.ResetsAt is { } monthlyReset)
            parts.Add($"monthly resets {FormatResetTime(monthlyReset)}");

        return string.Join(" · ", parts);
    }

    public static string FormatCodexFooter(CodexSnapshot codex, bool includeResets = true)
    {
        if (!codex.IsAvailable)
            return "";

        // ponytail: plan/credits footer hidden for now; resets stay inline on bars when breakdown is shown
        if (!includeResets)
            return "";

        return FormatResetTimes(codex.SessionResetsAt, codex.WeeklyResetsAt);
    }

    public static double ComputeClaudeProHeadline(ClaudeProSnapshot pro)
    {
        if (pro.ExtraUsageIsAvailable)
        {
            return HeadlinePercent3(
                pro.SessionPercentUsed,
                pro.WeeklyPercentUsed,
                pro.ExtraUsagePercentRaw);
        }

        return HeadlinePercent(pro.SessionPercentUsed, pro.WeeklyPercentUsed);
    }

    public static string FormatClaudeProExtraUsagePercent(ClaudeProSnapshot pro)
    {
        if (!pro.ExtraUsageIsAvailable)
            return "—";

        var rounded = Math.Round(pro.ExtraUsagePercentRaw);
        return $"{rounded.ToString(CultureInfo.InvariantCulture)}%";
    }

    public static string FormatClaudeProFooter(ClaudeProSnapshot pro, bool includeResets = true)
    {
        if (!pro.IsAvailable)
            return "";

        if (!includeResets)
            return "";

        var parts = new List<string>();
        var sessionWeekly = FormatResetTimes(pro.SessionResetsAt, pro.WeeklyResetsAt);
        if (!string.IsNullOrEmpty(sessionWeekly))
            parts.Add(sessionWeekly);

        if (pro.ExtraUsageIsAvailable && pro.ExtraUsageResetsAt is { } monthlyReset)
            parts.Add($"monthly resets {FormatResetTime(monthlyReset)}");

        if (pro.ExtraUsageOutOfCredits)
            parts.Add("monthly spend limit reached");

        if (pro.ExtraUsageDisabledUntil is { } disabledUntil)
            parts.Add($"blocked until {FormatResetTime(disabledUntil)}");

        return string.Join(" · ", parts);
    }

    public static string FormatAntigravityFooter(AntigravitySnapshot snapshot, bool includeResets = true)
    {
        if (!snapshot.IsAvailable)
            return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(snapshot.PlanLabel))
            parts.Add(snapshot.PlanLabel);

        if (includeResets)
        {
            var resets = FormatAntigravityResetTimes(snapshot);
            if (!string.IsNullOrEmpty(resets))
                parts.Add(resets);
        }

        return string.Join(" · ", parts);
    }

    private static string FormatAntigravityResetTimes(AntigravitySnapshot snapshot)
    {
        var sessionResetsAt = FirstReset(snapshot.Gemini.SessionResetsAt, snapshot.ThirdParty.SessionResetsAt);
        var weeklyResetsAt = FirstReset(snapshot.Gemini.WeeklyResetsAt, snapshot.ThirdParty.WeeklyResetsAt);
        return FormatResetTimes(sessionResetsAt, weeklyResetsAt);
    }

    private static DateTimeOffset? FirstReset(DateTimeOffset? first, DateTimeOffset? second)
    {
        if (first is null)
            return second;
        if (second is null)
            return first;

        return first < second ? first : second;
    }

    private static string FormatResetTime(DateTimeOffset resetAt) =>
        resetAt.ToLocalTime().ToString("ddd d MMM HH:mm", CultureInfo.InvariantCulture);

    public static void ApplyHeadline(
        double headlinePercent,
        bool isAvailable,
        string? statusMessage,
        TextBlock percentText,
        Grid mainTrack,
        Border mainFill,
        ref double lastHeadlinePercent)
    {
        if (!isAvailable)
        {
            percentText.Text = statusMessage ?? "—";
            percentText.Foreground = new SolidColorBrush(ErrorColor);
            mainFill.Width = 0;
            mainFill.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            mainTrack.Opacity = 0.45;
            lastHeadlinePercent = 0;
            return;
        }

        mainTrack.Opacity = 1;
        lastHeadlinePercent = headlinePercent;
        var rounded = Math.Round(headlinePercent);
        percentText.Text = $"{rounded.ToString(CultureInfo.InvariantCulture)}% used";
        var accent = UsageBarColors.GetColorForPercent(headlinePercent);
        percentText.Foreground = new SolidColorBrush(accent);
        mainFill.Background = new SolidColorBrush(accent);
        ProviderBarPresenter.UpdateProgressWidth(mainTrack, mainFill, headlinePercent);
    }

    public static void ApplyBreakdownSubBar(
        Grid track,
        Border fill,
        TextBlock percentText,
        ref double lastPercent,
        double percentUsed,
        bool isAvailable)
    {
        if (!isAvailable)
        {
            lastPercent = 0;
            fill.Width = 0;
            fill.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            track.Opacity = 0.45;
            percentText.Text = "—";
            return;
        }

        track.Opacity = 1;
        lastPercent = percentUsed;
        var rounded = Math.Round(percentUsed);
        percentText.Text = $"{rounded.ToString(CultureInfo.InvariantCulture)}%";
        fill.Background = new SolidColorBrush(UsageBarColors.GetColorForPercent(percentUsed));
        ProviderBarPresenter.UpdateProgressWidth(track, fill, percentUsed);
    }

    public static void ApplyResetLabel(
        TextBlock resetText,
        DateTimeOffset? resetsAt,
        bool showDetails,
        double? resetProgressPercent = null)
    {
        var label = showDetails ? FormatResetLabel(resetsAt) : "";
        resetText.Text = label;
        resetText.IsVisible = !string.IsNullOrEmpty(label);
        if (string.IsNullOrEmpty(label))
            return;

        var color = resetProgressPercent is { } progress
            ? UsageBarColors.GetMutedColorForResetProgress(progress)
            : UsageBarColors.ResetLabelFallback;
        resetText.Foreground = UsageBarBrushes.GetBrush(color);
    }

    public static void ApplyBreakdownLayout(
        bool showProBreakdown,
        bool isAvailable,
        string footerText,
        bool showFooter,
        StackPanel breakdownSection,
        Border breakdownPanel,
        Border barBorder,
        TextBlock remainingText)
    {
        remainingText.Text = showFooter ? footerText : "";
        remainingText.IsVisible = showFooter && !string.IsNullOrEmpty(footerText);

        var showBreakdown = showProBreakdown && isAvailable;
        if (!showBreakdown)
        {
            breakdownSection.IsVisible = false;
            breakdownPanel.IsVisible = false;
            barBorder.IsVisible = true;
            return;
        }

        breakdownSection.IsVisible = true;
        breakdownPanel.IsVisible = true;
        barBorder.IsVisible = false;
    }
}

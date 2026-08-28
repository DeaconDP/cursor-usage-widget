using Avalonia;
using Avalonia.Controls;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;
namespace DeezFuelGauge.Tests;

public sealed class CompactGlancePresenterTests
{
    [Fact]
    public void FromSnapshot_uses_placeholder_when_snapshot_missing()
    {
        var glance = CompactGlancePresenter.FromSnapshot(null, DisabledAll());

        Assert.Equal("...", glance.Rows[0].Text);
        Assert.False(glance.IsError);
    }

    [Fact]
    public void FromSnapshot_uses_error_message_when_snapshot_failed()
    {
        var glance = CompactGlancePresenter.FromSnapshot(
            UsageSnapshot.Error("Cursor token missing"),
            DisabledAll());

        Assert.Equal("Cursor token missing", glance.Rows[0].Text);
        Assert.True(glance.IsError);
    }

    [Fact]
    public void FromSnapshot_lists_cursor_breakdown_and_grok_bot()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            AutoPercentUsed = 47,
            ApiPercentUsed = 41,
            GrokBot = GrokBotSnapshot.FromUsage(25, null, null, true, true)
        };
        var settings = DisabledAll();
        settings.Cursor.ShowCursorSource = true;
        settings.ShowBreakdown = true;
        settings.GrokBot.ShowProLimits = true;

        var glance = CompactGlancePresenter.FromSnapshot(snapshot, settings);

        Assert.Equal(["CM", "CA", "GB"], glance.Rows.Select(r => r.Abbrev).ToArray());
        Assert.Equal("CM 47%", glance.Rows[0].Text);
        Assert.Equal("CA 41%", glance.Rows[1].Text);
        Assert.Equal("GB 25%", glance.Rows[2].Text);
    }

    [Fact]
    public void FromSnapshot_uses_single_cursor_row_when_breakdown_off()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            AutoPercentUsed = 90,
            ApiPercentUsed = 10
        };
        var settings = DisabledAll();
        settings.Cursor.ShowCursorSource = true;
        settings.ShowBreakdown = false;

        var glance = CompactGlancePresenter.FromSnapshot(snapshot, settings);

        Assert.Single(glance.Rows);
        Assert.Equal("C", glance.Rows[0].Abbrev);
        Assert.Equal("C 40%", glance.Rows[0].Text);
    }

    [Fact]
    public void FromSnapshot_omits_disabled_codex_and_shows_unavailable_direct()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 12,
            Codex = CodexSnapshot.FromUsage("plus", 84, 10, null, null, null, false),
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = false, PercentUsed = 99 }
        };
        var settings = DisabledAll();
        settings.Cursor.ShowCursorSource = true;
        settings.OpenAi.ShowDirectSource = true;
        settings.OpenAi.ShowProLimits = false;

        var glance = CompactGlancePresenter.FromSnapshot(snapshot, settings);

        Assert.Equal(["C", "OA"], glance.Rows.Select(r => r.Abbrev).ToArray());
        Assert.Equal("C 12%", glance.Rows[0].Text);
        Assert.Equal("OA —", glance.Rows[1].Text);
        Assert.DoesNotContain(glance.Rows, r => r.Abbrev == "CX");
    }

    [Fact]
    public void FromSnapshot_shows_em_dash_when_nothing_is_enabled()
    {
        var glance = CompactGlancePresenter.FromSnapshot(new UsageSnapshot { PercentUsed = 40 }, DisabledAll());

        Assert.Single(glance.Rows);
        Assert.Equal("—", glance.Rows[0].Text);
    }

    private static WidgetSettings DisabledAll()
    {
        var settings = new WidgetSettings { ShowBreakdown = false, QuotaAlerts = { Enabled = false } };
        settings.Cursor.ShowCursorSource = false;
        settings.OpenAi.ShowCursorSource = false;
        settings.OpenAi.ShowDirectSource = false;
        settings.OpenAi.ShowProLimits = false;
        settings.Claude.ShowCursorSource = false;
        settings.Claude.ShowProLimits = false;
        settings.Claude.ShowApiConsoleBilling = false;
        settings.Gemini.ShowCursorSource = false;
        settings.Gemini.ShowProLimits = false;
        settings.OpenRouter.ShowProLimits = false;
        settings.OpenCode.ShowDirectSource = false;
        settings.OpenCode.ShowProLimits = false;
        settings.Fal.ShowProLimits = false;
        settings.Xai.ShowProLimits = false;
        settings.GrokBot.ShowProLimits = false;
        return settings;
    }
}

public sealed class CompactHoverControllerTests
{
    [Fact]
    public void ShouldShowFullLayout_true_when_compact_mode_is_off()
    {
        Assert.True(CompactHoverController.ShouldShowFullLayout(
            useCompactMode: false,
            pointerOver: false,
            settingsExpanded: false,
            contextMenuOpen: false,
            dragging: false,
            inputFocused: false));
    }

    [Fact]
    public void ShouldShowFullLayout_false_at_compact_rest()
    {
        Assert.False(CompactHoverController.ShouldShowFullLayout(
            useCompactMode: true,
            pointerOver: false,
            settingsExpanded: false,
            contextMenuOpen: false,
            dragging: false,
            inputFocused: false));
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void ShouldShowFullLayout_true_while_hover_settings_menu_drag_or_focus(
        bool pointerOver,
        bool settingsExpanded,
        bool contextMenuOpen,
        bool dragging,
        bool inputFocused)
    {
        Assert.True(CompactHoverController.ShouldShowFullLayout(
            useCompactMode: true,
            pointerOver,
            settingsExpanded,
            contextMenuOpen,
            dragging,
            inputFocused));
    }
}

public sealed class CompactLayoutAnimatorTests
{
    [Fact]
    public void Interpolate_t0_keeps_start_size()
    {
        var start = new CompactAnimSample(120, 40, 10, 20);
        var end = new CompactAnimSample(300, 260, 10, 20);

        var sample = CompactLayoutAnimator.Interpolate(start, end, 0, expanding: true, reduceMotion: false);

        Assert.Equal(120, sample.Width);
        Assert.Equal(40, sample.Height);
        Assert.Equal(10, sample.X);
        Assert.Equal(20, sample.Y);
    }

    [Fact]
    public void Interpolate_t1_reaches_end_size()
    {
        var start = new CompactAnimSample(120, 40, 10, 20);
        var end = new CompactAnimSample(300, 260, -20, -40);

        var sample = CompactLayoutAnimator.Interpolate(start, end, 1, expanding: true, reduceMotion: false);

        Assert.Equal(300, sample.Width);
        Assert.Equal(260, sample.Height);
        Assert.Equal(-20, sample.X);
        Assert.Equal(-40, sample.Y);
    }

    [Fact]
    public void Interpolate_reduced_motion_snaps_to_end()
    {
        var start = new CompactAnimSample(120, 40, 10, 20);
        var end = new CompactAnimSample(300, 260, 10, 20);

        var sample = CompactLayoutAnimator.Interpolate(start, end, 0.4, expanding: true, reduceMotion: true);

        Assert.Equal(300, sample.Width);
        Assert.Equal(260, sample.Height);
    }

    [Fact]
    public void FullOpacity_stays_zero_until_fade_start()
    {
        Assert.Equal(0, CompactLayoutAnimator.FullOpacity(0.15));
        Assert.Equal(0, CompactLayoutAnimator.CompactOpacity(1));
        Assert.Equal(1, CompactLayoutAnimator.FullOpacity(1));
        Assert.Equal(1, CompactLayoutAnimator.CompactOpacity(0));
    }

    [Fact]
    public void DurationFor_uses_revised_expand_and_collapse_durations()
    {
        Assert.Equal(220, CompactLayoutAnimator.DurationFor(0, 1).TotalMilliseconds);
        Assert.Equal(170, CompactLayoutAnimator.DurationFor(1, 0).TotalMilliseconds);
        Assert.Equal(110, CompactLayoutAnimator.DurationFor(0.5, 1).TotalMilliseconds);
    }

    [Fact]
    public void ApplyEase_uses_quad_curves()
    {
        Assert.Equal(0.75, CompactLayoutAnimator.ApplyEase(0.5, expanding: true));
        Assert.Equal(0.25, CompactLayoutAnimator.ApplyEase(0.5, expanding: false));
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(0.5, true, true)]
    [InlineData(1, false, true)]
    [InlineData(0.96, false, true)]
    [InlineData(0.04, true, false)]
    public void Layer_visibility_thresholds(double progress, bool renderCompact, bool renderFull)
    {
        Assert.Equal(renderCompact, CompactLayoutAnimator.ShouldRenderCompact(progress));
        Assert.Equal(renderFull, CompactLayoutAnimator.ShouldRenderFull(progress));
    }
}

public sealed class CompactInteractionTrackerTests
{
    [Fact]
    public void ShouldKeepFullLayoutForFocus_false_for_null_or_non_input()
    {
        Assert.False(CompactInteractionTracker.ShouldKeepFullLayoutForFocus(null));
        Assert.False(CompactInteractionTracker.ShouldKeepFullLayoutForFocus(new TextBlock()));
    }

    [Fact]
    public void ShouldKeepFullLayoutForFocus_true_for_visible_text_box()
    {
        var host = new Panel { IsVisible = true };
        var textBox = new TextBox();
        host.Children.Add(textBox);

        Assert.True(CompactInteractionTracker.ShouldKeepFullLayoutForFocus(textBox));
    }

    [Fact]
    public void ShouldKeepFullLayoutForFocus_false_for_hidden_text_box()
    {
        var host = new Panel { IsVisible = false };
        var textBox = new TextBox();
        host.Children.Add(textBox);

        Assert.False(CompactInteractionTracker.ShouldKeepFullLayoutForFocus(textBox));
    }

    [Theory]
    [InlineData(-1, 5, 10, 10, false)]
    [InlineData(0, 0, 10, 10, true)]
    [InlineData(10, 10, 10, 10, true)]
    [InlineData(11, 5, 10, 10, false)]
    public void IsPointerWithinBounds_respects_window_edges(
        double x,
        double y,
        double width,
        double height,
        bool expected)
    {
        Assert.Equal(expected, CompactInteractionTracker.IsPointerWithinBounds(new Point(x, y), new Size(width, height)));
    }
}
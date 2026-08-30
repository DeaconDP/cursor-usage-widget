using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ProviderDashboardPresenterTests
{
    [Fact]
    public void IsOpenAiDashboardVisible_true_when_direct_or_pro_enabled()
    {
        Assert.True(ProviderDashboardPresenter.IsOpenAiDashboardVisible(new ProviderBillingSettings { ShowCursorSource = false, ShowDirectSource = true }));
        Assert.True(ProviderDashboardPresenter.IsOpenAiDashboardVisible(new ProviderBillingSettings { ShowCursorSource = false, ShowProLimits = true }));
    }

    [Fact]
    public void IsOpenAiDashboardVisible_ignores_cursor_source()
    {
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = false,
            ShowProLimits = false
        };

        Assert.False(ProviderDashboardPresenter.IsOpenAiDashboardVisible(settings));
    }

    [Fact]
    public void IsClaudeDashboardVisible_ignores_cursor_source()
    {
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowProLimits = false,
            ShowApiConsoleBilling = false
        };

        Assert.False(ProviderDashboardPresenter.IsClaudeDashboardVisible(settings));
    }

    [Fact]
    public void IsGeminiDashboardVisible_ignores_cursor_source()
    {
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowProLimits = false
        };

        Assert.False(ProviderDashboardPresenter.IsGeminiDashboardVisible(settings));
    }

    [Fact]
    public void IsCursorDashboardVisible_true_when_any_cursor_source_enabled()
    {
        Assert.True(ProviderDashboardPresenter.IsCursorDashboardVisible(new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            OpenAi = new ProviderBillingSettings { ShowCursorSource = true },
            Claude = new ProviderBillingSettings { ShowCursorSource = false },
            Gemini = new ProviderBillingSettings { ShowCursorSource = false }
        }));
    }

    [Fact]
    public void IsCursorDashboardVisible_false_when_all_cursor_sources_disabled()
    {
        Assert.False(ProviderDashboardPresenter.IsCursorDashboardVisible(new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            OpenAi = new ProviderBillingSettings { ShowCursorSource = false },
            Claude = new ProviderBillingSettings { ShowCursorSource = false },
            Gemini = new ProviderBillingSettings { ShowCursorSource = false },
            GrokBot = new ProviderBillingSettings { ShowProLimits = false }
        }));
    }

    [Fact]
    public void ComputeCursorHeadline_uses_hottest_pool_when_auto_is_higher()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            AutoPercentUsed = 90,
            ApiPercentUsed = 10
        };
        var settings = new WidgetSettings { ShowBreakdown = true };

        Assert.Equal(90, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeCursorHeadline_uses_hottest_pool_when_api_is_higher()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            AutoPercentUsed = 10,
            ApiPercentUsed = 85
        };
        var settings = new WidgetSettings { ShowBreakdown = true };

        Assert.Equal(85, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeCursorHeadline_uses_main_percent_when_breakdown_disabled()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            AutoPercentUsed = 90,
            ApiPercentUsed = 10
        };
        var settings = new WidgetSettings { ShowBreakdown = false };

        Assert.Equal(40, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeCursorHeadline_ignores_per_model_when_cursor_source_enabled()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 55 },
            Claude = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 88 },
            Gemini = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 25 }
        };
        var settings = new WidgetSettings { ShowBreakdown = false };

        Assert.Equal(40, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeCursorHeadline_falls_back_to_per_model_max_when_cursor_source_disabled()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 55 },
            Claude = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 88 },
            Gemini = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 25 }
        };
        var settings = new WidgetSettings { ShowBreakdown = false };
        settings.Cursor.ShowCursorSource = false;

        Assert.Equal(88, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeCursorHeadline_excludes_disabled_per_model_cursor_readings()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            Claude = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 88 }
        };
        var settings = new WidgetSettings { ShowBreakdown = false };
        settings.Cursor.ShowCursorSource = false;
        settings.Claude.ShowCursorSource = false;

        Assert.Equal(0, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeOpenAiHeadline_uses_max_across_direct_and_pro_sources()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 30 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = true, PercentUsed = 75 },
            Codex = CodexSnapshot.FromUsage("plus", 84, 4, null, null, null, false)
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = true,
            ShowProLimits = true
        };

        Assert.Equal(84, ProviderDashboardPresenter.ComputeOpenAiHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeOpenAiHeadline_excludes_cursor_source()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 30 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = true, PercentUsed = 75 },
            Codex = CodexSnapshot.FromUsage("plus", 84, 4, null, null, null, false)
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = false,
            ShowProLimits = false
        };

        Assert.Equal(0, ProviderDashboardPresenter.ComputeOpenAiHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeClaudeHeadline_uses_max_across_pro_and_api_sources()
    {
        var snapshot = new UsageSnapshot
        {
            Claude = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 20 },
            ClaudePro = ClaudeProSnapshot.FromUsage(12, 88, null, null),
            ClaudeDirect = new DirectProviderSnapshot { IsAvailable = true, PercentUsed = 50 }
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowProLimits = true,
            ShowApiConsoleBilling = true
        };

        Assert.Equal(88, ProviderDashboardPresenter.ComputeClaudeHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeClaudeHeadline_includes_extra_usage_monthly_when_highest()
    {
        var snapshot = new UsageSnapshot
        {
            ClaudePro = ClaudeProSnapshot.FromUsage(
                12,
                40,
                null,
                null,
                extraUsageIsAvailable: true,
                extraUsagePercentRaw: 235,
                extraUsageUsedUsd: 46.93m,
                extraUsageLimitUsd: 20m,
                extraUsageResetsAt: DateTimeOffset.UtcNow.AddDays(5))
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.Equal(235, ProviderDashboardPresenter.ComputeClaudeHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeGeminiHeadline_uses_antigravity_only()
    {
        var gemini = AntigravityGroupSnapshot.FromUsage(80, 60, null, null);
        var thirdParty = AntigravityGroupSnapshot.FromUsage(50, 40, null, null);
        var snapshot = new UsageSnapshot
        {
            Gemini = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 25 },
            Antigravity = AntigravitySnapshot.FromGroups("Pro", gemini, thirdParty)
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowProLimits = true
        };

        Assert.Equal(60, ProviderDashboardPresenter.ComputeGeminiHeadline(snapshot, settings));
    }

    [Fact]
    public void IsOpenAiHeadlineConnected_true_when_direct_or_codex_available()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 0 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = true, PercentUsed = 0 },
            Codex = CodexSnapshot.Unavailable("No Codex")
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = true,
            ShowProLimits = true
        };

        Assert.True(ProviderDashboardPresenter.IsOpenAiHeadlineConnected(snapshot, settings));
    }

    [Fact]
    public void IsOpenAiHeadlineConnected_false_when_api_enabled_but_unavailable_even_if_codex_ok()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAiDirect = DirectProviderSnapshot.Unavailable("Admin key with api.usage.read required"),
            Codex = CodexSnapshot.FromUsage("plus", 99, 1, null, null, 0m, false)
        };
        var settings = new ProviderBillingSettings
        {
            ShowDirectSource = true,
            ShowProLimits = true
        };

        Assert.False(ProviderDashboardPresenter.IsOpenAiHeadlineConnected(snapshot, settings));
        Assert.Equal(0, ProviderDashboardPresenter.ComputeOpenAiHeadline(snapshot, settings));
    }

    [Fact]
    public void IsOpenAiHeadlineConnected_false_when_no_enabled_source_is_available()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = false, PercentUsed = 0 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = false, PercentUsed = 0 },
            Codex = CodexSnapshot.Unavailable("No Codex")
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = true,
            ShowProLimits = true
        };

        Assert.False(ProviderDashboardPresenter.IsOpenAiHeadlineConnected(snapshot, settings));
    }

    [Fact]
    public void IsClaudeHeadlineConnected_false_when_api_enabled_but_unavailable_even_if_pro_ok()
    {
        var snapshot = new UsageSnapshot
        {
            ClaudeDirect = DirectProviderSnapshot.Unavailable("Admin API key not set"),
            ClaudePro = ClaudeProSnapshot.FromUsage(90, 10, null, null)
        };
        var settings = new ProviderBillingSettings
        {
            ShowProLimits = true,
            ShowApiConsoleBilling = true
        };

        Assert.False(ProviderDashboardPresenter.IsClaudeHeadlineConnected(snapshot, settings));
        Assert.Equal(0, ProviderDashboardPresenter.ComputeClaudeHeadline(snapshot, settings));
    }

    [Fact]
    public void IsOpenRouterHeadlineConnected_false_when_unavailable_even_with_pro_limits_enabled()
    {
        var snapshot = new UsageSnapshot
        {
            OpenRouter = OpenRouterSnapshot.Unavailable("Not connected")
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.False(ProviderDashboardPresenter.IsOpenRouterHeadlineConnected(snapshot, settings));
    }

    [Fact]
    public void IsOpenRouterHeadlineConnected_false_when_openrouter_hidden()
    {
        var snapshot = new UsageSnapshot
        {
            OpenRouter = new OpenRouterSnapshot
            {
                IsAvailable = true,
                HeadlinePercentUsed = 0,
                DetailLabel = "Connected"
            }
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.False(ProviderDashboardPresenter.IsOpenRouterHeadlineConnected(snapshot, settings));
    }

    [Fact]
    public void IsFalHeadlineConnected_true_when_available()
    {
        var snapshot = new UsageSnapshot
        {
            Fal = FalSnapshot.FromBalance(24.5)
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.True(ProviderDashboardPresenter.IsFalHeadlineConnected(snapshot, settings));
        Assert.Equal(0, ProviderDashboardPresenter.ComputeFalHeadline(snapshot, settings));
    }

    [Fact]
    public void IsFalHeadlineConnected_false_when_unavailable()
    {
        var snapshot = new UsageSnapshot
        {
            Fal = FalSnapshot.Unavailable("Admin API key not set")
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.False(ProviderDashboardPresenter.IsFalHeadlineConnected(snapshot, settings));
        Assert.Equal(0, ProviderDashboardPresenter.ComputeFalHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeFalHeadline_uses_prepaid_baseline_percent()
    {
        var falSettings = new ProviderBillingSettings
        {
            ShowProLimits = true,
            CreditBaselineUsd = 12,
            LastObservedBalanceUsd = 12
        };
        var percent = PrepaidCreditBaselineTracker.Update(falSettings, 3);
        var snapshot = new UsageSnapshot
        {
            Fal = FalSnapshot.FromBalance(3, percentUsed: percent)
        };

        Assert.Equal(75, ProviderDashboardPresenter.ComputeFalHeadline(snapshot, falSettings));
    }

    [Fact]
    public void IsXaiHeadlineConnected_true_when_available()
    {
        var snapshot = new UsageSnapshot
        {
            Xai = XaiSnapshot.FromBalance(24.5)
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.True(ProviderDashboardPresenter.IsXaiHeadlineConnected(snapshot, settings));
        Assert.Equal(0, ProviderDashboardPresenter.ComputeXaiHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeXaiHeadline_uses_prepaid_baseline_percent()
    {
        var xaiSettings = new ProviderBillingSettings
        {
            ShowProLimits = true,
            CreditBaselineUsd = 12,
            LastObservedBalanceUsd = 12
        };
        var percent = PrepaidCreditBaselineTracker.Update(xaiSettings, 3);
        var snapshot = new UsageSnapshot
        {
            Xai = XaiSnapshot.FromBalance(3, percentUsed: percent)
        };

        Assert.Equal(75, ProviderDashboardPresenter.ComputeXaiHeadline(snapshot, xaiSettings));
    }

    [Fact]
    public void IsXaiDashboardVisible_follows_show_pro_limits()
    {
        Assert.True(ProviderDashboardPresenter.IsXaiDashboardVisible(new ProviderBillingSettings { ShowProLimits = true }));
        Assert.False(ProviderDashboardPresenter.IsXaiDashboardVisible(new ProviderBillingSettings { ShowProLimits = false }));
    }

    [Fact]
    public void IsFalDashboardVisible_follows_show_pro_limits()
    {
        Assert.True(ProviderDashboardPresenter.IsFalDashboardVisible(new ProviderBillingSettings { ShowProLimits = true }));
        Assert.False(ProviderDashboardPresenter.IsFalDashboardVisible(new ProviderBillingSettings { ShowProLimits = false }));
    }

    [Fact]
    public void ComputeGrokBotHeadline_uses_weekly_percent_when_available()
    {
        var snapshot = new UsageSnapshot
        {
            GrokBot = GrokBotSnapshot.FromUsage(23.5, null, null, true, true)
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.True(ProviderDashboardPresenter.IsGrokBotHeadlineConnected(snapshot, settings));
        Assert.Equal(23.5, ProviderDashboardPresenter.ComputeGrokBotHeadline(snapshot, settings));
    }

    [Fact]
    public void IsCursorDashboardVisible_when_only_grok_bot_enabled()
    {
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            OpenAi = new ProviderBillingSettings { ShowCursorSource = false },
            Claude = new ProviderBillingSettings { ShowCursorSource = false },
            Gemini = new ProviderBillingSettings { ShowCursorSource = false },
            GrokBot = new ProviderBillingSettings { ShowProLimits = true }
        };

        Assert.True(ProviderDashboardPresenter.IsCursorDashboardVisible(settings));
    }

    [Fact]
    public void ComputeCursorHeadline_includes_grok_bot_when_breakdown_enabled()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 10,
            AutoPercentUsed = 20,
            ApiPercentUsed = 15,
            GrokBot = GrokBotSnapshot.FromUsage(45, null, null, true, true)
        };
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = true },
            ShowBreakdown = true,
            GrokBot = new ProviderBillingSettings { ShowProLimits = true }
        };

        Assert.Equal(45, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }
}

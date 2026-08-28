using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class SettingsPanelViewModelTests
{
    [Fact]
    public void Load_and_Commit_round_trip_preserves_provider_toggles()
    {
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false, ShowDetails = false },
            OpenAi = new ProviderBillingSettings
            {
                ShowCursorSource = true,
                ShowDirectSource = true,
                ShowProLimits = false,
                ShowProBreakdown = false,
                OrganizationId = "org-1",
                MonthlyBudgetUsd = 100
            },
            Gemini = new ProviderBillingSettings
            {
                ShowProLimits = false,
                ShowProBreakdown = true
            },
            ShowBreakdown = false,
            ShowDiskDrives = false
        };

        var viewModel = CreateViewModel();
        viewModel.Load(settings);

        Assert.False(viewModel.ShowCursor);
        Assert.True(viewModel.ShowOpenAiDirect);
        Assert.False(viewModel.ShowCodexLimits);
        Assert.Equal("org-1", viewModel.OpenAiOrgId);
        Assert.Equal("100", viewModel.OpenAiBudget);
        Assert.False(viewModel.ShowAntigravityLimits);

        viewModel.ShowCursor = true;
        viewModel.ShowCodexLimits = true;
        viewModel.ShowAntigravityLimits = true;
        viewModel.OpenAiOrgId = "org-2";

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.True(committed.Cursor.ShowCursorSource);
        Assert.True(committed.OpenAi.ShowProLimits);
        Assert.True(committed.Gemini.ShowProLimits);
        Assert.Equal("org-2", committed.OpenAi.OrganizationId);
        Assert.Equal(100, committed.OpenAi.MonthlyBudgetUsd);
    }

    [Fact]
    public void Commit_persists_disabled_antigravity_limits()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings
        {
            Gemini = new ProviderBillingSettings { ShowProLimits = true }
        });

        viewModel.ShowAntigravityLimits = false;

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.False(committed.Gemini.ShowProLimits);
    }

    [Fact]
    public void HasGeminiAutoAuth_true_when_cli_credentials_present()
    {
        var oauthPath = Path.Combine(Path.GetTempPath(), $".gemini-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(oauthPath);
        var credsPath = Path.Combine(oauthPath, "oauth_creds.json");
        File.WriteAllText(credsPath, """{"access_token":"cli-token","refresh_token":"refresh"}""");

        try
        {
            var viewModel = new SettingsPanelViewModel(
                new ProviderEasySetupService(),
                new OpenAiBillingClient(),
                new CodexUsageClient(),
                new AntigravityUsageClient(),
                new OpenRouterUsageClient(),
                new OpenCodeUsageClient(),
                cursorTokenReader: () => new CursorTokens(),
                geminiAuthResolver: new GeminiAuthResolver(
                    antigravityReader: () => new AntigravityOAuthTokens(),
                    geminiCliReader: () => GeminiCliTokenReader.Read(credsPath)));

            viewModel.Load(new WidgetSettings { Gemini = new ProviderBillingSettings { ShowProLimits = true } });

            Assert.True(viewModel.HasGeminiAutoAuth);
            Assert.Equal("Signed in via Gemini CLI", viewModel.GeminiAutoAuthSummary);

            var geminiLimits = viewModel.Sections
                .Single(s => s.Title == "Gemini")
                .Sources
                .Single(s => s.Kind == ProviderSourceKind.AntigravityLimits);
            Assert.True(geminiLimits.HasAutoAuth);
            Assert.False(geminiLimits.ShowAdvancedSection);
        }
        finally
        {
            File.Delete(credsPath);
            Directory.Delete(oauthPath);
        }
    }

    [Fact]
    public void ToggleExpandedProvider_collapses_other_sections()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings());

        viewModel.ToggleExpandedProvider(SettingsExpandedProvider.OpenAi);

        Assert.True(viewModel.IsOpenAiExpanded);
        Assert.False(viewModel.IsCursorExpanded);

        viewModel.ToggleExpandedProvider(SettingsExpandedProvider.OpenAi);

        Assert.False(viewModel.IsOpenAiExpanded);
    }

    [Fact]
    public void ShowCodexCredentials_hidden_when_auth_file_present()
    {
        var authPath = Path.Combine(Path.GetTempPath(), $"codex-vm-{Guid.NewGuid():N}.json");
        File.WriteAllText(authPath, """
            {
              "auth_mode": "chatgpt",
              "tokens": { "access_token": "tok", "account_id": "acc" }
            }
            """);

        try
        {
            var viewModel = new SettingsPanelViewModel(
                new ProviderEasySetupService(),
                new OpenAiBillingClient(),
                new CodexUsageClient(authFilePathResolver: () => authPath),
                new AntigravityUsageClient(),
                new OpenRouterUsageClient(),
                new OpenCodeUsageClient(),
                cursorTokenReader: () => new CursorTokens());

            viewModel.Load(new WidgetSettings());
            viewModel.ShowCodexLimits = true;

            Assert.False(viewModel.ShowCodexCredentials);
        }
        finally
        {
            File.Delete(authPath);
        }
    }

    [Fact]
    public void Load_builds_all_provider_sections()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings());

        Assert.Equal(9, viewModel.Sections.Count);
        Assert.Contains(viewModel.Sections, s => s.Title == "OpenAI" && s.Sources.Count == 2);
        Assert.Contains(viewModel.Sections, s => s.Title == "Claude" && s.Sources.Count == 2);
        Assert.Contains(viewModel.Sections, s => s.Title == "fal.ai" && s.Sources.Count == 1);
        Assert.Contains(viewModel.Sections, s => s.Title == "xAI" && s.Sources.Count == 1);
        Assert.Contains(
            viewModel.Sections,
            s => s.Title == "Cursor"
                 && s.Sources.Any(src => src.Kind == ProviderSourceKind.GrokBotLimits
                                         && src.Name == "Grok Bot"));
        Assert.Contains(viewModel.Sections, s => s.Title == "Hardware" && s.Sources.Count == 4);
        var cursorSection = viewModel.Sections.First(s => s.Title == "Cursor");
        Assert.Equal(6, cursorSection.Sources.Count);
        Assert.Contains(cursorSection.Sources, s => s.Kind == ProviderSourceKind.OpenAiViaCursor);
        Assert.Contains(cursorSection.Sources, s => s.Kind == ProviderSourceKind.ClaudeViaCursor);
        Assert.Contains(cursorSection.Sources, s => s.Kind == ProviderSourceKind.GeminiViaCursor);
        Assert.Contains(cursorSection.Sources, s => s.Kind == ProviderSourceKind.GrokBotLimits);
    }

    [Fact]
    public void Commit_persists_cursor_last_connection_status()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings());
        viewModel.CursorStatus = "Sign in to Cursor IDE, then click Test";

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.Equal("Sign in to Cursor IDE, then click Test", committed.Cursor.LastConnectionStatus);
    }

    [Fact]
    public void Commit_persists_expanded_provider()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings());
        viewModel.ToggleExpandedProvider(SettingsExpandedProvider.Gemini);

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.Equal(SettingsExpandedProvider.Gemini, committed.SettingsExpandedProvider);
    }

    [Fact(Skip = "OpenRouter is currently hidden.")]
    public void OpenRouter_api_key_field_stays_visible_when_saved()
    {
        var apiKeyId = CredentialStore.Store("openrouter-ui-test", "sk-or-test");
        try
        {
            var viewModel = CreateViewModel();
            viewModel.Load(new WidgetSettings
            {
                OpenRouter = new ProviderBillingSettings
                {
                    ShowProLimits = true,
                    CredentialId = apiKeyId
                }
            });

            var openRouter = viewModel.Sections
                .Single(s => s.Title == "OpenRouter")
                .Sources
                .Single(s => s.Kind == ProviderSourceKind.OpenRouterCredits);

            Assert.True(openRouter.ShowApiKeyField);
            Assert.True(openRouter.HasApiKeySaved);
            Assert.True(openRouter.ShowManagementApiKeyField);
        }
        finally
        {
            CredentialStore.Delete(apiKeyId);
        }
    }

    [Fact]
    public void Fal_api_key_field_stays_visible_when_saved()
    {
        var apiKeyId = CredentialStore.Store("fal-ui-test", "fal-test-key");
        try
        {
            var viewModel = CreateViewModel();
            viewModel.Load(new WidgetSettings
            {
                Fal = new ProviderBillingSettings
                {
                    ShowProLimits = true,
                    CredentialId = apiKeyId
                }
            });

            var fal = viewModel.Sections
                .Single(s => s.Title == "fal.ai")
                .Sources
                .Single(s => s.Kind == ProviderSourceKind.FalCredits);

            Assert.True(fal.ShowApiKeyField);
            Assert.True(fal.HasApiKeySaved);
            Assert.Contains("saved", fal.ApiKeyWatermark, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CredentialStore.Delete(apiKeyId);
        }
    }

    [Fact]
    public void SaveApiKey_empty_text_does_not_clear_saved_fal_key()
    {
        var apiKeyId = CredentialStore.Store("fal-ui-test-empty", "fal-keep-me");
        try
        {
            var viewModel = CreateViewModel();
            viewModel.Load(new WidgetSettings
            {
                Fal = new ProviderBillingSettings
                {
                    ShowProLimits = true,
                    CredentialId = apiKeyId
                }
            });

            Assert.True(viewModel.HasFalApiKeySaved);
            viewModel.SaveApiKey(ProviderSourceKind.FalCredits, "   ");
            Assert.True(viewModel.HasFalApiKeySaved);

            viewModel.ClearApiKey(ProviderSourceKind.FalCredits);
            Assert.False(viewModel.HasFalApiKeySaved);
        }
        finally
        {
            CredentialStore.Delete(apiKeyId);
        }
    }

    [Fact]
    public void Load_and_Commit_round_trip_preserves_hardware_toggles()
    {
        var settings = new WidgetSettings
        {
            ShowCpuUsage = true,
            ShowGpuUsage = false,
            ShowRamUsage = true,
            ShowCpuTemp = true,
            ShowCpuTempDetail = false,
            ShowHardwareDetails = false
        };

        var viewModel = CreateViewModel();
        viewModel.Load(settings);

        Assert.True(viewModel.ShowCpuUsage);
        Assert.False(viewModel.ShowGpuUsage);
        Assert.True(viewModel.ShowRamUsage);
        Assert.True(viewModel.ShowCpuTemp);
        Assert.False(viewModel.ShowCpuTempDetail);
        Assert.False(viewModel.ShowHardwareDetails);

        viewModel.ShowGpuUsage = true;
        viewModel.ShowHardwareDetails = true;
        viewModel.ShowCpuTempDetail = true;

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.True(committed.ShowCpuUsage);
        Assert.True(committed.ShowGpuUsage);
        Assert.True(committed.ShowRamUsage);
        Assert.True(committed.ShowCpuTemp);
        Assert.True(committed.ShowCpuTempDetail);
        Assert.True(committed.ShowHardwareDetails);
    }

    [Fact]
    public void ExpandedProvider_change_notifies_host_for_layout_refresh()
    {
        var host = new FakeSettingsPanelHost();
        var viewModel = CreateViewModel();
        viewModel.AttachHost(host);
        viewModel.Load(new WidgetSettings());

        viewModel.ExpandedProvider = SettingsExpandedProvider.OpenAi;

        Assert.Equal(1, host.LayoutChangedCount);

        viewModel.ExpandedProvider = SettingsExpandedProvider.None;

        Assert.Equal(2, host.LayoutChangedCount);
    }

    [Fact]
    public void NotifyLayoutChanged_forwards_to_host()
    {
        var host = new FakeSettingsPanelHost();
        var viewModel = CreateViewModel();
        viewModel.AttachHost(host);

        viewModel.NotifyLayoutChanged();

        Assert.Equal(1, host.LayoutChangedCount);
    }

    private sealed class FakeSettingsPanelHost : ISettingsPanelHost
    {
        public int LayoutChangedCount { get; private set; }

        public void OnSettingsChanged()
        {
        }

        public void OnSettingsLayoutChanged() => LayoutChangedCount++;

        public Task OnEasySetupCompletedAsync() => Task.CompletedTask;
    }

    private static SettingsPanelViewModel CreateViewModel() =>
        new(
            new ProviderEasySetupService(),
            new OpenAiBillingClient(),
            new CodexUsageClient(),
            new AntigravityUsageClient(),
            new OpenRouterUsageClient(),
            new OpenCodeUsageClient(),
            cursorTokenReader: () => new CursorTokens());
}

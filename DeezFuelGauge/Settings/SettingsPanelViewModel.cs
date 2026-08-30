using System.Collections.ObjectModel;
using System.Globalization;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

namespace DeezFuelGauge.Settings;

public sealed class SettingsPanelViewModel : ViewModelBase
{
    private readonly ProviderEasySetupService _easySetup;
    private readonly OpenAiBillingClient _openAiBilling;
    private readonly CodexUsageClient _codexBilling;
    private readonly CodexAuthResolver _codexAuthResolver;
    private readonly AnthropicBillingClient _anthropicBilling;
    private readonly ClaudeProUsageClient _claudeProBilling;
    private readonly ClaudeOAuthLoginService _claudeOAuthLogin;
    private readonly ExternalSetupLauncher _launcher;
    private readonly AntigravityUsageClient _antigravityBilling;
    private readonly OpenRouterUsageClient _openRouterBilling;
    private readonly OpenCodeUsageClient _openCodeBilling;
    private readonly FalUsageClient _falBilling;
    private readonly XaiUsageClient _xaiBilling;
    private readonly GrokBotUsageClient _grokBotBilling;
    private readonly OpenCodeAuthResolver _openCodeAuthResolver;
    private readonly GeminiAuthResolver _geminiAuthResolver;
    private readonly Func<CursorTokens> _cursorTokenReader;
    private ISettingsPanelHost? _host;
    private bool _suppressChangeNotifications;
    private bool _updatingDerivedState;
    private SettingsExpandedProvider _expandedProvider = SettingsExpandedProvider.None;

    private string? _openAiCredentialId;
    private string? _openAiProSessionCredentialId;
    private string? _openRouterCredentialId;
    private string? _openRouterManagementCredentialId;
    private string? _falCredentialId;
    private string? _xaiCredentialId;
    private string? _openCodeProSessionCredentialId;
    private string? _openCodeWorkspaceId;
    private string? _claudeProSessionCredentialId;
    private string? _claudeProOAuthCredentialId;
    private string? _claudeCredentialId;
    private string? _claudeOAuthCodeVerifier;
    private string? _claudeOAuthState;
    private string? _claudeOAuthRedirectUri;
    private CancellationTokenSource? _claudeOAuthListenerCts;
    private ClaudeOAuthCallbackListener? _claudeOAuthListener;
    private AuthDetectionCache? _authDetectionCache;
    private static readonly TimeSpan AuthDetectionCacheTtl = TimeSpan.FromSeconds(30);

    private sealed record AuthDetectionCache(
        bool Codex,
        bool Gemini,
        bool OpenCode,
        DateTimeOffset CachedAt);

    public SettingsPanelViewModel(
        ProviderEasySetupService easySetup,
        OpenAiBillingClient openAiBilling,
        CodexUsageClient codexBilling,
        AntigravityUsageClient antigravityBilling,
        OpenRouterUsageClient openRouterBilling,
        OpenCodeUsageClient openCodeBilling,
        FalUsageClient? falBilling = null,
        XaiUsageClient? xaiBilling = null,
        Func<CursorTokens>? cursorTokenReader = null,
        GeminiAuthResolver? geminiAuthResolver = null,
        AnthropicBillingClient? anthropicBilling = null,
        ClaudeProUsageClient? claudeProBilling = null,
        ClaudeOAuthLoginService? claudeOAuthLogin = null,
        ExternalSetupLauncher? launcher = null,
        GrokBotUsageClient? grokBotBilling = null)
    {
        _easySetup = easySetup;
        _openAiBilling = openAiBilling;
        _codexBilling = codexBilling;
        _codexAuthResolver = new CodexAuthResolver(
            authFileReader: () => CodexUsageClient.TryReadLocalAuthFile(out var auth, null) ? auth : null);
        _anthropicBilling = anthropicBilling ?? new AnthropicBillingClient();
        _claudeProBilling = claudeProBilling ?? new ClaudeProUsageClient();
        _claudeOAuthLogin = claudeOAuthLogin ?? new ClaudeOAuthLoginService();
        _launcher = launcher ?? new ExternalSetupLauncher();
        _antigravityBilling = antigravityBilling;
        _openRouterBilling = openRouterBilling;
        _openCodeBilling = openCodeBilling;
        _falBilling = falBilling ?? new FalUsageClient();
        _xaiBilling = xaiBilling ?? new XaiUsageClient();
        _grokBotBilling = grokBotBilling ?? new GrokBotUsageClient();
        _openCodeAuthResolver = new OpenCodeAuthResolver();
        _cursorTokenReader = cursorTokenReader ?? CursorTokenReader.Read;
        _geminiAuthResolver = geminiAuthResolver ?? new GeminiAuthResolver();
    }

    public ObservableCollection<ProviderSettingsSectionViewModel> Sections { get; } = [];

    public void AttachHost(ISettingsPanelHost host) => _host = host;

    public void NotifyLayoutChanged() => _host?.OnSettingsLayoutChanged();

    public SettingsExpandedProvider ExpandedProvider
    {
        get => _expandedProvider;
        set
        {
            if (!SetProperty(ref _expandedProvider, value))
                return;

            foreach (var section in Sections)
                section.IsExpanded = section.ProviderId == value;

            _host?.OnSettingsLayoutChanged();
        }
    }

    public bool IsCursorExpanded => ExpandedProvider == SettingsExpandedProvider.Cursor;
    public bool IsOpenAiExpanded => ExpandedProvider == SettingsExpandedProvider.OpenAi;
    public bool IsClaudeExpanded => ExpandedProvider == SettingsExpandedProvider.Claude;
    public bool IsGeminiExpanded => ExpandedProvider == SettingsExpandedProvider.Gemini;
    public bool IsOpenRouterExpanded => ExpandedProvider == SettingsExpandedProvider.OpenRouter;
    public bool IsOpenCodeExpanded => ExpandedProvider == SettingsExpandedProvider.OpenCode;
    public bool IsFalExpanded => ExpandedProvider == SettingsExpandedProvider.Fal;
    public bool IsXaiExpanded => ExpandedProvider == SettingsExpandedProvider.Xai;
    public bool IsDiskExpanded => ExpandedProvider == SettingsExpandedProvider.Disk;
    public bool IsHardwareExpanded => ExpandedProvider == SettingsExpandedProvider.Hardware;

    public bool ShowCursor
    {
        get => GetCursorMain().IsEnabled;
        set => SetSourceEnabled(GetCursorMain(), value);
    }

    public bool ShowCursorDetails
    {
        get => GetCursorMain().ShowDetails;
        set => SetSourceDetail(GetCursorMain(), value);
    }

    public bool ShowBreakdown
    {
        get => GetCursorBreakdown().ShowBreakdown;
        set
        {
            var breakdown = GetCursorBreakdown();
            if (SetPropertyOnSource(breakdown, breakdown.ShowBreakdown, value, nameof(ProviderSourceViewModel.ShowBreakdown)))
                NotifyChanged();
        }
    }

    public string CursorStatus
    {
        get => GetCursorMain().Status;
        set => SetSourceStatus(GetCursorMain(), value);
    }

    public bool ShowOpenAi
    {
        get => GetSource(ProviderSourceKind.OpenAiViaCursor).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenAiViaCursor), value);
    }

    public bool ShowOpenAiDetails
    {
        get => GetSource(ProviderSourceKind.OpenAiViaCursor).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenAiViaCursor), value);
    }

    public bool ShowOpenAiDirect
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenAiDirect), value);
    }

    public bool ShowOpenAiDirectDetails
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenAiDirect), value);
    }

    public bool ShowCodexLimits
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenAiCodex), value);
    }

    public bool ShowCodexDetails
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenAiCodex), value);
    }

    public bool ShowCodexBreakdown
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).ShowLimits;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenAiCodex);
            if (SetPropertyOnSource(source, source.ShowLimits, value, nameof(ProviderSourceViewModel.ShowLimits)))
                NotifyChanged();
        }
    }

    public string OpenAiOrgId
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).OrgId;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenAiDirect);
            if (source.OrgId != value)
            {
                source.OrgId = value;
                NotifyChanged();
            }
        }
    }

    public string OpenAiBudget
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).Budget;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenAiDirect);
            if (source.Budget != value)
            {
                source.Budget = value;
                NotifyChanged();
            }
        }
    }

    public string OpenAiStatus
    {
        get => GetSource(ProviderSourceKind.OpenAiViaCursor).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.OpenAiViaCursor), value);
    }

    public string CodexStatus
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.OpenAiCodex), value);
    }

    public bool ShowClaude
    {
        get => GetSource(ProviderSourceKind.ClaudeViaCursor).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.ClaudeViaCursor), value);
    }

    public bool ShowClaudeDetails
    {
        get => GetSource(ProviderSourceKind.ClaudeViaCursor).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.ClaudeViaCursor), value);
    }

    public bool ShowClaudePro
    {
        get => GetSource(ProviderSourceKind.ClaudePro).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.ClaudePro), value);
    }

    public bool ShowClaudeProDetails
    {
        get => GetSource(ProviderSourceKind.ClaudePro).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.ClaudePro), value);
    }

    public bool ShowClaudeProBreakdown
    {
        get => GetSource(ProviderSourceKind.ClaudePro).ShowLimits;
        set
        {
            var source = GetSource(ProviderSourceKind.ClaudePro);
            if (SetPropertyOnSource(source, source.ShowLimits, value, nameof(ProviderSourceViewModel.ShowLimits)))
                NotifyChanged();
        }
    }

    public bool ShowClaudeApiConsole
    {
        get => GetSource(ProviderSourceKind.ClaudeApiConsole).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.ClaudeApiConsole), value);
    }

    public bool ShowClaudeApiConsoleDetails
    {
        get => GetSource(ProviderSourceKind.ClaudeApiConsole).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.ClaudeApiConsole), value);
    }

    public string ClaudeBudget
    {
        get => GetSource(ProviderSourceKind.ClaudeApiConsole).Budget;
        set
        {
            var source = GetSource(ProviderSourceKind.ClaudeApiConsole);
            if (source.Budget != value)
            {
                source.Budget = value;
                NotifyChanged();
            }
        }
    }

    public string ClaudeStatus
    {
        get => GetSource(ProviderSourceKind.ClaudeViaCursor).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.ClaudeViaCursor), value);
    }

    public string ClaudeProStatus
    {
        get => GetSource(ProviderSourceKind.ClaudePro).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.ClaudePro), value);
    }

    public string ClaudeApiConsoleStatus
    {
        get => GetSource(ProviderSourceKind.ClaudeApiConsole).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.ClaudeApiConsole), value);
    }

    public bool ShowGemini
    {
        get => GetSource(ProviderSourceKind.GeminiViaCursor).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.GeminiViaCursor), value);
    }

    public bool ShowGeminiDetails
    {
        get => GetSource(ProviderSourceKind.GeminiViaCursor).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.GeminiViaCursor), value);
    }

    public bool ShowAntigravityLimits
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.AntigravityLimits), value);
    }

    public bool ShowAntigravityDetails
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.AntigravityLimits), value);
    }

    public bool ShowAntigravityBreakdown
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).ShowLimits;
        set
        {
            var source = GetSource(ProviderSourceKind.AntigravityLimits);
            if (SetPropertyOnSource(source, source.ShowLimits, value, nameof(ProviderSourceViewModel.ShowLimits)))
                NotifyChanged();
        }
    }

    public string AntigravityStatus
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.AntigravityLimits), value);
    }

    public bool ShowOpenRouterLimits
    {
        get => ProviderFeatureFlags.OpenRouterEnabled
            && GetSource(ProviderSourceKind.OpenRouterCredits).IsEnabled;
        set
        {
            if (ProviderFeatureFlags.OpenRouterEnabled)
                SetSourceEnabled(GetSource(ProviderSourceKind.OpenRouterCredits), value);
        }
    }

    public bool ShowOpenRouterDetails
    {
        get => ProviderFeatureFlags.OpenRouterEnabled
            && GetSource(ProviderSourceKind.OpenRouterCredits).ShowDetails;
        set
        {
            if (ProviderFeatureFlags.OpenRouterEnabled)
                SetSourceDetail(GetSource(ProviderSourceKind.OpenRouterCredits), value);
        }
    }

    public string OpenRouterStatus
    {
        get => ProviderFeatureFlags.OpenRouterEnabled
            ? GetSource(ProviderSourceKind.OpenRouterCredits).Status
            : "";
        set
        {
            if (ProviderFeatureFlags.OpenRouterEnabled)
                SetSourceStatus(GetSource(ProviderSourceKind.OpenRouterCredits), value);
        }
    }

    public bool ShowOpenCodeZen
    {
        get => GetSource(ProviderSourceKind.OpenCodeZen).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenCodeZen), value);
    }

    public bool ShowOpenCodeGo
    {
        get => GetSource(ProviderSourceKind.OpenCodeGo).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenCodeGo), value);
    }

    public bool ShowOpenCodeZenDetails
    {
        get => GetSource(ProviderSourceKind.OpenCodeZen).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenCodeZen), value);
    }

    public bool ShowOpenCodeGoDetails
    {
        get => GetSource(ProviderSourceKind.OpenCodeGo).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenCodeGo), value);
    }

    public bool ShowOpenCodeGoBreakdown
    {
        get => GetSource(ProviderSourceKind.OpenCodeGo).ShowLimits;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenCodeGo);
            if (SetPropertyOnSource(source, source.ShowLimits, value, nameof(ProviderSourceViewModel.ShowLimits)))
                NotifyChanged();
        }
    }

    public string OpenCodeWorkspaceId
    {
        get => Sections.Count > 0
            ? GetSource(ProviderSourceKind.OpenCodeGo).WorkspaceId
            : (_openCodeWorkspaceId ?? "");
        set
        {
            _openCodeWorkspaceId = value;
            if (Sections.Count == 0)
                return;

            var source = GetSource(ProviderSourceKind.OpenCodeGo);
            if (source.WorkspaceId != value)
            {
                source.WorkspaceId = value;
                NotifyChanged();
            }
        }
    }

    public string OpenCodeStatus
    {
        get => GetSource(ProviderSourceKind.OpenCodeZen).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.OpenCodeZen), value);
    }

    public bool ShowFalLimits
    {
        get => GetSource(ProviderSourceKind.FalCredits).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.FalCredits), value);
    }

    public bool ShowFalDetails
    {
        get => GetSource(ProviderSourceKind.FalCredits).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.FalCredits), value);
    }

    public string FalStatus
    {
        get => GetSource(ProviderSourceKind.FalCredits).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.FalCredits), value);
    }

    public bool ShowXaiLimits
    {
        get => GetSource(ProviderSourceKind.XaiCredits).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.XaiCredits), value);
    }

    public bool ShowXaiDetails
    {
        get => GetSource(ProviderSourceKind.XaiCredits).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.XaiCredits), value);
    }

    public string XaiStatus
    {
        get => GetSource(ProviderSourceKind.XaiCredits).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.XaiCredits), value);
    }

    public bool ShowGrokBotLimits
    {
        get => GetSource(ProviderSourceKind.GrokBotLimits).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.GrokBotLimits), value);
    }

    public bool ShowGrokBotDetails
    {
        get => GetSource(ProviderSourceKind.GrokBotLimits).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.GrokBotLimits), value);
    }

    public string GrokBotStatus
    {
        get => GetSource(ProviderSourceKind.GrokBotLimits).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.GrokBotLimits), value);
    }

    public bool ShowDiskDrives
    {
        get => GetSource(ProviderSourceKind.DiskDrives).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.DiskDrives), value);
    }

    public bool ShowDiskDetails
    {
        get => GetSource(ProviderSourceKind.DiskDrives).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.DiskDrives), value);
    }

    public bool ShowCpuUsage
    {
        get => GetSource(ProviderSourceKind.HardwareCpuUsage).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareCpuUsage), value);
    }

    public bool ShowGpuUsage
    {
        get => GetSource(ProviderSourceKind.HardwareGpuUsage).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareGpuUsage), value);
    }

    public bool ShowRamUsage
    {
        get => GetSource(ProviderSourceKind.HardwareRamUsage).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareRamUsage), value);
    }

    public bool ShowCpuTemp
    {
        get => GetSource(ProviderSourceKind.HardwareCpuTemp).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareCpuTemp), value);
    }

    public bool ShowCpuTempDetail
    {
        get => GetSource(ProviderSourceKind.HardwareCpuTemp).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.HardwareCpuTemp), value);
    }

    public bool ShowHardwareDetails
    {
        get => GetSource(ProviderSourceKind.HardwareRamUsage).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.HardwareRamUsage), value);
    }

    public string OpenAiApiKeyWatermark => BuildWatermark("Admin API key", _openAiCredentialId);
    public string OpenAiSessionCookieWatermark => BuildWatermark("ChatGPT session cookie", _openAiProSessionCredentialId);
    public string OpenRouterApiKeyWatermark => BuildWatermark("API key (sk-or-...)", _openRouterCredentialId);
    public string OpenRouterManagementApiKeyWatermark =>
        BuildWatermark("Management key (optional, for balance)", _openRouterManagementCredentialId);
    public string FalApiKeyWatermark => BuildWatermark("Admin API key", _falCredentialId);
    public string XaiApiKeyWatermark => BuildWatermark("Management API key", _xaiCredentialId);
    public string OpenCodeSessionWatermark => BuildWatermark("opencode.ai auth cookie", _openCodeProSessionCredentialId);
    public string ClaudeApiKeyWatermark => BuildWatermark("Admin API key (sk-ant-admin...)", _claudeCredentialId);
    public string ClaudeProSessionWatermark =>
        BuildWatermark("Session key (sessionKey cookie value)", _claudeProSessionCredentialId);

    public bool HasOpenAiApiKeySaved => !string.IsNullOrWhiteSpace(_openAiCredentialId);
    public bool HasOpenAiSessionCookieSaved => !string.IsNullOrWhiteSpace(_openAiProSessionCredentialId);
    public bool HasOpenRouterApiKeySaved => !string.IsNullOrWhiteSpace(_openRouterCredentialId);
    public bool HasOpenRouterManagementApiKeySaved => !string.IsNullOrWhiteSpace(_openRouterManagementCredentialId);
    public bool HasFalApiKeySaved => !string.IsNullOrWhiteSpace(_falCredentialId);
    public bool HasXaiApiKeySaved => !string.IsNullOrWhiteSpace(_xaiCredentialId);
    public bool HasOpenCodeSessionSaved => !string.IsNullOrWhiteSpace(_openCodeProSessionCredentialId);
    public bool HasClaudeApiKeySaved => !string.IsNullOrWhiteSpace(_claudeCredentialId);
    public bool HasClaudeSessionCookieSaved => !string.IsNullOrWhiteSpace(_claudeProSessionCredentialId);
    public bool HasClaudeAppOAuthSaved => !string.IsNullOrWhiteSpace(_claudeProOAuthCredentialId);
    public bool HasClaudeCodeAuth => ClaudeCodeTokenReader.Read() is { IsExpired: false };
    public bool HasClaudeProAuth => HasClaudeSessionCookieSaved || HasClaudeAppOAuthSaved || HasClaudeCodeAuth;
    public bool ShowClaudeSignInButton => ShowClaudePro && !HasClaudeCodeAuth && !HasClaudeAppOAuthSaved;
    public bool IsClaudeOAuthPending => _claudeOAuthCodeVerifier is not null;
    public bool ShowClaudeProCredentials =>
        ShowClaudePro && !HasClaudeCodeAuth && !HasClaudeAppOAuthSaved && !HasClaudeSessionCookieSaved;
    public bool ShowClaudeApiConsoleCredentials => ShowClaudeApiConsole && !HasClaudeApiKeySaved;

    public string ClaudeCodeAutoAuthSummary => HasClaudeCodeAuth ? "Signed in via Claude Code CLI" : "";

    public bool HasOpenCodeAutoAuth => GetAuthCache().OpenCode;

    public bool HasOpenCodeAutoAuthFor(ProviderBillingSettings openCode)
    {
        var defaults = CreateOpenCodeBillingSettings();
        if (openCode.ProSessionCredentialId == defaults.ProSessionCredentialId
            && openCode.WorkspaceId == defaults.WorkspaceId)
            return GetAuthCache().OpenCode;

        return _openCodeAuthResolver.HasDetectableAuth(openCode);
    }

    public bool HasOpenCodeApiKeyAuth => _openCodeAuthResolver.HasApiKeyAuth();

    public string OpenCodeAutoAuthSummary => OpenCodeAutoAuthSummaryFor(CreateOpenCodeBillingSettings());

    public string OpenCodeAutoAuthSummaryFor(ProviderBillingSettings openCode) => HasOpenCodeAutoAuthFor(openCode)
        ? (_openCodeAuthResolver.Resolve(openCode, tryBrowserCookies: false).Source switch
        {
            OpenCodeAuthSource.ApiKey => "Signed in via OpenCode CLI",
            OpenCodeAuthSource.SavedSession => "Signed in via saved session",
            _ => "Signed in via browser"
        })
        : "";

    public bool ShowOpenCodeCredentials =>
        (ShowOpenCodeZen || ShowOpenCodeGo)
        && !HasOpenCodeAutoAuth
        && !HasOpenCodeSessionSaved;

    public bool ShowOpenCodeCredentialsFor(ProviderBillingSettings openCode) =>
        (openCode.ShowDirectSource || openCode.ShowProLimits)
        && !HasOpenCodeAutoAuthFor(openCode)
        && !HasOpenCodeSessionSaved;

    public bool HasCodexAutoAuth => GetAuthCache().Codex;

    public string CodexAutoAuthSummary => HasCodexAutoAuth
        ? (_codexAuthResolver.Resolve(CreateOpenAiBillingSettings(), tryBrowserCookies: false).Source switch
        {
            CodexAuthSource.AuthFile => "Signed in via Codex CLI",
            CodexAuthSource.SavedSession => "Signed in via saved session",
            _ => "Signed in via browser"
        })
        : "";

    public bool ShowCodexCredentials =>
        ShowCodexLimits && !HasCodexAutoAuth && !HasOpenAiSessionCookieSaved;

    public bool ShowOpenAiDirectCredentials => ShowOpenAiDirect && !HasOpenAiApiKeySaved;

    public bool HasGeminiAutoAuth => GetAuthCache().Gemini;

    public string GeminiAutoAuthSummary => HasGeminiAutoAuth
        ? (_geminiAuthResolver.DetectedSource() switch
        {
            GeminiAuthSource.Antigravity => "Signed in via Antigravity IDE",
            GeminiAuthSource.GeminiCli => "Signed in via Gemini CLI",
            _ => "Signed in"
        })
        : "";

    public void ToggleExpandedProvider(SettingsExpandedProvider provider) =>
        ExpandedProvider = ExpandedProvider == provider ? SettingsExpandedProvider.None : provider;

    public void Load(WidgetSettings settings)
    {
        _suppressChangeNotifications = true;
        try
        {
            _openAiCredentialId = settings.OpenAi.CredentialId;
            _openAiProSessionCredentialId = settings.OpenAi.ProSessionCredentialId;
            _openRouterCredentialId = settings.OpenRouter.CredentialId;
            _openRouterManagementCredentialId = settings.OpenRouter.ManagementCredentialId;
            _falCredentialId = settings.Fal.CredentialId;
            _xaiCredentialId = settings.Xai.CredentialId;
            _openCodeProSessionCredentialId = settings.OpenCode.ProSessionCredentialId;
            _openCodeWorkspaceId = settings.OpenCode.WorkspaceId;
            _claudeProSessionCredentialId = settings.Claude.ProSessionCredentialId;
            _claudeProOAuthCredentialId = settings.Claude.ProOAuthCredentialId;
            _claudeCredentialId = settings.Claude.CredentialId;

            SettingsSectionMapper.PopulateSections(Sections, settings, this);
            _expandedProvider = settings.SettingsExpandedProvider;

            UpdateCredentialDerivedState();
            UpdateConnectionStates();
            WireSectionNotifications();
            OnPropertyChanged(nameof(ExpandedProvider));
            NotifyAccordionPropertiesChanged();
        }
        finally
        {
            _suppressChangeNotifications = false;
        }
    }

    public void Commit(WidgetSettings settings)
    {
        SettingsSectionMapper.ApplyToSettings(Sections, settings, ExpandedProvider);

        settings.OpenAi.CredentialId = _openAiCredentialId;
        settings.OpenAi.ProSessionCredentialId = _openAiProSessionCredentialId;
        settings.OpenRouter.CredentialId = _openRouterCredentialId;
        settings.OpenRouter.ManagementCredentialId = _openRouterManagementCredentialId;
        settings.Fal.CredentialId = _falCredentialId;
        settings.Xai.CredentialId = _xaiCredentialId;
        settings.OpenCode.ProSessionCredentialId = _openCodeProSessionCredentialId;
        settings.Claude.ProSessionCredentialId = _claudeProSessionCredentialId;
        settings.Claude.ProOAuthCredentialId = _claudeProOAuthCredentialId;
        settings.Claude.CredentialId = _claudeCredentialId;
    }

    public string ResolveCursorStatus()
    {
        var tokens = _cursorTokenReader();
        return string.IsNullOrWhiteSpace(tokens.AccessToken)
            ? "Not signed in to Cursor"
            : "Connected via Cursor session";
    }

    public void UpdateStatusFromSettings(WidgetSettings settings)
    {
        if (Sections.Count == 0)
            return;

        CursorStatus = settings.Cursor.LastConnectionStatus ?? ResolveCursorStatus();
        OpenAiStatus = settings.OpenAi.LastConnectionStatus ?? OpenAiStatus;
        CodexStatus = settings.OpenAi.ProLastConnectionStatus ?? CodexStatus;
        ClaudeStatus = settings.Claude.LastConnectionStatus ?? ClaudeStatus;
        ClaudeProStatus = settings.Claude.ProLastConnectionStatus ?? ClaudeProStatus;
        ClaudeApiConsoleStatus = settings.Claude.LastConnectionStatus ?? ClaudeApiConsoleStatus;
        AntigravityStatus = settings.Gemini.ProLastConnectionStatus ?? AntigravityStatus;
        if (ProviderFeatureFlags.OpenRouterEnabled)
            OpenRouterStatus = settings.OpenRouter.LastConnectionStatus ?? OpenRouterStatus;
        OpenCodeStatus = settings.OpenCode.ProLastConnectionStatus ?? OpenCodeStatus;
        FalStatus = settings.Fal.LastConnectionStatus ?? FalStatus;
        XaiStatus = settings.Xai.LastConnectionStatus ?? XaiStatus;
        GrokBotStatus = settings.GrokBot.LastConnectionStatus ?? GrokBotStatus;
        UpdateConnectionStates();
    }

    public void UpdateCursorConnectionStatus()
    {
        var status = ResolveCursorStatus();
        if (string.IsNullOrWhiteSpace(GetCursorMain().Status))
            SetSourceStatus(GetCursorMain(), status, notify: false);
        UpdateConnectionStates();
    }

    /// <summary>
    /// Copy a resolved xAI team UUID from persisted settings into the Credits source
    /// so Commit cannot wipe it and the Advanced field stays in sync after refresh.
    /// </summary>
    public void SyncXaiWorkspaceFromSettings(WidgetSettings settings)
    {
        var credits = GetSource(ProviderSourceKind.XaiCredits);
        var workspaceId = settings.Xai.WorkspaceId ?? "";
        if (credits.WorkspaceId != workspaceId)
            credits.WorkspaceId = workspaceId;
        UpdateConnectionStates();
    }

    public void UpdateConnectionStates()
    {
        var hasCursorToken = !string.IsNullOrWhiteSpace(_cursorTokenReader().AccessToken);

        var cursorNative = ProviderConnectionStateHelper.FromConnected(ShowCursor, hasCursorToken);
        var openAiCursor = ProviderConnectionStateHelper.FromConnected(ShowOpenAi, hasCursorToken);
        var claudeCursor = ProviderConnectionStateHelper.FromConnected(ShowClaude, hasCursorToken);
        var geminiCursor = ProviderConnectionStateHelper.FromConnected(ShowGemini, hasCursorToken);
        var grokBot = ProviderConnectionStateHelper.FromConnected(ShowGrokBotLimits, hasCursorToken);
        SetSectionColor(
            SettingsExpandedProvider.Cursor,
            ProviderConnectionStateHelper.Aggregate(
                cursorNative, openAiCursor, claudeCursor, geminiCursor, grokBot));

        var openAiDirect = ProviderConnectionStateHelper.FromConnected(ShowOpenAiDirect, HasOpenAiApiKeySaved);
        var openAiCodex = ProviderConnectionStateHelper.FromConnected(
            ShowCodexLimits,
            HasCodexAutoAuth || HasOpenAiSessionCookieSaved);
        SetSectionColor(
            SettingsExpandedProvider.OpenAi,
            ProviderConnectionStateHelper.Aggregate(openAiDirect, openAiCodex));

        var claudePro = ProviderConnectionStateHelper.FromConnected(ShowClaudePro, HasClaudeProAuth);
        var claudeApi = ProviderConnectionStateHelper.FromConnected(ShowClaudeApiConsole, HasClaudeApiKeySaved);
        SetSectionColor(
            SettingsExpandedProvider.Claude,
            ProviderConnectionStateHelper.Aggregate(claudePro, claudeApi));

        var geminiLimits = ProviderConnectionStateHelper.FromConnected(ShowAntigravityLimits, HasGeminiAutoAuth);
        SetSectionColor(
            SettingsExpandedProvider.Gemini,
            geminiLimits);

        if (ProviderFeatureFlags.OpenRouterEnabled)
        {
            SetSectionColor(
                SettingsExpandedProvider.OpenRouter,
                ProviderConnectionStateHelper.FromConnected(ShowOpenRouterLimits, HasOpenRouterApiKeySaved));
        }

        var openCodeConnected = HasOpenCodeApiKeyAuth || HasOpenCodeAutoAuth;
        SetSectionColor(
            SettingsExpandedProvider.OpenCode,
            ProviderConnectionStateHelper.Aggregate(
                ProviderConnectionStateHelper.FromConnected(ShowOpenCodeZen, openCodeConnected),
                ProviderConnectionStateHelper.FromConnected(ShowOpenCodeGo, openCodeConnected)));

        SetSectionColor(
            SettingsExpandedProvider.Fal,
            ProviderConnectionStateHelper.FromConnected(ShowFalLimits, HasFalApiKeySaved));

        SetSectionColor(
            SettingsExpandedProvider.Xai,
            ProviderConnectionStateHelper.FromConnected(ShowXaiLimits, HasXaiApiKeySaved));

        SetSectionColor(
            SettingsExpandedProvider.Disk,
            ShowDiskDrives ? ProviderConnectionState.Connected : ProviderConnectionState.Off);

        SetSectionColor(
            SettingsExpandedProvider.Hardware,
            ShowCpuUsage || ShowGpuUsage || ShowRamUsage || ShowCpuTemp
                ? ProviderConnectionState.Connected
                : ProviderConnectionState.Off);
    }

    public async Task ConnectAsync(ProviderSourceKind kind, WidgetSettings settings)
    {
        switch (kind)
        {
            case ProviderSourceKind.CursorWidget:
                await RunEasySetupCursorAsync(settings);
                break;
            case ProviderSourceKind.OpenAiViaCursor:
                await RunEasySetupOpenAiViaCursorAsync(settings);
                break;
            case ProviderSourceKind.OpenAiCodex:
                await RunEasySetupCodexAsync(settings);
                break;
            case ProviderSourceKind.ClaudeViaCursor:
                await RunEasySetupClaudeViaCursorAsync(settings);
                break;
            case ProviderSourceKind.ClaudePro:
                await RunEasySetupClaudeAsync(settings);
                break;
            case ProviderSourceKind.GeminiViaCursor:
                await RunEasySetupGeminiViaCursorAsync(settings);
                break;
            case ProviderSourceKind.AntigravityLimits:
                await RunEasySetupGeminiAsync(settings);
                break;
            case ProviderSourceKind.OpenRouterCredits:
                await RunEasySetupOpenRouterAsync(settings);
                break;
            case ProviderSourceKind.FalCredits:
                await RunEasySetupFalAsync(settings);
                break;
            case ProviderSourceKind.XaiCredits:
                await RunEasySetupXaiAsync(settings);
                break;
            case ProviderSourceKind.OpenCodeZen:
            case ProviderSourceKind.OpenCodeGo:
                await RunEasySetupOpenCodeAsync(settings);
                break;
            case ProviderSourceKind.GrokBotLimits:
                await RunEasySetupGrokBotAsync(settings);
                break;
        }
    }

    public async Task TestAsync(ProviderSourceKind kind, WidgetSettings settings)
    {
        Commit(settings);
        switch (kind)
        {
            case ProviderSourceKind.CursorWidget:
            case ProviderSourceKind.OpenAiViaCursor:
            case ProviderSourceKind.GeminiViaCursor:
            case ProviderSourceKind.ClaudeViaCursor:
                await TestCursorAsync(settings);
                break;
            case ProviderSourceKind.OpenAiDirect:
                await TestOpenAiAsync(settings);
                break;
            case ProviderSourceKind.OpenAiCodex:
                await TestCodexAsync(settings);
                break;
            case ProviderSourceKind.ClaudePro:
                await TestClaudeProAsync(settings);
                break;
            case ProviderSourceKind.ClaudeApiConsole:
                await TestClaudeApiConsoleAsync(settings);
                break;
            case ProviderSourceKind.AntigravityLimits:
                await TestAntigravityAsync(settings);
                break;
            case ProviderSourceKind.OpenRouterCredits:
                await TestOpenRouterAsync(settings);
                break;
            case ProviderSourceKind.FalCredits:
                await TestFalAsync(settings);
                break;
            case ProviderSourceKind.XaiCredits:
                await TestXaiAsync(settings);
                break;
            case ProviderSourceKind.OpenCodeZen:
            case ProviderSourceKind.OpenCodeGo:
                await TestOpenCodeAsync(settings);
                break;
            case ProviderSourceKind.GrokBotLimits:
                await TestGrokBotAsync(settings);
                break;
        }
    }

    public void SaveApiKey(ProviderSourceKind kind, string? text)
    {
        // Empty blur must not wipe a saved key — use Clear for that.
        if (string.IsNullOrWhiteSpace(text))
            return;

        switch (kind)
        {
            case ProviderSourceKind.OpenAiDirect:
                SaveCredential("openai", ref _openAiCredentialId, text);
                break;
            case ProviderSourceKind.OpenRouterCredits:
                SaveCredential("openrouter", ref _openRouterCredentialId, text);
                break;
            case ProviderSourceKind.FalCredits:
                SaveCredential("fal", ref _falCredentialId, text);
                break;
            case ProviderSourceKind.XaiCredits:
                SaveCredential("xai", ref _xaiCredentialId, text);
                break;
            case ProviderSourceKind.ClaudeApiConsole:
                SaveCredential("claude", ref _claudeCredentialId, text);
                break;
        }

        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void SaveManagementApiKey(ProviderSourceKind kind, string? text)
    {
        if (kind != ProviderSourceKind.OpenRouterCredits)
            return;

        // Empty blur must not wipe a saved key — use Clear for that.
        if (string.IsNullOrWhiteSpace(text))
            return;

        SaveCredential("openrouter-mgmt", ref _openRouterManagementCredentialId, text);
        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void SaveSession(ProviderSourceKind kind, string? text)
    {
        // Empty blur must not wipe a saved session — use Clear for that.
        if (string.IsNullOrWhiteSpace(text))
            return;

        switch (kind)
        {
            case ProviderSourceKind.OpenAiCodex:
                SaveCredential("openai-codex", ref _openAiProSessionCredentialId, text);
                break;
            case ProviderSourceKind.ClaudePro:
                SaveCredential("claude-pro", ref _claudeProSessionCredentialId, text);
                break;
            case ProviderSourceKind.OpenCodeGo:
                SaveCredential("opencode", ref _openCodeProSessionCredentialId, text);
                break;
        }

        InvalidateAuthDetectionCache();
        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void ClearApiKey(ProviderSourceKind kind)
    {
        switch (kind)
        {
            case ProviderSourceKind.OpenAiDirect:
                CredentialStore.Delete(_openAiCredentialId);
                _openAiCredentialId = null;
                break;
            case ProviderSourceKind.OpenRouterCredits:
                CredentialStore.Delete(_openRouterCredentialId);
                _openRouterCredentialId = null;
                break;
            case ProviderSourceKind.FalCredits:
                CredentialStore.Delete(_falCredentialId);
                _falCredentialId = null;
                break;
            case ProviderSourceKind.XaiCredits:
                CredentialStore.Delete(_xaiCredentialId);
                _xaiCredentialId = null;
                break;
            case ProviderSourceKind.ClaudeApiConsole:
                CredentialStore.Delete(_claudeCredentialId);
                _claudeCredentialId = null;
                break;
        }

        InvalidateAuthDetectionCache();
        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void ClearManagementApiKey(ProviderSourceKind kind)
    {
        if (kind != ProviderSourceKind.OpenRouterCredits)
            return;

        CredentialStore.Delete(_openRouterManagementCredentialId);
        _openRouterManagementCredentialId = null;
        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void ClearSession(ProviderSourceKind kind)
    {
        switch (kind)
        {
            case ProviderSourceKind.OpenAiCodex:
                CredentialStore.Delete(_openAiProSessionCredentialId);
                _openAiProSessionCredentialId = null;
                break;
            case ProviderSourceKind.ClaudePro:
                CredentialStore.Delete(_claudeProSessionCredentialId);
                _claudeProSessionCredentialId = null;
                break;
            case ProviderSourceKind.OpenCodeGo:
                CredentialStore.Delete(_openCodeProSessionCredentialId);
                _openCodeProSessionCredentialId = null;
                break;
        }

        InvalidateAuthDetectionCache();
        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void OnMasterEnableChanged(ProviderSettingsSectionViewModel section, bool enabled)
    {
        section.MasterEnable = enabled;
        foreach (var source in section.Sources.Where(s => s.HasEnableToggle))
        {
            // Preserve per-drive picks when toggling the Disk section master.
            if (section.ProviderId == SettingsExpandedProvider.Disk
                && source.Kind == ProviderSourceKind.DiskDrive)
                continue;

            source.IsEnabled = enabled;
        }

        if (section.ProviderId == SettingsExpandedProvider.Disk)
        {
            SettingsSectionMapper.ApplyDiskSummary(section, enabled);
        }
        else if (section.ProviderId == SettingsExpandedProvider.Hardware)
        {
            section.SummaryStatus = enabled ? "Enabled" : "Off";
        }

        NotifyChanged();
    }

    public async Task RunEasySetupCursorAsync(WidgetSettings settings)
    {
        var result = _easySetup.SetupCursor(settings);
        CursorStatus = settings.Cursor.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings, preserveCursorStatus: true);
    }

    public async Task RunEasySetupOpenAiViaCursorAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenAiAsync(settings);
        OpenAiStatus = settings.OpenAi.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupCodexAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupCodexAsync(settings);
        CodexStatus = settings.OpenAi.ProLastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupClaudeViaCursorAsync(WidgetSettings settings)
    {
        ShowClaude = true;
        ShowClaudeDetails = true;
        ClaudeStatus = "Via Cursor: enabled";
        settings.Claude.ShowCursorSource = true;
        settings.Claude.ShowDetails = true;
        settings.Claude.LastConnectionStatus = ClaudeStatus;
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupClaudeAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupClaudeAsync(settings);
        ClaudeProStatus = settings.Claude.ProLastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupGeminiViaCursorAsync(WidgetSettings settings)
    {
        ShowGemini = true;
        ShowGeminiDetails = true;
        await _easySetup.SetupGeminiViaCursorAsync(settings);
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupGeminiAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupGeminiAsync(settings);
        AntigravityStatus = settings.Gemini.ProLastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupOpenRouterAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenRouterAsync(settings);
        OpenRouterStatus = settings.OpenRouter.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupFalAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupFalAsync(settings);
        FalStatus = settings.Fal.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupXaiAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupXaiAsync(settings);
        XaiStatus = settings.Xai.LastConnectionStatus ?? result.StatusMessage ?? "";
        // Persist resolved team into the VM before CompleteEasySetup Commit can wipe it.
        SyncXaiWorkspaceFromSettings(settings);
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupOpenCodeAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenCodeAsync(settings);
        OpenCodeStatus = settings.OpenCode.ProLastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupGrokBotAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupGrokBotAsync(settings);
        GrokBotStatus = settings.GrokBot.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task TestCursorAsync(WidgetSettings settings)
    {
        Commit(settings);
        var status = ResolveCursorStatus();
        CursorStatus = status;
        settings.Cursor.LastConnectionStatus = status;
        UpdateConnectionStates();
        _host?.OnSettingsChanged();
        await Task.CompletedTask;
    }

    public async Task TestOpenAiAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_openAiCredentialId);
        OpenAiStatus = await _openAiBilling.TestConnectionAsync(key ?? "", settings.OpenAi.OrganizationId);
        settings.OpenAi.LastConnectionStatus = OpenAiStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestCodexAsync(WidgetSettings settings)
    {
        Commit(settings);
        settings.OpenAi.ShowProLimits = true;
        CodexStatus = await _codexBilling.RefreshAndConnectAsync(settings.OpenAi);
        _openAiProSessionCredentialId = settings.OpenAi.ProSessionCredentialId;
        UpdateCredentialDerivedState();
        _host?.OnSettingsChanged();
    }

    public async Task TestClaudeProAsync(WidgetSettings settings)
    {
        Commit(settings);
        settings.Claude.ShowProLimits = true;
        ClaudeProStatus = await _claudeProBilling.RefreshAndConnectAsync(settings.Claude);
        _claudeProSessionCredentialId = settings.Claude.ProSessionCredentialId;
        _claudeProOAuthCredentialId = settings.Claude.ProOAuthCredentialId;
        UpdateCredentialDerivedState();
        _host?.OnSettingsChanged();
    }

    public async Task TestClaudeApiConsoleAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_claudeCredentialId);
        ClaudeApiConsoleStatus = await _anthropicBilling.TestConnectionAsync(key ?? "");
        settings.Claude.LastConnectionStatus = ClaudeApiConsoleStatus;
        _host?.OnSettingsChanged();
    }

    public void BeginClaudeSignIn(WidgetSettings? settings = null)
    {
        CancelClaudeSignInListener();

        var listener = settings is not null
            ? ClaudeOAuthCallbackListener.TryStart(ClaudeOAuthLoginService.DefaultCallbackPort)
              ?? ClaudeOAuthCallbackListener.TryStart(0)
            : null;
        _claudeOAuthListener = listener;

        var start = _claudeOAuthLogin.BeginLogin(listener is null
            ? null
            : ClaudeOAuthLoginService.BuildLoopbackRedirectUri(listener.Port));
        _claudeOAuthCodeVerifier = start.CodeVerifier;
        _claudeOAuthState = start.State;
        _claudeOAuthRedirectUri = start.RedirectUri;
        _launcher.OpenUrl(start.AuthorizeUrl);

        if (listener is not null && settings is not null)
        {
            ClaudeProStatus = "Approve the sign-in in your browser — connecting automatically…";
            _claudeOAuthListenerCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            _ = ListenForClaudeCallbackAsync(listener, settings, _claudeOAuthListenerCts.Token);
        }
        else
        {
            ClaudeProStatus = "Paste the code from your browser below, then click Connect";
        }

        UpdateClaudeOAuthDerivedState();
    }

    public async Task CompleteClaudeSignInAsync(WidgetSettings settings, string? pastedCode)
    {
        if (_claudeOAuthCodeVerifier is null || _claudeOAuthState is null)
        {
            ClaudeProStatus = "Click 'Sign in with Claude' first";
            return;
        }

        if (string.IsNullOrWhiteSpace(pastedCode))
        {
            ClaudeProStatus = "Paste the code from claude.ai first";
            return;
        }

        try
        {
            var token = await _claudeOAuthLogin.ExchangeCodeAsync(
                pastedCode,
                _claudeOAuthCodeVerifier,
                _claudeOAuthState,
                _claudeOAuthRedirectUri,
                CancellationToken.None);

            CancelClaudeSignInListener();
            ClaudeOAuthTokenStore.Persist(settings.Claude, token);
            _claudeProOAuthCredentialId = settings.Claude.ProOAuthCredentialId;
            _claudeOAuthCodeVerifier = null;
            _claudeOAuthState = null;
            _claudeOAuthRedirectUri = null;

            settings.Claude.ShowProLimits = true;
            Commit(settings);
            ClaudeProStatus = await _claudeProBilling.RefreshAndConnectAsync(settings.Claude);
            _claudeProOAuthCredentialId = settings.Claude.ProOAuthCredentialId;
            UpdateClaudeOAuthDerivedState();
            await CompleteEasySetupAsync(settings);
        }
        catch (ClaudeOAuthException ex)
        {
            ClaudeProStatus = ex.Message;
        }
    }

    public void DisconnectClaudeOAuth(WidgetSettings settings)
    {
        ClaudeOAuthTokenStore.Clear(settings.Claude);
        _claudeProOAuthCredentialId = null;
        settings.Claude.ProLastConnectionStatus = null;
        ClaudeProStatus = "";
        InvalidateAuthDetectionCache();
        UpdateClaudeOAuthDerivedState();
        Commit(settings);
        _host?.OnSettingsChanged();
    }

    private async Task ListenForClaudeCallbackAsync(
        ClaudeOAuthCallbackListener listener,
        WidgetSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await listener.WaitForCallbackAsync(cancellationToken);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (result.Error is not null || string.IsNullOrWhiteSpace(result.Code))
                {
                    ClaudeProStatus = "Sign-in was not completed — click Sign in with Claude to try again";
                    return;
                }

                var pasted = string.IsNullOrEmpty(result.State) ? result.Code : $"{result.Code}#{result.State}";
                await CompleteClaudeSignInAsync(settings, pasted);
            });
        }
        catch (OperationCanceledException)
        {
            // Sign-in abandoned or superseded.
        }
        catch
        {
            // Listener failures leave the manual paste path available.
        }
        finally
        {
            listener.Dispose();
        }
    }

    private void CancelClaudeSignInListener()
    {
        _claudeOAuthListenerCts?.Cancel();
        _claudeOAuthListenerCts?.Dispose();
        _claudeOAuthListenerCts = null;
        _claudeOAuthListener?.Dispose();
        _claudeOAuthListener = null;
    }

    public async Task TestAntigravityAsync(WidgetSettings settings)
    {
        Commit(settings);
        AntigravityStatus = await _antigravityBilling.TestConnectionAsync();
        settings.Gemini.ProLastConnectionStatus = AntigravityStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestOpenRouterAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_openRouterCredentialId);
        var managementKey = CredentialStore.Retrieve(_openRouterManagementCredentialId);
        OpenRouterStatus = await _openRouterBilling.TestConnectionAsync(key ?? "", managementKey);
        settings.OpenRouter.LastConnectionStatus = OpenRouterStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestFalAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_falCredentialId);
        FalStatus = await _falBilling.TestConnectionAsync(key ?? "");
        settings.Fal.LastConnectionStatus = FalStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestXaiAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_xaiCredentialId);
        XaiStatus = await _xaiBilling.TestConnectionAsync(
            key ?? "",
            settings.Xai.WorkspaceId,
            settings.Xai);
        SyncXaiWorkspaceFromSettings(settings);
        _host?.OnSettingsChanged();
    }

    public async Task TestOpenCodeAsync(WidgetSettings settings)
    {
        Commit(settings);
        OpenCodeStatus = await _openCodeBilling.TestConnectionAsync(settings.OpenCode);
        settings.OpenCode.ProLastConnectionStatus = OpenCodeStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestGrokBotAsync(WidgetSettings settings)
    {
        Commit(settings);
        var tokens = _cursorTokenReader();
        _grokBotBilling.SetTokens(tokens.AccessToken, tokens.RefreshToken);
        GrokBotStatus = await _grokBotBilling.TestConnectionAsync(settings.GrokBot);
        settings.GrokBot.LastConnectionStatus = GrokBotStatus;
        _host?.OnSettingsChanged();
    }

    private async Task CompleteEasySetupAsync(WidgetSettings settings, bool preserveCursorStatus = false)
    {
        var savedCursorStatus = preserveCursorStatus ? CursorStatus : null;
        Commit(settings);
        Load(settings);
        if (preserveCursorStatus && savedCursorStatus is not null)
        {
            CursorStatus = savedCursorStatus;
            settings.Cursor.LastConnectionStatus = savedCursorStatus;
            Commit(settings);
        }

        if (_host is not null)
            await _host.OnEasySetupCompletedAsync();
    }

    private ProviderSourceViewModel GetCursorMain() =>
        GetSection(SettingsExpandedProvider.Cursor).Sources.First(s => s.HasEnableToggle);

    private ProviderSourceViewModel GetCursorBreakdown() =>
        GetSection(SettingsExpandedProvider.Cursor).Sources.First(s => s.HasBreakdownToggle);

    private ProviderSourceViewModel GetSource(ProviderSourceKind kind) =>
        Sections.SelectMany(s => s.Sources).First(s => s.Kind == kind);

    private ProviderSettingsSectionViewModel GetSection(SettingsExpandedProvider provider) =>
        Sections.First(s => s.ProviderId == provider);

    private void SetSectionColor(SettingsExpandedProvider provider, ProviderConnectionState state)
    {
        var section = GetSection(provider);
        section.HeaderColor = ProviderConnectionStateHelper.ToColor(state);
    }

    private void SetSourceEnabled(ProviderSourceViewModel source, bool value)
    {
        if (SetPropertyOnSource(source, source.IsEnabled, value, nameof(ProviderSourceViewModel.IsEnabled)))
            NotifyChanged();
    }

    private void SetSourceDetail(ProviderSourceViewModel source, bool value)
    {
        if (SetPropertyOnSource(source, source.ShowDetails, value, nameof(ProviderSourceViewModel.ShowDetails)))
            NotifyChanged();
    }

    private void SetSourceStatus(ProviderSourceViewModel source, string value, bool notify = true)
    {
        if (source.Status == value)
            return;

        source.Status = value;
        if (notify)
            NotifyChanged();
    }

    private static bool SetPropertyOnSource<T>(
        ProviderSourceViewModel source,
        T current,
        T value,
        string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
            return false;

        switch (propertyName)
        {
            case nameof(ProviderSourceViewModel.IsEnabled):
                source.IsEnabled = (bool)(object)value!;
                break;
            case nameof(ProviderSourceViewModel.ShowDetails):
                source.ShowDetails = (bool)(object)value!;
                break;
            case nameof(ProviderSourceViewModel.ShowLimits):
                source.ShowLimits = (bool)(object)value!;
                break;
            case nameof(ProviderSourceViewModel.ShowBreakdown):
                source.ShowBreakdown = (bool)(object)value!;
                break;
        }

        return true;
    }

    public void InvalidateAuthDetectionCache() => _authDetectionCache = null;

    private AuthDetectionCache GetAuthCache()
    {
        if (_authDetectionCache is { } cached
            && DateTimeOffset.UtcNow - cached.CachedAt < AuthDetectionCacheTtl)
            return cached;

        cached = new AuthDetectionCache(
            Codex: _codexAuthResolver.HasDetectableAuth(CreateOpenAiBillingSettings()),
            Gemini: _geminiAuthResolver.HasDetectableAuth(),
            OpenCode: _openCodeAuthResolver.HasDetectableAuth(CreateOpenCodeBillingSettings()),
            CachedAt: DateTimeOffset.UtcNow);
        _authDetectionCache = cached;
        return cached;
    }

    private void NotifyChanged()
    {
        UpdateCredentialDerivedState();
        UpdateConnectionStates();
        if (!_suppressChangeNotifications)
            _host?.OnSettingsChanged();
    }

    private void UpdateCredentialDerivedState()
    {
        if (_updatingDerivedState || Sections.Count == 0)
            return;

        _updatingDerivedState = true;
        try
        {
            var codex = GetSource(ProviderSourceKind.OpenAiCodex);
            codex.HasAutoAuth = HasCodexAutoAuth;
            codex.AutoAuthSummary = CodexAutoAuthSummary;
            codex.ShowAdvancedSection = ShowCodexLimits && !HasCodexAutoAuth;
            codex.ShowSessionField = !HasCodexAutoAuth;
            codex.HasSessionSaved = HasOpenAiSessionCookieSaved;
            codex.SessionWatermark = OpenAiSessionCookieWatermark;
            codex.NotifyAdvancedVisibility();

            var direct = GetSource(ProviderSourceKind.OpenAiDirect);
            direct.ShowAdvancedSection = ShowOpenAiDirect;
            // Keep field visible after save so Clear / replace still work.
            direct.ShowApiKeyField = ShowOpenAiDirect;
            direct.ShowOrgIdField = ShowOpenAiDirect;
            direct.ShowBudgetField = ShowOpenAiDirect;
            direct.HasApiKeySaved = HasOpenAiApiKeySaved;
            direct.ApiKeyWatermark = OpenAiApiKeyWatermark;
            direct.NotifyAdvancedVisibility();

            var geminiLimits = GetSource(ProviderSourceKind.AntigravityLimits);
            geminiLimits.HasAutoAuth = HasGeminiAutoAuth;
            geminiLimits.AutoAuthSummary = GeminiAutoAuthSummary;
            geminiLimits.ShowAdvancedSection = ShowAntigravityLimits && !HasGeminiAutoAuth;
            geminiLimits.NotifyAdvancedVisibility();

            if (ProviderFeatureFlags.OpenRouterEnabled)
            {
                var openRouter = GetSource(ProviderSourceKind.OpenRouterCredits);
                openRouter.ShowAdvancedSection = ShowOpenRouterLimits;
                // Keep fields visible after save so Clear / replace still work.
                openRouter.ShowApiKeyField = ShowOpenRouterLimits;
                openRouter.ShowManagementApiKeyField = ShowOpenRouterLimits;
                openRouter.HasApiKeySaved = HasOpenRouterApiKeySaved;
                openRouter.HasManagementApiKeySaved = HasOpenRouterManagementApiKeySaved;
                openRouter.ApiKeyWatermark = OpenRouterApiKeyWatermark;
                openRouter.ManagementApiKeyWatermark = OpenRouterManagementApiKeyWatermark;
                openRouter.NotifyAdvancedVisibility();
            }

            var fal = GetSource(ProviderSourceKind.FalCredits);
            fal.ShowAdvancedSection = ShowFalLimits;
            // Keep field visible after save so Clear / replace still work.
            fal.ShowApiKeyField = ShowFalLimits;
            fal.HasApiKeySaved = HasFalApiKeySaved;
            fal.ApiKeyWatermark = FalApiKeyWatermark;
            fal.NotifyAdvancedVisibility();

            var xai = GetSource(ProviderSourceKind.XaiCredits);
            xai.ShowAdvancedSection = ShowXaiLimits;
            xai.ShowApiKeyField = ShowXaiLimits;
            xai.ShowWorkspaceField = ShowXaiLimits;
            xai.HasApiKeySaved = HasXaiApiKeySaved;
            xai.ApiKeyWatermark = XaiApiKeyWatermark;
            xai.WorkspaceWatermark = "Team UUID (optional)";
            xai.NotifyAdvancedVisibility();

            var claudePro = GetSource(ProviderSourceKind.ClaudePro);
            claudePro.HasAutoAuth = HasClaudeCodeAuth;
            claudePro.AutoAuthSummary = ClaudeCodeAutoAuthSummary;
            claudePro.ShowAdvancedSection = ShowClaudePro && ShowClaudeProCredentials;
            claudePro.ShowSessionField = ShowClaudeProCredentials;
            claudePro.HasSessionSaved = HasClaudeSessionCookieSaved;
            claudePro.SessionWatermark = ClaudeProSessionWatermark;
            claudePro.ShowSignInButton = ShowClaudeSignInButton;
            claudePro.ShowDisconnectOAuth = HasClaudeAppOAuthSaved;
            claudePro.IsOAuthPending = IsClaudeOAuthPending;
            claudePro.NotifyAdvancedVisibility();

            var claudeApi = GetSource(ProviderSourceKind.ClaudeApiConsole);
            claudeApi.ShowAdvancedSection = ShowClaudeApiConsole;
            // Keep field visible after save so Clear / replace still work.
            claudeApi.ShowApiKeyField = ShowClaudeApiConsole;
            claudeApi.ShowBudgetField = ShowClaudeApiConsole;
            claudeApi.HasApiKeySaved = HasClaudeApiKeySaved;
            claudeApi.ApiKeyWatermark = ClaudeApiKeyWatermark;
            claudeApi.NotifyAdvancedVisibility();

            OnPropertyChanged(nameof(ShowClaudeProCredentials));
            OnPropertyChanged(nameof(ShowClaudeApiConsoleCredentials));
            OnPropertyChanged(nameof(ShowClaudeSignInButton));
            OnPropertyChanged(nameof(IsClaudeOAuthPending));
            OnPropertyChanged(nameof(HasClaudeAppOAuthSaved));
            OnPropertyChanged(nameof(HasClaudeCodeAuth));

            var openCodeZen = GetSource(ProviderSourceKind.OpenCodeZen);
            openCodeZen.HasAutoAuth = HasOpenCodeAutoAuth;
            openCodeZen.AutoAuthSummary = OpenCodeAutoAuthSummary;
            openCodeZen.NotifyAdvancedVisibility();

            var openCodeGo = GetSource(ProviderSourceKind.OpenCodeGo);
            openCodeGo.HasAutoAuth = HasOpenCodeAutoAuth;
            openCodeGo.AutoAuthSummary = OpenCodeAutoAuthSummary;
            openCodeGo.ShowAdvancedSection = (ShowOpenCodeZen || ShowOpenCodeGo) && !HasOpenCodeApiKeyAuth;
            openCodeGo.ShowSessionField = ShowOpenCodeCredentials;
            openCodeGo.ShowWorkspaceField = !HasOpenCodeApiKeyAuth;
            openCodeGo.HasSessionSaved = HasOpenCodeSessionSaved;
            openCodeGo.SessionWatermark = OpenCodeSessionWatermark;
            openCodeGo.NotifyAdvancedVisibility();

            OnPropertyChanged(nameof(ShowOpenCodeCredentials));
            OnPropertyChanged(nameof(HasOpenCodeAutoAuth));
            OnPropertyChanged(nameof(HasOpenCodeApiKeyAuth));

            OnPropertyChanged(nameof(ShowCodexCredentials));
            OnPropertyChanged(nameof(ShowOpenAiDirectCredentials));
            OnPropertyChanged(nameof(HasCodexAutoAuth));
            OnPropertyChanged(nameof(HasGeminiAutoAuth));
        }
        finally
        {
            _updatingDerivedState = false;
        }
    }

    private void WireSectionNotifications()
    {
        foreach (var section in Sections)
        {
            section.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ProviderSettingsSectionViewModel.MasterEnable))
                    return;
                NotifyChanged();
            };

            foreach (var source in section.Sources)
            {
                source.PropertyChanged += (_, _) =>
                {
                    if (!_suppressChangeNotifications && !_updatingDerivedState)
                        NotifyChanged();
                };
            }
        }
    }

    private ProviderBillingSettings CreateOpenAiBillingSettings() => new()
    {
        ProSessionCredentialId = _openAiProSessionCredentialId
    };

    private ProviderBillingSettings CreateOpenCodeBillingSettings() => new()
    {
        ProSessionCredentialId = _openCodeProSessionCredentialId,
        WorkspaceId = _openCodeWorkspaceId
    };

    private void UpdateClaudeOAuthDerivedState()
    {
        if (Sections.Count == 0)
            return;

        var claudePro = GetSource(ProviderSourceKind.ClaudePro);
        claudePro.ShowSignInButton = ShowClaudeSignInButton;
        claudePro.ShowDisconnectOAuth = HasClaudeAppOAuthSaved;
        claudePro.IsOAuthPending = IsClaudeOAuthPending;
        claudePro.NotifyAdvancedVisibility();
        OnPropertyChanged(nameof(ShowClaudeSignInButton));
        OnPropertyChanged(nameof(IsClaudeOAuthPending));
        OnPropertyChanged(nameof(HasClaudeAppOAuthSaved));
    }

    private void NotifyAccordionPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsCursorExpanded));
        OnPropertyChanged(nameof(IsOpenAiExpanded));
        OnPropertyChanged(nameof(IsClaudeExpanded));
        OnPropertyChanged(nameof(IsGeminiExpanded));
        OnPropertyChanged(nameof(IsOpenRouterExpanded));
        OnPropertyChanged(nameof(IsOpenCodeExpanded));
        OnPropertyChanged(nameof(IsFalExpanded));
        OnPropertyChanged(nameof(IsXaiExpanded));
        OnPropertyChanged(nameof(IsDiskExpanded));
        OnPropertyChanged(nameof(IsHardwareExpanded));
    }

    private static string BuildWatermark(string label, string? credentialId) =>
        string.IsNullOrWhiteSpace(credentialId) ? label : $"{label} — saved";

    private static void SaveCredential(string provider, ref string? credentialId, string? text)
    {
        var id = credentialId;
        CredentialStore.Replace(provider, credentialId, text?.Trim() ?? "", newId => id = newId);
        credentialId = id;
    }
}

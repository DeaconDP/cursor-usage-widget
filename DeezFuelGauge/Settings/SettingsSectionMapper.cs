using System.Globalization;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

namespace DeezFuelGauge.Settings;

internal static class SettingsSectionMapper
{
    public static void PopulateSections(
        IList<ProviderSettingsSectionViewModel> sections,
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        sections.Clear();

        sections.Add(BuildCursorSection(settings, host));
        sections.Add(BuildOpenAiSection(settings, host));
        sections.Add(BuildClaudeSection(settings, host));
        sections.Add(BuildGeminiSection(settings, host));
        if (ProviderFeatureFlags.OpenRouterEnabled)
            sections.Add(BuildOpenRouterSection(settings, host));
        sections.Add(BuildOpenCodeSection(settings, host));
        sections.Add(BuildFalSection(settings, host));
        sections.Add(BuildXaiSection(settings, host));
        sections.Add(BuildDiskSection(settings, host));
        sections.Add(BuildHardwareSection(settings, host));

        foreach (var section in sections)
        {
            // Legacy saves may still point at the removed Grok Bot accordion — open Cursor instead.
            var expandedId = settings.SettingsExpandedProvider == SettingsExpandedProvider.GrokBot
                ? SettingsExpandedProvider.Cursor
                : settings.SettingsExpandedProvider;
            section.IsExpanded = expandedId == section.ProviderId;
        }
    }

    public static void ApplyToSettings(
        IList<ProviderSettingsSectionViewModel> sections,
        WidgetSettings settings,
        SettingsExpandedProvider expandedProvider)
    {
        foreach (var section in sections)
        {
            switch (section.ProviderId)
            {
                case SettingsExpandedProvider.Cursor:
                    ApplyCursor(section, settings);
                    break;
                case SettingsExpandedProvider.OpenAi:
                    ApplyOpenAi(section, settings);
                    break;
                case SettingsExpandedProvider.Claude:
                    ApplyClaude(section, settings);
                    break;
                case SettingsExpandedProvider.Gemini:
                    ApplyGemini(section, settings);
                    break;
                case SettingsExpandedProvider.OpenRouter:
                    ApplyOpenRouter(section, settings);
                    break;
                case SettingsExpandedProvider.OpenCode:
                    ApplyOpenCode(section, settings);
                    break;
                case SettingsExpandedProvider.Fal:
                    ApplyFal(section, settings);
                    break;
                case SettingsExpandedProvider.Xai:
                    ApplyXai(section, settings);
                    break;
                case SettingsExpandedProvider.Disk:
                    ApplyDisk(section, settings);
                    break;
                case SettingsExpandedProvider.Hardware:
                    ApplyHardware(section, settings);
                    break;
            }
        }

        settings.SettingsExpandedProvider = expandedProvider;
    }

    private static ProviderSettingsSectionViewModel BuildCursorSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var breakdown = new ProviderSourceViewModel
        {
            Kind = ProviderSourceKind.CursorWidget,
            Name = "Auto/API breakdown",
            HasEnableToggle = false,
            HasDetailsToggle = false,
            ShowBreakdown = settings.ShowBreakdown,
            HasBreakdownToggle = true,
            ShowConnect = false,
            ShowTest = false
        };

        var main = new ProviderSourceViewModel
        {
            Kind = ProviderSourceKind.CursorWidget,
            Name = "Cursor usage",
            IsEnabled = settings.Cursor.ShowCursorSource,
            ShowDetails = settings.Cursor.ShowDetails,
            Status = settings.Cursor.LastConnectionStatus ?? host.ResolveCursorStatus(),
            ShowConnect = true,
            ShowTest = true,
            HasLimitsToggle = false
        };

        var openAiViaCursor = CreateSource(
            ProviderSourceKind.OpenAiViaCursor,
            "OpenAI (via Cursor)",
            settings.OpenAi.ShowCursorSource,
            settings.OpenAi.ShowDetails,
            settings.OpenAi.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);

        var geminiViaCursor = CreateSource(
            ProviderSourceKind.GeminiViaCursor,
            "Gemini (via Cursor)",
            settings.Gemini.ShowCursorSource,
            settings.Gemini.ShowDetails,
            "",
            showConnect: true,
            showTest: true);

        var claudeViaCursor = CreateSource(
            ProviderSourceKind.ClaudeViaCursor,
            "Claude (via Cursor)",
            settings.Claude.ShowCursorSource,
            settings.Claude.ShowDetails,
            settings.Claude.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);

        var grokBot = CreateSource(
            ProviderSourceKind.GrokBotLimits,
            "Grok Bot",
            settings.GrokBot.ShowProLimits,
            settings.GrokBot.ShowDetails,
            settings.GrokBot.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        grokBot.AdvancedHint =
            "Grok Bot allowance via Cursor login (Ultra / Premium). No API key needed.";
        grokBot.ShowAdvancedSection = false;

        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.Cursor,
            Title = "Cursor",
            MasterEnable = HasAnyCursorDashboardSource(settings),
            SummaryStatus = main.Status
        };
        section.Sources.Add(main);
        section.Sources.Add(openAiViaCursor);
        section.Sources.Add(claudeViaCursor);
        section.Sources.Add(geminiViaCursor);
        section.Sources.Add(grokBot);
        section.Sources.Add(breakdown);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildClaudeSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var pro = CreateSource(
            ProviderSourceKind.ClaudePro,
            "Claude.ai",
            settings.Claude.ShowProLimits,
            settings.Claude.EffectiveShowProDetails,
            settings.Claude.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        pro.HasLimitsToggle = true;
        pro.ShowLimits = settings.Claude.ShowProBreakdown;
        pro.SupportsAdvanced = true;
        pro.HasAutoAuth = host.HasClaudeCodeAuth;
        pro.AutoAuthSummary = host.ClaudeCodeAutoAuthSummary;
        var showProCredentials = settings.Claude.ShowProLimits
                                 && !host.HasClaudeCodeAuth
                                 && !host.HasClaudeAppOAuthSaved
                                 && !host.HasClaudeSessionCookieSaved;
        pro.ShowSessionField = showProCredentials;
        pro.SessionWatermark = host.ClaudeProSessionWatermark;
        pro.HasSessionSaved = host.HasClaudeSessionCookieSaved;
        pro.ShowSignInButton = settings.Claude.ShowProLimits
                               && !host.HasClaudeCodeAuth
                               && !host.HasClaudeAppOAuthSaved;
        pro.ShowDisconnectOAuth = host.HasClaudeAppOAuthSaved;
        pro.IsOAuthPending = host.IsClaudeOAuthPending;
        pro.AdvancedHint =
            "Sign in with Claude, or run 'claude login'. Paste a session key in Advanced as fallback.";
        pro.ShowAdvancedSection = showProCredentials;

        var apiConsole = CreateSource(
            ProviderSourceKind.ClaudeApiConsole,
            "Claude API",
            settings.Claude.ShowApiConsoleBilling,
            settings.Claude.EffectiveShowDirectDetails,
            settings.Claude.LastConnectionStatus ?? "",
            showConnect: false,
            showTest: true);
        apiConsole.SupportsAdvanced = true;
        // Keep field visible after save so Clear / replace still work.
        apiConsole.ShowApiKeyField = settings.Claude.ShowApiConsoleBilling;
        apiConsole.ShowBudgetField = settings.Claude.ShowApiConsoleBilling;
        apiConsole.Budget = FormatBudget(settings.Claude.MonthlyBudgetUsd);
        apiConsole.ApiKeyWatermark = host.ClaudeApiKeyWatermark;
        apiConsole.HasApiKeySaved = host.HasClaudeApiKeySaved;
        apiConsole.AdvancedHint = "Anthropic Admin API key for Console spend vs monthly budget.";
        apiConsole.ShowAdvancedSection = settings.Claude.ShowApiConsoleBilling;

        var section = CreateSection(
            SettingsExpandedProvider.Claude,
            "Claude",
            pro.Status,
            apiConsole.Status);
        section.MasterEnable = settings.Claude.ShowProLimits || settings.Claude.ShowApiConsoleBilling;
        section.Sources.Add(pro);
        section.Sources.Add(apiConsole);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildOpenAiSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var direct = CreateSource(
            ProviderSourceKind.OpenAiDirect,
            "OpenAI API",
            settings.OpenAi.ShowDirectSource,
            settings.OpenAi.EffectiveShowDirectDetails,
            settings.OpenAi.LastConnectionStatus ?? "",
            showConnect: false,
            showTest: true);
        direct.SupportsAdvanced = true;
        // Keep field visible after save so Clear / replace still work.
        direct.ShowApiKeyField = settings.OpenAi.ShowDirectSource;
        direct.ShowOrgIdField = settings.OpenAi.ShowDirectSource;
        direct.ShowBudgetField = settings.OpenAi.ShowDirectSource;
        direct.OrgId = settings.OpenAi.OrganizationId ?? "";
        direct.Budget = FormatBudget(settings.OpenAi.MonthlyBudgetUsd);
        direct.ApiKeyWatermark = host.OpenAiApiKeyWatermark;
        direct.HasApiKeySaved = host.HasOpenAiApiKeySaved;
        direct.AdvancedHint =
            "API key for prepaid credit balance when available; Admin key with api.usage.read for spend vs monthly budget. Separate from ChatGPT/Codex.";
        direct.ShowAdvancedSection = settings.OpenAi.ShowDirectSource;

        var codex = CreateSource(
            ProviderSourceKind.OpenAiCodex,
            "Codex",
            settings.OpenAi.ShowProLimits,
            settings.OpenAi.EffectiveShowProDetails,
            settings.OpenAi.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        codex.HasLimitsToggle = true;
        codex.ShowLimits = settings.OpenAi.ShowProBreakdown;
        codex.SupportsAdvanced = true;
        codex.HasAutoAuth = host.HasCodexAutoAuth;
        codex.AutoAuthSummary = host.CodexAutoAuthSummary;
        codex.ShowSessionField = !host.HasCodexAutoAuth;
        codex.SessionWatermark = host.OpenAiSessionCookieWatermark;
        codex.HasSessionSaved = host.HasOpenAiSessionCookieSaved;
        codex.AdvancedHint = "Uses ~/.codex/auth.json when present.";
        codex.ShowAdvancedSection = settings.OpenAi.ShowProLimits && !host.HasCodexAutoAuth;

        var section = CreateSection(
            SettingsExpandedProvider.OpenAi,
            "OpenAI",
            codex.Status,
            direct.Status);
        section.MasterEnable = settings.OpenAi.ShowDirectSource || settings.OpenAi.ShowProLimits;
        section.Sources.Add(direct);
        section.Sources.Add(codex);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildGeminiSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var antigravity = CreateSource(
            ProviderSourceKind.AntigravityLimits,
            "Gemini App",
            settings.Gemini.ShowProLimits,
            settings.Gemini.EffectiveShowProDetails,
            settings.Gemini.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        antigravity.HasLimitsToggle = true;
        antigravity.ShowLimits = settings.Gemini.ShowProBreakdown;
        antigravity.SupportsAdvanced = true;
        antigravity.HasAutoAuth = host.HasGeminiAutoAuth;
        antigravity.AutoAuthSummary = host.GeminiAutoAuthSummary;
        antigravity.AdvancedHint =
            "Gemini App / IDE quotas via Antigravity IDE or gemini login (Gemini CLI). Gemini API billing is not metered yet.";
        antigravity.ShowAdvancedSection = settings.Gemini.ShowProLimits && !host.HasGeminiAutoAuth;

        var section = CreateSection(
            SettingsExpandedProvider.Gemini,
            "Gemini",
            antigravity.Status);
        section.MasterEnable = settings.Gemini.ShowProLimits;
        section.Sources.Add(antigravity);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildOpenRouterSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var credits = CreateSource(
            ProviderSourceKind.OpenRouterCredits,
            "Credits",
            settings.OpenRouter.ShowProLimits,
            settings.OpenRouter.ShowDetails,
            settings.OpenRouter.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        credits.SupportsAdvanced = true;
        // Keep fields visible after save so Clear / replace still work.
        credits.ShowApiKeyField = settings.OpenRouter.ShowProLimits;
        credits.ShowManagementApiKeyField = settings.OpenRouter.ShowProLimits;
        credits.ApiKeyWatermark = host.OpenRouterApiKeyWatermark;
        credits.ManagementApiKeyWatermark = host.OpenRouterManagementApiKeyWatermark;
        credits.HasApiKeySaved = host.HasOpenRouterApiKeySaved;
        credits.HasManagementApiKeySaved = host.HasOpenRouterManagementApiKeySaved;
        credits.AdvancedHint =
            "API key from openrouter.ai/settings/keys (usage and per-key limits via GET /key). " +
            "Optional management key from openrouter.ai/settings/management-keys for account balance.";
        credits.ShowAdvancedSection = settings.OpenRouter.ShowProLimits;

        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.OpenRouter,
            Title = "OpenRouter",
            MasterEnable = settings.OpenRouter.ShowProLimits,
            SummaryStatus = credits.Status
        };
        section.Sources.Add(credits);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildFalSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var credits = CreateSource(
            ProviderSourceKind.FalCredits,
            "Credits",
            settings.Fal.ShowProLimits,
            settings.Fal.ShowDetails,
            settings.Fal.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        credits.SupportsAdvanced = true;
        // Keep field visible after save so Clear / replace still work.
        credits.ShowApiKeyField = settings.Fal.ShowProLimits;
        credits.ApiKeyWatermark = host.FalApiKeyWatermark;
        credits.HasApiKeySaved = host.HasFalApiKeySaved;
        credits.AdvancedHint =
            "Admin API key from fal.ai/dashboard/keys (ADMIN scope, not API). Keys do not expire — " +
            "create a new one if revoked. Select the correct personal or team account before creating the key.";
        credits.ShowAdvancedSection = settings.Fal.ShowProLimits;

        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.Fal,
            Title = "fal.ai",
            MasterEnable = settings.Fal.ShowProLimits,
            SummaryStatus = credits.Status
        };
        section.Sources.Add(credits);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildXaiSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var credits = CreateSource(
            ProviderSourceKind.XaiCredits,
            "Credits",
            settings.Xai.ShowProLimits,
            settings.Xai.ShowDetails,
            settings.Xai.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        credits.SupportsAdvanced = true;
        // Keep field visible after save so Clear / replace still work.
        credits.ShowApiKeyField = settings.Xai.ShowProLimits;
        credits.ApiKeyWatermark = host.XaiApiKeyWatermark;
        credits.HasApiKeySaved = host.HasXaiApiKeySaved;
        credits.ShowWorkspaceField = settings.Xai.ShowProLimits;
        credits.WorkspaceWatermark = "Team UUID (optional)";
        credits.WorkspaceId = settings.Xai.WorkspaceId ?? "";
        credits.AdvancedHint =
            "Requires a Management key from console.x.ai → Settings → Management Keys. " +
            "Regular API / inference keys (API Keys page) return “invalid bearer token”. " +
            "Team UUID is usually auto-detected; if Connect fails, paste the UUID from team settings — not “default”.";
        credits.ShowAdvancedSection = settings.Xai.ShowProLimits;

        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.Xai,
            Title = "xAI",
            MasterEnable = settings.Xai.ShowProLimits,
            SummaryStatus = credits.Status
        };
        section.Sources.Add(credits);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildOpenCodeSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var zen = CreateSource(
            ProviderSourceKind.OpenCodeZen,
            "Zen",
            settings.OpenCode.ShowDirectSource,
            settings.OpenCode.ShowDetails,
            settings.OpenCode.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        zen.HasAutoAuth = host.HasOpenCodeAutoAuthFor(settings.OpenCode);
        zen.AutoAuthSummary = host.OpenCodeAutoAuthSummaryFor(settings.OpenCode);
        zen.AdvancedHint = "Uses ~/.local/share/opencode/auth.json when present.";

        var go = CreateSource(
            ProviderSourceKind.OpenCodeGo,
            "Go",
            settings.OpenCode.ShowProLimits,
            settings.OpenCode.EffectiveShowProDetails,
            settings.OpenCode.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        go.HasLimitsToggle = true;
        go.ShowLimits = settings.OpenCode.ShowProBreakdown;
        go.HasAutoAuth = host.HasOpenCodeAutoAuthFor(settings.OpenCode);
        go.AutoAuthSummary = host.OpenCodeAutoAuthSummaryFor(settings.OpenCode);
        go.SupportsAdvanced = true;
        go.ShowWorkspaceField = !host.HasOpenCodeApiKeyAuth;
        go.ShowSessionField = host.ShowOpenCodeCredentialsFor(settings.OpenCode);
        go.WorkspaceId = settings.OpenCode.WorkspaceId ?? "";
        go.SessionWatermark = host.OpenCodeSessionWatermark;
        go.HasSessionSaved = host.HasOpenCodeSessionSaved;
        go.AdvancedHint = host.HasOpenCodeApiKeyAuth
            ? "Dashboard fallback: auth cookie + workspace ID from opencode.ai."
            : "Sign in at opencode.ai or run opencode /connect; set workspace ID if needed.";
        go.ShowAdvancedSection = (settings.OpenCode.ShowDirectSource || settings.OpenCode.ShowProLimits)
                                 && !host.HasOpenCodeApiKeyAuth;

        var section = CreateSection(
            SettingsExpandedProvider.OpenCode,
            "OpenCode",
            zen.Status,
            go.Status);
        section.MasterEnable = settings.OpenCode.HasAnyDashboardSource;
        section.Sources.Add(zen);
        section.Sources.Add(go);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildDiskSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var drives = CreateSource(
            ProviderSourceKind.DiskDrives,
            "Local drives",
            settings.ShowDiskDrives,
            settings.ShowDiskDetails,
            "",
            showConnect: false,
            showTest: false);
        drives.HasDetailsToggle = true;
        drives.ShowDetails = settings.ShowDiskDetails;

        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.Disk,
            Title = "Disk",
            MasterEnable = settings.ShowDiskDrives,
            SummaryStatus = BuildDiskSummaryStatus(settings)
        };
        section.Sources.Add(drives);

        var disabled = settings.DisabledDiskDrives ?? [];
        foreach (var drive in DiskSpaceProvider.GetDriveDescriptors())
        {
            var source = CreateSource(
                ProviderSourceKind.DiskDrive,
                drive.DisplayLabel,
                !DiskSpaceProvider.IsDriveDisabled(disabled, drive.Name),
                details: false,
                "",
                showConnect: false,
                showTest: false);
            source.DrivePath = drive.Name;
            source.HasDetailsToggle = false;
            section.Sources.Add(source);
        }

        return section;
    }

    private static ProviderSourceViewModel CreateSource(
        ProviderSourceKind kind,
        string name,
        bool enabled,
        bool details,
        string status,
        bool showConnect,
        bool showTest) => new()
    {
        Kind = kind,
        Name = name,
        IsEnabled = enabled,
        ShowDetails = details,
        Status = status,
        ShowConnect = showConnect,
        ShowTest = showTest
    };

    private static ProviderSettingsSectionViewModel CreateSection(
        SettingsExpandedProvider id,
        string title,
        params string[] statuses)
    {
        var summary = statuses.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
        return new ProviderSettingsSectionViewModel
        {
            ProviderId = id,
            Title = title,
            SummaryStatus = summary
        };
    }

    private static bool HasAnyCursorDashboardSource(WidgetSettings settings) =>
        settings.Cursor.ShowCursorSource
        || settings.OpenAi.ShowCursorSource
        || settings.Claude.ShowCursorSource
        || settings.Gemini.ShowCursorSource
        || settings.GrokBot.ShowProLimits;

    private static void ApplyCursor(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var main = section.Sources.First(s => s.Kind == ProviderSourceKind.CursorWidget && s.HasEnableToggle);
        var openAiViaCursor = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenAiViaCursor);
        var claudeViaCursor = section.Sources.First(s => s.Kind == ProviderSourceKind.ClaudeViaCursor);
        var geminiViaCursor = section.Sources.First(s => s.Kind == ProviderSourceKind.GeminiViaCursor);
        var grokBot = section.Sources.First(s => s.Kind == ProviderSourceKind.GrokBotLimits);
        var breakdown = section.Sources.First(s => s.HasBreakdownToggle);

        settings.Cursor.ShowCursorSource = main.IsEnabled;
        settings.Cursor.ShowDetails = main.ShowDetails;
        settings.Cursor.LastConnectionStatus = NullIfEmpty(main.Status);
        settings.OpenAi.ShowCursorSource = openAiViaCursor.IsEnabled;
        settings.OpenAi.ShowDetails = openAiViaCursor.ShowDetails;
        settings.OpenAi.LastConnectionStatus = NullIfEmpty(openAiViaCursor.Status);
        settings.Claude.ShowCursorSource = claudeViaCursor.IsEnabled;
        settings.Claude.ShowDetails = claudeViaCursor.ShowDetails;
        settings.Claude.LastConnectionStatus = NullIfEmpty(claudeViaCursor.Status);
        settings.Gemini.ShowCursorSource = geminiViaCursor.IsEnabled;
        settings.Gemini.ShowDetails = geminiViaCursor.ShowDetails;
        settings.GrokBot.ShowProLimits = grokBot.IsEnabled;
        settings.GrokBot.ShowDetails = grokBot.ShowDetails;
        settings.GrokBot.LastConnectionStatus = NullIfEmpty(grokBot.Status);
        settings.ShowBreakdown = breakdown.ShowBreakdown;
        section.MasterEnable = HasAnyCursorDashboardSource(settings);
    }

    private static void ApplyOpenAi(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var direct = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenAiDirect);
        var codex = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenAiCodex);

        settings.OpenAi.ShowDirectSource = direct.IsEnabled;
        settings.OpenAi.ShowDirectDetails = direct.ShowDetails;
        settings.OpenAi.OrganizationId = NullIfEmpty(direct.OrgId);
        settings.OpenAi.MonthlyBudgetUsd = ParseBudget(direct.Budget);
        settings.OpenAi.ShowProLimits = codex.IsEnabled;
        settings.OpenAi.ShowProDetails = codex.ShowDetails;
        settings.OpenAi.ShowProBreakdown = codex.ShowLimits;
        settings.OpenAi.ProLastConnectionStatus = NullIfEmpty(codex.Status);
        section.MasterEnable = settings.OpenAi.ShowDirectSource || settings.OpenAi.ShowProLimits;
    }

    private static void ApplyClaude(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var pro = section.Sources.First(s => s.Kind == ProviderSourceKind.ClaudePro);
        var apiConsole = section.Sources.First(s => s.Kind == ProviderSourceKind.ClaudeApiConsole);

        settings.Claude.ShowProLimits = pro.IsEnabled;
        settings.Claude.ShowProDetails = pro.ShowDetails;
        settings.Claude.ShowProBreakdown = pro.ShowLimits;
        settings.Claude.ProLastConnectionStatus = NullIfEmpty(pro.Status);
        settings.Claude.ShowApiConsoleBilling = apiConsole.IsEnabled;
        settings.Claude.ShowDirectDetails = apiConsole.ShowDetails;
        settings.Claude.MonthlyBudgetUsd = ParseBudget(apiConsole.Budget);
        settings.Claude.LastConnectionStatus = NullIfEmpty(apiConsole.Status);
        section.MasterEnable = settings.Claude.ShowProLimits || settings.Claude.ShowApiConsoleBilling;
    }

    private static void ApplyGemini(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var antigravity = section.Sources.First(s => s.Kind == ProviderSourceKind.AntigravityLimits);

        settings.Gemini.ShowProLimits = antigravity.IsEnabled;
        settings.Gemini.ShowProDetails = antigravity.ShowDetails;
        settings.Gemini.ShowProBreakdown = antigravity.ShowLimits;
        settings.Gemini.ProLastConnectionStatus = NullIfEmpty(antigravity.Status);
        section.MasterEnable = settings.Gemini.ShowProLimits;
    }

    private static void ApplyOpenRouter(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var credits = section.Sources.Single();
        settings.OpenRouter.ShowProLimits = credits.IsEnabled;
        settings.OpenRouter.ShowDetails = credits.ShowDetails;
        settings.OpenRouter.LastConnectionStatus = NullIfEmpty(credits.Status);
        section.MasterEnable = credits.IsEnabled;
    }

    private static void ApplyFal(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var credits = section.Sources.Single();
        settings.Fal.ShowProLimits = credits.IsEnabled;
        settings.Fal.ShowDetails = credits.ShowDetails;
        settings.Fal.LastConnectionStatus = NullIfEmpty(credits.Status);
        section.MasterEnable = credits.IsEnabled;
    }

    private static void ApplyXai(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var credits = section.Sources.Single();
        settings.Xai.ShowProLimits = credits.IsEnabled;
        settings.Xai.ShowDetails = credits.ShowDetails;
        settings.Xai.WorkspaceId = NullIfEmpty(credits.WorkspaceId);
        settings.Xai.LastConnectionStatus = NullIfEmpty(credits.Status);
        section.MasterEnable = credits.IsEnabled;
    }

    private static void ApplyOpenCode(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var zen = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenCodeZen);
        var go = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenCodeGo);

        settings.OpenCode.ShowDirectSource = zen.IsEnabled;
        settings.OpenCode.ShowDetails = zen.ShowDetails;
        settings.OpenCode.ShowProLimits = go.IsEnabled;
        settings.OpenCode.ShowProDetails = go.ShowDetails;
        settings.OpenCode.ShowProBreakdown = go.ShowLimits;
        settings.OpenCode.WorkspaceId = NullIfEmpty(go.WorkspaceId);
        settings.OpenCode.ProLastConnectionStatus = NullIfEmpty(zen.Status) ?? NullIfEmpty(go.Status);
        section.MasterEnable = settings.OpenCode.HasAnyDashboardSource;
    }

    private static void ApplyDisk(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var drives = section.Sources.First(s => s.Kind == ProviderSourceKind.DiskDrives);
        settings.ShowDiskDrives = drives.IsEnabled;
        settings.ShowDiskDetails = drives.ShowDetails;
        settings.DisabledDiskDrives = section.Sources
            .Where(s => s.Kind == ProviderSourceKind.DiskDrive
                        && !string.IsNullOrWhiteSpace(s.DrivePath)
                        && !s.IsEnabled)
            .Select(s => s.DrivePath!)
            .ToList();
        section.MasterEnable = drives.IsEnabled;
        section.SummaryStatus = BuildDiskSummaryStatus(settings, section);
    }

    internal static void ApplyDiskSummary(ProviderSettingsSectionViewModel section, bool enabled) =>
        section.SummaryStatus = BuildDiskSummaryStatus(
            new WidgetSettings { ShowDiskDrives = enabled },
            section);

    private static string BuildDiskSummaryStatus(
        WidgetSettings settings,
        ProviderSettingsSectionViewModel? section = null)
    {
        if (!settings.ShowDiskDrives)
            return "Off";

        var enabledCount = section?.Sources.Count(s =>
                               s.Kind == ProviderSourceKind.DiskDrive && s.IsEnabled)
                           ?? DiskSpaceProvider.GetDriveDescriptors()
                               .Count(d => !DiskSpaceProvider.IsDriveDisabled(
                                   settings.DisabledDiskDrives ?? [], d.Name));

        return enabledCount switch
        {
            0 => "No drives",
            1 => "1 drive",
            _ => $"{enabledCount} drives"
        };
    }

    private static ProviderSettingsSectionViewModel BuildHardwareSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var cpu = CreateSource(
            ProviderSourceKind.HardwareCpuUsage,
            "CPU usage",
            settings.ShowCpuUsage,
            false,
            "",
            showConnect: false,
            showTest: false);
        cpu.HasDetailsToggle = false;

        var gpu = CreateSource(
            ProviderSourceKind.HardwareGpuUsage,
            "GPU usage",
            settings.ShowGpuUsage,
            false,
            "",
            showConnect: false,
            showTest: false);
        gpu.HasDetailsToggle = false;

        var ram = CreateSource(
            ProviderSourceKind.HardwareRamUsage,
            "RAM usage",
            settings.ShowRamUsage,
            settings.ShowHardwareDetails,
            "",
            showConnect: false,
            showTest: false);

        var cpuTemp = CreateSource(
            ProviderSourceKind.HardwareCpuTemp,
            "CPU temp",
            settings.ShowCpuTemp,
            settings.ShowCpuTempDetail,
            "",
            showConnect: false,
            showTest: false);

        var anyEnabled = settings.ShowCpuUsage
                           || settings.ShowGpuUsage
                           || settings.ShowRamUsage
                           || settings.ShowCpuTemp;
        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.Hardware,
            Title = "Hardware",
            MasterEnable = anyEnabled,
            SummaryStatus = anyEnabled ? "Enabled" : "Off"
        };
        section.Sources.Add(cpu);
        section.Sources.Add(gpu);
        section.Sources.Add(ram);
        section.Sources.Add(cpuTemp);
        return section;
    }

    private static void ApplyHardware(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var cpu = section.Sources.First(s => s.Kind == ProviderSourceKind.HardwareCpuUsage);
        var gpu = section.Sources.First(s => s.Kind == ProviderSourceKind.HardwareGpuUsage);
        var ram = section.Sources.First(s => s.Kind == ProviderSourceKind.HardwareRamUsage);
        var cpuTemp = section.Sources.First(s => s.Kind == ProviderSourceKind.HardwareCpuTemp);

        settings.ShowCpuUsage = cpu.IsEnabled;
        settings.ShowGpuUsage = gpu.IsEnabled;
        settings.ShowRamUsage = ram.IsEnabled;
        settings.ShowCpuTemp = cpuTemp.IsEnabled;
        settings.ShowCpuTempDetail = cpuTemp.ShowDetails;
        settings.ShowHardwareDetails = ram.ShowDetails;

        var anyEnabled = settings.ShowCpuUsage
                         || settings.ShowGpuUsage
                         || settings.ShowRamUsage
                         || settings.ShowCpuTemp;
        section.MasterEnable = anyEnabled;
        section.SummaryStatus = anyEnabled ? "Enabled" : "Off";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? ParseBudget(string? text) =>
        decimal.TryParse(text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;

    private static string FormatBudget(decimal? value) =>
        value is > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : "";
}

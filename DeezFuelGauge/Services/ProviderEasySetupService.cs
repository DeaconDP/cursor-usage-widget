using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class ProviderEasySetupService
{
    private readonly CodexUsageClient _codex;
    private readonly ClaudeProUsageClient _claudePro;
    private readonly AntigravityUsageClient _antigravity;
    private readonly OpenRouterUsageClient _openRouter;
    private readonly FalUsageClient _fal;
    private readonly XaiUsageClient _xai;
    private readonly GrokBotUsageClient? _grokBot;
    private readonly ExternalSetupLauncher _launcher;
    private readonly Func<CursorTokens> _cursorTokenReader;
    private readonly GeminiAuthResolver _geminiAuthResolver;
    private readonly CodexAuthResolver _codexAuthResolver;
    private readonly OpenCodeAuthResolver _openCodeAuthResolver;

    public ProviderEasySetupService(
        CodexUsageClient? codex = null,
        ClaudeProUsageClient? claudePro = null,
        AntigravityUsageClient? antigravity = null,
        OpenRouterUsageClient? openRouter = null,
        FalUsageClient? fal = null,
        XaiUsageClient? xai = null,
        GrokBotUsageClient? grokBot = null,
        ExternalSetupLauncher? launcher = null,
        Func<CursorTokens>? cursorTokenReader = null,
        GeminiAuthResolver? geminiAuthResolver = null,
        CodexAuthResolver? codexAuthResolver = null,
        OpenCodeAuthResolver? openCodeAuthResolver = null)
    {
        _codex = codex ?? new CodexUsageClient();
        _claudePro = claudePro ?? new ClaudeProUsageClient();
        _antigravity = antigravity ?? new AntigravityUsageClient();
        _openRouter = openRouter ?? new OpenRouterUsageClient();
        _fal = fal ?? new FalUsageClient();
        _xai = xai ?? new XaiUsageClient();
        _grokBot = grokBot;
        _launcher = launcher ?? new ExternalSetupLauncher();
        _cursorTokenReader = cursorTokenReader ?? CursorTokenReader.Read;
        _geminiAuthResolver = geminiAuthResolver ?? new GeminiAuthResolver();
        _codexAuthResolver = codexAuthResolver ?? new CodexAuthResolver();
        _openCodeAuthResolver = openCodeAuthResolver ?? new OpenCodeAuthResolver();
    }

    public EasySetupResult SetupCursor(WidgetSettings settings)
    {
        settings.Cursor.ShowCursorSource = true;
        settings.Cursor.ShowDetails = true;
        settings.ShowBreakdown = true;
        settings.GrokBot.ShowProLimits = true;
        settings.GrokBot.ShowDetails = true;

        var tokens = _cursorTokenReader();
        if (!string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            const string connected = "Connected via Cursor session";
            settings.Cursor.LastConnectionStatus = connected;
            settings.GrokBot.LastConnectionStatus = connected;
            return new EasySetupResult(connected);
        }

        var launch = _launcher.LaunchCursorIde();
        const string message = "Sign in to Cursor IDE, then click Test";
        settings.Cursor.LastConnectionStatus = message;
        return launch switch
        {
            AppLaunchResult.Launched => new EasySetupResult(message, OpenedExternalUrl: true),
            AppLaunchResult.OpenedFallbackUrl => new EasySetupResult(
                "Open Cursor IDE and sign in, then click Test",
                OpenedExternalUrl: true),
            _ => new EasySetupResult("Could not launch Cursor IDE — open it manually, then click Test")
        };
    }

    public async Task<EasySetupResult> SetupCodexAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.OpenAi.ShowProLimits = true;
        settings.OpenAi.ShowProDetails ??= true;

        if (TryLaunchCodexWhenAuthMissing(settings, out var launchResult))
            return launchResult;

        var session = CredentialStore.Retrieve(settings.OpenAi.ProSessionCredentialId);
        var status = await _codex.TestConnectionAsync(settings.OpenAi, cancellationToken);

        if (!status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
        {
            var relaunch = TryLaunchCodexWhenAuthMissing(settings, out launchResult);
            if (relaunch)
                return launchResult;
        }

        settings.OpenAi.ProLastConnectionStatus = status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
            ? $"Codex: {status}"
            : status;
        return new EasySetupResult(status);
    }

    public Task<EasySetupResult> SetupOpenAiAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.OpenAi.ShowCursorSource = true;
        settings.OpenAi.ShowDetails = true;

        var cursorTokens = _cursorTokenReader();
        settings.OpenAi.LastConnectionStatus = string.IsNullOrWhiteSpace(cursorTokens.AccessToken)
            ? "Via Cursor: sign in to Cursor IDE"
            : "Via Cursor: connected";

        return Task.FromResult(new EasySetupResult(settings.OpenAi.LastConnectionStatus));
    }

    public Task<EasySetupResult> SetupGeminiViaCursorAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Gemini.ShowCursorSource = true;
        settings.Gemini.ShowDetails = true;
        return Task.FromResult(new EasySetupResult("Via Cursor: enabled"));
    }

    public async Task<EasySetupResult> SetupClaudeAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Claude.ShowProLimits = true;

        var status = await _claudePro.RefreshAndConnectAsync(settings.Claude, cancellationToken);
        if (status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
            return new EasySetupResult(status);

        _launcher.OpenClaudeAi();
        const string message = "Run 'claude login', or paste a session key in Settings, then click Refresh";
        settings.Claude.ProLastConnectionStatus = message;
        return new EasySetupResult(message, OpenedExternalUrl: true);
    }

    public async Task<EasySetupResult> SetupGeminiAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Gemini.ShowProLimits = true;

        if (!_geminiAuthResolver.HasDetectableAuth())
        {
            if (TryLaunchGeminiWhenAuthMissing(settings, out var launchResult))
                return launchResult;
        }

        var status = await _antigravity.TestConnectionAsync(cancellationToken);
        settings.Gemini.ProLastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    public async Task<EasySetupResult> SetupOpenRouterAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.OpenRouter.ShowProLimits = true;
        settings.OpenRouter.ShowDetails = true;

        var apiKey = CredentialStore.Retrieve(settings.OpenRouter.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _launcher.OpenOpenRouter();
            const string message = "Paste your OpenRouter API key (sk-or-...) in Advanced. Optional management key adds account balance.";
            settings.OpenRouter.LastConnectionStatus = message;
            return new EasySetupResult(message, OpenedExternalUrl: true);
        }

        var status = await _openRouter.TestConnectionAsync(apiKey, cancellationToken);
        settings.OpenRouter.LastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    public async Task<EasySetupResult> SetupFalAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Fal.ShowProLimits = true;
        settings.Fal.ShowDetails = true;

        var apiKey = CredentialStore.Retrieve(settings.Fal.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _launcher.OpenFal();
            const string message = "Paste your fal.ai Admin API key in Advanced.";
            settings.Fal.LastConnectionStatus = message;
            return new EasySetupResult(message, OpenedExternalUrl: true);
        }

        var status = await _fal.TestConnectionAsync(apiKey, cancellationToken);
        settings.Fal.LastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    public async Task<EasySetupResult> SetupXaiAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Xai.ShowProLimits = true;
        settings.Xai.ShowDetails = true;

        var apiKey = CredentialStore.Retrieve(settings.Xai.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _launcher.OpenXai();
            const string message = "Paste your xAI Management API key in Advanced (team UUID optional for team-scoped keys).";
            settings.Xai.LastConnectionStatus = message;
            return new EasySetupResult(message, OpenedExternalUrl: true);
        }

        var status = await _xai.TestConnectionAsync(apiKey, settings.Xai.WorkspaceId, cancellationToken);
        settings.Xai.LastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    public Task<EasySetupResult> SetupOpenCodeAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.OpenCode.ShowDirectSource = true;
        settings.OpenCode.ShowProLimits = true;
        settings.OpenCode.ShowDetails = true;

        if (_openCodeAuthResolver.HasApiKeyAuth())
            return Task.FromResult(new EasySetupResult("Connected via OpenCode CLI"));

        if (_openCodeAuthResolver.HasDetectableAuth(settings.OpenCode))
            return Task.FromResult(new EasySetupResult(settings.OpenCode.ProLastConnectionStatus ?? "Connected"));

        _launcher.OpenOpenCode();
        const string message =
            "Sign in at opencode.ai or run opencode /connect; set workspace ID in Advanced if needed";
        settings.OpenCode.ProLastConnectionStatus = message;
        return Task.FromResult(new EasySetupResult(message, OpenedExternalUrl: true));
    }

    public async Task<EasySetupResult> SetupGrokBotAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.GrokBot.ShowProLimits = true;
        settings.GrokBot.ShowDetails = true;

        var tokens = _cursorTokenReader();
        if (string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            var launch = _launcher.LaunchCursorIde();
            const string message = "Sign in to Cursor IDE (Ultra / Premium), then click Test";
            settings.GrokBot.LastConnectionStatus = message;
            return launch switch
            {
                AppLaunchResult.Launched => new EasySetupResult(message, OpenedExternalUrl: true),
                AppLaunchResult.OpenedFallbackUrl => new EasySetupResult(
                    "Open Cursor IDE and sign in, then click Test",
                    OpenedExternalUrl: true),
                _ => new EasySetupResult("Could not launch Cursor IDE — open it manually, then click Test")
            };
        }

        var client = _grokBot ?? new GrokBotUsageClient(tokenReader: _cursorTokenReader);
        client.SetTokens(tokens.AccessToken, tokens.RefreshToken);
        var status = await client.TestConnectionAsync(settings.GrokBot, cancellationToken);
        settings.GrokBot.LastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    public EasySetupResult SetupDisk(WidgetSettings settings)
    {
        settings.ShowDiskDrives = true;
        settings.ShowDiskDetails = true;
        return new EasySetupResult("Disk drives enabled");
    }

    private bool TryLaunchCodexWhenAuthMissing(WidgetSettings settings, out EasySetupResult result)
    {
        if (_codexAuthResolver.HasDetectableAuth(settings.OpenAi))
        {
            result = default!;
            return false;
        }

        var launchedTerminal = _launcher.TryLaunchCodexLogin();
        _launcher.OpenChatGpt();

        string message;
        if (launchedTerminal)
        {
            message = "Codex: run codex login in the terminal, or paste a ChatGPT session cookie in Advanced";
        }
        else
        {
            message =
                "Install Codex CLI (npm i -g @openai/codex), run codex login, or paste a ChatGPT session cookie in Advanced";
        }

        settings.OpenAi.ProLastConnectionStatus = message;
        result = new EasySetupResult(message, LaunchedCodexLogin: launchedTerminal, OpenedExternalUrl: true);
        return true;
    }

    private bool TryLaunchGeminiWhenAuthMissing(WidgetSettings settings, out EasySetupResult result)
    {
        if (_geminiAuthResolver.HasDetectableAuth())
        {
            result = default!;
            return false;
        }

        var launchedTerminal = _launcher.TryLaunchGeminiLogin();
        if (!launchedTerminal)
            _launcher.LaunchAntigravityIde();

        string message;
        if (launchedTerminal)
        {
            message = "Gemini: sign in via gemini in the terminal, or sign in to Antigravity IDE, then click Test";
        }
        else
        {
            message =
                "Install Gemini CLI (npm i -g @google/gemini-cli) and run gemini, or sign in to Antigravity IDE, then click Test";
        }

        settings.Gemini.ProLastConnectionStatus = message;
        result = new EasySetupResult(message, OpenedExternalUrl: true);
        return true;
    }
}

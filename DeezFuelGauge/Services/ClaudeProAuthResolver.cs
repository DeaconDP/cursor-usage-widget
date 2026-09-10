using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public enum ClaudeProAuthSource
{
    None,
    ClaudeCodeOAuth,
    AppOAuth,
    SavedSession
}

public sealed class ClaudeProAuthResult
{
    public ClaudeProAuthSource Source { get; init; }
    public string? OAuthAccessToken { get; init; }
    public string? SessionCookie { get; init; }
    public ClaudeOAuthToken? AppOAuthToken { get; init; }
    public string? FailureMessage { get; init; }

    public bool HasAuth =>
        !string.IsNullOrWhiteSpace(OAuthAccessToken) || !string.IsNullOrWhiteSpace(SessionCookie);

    public static ClaudeProAuthResult FromOAuth(string accessToken) => new()
    {
        Source = ClaudeProAuthSource.ClaudeCodeOAuth,
        OAuthAccessToken = accessToken
    };

    public static ClaudeProAuthResult FromAppOAuth(ClaudeOAuthToken token) => new()
    {
        Source = ClaudeProAuthSource.AppOAuth,
        OAuthAccessToken = token.AccessToken,
        AppOAuthToken = token
    };

    public static ClaudeProAuthResult FromSavedSession(string sessionCookie) => new()
    {
        Source = ClaudeProAuthSource.SavedSession,
        SessionCookie = sessionCookie
    };

    public static ClaudeProAuthResult Failed(string message) => new()
    {
        Source = ClaudeProAuthSource.None,
        FailureMessage = message
    };
}

public sealed class ClaudeProAuthResolver
{
    private readonly Func<ClaudeCodeOAuthCredential?> _claudeCodeReader;
    private readonly Func<string?, ClaudeOAuthToken?> _appOAuthReader;
    private readonly Func<string?, string?> _savedSessionReader;

    public ClaudeProAuthResolver(
        Func<ClaudeCodeOAuthCredential?>? claudeCodeReader = null,
        Func<string?, ClaudeOAuthToken?>? appOAuthReader = null,
        Func<string?, string?>? savedSessionReader = null)
    {
        _claudeCodeReader = claudeCodeReader ?? ClaudeCodeTokenReader.Read;
        _appOAuthReader = appOAuthReader ?? ClaudeOAuthTokenStore.Retrieve;
        _savedSessionReader = savedSessionReader ?? (id => CredentialStore.Retrieve(id));
    }

    public ClaudeProAuthResult Resolve(ProviderBillingSettings settings)
    {
        string? expiredClaudeCodeMessage = null;
        var oauth = _claudeCodeReader();
        if (oauth is not null)
        {
            if (!oauth.IsExpired)
                return ClaudeProAuthResult.FromOAuth(oauth.AccessToken);

            expiredClaudeCodeMessage = "Claude Code token expired — run claude login again";
        }

        var appToken = _appOAuthReader(settings.ProOAuthCredentialId);
        if (appToken is not null)
            return ClaudeProAuthResult.FromAppOAuth(appToken);

        var savedSession = _savedSessionReader(settings.ProSessionCredentialId);
        if (!string.IsNullOrWhiteSpace(savedSession))
            return ClaudeProAuthResult.FromSavedSession(savedSession);

        return ClaudeProAuthResult.Failed(
            expiredClaudeCodeMessage ?? "Sign in with Claude in Settings, or run 'claude login'");
    }

    public static void PersistSessionKey(ProviderBillingSettings settings, string sessionCookie)
    {
        CredentialStore.Replace(
            "claude-pro",
            settings.ProSessionCredentialId,
            sessionCookie,
            id => settings.ProSessionCredentialId = id);
    }
}

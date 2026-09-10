using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeProAuthResolverTests
{
    [Fact]
    public void Resolve_prefers_claude_code_oauth_over_saved_session()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token" },
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.ClaudeCodeOAuth, result.Source);
        Assert.Equal("oauth-token", result.OAuthAccessToken);
    }

    [Fact]
    public void Resolve_uses_saved_session_when_oauth_missing()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => null,
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.SavedSession, result.Source);
        Assert.Equal("saved-session", result.SessionCookie);
    }

    [Fact]
    public void Resolve_prefers_app_oauth_over_saved_session_when_claude_code_missing()
    {
        var appToken = new ClaudeOAuthToken { AccessToken = "app-oauth-token", ExpiresAtUnixMs = long.MaxValue };
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => null,
            appOAuthReader: _ => appToken,
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.AppOAuth, result.Source);
        Assert.Equal("app-oauth-token", result.OAuthAccessToken);
        Assert.Same(appToken, result.AppOAuthToken);
    }

    [Fact]
    public void Resolve_uses_saved_session_when_claude_code_token_expired()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token", ExpiresAt = 0 },
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.SavedSession, result.Source);
        Assert.Equal("saved-session", result.SessionCookie);
    }

    [Fact]
    public void Resolve_returns_expired_message_when_claude_code_token_expired_and_no_fallback()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token", ExpiresAt = 0 },
            appOAuthReader: _ => null,
            savedSessionReader: _ => null);

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.False(result.HasAuth);
        Assert.Contains("claude login", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_uses_app_oauth_when_claude_code_token_expired()
    {
        var appToken = new ClaudeOAuthToken { AccessToken = "app-oauth-token", ExpiresAtUnixMs = long.MaxValue };
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token", ExpiresAt = 0 },
            appOAuthReader: _ => appToken,
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.AppOAuth, result.Source);
        Assert.Equal("app-oauth-token", result.OAuthAccessToken);
        Assert.Same(appToken, result.AppOAuthToken);
    }

    [Fact]
    public void Resolve_returns_failure_when_no_auth_found()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => null,
            savedSessionReader: _ => null);

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.False(result.HasAuth);
        Assert.Contains("claude login", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistSessionKey_stores_encrypted_credential_id()
    {
        var settings = new ProviderBillingSettings();

        try
        {
            ClaudeProAuthResolver.PersistSessionKey(settings, "session-key");

            Assert.False(string.IsNullOrWhiteSpace(settings.ProSessionCredentialId));
            Assert.Equal("session-key", CredentialStore.Retrieve(settings.ProSessionCredentialId));
        }
        finally
        {
            CredentialStore.Delete(settings.ProSessionCredentialId);
        }
    }
}

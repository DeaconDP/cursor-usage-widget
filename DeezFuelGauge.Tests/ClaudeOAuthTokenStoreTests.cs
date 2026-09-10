using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeOAuthTokenStoreTests
{
    [Fact]
    public void Persist_twice_keeps_same_credential_id()
    {
        var settings = new ProviderBillingSettings();

        try
        {
            ClaudeOAuthTokenStore.Persist(settings, new ClaudeOAuthToken
            {
                AccessToken = "first-access",
                RefreshToken = "first-refresh",
                ExpiresAtUnixMs = 1
            });

            var firstId = settings.ProOAuthCredentialId;

            ClaudeOAuthTokenStore.Persist(settings, new ClaudeOAuthToken
            {
                AccessToken = "second-access",
                RefreshToken = "second-refresh",
                ExpiresAtUnixMs = 2
            });

            Assert.Equal(firstId, settings.ProOAuthCredentialId);

            var persisted = ClaudeOAuthTokenStore.Retrieve(settings.ProOAuthCredentialId);
            Assert.NotNull(persisted);
            Assert.Equal("second-access", persisted!.AccessToken);
            Assert.Equal("second-refresh", persisted.RefreshToken);
        }
        finally
        {
            ClaudeOAuthTokenStore.Clear(settings);
        }
    }

    [Fact]
    public void Persist_preserves_previous_refresh_token_when_omitted()
    {
        var settings = new ProviderBillingSettings();
        var previous = new ClaudeOAuthToken
        {
            AccessToken = "old-access",
            RefreshToken = "keep-refresh",
            ExpiresAtUnixMs = 1
        };

        try
        {
            ClaudeOAuthTokenStore.Persist(settings, new ClaudeOAuthToken
            {
                AccessToken = "new-access",
                RefreshToken = null,
                ExpiresAtUnixMs = 2
            }, previous);

            var persisted = ClaudeOAuthTokenStore.Retrieve(settings.ProOAuthCredentialId);
            Assert.NotNull(persisted);
            Assert.Equal("new-access", persisted!.AccessToken);
            Assert.Equal("keep-refresh", persisted.RefreshToken);
            Assert.Equal(2, persisted.ExpiresAtUnixMs);
        }
        finally
        {
            ClaudeOAuthTokenStore.Clear(settings);
        }
    }
}

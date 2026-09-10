using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class ClaudeOAuthTokenStore
{
    public static void Persist(
        ProviderBillingSettings settings,
        ClaudeOAuthToken token,
        ClaudeOAuthToken? previous = null)
    {
        var previousRefresh = previous?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token.RefreshToken)
            && !string.IsNullOrWhiteSpace(previousRefresh))
        {
            token = new ClaudeOAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = previousRefresh,
                ExpiresAtUnixMs = token.ExpiresAtUnixMs
            };
        }

        var json = JsonSerializer.Serialize(token);
        CredentialStore.Replace(
            "claude-pro-oauth",
            settings.ProOAuthCredentialId,
            json,
            id => settings.ProOAuthCredentialId = id);
    }

    public static ClaudeOAuthToken? Retrieve(string? credentialId)
    {
        var json = CredentialStore.Retrieve(credentialId);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ClaudeOAuthToken>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear(ProviderBillingSettings settings)
    {
        CredentialStore.Delete(settings.ProOAuthCredentialId);
        settings.ProOAuthCredentialId = null;
    }
}

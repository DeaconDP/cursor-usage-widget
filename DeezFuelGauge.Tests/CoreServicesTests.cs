using System.Text;
using DeezFuelGauge;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Services.Credentials;
using DeezFuelGauge.Services.Providers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class CredentialProtectorTests
{
    [Fact]
    public void Protect_unprotect_round_trips_plaintext()
    {
        var original = "sk-test-secret-value"u8.ToArray();
        var protectedBytes = CredentialProtector.Protect(original);
        var restored = CredentialProtector.Unprotect(protectedBytes);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Unprotect_decrypts_legacy_encrypted_data()
    {
        var plain = Encoding.UTF8.GetBytes("legacy-secret");
        var protectedBytes = CredentialProtector.ProtectWithSlug(plain, AppBranding.LegacySettingsSlug);

        var decrypted = CredentialProtector.Unprotect(protectedBytes);

        Assert.Equal("legacy-secret", Encoding.UTF8.GetString(decrypted));
    }
}

public sealed class UsageRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_reports_cursor_failure()
    {
        using var refresh = new UsageRefreshService(
            new UsageClient(new HttpClient(new AlwaysNotFoundHandler())),
            new DirectBillingService());

        var result = await refresh.RefreshAsync(new WidgetSettings());

        Assert.False(result.CursorFetchSucceeded);
        Assert.True(result.Snapshot.IsError);
    }

    private sealed class AlwaysNotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}

public sealed class ProviderHealthPresenterTests
{
    [Fact]
    public void FormatDegradedMessage_uses_provider_label()
    {
        var message = ProviderHealthPresenter.FormatDegradedMessage("codex", "timeout");
        Assert.Contains("Codex", message);
        Assert.Contains("unavailable", message);
        Assert.Contains("timeout", message);
    }

    [Fact]
    public void FormatDegradedMessage_falls_back_when_detail_missing()
    {
        var message = ProviderHealthPresenter.FormatDegradedMessage("xai", null);
        Assert.Equal("xAI unavailable — API may have changed", message);
    }
}

public sealed class ProviderUsageAdapterTests
{
    [Fact]
    public void CodexUsageAdapter_exposes_provider_key()
    {
        var adapter = new CodexUsageAdapter(new CodexUsageClient(), new ProviderBillingSettings());
        Assert.Equal("codex", adapter.ProviderKey);
    }
}

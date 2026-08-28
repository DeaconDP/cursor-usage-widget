using System.Net;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class FalUsageClientTests
{
    [Fact]
    public void ParseBillingResponse_reads_balance_and_currency()
    {
        const string json = """
            {
              "username": "my-team",
              "credits": {
                "current_balance": 24.5,
                "currency": "USD"
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var snapshot = FalUsageClient.ParseBillingResponse(doc.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(24.5, snapshot.BalanceUsd);
        Assert.Equal("USD", snapshot.Currency);
        Assert.Equal(0, snapshot.HeadlinePercentUsed);
        Assert.Equal("$24.50 left", snapshot.DetailLabel);
    }

    [Theory]
    [InlineData(25, 25, 0)]
    [InlineData(25, 21.82, 12.72)]
    [InlineData(25, 0, 100)]
    [InlineData(26, 26, 0)]
    public void ComputePercentUsed_uses_baseline(double baseline, double balance, double expected)
    {
        Assert.Equal(expected, PrepaidCreditBaselineTracker.ComputePercentUsed(baseline, balance), 2);
    }

    [Fact]
    public void Update_seeds_baseline_on_first_observe()
    {
        var settings = new ProviderBillingSettings();

        var percent = PrepaidCreditBaselineTracker.Update(settings, 25);

        Assert.Equal(0, percent);
        Assert.Equal(25, settings.CreditBaselineUsd);
        Assert.Equal(25, settings.LastObservedBalanceUsd);
    }

    [Fact]
    public void Update_keeps_baseline_while_spending()
    {
        var settings = new ProviderBillingSettings
        {
            CreditBaselineUsd = 25,
            LastObservedBalanceUsd = 25
        };

        var percent = PrepaidCreditBaselineTracker.Update(settings, 21.82);

        Assert.Equal(12.72, percent, 2);
        Assert.Equal(25, settings.CreditBaselineUsd);
        Assert.Equal(21.82, settings.LastObservedBalanceUsd);
    }

    [Fact]
    public void Update_raises_baseline_on_top_up()
    {
        var settings = new ProviderBillingSettings
        {
            CreditBaselineUsd = 25,
            LastObservedBalanceUsd = 1
        };

        var percent = PrepaidCreditBaselineTracker.Update(settings, 26);

        Assert.Equal(0, percent);
        Assert.Equal(26, settings.CreditBaselineUsd);
        Assert.Equal(26, settings.LastObservedBalanceUsd);
    }

    [Fact]
    public void Update_ignores_sub_epsilon_jitter()
    {
        var settings = new ProviderBillingSettings
        {
            CreditBaselineUsd = 25,
            LastObservedBalanceUsd = 21.82
        };

        var percent = PrepaidCreditBaselineTracker.Update(settings, 21.824);

        Assert.Equal(12.704, percent, 2);
        Assert.Equal(25, settings.CreditBaselineUsd);
        Assert.Equal(21.824, settings.LastObservedBalanceUsd);
    }

    [Fact]
    public void FromBalance_empty_tank_when_zero()
    {
        var settings = new ProviderBillingSettings
        {
            CreditBaselineUsd = 25,
            LastObservedBalanceUsd = 1
        };
        var percent = PrepaidCreditBaselineTracker.Update(settings, 0);
        var snapshot = FalSnapshot.FromBalance(0, percentUsed: percent);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(100, percent);
        Assert.Equal(100, snapshot.HeadlinePercentUsed);
        Assert.Equal("$0.00 left", snapshot.DetailLabel);
        Assert.Equal(25, settings.CreditBaselineUsd);
        Assert.Equal(0, settings.LastObservedBalanceUsd);
    }

    [Fact]
    public async Task FetchAsync_returns_unavailable_when_key_missing()
    {
        using var client = new FalUsageClient(new HttpClient(new AlwaysOkHandler()));
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        var snapshot = await client.FetchAsync(settings);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("Admin API key not set", snapshot.StatusMessage);
    }

    [Fact]
    public async Task FetchAsync_reads_billing_with_key_auth()
    {
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal("Key", request.Headers.Authorization?.Scheme);
            Assert.Equal("fal-admin-test", request.Headers.Authorization?.Parameter);
            Assert.Contains("expand=credits", request.RequestUri!.Query);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"username":"u","credits":{"current_balance":4.2,"currency":"USD"}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var credentialId = CredentialStore.Store("fal", "fal-admin-test");
        try
        {
            using var client = new FalUsageClient(new HttpClient(handler));
            var settings = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CredentialId = credentialId,
                CreditBaselineUsd = 10,
                LastObservedBalanceUsd = 10
            };

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(4.2, snapshot.BalanceUsd);
            Assert.Equal(58, snapshot.HeadlinePercentUsed, 0);
            Assert.Equal(10, settings.CreditBaselineUsd);
            Assert.Equal(4.2, settings.LastObservedBalanceUsd);
            Assert.Equal("Connected", settings.LastConnectionStatus);
        }
        finally
        {
            CredentialStore.Delete(credentialId);
        }
    }

    [Fact]
    public async Task TestConnectionAsync_reports_unauthorized_as_revoked_key()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        using var client = new FalUsageClient(new HttpClient(handler));
        var status = await client.TestConnectionAsync("bad-key");

        Assert.Equal(
            "Invalid or revoked Admin API key — create a new one at fal.ai/dashboard/keys",
            status);
    }

    [Fact]
    public async Task TestConnectionAsync_reports_forbidden_as_admin_scope_required()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden));

        using var client = new FalUsageClient(new HttpClient(handler));
        var status = await client.TestConnectionAsync("bad-key");

        Assert.Equal(
            "Admin scope required for billing — check key scope and account/team in the fal dashboard",
            status);
    }

    [Fact]
    public async Task TestConnectionAsync_reports_rate_limited()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage((HttpStatusCode)429));

        using var client = new FalUsageClient(new HttpClient(handler));
        var status = await client.TestConnectionAsync("key");

        Assert.Equal("Rate limited — wait a moment and try again", status);
    }

    [Fact]
    public async Task TestConnectionAsync_surfaces_json_error_message()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    """{"error":{"type":"authorization_error","message":"Access denied"}}""",
                    Encoding.UTF8,
                    "application/json")
            });

        using var client = new FalUsageClient(new HttpClient(handler));
        var status = await client.TestConnectionAsync("key");

        Assert.Equal("Access denied", status);
    }

    private sealed class AlwaysOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"username":"u"}""", Encoding.UTF8, "application/json")
            });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}

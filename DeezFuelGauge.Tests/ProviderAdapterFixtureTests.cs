using System.Text.Json;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ProviderAdapterFixtureTests
{
    [Fact]
    public void Codex_adapter_fixture_contract()
    {
        const string json = """
            {
              "plan_type": "plus",
              "rate_limit": {
                "limit_reached": false,
                "primary_window": { "used_percent": 10.0, "reset_after_seconds": 3600 },
                "secondary_window": { "used_percent": 5.0, "reset_after_seconds": 86400 }
              },
              "credits": { "balance": "0" }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var snapshot = CodexUsageClient.ParseUsageResponse(document.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(10, snapshot.SessionPercentUsed);
    }

    [Fact]
    public void OpenRouter_adapter_fixture_contract()
    {
        const string json = """{"data":{"limit":100,"limit_remaining":50,"usage_daily":1,"usage_weekly":2,"usage_monthly":3}}""";
        using var document = JsonDocument.Parse(json);
        var key = OpenRouterUsageClient.ParseKeyResponse(document.RootElement);
        var snapshot = OpenRouterUsageClient.MergeResponses(key, credits: null);

        Assert.True(snapshot.IsAvailable);
    }

    [Fact]
    public void Fal_adapter_fixture_contract()
    {
        const string json = """{"username":"u","credits":{"current_balance":12.5,"currency":"USD"}}""";
        using var document = JsonDocument.Parse(json);
        var snapshot = FalUsageClient.ParseBillingResponse(document.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(12.5, snapshot.BalanceUsd);
        Assert.Equal(0, snapshot.HeadlinePercentUsed);
    }

    [Fact]
    public void Xai_adapter_fixture_contract()
    {
        const string json = """{"total":{"val":"-1250"}}""";
        using var document = JsonDocument.Parse(json);
        var snapshot = XaiUsageClient.ParseBalanceResponse(document.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(12.5, snapshot.BalanceUsd);
        Assert.Equal(0, snapshot.HeadlinePercentUsed);
    }
}

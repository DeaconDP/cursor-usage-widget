using System.Net;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeProUsageClientTests
{
    [Theory]
    [InlineData(0.42, 42)]
    [InlineData(1, 100)]
    [InlineData(42, 42)]
    [InlineData(0, 0)]
    public void NormalizeUtilization_handles_fraction_and_percent(double input, double expected)
    {
        var result = ClaudeProUsageClient.NormalizeUtilization(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseOrgUuid_reads_organization_uuid_from_memberships()
    {
        const string json = """
            {
              "memberships": [
                {
                  "organization": { "uuid": "org-abc-123" }
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var uuid = ClaudeProUsageClient.ParseOrgUuid(document.RootElement);

        Assert.Equal("org-abc-123", uuid);
    }

    [Fact]
    public void ParseUsageResponse_reads_session_and_weekly_windows()
    {
        const string json = """
            {
              "five_hour": { "utilization": 0.12, "resets_at": "2026-06-24T18:00:00Z" },
              "seven_day": { "utilization": 34, "resets_at": "2026-07-01T00:00:00Z" }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var snapshot = ClaudeProUsageClient.ParseUsageResponse(document.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(12, snapshot.SessionPercentUsed, 1);
        Assert.Equal(34, snapshot.WeeklyPercentUsed, 1);
        Assert.Equal("2026-06-24T18:00:00Z", snapshot.SessionResetsAt?.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        Assert.Equal("2026-07-01T00:00:00Z", snapshot.WeeklyResetsAt?.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        Assert.Contains("5h 12%", snapshot.DetailLabel);
        Assert.Contains("wk 34%", snapshot.DetailLabel);
    }

    [Fact]
    public void BuildCookieHeader_wraps_raw_session_value()
    {
        Assert.Equal("sessionKey=abc123", ClaudeProUsageClient.BuildCookieHeader("abc123"));
        Assert.Equal("sessionKey=abc123", ClaudeProUsageClient.BuildCookieHeader("sessionKey=abc123"));
    }

    [Fact]
    public async Task FetchAsync_returns_usage_from_mocked_http()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/account")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"memberships":[{"organization":{"uuid":"org-1"}}]}
                        """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "five_hour": { "utilization": 0.25 },
                      "seven_day": { "utilization": 0.5 }
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var client = new ClaudeProUsageClient(
            new HttpClient(handler),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => null,
                appOAuthReader: _ => null));
        var settings = new ProviderBillingSettings
        {
            ProSessionCredentialId = CredentialStore.Store("claude-pro-test", "test-session")
        };

        try
        {
            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(25, snapshot.SessionPercentUsed, 1);
            Assert.Equal(50, snapshot.WeeklyPercentUsed, 1);
            Assert.Equal("Connected", settings.ProLastConnectionStatus);
        }
        finally
        {
            CredentialStore.Delete(settings.ProSessionCredentialId);
        }
    }

    [Fact]
    public async Task FetchAsync_uses_oauth_usage_endpoint_for_claude_code_auth()
    {
        var handler = new StubHttpHandler(request =>
        {
            Assert.Equal("/api/oauth/usage", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("oauth-token", request.Headers.Authorization?.Parameter);
            // Without a claude-code User-Agent the endpoint returns persistent 429s.
            Assert.Contains(
                request.Headers.UserAgent,
                product => product.Product?.Name == "claude-code");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.1},"seven_day":{"utilization":0.2}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = new ClaudeProUsageClient(
            new HttpClient(handler),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token" },
                savedSessionReader: _ => null));

        var settings = new ProviderBillingSettings();
        var snapshot = await client.FetchAsync(settings);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(10, snapshot.SessionPercentUsed, 1);
        Assert.Equal(20, snapshot.WeeklyPercentUsed, 1);
    }

    [Fact]
    public async Task RefreshAndConnectAsync_uses_saved_session_key()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/account")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"memberships":[{"organization":{"uuid":"org-1"}}]}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.3},"seven_day":{"utilization":0.4}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = new ClaudeProUsageClient(
            new HttpClient(handler),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => null,
                savedSessionReader: _ => "pasted-session-key"));

        var settings = new ProviderBillingSettings();

        var status = await client.RefreshAndConnectAsync(settings);

        Assert.Equal("Connected", status);
    }

    [Fact]
    public async Task FetchAsync_refreshes_expired_app_oauth_token_and_persists_it()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/oauth/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"fresh-token","refresh_token":"fresh-refresh","expires_in":28800}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("fresh-token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.4},"seven_day":{"utilization":0.6}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var settings = new ProviderBillingSettings();
        var expiredToken = new ClaudeOAuthToken
        {
            AccessToken = "expired-token",
            RefreshToken = "old-refresh",
            ExpiresAtUnixMs = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds()
        };

        var httpClient = new HttpClient(handler);
        var client = new ClaudeProUsageClient(
            httpClient,
            new ClaudeProAuthResolver(
                claudeCodeReader: () => null,
                appOAuthReader: _ => expiredToken,
                savedSessionReader: _ => null),
            new ClaudeOAuthLoginService(httpClient));

        try
        {
            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(40, snapshot.SessionPercentUsed, 1);
            Assert.Equal(60, snapshot.WeeklyPercentUsed, 1);

            var persisted = ClaudeOAuthTokenStore.Retrieve(settings.ProOAuthCredentialId);
            Assert.NotNull(persisted);
            Assert.Equal("fresh-token", persisted!.AccessToken);
        }
        finally
        {
            CredentialStore.Delete(settings.ProOAuthCredentialId);
        }
    }

    [Fact]
    public void ParseUsageResponse_reads_extra_usage_monthly_spend()
    {
        const string json = """
            {
              "five_hour": { "utilization": 0.1 },
              "seven_day": { "utilization": 0.2 },
              "extra_usage": {
                "is_enabled": true,
                "monthly_limit": 2000,
                "used_credits": 4693,
                "utilization": 2.35,
                "currency": "USD"
              }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var snapshot = ClaudeProUsageClient.ParseUsageResponse(document.RootElement);

        Assert.True(snapshot.ExtraUsageIsAvailable);
        Assert.Equal(234.65, snapshot.ExtraUsagePercentRaw, 1);
        Assert.Equal(100, snapshot.ExtraUsagePercentUsed, 1);
        Assert.Equal(46.93m, snapshot.ExtraUsageUsedUsd);
        Assert.Equal(20m, snapshot.ExtraUsageLimitUsd);
        Assert.Contains("$46.93 / $20 mo", snapshot.DetailLabel);
    }

    [Fact]
    public void ParseUsageResponse_ignores_disabled_extra_usage()
    {
        const string json = """
            {
              "five_hour": { "utilization": 0.1 },
              "extra_usage": { "is_enabled": false, "monthly_limit": 2000, "used_credits": 1000 }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var snapshot = ClaudeProUsageClient.ParseUsageResponse(document.RootElement);

        Assert.False(snapshot.ExtraUsageIsAvailable);
    }

    [Fact]
    public void ParseOverageSpendLimit_reads_monthly_credit_limit_and_blocked_state()
    {
        const string json = """
            {
              "is_enabled": true,
              "monthly_credit_limit": 1951,
              "used_credits": 1951,
              "out_of_credits": true,
              "disabled_until": "2026-08-26T14:40:00Z"
            }
            """;

        using var document = JsonDocument.Parse(json);
        var overage = ClaudeProUsageClient.ParseOverageSpendLimit(document.RootElement);

        Assert.NotNull(overage);
        Assert.Equal(19.51m, overage.Value.LimitUsd);
        Assert.Equal(19.51m, overage.Value.UsedUsd);
        Assert.True(overage.Value.OutOfCredits);
        Assert.Equal(100, overage.Value.PercentRaw, 1);
    }

    [Fact]
    public void MergeExtraUsage_prefers_overage_endpoint_values()
    {
        var baseSnapshot = ClaudeProSnapshot.FromUsage(10, 20, null, null);
        var overage = new ClaudeExtraUsageData(
            IsEnabled: true,
            UsedUsd: 46.93m,
            LimitUsd: 20m,
            PercentRaw: 235,
            OutOfCredits: true,
            DisabledUntil: DateTimeOffset.Parse("2026-08-26T14:40:00Z"));

        var merged = ClaudeProUsageClient.MergeExtraUsage(baseSnapshot, overage);

        Assert.True(merged.ExtraUsageIsAvailable);
        Assert.Equal(235, merged.ExtraUsagePercentRaw, 1);
        Assert.True(merged.ExtraUsageOutOfCredits);
        Assert.Equal("Monthly spend limit reached", merged.StatusMessage);
    }

    [Fact]
    public async Task FetchAsync_enriches_session_usage_from_overage_endpoint()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/account")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"memberships":[{"organization":{"uuid":"org-1"}}]}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/overage_spend_limit", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "is_enabled": true,
                          "monthly_credit_limit": 2000,
                          "used_credits": 1000,
                          "out_of_credits": false
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.3},"seven_day":{"utilization":0.4}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = new ClaudeProUsageClient(
            new HttpClient(handler),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => null,
                savedSessionReader: _ => "pasted-session-key"));

        var settings = new ProviderBillingSettings();
        var snapshot = await client.FetchAsync(settings);

        Assert.True(snapshot.ExtraUsageIsAvailable);
        Assert.Equal(50, snapshot.ExtraUsagePercentRaw, 1);
        Assert.Equal(10m, snapshot.ExtraUsageUsedUsd);
        Assert.Equal(20m, snapshot.ExtraUsageLimitUsd);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}

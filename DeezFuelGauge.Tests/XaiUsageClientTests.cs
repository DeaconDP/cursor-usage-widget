using System.Net;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class XaiUsageClientTests
{
    [Fact]
    public void ParseBalanceResponse_inverts_cents_ledger()
    {
        const string json = """
            {
              "changes": [],
              "total": { "val": "-2450" }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var snapshot = XaiUsageClient.ParseBalanceResponse(doc.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(24.5, snapshot.BalanceUsd);
        Assert.Equal("USD", snapshot.Currency);
        Assert.Equal(0, snapshot.HeadlinePercentUsed);
        Assert.Equal("$24.50 left", snapshot.DetailLabel);
    }

    [Fact]
    public void ParseBalanceResponse_rejects_missing_total()
    {
        using var doc = JsonDocument.Parse("""{"changes":[]}""");

        var ex = Assert.Throws<XaiUsageException>(() =>
            XaiUsageClient.ParseBalanceResponse(doc.RootElement));

        Assert.Equal("Billing response missing prepaid total", ex.Message);
    }

    [Fact]
    public void ParseBalanceResponse_updates_baseline_when_settings_provided()
    {
        const string json = """{"total":{"val":"-420"}}""";
        using var doc = JsonDocument.Parse(json);
        var settings = new ProviderBillingSettings
        {
            CreditBaselineUsd = 10,
            LastObservedBalanceUsd = 10
        };

        var snapshot = XaiUsageClient.ParseBalanceResponse(doc.RootElement, settings);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(4.2, snapshot.BalanceUsd);
        Assert.Equal(58, snapshot.HeadlinePercentUsed, 0);
        Assert.Equal(10, settings.CreditBaselineUsd);
        Assert.Equal(4.2, settings.LastObservedBalanceUsd);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("default")]
    [InlineData("Default")]
    public void NeedsTeamResolution_true_for_empty_or_default_slug(string? teamId) =>
        Assert.True(XaiUsageClient.NeedsTeamResolution(teamId));

    [Fact]
    public void NeedsTeamResolution_false_for_uuid() =>
        Assert.False(XaiUsageClient.NeedsTeamResolution("65c1e471-205f-4566-9c5a-07198badf4ce"));

    [Fact]
    public void ParseTeamIdFromValidation_prefers_scopeId_for_team_scope()
    {
        using var doc = JsonDocument.Parse("""
            {
              "scope": "SCOPE_TEAM",
              "scopeId": "65c1e471-205f-4566-9c5a-07198badf4ce",
              "teamId": "legacy-ignored"
            }
            """);

        Assert.Equal(
            "65c1e471-205f-4566-9c5a-07198badf4ce",
            XaiUsageClient.ParseTeamIdFromValidation(doc.RootElement));
    }

    [Fact]
    public void ParseTeamIdFromValidation_org_scope_requires_teamId()
    {
        using var doc = JsonDocument.Parse("""
            {
              "scope": "SCOPE_ORGANIZATION",
              "scopeId": "org-77"
            }
            """);

        var ex = Assert.Throws<XaiUsageException>(() =>
            XaiUsageClient.ParseTeamIdFromValidation(doc.RootElement));

        Assert.Contains("organization-scoped", ex.Message);
        Assert.DoesNotContain("org-77", ex.Message);
    }

    [Fact]
    public void ParseTeamIdFromValidation_org_scope_uses_deprecated_teamId()
    {
        using var doc = JsonDocument.Parse("""
            {
              "scope": "SCOPE_ORGANIZATION",
              "scopeId": "org-77",
              "teamId": "65c1e471-205f-4566-9c5a-07198badf4ce"
            }
            """);

        Assert.Equal(
            "65c1e471-205f-4566-9c5a-07198badf4ce",
            XaiUsageClient.ParseTeamIdFromValidation(doc.RootElement));
    }

    [Fact]
    public void ApplyResolvedTeamId_keeps_baseline_when_first_resolving_empty()
    {
        var settings = new ProviderBillingSettings
        {
            CreditBaselineUsd = 12,
            LastObservedBalanceUsd = 8
        };

        XaiUsageClient.ApplyResolvedTeamId(settings, "team-xyz");

        Assert.Equal("team-xyz", settings.WorkspaceId);
        Assert.Equal(12, settings.CreditBaselineUsd);
        Assert.Equal(8, settings.LastObservedBalanceUsd);
    }

    [Fact]
    public void ApplyResolvedTeamId_clears_baseline_on_team_change()
    {
        var settings = new ProviderBillingSettings
        {
            WorkspaceId = "team-a",
            CreditBaselineUsd = 12,
            LastObservedBalanceUsd = 8
        };

        XaiUsageClient.ApplyResolvedTeamId(settings, "team-b");

        Assert.Equal("team-b", settings.WorkspaceId);
        Assert.Null(settings.CreditBaselineUsd);
        Assert.Null(settings.LastObservedBalanceUsd);
    }

    [Fact]
    public async Task TestConnectionAsync_persists_resolved_team_when_settings_provided()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("validation", StringComparison.Ordinal))
                return OkJson("""{"scope":"SCOPE_TEAM","scopeId":"team-from-test"}""");
            if (path.EndsWith("/prepaid/balance", StringComparison.Ordinal))
                return OkJson("""{"total":{"val":"-1000"}}""");
            if (path.EndsWith("/postpaid/invoice/preview", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            throw new InvalidOperationException($"Unexpected path: {path}");
        });

        using var client = new XaiUsageClient(new HttpClient(handler));
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        var status = await client.TestConnectionAsync("mgmt-key", teamId: null, settings);

        Assert.Equal("Connected", status);
        Assert.Equal("team-from-test", settings.WorkspaceId);
        Assert.Equal("Connected", settings.LastConnectionStatus);
        Assert.Equal(10, settings.CreditBaselineUsd);
    }

    [Fact]
    public void ComposeCredits_uses_live_remaining_from_total_minus_used()
    {
        var settings = new ProviderBillingSettings
        {
            CreditBaselineUsd = 99,
            LastObservedBalanceUsd = 50
        };

        var snapshot = XaiUsageClient.ComposeCredits(
            prepaidTotalUsd: 10,
            prepaidUsedUsd: 2.26,
            settings);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(7.74, snapshot.BalanceUsd!.Value, 2);
        Assert.Equal(10, snapshot.PrepaidTotalUsd);
        Assert.Equal(2.26, snapshot.PrepaidUsedUsd);
        Assert.Equal(22.6, snapshot.HeadlinePercentUsed, 1);
        Assert.Equal("$7.74 left · $2.26 used of $10.00", snapshot.DetailLabel);
        Assert.Equal(10, settings.CreditBaselineUsd);
        Assert.Equal(7.74, settings.LastObservedBalanceUsd!.Value, 2);
    }

    [Fact]
    public void ComposeCredits_falls_back_to_posted_remaining_without_used()
    {
        var settings = new ProviderBillingSettings();
        var snapshot = XaiUsageClient.ComposeCredits(10, prepaidUsedUsd: null, settings);

        Assert.Equal(10, snapshot.BalanceUsd);
        Assert.Null(snapshot.PrepaidUsedUsd);
        Assert.Equal(0, snapshot.HeadlinePercentUsed);
        Assert.Equal("$10.00 left", snapshot.DetailLabel);
        Assert.Equal(10, settings.CreditBaselineUsd);
    }

    [Fact]
    public void CentsLedgerToUsd_takes_absolute_magnitude()
    {
        Assert.Equal(10, XaiUsageClient.CentsLedgerToUsd(-1000));
        Assert.Equal(2.26, XaiUsageClient.CentsLedgerToUsd(226));
        Assert.Equal(4.5, XaiUsageClient.CentsLedgerToUsd(-450));
    }

    [Fact]
    public void TryParsePrepaidCreditsUsedUsd_reads_core_invoice_field()
    {
        using var doc = JsonDocument.Parse("""
            {
              "coreInvoice": {
                "prepaidCreditsUsed": { "val": "226" }
              }
            }
            """);

        Assert.True(XaiUsageClient.TryParsePrepaidCreditsUsedUsd(doc.RootElement, out var used));
        Assert.Equal(2.26, used, 2);
    }

    [Fact]
    public async Task FetchAsync_computes_live_remaining_from_invoice_preview()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/prepaid/balance", StringComparison.Ordinal))
                return OkJson("""{"total":{"val":"-1000"}}""");
            if (path.EndsWith("/postpaid/invoice/preview", StringComparison.Ordinal))
            {
                return OkJson("""
                    {
                      "coreInvoice": {
                        "prepaidCreditsUsed": { "val": "226" }
                      }
                    }
                    """);
            }

            throw new InvalidOperationException($"Unexpected path: {path}");
        });

        var credentialId = CredentialStore.Store("xai", "mgmt-key");
        try
        {
            using var client = new XaiUsageClient(new HttpClient(handler));
            var settings = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CredentialId = credentialId,
                WorkspaceId = "team-live"
            };

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(7.74, snapshot.BalanceUsd!.Value, 2);
            Assert.Equal(22.6, snapshot.HeadlinePercentUsed, 1);
            Assert.Equal("$7.74 left · $2.26 used of $10.00", snapshot.DetailLabel);
            Assert.Equal(10, settings.CreditBaselineUsd);
            Assert.Equal(7.74, settings.LastObservedBalanceUsd!.Value, 2);
        }
        finally
        {
            CredentialStore.Delete(credentialId);
        }
    }

    [Fact]
    public async Task FetchAsync_falls_back_when_invoice_preview_fails()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/prepaid/balance", StringComparison.Ordinal))
                return OkJson("""{"total":{"val":"-1000"}}""");
            if (path.EndsWith("/postpaid/invoice/preview", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            throw new InvalidOperationException($"Unexpected path: {path}");
        });

        var credentialId = CredentialStore.Store("xai", "mgmt-key");
        try
        {
            using var client = new XaiUsageClient(new HttpClient(handler));
            var settings = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CredentialId = credentialId,
                WorkspaceId = "team-fallback"
            };

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(10, snapshot.BalanceUsd);
            Assert.Null(snapshot.PrepaidUsedUsd);
            Assert.Equal("$10.00 left", snapshot.DetailLabel);
            Assert.Equal("Connected", settings.LastConnectionStatus);
        }
        finally
        {
            CredentialStore.Delete(credentialId);
        }
    }

    [Fact]
    public async Task FetchAsync_returns_unavailable_when_key_missing()
    {
        using var client = new XaiUsageClient(new HttpClient(new AlwaysOkHandler()));
        var settings = new ProviderBillingSettings
        {
            ShowProLimits = true,
            WorkspaceId = "team-1"
        };

        var snapshot = await client.FetchAsync(settings);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("Management API key not set", snapshot.StatusMessage);
    }

    [Fact]
    public async Task FetchAsync_resolves_team_uuid_when_missing()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("validation", StringComparison.Ordinal))
            {
                return OkJson("""{"scope":"SCOPE_TEAM","scopeId":"team-xyz","teamId":"team-xyz"}""");
            }

            if (path.EndsWith("/prepaid/balance", StringComparison.Ordinal))
            {
                Assert.Equal("/v1/billing/teams/team-xyz/prepaid/balance", path);
                return OkJson("""{"total":{"val":"-2500"}}""");
            }

            if (path.EndsWith("/postpaid/invoice/preview", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            throw new InvalidOperationException($"Unexpected path: {path}");
        });

        var credentialId = CredentialStore.Store("xai", "mgmt-key");
        try
        {
            using var client = new XaiUsageClient(new HttpClient(handler));
            var settings = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CredentialId = credentialId
            };

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(25, snapshot.BalanceUsd);
            Assert.Equal("team-xyz", settings.WorkspaceId);
            Assert.Equal("Connected", settings.LastConnectionStatus);
        }
        finally
        {
            CredentialStore.Delete(credentialId);
        }
    }

    [Fact]
    public async Task FetchAsync_resolves_when_team_is_default_slug()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("validation", StringComparison.Ordinal))
                return OkJson("""{"scope":"SCOPE_TEAM","scopeId":"real-uuid"}""");

            if (path.EndsWith("/prepaid/balance", StringComparison.Ordinal))
            {
                Assert.Equal("/v1/billing/teams/real-uuid/prepaid/balance", path);
                return OkJson("""{"total":{"val":"-100"}}""");
            }

            if (path.EndsWith("/postpaid/invoice/preview", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            throw new InvalidOperationException($"Unexpected path: {path}");
        });

        var credentialId = CredentialStore.Store("xai", "mgmt-key");
        try
        {
            using var client = new XaiUsageClient(new HttpClient(handler));
            var settings = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CredentialId = credentialId,
                WorkspaceId = "default"
            };

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal("real-uuid", settings.WorkspaceId);
        }
        finally
        {
            CredentialStore.Delete(credentialId);
        }
    }

    [Fact]
    public async Task FetchAsync_reads_balance_with_bearer_auth()
    {
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("mgmt-key", request.Headers.Authorization?.Parameter);
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/prepaid/balance", StringComparison.Ordinal))
            {
                Assert.Equal("/v1/billing/teams/team-abc/prepaid/balance", path);
                return OkJson("""{"total":{"val":"-420"}}""");
            }

            if (path.EndsWith("/postpaid/invoice/preview", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            throw new InvalidOperationException($"Unexpected path: {path}");
        });

        var credentialId = CredentialStore.Store("xai", "mgmt-key");
        try
        {
            using var client = new XaiUsageClient(new HttpClient(handler));
            var settings = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CredentialId = credentialId,
                WorkspaceId = "team-abc",
                CreditBaselineUsd = 10,
                LastObservedBalanceUsd = 10
            };

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(4.2, snapshot.BalanceUsd);
            Assert.Equal(58, snapshot.HeadlinePercentUsed, 0);
            Assert.Equal("Connected", settings.LastConnectionStatus);
        }
        finally
        {
            CredentialStore.Delete(credentialId);
        }
    }

    [Fact]
    public async Task TestConnectionAsync_reports_unauthorized()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        using var client = new XaiUsageClient(new HttpClient(handler));
        var status = await client.TestConnectionAsync("bad-key", "team-1");

        Assert.Equal(XaiUsageException.WrongKeyTypeMessage, status);
    }

    [Fact]
    public async Task TestConnectionAsync_reports_not_found_as_bad_team()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        using var client = new XaiUsageClient(new HttpClient(handler));
        var status = await client.TestConnectionAsync("mgmt-key", "wrong-team");

        Assert.Contains("Team not found", status);
        Assert.Contains("UUID", status);
    }

    [Fact]
    public void FromResponse_rewrites_invalid_bearer_to_management_key_hint()
    {
        var ex = XaiUsageException.FromResponse(
            HttpStatusCode.Unauthorized,
            """{"message":"invalid bearer token"}""");

        Assert.Equal(XaiUsageException.WrongKeyTypeMessage, ex.Message);
    }

    [Fact]
    public void FromResponse_appends_uuid_hint_for_api_uuid_errors()
    {
        var ex = XaiUsageException.FromResponse(
            HttpStatusCode.BadRequest,
            """{"message":"invalid uuid"}""");

        Assert.Contains("invalid uuid", ex.Message);
        Assert.Contains("team UUID", ex.Message);
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class AlwaysOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(OkJson("""{"total":{"val":"-100"}}"""));
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

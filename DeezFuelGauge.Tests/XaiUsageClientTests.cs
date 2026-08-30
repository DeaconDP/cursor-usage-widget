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
            if (request.RequestUri!.AbsolutePath.Contains("validation", StringComparison.Ordinal))
            {
                return OkJson("""{"scope":"SCOPE_TEAM","scopeId":"team-xyz","teamId":"team-xyz"}""");
            }

            Assert.Equal("/v1/billing/teams/team-xyz/prepaid/balance", request.RequestUri.AbsolutePath);
            return OkJson("""{"total":{"val":"-2500"}}""");
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
            if (request.RequestUri!.AbsolutePath.Contains("validation", StringComparison.Ordinal))
                return OkJson("""{"scope":"SCOPE_TEAM","scopeId":"real-uuid"}""");

            Assert.Equal("/v1/billing/teams/real-uuid/prepaid/balance", request.RequestUri.AbsolutePath);
            return OkJson("""{"total":{"val":"-100"}}""");
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
            Assert.Equal(
                "/v1/billing/teams/team-abc/prepaid/balance",
                request.RequestUri!.AbsolutePath);

            return OkJson("""{"total":{"val":"-420"}}""");
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

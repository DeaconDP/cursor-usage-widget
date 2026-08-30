using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class XaiUsageClient : IDisposable
{
    private const string ManagementApiBase = "https://management-api.x.ai";
    private const string ValidationPath = "/auth/management-keys/validation";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public XaiUsageClient(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
    }

    public async Task<XaiSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = CredentialStore.Retrieve(settings.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return XaiSnapshot.Unavailable("Management API key not set");

        try
        {
            var teamId = await ResolveTeamIdAsync(apiKey, settings.WorkspaceId, cancellationToken);
            // Persist resolved UUID so the next refresh skips validation.
            if (!string.Equals(settings.WorkspaceId?.Trim(), teamId, StringComparison.Ordinal))
                settings.WorkspaceId = teamId;

            var snapshot = await FetchBalanceAsync(apiKey, teamId, settings, cancellationToken);
            settings.LastConnectionStatus = snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
            return snapshot;
        }
        catch (XaiUsageException ex)
        {
            settings.LastConnectionStatus = ex.Message;
            return XaiSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.LastConnectionStatus = "Request failed";
            return XaiSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(
        string apiKey,
        string? teamId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "Management API key required";

        try
        {
            var resolvedTeamId = await ResolveTeamIdAsync(apiKey, teamId, cancellationToken);
            var snapshot = await FetchBalanceAsync(apiKey, resolvedTeamId, settings: null, cancellationToken);
            return snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
        }
        catch (XaiUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static XaiSnapshot ParseBalanceResponse(JsonElement root, ProviderBillingSettings? settings = null)
    {
        if (!root.TryGetProperty("total", out var total) || total.ValueKind != JsonValueKind.Object)
            throw new XaiUsageException("Billing response missing prepaid total");

        if (!total.TryGetProperty("val", out var valEl)
            || (valEl.ValueKind != JsonValueKind.String && valEl.ValueKind != JsonValueKind.Number))
            throw new XaiUsageException("Invalid prepaid balance");

        var centsText = valEl.ValueKind == JsonValueKind.String
            ? valEl.GetString()
            : valEl.GetRawText();

        if (string.IsNullOrWhiteSpace(centsText)
            || !double.TryParse(centsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var cents))
            throw new XaiUsageException("Invalid prepaid balance");

        // Inverted ledger: a $10 top-up appears as "-1000" (USD cents).
        var balance = -cents / 100.0;

        var percent = settings is null
            ? PrepaidCreditBaselineTracker.ComputePercentUsed(Math.Max(balance, 0), balance)
            : PrepaidCreditBaselineTracker.Update(settings, balance);

        return XaiSnapshot.FromBalance(balance, "USD", percent);
    }

    /// <summary>
    /// Team-scoped management keys expose the team UUID via validation.
    /// Console URLs often show <c>default</c> as a slug — that is not a team id.
    /// </summary>
    internal static string ParseTeamIdFromValidation(JsonElement root)
    {
        var scope = ReadString(root, "scope") ?? "";
        var scopeId = ReadString(root, "scopeId") ?? ReadString(root, "scope_id");
        var teamId = ReadString(root, "teamId") ?? ReadString(root, "team_id");

        if (string.Equals(scope, "SCOPE_ORGANIZATION", StringComparison.OrdinalIgnoreCase))
        {
            // Org-scoped keys: scopeId is the org, not a team. Prefer deprecated teamId when present.
            if (!string.IsNullOrWhiteSpace(teamId))
                return teamId.Trim();

            throw new XaiUsageException(
                "Management key is organization-scoped — paste the team UUID from console.x.ai (URL/team settings), not “default”");
        }

        // Team-scoped or legacy (no scope): scopeId is the team UUID.
        if (!string.IsNullOrWhiteSpace(scopeId))
            return scopeId.Trim();
        if (!string.IsNullOrWhiteSpace(teamId))
            return teamId.Trim();

        throw new XaiUsageException(
            "Could not resolve team UUID from Management key — paste team ID from console.x.ai team settings");
    }

    internal static bool NeedsTeamResolution(string? configuredTeamId)
    {
        if (string.IsNullOrWhiteSpace(configuredTeamId))
            return true;

        // Console path slug, not a team UUID.
        return string.Equals(configuredTeamId.Trim(), "default", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ResolveTeamIdAsync(
        string apiKey,
        string? configuredTeamId,
        CancellationToken cancellationToken)
    {
        if (!NeedsTeamResolution(configuredTeamId))
            return configuredTeamId!.Trim();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ManagementApiBase}{ValidationPath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw XaiUsageException.FromResponse(response.StatusCode, body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseTeamIdFromValidation(doc.RootElement);
    }

    private async Task<XaiSnapshot> FetchBalanceAsync(
        string apiKey,
        string teamId,
        ProviderBillingSettings? settings,
        CancellationToken cancellationToken)
    {
        var url = $"{ManagementApiBase}/v1/billing/teams/{Uri.EscapeDataString(teamId)}/prepaid/balance";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw XaiUsageException.FromResponse(response.StatusCode, body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseBalanceResponse(doc.RootElement, settings);
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

public sealed class XaiUsageException : Exception
{
    public XaiUsageException(string message) : base(message) { }

    public static XaiUsageException FromResponse(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var messageEl)
                && messageEl.ValueKind == JsonValueKind.String)
            {
                var msg = messageEl.GetString();
                if (!string.IsNullOrWhiteSpace(msg))
                    return new XaiUsageException(NormalizeApiMessage(msg));
            }

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    var msg = error.GetString();
                    if (!string.IsNullOrWhiteSpace(msg))
                        return new XaiUsageException(NormalizeApiMessage(msg));
                }
                else if (error.ValueKind == JsonValueKind.Object
                         && error.TryGetProperty("message", out var errorMsg)
                         && errorMsg.ValueKind == JsonValueKind.String)
                {
                    var msg = errorMsg.GetString();
                    if (!string.IsNullOrWhiteSpace(msg))
                        return new XaiUsageException(NormalizeApiMessage(msg));
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        return status switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                new XaiUsageException(WrongKeyTypeMessage),
            System.Net.HttpStatusCode.Forbidden =>
                new XaiUsageException("Management key needs billing read access — check ACLs in the xAI console"),
            System.Net.HttpStatusCode.NotFound =>
                new XaiUsageException("Team not found — paste the team UUID from console.x.ai (not the “default” URL slug)"),
            System.Net.HttpStatusCode.TooManyRequests =>
                new XaiUsageException("Rate limited — wait a moment and try again"),
            _ => new XaiUsageException($"Billing request failed ({(int)status})")
        };
    }

    internal const string WrongKeyTypeMessage =
        "Need a Management key (console.x.ai → Settings → Management Keys), not an API/inference key";

    private static string NormalizeApiMessage(string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Contains("bearer", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("invalid token", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("invalid api key", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            return WrongKeyTypeMessage;
        }

        if (trimmed.Contains("uuid", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("team", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed} — use the team UUID from console.x.ai team settings (not “default”)";
        }

        return trimmed;
    }
}

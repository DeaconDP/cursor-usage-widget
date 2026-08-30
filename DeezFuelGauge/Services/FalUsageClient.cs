using System.Net.Http.Headers;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class FalUsageClient : IDisposable
{
    private const string BillingUrl = "https://api.fal.ai/v1/account/billing?expand=credits";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public FalUsageClient(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
    }

    public async Task<FalSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = CredentialStore.Retrieve(settings.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return FalSnapshot.Unavailable("Admin API key not set");

        try
        {
            var snapshot = await FetchBillingAsync(apiKey, settings, cancellationToken);
            settings.LastConnectionStatus = snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
            return snapshot;
        }
        catch (FalUsageException ex)
        {
            settings.LastConnectionStatus = ex.Message;
            return FalSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.LastConnectionStatus = "Request failed";
            return FalSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "Admin API key required";

        try
        {
            var snapshot = await FetchBillingAsync(apiKey, settings: null, cancellationToken);
            return snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
        }
        catch (FalUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static FalSnapshot ParseBillingResponse(JsonElement root, ProviderBillingSettings? settings = null)
    {
        if (!root.TryGetProperty("credits", out var credits) || credits.ValueKind != JsonValueKind.Object)
            throw new FalUsageException("Billing response missing credits (use Admin API key)");

        if (!credits.TryGetProperty("current_balance", out var balanceEl) || balanceEl.ValueKind != JsonValueKind.Number)
            throw new FalUsageException("Invalid credits balance");

        var balance = balanceEl.GetDouble();
        var currency = credits.TryGetProperty("currency", out var currencyEl) && currencyEl.ValueKind == JsonValueKind.String
            ? currencyEl.GetString()
            : "USD";

        var percent = settings is null
            ? PrepaidCreditBaselineTracker.ComputePercentUsed(Math.Max(balance, 0), balance)
            : PrepaidCreditBaselineTracker.Update(settings, balance);

        return FalSnapshot.FromBalance(balance, currency, percent);
    }

    private async Task<FalSnapshot> FetchBillingAsync(
        string apiKey,
        ProviderBillingSettings? settings,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BillingUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Key", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw FalUsageException.FromResponse(response.StatusCode, body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseBillingResponse(doc.RootElement, settings);
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

public sealed class FalUsageException : Exception
{
    public FalUsageException(string message) : base(message) { }

    public static FalUsageException FromResponse(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var messageEl))
            {
                var msg = messageEl.GetString();
                if (!string.IsNullOrWhiteSpace(msg))
                    return new FalUsageException(msg);
            }
        }
        catch
        {
            // ignore parse errors
        }

        return status switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                new FalUsageException("Invalid or revoked Admin API key — create a new one at fal.ai/dashboard/keys"),
            System.Net.HttpStatusCode.Forbidden =>
                new FalUsageException("Admin scope required for billing — check key scope and account/team in the fal dashboard"),
            System.Net.HttpStatusCode.TooManyRequests =>
                new FalUsageException("Rate limited — wait a moment and try again"),
            _ => new FalUsageException($"Billing request failed ({(int)status})")
        };
    }
}

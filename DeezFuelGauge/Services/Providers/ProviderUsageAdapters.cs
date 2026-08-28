using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services.Providers;

public interface IProviderUsageAdapter<TSnapshot>
{
    string ProviderKey { get; }
    Task<TSnapshot> FetchAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexUsageAdapter : IProviderUsageAdapter<CodexSnapshot>
{
    private readonly CodexUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public CodexUsageAdapter(CodexUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "codex";

    public Task<CodexSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class AntigravityUsageAdapter : IProviderUsageAdapter<AntigravitySnapshot>
{
    private readonly AntigravityUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public AntigravityUsageAdapter(AntigravityUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "antigravity";

    public Task<AntigravitySnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class OpenRouterUsageAdapter : IProviderUsageAdapter<OpenRouterSnapshot>
{
    private readonly OpenRouterUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public OpenRouterUsageAdapter(OpenRouterUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "openrouter";

    public Task<OpenRouterSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class OpenCodeUsageAdapter : IProviderUsageAdapter<OpenCodeSnapshot>
{
    private readonly OpenCodeUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public OpenCodeUsageAdapter(OpenCodeUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "opencode";

    public Task<OpenCodeSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class FalUsageAdapter : IProviderUsageAdapter<FalSnapshot>
{
    private readonly FalUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public FalUsageAdapter(FalUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "fal";

    public Task<FalSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class XaiUsageAdapter : IProviderUsageAdapter<XaiSnapshot>
{
    private readonly XaiUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public XaiUsageAdapter(XaiUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "xai";

    public Task<XaiSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

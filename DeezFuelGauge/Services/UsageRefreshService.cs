using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class UsageRefreshService : IDisposable
{
    private readonly UsageClient _usageClient;
    private readonly DirectBillingService _directBilling;
    private readonly bool _ownsClients;

    public UsageRefreshService(
        UsageClient? usageClient = null,
        DirectBillingService? directBilling = null)
    {
        _ownsClients = usageClient is null && directBilling is null;
        _usageClient = usageClient ?? new UsageClient();
        _directBilling = directBilling ?? new DirectBillingService();
    }

    public async Task<RefreshResult> RefreshAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        var refreshedAt = DateTimeOffset.UtcNow;
        var tokens = CursorTokenReader.Read();
        _usageClient.SetTokens(tokens.AccessToken, tokens.RefreshToken);

        var cursorSnapshot = await _usageClient.FetchAsync(cancellationToken);
        var cursorSucceeded = !cursorSnapshot.IsError;

        var baseSnapshot = cursorSucceeded
            ? cursorSnapshot
            : CreateCursorErrorBase(cursorSnapshot);

        var enriched = await _directBilling.EnrichAsync(baseSnapshot, settings, cancellationToken);
        var providerStatuses = BuildProviderStatuses(enriched, settings);

        return new RefreshResult
        {
            Snapshot = enriched,
            RefreshedAt = refreshedAt,
            CursorFetchSucceeded = cursorSucceeded,
            CursorError = cursorSucceeded ? null : cursorSnapshot.ErrorMessage,
            ProviderStatuses = providerStatuses
        };
    }

    internal static UsageSnapshot CreateCursorErrorBase(UsageSnapshot cursorSnapshot) =>
        new()
        {
            IsError = true,
            ErrorMessage = cursorSnapshot.ErrorMessage ?? "Can't fetch Cursor usage",
            PercentUsed = 0,
            RemainingLabel = cursorSnapshot.ErrorMessage ?? "Can't fetch Cursor usage"
        };

    private static IReadOnlyDictionary<string, ProviderRefreshStatus> BuildProviderStatuses(
        UsageSnapshot snapshot,
        WidgetSettings settings)
    {
        var statuses = new Dictionary<string, ProviderRefreshStatus>();

        if (settings.OpenAi.ShowDirectSource)
            statuses["openai-platform"] = StatusFromDirect(snapshot.OpenAiDirect);

        if (settings.OpenAi.ShowProLimits)
            statuses["codex"] = StatusFromCodex(snapshot.Codex);

        if (settings.Gemini.ShowProLimits)
            statuses["antigravity"] = StatusFromAntigravity(snapshot.Antigravity);

        if (settings.OpenRouter.ShowProLimits)
            statuses["openrouter"] = StatusFromOpenRouter(snapshot.OpenRouter);

        if (settings.OpenCode.ShowDirectSource || settings.OpenCode.ShowProLimits)
            statuses["opencode"] = StatusFromOpenCode(snapshot.OpenCode);

        if (settings.Fal.ShowProLimits)
            statuses["fal"] = StatusFromFal(snapshot.Fal);

        if (settings.Xai.ShowProLimits)
            statuses["xai"] = StatusFromXai(snapshot.Xai);

        if (settings.GrokBot.ShowProLimits)
            statuses["grokbot"] = StatusFromGrokBot(snapshot.GrokBot);

        return statuses;
    }

    private static ProviderRefreshStatus StatusFromDirect(DirectProviderSnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    private static ProviderRefreshStatus StatusFromCodex(CodexSnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    private static ProviderRefreshStatus StatusFromAntigravity(AntigravitySnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    private static ProviderRefreshStatus StatusFromOpenRouter(OpenRouterSnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    private static ProviderRefreshStatus StatusFromOpenCode(OpenCodeSnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    private static ProviderRefreshStatus StatusFromFal(FalSnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    private static ProviderRefreshStatus StatusFromXai(XaiSnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    private static ProviderRefreshStatus StatusFromGrokBot(GrokBotSnapshot snapshot) =>
        snapshot.IsAvailable
            ? ProviderRefreshStatus.Ok()
            : ProviderRefreshStatus.Failed(snapshot.StatusMessage ?? "Unavailable", degraded: true);

    public void Dispose()
    {
        if (!_ownsClients)
            return;

        _usageClient.Dispose();
        _directBilling.Dispose();
    }
}

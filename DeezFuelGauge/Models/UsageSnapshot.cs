namespace DeezFuelGauge.Models;

public sealed class UsageSnapshot
{
    public double PercentUsed { get; init; }
    public string RemainingLabel { get; init; } = "";
    public double? AutoPercentUsed { get; init; }
    public double? ApiPercentUsed { get; init; }
    public bool HasBreakdown => AutoPercentUsed is not null || ApiPercentUsed is not null;
    public long? PlanLimitCents { get; init; }
    public long? BillingCycleStartMs { get; init; }
    public long? BillingCycleEndMs { get; init; }
    public ProviderUsageSnapshot OpenAi { get; init; } = ProviderUsageSnapshot.Unavailable();
    public ProviderUsageSnapshot Claude { get; init; } = ProviderUsageSnapshot.Unavailable();
    public ProviderUsageSnapshot Gemini { get; init; } = ProviderUsageSnapshot.Unavailable();
    public CodexSnapshot Codex { get; init; } = CodexSnapshot.Unavailable();
    public ClaudeProSnapshot ClaudePro { get; init; } = ClaudeProSnapshot.Unavailable();
    public DirectProviderSnapshot OpenAiDirect { get; init; } = DirectProviderSnapshot.Unavailable();
    public DirectProviderSnapshot ClaudeDirect { get; init; } = DirectProviderSnapshot.Unavailable();
    public AntigravitySnapshot Antigravity { get; init; } = AntigravitySnapshot.Unavailable();
    public OpenRouterSnapshot OpenRouter { get; init; } = OpenRouterSnapshot.Unavailable();
    public OpenCodeSnapshot OpenCode { get; init; } = OpenCodeSnapshot.Unavailable();
    public FalSnapshot Fal { get; init; } = FalSnapshot.Unavailable();
    public XaiSnapshot Xai { get; init; } = XaiSnapshot.Unavailable();
    public GrokBotSnapshot GrokBot { get; init; } = GrokBotSnapshot.Unavailable();
    public bool HasProviderBreakdown =>
        OpenAi.IsAvailable || Claude.IsAvailable || Gemini.IsAvailable;
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }

    public static UsageSnapshot Error(string message) => new()
    {
        IsError = true,
        ErrorMessage = message,
        PercentUsed = 0,
        RemainingLabel = message
    };
}

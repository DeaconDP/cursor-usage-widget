namespace DeezFuelGauge.Services;

public static class ProviderHealthPresenter
{
    private static readonly Dictionary<string, string> ProviderLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai-platform"] = "OpenAI API",
        ["codex"] = "Codex",
        ["antigravity"] = "Gemini App",
        ["openrouter"] = "OpenRouter",
        ["opencode"] = "OpenCode",
        ["fal"] = "fal.ai",
        ["xai"] = "xAI",
        ["grokbot"] = "Grok Bot"
    };

    public static string FormatDegradedMessage(string providerKey, string? detail) =>
        ProviderLabels.TryGetValue(providerKey, out var label)
            ? string.IsNullOrWhiteSpace(detail)
                ? $"{label} unavailable — API may have changed"
                : $"{label} unavailable — {detail}"
            : detail ?? "Provider unavailable";

    public static string FormatHeadlineBadge(string? degradedMessage) =>
        string.IsNullOrWhiteSpace(degradedMessage) ? "" : " ⚠";
}

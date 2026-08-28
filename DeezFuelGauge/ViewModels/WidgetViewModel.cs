namespace DeezFuelGauge.ViewModels;

using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

public sealed class ProviderSectionViewModel
{
    public string Title { get; init; } = "";
    public double HeadlinePercent { get; set; }
    public bool IsExpanded { get; set; }
    public string? DegradedMessage { get; set; }
    public bool HasDegradedState => !string.IsNullOrWhiteSpace(DegradedMessage);
    public string? UnusedQuotaMessage { get; set; }
    public bool HasUnusedQuotaAlert => !string.IsNullOrWhiteSpace(UnusedQuotaMessage);
}

public sealed class WidgetViewModel
{
    public ProviderSectionViewModel Cursor { get; } = new() { Title = "Cursor" };
    public ProviderSectionViewModel OpenAi { get; } = new() { Title = "OpenAI" };
    public ProviderSectionViewModel Gemini { get; } = new() { Title = "Gemini" };
    public ProviderSectionViewModel OpenRouter { get; } = new() { Title = "OpenRouter" };
    public ProviderSectionViewModel OpenCode { get; } = new() { Title = "OpenCode" };
    public ProviderSectionViewModel Fal { get; } = new() { Title = "fal.ai" };
    public ProviderSectionViewModel Xai { get; } = new() { Title = "xAI" };
    public ProviderSectionViewModel GrokBot { get; } = new() { Title = "Grok Bot" };

    public DateTimeOffset? LastRefreshedAt { get; set; }

    public string LastRefreshedLabel =>
        LastRefreshedAt is { } at
            ? $"Updated {at.ToLocalTime():HH:mm}"
            : "";

    public IReadOnlyList<QuotaAlert> ActiveQuotaAlerts { get; private set; } = Array.Empty<QuotaAlert>();

    public string QuotaAlertHeaderSummary =>
        QuotaAlertPresenter.FormatHeaderSummary(ActiveQuotaAlerts);

    public string QuotaAlertHeaderTooltip =>
        QuotaAlertPresenter.FormatHeaderTooltip(ActiveQuotaAlerts);

    public bool HasQuotaAlerts => ActiveQuotaAlerts.Count > 0;

    public void ApplyQuotaAlerts(IReadOnlyList<QuotaAlert> alerts)
    {
        ActiveQuotaAlerts = alerts;
        ClearUnusedQuotaStates();

        foreach (var group in alerts.GroupBy(a => a.ProviderKey))
        {
            var message = QuotaAlertPresenter.FormatProviderTooltip(group);
            AssignUnusedQuotaMessage(group.Key, message);
        }
    }

    public void ApplyRefreshResult(Models.RefreshResult result)
    {
        LastRefreshedAt = result.RefreshedAt;
        ClearDegradedStates();

        foreach (var (key, status) in result.ProviderStatuses)
        {
            if (status.Succeeded || !status.IsDegraded)
                continue;

            var message = ProviderHealthPresenter.FormatDegradedMessage(key, status.ErrorMessage);
            AssignDegradedMessage(key, message);
        }
    }

    private void ClearDegradedStates()
    {
        OpenAi.DegradedMessage = null;
        Gemini.DegradedMessage = null;
        OpenRouter.DegradedMessage = null;
        OpenCode.DegradedMessage = null;
        Fal.DegradedMessage = null;
        Xai.DegradedMessage = null;
        GrokBot.DegradedMessage = null;
    }

    private void ClearUnusedQuotaStates()
    {
        Cursor.UnusedQuotaMessage = null;
        OpenAi.UnusedQuotaMessage = null;
        Gemini.UnusedQuotaMessage = null;
        OpenRouter.UnusedQuotaMessage = null;
        OpenCode.UnusedQuotaMessage = null;
        Fal.UnusedQuotaMessage = null;
        Xai.UnusedQuotaMessage = null;
        GrokBot.UnusedQuotaMessage = null;
    }

    private void AssignUnusedQuotaMessage(string providerKey, string message)
    {
        switch (providerKey)
        {
            case "cursor":
                Cursor.UnusedQuotaMessage = message;
                break;
            case "openai":
                OpenAi.UnusedQuotaMessage = message;
                break;
            case "gemini":
                Gemini.UnusedQuotaMessage = message;
                break;
            case "openrouter":
                OpenRouter.UnusedQuotaMessage = message;
                break;
            case "opencode":
                OpenCode.UnusedQuotaMessage = message;
                break;
            case "fal":
                Fal.UnusedQuotaMessage = message;
                break;
            case "xai":
                Xai.UnusedQuotaMessage = message;
                break;
            case "grokbot":
                GrokBot.UnusedQuotaMessage = message;
                break;
        }
    }

    private void AssignDegradedMessage(string providerKey, string message)
    {
        switch (providerKey)
        {
            case "openai-platform":
            case "codex":
                OpenAi.DegradedMessage ??= message;
                break;
            case "antigravity":
                Gemini.DegradedMessage ??= message;
                break;
            case "openrouter":
                OpenRouter.DegradedMessage = message;
                break;
            case "opencode":
                OpenCode.DegradedMessage = message;
                break;
            case "fal":
                Fal.DegradedMessage = message;
                break;
            case "xai":
                Xai.DegradedMessage = message;
                break;
            case "grokbot":
                GrokBot.DegradedMessage = message;
                break;
        }
    }
}

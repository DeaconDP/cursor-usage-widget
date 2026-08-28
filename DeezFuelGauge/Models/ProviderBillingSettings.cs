using System.Text.Json.Serialization;

namespace DeezFuelGauge.Models;

public sealed class ProviderBillingSettings
{
    [JsonPropertyName("IsVisible")]
    public bool ShowCursorSource { get; set; } = true;

    public bool ShowDirectSource { get; set; }

    public bool ShowProLimits { get; set; } = true;

    public bool ShowProBreakdown { get; set; } = true;

    public string? ProSessionCredentialId { get; set; }

    public string? ProOAuthCredentialId { get; set; }

    public bool ShowApiConsoleBilling { get; set; }

    public string? ProLastConnectionStatus { get; set; }

    public bool ShowDetails { get; set; } = true;

    /// <summary>When null, inherits <see cref="ShowDetails"/> for settings saved before per-source detail toggles.</summary>
    public bool? ShowDirectDetails { get; set; }

    /// <summary>When null, inherits <see cref="ShowDetails"/> for settings saved before per-source detail toggles.</summary>
    public bool? ShowProDetails { get; set; }

    public bool EffectiveShowDirectDetails => ShowDirectDetails ?? ShowDetails;

    public bool EffectiveShowProDetails => ShowProDetails ?? ShowDetails;

    public decimal? MonthlyBudgetUsd { get; set; }

    public string? OrganizationId { get; set; }

    public string? ProjectId { get; set; }

    public string? WorkspaceId { get; set; }

    public string? CredentialId { get; set; }

    /// <summary>Optional management API key for account-wide credit balance (OpenRouter).</summary>
    public string? ManagementCredentialId { get; set; }

    public string? LastConnectionStatus { get; set; }

    /// <summary>Prepaid tank size after last observed top-up (or first balance).</summary>
    public double? CreditBaselineUsd { get; set; }

    /// <summary>Last remaining credit balance observed; used to detect top-ups.</summary>
    public double? LastObservedBalanceUsd { get; set; }

    [JsonIgnore]
    public bool IsVisible
    {
        get => ShowCursorSource;
        set => ShowCursorSource = value;
    }

    public bool HasAnyDashboardSource =>
        ShowCursorSource || ShowDirectSource || ShowProLimits || ShowApiConsoleBilling;
}

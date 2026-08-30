using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

/// <summary>
/// Persists a prepaid "tank" size from observed balances: seed on first
/// fetch, raise when remaining credit increases (top-up), then compute % used.
/// </summary>
public static class PrepaidCreditBaselineTracker
{
    /// <summary>Half-cent floor so float/API jitter does not count as a top-up.</summary>
    internal const double TopUpEpsilonUsd = 0.005;

    public static double Update(ProviderBillingSettings settings, double balanceUsd)
    {
        var baseline = settings.CreditBaselineUsd;
        var lastObserved = settings.LastObservedBalanceUsd;

        if (baseline is null or <= 0)
        {
            baseline = Math.Max(balanceUsd, 0);
        }
        else if (lastObserved is { } prev && balanceUsd > prev + TopUpEpsilonUsd)
        {
            // Top-up: new tank size is remaining after the credit load.
            baseline = balanceUsd;
        }
        else if (balanceUsd > baseline.Value + TopUpEpsilonUsd)
        {
            baseline = balanceUsd;
        }

        settings.CreditBaselineUsd = baseline;
        settings.LastObservedBalanceUsd = balanceUsd;

        return ComputePercentUsed(baseline.Value, balanceUsd);
    }

    internal static double ComputePercentUsed(double baselineUsd, double balanceUsd)
    {
        if (balanceUsd <= 0)
            return 100;

        if (baselineUsd <= 0)
            return 0;

        return Math.Clamp((baselineUsd - balanceUsd) / baselineUsd * 100.0, 0, 100);
    }
}

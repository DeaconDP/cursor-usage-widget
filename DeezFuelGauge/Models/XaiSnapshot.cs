using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class XaiSnapshot
{
    public double? BalanceUsd { get; init; }
    public string Currency { get; init; } = "USD";
    public double HeadlinePercentUsed { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static XaiSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static XaiSnapshot FromBalance(
        double balanceUsd,
        string? currency = null,
        double? percentUsed = null)
    {
        var currencyCode = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        var percent = percentUsed
            ?? (balanceUsd <= 0 ? 100 : 0);
        var balanceLabel = currencyCode == "USD"
            ? $"${balanceUsd.ToString("F2", CultureInfo.InvariantCulture)} left"
            : $"{balanceUsd.ToString("F2", CultureInfo.InvariantCulture)} {currencyCode} left";

        return new XaiSnapshot
        {
            IsAvailable = true,
            BalanceUsd = balanceUsd,
            Currency = currencyCode,
            HeadlinePercentUsed = percent,
            DetailLabel = balanceLabel
        };
    }
}

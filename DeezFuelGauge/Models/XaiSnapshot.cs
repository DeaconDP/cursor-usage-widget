using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class XaiSnapshot
{
    public double? BalanceUsd { get; init; }
    public double? PrepaidTotalUsd { get; init; }
    public double? PrepaidUsedUsd { get; init; }
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
        double? percentUsed = null) =>
        FromCredits(balanceUsd, prepaidTotalUsd: null, prepaidUsedUsd: null, percentUsed, currency);

    public static XaiSnapshot FromCredits(
        double remainingUsd,
        double? prepaidTotalUsd,
        double? prepaidUsedUsd,
        double? percentUsed = null,
        string? currency = null)
    {
        var currencyCode = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        var percent = percentUsed
            ?? (remainingUsd <= 0 ? 100 : 0);

        return new XaiSnapshot
        {
            IsAvailable = true,
            BalanceUsd = remainingUsd,
            PrepaidTotalUsd = prepaidTotalUsd,
            PrepaidUsedUsd = prepaidUsedUsd,
            Currency = currencyCode,
            HeadlinePercentUsed = percent,
            DetailLabel = FormatDetailLabel(remainingUsd, prepaidTotalUsd, prepaidUsedUsd, currencyCode)
        };
    }

    internal static string FormatDetailLabel(
        double remainingUsd,
        double? prepaidTotalUsd,
        double? prepaidUsedUsd,
        string currencyCode)
    {
        var remainingLabel = currencyCode == "USD"
            ? $"${remainingUsd.ToString("F2", CultureInfo.InvariantCulture)} left"
            : $"{remainingUsd.ToString("F2", CultureInfo.InvariantCulture)} {currencyCode} left";

        if (prepaidTotalUsd is not { } total || prepaidUsedUsd is not { } used || currencyCode != "USD")
            return remainingLabel;

        return $"{remainingLabel} · ${used.ToString("F2", CultureInfo.InvariantCulture)} used of ${total.ToString("F2", CultureInfo.InvariantCulture)}";
    }
}

namespace DeezFuelGauge.Models;

public sealed class QuotaAlertSettings
{
    public bool Enabled { get; set; } = true;
    public int DaysBeforePeriodEnd { get; set; } = 7;
    public int MaxPercentUsed { get; set; } = 75;

    public bool CursorPlan { get; set; } = true;
    public bool OpenAiCursor { get; set; }
    public bool OpenAiPlatform { get; set; } = true;
    public bool ClaudeCursor { get; set; }
    public bool ClaudeApi { get; set; } = true;
    public bool GeminiCursor { get; set; }
    public bool OpenRouterKeyLimit { get; set; } = true;
    public bool OpenCodeZenMonthly { get; set; } = true;
    public bool OpenCodeGoMonthly { get; set; } = true;
    public bool ClaudeExtraUsageMonthly { get; set; } = true;
    public bool FalBalance { get; set; } = true;
    public bool XaiBalance { get; set; } = true;
    public bool GrokBotWeekly { get; set; } = true;
}

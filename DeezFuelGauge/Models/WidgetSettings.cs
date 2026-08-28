namespace DeezFuelGauge.Models;

public sealed class WidgetSettings
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public bool IsPositionPinned { get; set; }
    public bool IsBreakdownExpanded { get; set; }
    public bool IsCodexLimitsExpanded { get; set; } = true;
    public bool IsClaudeProLimitsExpanded { get; set; } = true;
    public bool IsAntigravityLimitsExpanded { get; set; }
    public bool IsOpenCodeGoLimitsExpanded { get; set; }
    public bool IsSettingsExpanded { get; set; }
    public SettingsExpandedProvider SettingsExpandedProvider { get; set; } = SettingsExpandedProvider.None;
    public bool IsCursorProviderExpanded { get; set; }
    public bool IsOpenAiProviderExpanded { get; set; }
    public bool IsClaudeProviderExpanded { get; set; }
    public bool IsGeminiProviderExpanded { get; set; }
    public bool IsOpenRouterProviderExpanded { get; set; }
    public bool IsOpenCodeProviderExpanded { get; set; }
    public bool IsFalProviderExpanded { get; set; }
    public bool IsXaiProviderExpanded { get; set; }
    public ProviderBillingSettings Cursor { get; set; } = new();
    public ProviderBillingSettings OpenAi { get; set; } = new();
    public ProviderBillingSettings Claude { get; set; } = new();
    public ProviderBillingSettings Gemini { get; set; } = new();
    public ProviderBillingSettings OpenRouter { get; set; } = new();
    public ProviderBillingSettings OpenCode { get; set; } = new();
    public ProviderBillingSettings Fal { get; set; } = new();
    public ProviderBillingSettings Xai { get; set; } = new();
    public ProviderBillingSettings GrokBot { get; set; } = new() { ShowProLimits = true };
    public bool ShowBreakdown { get; set; } = true;
    public bool ShowDiskDrives { get; set; } = true;
    public bool ShowDiskDetails { get; set; } = true;
    public List<string> DisabledDiskDrives { get; set; } = [];
    public bool ShowCpuUsage { get; set; }
    public bool ShowGpuUsage { get; set; }
    public bool ShowRamUsage { get; set; }
    public bool ShowCpuTemp { get; set; }
    public bool ShowCpuTempDetail { get; set; } = true;
    public bool ShowHardwareDetails { get; set; } = true;
    public int RefreshIntervalMinutes { get; set; } = 5;
    public bool LaunchAtLogin { get; set; }
    public bool UseCompactMode { get; set; }
    public bool HasCompletedFirstRun { get; set; }
    public QuotaAlertSettings QuotaAlerts { get; set; } = new();
}

using DeezFuelGauge.Models;

namespace DeezFuelGauge.Settings;

public sealed class ProviderSourceViewModel : ViewModelBase
{
    private bool _isEnabled;
    private bool _showDetails = true;
    private bool _showLimits = true;
    private bool _showBreakdown;
    private string _status = "";
    private bool _isAdvancedExpanded;
    private string _orgId = "";
    private string _budget = "";
    private string _workspaceId = "";
    private string _workspaceWatermark = "Workspace ID";
    private string _apiKeyWatermark = "";
    private string _managementApiKeyWatermark = "";
    private string _sessionWatermark = "";

    public ProviderSourceKind Kind { get; set; }

    public string Name { get; set; } = "";

    public string? DrivePath { get; set; }

    public bool HasEnableToggle { get; set; } = true;

    public bool HasDetailsToggle { get; set; } = true;

    public bool HasLimitsToggle { get; set; }

    public bool HasBreakdownToggle { get; set; }

    public bool ShowConnect { get; set; }

    public bool ShowTest { get; set; }

    public bool SupportsAdvanced { get; set; }

    public string AdvancedHint { get; set; } = "";

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool ShowDetails
    {
        get => _showDetails;
        set => SetProperty(ref _showDetails, value);
    }

    public bool ShowLimits
    {
        get => _showLimits;
        set => SetProperty(ref _showLimits, value);
    }

    public bool ShowBreakdown
    {
        get => _showBreakdown;
        set => SetProperty(ref _showBreakdown, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsAdvancedExpanded
    {
        get => _isAdvancedExpanded;
        set => SetProperty(ref _isAdvancedExpanded, value);
    }

    public bool ShowAdvancedSection { get; set; }

    public bool HasApiKeySaved { get; set; }

    public bool HasSessionSaved { get; set; }

    public bool ShowApiKeyField { get; set; }

    public bool ShowManagementApiKeyField { get; set; }

    public bool HasManagementApiKeySaved { get; set; }

    public bool ShowSessionField { get; set; }

    public bool ShowBudgetField { get; set; }

    public bool ShowOrgIdField { get; set; }

    public bool ShowWorkspaceField { get; set; }

    public bool ShowStandaloneBudgetField => ShowBudgetField && !ShowOrgIdField;

    public string OrgId
    {
        get => _orgId;
        set => SetProperty(ref _orgId, value);
    }

    public string Budget
    {
        get => _budget;
        set => SetProperty(ref _budget, value);
    }

    public string WorkspaceId
    {
        get => _workspaceId;
        set => SetProperty(ref _workspaceId, value);
    }

    public string WorkspaceWatermark
    {
        get => _workspaceWatermark;
        set => SetProperty(ref _workspaceWatermark, value);
    }

    public string ApiKeyWatermark
    {
        get => _apiKeyWatermark;
        set => SetProperty(ref _apiKeyWatermark, value);
    }

    public string ManagementApiKeyWatermark
    {
        get => _managementApiKeyWatermark;
        set => SetProperty(ref _managementApiKeyWatermark, value);
    }

    public string SessionWatermark
    {
        get => _sessionWatermark;
        set => SetProperty(ref _sessionWatermark, value);
    }

    public string AutoAuthSummary { get; set; } = "";

    public bool HasAutoAuth { get; set; }

    public bool ShowSignInButton { get; set; }

    public bool ShowDisconnectOAuth { get; set; }

    public bool IsOAuthPending { get; set; }

    public void NotifyAdvancedVisibility()
    {
        OnPropertyChanged(nameof(ShowAdvancedSection));
        OnPropertyChanged(nameof(HasAutoAuth));
        OnPropertyChanged(nameof(AutoAuthSummary));
        OnPropertyChanged(nameof(ShowApiKeyField));
        OnPropertyChanged(nameof(HasApiKeySaved));
        OnPropertyChanged(nameof(ApiKeyWatermark));
        OnPropertyChanged(nameof(ShowManagementApiKeyField));
        OnPropertyChanged(nameof(HasManagementApiKeySaved));
        OnPropertyChanged(nameof(ManagementApiKeyWatermark));
        OnPropertyChanged(nameof(ShowSessionField));
        OnPropertyChanged(nameof(HasSessionSaved));
        OnPropertyChanged(nameof(SessionWatermark));
        OnPropertyChanged(nameof(ShowSignInButton));
        OnPropertyChanged(nameof(ShowDisconnectOAuth));
        OnPropertyChanged(nameof(IsOAuthPending));
    }
}

using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class SettingsSectionMapperTests
{
    [Fact]
    public void BuildDiskSection_lists_all_available_drives()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();

        SettingsSectionMapper.PopulateSections(sections, new WidgetSettings(), host);

        var diskSection = sections.Single(s => s.ProviderId == SettingsExpandedProvider.Disk);
        var driveSources = diskSection.Sources.Where(s => s.Kind == ProviderSourceKind.DiskDrive).ToList();
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();

        Assert.Equal(descriptors.Count, driveSources.Count);
        Assert.Contains(diskSection.Sources, s => s.Kind == ProviderSourceKind.DiskDrives);
        Assert.All(driveSources, s => Assert.False(string.IsNullOrWhiteSpace(s.DrivePath)));
    }

    [Fact]
    public void ApplyDisk_round_trips_disabled_drives()
    {
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();
        if (descriptors.Count == 0)
            return;

        var disabledDrive = descriptors[0].Name;
        var original = new WidgetSettings
        {
            ShowDiskDrives = true,
            ShowDiskDetails = false,
            DisabledDiskDrives = [disabledDrive]
        };

        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();
        SettingsSectionMapper.PopulateSections(sections, original, host);

        var committed = new WidgetSettings();
        SettingsSectionMapper.ApplyToSettings(sections, committed, SettingsExpandedProvider.None);

        Assert.True(committed.ShowDiskDrives);
        Assert.False(committed.ShowDiskDetails);
        Assert.Equal([disabledDrive], committed.DisabledDiskDrives);
    }

    [Fact]
    public void OnMasterEnableChanged_does_not_reset_per_drive_toggles()
    {
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();
        if (descriptors.Count < 2)
            return;

        var settings = new WidgetSettings
        {
            ShowDiskDrives = true,
            DisabledDiskDrives = [descriptors[1].Name]
        };

        var viewModel = CreateViewModel();
        viewModel.Load(settings);

        var diskSection = viewModel.Sections.Single(s => s.ProviderId == SettingsExpandedProvider.Disk);
        var driveSources = diskSection.Sources
            .Where(s => s.Kind == ProviderSourceKind.DiskDrive)
            .ToList();
        var firstDrive = driveSources[0];
        var secondDrive = driveSources[1];

        Assert.True(firstDrive.IsEnabled);
        Assert.False(secondDrive.IsEnabled);

        viewModel.OnMasterEnableChanged(diskSection, false);
        viewModel.OnMasterEnableChanged(diskSection, true);

        Assert.True(firstDrive.IsEnabled);
        Assert.False(secondDrive.IsEnabled);
    }

    [Fact]
    public void PopulateSections_includes_fal_section()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();

        SettingsSectionMapper.PopulateSections(sections, new WidgetSettings(), host);

        var fal = sections.Single(s => s.ProviderId == SettingsExpandedProvider.Fal);
        Assert.Equal("fal.ai", fal.Title);
        Assert.Contains(fal.Sources, s => s.Kind == ProviderSourceKind.FalCredits);
    }

    [Fact]
    public void ApplyFal_round_trips_enable_and_details()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();
        var settings = new WidgetSettings
        {
            Fal = new ProviderBillingSettings { ShowProLimits = true, ShowDetails = false }
        };

        SettingsSectionMapper.PopulateSections(sections, settings, host);
        var fal = sections.Single(s => s.ProviderId == SettingsExpandedProvider.Fal);
        fal.Sources.Single().IsEnabled = false;
        fal.Sources.Single().ShowDetails = true;

        var applied = new WidgetSettings();
        SettingsSectionMapper.ApplyToSettings(sections, applied, SettingsExpandedProvider.Fal);

        Assert.False(applied.Fal.ShowProLimits);
        Assert.True(applied.Fal.ShowDetails);
    }

    [Fact]
    public void PopulateSections_includes_xai_section()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();

        SettingsSectionMapper.PopulateSections(sections, new WidgetSettings(), host);

        var xai = sections.Single(s => s.ProviderId == SettingsExpandedProvider.Xai);
        Assert.Equal("xAI", xai.Title);
        Assert.Contains(xai.Sources, s => s.Kind == ProviderSourceKind.XaiCredits);
    }

    [Fact]
    public void ApplyXai_round_trips_enable_details_and_team_id()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();
        var settings = new WidgetSettings
        {
            Xai = new ProviderBillingSettings
            {
                ShowProLimits = true,
                ShowDetails = false,
                WorkspaceId = "team-old"
            }
        };

        SettingsSectionMapper.PopulateSections(sections, settings, host);
        var xai = sections.Single(s => s.ProviderId == SettingsExpandedProvider.Xai);
        xai.Sources.Single().IsEnabled = false;
        xai.Sources.Single().ShowDetails = true;
        xai.Sources.Single().WorkspaceId = "team-new";

        var applied = new WidgetSettings();
        SettingsSectionMapper.ApplyToSettings(sections, applied, SettingsExpandedProvider.Xai);

        Assert.False(applied.Xai.ShowProLimits);
        Assert.True(applied.Xai.ShowDetails);
        Assert.Equal("team-new", applied.Xai.WorkspaceId);
    }

    private static SettingsPanelViewModel CreateViewModel() =>
        new(
            new ProviderEasySetupService(),
            new OpenAiBillingClient(),
            new CodexUsageClient(),
            new AntigravityUsageClient(),
            new OpenRouterUsageClient(),
            new OpenCodeUsageClient(),
            cursorTokenReader: () => new CursorTokens());
}

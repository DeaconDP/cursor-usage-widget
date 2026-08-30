using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class WindowAnchorHelperTests
{
    [Theory]
    [InlineData(100, 150, 200, 150)]
    [InlineData(200, 150, 100, 150)]
    [InlineData(100, 100, 50, 50)]
    public void CompensateVerticalGrowth_moves_top_up_when_height_increases(
        double oldHeight,
        double newHeight,
        int currentY,
        int expectedY)
    {
        var result = WindowAnchorHelper.CompensateVerticalGrowth(oldHeight, newHeight, currentY);
        Assert.Equal(expectedY, result);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080, 300, 400, 810, 340)]
    [InlineData(100, 50, 800, 600, 300, 400, 350, 150)]
    public void ComputeCenteredPosition_centers_window_in_working_area(
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight,
        int windowWidth,
        int windowHeight,
        int expectedX,
        int expectedY)
    {
        var (x, y) = WindowAnchorHelper.ComputeCenteredPosition(
            workAreaX, workAreaY, workAreaWidth, workAreaHeight, windowWidth, windowHeight);

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Theory]
    [InlineData(300, 200, 100)]
    [InlineData(450.5, 280.5, 170)]
    public void ComputeBottomAnchoredY_keeps_bottom_edge_fixed(double anchorBottom, double height, int expectedY)
    {
        var result = WindowAnchorHelper.ComputeBottomAnchoredY(anchorBottom, height);
        Assert.Equal(expectedY, result);
        Assert.Equal(anchorBottom, result + height, precision: 5);
    }

    [Theory]
    [InlineData(200, 100, 150, 150)] // grow: Y moves up by 50
    [InlineData(200, 150, 100, 250)] // shrink: Y moves down by 50
    [InlineData(40, 36, 260, -184)] // near top of screen still grows upward
    public void ResolveSettingsExpandEndY_keeps_bottom_edge_fixed(
        int currentY,
        double currentHeight,
        double newHeight,
        int expectedY)
    {
        var result = WindowAnchorHelper.ResolveSettingsExpandEndY(currentY, currentHeight, newHeight);
        Assert.Equal(expectedY, result);
        Assert.Equal(currentY + currentHeight, result + newHeight, precision: 5);
    }

    [Fact]
    public void ResolveSettingsExpandEndY_prefers_bottom_lock_even_when_nearer_top_than_CompensateSizeChange()
    {
        var areas = new[] { (0, 0, 1920, 1080) };
        const int currentX = 40;
        const int currentY = 40;
        const double currentHeight = 36;
        const double newHeight = 260;

        var (_, edgeAwareY) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: currentHeight,
            newWidth: 300,
            newHeight: newHeight,
            currentX: currentX,
            currentY: currentY,
            areas);

        var settingsY = WindowAnchorHelper.ResolveSettingsExpandEndY(currentY, currentHeight, newHeight);

        Assert.Equal(40, edgeAwareY); // edge-aware keeps top when near top
        Assert.Equal(40 + 36 - 260, settingsY); // settings always bottom-locks
        Assert.True(settingsY < edgeAwareY);
    }

    [Fact]
    public void ComputeCenteredPosition_clamps_to_work_area_origin_when_window_is_larger()
    {
        var (x, y) = WindowAnchorHelper.ComputeCenteredPosition(
            100, 50, 800, 600, 900, 700);

        Assert.Equal(100, x);
        Assert.Equal(50, y);
    }

    [Fact]
    public void ClampToWorkingAreas_keeps_position_when_already_visible()
    {
        var areas = new[] { (0, 0, 1728, 1117) };

        var (x, y) = WindowAnchorHelper.ClampToWorkingAreas(1200, 600, 300, 254, areas);

        Assert.Equal(1200, x);
        Assert.Equal(600, y);
    }

    [Fact]
    public void ClampToWorkingAreas_pulls_offscreen_pinned_window_onto_display()
    {
        // Saved coords from a disconnected secondary monitor (below the laptop panel).
        var areas = new[] { (0, 0, 1728, 1117) };

        var (x, y) = WindowAnchorHelper.ClampToWorkingAreas(1428, 1353, 300, 254, areas);

        Assert.Equal(1428, x);
        Assert.Equal(1117 - 254, y);
        Assert.True(WindowAnchorHelper.HasVisibleOverlap(x, y, 300, 254, areas[0]));
    }

    [Fact]
    public void ClampToWorkingAreas_chooses_nearest_of_multiple_displays()
    {
        var areas = new[]
        {
            (0, 0, 1728, 1117),
            (1728, 0, 1920, 1080)
        };

        var (x, y) = WindowAnchorHelper.ClampToWorkingAreas(4000, 40, 300, 254, areas);

        Assert.Equal(1728 + 1920 - 300, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void CompensateSizeChange_grows_down_and_right_when_away_from_far_edges()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: 36,
            newWidth: 300,
            newHeight: 260,
            currentX: 40,
            currentY: 40,
            areas);

        Assert.Equal(40, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void CompensateSizeChange_keeps_right_and_bottom_when_near_those_edges()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: 36,
            newWidth: 300,
            newHeight: 260,
            currentX: 1920 - 140 - 10,
            currentY: 1080 - 36 - 10,
            areas);

        Assert.Equal(1920 - 300 - 10, x);
        Assert.Equal(1080 - 260 - 10, y);
    }

    [Fact]
    public void CompensateSizeChange_clamps_into_working_area_when_expanding_off_top()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: 36,
            newWidth: 300,
            newHeight: 400,
            currentX: 10,
            currentY: 10,
            areas);

        Assert.Equal(10, x);
        Assert.Equal(10, y);
        Assert.True(WindowAnchorHelper.HasVisibleOverlap(x, y, 300, 400, areas[0]));
    }
}

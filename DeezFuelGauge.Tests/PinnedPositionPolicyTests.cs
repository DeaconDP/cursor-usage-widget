using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class PinnedPositionPolicyTests
{
    [Fact]
    public void ShouldCapture_false_when_not_pinned()
    {
        Assert.False(PinnedPositionPolicy.ShouldCapture(
            isPositionPinned: false,
            initialPositionApplied: true,
            compactAnimActive: false,
            useCompactMode: false,
            showingCompactRest: false));
    }

    [Fact]
    public void ShouldCapture_false_before_initial_position_applied()
    {
        Assert.False(PinnedPositionPolicy.ShouldCapture(
            isPositionPinned: true,
            initialPositionApplied: false,
            compactAnimActive: false,
            useCompactMode: true,
            showingCompactRest: true));
    }

    [Fact]
    public void ShouldCapture_false_while_animating()
    {
        Assert.False(PinnedPositionPolicy.ShouldCapture(
            isPositionPinned: true,
            initialPositionApplied: true,
            compactAnimActive: true,
            useCompactMode: true,
            showingCompactRest: true));
    }

    [Fact]
    public void ShouldCapture_false_when_compact_and_expanded()
    {
        Assert.False(PinnedPositionPolicy.ShouldCapture(
            isPositionPinned: true,
            initialPositionApplied: true,
            compactAnimActive: false,
            useCompactMode: true,
            showingCompactRest: false));
    }

    [Fact]
    public void ShouldCapture_true_at_compact_rest_when_pinned()
    {
        Assert.True(PinnedPositionPolicy.ShouldCapture(
            isPositionPinned: true,
            initialPositionApplied: true,
            compactAnimActive: false,
            useCompactMode: true,
            showingCompactRest: true));
    }

    [Fact]
    public void ShouldCapture_true_when_pinned_and_compact_off()
    {
        Assert.True(PinnedPositionPolicy.ShouldCapture(
            isPositionPinned: true,
            initialPositionApplied: true,
            compactAnimActive: false,
            useCompactMode: false,
            showingCompactRest: false));
    }

    [Fact]
    public void ShouldSchedulePositionSave_matches_ShouldCapture()
    {
        Assert.Equal(
            PinnedPositionPolicy.ShouldCapture(true, true, false, true, true),
            PinnedPositionPolicy.ShouldSchedulePositionSave(true, true, false, true, true));
        Assert.Equal(
            PinnedPositionPolicy.ShouldCapture(true, true, true, true, true),
            PinnedPositionPolicy.ShouldSchedulePositionSave(true, true, true, true, true));
        Assert.Equal(
            PinnedPositionPolicy.ShouldCapture(true, false, false, false, false),
            PinnedPositionPolicy.ShouldSchedulePositionSave(true, false, false, false, false));
    }
}

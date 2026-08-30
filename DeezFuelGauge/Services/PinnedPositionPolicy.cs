namespace DeezFuelGauge.Services;

/// <summary>
/// When pinned Left/Top may be captured from the live window position.
/// Compact mode only captures at compact-rest, never mid-animation or before initial restore.
/// </summary>
public static class PinnedPositionPolicy
{
    public static bool ShouldCapture(
        bool isPositionPinned,
        bool initialPositionApplied,
        bool compactAnimActive,
        bool useCompactMode,
        bool showingCompactRest)
    {
        if (!isPositionPinned || !initialPositionApplied || compactAnimActive)
            return false;

        return !useCompactMode || showingCompactRest;
    }

    /// <summary>
    /// Whether a PositionChanged event should schedule a debounced settings save for pin coords.
    /// </summary>
    public static bool ShouldSchedulePositionSave(
        bool isPositionPinned,
        bool initialPositionApplied,
        bool compactAnimActive,
        bool useCompactMode,
        bool showingCompactRest) =>
        ShouldCapture(
            isPositionPinned,
            initialPositionApplied,
            compactAnimActive,
            useCompactMode,
            showingCompactRest);
}

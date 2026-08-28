namespace DeezFuelGauge.Services;

public static class CompactHoverController
{
    public static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(400);

    public static bool ShouldShowFullLayout(
        bool useCompactMode,
        bool pointerOver,
        bool settingsExpanded,
        bool contextMenuOpen,
        bool dragging,
        bool inputFocused)
    {
        if (!useCompactMode)
            return true;

        return pointerOver
               || settingsExpanded
               || contextMenuOpen
               || dragging
               || inputFocused;
    }
}

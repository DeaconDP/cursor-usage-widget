namespace DeezFuelGauge.Services;

public static class WindowAnchorHelper
{
    /// <summary>
    /// Minimum intersection (px) on both axes before a pinned position counts as on-screen.
    /// </summary>
    public const int MinVisibleOverlapPx = 40;

    /// <summary>
    /// Returns a new Y position so the window bottom edge stays fixed when height changes.
    /// </summary>
    public static int CompensateVerticalGrowth(double oldHeight, double newHeight, int currentY)
    {
        var delta = (int)Math.Round(newHeight - oldHeight);
        return currentY - delta;
    }

    /// <summary>
    /// Repositions the window when both width and height change. Grows toward the interior of
    /// the nearest working area (keep the right edge when closer to the right, keep the bottom
    /// edge when closer to the bottom). Otherwise keeps the top-left origin.
    /// </summary>
    public static (int X, int Y) CompensateSizeChange(
        double oldWidth,
        double oldHeight,
        double newWidth,
        double newHeight,
        int currentX,
        int currentY,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workingAreas)
    {
        var keepRight = false;
        var keepBottom = false;
        if (workingAreas.Count > 0)
        {
            var oldW = Math.Max(1, (int)Math.Round(oldWidth));
            var oldH = Math.Max(1, (int)Math.Round(oldHeight));
            var area = FindNearestWorkingArea(currentX, currentY, oldW, oldH, workingAreas);
            var distLeft = currentX - area.X;
            var distRight = area.X + area.Width - (currentX + oldW);
            keepRight = distRight < distLeft;

            var distTop = currentY - area.Y;
            var distBottom = area.Y + area.Height - (currentY + oldH);
            keepBottom = distBottom < distTop;
        }

        var dx = (int)Math.Round(newWidth - oldWidth);
        var dy = (int)Math.Round(newHeight - oldHeight);
        var x = keepRight ? currentX - dx : currentX;
        var y = keepBottom ? currentY - dy : currentY;

        return ClampToWorkingAreas(
            x,
            y,
            Math.Max(1, (int)Math.Round(newWidth)),
            Math.Max(1, (int)Math.Round(newHeight)),
            workingAreas);
    }

    /// <summary>
    /// Returns the window Y position that keeps the bottom edge at <paramref name="anchorBottom"/>.
    /// </summary>
    public static int ComputeBottomAnchoredY(double anchorBottom, double height) =>
        (int)Math.Round(anchorBottom - height);

    /// <summary>
    /// End Y for settings expand/collapse: keep the current bottom edge fixed while height changes.
    /// </summary>
    public static int ResolveSettingsExpandEndY(int currentY, double currentHeight, double newHeight) =>
        ComputeBottomAnchoredY(currentY + currentHeight, newHeight);

    public static (int X, int Y) ComputeCenteredPosition(
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight,
        int windowWidth,
        int windowHeight)
    {
        var x = workAreaX + Math.Max(0, (workAreaWidth - windowWidth) / 2);
        var y = workAreaY + Math.Max(0, (workAreaHeight - windowHeight) / 2);
        return (x, y);
    }

    /// <summary>
    /// Keeps a pinned window on a connected display. If the rect does not overlap any working
    /// area by at least <see cref="MinVisibleOverlapPx"/> on both axes (e.g. after a monitor
    /// was unplugged), clamps into the nearest working area.
    /// </summary>
    public static (int X, int Y) ClampToWorkingAreas(
        int x,
        int y,
        int windowWidth,
        int windowHeight,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workingAreas)
    {
        if (workingAreas.Count == 0 || windowWidth <= 0 || windowHeight <= 0)
            return (x, y);

        foreach (var area in workingAreas)
        {
            if (HasVisibleOverlap(x, y, windowWidth, windowHeight, area))
                return (x, y);
        }

        var target = FindNearestWorkingArea(x, y, windowWidth, windowHeight, workingAreas);
        return ClampIntoArea(x, y, windowWidth, windowHeight, target);
    }

    public static bool HasVisibleOverlap(
        int x,
        int y,
        int windowWidth,
        int windowHeight,
        (int X, int Y, int Width, int Height) area)
    {
        var overlapW = OverlapLength(x, windowWidth, area.X, area.Width);
        var overlapH = OverlapLength(y, windowHeight, area.Y, area.Height);
        return overlapW >= MinVisibleOverlapPx && overlapH >= MinVisibleOverlapPx;
    }

    private static (int X, int Y, int Width, int Height) FindNearestWorkingArea(
        int x,
        int y,
        int windowWidth,
        int windowHeight,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workingAreas)
    {
        var windowCx = x + windowWidth / 2.0;
        var windowCy = y + windowHeight / 2.0;
        var best = workingAreas[0];
        var bestDist = double.MaxValue;

        foreach (var area in workingAreas)
        {
            var areaCx = area.X + area.Width / 2.0;
            var areaCy = area.Y + area.Height / 2.0;
            var dx = windowCx - areaCx;
            var dy = windowCy - areaCy;
            var dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = area;
            }
        }

        return best;
    }

    private static (int X, int Y) ClampIntoArea(
        int x,
        int y,
        int windowWidth,
        int windowHeight,
        (int X, int Y, int Width, int Height) area)
    {
        var maxX = area.X + Math.Max(0, area.Width - windowWidth);
        var maxY = area.Y + Math.Max(0, area.Height - windowHeight);
        var clampedX = Math.Clamp(x, area.X, maxX);
        var clampedY = Math.Clamp(y, area.Y, maxY);
        return (clampedX, clampedY);
    }

    private static int OverlapLength(int start, int length, int areaStart, int areaLength)
    {
        var end = start + length;
        var areaEnd = areaStart + areaLength;
        var overlapStart = Math.Max(start, areaStart);
        var overlapEnd = Math.Min(end, areaEnd);
        return Math.Max(0, overlapEnd - overlapStart);
    }
}

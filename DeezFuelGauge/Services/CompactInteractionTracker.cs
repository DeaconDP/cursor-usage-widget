using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace DeezFuelGauge.Services;

public static class CompactInteractionTracker
{
    public static bool ShouldKeepFullLayoutForFocus(IInputElement? focusedElement)
    {
        if (focusedElement is not Visual visual)
            return false;

        if (!IsVisibleInTree(visual))
            return false;

        return focusedElement is TextBox or ComboBox or NumericUpDown;
    }

    public static bool IsPointerWithinBounds(Point pointerPosition, Size boundsSize) =>
        pointerPosition.X >= 0
        && pointerPosition.Y >= 0
        && pointerPosition.X <= boundsSize.Width
        && pointerPosition.Y <= boundsSize.Height;

    private static bool IsVisibleInTree(Visual visual)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (!current.IsVisible)
                return false;
        }

        return true;
    }
}

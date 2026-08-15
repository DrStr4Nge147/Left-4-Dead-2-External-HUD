using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OverlayHud.ViewModel;

/// <summary>
/// Measures a HUD border's content without the window's arranged bounds. ActualHeight and
/// ActualWidth report the clipped layout slot once the panel reaches a screen edge, which
/// cannot be used to decide whether the content overflowed that edge.
/// </summary>
internal static class LayoutMeasurement
{
    public static Size NaturalSize(Border panel)
    {
        if (panel.Child is not UIElement content) return new Size();

        // ItemsSource can change from one column to two immediately before this call.
        // WPF otherwise reuses the still-valid DesiredSize from the old item layout.
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        Size measured = new();
        for (int pass = 0; pass < 6; pass++)
        {
            // An ItemsControl can create its item containers during a measure pass. Repeat
            // until the desired size settles so a newly-created card cannot grow into the
            // sibling You panel after the fit pass has already accepted the roster.
            InvalidateMeasureTree(content);
            content.Measure(infinite);
            Size current = content.DesiredSize;
            if (pass > 0 && Math.Abs(current.Width - measured.Width) < 0.1
                          && Math.Abs(current.Height - measured.Height) < 0.1)
            {
                measured = current;
                break;
            }

            measured = current;
        }

        return new Size(
            measured.Width + panel.Padding.Left + panel.Padding.Right
                                      + panel.BorderThickness.Left + panel.BorderThickness.Right,
            measured.Height + panel.Padding.Top + panel.Padding.Bottom
                                       + panel.BorderThickness.Top + panel.BorderThickness.Bottom);
    }

    private static void InvalidateMeasureTree(DependencyObject node)
    {
        if (node is UIElement element) element.InvalidateMeasure();

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            InvalidateMeasureTree(VisualTreeHelper.GetChild(node, i));
        }
    }
}

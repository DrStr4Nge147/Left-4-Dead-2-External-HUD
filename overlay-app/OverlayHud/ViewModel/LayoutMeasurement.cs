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
        InvalidateMeasureTree(content);
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return new Size(
            content.DesiredSize.Width + panel.Padding.Left + panel.Padding.Right
                                      + panel.BorderThickness.Left + panel.BorderThickness.Right,
            content.DesiredSize.Height + panel.Padding.Top + panel.Padding.Bottom
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

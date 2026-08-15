using System.Windows;
using System.Windows.Controls;

namespace OverlayHud.Services;

/// <summary>
/// Design is inherited from the HUD root so the same selector can serve row cards,
/// vertical cards, and the separate You card without keeping mutable global state.
/// </summary>
internal static class ConsistentHudDesign
{
    public static readonly DependencyProperty DesignProperty =
        DependencyProperty.RegisterAttached(
            "Design",
            typeof(string),
            typeof(ConsistentHudDesign),
            new FrameworkPropertyMetadata(
                ConsistentHudPolicy.BasicDesign,
                FrameworkPropertyMetadataOptions.Inherits
                | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static void SetDesign(DependencyObject element, string? value) =>
        element.SetValue(DesignProperty, ConsistentHudPolicy.ParseDesign(value));

    public static string GetDesign(DependencyObject element) =>
        (string?)element.GetValue(DesignProperty) ?? ConsistentHudPolicy.BasicDesign;
}

/// <summary>
/// Selects the consistent-HUD card template from the design inherited by its item
/// container. Scoreboard cards never use this selector.
/// </summary>
public sealed class ConsistentHudTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BasicTemplate { get; set; }
    public DataTemplate? MinimalistTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        bool minimalist = ConsistentHudPolicy.ParseDesign(
            ConsistentHudDesign.GetDesign(container)) == ConsistentHudPolicy.MinimalistDesign;
        DataTemplate? selected = minimalist ? MinimalistTemplate : BasicTemplate;
        return selected ?? BasicTemplate ?? MinimalistTemplate
            ?? throw new InvalidOperationException("A consistent HUD card template is missing.");
    }
}

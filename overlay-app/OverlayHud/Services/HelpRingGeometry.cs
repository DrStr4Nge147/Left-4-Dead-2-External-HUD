using System.Windows;
using System.Windows.Media;

namespace OverlayHud.Services;

/// <summary>
/// The reinforcement ring's arc. WPF has no circular progress control, so the sweep is a
/// stroked path rebuilt whenever the fraction changes.
///
/// The arc starts at twelve o'clock and runs clockwise, which is the direction every
/// cooldown dial in the genre runs, and it is drawn along the centre line of the stroke so
/// the ring's outer edge stays inside the box whatever thickness it is given.
/// </summary>
internal static class HelpRingGeometry
{
    /// <summary>Fractions this close to full are drawn as a circle - see <see cref="Arc"/>.</summary>
    private const double FullEpsilon = 0.0005;

    /// <summary>
    /// The sweep for <paramref name="fraction"/> of a ring <paramref name="diameter"/> across
    /// with a stroke <paramref name="thickness"/> wide.
    ///
    /// A full ring is an <see cref="EllipseGeometry"/> rather than a 360-degree arc: an arc
    /// whose end point is its start point is ambiguous, and WPF resolves it by drawing
    /// nothing, which would blank the ring at exactly the moment it is meant to be complete.
    /// </summary>
    public static Geometry Arc(double diameter, double thickness, double fraction)
    {
        double radius = Math.Max(0.0, (diameter - thickness) / 2.0);
        var centre = new Point(diameter / 2.0, diameter / 2.0);

        if (radius <= 0.0 || fraction <= 0.0) return Geometry.Empty;
        if (fraction >= 1.0 - FullEpsilon) return new EllipseGeometry(centre, radius, radius);

        double sweep = Math.Clamp(fraction, 0.0, 1.0) * 2.0 * Math.PI;
        var start = new Point(centre.X, centre.Y - radius);
        var end = new Point(centre.X + radius * Math.Sin(sweep),
                            centre.Y - radius * Math.Cos(sweep));

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            IsLargeArc = fraction > 0.5,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
}

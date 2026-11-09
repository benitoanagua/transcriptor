using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Transcriptor.UI.Spectrum;

public sealed class SpectrumProgressCircle : ProgressBar
{
    public override void Render(DrawingContext context)
    {
        var stroke = BorderThickness.Left;
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - stroke;
        if (radius <= 0 || stroke <= 0)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(null, new Pen(Background, stroke), center, radius, radius);

        var progress = Maximum > Minimum
            ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1)
            : 0;
        if (progress <= 0)
            return;
        if (progress >= 1)
        {
            context.DrawEllipse(null, new Pen(Foreground, stroke), center, radius, radius);
            return;
        }

        var startAngle = -90d;
        var endAngle = startAngle + 360 * progress;
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(start, false);
            path.ArcTo(
                end,
                new Size(radius, radius),
                0,
                progress > 0.5,
                SweepDirection.Clockwise);
        }

        context.DrawGeometry(null, new Pen(Foreground, stroke), geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var angle = angleDegrees * Math.PI / 180;
        return new Point(
            center.X + radius * Math.Cos(angle),
            center.Y + radius * Math.Sin(angle));
    }
}

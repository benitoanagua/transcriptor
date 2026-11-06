using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;

namespace Transcriptor.UI.Spectrum;

public sealed class SpectrumActionGroup : StackPanel { }

public sealed class SpectrumProgressBar : ProgressBar
{
    private Border? _segment;

    public SpectrumProgressBar()
    {
        SizeChanged += (_, _) => LayoutSegment();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _segment = e.NameScope.Find<Border>("PART_Segment");
        LayoutSegment();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty) LayoutSegment();
    }

    // Spectrum recorre un segmento sobre la pista; Avalonia no anima distancias
    // relativas, así que el recorrido se recalcula cuando cambia el ancho.
    private void LayoutSegment()
    {
        if (_segment is not { } segment) return;
        segment.ClearValue(Canvas.LeftProperty);
        if (!IsIndeterminate) return;

        var track = Math.Max(1.0, Bounds.Width);
        var length = Math.Max(1.0, track / 4);
        segment.Width = length;
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.6),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Canvas.LeftProperty, -length) } },
                new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Canvas.LeftProperty, track) } }
            }
        };
        animation.RunAsync(segment);
    }
}

public sealed class SpectrumBadge : ContentControl { }

public sealed class SpectrumStatusLight : ContentControl { }

public sealed class SpectrumDivider : Border { }

public sealed class SpectrumInlineAlert : ContentControl { }

public sealed class SpectrumToast : ContentControl { }

public sealed class SpectrumAlertDialog : ContentControl { }

public sealed class SpectrumListView : ListBox { }

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

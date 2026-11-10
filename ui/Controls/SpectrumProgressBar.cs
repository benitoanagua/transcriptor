using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;

namespace Transcriptor.UI.Spectrum;

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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Transcriptor.UI;

public sealed class WaveformView : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> PeaksProperty =
        AvaloniaProperty.Register<WaveformView, IReadOnlyList<double>?>(nameof(Peaks));
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<WaveformView, IBrush?>(nameof(Stroke));

    static WaveformView()
    {
        AffectsRender<WaveformView>(PeaksProperty, StrokeProperty);
    }

    public IReadOnlyList<double>? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Peaks is not { Count: > 1 } peaks || Stroke is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var pen = new Pen(Stroke, 1.25);
        var center = Bounds.Height / 2;
        var halfHeight = Math.Max(0, center - 3);
        for (var index = 0; index < peaks.Count; index++)
        {
            var x = (index + 0.5) * Bounds.Width / peaks.Count;
            var amplitude = Math.Clamp(peaks[index], 0, 1) * halfHeight;
            context.DrawLine(pen, new Point(x, center - amplitude), new Point(x, center + amplitude));
        }
    }
}

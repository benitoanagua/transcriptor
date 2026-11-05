using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Transcriptor.UI;

public sealed class WaveformView : Control
{
    private const int BarCount = 48;
    public static readonly StyledProperty<IReadOnlyList<double>?> LevelsProperty =
        AvaloniaProperty.Register<WaveformView, IReadOnlyList<double>?>(nameof(Levels));
    public static readonly StyledProperty<IBrush?> ColorProperty =
        AvaloniaProperty.Register<WaveformView, IBrush?>(nameof(Color));
    public static readonly StyledProperty<IBrush?> InactiveColorProperty =
        AvaloniaProperty.Register<WaveformView, IBrush?>(nameof(InactiveColor));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<WaveformView, bool>(nameof(IsActive));

    static WaveformView()
    {
        AffectsRender<WaveformView>(LevelsProperty, ColorProperty, InactiveColorProperty, IsActiveProperty);
    }

    public IReadOnlyList<double>? Levels
    {
        get => GetValue(LevelsProperty);
        set => SetValue(LevelsProperty, value);
    }

    public IBrush? Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public IBrush? InactiveColor
    {
        get => GetValue(InactiveColorProperty);
        set => SetValue(InactiveColorProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var brush = IsActive ? Color : InactiveColor ?? Color;
        if (brush is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (!IsActive)
        {
            context.DrawLine(new Pen(brush, 1), new Point(0, Bounds.Height / 2), new Point(Bounds.Width, Bounds.Height / 2));
            return;
        }

        var slotWidth = Bounds.Width / BarCount;
        var barWidth = Math.Max(0, slotWidth - 2);
        var maxHeight = Bounds.Height * 0.9;
        var levels = Levels ?? Array.Empty<double>();
        var leadingEmpty = Math.Max(0, BarCount - levels.Count);
        for (var index = 0; index < BarCount; index++)
        {
            var height = index < leadingEmpty ? 0 : Math.Clamp(levels[index - leadingEmpty], 0, 1) * maxHeight;
            if (height <= 0) continue;
            var x = index * slotWidth + (slotWidth - barWidth) / 2;
            var y = (Bounds.Height - height) / 2;
            context.DrawRectangle(brush, null, new Rect(x, y, barWidth, height), 1);
        }
    }
}

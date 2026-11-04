using Avalonia;

namespace Transcriptor.Desktop;

/// <summary>Pure placement validation for keeping the Mini-Mode title bar reachable.</summary>
public static class MiniModePlacement
{
    public static PixelPoint GetVisiblePosition(
        PixelPoint requested,
        int windowWidth,
        int windowHeight,
        IReadOnlyList<PixelRect> workingAreas,
        PixelRect? primaryWorkingArea)
    {
        const int minimumVisibleWidth = 80;
        const int titleBarHeight = 40;

        foreach (var area in workingAreas)
        {
            var visibleWidth = Math.Min(requested.X + windowWidth, area.Right)
                - Math.Max(requested.X, area.X);
            var visibleHeight = Math.Min(requested.Y + Math.Min(windowHeight, titleBarHeight), area.Bottom)
                - Math.Max(requested.Y, area.Y);
            if (visibleWidth >= minimumVisibleWidth && visibleHeight >= titleBarHeight)
                return requested;
        }

        return primaryWorkingArea is { } primary
            ? new PixelPoint(primary.X + 24, primary.Y + 24)
            : new PixelPoint(24, 24);
    }
}

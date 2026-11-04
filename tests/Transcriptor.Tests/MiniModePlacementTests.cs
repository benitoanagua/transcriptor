using Avalonia;
using Transcriptor.Desktop;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class MiniModePlacementTests
{
    [Fact]
    public void GetVisiblePosition_RestoresVisibleWindow_OrFallsBackToPrimaryWorkingArea()
    {
        var primary = new PixelRect(0, 0, 1920, 1080);
        var secondary = new PixelRect(1920, 0, 1920, 1080);

        Assert.Equal(new PixelPoint(1950, 20),
            MiniModePlacement.GetVisiblePosition(new PixelPoint(1950, 20), 420, 150,
                [primary, secondary], primary));

        Assert.Equal(new PixelPoint(24, 24),
            MiniModePlacement.GetVisiblePosition(new PixelPoint(5000, 5000), 420, 150,
                [primary], primary));
    }
}

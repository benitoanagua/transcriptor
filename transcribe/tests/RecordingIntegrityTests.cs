using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class RecordingIntegrityTests
{
    [Fact]
    public void MarkIncomplete_CreatesPersistentMarker_AndIsIdempotent()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TranscriptorIntegrityTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.False(RecordingIntegrity.IsIncomplete(directory));

            RecordingIntegrity.MarkIncomplete(directory, new IOException("simulated capture failure"));
            var markerPath = Path.Combine(directory, RecordingIntegrity.IncompleteMarkerFileName);

            Assert.True(File.Exists(markerPath));
            Assert.True(RecordingIntegrity.IsIncomplete(directory));
            var firstContents = File.ReadAllText(markerPath);

            RecordingIntegrity.MarkIncomplete(directory, new InvalidOperationException("second failure"));

            Assert.Equal(firstContents, File.ReadAllText(markerPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IsIncomplete_RejectsBlankDirectory()
    {
        Assert.Throws<ArgumentException>(() => RecordingIntegrity.IsIncomplete(" "));
    }
}

using Transcriptor.Desktop;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class PortableModeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TranscriptorPortableTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void IsPortableAt_RequiresTheMarkerFile()
    {
        Directory.CreateDirectory(_directory);
        Assert.False(PortableMode.IsPortableAt(_directory));

        File.WriteAllText(Path.Combine(_directory, PortableMode.MarkerFileName), "portable");

        Assert.True(PortableMode.IsPortableAt(_directory));
    }

    [Fact]
    public void IsPortableAt_RejectsBlankDirectory()
    {
        Assert.False(PortableMode.IsPortableAt(string.Empty));
    }

    [Fact]
    public void CreateDefault_WithPortableRoot_KeepsEverythingInsideTheApplicationFolder()
    {
        var settings = AppSettings.CreateDefault(_directory);

        Assert.Equal(Path.Combine(_directory, "data", "Grabaciones"), settings.RecordingsDirectory);
        Assert.Equal(Path.Combine(_directory, "whisper", "whisper-cli.exe"), settings.WhisperExecutablePath);
        Assert.Equal(Path.Combine(_directory, "models", "ggml-small.bin"), settings.WhisperModelPath);
        Assert.Equal(Path.Combine(_directory, "data", "settings.json"), PortableMode.SettingsPath(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}

using Transcriptor.Desktop;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TranscriptorSettingsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_PreservesAllPreferences()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        var expected = new AppSettings
        {
            RecordingsDirectory = "C:\\Calls",
            WhisperExecutablePath = "C:\\Whisper\\whisper-cli.exe",
            WhisperModelPath = "C:\\Models\\ggml-small.bin",
            Language = "EN",
            MicrophoneDeviceId = "mic-123",
            OutputDeviceId = "speaker-456"
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Null(actual.Warning);
        Assert.Equal(expected with { Language = "en" }, actual.Settings);
    }

    [Fact]
    public void Load_IgnoresLegacyRecorderDllSetting()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, """{"RecorderDllPath":"C:\\Tools\\rec.dll","Language":"es"}""");

        var result = new SettingsStore(path).Load();

        Assert.Null(result.Warning);
        Assert.Equal("es", result.Settings.Language);
        new SettingsStore(path).Save(result.Settings);
        Assert.DoesNotContain("RecorderDllPath", File.ReadAllText(path));
    }

    [Fact]
    public void Load_WhenFileIsMissing_ReturnsDefaultsWithoutWarning()
    {
        var store = new SettingsStore(Path.Combine(_directory, "missing.json"));

        var result = store.Load();

        Assert.Null(result.Warning);
        Assert.Equal("es", result.Settings.Language);
    }

    [Fact]
    public void Load_WhenJsonIsCorrupt_UsesDefaultsAndPreservesBackup()
    {
        var path = Path.Combine(_directory, "settings.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ invalid json");

        var result = new SettingsStore(path).Load();

        Assert.NotNull(result.Warning);
        Assert.Equal("es", result.Settings.Language);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(_directory, "settings.json.corrupt-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}

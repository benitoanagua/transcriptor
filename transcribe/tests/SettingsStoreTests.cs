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
            OutputDeviceId = "speaker-456",
            MiniModeAutoShow = false,
            ThemeMode = "Light",
            MiniModeAlwaysOnTop = false,
            MiniModeLeft = 120,
            MiniModeTop = 240,
            MiniModeWidth = 420,
            MiniModeHeight = 166
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Null(actual.Warning);
        Assert.Equal(expected with { Language = "en" }, actual.Settings);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4000, 3650)]
    public void Load_NormalizesRetentionDays(int configuredDays, int expectedDays)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"RetentionDays\":" + configuredDays + "}");

        var result = new SettingsStore(path).Load();

        Assert.Null(result.Warning);
        Assert.Equal(expectedDays, result.Settings.RetentionDays);
    }

    [Fact]
    public void Load_NormalizesUnknownThemeToDark()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"ThemeMode\":\"Sepia\"}");

        var result = new SettingsStore(path).Load();

        Assert.Null(result.Warning);
        Assert.Equal("Dark", result.Settings.ThemeMode);
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


    [Fact]
    public void Load_WhenJsonIsCorrupt_UsesInjectedClockForBackupName()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ invalid json");
        var clock = new FixedClock(new DateTimeOffset(2024, 2, 3, 4, 5, 6, 789, TimeSpan.Zero));

        var result = new SettingsStore(path, new PhysicalSettingsFileSystem(), clock).Load();

        Assert.NotNull(result.Warning);
        Assert.True(File.Exists(path + ".corrupt-20240203040506789"));
    }

    [Fact]
    public void Save_UsesFilesystemPortForAtomicPublication()
    {
        var fileSystem = new SpySettingsFileSystem();
        var path = Path.Combine(_directory, "settings.json");
        var store = new SettingsStore(path, fileSystem);

        store.Save(new AppSettings { Language = "ES" });

        Assert.Equal(1, fileSystem.AtomicWriteCalls);
        Assert.Equal("es", new SettingsStore(path).Load().Settings.Language);
    }

    [Fact]
    public void Save_WhenAtomicPublicationFails_PreservesPreviousSettings()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"Language\":\"en\"}");
        var fileSystem = new SpySettingsFileSystem { FailAtomicWrite = true };
        var store = new SettingsStore(path, fileSystem);

        Assert.Throws<IOException>(() => store.Save(new AppSettings { Language = "es" }));
        Assert.Equal("en", new SettingsStore(path).Load().Settings.Language);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
        public DateTime LocalNow => UtcNow.LocalDateTime;
    }

    private sealed class SpySettingsFileSystem : ISettingsFileSystem
    {
        private readonly PhysicalSettingsFileSystem _inner = new();
        public int AtomicWriteCalls { get; private set; }
        public bool FailAtomicWrite { get; init; }

        public bool FileExists(string path) => _inner.FileExists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public string ReadAllText(string path) => _inner.ReadAllText(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public void Move(string source, string destination, bool overwrite) =>
            _inner.Move(source, destination, overwrite);

        public void WriteAllTextAtomically(string path, string content)
        {
            AtomicWriteCalls++;
            if (FailAtomicWrite)
                throw new IOException("Simulated atomic publication failure.");
            _inner.WriteAllTextAtomically(path, content);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}

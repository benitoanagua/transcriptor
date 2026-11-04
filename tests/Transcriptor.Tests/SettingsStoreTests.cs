using Transcriptor.Desktop;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TranscriptorSettingsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SettingsStore_UsesLocalApplicationDataForDefaultPath()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Transcriptor", "settings.json");

        Assert.Equal(expected, new SettingsStore().SettingsPath);
    }

    [Fact]
    public void CreateDefault_UsesTheStandardUserProfileRecordingDirectory()
    {
        var original = Environment.GetEnvironmentVariable("TRANSCRIPTOR_DIR");
        try
        {
            Environment.SetEnvironmentVariable("TRANSCRIPTOR_DIR", Path.Combine(_directory, "custom-root"));

            var settings = AppSettings.CreateDefault();
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Transcriptor", "Grabaciones");

            Assert.Equal(expected, settings.RecordingsDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TRANSCRIPTOR_DIR", original);
        }
    }

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

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    [InlineData("HighContrast")]
    [InlineData("System")]
    public void Load_PreservesEverySupportedThemeMode(string themeMode)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"ThemeMode\":\"" + themeMode + "\"}");

        var result = new SettingsStore(path).Load();

        Assert.Null(result.Warning);
        Assert.Equal(themeMode, result.Settings.ThemeMode);
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

    [Fact]
    public void SaveAndLoad_RoundTripsHostileButValidPreferences()
    {
        var store = new SettingsStore(Path.Combine(_directory, "hostile-settings.json"));
        var payloads = new[]
        {
            new AppSettings { RecordingsDirectory = "  C:\\\\Calls\\\\日本語\\\\🎙  ", Language = " ES ", RetentionDays = int.MinValue },
            new AppSettings { WhisperExecutablePath = new string('x', 2048), Language = "PT-br", RetentionDays = int.MaxValue },
            new AppSettings { RecordingsDirectory = "relative/../recordings", Language = "\\t", RetentionDays = 1 },
            new AppSettings { MicrophoneDeviceId = "  {device}:\\r\\n\\\\0  ", OutputDeviceId = "speaker/../id", RetentionDays = 3650 }
        };

        foreach (var payload in payloads)
        {
            store.Save(payload);
            var loaded = store.Load();

            Assert.Null(loaded.Warning);
            Assert.Equal(payload.RecordingsDirectory?.Trim() ?? string.Empty, loaded.Settings.RecordingsDirectory);
            Assert.Equal(payload.WhisperExecutablePath?.Trim() ?? string.Empty, loaded.Settings.WhisperExecutablePath);
            Assert.Equal(payload.Language?.Trim().ToLowerInvariant() is { Length: > 0 } language ? language : "es", loaded.Settings.Language);
            Assert.InRange(loaded.Settings.RetentionDays, 0, 3650);
        }
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

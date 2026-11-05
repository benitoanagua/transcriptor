using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TranscriptorSettingsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void PlatformDefaultPaths_UsesLocalApplicationDataForSettings()
    {
        var paths = new PlatformDefaultPaths();

        Assert.Equal(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Transcriptor", "settings.json"), paths.SettingsPath);
    }

    [Fact]
    public void PlatformDefaultPaths_PutsRecordingsUnderTheUserProfile()
    {
        var paths = new PlatformDefaultPaths();

        Assert.Equal(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Transcriptor", "Grabaciones"), paths.RecordingsRoot);
    }

    [Fact]
    public void CreateDefault_UsesTheInjectedDefaultPaths()
    {
        var paths = new TestDefaultPaths();

        var settings = AppSettings.CreateDefault(paths);

        Assert.Equal(paths.RecordingsRoot, settings.RecordingsDirectory);
        Assert.Equal(paths.WhisperExecutablePath, settings.WhisperExecutablePath);
        Assert.Equal(paths.WhisperModelPath, settings.WhisperModelPath);
        Assert.Equal("es", settings.Language);
    }

    [Fact]
    public void SaveAndLoad_PreservesAllPreferences()
    {
        var expected = new AppSettings
        {
            RecordingsDirectory = "C:\\Calls",
            WhisperExecutablePath = "C:\\Whisper\\whisper-cli.exe",
            WhisperModelPath = "C:\\Models\\ggml-small.bin",
            Language = "EN",
            MicrophoneDeviceId = "mic-123",
            OutputDeviceId = "speaker-456",
            LibraryPanelVisible = false
        };

        CreateStore(Path.Combine(_directory, "settings.json")).Save(expected);
        var actual = CreateStore(Path.Combine(_directory, "settings.json")).Load();

        Assert.Null(actual.Warning);
        Assert.Equal(expected with { Language = "en" }, actual.Settings);
    }

    [Fact]
    public void Load_IgnoresObsoleteWorkspacePanelKeys()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"ShowLibraryPanel\":false,\"ShowDevicesPanel\":false,\"ShowSettingsPanel\":false,\"ShowLogPanel\":false}");

        var result = CreateStore(path).Load();

        Assert.Null(result.Warning);
        Assert.Equal("es", result.Settings.Language);
        CreateStore(path).Save(result.Settings);
        var savedJson = File.ReadAllText(path);
        Assert.DoesNotContain("ShowLibraryPanel", savedJson);
        Assert.DoesNotContain("ShowDevicesPanel", savedJson);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4000, 3650)]
    public void Load_NormalizesRetentionDays(int configuredDays, int expectedDays)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"RetentionDays\":" + configuredDays + "}");

        var result = CreateStore(path).Load();

        Assert.Null(result.Warning);
        Assert.Equal(expectedDays, result.Settings.RetentionDays);
    }

    [Fact]
    public void Load_IgnoresThemeModeFromOlderSettingsFiles()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"ThemeMode\":\"Light\",\"Language\":\"es\"}");

        var result = CreateStore(path).Load();

        Assert.Null(result.Warning);
        Assert.Equal("es", result.Settings.Language);
        CreateStore(path).Save(result.Settings);
        Assert.DoesNotContain("ThemeMode", File.ReadAllText(path));
    }

    [Fact]
    public void Load_WhenFileIsMissing_ReturnsDefaultsWithoutWarning()
    {
        var store = CreateStore(Path.Combine(_directory, "missing.json"));

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

        var result = CreateStore(path).Load();

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

        var result = new SettingsStore(path, new PhysicalSettingsFileSystem(), clock, new TestDefaultPaths()).Load();

        Assert.NotNull(result.Warning);
        Assert.True(File.Exists(path + ".corrupt-20240203040506789"));
    }

    [Fact]
    public void Save_UsesFilesystemPortForAtomicPublication()
    {
        var fileSystem = new SpySettingsFileSystem();
        var path = Path.Combine(_directory, "settings.json");
        var store = new SettingsStore(path, fileSystem, SystemClock.Instance, new TestDefaultPaths());

        store.Save(new AppSettings { Language = "ES" });

        Assert.Equal(1, fileSystem.AtomicWriteCalls);
        Assert.Equal("es", CreateStore(path).Load().Settings.Language);
    }

    [Fact]
    public void Save_WhenAtomicPublicationFails_PreservesPreviousSettings()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"Language\":\"en\"}");
        var fileSystem = new SpySettingsFileSystem { FailAtomicWrite = true };
        var store = new SettingsStore(path, fileSystem, SystemClock.Instance, new TestDefaultPaths());

        Assert.Throws<IOException>(() => store.Save(new AppSettings { Language = "es" }));
        Assert.Equal("en", CreateStore(path).Load().Settings.Language);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsHostileButValidPreferences()
    {
        var store = CreateStore(Path.Combine(_directory, "hostile-settings.json"));
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

    private static SettingsStore CreateStore(string path) =>
        new(path, new PhysicalSettingsFileSystem(), SystemClock.Instance, new TestDefaultPaths());

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

using Transcriptor.UI;
using Transcriptor.UI.ViewModels;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ChangesUpdateDirtyStateAndLanguageSelection()
    {
        var paths = new TestPaths();
        var settings = AppSettings.CreateDefault(paths);
        var viewModel = new SettingsViewModel(
            settings, CreateServices(paths), new TestHost(), new TestLogSink(),
            _ => { }, () => (null, null), new TranscriptionUiState(), () => false);

        Assert.False(viewModel.IsDirty);

        viewModel.LanguageSelectionIndex = 5;

        Assert.True(viewModel.IsDirty);
        Assert.Equal("auto", viewModel.Snapshot().Language);
        Assert.False(viewModel.IsCustomLanguage);

        viewModel.LanguageSelectionIndex = 6;
        viewModel.WhisperLanguageText = "it";

        Assert.True(viewModel.IsCustomLanguage);
        Assert.Equal("it", viewModel.Snapshot().Language);
    }

    [Fact]
    public void PathValidationRefreshesWhenPathChanges()
    {
        var paths = new TestPaths();
        var viewModel = new SettingsViewModel(
            AppSettings.CreateDefault(paths), CreateServices(paths), new TestHost(),
            new TestLogSink(), _ => { }, () => (null, null), new TranscriptionUiState(), () => false);
        var writableDirectory = Path.Combine(Path.GetTempPath(), "TranscriptorSettingsTests", Guid.NewGuid().ToString("N"));

        try
        {
            Assert.Contains("no existe", viewModel.RecordingsDirectoryStatus);

            Directory.CreateDirectory(writableDirectory);
            viewModel.RecordingsDirectoryText = writableDirectory;

            Assert.Contains("escribible", viewModel.RecordingsDirectoryStatus);
            Assert.Empty(Directory.GetFiles(writableDirectory));
        }
        finally
        {
            if (Directory.Exists(writableDirectory))
                Directory.Delete(writableDirectory, recursive: true);
        }
    }

    [Fact]
    public void WhisperTestBecomesEnabledOnlyWhenBothPathsAreValid()
    {
        var paths = new TestPaths();
        var viewModel = new SettingsViewModel(
            AppSettings.CreateDefault(paths), CreateServices(paths), new TestHost(),
            new TestLogSink(), _ => { }, () => (null, null), new TranscriptionUiState(), () => false);
        var directory = Path.Combine(Path.GetTempPath(), "TranscriptorSettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            Assert.False(viewModel.TestWhisperCommand.CanExecute(null));

            var executable = Path.Combine(directory, OperatingSystem.IsWindows() ? "whisper.exe" : "whisper");
            var model = Path.Combine(directory, "model.bin");
            File.WriteAllBytes(executable, []);
            File.WriteAllBytes(model, []);
            viewModel.WhisperExecutableText = executable;
            Assert.False(viewModel.TestWhisperCommand.CanExecute(null));

            viewModel.WhisperModelText = model;

            Assert.True(viewModel.CanTestWhisper);
            Assert.True(viewModel.TestWhisperCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRetentionConfirmsCandidateCountBeforeDeletingAndSaving()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRetentionTests", Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "2026-01-01", "old-call");
        Directory.CreateDirectory(session);
        var track = Path.Combine(session, "yo.wav");
        await File.WriteAllBytesAsync(track, [1]);
        File.SetLastWriteTimeUtc(track, DateTime.UtcNow.AddDays(-10));
        var paths = new TestPaths(root);
        var host = new TestHost();
        AppSettings? saved = null;
        var viewModel = new SettingsViewModel(
            AppSettings.CreateDefault(paths) with { RetentionDays = 1 },
            CreateServices(paths), host, new TestLogSink(),
            settings => saved = settings, () => (null, null),
            new TranscriptionUiState(), () => false)
        {
            RetentionSettingsText = "1"
        };

        try
        {
            Assert.True(viewModel.ApplyRetentionCommand.CanExecute(null));

            await viewModel.ApplyRetentionCommand.ExecuteAsync(null);

            Assert.Contains("Se eliminarán 1 sesión(es)", host.LastConfirmationMessage);
            Assert.False(Directory.Exists(session));
            Assert.Equal(1, saved?.RetentionDays);
            Assert.Contains("se eliminaron 1 de 1", viewModel.RetentionStatusText);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRetentionCancellationLeavesSessionAndSettingsUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRetentionTests", Guid.NewGuid().ToString("N"));
        var session = Path.Combine(root, "2026-01-01", "old-call");
        Directory.CreateDirectory(session);
        var track = Path.Combine(session, "yo.wav");
        await File.WriteAllBytesAsync(track, [1]);
        File.SetLastWriteTimeUtc(track, DateTime.UtcNow.AddDays(-10));
        var paths = new TestPaths(root);
        var host = new TestHost { ConfirmationResult = false };
        var saved = false;
        var viewModel = new SettingsViewModel(
            AppSettings.CreateDefault(paths) with { RetentionDays = 1 },
            CreateServices(paths), host, new TestLogSink(),
            _ => saved = true, () => (null, null),
            new TranscriptionUiState(), () => false)
        {
            RetentionSettingsText = "1"
        };

        try
        {
            await viewModel.ApplyRetentionCommand.ExecuteAsync(null);

            Assert.True(Directory.Exists(session));
            Assert.False(saved);
            Assert.Equal("Retención cancelada.", viewModel.RetentionStatusText);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RetentionCommandRespectsRecordingAndTranscriptionLocks()
    {
        var root = Path.Combine(Path.GetTempPath(), "TranscriptorRetentionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var paths = new TestPaths(root);
        var state = new TranscriptionUiState();
        var recordingActive = false;
        var viewModel = new SettingsViewModel(
            AppSettings.CreateDefault(paths), CreateServices(paths), new TestHost(),
            new TestLogSink(), _ => { }, () => (null, null),
            state, () => recordingActive)
        {
            RetentionSettingsText = "7"
        };

        try
        {
            Assert.True(viewModel.ApplyRetentionCommand.CanExecute(null));

            recordingActive = true;
            viewModel.NotifyRecordingStateChanged();
            Assert.False(viewModel.ApplyRetentionCommand.CanExecute(null));

            recordingActive = false;
            viewModel.NotifyRecordingStateChanged();
            Assert.True(state.TryBegin());
            Assert.False(viewModel.ApplyRetentionCommand.CanExecute(null));
            state.Complete();
            Assert.True(viewModel.ApplyRetentionCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static UiServices CreateServices(IDefaultPaths paths) => new(
        null!, null!, null!, new PassThroughSessionSecurity(), new ReversibleSessionMigration(),
        null!, SystemClock.Instance, paths, null!);

    private sealed class TestPaths(string? root = null) : IDefaultPaths
    {
        public string SettingsPath => Path.Combine(Path.GetTempPath(), "settings-test.json");
        public string RecordingsRoot => root ?? Path.Combine(Path.GetTempPath(), "missing-recordings-root");
        public string WhisperExecutablePath => "whisper";
        public string WhisperModelPath => "model.bin";
        public string? WhisperExecutablePickerPattern => null;
    }

    private sealed class TestHost : IUiHost
    {
        public bool ConfirmationResult { get; init; } = true;
        public string LastConfirmationMessage { get; private set; } = string.Empty;
        public Task<bool> ConfirmAsync(string title, string message)
        {
            LastConfirmationMessage = message;
            return Task.FromResult(ConfirmationResult);
        }
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickFileAsync(string title, string? pattern) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string pattern) => Task.FromResult<string?>(null);
        public Task OpenFolderAsync(string path) => Task.CompletedTask;
        public Task<bool> CopyToClipboardAsync(string text) => Task.FromResult(true);
    }

    private sealed class TestLogSink : ILogSink
    {
        public void Log(string message) { }
    }
}

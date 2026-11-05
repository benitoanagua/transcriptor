using Transcriptor.UI;
using Transcriptor.UI.ViewModels;
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
            _ => { }, () => (null, null));

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
            new TestLogSink(), _ => { }, () => (null, null));

        Assert.Contains("no existe", viewModel.RecordingsDirectoryStatus);

        viewModel.RecordingsDirectoryText = Path.GetTempPath();

        Assert.Contains("existente", viewModel.RecordingsDirectoryStatus);
    }

    private static UiServices CreateServices(IDefaultPaths paths) => new(
        null!, null!, null!, null!, null!, null!, SystemClock.Instance, paths, null!, _ => { });

    private sealed class TestPaths : IDefaultPaths
    {
        public string SettingsPath => Path.Combine(Path.GetTempPath(), "settings-test.json");
        public string RecordingsRoot => Path.Combine(Path.GetTempPath(), "missing-recordings-root");
        public string WhisperExecutablePath => "whisper";
        public string WhisperModelPath => "model.bin";
        public string? WhisperExecutablePickerPattern => null;
    }

    private sealed class TestHost : IUiHost
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
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

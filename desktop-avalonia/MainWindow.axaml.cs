using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using NAudio.Wave;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Transcriptor.Rec;
using Transcriptor.Transcription;

namespace Transcriptor.Desktop;

internal sealed record AvaloniaRecordingEntry(string Title, string DirectoryPath, string MicrophonePath, string ClientPath, bool HasTranscript, bool IsIncomplete)
{
    public bool HasMicrophone => File.Exists(MicrophonePath);
    public bool HasClient => File.Exists(ClientPath);
    public string Summary => $"{(HasMicrophone ? "MIC ✓" : "MIC —")} · {(HasClient ? "SISTEMA ✓" : "SISTEMA —")}" + (HasTranscript ? " · TRANSCRIPCIÓN" : "") + (IsIncomplete ? " · INCOMPLETA" : "");
}

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ISessionStore _sessionStore = new LocalSessionStore();
    private readonly IClock _clock = SystemClock.Instance;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TranscriptionUiState _transcriptionUiState = new();
    private readonly List<AvaloniaRecordingEntry> _allRecordings = new();
    private WaveOutEvent? _playback;
    private AudioFileReader? _playbackReader;
    private string? _playbackTemporaryPath;
    private TranscriptionSessionLease? _playbackLease;
    private string? _playbackTrackName;
    private bool _updatingPlaybackTimeline;
    private AppSettings _settings;
    private RecordingSession? _recording;
    private CancellationTokenSource? _transcriptionCancellation;
    private MiniModeWindow? _miniModeWindow;
    private string _miniModeTranscriptionStatus = "Preparando transcripción…";
    private string _activeSessionTitle = string.Empty;
    internal bool MiniModeAlwaysOnTop => _settings.MiniModeAlwaysOnTop;
    internal double? MiniModeLeft => _settings.MiniModeLeft;
    internal double? MiniModeTop => _settings.MiniModeTop;
    internal double? MiniModeWidth => _settings.MiniModeWidth;
    internal double? MiniModeHeight => _settings.MiniModeHeight;
    internal bool IsMiniModeTranscriptionCancellationRequested => _transcriptionCancellation?.IsCancellationRequested == true;

    public MainWindow()
    {
        InitializeComponent();
        var loaded = _settingsStore.Load();
        _settings = loaded.Settings;
        this.FindControl<TextBox>("RecordingsPathBox")!.Text = _settings.RecordingsDirectory;
        this.FindControl<TextBox>("WhisperExecutableBox")!.Text = _settings.WhisperExecutablePath;
        this.FindControl<TextBox>("WhisperModelBox")!.Text = _settings.WhisperModelPath;
        this.FindControl<TextBox>("WhisperLanguageBox")!.Text = _settings.Language;
        this.FindControl<TextBox>("RetentionDaysBox")!.Text = _settings.RetentionDays.ToString();
        this.FindControl<TextBox>("RetentionSettingsBox")!.Text = _settings.RetentionDays.ToString();
        this.FindControl<CheckBox>("MiniModeAutoShowCheckBox")!.IsChecked = _settings.MiniModeAutoShow;
        this.FindControl<ComboBox>("ThemeModeBox")!.SelectedIndex = _settings.ThemeMode switch { "Light" => 1, "Dark" => 2, _ => 0 };
        if (!string.IsNullOrWhiteSpace(loaded.Warning)) Log(loaded.Warning);
        Log("Configuración cargada desde " + _settingsStore.SettingsPath);
        _timer.Tick += (_, _) =>
        {
            RefreshRecordingStatus();
            UpdatePlaybackTimeline();
        };
        Opened += (_, _) =>
        {
            RefreshAudioDevices();
            RefreshLibrary();
        };
    }

    private void InitializeComponent()
    {
        NameScope.SetNameScope(this, new NameScope());
        AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseRecordingsPath_Click(object? sender, RoutedEventArgs e)
    {
        var path = await OpenPickerPathAsync(folder: true, "Carpeta de grabaciones");
        if (path is not null) this.FindControl<TextBox>("RecordingsPathBox")!.Text = path;
    }

    private async void BrowseWhisperExecutable_Click(object? sender, RoutedEventArgs e)
    {
        var path = await OpenPickerPathAsync(folder: false, "Seleccionar Whisper CLI", "*.exe");
        if (path is not null) this.FindControl<TextBox>("WhisperExecutableBox")!.Text = path;
    }

    private async void BrowseWhisperModel_Click(object? sender, RoutedEventArgs e)
    {
        var path = await OpenPickerPathAsync(folder: false, "Seleccionar modelo Whisper", "*.bin");
        if (path is not null) this.FindControl<TextBox>("WhisperModelBox")!.Text = path;
    }

    private async Task<string?> OpenPickerPathAsync(bool folder, string title, string? pattern = null)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return null;

        IStorageItem? selected;
        if (folder)
        {
            var items = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
            selected = items.FirstOrDefault();
        }
        else
        {
            var items = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = pattern is null
                    ? null
                    : [new FilePickerFileType(title) { Patterns = [pattern] }]
            });
            selected = items.FirstOrDefault();
        }

        return selected?.TryGetLocalPath();
    }

    private void RefreshDevices_Click(object? sender, RoutedEventArgs e) => RefreshAudioDevices();
    private void SaveSettings_Click(object? sender, RoutedEventArgs e) => SaveSettings();
    private void RefreshLibrary_Click(object? sender, RoutedEventArgs e) => RefreshLibrary();
    private void LibrarySearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyLibraryFilters();
    }

    private void LibraryFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyLibraryFilters();
    }
    private void CancelTranscription_Click(object? sender, RoutedEventArgs e) => _transcriptionCancellation?.Cancel();
    private void ShowMiniMode_Click(object? sender, RoutedEventArgs e) => ShowMiniMode(force: true);
    private void ShowMiniMode(bool force = false)
    {
        if (!force && !_settings.MiniModeAutoShow) return;
        if (_miniModeWindow is null) _miniModeWindow = new MiniModeWindow(this);
        if (!_miniModeWindow.IsVisible) { _miniModeWindow.EnsureVisiblePosition(); _miniModeWindow.Show(); }
        _miniModeWindow.Activate();
        Hide();
    }
    internal AvaloniaMiniModeSnapshot? GetMiniModeSnapshot()
    {
        if (_recording is { } recording)
            return new AvaloniaMiniModeSnapshot(true, false, false, recording.Elapsed, recording.MicrophoneLevel,
                recording.SystemLevel, recording.IsPaused, recording.IsPaused ? "PAUSADO" : "GRABANDO",
                _activeSessionTitle, false, false, false, _playback?.PlaybackState == PlaybackState.Playing,
                _playbackReader is not null, _playbackReader?.CurrentTime ?? TimeSpan.Zero,
                _playbackReader?.TotalTime ?? TimeSpan.Zero, _playbackTrackName ?? string.Empty);
        if (_transcriptionUiState.IsTranscribing)
            return new AvaloniaMiniModeSnapshot(false, true, false, TimeSpan.Zero, 0, 0, false,
                _miniModeTranscriptionStatus, string.Empty, false, false, false, false, false,
                TimeSpan.Zero, TimeSpan.Zero, string.Empty);
        var selected = this.FindControl<ListBox>("LibraryList")!.SelectedItem as AvaloniaRecordingEntry;
        return new AvaloniaMiniModeSnapshot(false, false, false, TimeSpan.Zero, 0, 0, false, "LISTO",
            selected?.Title ?? "Selecciona una sesión en la ventana principal", true,
            selected?.HasMicrophone == true, selected?.HasClient == true,
            _playback?.PlaybackState == PlaybackState.Playing, _playbackReader is not null,
            _playbackReader?.CurrentTime ?? TimeSpan.Zero, _playbackReader?.TotalTime ?? TimeSpan.Zero,
            _playbackTrackName ?? string.Empty);
    }
    internal void PlayMicrophoneFromMiniMode()
    {
        if (_recording is null && !_transcriptionUiState.IsTranscribing && this.FindControl<ListBox>("LibraryList")!.SelectedItem is AvaloniaRecordingEntry entry && entry.HasMicrophone)
            PlayTrack(entry.MicrophonePath, "micrófono");
    }
    internal void PlayClientFromMiniMode()
    {
        if (_recording is null && !_transcriptionUiState.IsTranscribing && this.FindControl<ListBox>("LibraryList")!.SelectedItem is AvaloniaRecordingEntry entry && entry.HasClient)
            PlayTrack(entry.ClientPath, "audio del sistema");
    }
    internal void SeekPlaybackFromMiniMode(TimeSpan position)
    {
        if (_playbackReader is null) return;
        _playbackReader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, _playbackReader.TotalTime.TotalSeconds));
        UpdatePlaybackTimeline();
    }
    internal void StopPlaybackFromMiniMode() => StopPlayback();
    internal void PauseRecordingFromMiniMode() { if (_recording is not null && !_recording.IsPaused) _recording.Pause(); }
    internal void ResumeRecordingFromMiniMode() { if (_recording is not null && _recording.IsPaused) _recording.Resume(); }
    internal async Task StopRecordingFromMiniModeAsync()
    {
        var recording = _recording;
        if (recording is null) return;
        try { await Task.Run(recording.Stop); Log("Grabación finalizada desde Mini-Mode."); }
        finally { recording.Dispose(); _recording = null; _activeSessionTitle = string.Empty; ResetRecordingState(); RefreshLibrary(); }
    }
    internal void CancelTranscriptionFromMiniMode() => _transcriptionCancellation?.Cancel();
    internal Task<bool> ConfirmMiniModeStopAsync(Window owner) => ConfirmAsync("Detener grabación", "Se detendrán de forma coordinada las pistas MIC y SISTEMA. ¿Deseas finalizar la grabación?", owner);
    internal void ReportMiniModeError(string message) => Log(message);
    internal void UpdateMiniModePreferences(bool alwaysOnTop, double left, double top, double width, double height)
    {
        _settings = _settings with { MiniModeAlwaysOnTop = alwaysOnTop, MiniModeLeft = left, MiniModeTop = top, MiniModeWidth = width, MiniModeHeight = height };
        try { _settingsStore.Save(_settings); } catch (Exception ex) { Log("No se pudieron guardar las preferencias de Mini-Mode: " + ex.Message); }
    }
    internal void ExpandFromMiniMode(MiniModeWindow window)
    {
        if (!ReferenceEquals(_miniModeWindow, window)) return;
        _miniModeWindow = null; window.CloseFromMain(); Show(); WindowState = WindowState.Normal; Activate();
    }
    internal void CloseMiniModeFromWindow(MiniModeWindow window)
    {
        if (!ReferenceEquals(_miniModeWindow, window)) return;
        _miniModeWindow = null; window.CloseFromMain(); Show(); WindowState = WindowState.Normal; Activate();
    }
    internal void MiniModeWindowClosed(MiniModeWindow window)
    {
        if (!ReferenceEquals(_miniModeWindow, window)) return;
        _miniModeWindow = null;
        if (!IsVisible) { Show(); WindowState = WindowState.Normal; Activate(); }
    }

    private async Task<bool> ConfirmAsync(string title, string message, Window? owner = null)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 430,
            SizeToContent = SizeToContent.Height,
            MinHeight = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button { Content = "Cancelar", MinWidth = 90 },
                            new Button { Content = "Confirmar", MinWidth = 90 }
                        }
                    }
                }
            }
        };
        var actions = (StackPanel)((StackPanel)dialog.Content!).Children[1];
        ((Button)actions.Children[0]).Click += (_, _) => dialog.Close(false);
        ((Button)actions.Children[1]).Click += (_, _) => dialog.Close(true);
        return await dialog.ShowDialog<bool>(owner ?? this);
    }

    private async void MigrateSession_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("LibraryList")!.SelectedItem is not AvaloniaRecordingEntry entry || !_transcriptionUiState.TryBegin()) return;
        if (!await ConfirmAsync("Migrar sesión a cifrado",
            "Se crearán y verificarán copias cifradas. Los originales en claro solo se eliminarán después de verificar todos los archivos. El borrado no es forense. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        if (_playbackReader is not null) StopPlayback();
        SetLibraryOperationEnabled(false);
        this.FindControl<TextBlock>("LifecycleProgressText")!.Text = "Migrando y verificando archivos…";
        try
        {
            var result = await Task.Run(() => _sessionStore.MigrateSession(entry.DirectoryPath));
            this.FindControl<TextBlock>("LifecycleProgressText")!.Text = $"Migración completada: {result.AudioTracksMigrated} pistas, {result.TranscriptsMigrated} transcripciones, {result.AlreadyEncrypted} ya cifrados.";
            Log(this.FindControl<TextBlock>("LifecycleProgressText")!.Text);
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("LifecycleProgressText")!.Text = "Migración incompleta; revisa el registro. Los originales no verificados se conservaron.";
            Log("Error de migración: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            SetLibraryOperationEnabled(true);
            RefreshLibrary();
            SelectLibraryEntry(entry.DirectoryPath);
        }
    }

    private async void DeleteSession_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("LibraryList")!.SelectedItem is not AvaloniaRecordingEntry entry || !_transcriptionUiState.TryBegin()) return;
        if (!await ConfirmAsync("Eliminar sesión",
            "Se eliminarán permanentemente los audios y transcripciones de esta sesión. Esta acción no se puede deshacer. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        if (_playbackReader is not null) StopPlayback();
        SetLibraryOperationEnabled(false);
        try
        {
            var root = this.FindControl<TextBox>("RecordingsPathBox")!.Text?.Trim() ?? string.Empty;
            await Task.Run(() => _sessionStore.DeleteSession(root, entry.DirectoryPath));
            this.FindControl<TextBlock>("LifecycleProgressText")!.Text = "Sesión eliminada.";
            Log("Sesión eliminada: " + entry.Title);
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("LifecycleProgressText")!.Text = "No se pudo eliminar la sesión.";
            Log("Error al eliminar sesión: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            SetLibraryOperationEnabled(true);
            RefreshLibrary();
        }
    }

    private async void ApplyRetention_Click(object? sender, RoutedEventArgs e)
    {
        if (!_transcriptionUiState.TryBegin()) return;
        var days = int.TryParse(this.FindControl<TextBox>("RetentionDaysBox")!.Text, out var parsed) ? Math.Clamp(parsed, 0, 3650) : 0;
        if (days <= 0)
        {
            _transcriptionUiState.Complete();
            this.FindControl<TextBlock>("LifecycleProgressText")!.Text = "Retención desactivada. Define entre 1 y 3650 días.";
            return;
        }
        if (!await ConfirmAsync("Aplicar retención",
            $"Se eliminarán las sesiones anteriores a {days} días. La operación no se puede deshacer. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        if (_playbackReader is not null) StopPlayback();
        SetLibraryOperationEnabled(false);
        try
        {
            _settings = _settings with { RetentionDays = days };
            this.FindControl<TextBox>("RetentionSettingsBox")!.Text = days.ToString();
            _settingsStore.Save(_settings);
            var root = this.FindControl<TextBox>("RecordingsPathBox")!.Text?.Trim() ?? string.Empty;
            var deleted = await Task.Run(() => _sessionStore.ApplyRetention(root, days, _clock.UtcNow.UtcDateTime));
            this.FindControl<TextBlock>("LifecycleProgressText")!.Text = $"Retención aplicada: {deleted.Count} sesión(es) eliminadas.";
            Log(this.FindControl<TextBlock>("LifecycleProgressText")!.Text);
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("LifecycleProgressText")!.Text = "No se pudo completar la retención.";
            Log("Error de retención: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            SetLibraryOperationEnabled(true);
            RefreshLibrary();
        }
    }

    private void SetLibraryOperationEnabled(bool enabled)
    {
        var idle = enabled && !_transcriptionUiState.IsTranscribing;
        this.FindControl<Button>("MigrateSessionButton")!.IsEnabled = idle;
        this.FindControl<Button>("DeleteSessionButton")!.IsEnabled = idle;
        this.FindControl<Button>("ApplyRetentionButton")!.IsEnabled = idle;
        this.FindControl<Button>("TranscribeButton")!.IsEnabled = idle && _recording is null;
        this.FindControl<Button>("StartButton")!.IsEnabled = idle && _recording is null;
        this.FindControl<ListBox>("LibraryList")!.IsEnabled = idle;
        this.FindControl<Button>("RefreshLibraryButton")!.IsEnabled = idle;
        if (!enabled)
        {
            this.FindControl<Button>("CancelTranscriptionButton")!.IsEnabled = false;
            StopPlayback();
        }
    }

    private void SelectLibraryEntry(string path)
    {
        this.FindControl<ListBox>("LibraryList")!.SelectedItem = (this.FindControl<ListBox>("LibraryList")!.ItemsSource as IEnumerable<AvaloniaRecordingEntry>)?
            .FirstOrDefault(item => string.Equals(item.DirectoryPath, path, StringComparison.OrdinalIgnoreCase));
    }

    private async void TranscribePending_Click(object? sender, RoutedEventArgs e)
    {
        if (_recording is not null)
        {
            Log("Transcripción bloqueada: primero finaliza la grabación activa.");
            return;
        }
        if (_playbackReader is not null) StopPlayback();
        if (!_transcriptionUiState.TryBegin()) return;

        var root = this.FindControl<TextBox>("RecordingsPathBox")!.Text?.Trim() ?? string.Empty;
        var executable = this.FindControl<TextBox>("WhisperExecutableBox")!.Text?.Trim() ?? string.Empty;
        var model = this.FindControl<TextBox>("WhisperModelBox")!.Text?.Trim() ?? string.Empty;
        var language = string.IsNullOrWhiteSpace(this.FindControl<TextBox>("WhisperLanguageBox")!.Text) ? "es" : this.FindControl<TextBox>("WhisperLanguageBox")!.Text.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)
            || string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(model))
        {
            _transcriptionUiState.Complete();
            this.FindControl<TextBlock>("TranscriptionProgressText")!.Text = "Configura una carpeta existente, whisper-cli y el modelo en AJUSTES.";
            Log("Transcripción no iniciada: faltan rutas válidas de grabaciones, Whisper o modelo.");
            return;
        }

        _transcriptionCancellation = new CancellationTokenSource();
        this.FindControl<Button>("TranscribeButton")!.IsEnabled = false;
        this.FindControl<Button>("MigrateSessionButton")!.IsEnabled = false;
        this.FindControl<Button>("DeleteSessionButton")!.IsEnabled = false;
        this.FindControl<Button>("ApplyRetentionButton")!.IsEnabled = false;
        this.FindControl<ListBox>("LibraryList")!.IsEnabled = false;
        this.FindControl<Button>("RefreshLibraryButton")!.IsEnabled = false;
        this.FindControl<Button>("CancelTranscriptionButton")!.IsEnabled = true;
        this.FindControl<Button>("StartButton")!.IsEnabled = false;
        this.FindControl<TextBlock>("TranscriptionProgressText")!.Text = "Preparando transcripción…";
        _miniModeTranscriptionStatus = "Preparando transcripción…";
        ShowMiniMode(force: true);
        Log("Iniciando transcripción local de sesiones pendientes.");
        var progress = new Progress<string>(message =>
        {
            this.FindControl<TextBlock>("TranscriptionProgressText")!.Text = message;
            _miniModeTranscriptionStatus = message;
            Log(message);
        });
        try
        {
            var engine = new TranscriptionEngine(executable, model, language);
            var processed = await engine.ProcessPendingCallsAsync(root, progress, _transcriptionCancellation.Token);
            this.FindControl<TextBlock>("TranscriptionProgressText")!.Text = $"Proceso finalizado. Sesiones transcritas: {processed}.";
            Log($"Transcripción finalizada. Sesiones procesadas: {processed}.");
        }
        catch (OperationCanceledException)
        {
            this.FindControl<TextBlock>("TranscriptionProgressText")!.Text = "Transcripción cancelada. Los originales se conservaron.";
            Log("Transcripción cancelada por el usuario.");
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("TranscriptionProgressText")!.Text = "La transcripción terminó con errores. Consulta el registro.";
            Log("Error de transcripción: " + ex.Message);
        }
        finally
        {
            _transcriptionCancellation.Dispose();
            _transcriptionCancellation = null;
            _transcriptionUiState.Complete();
            this.FindControl<Button>("TranscribeButton")!.IsEnabled = true;
            this.FindControl<Button>("MigrateSessionButton")!.IsEnabled = true;
            this.FindControl<Button>("DeleteSessionButton")!.IsEnabled = true;
            this.FindControl<Button>("ApplyRetentionButton")!.IsEnabled = true;
            this.FindControl<ListBox>("LibraryList")!.IsEnabled = true;
            this.FindControl<Button>("RefreshLibraryButton")!.IsEnabled = true;
            this.FindControl<Button>("CancelTranscriptionButton")!.IsEnabled = false;
            this.FindControl<Button>("StartButton")!.IsEnabled = _recording is null;
            RefreshLibrary();
        }
    }

    private void RefreshAudioDevices()
    {
        try
        {
            var microphones = RecordingSession.GetCaptureDevices();
            var outputs = RecordingSession.GetRenderDevices();
            this.FindControl<ComboBox>("MicrophoneDeviceBox")!.ItemsSource = microphones;
            this.FindControl<ComboBox>("OutputDeviceBox")!.ItemsSource = outputs;
            var micId = _settings.MicrophoneDeviceId ?? RecordingSession.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Capture);
            var outputId = _settings.OutputDeviceId ?? RecordingSession.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Render);
            this.FindControl<ComboBox>("MicrophoneDeviceBox")!.SelectedItem = microphones.FirstOrDefault(x => x.Id == micId) ?? microphones.FirstOrDefault();
            this.FindControl<ComboBox>("OutputDeviceBox")!.SelectedItem = outputs.FirstOrDefault(x => x.Id == outputId) ?? outputs.FirstOrDefault();
            Log($"Dispositivos detectados: {microphones.Count} entradas y {outputs.Count} salidas.");
        }
        catch (Exception ex) { Log("No se pudieron enumerar los dispositivos: " + ex.Message); }
    }

    private void SaveSettings()
    {
        _settings = _settings with
        {
            RecordingsDirectory = this.FindControl<TextBox>("RecordingsPathBox")!.Text?.Trim() ?? string.Empty,
            WhisperExecutablePath = this.FindControl<TextBox>("WhisperExecutableBox")!.Text?.Trim() ?? string.Empty,
            WhisperModelPath = this.FindControl<TextBox>("WhisperModelBox")!.Text?.Trim() ?? string.Empty,
            Language = string.IsNullOrWhiteSpace(this.FindControl<TextBox>("WhisperLanguageBox")!.Text) ? "es" : this.FindControl<TextBox>("WhisperLanguageBox")!.Text.Trim(),
            RetentionDays = int.TryParse(this.FindControl<TextBox>("RetentionSettingsBox")!.Text, out var retentionDays) ? Math.Clamp(retentionDays, 0, 3650) : 0,
        MiniModeAutoShow = this.FindControl<CheckBox>("MiniModeAutoShowCheckBox")!.IsChecked == true,
            ThemeMode = this.FindControl<ComboBox>("ThemeModeBox")!.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" },
            MicrophoneDeviceId = (this.FindControl<ComboBox>("MicrophoneDeviceBox")!.SelectedItem as AudioDeviceOption)?.Id,
            OutputDeviceId = (this.FindControl<ComboBox>("OutputDeviceBox")!.SelectedItem as AudioDeviceOption)?.Id
        };
        _settingsStore.Save(_settings);
        (Application.Current as App)?.ApplyTheme(_settings.ThemeMode);
        Log("Configuración guardada.");
        RefreshLibrary();
    }

    private void RefreshLibrary()
    {
        if (_transcriptionUiState.IsTranscribing) return;
        try
        {
            var root = this.FindControl<TextBox>("RecordingsPathBox")!.Text?.Trim() ?? string.Empty;
            _allRecordings.Clear();
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                foreach (var directory in _sessionStore.GetSessions(root))
                {
                    var micEncrypted = Path.Combine(directory, "yo.trn");
                    var clientEncrypted = Path.Combine(directory, "cliente.trn");
                    var micPlain = Path.Combine(directory, "yo.wav");
                    var clientPlain = Path.Combine(directory, "cliente.wav");
                    var mic = File.Exists(micEncrypted) ? micEncrypted : micPlain;
                    var client = File.Exists(clientEncrypted) ? clientEncrypted : clientPlain;
                    var parent = Directory.GetParent(directory)?.Name;
                    var title = string.IsNullOrWhiteSpace(parent) ? Path.GetFileName(directory) : $"{parent} · {Path.GetFileName(directory)}";
                    var hasTranscript = new[] { "transcripcion.srt.trn", "yo.srt.trn", "cliente.srt.trn", "transcripcion.srt", "yo.srt", "cliente.srt" }
                        .Any(name => File.Exists(Path.Combine(directory, name)));
                    _allRecordings.Add(new AvaloniaRecordingEntry(title, directory, mic, client, hasTranscript, RecordingIntegrity.IsIncomplete(directory)));
                }
            }
            _allRecordings.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(b.DirectoryPath, a.DirectoryPath));
            ApplyLibraryFilters();
            Log($"Biblioteca actualizada: {_allRecordings.Count} sesión(es) reconocida(s).");
        }
        catch (Exception ex)
        {
            this.FindControl<TextBlock>("LibraryCountText")!.Text = "No se pudo cargar la biblioteca.";
            this.FindControl<TextBlock>("LibraryDetailsTitle")!.Text = "Error de biblioteca";
            this.FindControl<TextBlock>("LibraryDetailsText")!.Text = ex.Message;
            Log("No se pudo actualizar la biblioteca: " + ex.Message);
        }
    }

    private void ApplyLibraryFilters()
    {
        if (this.FindControl<ListBox>("LibraryList")! is null) return;
        var query = this.FindControl<TextBox>("LibrarySearchBox")!.Text?.Trim() ?? string.Empty;
        var filter = this.FindControl<ComboBox>("LibraryFilterBox")!.SelectedIndex;
        var selectedPath = (this.FindControl<ListBox>("LibraryList")!.SelectedItem as AvaloniaRecordingEntry)?.DirectoryPath;
        var filtered = _allRecordings.Where(entry =>
        {
            if (query.Length > 0 && !entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !entry.DirectoryPath.Contains(query, StringComparison.OrdinalIgnoreCase)) return false;
            return filter switch
            {
                1 => entry.HasMicrophone && entry.HasClient,
                2 => entry.HasMicrophone && !entry.HasClient,
                3 => !entry.HasMicrophone && entry.HasClient,
                4 => entry.HasTranscript,
                5 => entry.IsIncomplete,
                _ => true
            };
        }).ToList();
        this.FindControl<ListBox>("LibraryList")!.ItemsSource = filtered;
        this.FindControl<ListBox>("LibraryList")!.SelectedItem = selectedPath is null ? null : filtered.FirstOrDefault(entry =>
            string.Equals(entry.DirectoryPath, selectedPath, StringComparison.OrdinalIgnoreCase));
        this.FindControl<TextBlock>("LibraryCountText")!.Text = $"{filtered.Count} de {_allRecordings.Count} sesión(es)";
    }

    private void LibraryList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_playbackReader is not null) StopPlayback();
        if (this.FindControl<ListBox>("LibraryList")!.SelectedItem is not AvaloniaRecordingEntry entry)
        {
            this.FindControl<TextBlock>("LibraryDetailsTitle")!.Text = "Selecciona una sesión";
            this.FindControl<TextBlock>("LibraryDetailsText")!.Text = "La biblioteca muestra sesiones locales reconocidas.";
            return;
        }
        this.FindControl<TextBlock>("LibraryDetailsTitle")!.Text = entry.Title;
        this.FindControl<TextBlock>("LibraryDetailsText")!.Text = $"{entry.Summary}{Environment.NewLine}{entry.DirectoryPath}";
        if (entry.IsIncomplete)
            this.FindControl<TextBlock>("LibraryDetailsText")!.Text += Environment.NewLine + "Captura incompleta: no debe transcribirse automáticamente.";
    }

    private void StartRecording_Click(object? sender, RoutedEventArgs e)
    {
        if (_recording is not null) return;
        if (this.FindControl<CheckBox>("ConsentCheckBox")!.IsChecked != true)
        {
            this.FindControl<TextBlock>("StatusText")!.Text = "CONSENTIMIENTO REQUERIDO";
            Log("Inicio bloqueado: falta confirmar autorización de todas las personas.");
            return;
        }
        if (this.FindControl<ComboBox>("MicrophoneDeviceBox")!.SelectedItem is not AudioDeviceOption mic || this.FindControl<ComboBox>("OutputDeviceBox")!.SelectedItem is not AudioDeviceOption output)
        {
            this.FindControl<TextBlock>("StatusText")!.Text = "SELECCIONA DISPOSITIVOS";
            return;
        }
        try
        {
            SaveSettings();
            var directory = RecordingDirectoryFactory.Create(_settings.RecordingsDirectory, this.FindControl<TextBox>("ReferenceBox")!.Text ?? "Llamada", _clock);
            RecordingAccessControl.RestrictSessionDirectory(directory);
            var session = new RecordingSession(directory, mic.Id, output.Id);
            session.Faulted += ex => Dispatcher.UIThread.Post(() =>
            {
                this.FindControl<TextBlock>("StatusText")!.Text = "ERROR DE GRABACIÓN";
                Log("Fallo de captura: " + ex.Message);
                this.FindControl<Button>("StartButton")!.IsEnabled = false;
                this.FindControl<Button>("StopButton")!.IsEnabled = true;
            });
            try { session.Start(); _recording = session; _activeSessionTitle = Path.GetFileName(directory); }
            catch { session.Dispose(); throw; }
            this.FindControl<Button>("StartButton")!.IsEnabled = false;
            this.FindControl<Button>("PauseButton")!.IsEnabled = true;
            this.FindControl<Button>("StopButton")!.IsEnabled = true;
            this.FindControl<ComboBox>("MicrophoneDeviceBox")!.IsEnabled = false;
            this.FindControl<ComboBox>("OutputDeviceBox")!.IsEnabled = false;
            this.FindControl<TextBox>("ReferenceBox")!.IsEnabled = false;
            this.FindControl<CheckBox>("ConsentCheckBox")!.IsEnabled = false;
            this.FindControl<TextBlock>("StatusText")!.Text = "GRABANDO";
            _timer.Start();
            Log("Grabación iniciada. Micrófono: " + session.MicrophoneName + "; salida: " + session.OutputName);
            ShowMiniMode();
            Log("Guardando pistas cifradas en: " + directory);
        }
        catch (Exception ex) { this.FindControl<TextBlock>("StatusText")!.Text = "ERROR"; Log("No se pudo iniciar: " + ex.Message); ResetRecordingState(); }
    }

    private void PauseRecording_Click(object? sender, RoutedEventArgs e)
    {
        if (_recording is null) return;
        try
        {
            if (_recording.IsPaused) { _recording.Resume(); this.FindControl<Button>("PauseButton")!.Content = "Ⅱ PAUSAR"; this.FindControl<TextBlock>("StatusText")!.Text = "GRABANDO"; }
            else { _recording.Pause(); this.FindControl<Button>("PauseButton")!.Content = "▶ REANUDAR"; this.FindControl<TextBlock>("StatusText")!.Text = "PAUSADO"; }
        }
        catch (Exception ex) { Log("No se pudo cambiar el estado: " + ex.Message); }
    }

    private async void StopRecording_Click(object? sender, RoutedEventArgs e)
    {
        var recording = _recording;
        if (recording is null) return;
        this.FindControl<Button>("StopButton")!.IsEnabled = false;
        this.FindControl<Button>("PauseButton")!.IsEnabled = false;
        Log("Cerrando ambas pistas...");
        try { await Task.Run(recording.Stop); Log("Grabación finalizada. Ambas pistas quedaron cerradas."); }
        catch (Exception ex) { Log("Error al finalizar: " + ex.Message); }
        finally
        {
            recording.Dispose();
            _recording = null;
            ResetRecordingState();
            RefreshLibrary();
        }
    }

    private void RefreshRecordingStatus()
    {
        if (_recording is not { } recording) return;
        var elapsed = recording.Elapsed;
        this.FindControl<TextBlock>("ElapsedText")!.Text = $"{(long)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        this.FindControl<ProgressBar>("MicLevel")!.Value = Math.Clamp(recording.MicrophoneLevel * 100, 0, 100);
        this.FindControl<ProgressBar>("SystemLevel")!.Value = Math.Clamp(recording.SystemLevel * 100, 0, 100);
        this.FindControl<TextBlock>("MicLevelText")!.Text = this.FindControl<ProgressBar>("MicLevel")!.Value.ToString("F0") + "%";
        this.FindControl<TextBlock>("SystemLevelText")!.Text = this.FindControl<ProgressBar>("SystemLevel")!.Value.ToString("F0") + "%";
        if (recording.IsPaused) this.FindControl<TextBlock>("StatusText")!.Text = "PAUSADO";
    }

    private void ResetRecordingState()
    {
        _timer.Stop();
        _activeSessionTitle = string.Empty;
        this.FindControl<Button>("StartButton")!.IsEnabled = true;
        this.FindControl<Button>("PauseButton")!.IsEnabled = false;
        this.FindControl<Button>("StopButton")!.IsEnabled = false;
        this.FindControl<Button>("PauseButton")!.Content = "Ⅱ PAUSAR";
        this.FindControl<ComboBox>("MicrophoneDeviceBox")!.IsEnabled = true;
        this.FindControl<ComboBox>("OutputDeviceBox")!.IsEnabled = true;
        this.FindControl<TextBox>("ReferenceBox")!.IsEnabled = true;
        this.FindControl<CheckBox>("ConsentCheckBox")!.IsEnabled = true;
        this.FindControl<TextBlock>("ElapsedText")!.Text = "00:00:00";
        this.FindControl<ProgressBar>("MicLevel")!.Value = 0;
        this.FindControl<ProgressBar>("SystemLevel")!.Value = 0;
        this.FindControl<TextBlock>("MicLevelText")!.Text = "En espera";
        this.FindControl<TextBlock>("SystemLevelText")!.Text = "En espera";
        this.FindControl<TextBlock>("StatusText")!.Text = "LISTO";
    }

    private void PlayMicrophone_Click(object? sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (this.FindControl<ListBox>("LibraryList")!.SelectedItem is AvaloniaRecordingEntry entry)
            PlayTrack(entry.MicrophonePath, "micrófono");
    }

    private void PlaySystem_Click(object? sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (this.FindControl<ListBox>("LibraryList")!.SelectedItem is AvaloniaRecordingEntry entry)
            PlayTrack(entry.ClientPath, "audio del sistema");
    }

    private void PlayTrack(string path, string trackName)
    {
        if (!File.Exists(path))
        {
            Log("Pista no disponible: " + path);
            this.FindControl<TextBlock>("LibraryDetailsText")!.Text = "No se encuentra el archivo de audio seleccionado.";
            return;
        }

        try
        {
            StopPlayback();
            var sessionDirectory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("La pista no tiene carpeta de sesión.");
            var encrypted = path.EndsWith(".trn", StringComparison.OrdinalIgnoreCase);
            if (encrypted)
                RecordingAccessControl.RestrictSessionDirectory(sessionDirectory);
            else
                RecordingAccessControl.ValidateSessionDirectory(sessionDirectory);

            _playbackLease = TranscriptionSessionLease.TryAcquire(sessionDirectory)
                ?? throw new InvalidOperationException("La sesión está siendo transcrita, migrada o modificada por otra operación.");

            var playablePath = encrypted ? EncryptedAudioTrack.DecryptToWaveFile(path) : path;
            if (!string.Equals(playablePath, path, StringComparison.OrdinalIgnoreCase))
                _playbackTemporaryPath = playablePath;

            _playbackReader = new AudioFileReader(playablePath);
            _playbackTrackName = trackName;
            this.FindControl<Slider>("PlaybackSeekSlider")!.Minimum = 0;
            this.FindControl<Slider>("PlaybackSeekSlider")!.Maximum = Math.Max(1, _playbackReader.TotalTime.TotalSeconds);
            this.FindControl<Slider>("PlaybackSeekSlider")!.Value = 0;
            this.FindControl<Slider>("PlaybackSeekSlider")!.IsEnabled = _playbackReader.TotalTime.TotalSeconds > 0;
            this.FindControl<TextBlock>("PlaybackCurrentTimeText")!.Text = "00:00";
            this.FindControl<TextBlock>("PlaybackDurationText")!.Text = FormatPlaybackTime(_playbackReader.TotalTime);
            this.FindControl<TextBlock>("PlaybackTrackText")!.Text = $"REPRODUCIENDO · {trackName.ToUpperInvariant()}";

            _playback = new WaveOutEvent();
            _playback.PlaybackStopped += (_, args) => Dispatcher.UIThread.Post(() =>
            {
                if (args.Exception is not null) Log("Error de reproducción: " + args.Exception.Message);
                ReleasePlayback();
            });
            _playback.Init(_playbackReader);
            _playback.Play();
            _timer.Start();
            Log($"Reproduciendo pista de {trackName}: {path}");
        }
        catch (Exception ex)
        {
            ReleasePlayback();
            Log("No se pudo reproducir la pista: " + ex.Message);
            this.FindControl<TextBlock>("LibraryDetailsText")!.Text = "Error de reproducción: " + ex.Message;
        }
    }

    private void StopPlayback_Click(object? sender, RoutedEventArgs e) => StopPlayback();

    private void PlaybackSeekSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingPlaybackTimeline || _playbackReader is null) return;
        _playbackReader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(
            this.FindControl<Slider>("PlaybackSeekSlider")!.Value, 0, _playbackReader.TotalTime.TotalSeconds));
    }

    private void UpdatePlaybackTimeline()
    {
        var reader = _playbackReader;
        if (reader is null) return;
        _updatingPlaybackTimeline = true;
        try
        {
            this.FindControl<Slider>("PlaybackSeekSlider")!.Value = Math.Clamp(reader.CurrentTime.TotalSeconds,
                this.FindControl<Slider>("PlaybackSeekSlider")!.Minimum, this.FindControl<Slider>("PlaybackSeekSlider")!.Maximum);
            this.FindControl<TextBlock>("PlaybackCurrentTimeText")!.Text = FormatPlaybackTime(reader.CurrentTime);
        }
        finally { _updatingPlaybackTimeline = false; }
    }

    private static string FormatPlaybackTime(TimeSpan time)
    {
        var totalSeconds = Math.Max(0, (long)time.TotalSeconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void StopPlayback()
    {
        try { _playback?.Stop(); }
        finally { ReleasePlayback(); }
    }

    private void ReleasePlayback()
    {
        var playback = _playback;
        var reader = _playbackReader;
        var temporaryPath = _playbackTemporaryPath;
        var lease = _playbackLease;
        _playback = null;
        _playbackReader = null;
        _playbackTemporaryPath = null;
        _playbackLease = null;
        _playbackTrackName = null;
        this.FindControl<Slider>("PlaybackSeekSlider")!.IsEnabled = false;
        this.FindControl<Slider>("PlaybackSeekSlider")!.Value = 0;
        this.FindControl<TextBlock>("PlaybackCurrentTimeText")!.Text = "00:00";
        this.FindControl<TextBlock>("PlaybackDurationText")!.Text = "00:00";
        this.FindControl<TextBlock>("PlaybackTrackText")!.Text = "Selecciona una pista para reproducir";

        try { playback?.Dispose(); }
        catch (Exception ex) { Log("No se pudo liberar el dispositivo de reproducción: " + ex.Message); }
        try { reader?.Dispose(); }
        catch (Exception ex) { Log("No se pudo liberar el lector de audio: " + ex.Message); }
        if (temporaryPath is not null)
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception ex) { Log("No se pudo eliminar el temporal de reproducción: " + ex.Message); }
        }
        lease?.Dispose();
        if (_recording is null) _timer.Stop();
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        this.FindControl<TextBox>("LogBox")!.Text = string.IsNullOrEmpty(this.FindControl<TextBox>("LogBox")!.Text) ? line : this.FindControl<TextBox>("LogBox")!.Text + Environment.NewLine + line;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _transcriptionCancellation?.Cancel();
        if (_miniModeWindow is not null) { var mini = _miniModeWindow; _miniModeWindow = null; mini.CloseFromMain(); }
        StopPlayback();
        if (_recording is { } recording)
        {
            try { recording.Stop(); }
            catch (Exception ex) { Log("Error al cerrar captura: " + ex.Message); }
            finally { recording.Dispose(); _recording = null; }
        }
        base.OnClosed(e);
    }
}
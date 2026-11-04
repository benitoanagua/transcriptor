using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using NAudio.Wave;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

    public MainWindow()
    {
        InitializeComponent();
        var loaded = _settingsStore.Load();
        _settings = loaded.Settings;
        RecordingsPathBox.Text = _settings.RecordingsDirectory;
        WhisperExecutableBox.Text = _settings.WhisperExecutablePath;
        WhisperModelBox.Text = _settings.WhisperModelPath;
        WhisperLanguageBox.Text = _settings.Language;
        RetentionDaysBox.Text = _settings.RetentionDays.ToString();
        RetentionSettingsBox.Text = _settings.RetentionDays.ToString();
        if (!string.IsNullOrWhiteSpace(loaded.Warning)) Log(loaded.Warning);
        Log("Configuración cargada desde " + _settingsStore.SettingsPath);
        _timer.Tick += (_, _) =>
        {
            RefreshRecordingStatus();
            UpdatePlaybackTimeline();
        };
        RefreshAudioDevices();
        RefreshLibrary();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private void RefreshDevices_Click(object? sender, RoutedEventArgs e) => RefreshAudioDevices();
    private void SaveSettings_Click(object? sender, RoutedEventArgs e) => SaveSettings();
    private void RefreshLibrary_Click(object? sender, RoutedEventArgs e) => RefreshLibrary();
    private void LibrarySearch_TextChanged(object? sender, TextChangedEventArgs e) => ApplyLibraryFilters();
    private void LibraryFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ApplyLibraryFilters();
    private void CancelTranscription_Click(object? sender, RoutedEventArgs e) => _transcriptionCancellation?.Cancel();

    private async Task<bool> ConfirmAsync(string title, string message)
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
        return await dialog.ShowDialog<bool>(this);
    }

    private async void MigrateSession_Click(object? sender, RoutedEventArgs e)
    {
        if (LibraryList.SelectedItem is not AvaloniaRecordingEntry entry || !_transcriptionUiState.TryBegin()) return;
        if (!await ConfirmAsync("Migrar sesión a cifrado",
            "Se crearán y verificarán copias cifradas. Los originales en claro solo se eliminarán después de verificar todos los archivos. El borrado no es forense. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        if (_playbackReader is not null) StopPlayback();
        SetLibraryOperationEnabled(false);
        LifecycleProgressText.Text = "Migrando y verificando archivos…";
        try
        {
            var result = await Task.Run(() => _sessionStore.MigrateSession(entry.DirectoryPath));
            LifecycleProgressText.Text = $"Migración completada: {result.AudioTracksMigrated} pistas, {result.TranscriptsMigrated} transcripciones, {result.AlreadyEncrypted} ya cifrados.";
            Log(LifecycleProgressText.Text);
        }
        catch (Exception ex)
        {
            LifecycleProgressText.Text = "Migración incompleta; revisa el registro. Los originales no verificados se conservaron.";
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
        if (LibraryList.SelectedItem is not AvaloniaRecordingEntry entry || !_transcriptionUiState.TryBegin()) return;
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
            var root = RecordingsPathBox.Text?.Trim() ?? string.Empty;
            await Task.Run(() => _sessionStore.DeleteSession(root, entry.DirectoryPath));
            LifecycleProgressText.Text = "Sesión eliminada.";
            Log("Sesión eliminada: " + entry.Title);
        }
        catch (Exception ex)
        {
            LifecycleProgressText.Text = "No se pudo eliminar la sesión.";
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
        var days = int.TryParse(RetentionDaysBox.Text, out var parsed) ? Math.Clamp(parsed, 0, 3650) : 0;
        if (days <= 0)
        {
            _transcriptionUiState.Complete();
            LifecycleProgressText.Text = "Retención desactivada. Define entre 1 y 3650 días.";
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
            RetentionSettingsBox.Text = days.ToString();
            _settingsStore.Save(_settings);
            var root = RecordingsPathBox.Text?.Trim() ?? string.Empty;
            var deleted = await Task.Run(() => _sessionStore.ApplyRetention(root, days, _clock.UtcNow.UtcDateTime));
            LifecycleProgressText.Text = $"Retención aplicada: {deleted.Count} sesión(es) eliminadas.";
            Log(LifecycleProgressText.Text);
        }
        catch (Exception ex)
        {
            LifecycleProgressText.Text = "No se pudo completar la retención.";
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
        MigrateSessionButton.IsEnabled = idle;
        DeleteSessionButton.IsEnabled = idle;
        ApplyRetentionButton.IsEnabled = idle;
        TranscribeButton.IsEnabled = idle && _recording is null;
        StartButton.IsEnabled = idle && _recording is null;
        LibraryList.IsEnabled = idle;
        RefreshLibraryButton.IsEnabled = idle;
        if (!enabled)
        {
            CancelTranscriptionButton.IsEnabled = false;
            StopPlayback();
        }
    }

    private void SelectLibraryEntry(string path)
    {
        LibraryList.SelectedItem = (LibraryList.ItemsSource as IEnumerable<AvaloniaRecordingEntry>)?
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

        var root = RecordingsPathBox.Text?.Trim() ?? string.Empty;
        var executable = WhisperExecutableBox.Text?.Trim() ?? string.Empty;
        var model = WhisperModelBox.Text?.Trim() ?? string.Empty;
        var language = string.IsNullOrWhiteSpace(WhisperLanguageBox.Text) ? "es" : WhisperLanguageBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)
            || string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(model))
        {
            _transcriptionUiState.Complete();
            TranscriptionProgressText.Text = "Configura una carpeta existente, whisper-cli y el modelo en AJUSTES.";
            Log("Transcripción no iniciada: faltan rutas válidas de grabaciones, Whisper o modelo.");
            return;
        }

        _transcriptionCancellation = new CancellationTokenSource();
        TranscribeButton.IsEnabled = false;
        MigrateSessionButton.IsEnabled = false;
        DeleteSessionButton.IsEnabled = false;
        ApplyRetentionButton.IsEnabled = false;
        LibraryList.IsEnabled = false;
        RefreshLibraryButton.IsEnabled = false;
        CancelTranscriptionButton.IsEnabled = true;
        StartButton.IsEnabled = false;
        TranscriptionProgressText.Text = "Preparando transcripción…";
        Log("Iniciando transcripción local de sesiones pendientes.");
        var progress = new Progress<string>(message =>
        {
            TranscriptionProgressText.Text = message;
            Log(message);
        });
        try
        {
            var engine = new TranscriptionEngine(executable, model, language);
            var processed = await engine.ProcessPendingCallsAsync(root, progress, _transcriptionCancellation.Token);
            TranscriptionProgressText.Text = $"Proceso finalizado. Sesiones transcritas: {processed}.";
            Log($"Transcripción finalizada. Sesiones procesadas: {processed}.");
        }
        catch (OperationCanceledException)
        {
            TranscriptionProgressText.Text = "Transcripción cancelada. Los originales se conservaron.";
            Log("Transcripción cancelada por el usuario.");
        }
        catch (Exception ex)
        {
            TranscriptionProgressText.Text = "La transcripción terminó con errores. Consulta el registro.";
            Log("Error de transcripción: " + ex.Message);
        }
        finally
        {
            _transcriptionCancellation.Dispose();
            _transcriptionCancellation = null;
            _transcriptionUiState.Complete();
            TranscribeButton.IsEnabled = true;
            MigrateSessionButton.IsEnabled = true;
            DeleteSessionButton.IsEnabled = true;
            ApplyRetentionButton.IsEnabled = true;
            LibraryList.IsEnabled = true;
            RefreshLibraryButton.IsEnabled = true;
            CancelTranscriptionButton.IsEnabled = false;
            StartButton.IsEnabled = _recording is null;
            RefreshLibrary();
        }
    }

    private void RefreshAudioDevices()
    {
        try
        {
            var microphones = RecordingSession.GetCaptureDevices();
            var outputs = RecordingSession.GetRenderDevices();
            MicrophoneDeviceBox.ItemsSource = microphones;
            OutputDeviceBox.ItemsSource = outputs;
            var micId = _settings.MicrophoneDeviceId ?? RecordingSession.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Capture);
            var outputId = _settings.OutputDeviceId ?? RecordingSession.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Render);
            MicrophoneDeviceBox.SelectedItem = microphones.FirstOrDefault(x => x.Id == micId) ?? microphones.FirstOrDefault();
            OutputDeviceBox.SelectedItem = outputs.FirstOrDefault(x => x.Id == outputId) ?? outputs.FirstOrDefault();
            Log($"Dispositivos detectados: {microphones.Count} entradas y {outputs.Count} salidas.");
        }
        catch (Exception ex) { Log("No se pudieron enumerar los dispositivos: " + ex.Message); }
    }

    private void SaveSettings()
    {
        _settings = _settings with
        {
            RecordingsDirectory = RecordingsPathBox.Text?.Trim() ?? string.Empty,
            WhisperExecutablePath = WhisperExecutableBox.Text?.Trim() ?? string.Empty,
            WhisperModelPath = WhisperModelBox.Text?.Trim() ?? string.Empty,
            Language = string.IsNullOrWhiteSpace(WhisperLanguageBox.Text) ? "es" : WhisperLanguageBox.Text.Trim(),
            RetentionDays = int.TryParse(RetentionSettingsBox.Text, out var retentionDays) ? Math.Clamp(retentionDays, 0, 3650) : 0,
            MicrophoneDeviceId = (MicrophoneDeviceBox.SelectedItem as AudioDeviceOption)?.Id,
            OutputDeviceId = (OutputDeviceBox.SelectedItem as AudioDeviceOption)?.Id
        };
        _settingsStore.Save(_settings);
        Log("Configuración guardada.");
        RefreshLibrary();
    }

    private void RefreshLibrary()
    {
        if (_transcriptionUiState.IsTranscribing) return;
        try
        {
            var root = RecordingsPathBox.Text?.Trim() ?? string.Empty;
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
            LibraryCountText.Text = "No se pudo cargar la biblioteca.";
            LibraryDetailsTitle.Text = "Error de biblioteca";
            LibraryDetailsText.Text = ex.Message;
            Log("No se pudo actualizar la biblioteca: " + ex.Message);
        }
    }

    private void ApplyLibraryFilters()
    {
        if (LibraryList is null) return;
        var query = LibrarySearchBox.Text?.Trim() ?? string.Empty;
        var filter = LibraryFilterBox.SelectedIndex;
        var selectedPath = (LibraryList.SelectedItem as AvaloniaRecordingEntry)?.DirectoryPath;
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
        LibraryList.ItemsSource = filtered;
        LibraryList.SelectedItem = selectedPath is null ? null : filtered.FirstOrDefault(entry =>
            string.Equals(entry.DirectoryPath, selectedPath, StringComparison.OrdinalIgnoreCase));
        LibraryCountText.Text = $"{filtered.Count} de {_allRecordings.Count} sesión(es)";
    }

    private void LibraryList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_playbackReader is not null) StopPlayback();
        if (LibraryList.SelectedItem is not AvaloniaRecordingEntry entry)
        {
            LibraryDetailsTitle.Text = "Selecciona una sesión";
            LibraryDetailsText.Text = "La biblioteca muestra sesiones locales reconocidas.";
            return;
        }
        LibraryDetailsTitle.Text = entry.Title;
        LibraryDetailsText.Text = $"{entry.Summary}{Environment.NewLine}{entry.DirectoryPath}";
        if (entry.IsIncomplete)
            LibraryDetailsText.Text += Environment.NewLine + "Captura incompleta: no debe transcribirse automáticamente.";
    }

    private void StartRecording_Click(object? sender, RoutedEventArgs e)
    {
        if (_recording is not null) return;
        if (ConsentCheckBox.IsChecked != true)
        {
            StatusText.Text = "CONSENTIMIENTO REQUERIDO";
            Log("Inicio bloqueado: falta confirmar autorización de todas las personas.");
            return;
        }
        if (MicrophoneDeviceBox.SelectedItem is not AudioDeviceOption mic || OutputDeviceBox.SelectedItem is not AudioDeviceOption output)
        {
            StatusText.Text = "SELECCIONA DISPOSITIVOS";
            return;
        }
        try
        {
            SaveSettings();
            var directory = RecordingDirectoryFactory.Create(_settings.RecordingsDirectory, ReferenceBox.Text ?? "Llamada", _clock);
            RecordingAccessControl.RestrictSessionDirectory(directory);
            var session = new RecordingSession(directory, mic.Id, output.Id);
            session.Faulted += ex => Dispatcher.UIThread.Post(() =>
            {
                StatusText.Text = "ERROR DE GRABACIÓN";
                Log("Fallo de captura: " + ex.Message);
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            });
            try { session.Start(); _recording = session; }
            catch { session.Dispose(); throw; }
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            MicrophoneDeviceBox.IsEnabled = false;
            OutputDeviceBox.IsEnabled = false;
            ReferenceBox.IsEnabled = false;
            ConsentCheckBox.IsEnabled = false;
            StatusText.Text = "GRABANDO";
            _timer.Start();
            Log("Grabación iniciada. Micrófono: " + session.MicrophoneName + "; salida: " + session.OutputName);
            Log("Guardando pistas cifradas en: " + directory);
        }
        catch (Exception ex) { StatusText.Text = "ERROR"; Log("No se pudo iniciar: " + ex.Message); ResetRecordingState(); }
    }

    private void PauseRecording_Click(object? sender, RoutedEventArgs e)
    {
        if (_recording is null) return;
        try
        {
            if (_recording.IsPaused) { _recording.Resume(); PauseButton.Content = "Ⅱ PAUSAR"; StatusText.Text = "GRABANDO"; }
            else { _recording.Pause(); PauseButton.Content = "▶ REANUDAR"; StatusText.Text = "PAUSADO"; }
        }
        catch (Exception ex) { Log("No se pudo cambiar el estado: " + ex.Message); }
    }

    private async void StopRecording_Click(object? sender, RoutedEventArgs e)
    {
        var recording = _recording;
        if (recording is null) return;
        StopButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
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
        ElapsedText.Text = $"{(long)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        MicLevel.Value = Math.Clamp(recording.MicrophoneLevel * 100, 0, 100);
        SystemLevel.Value = Math.Clamp(recording.SystemLevel * 100, 0, 100);
        MicLevelText.Text = MicLevel.Value.ToString("F0") + "%";
        SystemLevelText.Text = SystemLevel.Value.ToString("F0") + "%";
        if (recording.IsPaused) StatusText.Text = "PAUSADO";
    }

    private void ResetRecordingState()
    {
        _timer.Stop();
        StartButton.IsEnabled = true;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        PauseButton.Content = "Ⅱ PAUSAR";
        MicrophoneDeviceBox.IsEnabled = true;
        OutputDeviceBox.IsEnabled = true;
        ReferenceBox.IsEnabled = true;
        ConsentCheckBox.IsEnabled = true;
        ElapsedText.Text = "00:00:00";
        MicLevel.Value = 0;
        SystemLevel.Value = 0;
        MicLevelText.Text = "En espera";
        SystemLevelText.Text = "En espera";
        StatusText.Text = "LISTO";
    }

    private void PlayMicrophone_Click(object? sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (LibraryList.SelectedItem is AvaloniaRecordingEntry entry)
            PlayTrack(entry.MicrophonePath, "micrófono");
    }

    private void PlaySystem_Click(object? sender, RoutedEventArgs e)
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (LibraryList.SelectedItem is AvaloniaRecordingEntry entry)
            PlayTrack(entry.ClientPath, "audio del sistema");
    }

    private void PlayTrack(string path, string trackName)
    {
        if (!File.Exists(path))
        {
            Log("Pista no disponible: " + path);
            LibraryDetailsText.Text = "No se encuentra el archivo de audio seleccionado.";
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
            PlaybackSeekSlider.Minimum = 0;
            PlaybackSeekSlider.Maximum = Math.Max(1, _playbackReader.TotalTime.TotalSeconds);
            PlaybackSeekSlider.Value = 0;
            PlaybackSeekSlider.IsEnabled = _playbackReader.TotalTime.TotalSeconds > 0;
            PlaybackCurrentTimeText.Text = "00:00";
            PlaybackDurationText.Text = FormatPlaybackTime(_playbackReader.TotalTime);
            PlaybackTrackText.Text = $"REPRODUCIENDO · {trackName.ToUpperInvariant()}";

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
            LibraryDetailsText.Text = "Error de reproducción: " + ex.Message;
        }
    }

    private void StopPlayback_Click(object? sender, RoutedEventArgs e) => StopPlayback();

    private void PlaybackSeekSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingPlaybackTimeline || _playbackReader is null) return;
        _playbackReader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(
            PlaybackSeekSlider.Value, 0, _playbackReader.TotalTime.TotalSeconds));
    }

    private void UpdatePlaybackTimeline()
    {
        var reader = _playbackReader;
        if (reader is null) return;
        _updatingPlaybackTimeline = true;
        try
        {
            PlaybackSeekSlider.Value = Math.Clamp(reader.CurrentTime.TotalSeconds,
                PlaybackSeekSlider.Minimum, PlaybackSeekSlider.Maximum);
            PlaybackCurrentTimeText.Text = FormatPlaybackTime(reader.CurrentTime);
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
        PlaybackSeekSlider.IsEnabled = false;
        PlaybackSeekSlider.Value = 0;
        PlaybackCurrentTimeText.Text = "00:00";
        PlaybackDurationText.Text = "00:00";
        PlaybackTrackText.Text = "Selecciona una pista para reproducir";

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
        LogBox.Text = string.IsNullOrEmpty(LogBox.Text) ? line : LogBox.Text + Environment.NewLine + line;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _transcriptionCancellation?.Cancel();
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
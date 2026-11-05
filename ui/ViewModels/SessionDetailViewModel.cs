using System.IO;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transcriptor.Transcription;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Panel de detalle de la sesión seleccionada: título/ruta/aviso de
/// integridad, reproducción respetando el lease y la limpieza del temporal,
/// operaciones de transcripción/migración/eliminación (con las mismas
/// confirmaciones de siempre). La velocidad de refresco del reproductor la ordena la vista
/// con <see cref="Tick"/>.
/// </summary>
public sealed partial class SessionDetailViewModel : ObservableObject
{
    private readonly UiServices _services;
    private readonly TranscriptionUiState _transcriptionUiState;
    private readonly ILogSink _log;
    private readonly IUiHost _host;
    private readonly Func<string> _recordingsDirectory;

    private IAudioPlayer? _playbackPlayer;
    private string? _playbackTemporaryPath;
    private TranscriptionSessionLease? _playbackLease;
    private string? _playbackTrackName;
    private bool _suppressSeek;

    public SessionDetailViewModel(
        UiServices services, TranscriptionUiState transcriptionUiState, ILogSink log, IUiHost host,
        LibraryViewModel library, Func<string> recordingsDirectory)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _transcriptionUiState = transcriptionUiState ?? throw new ArgumentNullException(nameof(transcriptionUiState));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _recordingsDirectory = recordingsDirectory ?? throw new ArgumentNullException(nameof(recordingsDirectory));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _sessionStore = services.CreateSessionStore();
        library.SelectionChanged += OnLibrarySelectionChanged;
        _transcriptionUiState.Changed += () =>
        {
            OnPropertyChanged(nameof(CanUseLibraryOperations));
            OnPropertyChanged(nameof(CanUseSelectedLibraryOperations));
        };
    }

    private readonly LibraryViewModel _library;
    private readonly ISessionStore _sessionStore;

    public bool CanUseLibraryOperations => !_transcriptionUiState.IsTranscribing;
    public bool CanUseSelectedLibraryOperations => CanUseLibraryOperations && _library.SelectedRecording is not null;
    public bool SupportsMixedAudioPlayback => _services.Capabilities.SupportsMixedAudioPlayback;
    public bool SupportsVariablePlaybackRate => _services.Capabilities.SupportsVariablePlaybackRate;

    // ------------------------------------------------------------------ observables
    [ObservableProperty] private string _libraryDetailsTitle = "Selecciona una sesión";
    [ObservableProperty] private string _libraryDetailsText = "Selecciona una sesión para ver las pistas reconocidas.";
    [ObservableProperty] private string _playbackCurrentTimeText = "00:00";
    [ObservableProperty] private string _playbackDurationText = "00:00";
    [ObservableProperty] private string _playbackTrackText = "Selecciona una pista para reproducir";
    [ObservableProperty] private double _seekValue;
    [ObservableProperty] private double _seekMaximum = 1;
    [ObservableProperty] private bool _seekEnabled;
    [ObservableProperty] private bool _playbackPaused;

    public string PlaybackToggleLabel => PlaybackPaused ? "▶ REANUDAR" : "Ⅱ PAUSAR";

    partial void OnPlaybackPausedChanged(bool value) => OnPropertyChanged(nameof(PlaybackToggleLabel));
    [ObservableProperty] private TimeSpan _currentPlaybackPosition;
    [ObservableProperty] private string _lifecycleProgressText = "Migración y eliminación requieren confirmación.";

    partial void OnSeekValueChanged(double value)
    {
        if (_suppressSeek) return;
        if (value < 0 || value > SeekMaximum) return;
        _playbackPlayer?.Seek(TimeSpan.FromSeconds(Math.Clamp(value, 0, Math.Max(1, _playbackPlayer.Duration.TotalSeconds))));
    }

    /// <summary>Refresco del reproductor activo (tiempo y seek); lo llama la vista.</summary>
    public void Tick()
    {
        if (_playbackPlayer is not { } player) return;
        _suppressSeek = true;
        try { SeekValue = Math.Clamp(player.Position.TotalSeconds, 0, Math.Max(1, SeekMaximum)); }
        finally { _suppressSeek = false; }
        PlaybackCurrentTimeText = FormatPlaybackTime(player.Position);
        CurrentPlaybackPosition = player.Position;
    }

    public void SeekTo(TimeSpan position)
    {
        if (_playbackPlayer is not { } player) return;
        var clamped = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, Math.Max(0, player.Duration.TotalSeconds)));
        player.Seek(clamped);
        CurrentPlaybackPosition = clamped;
        _suppressSeek = true;
        try { SeekValue = clamped.TotalSeconds; }
        finally { _suppressSeek = false; }
        PlaybackCurrentTimeText = FormatPlaybackTime(clamped);
    }

    private void OnLibrarySelectionChanged(LibraryEntry? value)
    {
        OnPropertyChanged(nameof(CanUseSelectedLibraryOperations));
        if (_playbackPlayer is not null) StopPlayback();
        if (value is null)
        {
            LibraryDetailsTitle = "Selecciona una sesión";
            LibraryDetailsText = "Selecciona una sesión para ver las pistas reconocidas.";
            return;
        }
        LibraryDetailsTitle = value.Title;
        LibraryDetailsText = $"{value.Summary}{Environment.NewLine}{value.DirectoryPath}";
        if (value.IsIncomplete)
            LibraryDetailsText += Environment.NewLine + "Captura incompleta: no debe transcribirse automáticamente.";
    }

    private static string FormatPlaybackTime(TimeSpan time)
    {
        var totalSeconds = Math.Max(0, (long)time.TotalSeconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    // ------------------------------------------------------------------ reproducción
    [RelayCommand]
    private void PlayMicrophone()
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (_library.SelectedRecording is { } entry) PlayTrack(entry.MicrophonePath, "micrófono");
    }

    [RelayCommand]
    private void PlaySystem()
    {
        if (_transcriptionUiState.IsTranscribing) return;
        if (_library.SelectedRecording is { } entry) PlayTrack(entry.ClientPath, "audio del sistema");
    }

    [RelayCommand]
    private async Task OpenSelectedFolderAsync()
    {
        if (_library.SelectedRecording is not { } entry) return;
        try { await _host.OpenFolderAsync(entry.DirectoryPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Win32Exception)
        {
            LifecycleProgressText = "No se pudo abrir la carpeta de la sesión.";
            _log.Log("No se pudo abrir la carpeta: " + ex.Message);
        }
    }

    private void PlayTrack(string path, string trackName)
    {
        if (!_services.Capabilities.SupportsAudioPlayback)
        {
            _log.Log("La reproducción no está disponible en esta plataforma.");
            return;
        }
        if (!File.Exists(path))
        {
            _log.Log("Pista no disponible: " + path);
            LibraryDetailsText = "No se encuentra el archivo de audio seleccionado.";
            return;
        }

        try
        {
            StopPlayback();
            var sessionDirectory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("La pista no tiene carpeta de sesión.");
            var encrypted = path.EndsWith(".trn", StringComparison.OrdinalIgnoreCase);
            if (encrypted)
                _services.Security.RestrictSessionDirectory(sessionDirectory);
            else
                _services.Security.ValidateSessionDirectory(sessionDirectory);

            _playbackLease = TranscriptionSessionLease.TryAcquire(sessionDirectory)
                ?? throw new InvalidOperationException("La sesión está siendo transcrita, migrada o modificada por otra operación.");

            var playablePath = encrypted ? EncryptedAudioTrack.DecryptToWaveFile(path) : path;
            if (!string.Equals(playablePath, path, StringComparison.OrdinalIgnoreCase))
                _playbackTemporaryPath = playablePath;

            _playbackPlayer = _services.PlayerFactory.Create(playablePath);
            var duration = _playbackPlayer.Duration;
            _playbackPlayer.Ended += () => _uiPost(ReleasePlayback);
            _playbackPlayer.Failed += ex => _uiPost(() =>
            {
                _log.Log("Error de reproducción: " + ex.Message);
                ReleasePlayback();
            });
            _playbackTrackName = trackName;
            SeekMaximum = Math.Max(1, duration.TotalSeconds);
            _suppressSeek = true;
            try { SeekValue = 0; }
            finally { _suppressSeek = false; }
            SeekEnabled = duration.TotalSeconds > 0;
            PlaybackCurrentTimeText = "00:00";
            PlaybackDurationText = FormatPlaybackTime(duration);
            PlaybackTrackText = $"REPRODUCIENDO · {trackName.ToUpperInvariant()}";
            _log.Log($"Reproduciendo pista de {trackName}: {path}");
        }
        catch (Exception ex)
        {
            ReleasePlayback();
            _log.Log("No se pudo reproducir la pista: " + ex.Message);
            LibraryDetailsText = "Error de reproducción: " + ex.Message;
        }
    }

    /// <summary>Despacho configurable; el shell fija Dispatcher.UIThread.Post.</summary>
    public Action<Action>? UiPost { get; set; }

    private void _uiPost(Action action) => (UiPost ?? (a => a()))(action);

    /// <summary>Detiene la reproducción activa (lease y temporal incluidos).</summary>
    [RelayCommand]
    public void StopPlayback()
    {
        var player = _playbackPlayer;
        try { player?.Dispose(); }
        finally { ReleasePlayback(); }
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (_playbackPlayer is not { } player) return;
        try
        {
            if (PlaybackPaused)
            {
                player.Resume();
                PlaybackPaused = false;
                PlaybackTrackText = $"REPRODUCIENDO · {_playbackTrackName?.ToUpperInvariant()}";
            }
            else
            {
                player.Pause();
                PlaybackPaused = true;
                PlaybackTrackText = $"PAUSADO · {_playbackTrackName?.ToUpperInvariant()}";
            }
        }
        catch (Exception ex)
        {
            _log.Log("No se pudo pausar o reanudar la reproducción: " + ex.Message);
        }
    }

    private void ReleasePlayback()
    {
        var player = _playbackPlayer;
        var temporaryPath = _playbackTemporaryPath;
        var lease = _playbackLease;
        _playbackPlayer = null;
        _playbackTemporaryPath = null;
        _playbackLease = null;
        _playbackTrackName = null;
        PlaybackPaused = false;
        SeekEnabled = false;
        _suppressSeek = true;
        try { SeekValue = 0; }
        finally { _suppressSeek = false; }
        PlaybackCurrentTimeText = "00:00";
        PlaybackDurationText = "00:00";
        CurrentPlaybackPosition = TimeSpan.Zero;
        PlaybackTrackText = "Selecciona una pista para reproducir";

        try { player?.Dispose(); }
        catch (Exception ex) { _log.Log("No se pudo liberar el dispositivo de reproducción: " + ex.Message); }
        if (temporaryPath is not null)
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception ex) { _log.Log("No se pudo eliminar el temporal de reproducción: " + ex.Message); }
        }
        lease?.Dispose();
    }

    // ------------------------------------------------------------------ operaciones de ciclo de vida
    [RelayCommand]
    private async Task MigrateSessionAsync()
    {
        if (_library.SelectedRecording is not { } entry || !_transcriptionUiState.TryBegin()) return;
        if (!await _host.ConfirmAsync("Migrar sesión a cifrado",
            "Se crearán y verificarán copias cifradas. Los originales en claro solo se eliminarán después de verificar todos los archivos. El borrado no es forense. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        StopPlayback();
        LifecycleProgressText = "Migrando y verificando archivos…";
        try
        {
            var result = await Task.Run(() => _sessionStore.MigrateSession(entry.DirectoryPath));
            var completed = $"Migración completada: {result.AudioTracksMigrated} pistas, {result.TranscriptsMigrated} transcripciones, {result.AlreadyEncrypted} ya cifrados.";
            LifecycleProgressText = completed;
            _log.Log(completed);
        }
        catch (Exception ex)
        {
            LifecycleProgressText = "Migración incompleta; revisa el registro. Los originales no verificados se conservaron.";
            _log.Log("Error de migración: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            _library.RefreshLibraryCommand.Execute(null);
            SelectLibraryEntry(entry.DirectoryPath);
        }
    }

    [RelayCommand]
    private async Task DeleteSessionAsync()
    {
        if (_library.SelectedRecording is not { } entry || !_transcriptionUiState.TryBegin()) return;
        if (!await _host.ConfirmAsync("Eliminar sesión",
            "Se eliminarán permanentemente los audios y transcripciones de esta sesión. Esta acción no se puede deshacer. ¿Continuar?"))
        {
            _transcriptionUiState.Complete();
            return;
        }
        StopPlayback();
        try
        {
            await Task.Run(() => _sessionStore.DeleteSession(_recordingsDirectory(), entry.DirectoryPath));
            LifecycleProgressText = "Sesión eliminada.";
            _log.Log("Sesión eliminada: " + entry.Title);
        }
        catch (Exception ex)
        {
            LifecycleProgressText = "No se pudo eliminar la sesión.";
            _log.Log("Error al eliminar sesión: " + ex.Message);
        }
        finally
        {
            _transcriptionUiState.Complete();
            _library.RefreshLibraryCommand.Execute(null);
        }
    }

    private void SelectLibraryEntry(string path)
    {
        _library.SelectEntry(path);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transcriptor.Transcription;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Estado de captura y de la configuración de grabación: dispositivos,
/// referencia, consentimiento, cronómetro y niveles de las dos pistas.
/// La llamada a <see cref="Tick"/> la hace la vista (DispatcherTimer);
/// el ViewModel permanece testeable sin Avalonia delegando el despacho
/// de eventos de fondo en <paramref name="uiDispatch"/>.
/// </summary>
public sealed partial class RecorderViewModel : ObservableObject
{
    private readonly UiServices _services;
    private readonly TranscriptionUiState _transcriptionUiState;
    private readonly ILogSink _log;
    private readonly Action<Action> _uiDispatch;
    private readonly AudioPeakHoldMeter _microphoneMeter;
    private readonly AudioPeakHoldMeter _systemMeter;
    private readonly AudioSilenceDetector _microphoneSilence;
    private readonly AudioSilenceDetector _systemSilence;
    private readonly AudioNoiseFloorEstimator _microphoneNoiseFloor = new();
    private readonly AudioNoiseFloorEstimator _systemNoiseFloor = new();
    private readonly PeakHistory _microphonePeaks = new();
    private readonly PeakHistory _systemPeaks = new();
    private IAudioCaptureSession? _recording;
    private Task<bool>? _stopTask;
    private string? _activeSessionDirectory;

    public RecorderViewModel(
        UiServices services, TranscriptionUiState transcriptionUiState, ILogSink log, Action<Action> uiDispatch)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _transcriptionUiState = transcriptionUiState ?? throw new ArgumentNullException(nameof(transcriptionUiState));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
        _microphoneMeter = new AudioPeakHoldMeter(services.Clock);
        _systemMeter = new AudioPeakHoldMeter(services.Clock);
        _microphoneSilence = new AudioSilenceDetector(services.Clock);
        _systemSilence = new AudioSilenceDetector(services.Clock);
        _transcriptionUiState.Changed += OnTranscriptionStateChanged;
        RefreshDevices();
    }

    /// <summary>La sesión activa cambia (arranque/parada) para coordinar cierre y bloqueos.</summary>
    public event Action? RecordingEnded;
    public event Action? RecordingStateChanged;
    public event Action<Exception>? Faulted;
    public event Func<string, Task>? RecordingStoppedSuccessfully;

    private void RaiseCanStart()
    {
        OnPropertyChanged(nameof(IsRecordingActive));
        OnPropertyChanged(nameof(CanStartRecording));
        OnPropertyChanged(nameof(CanStopRecording));
        OnPropertyChanged(nameof(RecordingControlsEnabled));
        StartRecordingCommand.NotifyCanExecuteChanged();
        PauseRecordingCommand.NotifyCanExecuteChanged();
        StopRecordingCommand.NotifyCanExecuteChanged();
        RecordingStateChanged?.Invoke();
    }

    private void OnTranscriptionStateChanged()
    {
        OnPropertyChanged(nameof(CanStartRecording));
        StartRecordingCommand.NotifyCanExecuteChanged();
    }

    // ------------------------------------------------------------------ observables
    [ObservableProperty] private string _statusText = "LISTO";
    [ObservableProperty] private string _elapsedText = "00:00:00";
    [ObservableProperty] private double _micLevel;
    [ObservableProperty] private string _micLevelText = "En espera";
    [ObservableProperty] private double _systemLevel;
    [ObservableProperty] private string _systemLevelText = "En espera";
    [ObservableProperty] private string _micPeakHoldText = "Peak −∞ dBFS";
    [ObservableProperty] private string _systemPeakHoldText = "Peak −∞ dBFS";
    [ObservableProperty] private string _micRmsText = "RMS −∞ dBFS";
    [ObservableProperty] private string _systemRmsText = "RMS −∞ dBFS";
    [ObservableProperty] private string _micNoiseFloorText = "Piso (est.) −∞ dBFS";
    [ObservableProperty] private string _systemNoiseFloorText = "Piso (est.) −∞ dBFS";
    [ObservableProperty] private IReadOnlyList<double> _microphoneWaveform = Array.Empty<double>();
    [ObservableProperty] private IReadOnlyList<double> _systemWaveform = Array.Empty<double>();
    [ObservableProperty] private bool _micClipping;
    [ObservableProperty] private bool _systemClipping;
    [ObservableProperty] private bool _micSilenceWarning;
    [ObservableProperty] private bool _systemSilenceWarning;
    [ObservableProperty] private string _captureFaultMessage = string.Empty;
    [ObservableProperty] private string _referenceText = "Llamada";
    [ObservableProperty] private bool _consentGranted;
    [ObservableProperty] private IReadOnlyList<AudioDeviceOption> _microphones = Array.Empty<AudioDeviceOption>();
    [ObservableProperty] private IReadOnlyList<AudioDeviceOption> _outputs = Array.Empty<AudioDeviceOption>();
    [ObservableProperty] private AudioDeviceOption? _selectedMicrophone;
    [ObservableProperty] private AudioDeviceOption? _selectedOutput;
    [ObservableProperty] private string _pauseLabel = "Ⅱ PAUSAR";

    private string? _recordingsDirectory;

    public bool IsRecordingActive => _recording is not null;
    public bool CanStartRecording => !_transcriptionUiState.IsTranscribing && !IsRecordingActive;
    public bool CanStopRecording => IsRecordingActive;
    public bool RecordingControlsEnabled => IsRecordingActive;
    public bool IsSystemLoopbackAvailable => _services.Capabilities.SupportsSystemLoopback;
    public string SystemUnavailableText => IsSystemLoopbackAvailable ? string.Empty : "No disponible en esta plataforma";
    public bool HasCaptureFault => !string.IsNullOrWhiteSpace(CaptureFaultMessage);

    partial void OnCaptureFaultMessageChanged(string value) => OnPropertyChanged(nameof(HasCaptureFault));

    /// <summary>Al estado inicial de todos los controles tras terminar o fallar.</summary>
    private void ResetRecordingState()
    {
        _recording = null;
        ReferenceText = "Llamada";
        ConsentGranted = false;
        ElapsedText = "00:00:00";
        MicLevel = 0;
        SystemLevel = 0;
        MicLevelText = "En espera";
        SystemLevelText = "En espera";
        MicPeakHoldText = "Peak −∞ dBFS";
        SystemPeakHoldText = "Peak −∞ dBFS";
        MicRmsText = "RMS −∞ dBFS";
        SystemRmsText = "RMS −∞ dBFS";
        MicNoiseFloorText = "Piso (est.) −∞ dBFS";
        SystemNoiseFloorText = "Piso (est.) −∞ dBFS";
        MicClipping = false;
        SystemClipping = false;
        MicSilenceWarning = false;
        SystemSilenceWarning = false;
        _microphoneMeter.Reset();
        _systemMeter.Reset();
        _microphoneSilence.Reset();
        _systemSilence.Reset();
        _microphoneNoiseFloor.Reset();
        _systemNoiseFloor.Reset();
        _microphonePeaks.Clear();
        _systemPeaks.Clear();
        OnPropertyChanged(nameof(MicrophoneWaveform));
        OnPropertyChanged(nameof(SystemWaveform));
        StatusText = "LISTO";
        RaiseCanStart();
    }

    // ------------------------------------------------------------------ dispositivos
    [RelayCommand]
    private void RefreshDevices()
    {
        try
        {
            var microphones = _services.Devices.GetCaptureDevices();
            var outputs = _services.Devices.GetOutputDevices();
            Microphones = microphones;
            Outputs = outputs;
            SelectedMicrophone = microphones.FirstOrDefault();
            SelectedOutput = outputs.FirstOrDefault();
            _log.Log($"Dispositivos detectados: {microphones.Count} entradas y {outputs.Count} salidas.");
        }
        catch (Exception ex) { _log.Log("No se pudieron enumerar los dispositivos: " + ex.Message); }
    }

    // ------------------------------------------------------------------ ciclo
    public bool IsPaused => _recording is { } r && r.IsPaused;

    /// <summary>Actualiza cronómetro y niveles de la captura activa; lo llama la vista.</summary>
    public void Tick()
    {
        if (_recording is not { } recording) return;
        var elapsed = recording.Elapsed;
        ElapsedText = $"{(long)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        var microphonePeak = recording.IsPaused ? 0 : recording.MicrophonePeak;
        var systemPeak = recording.IsPaused ? 0 : recording.SystemPeak;
        _microphoneMeter.Update(microphonePeak);
        _systemMeter.Update(systemPeak);
        MicLevel = AudioLevelMath.ToMeterPercent(_microphoneMeter.CurrentDbfs);
        SystemLevel = AudioLevelMath.ToMeterPercent(_systemMeter.CurrentDbfs);
        MicLevelText = AudioLevelMath.FormatDbfs(_microphoneMeter.CurrentDbfs) + " dBFS";
        SystemLevelText = AudioLevelMath.FormatDbfs(_systemMeter.CurrentDbfs) + " dBFS";
        MicPeakHoldText = "Peak " + AudioLevelMath.FormatDbfs(_microphoneMeter.PeakHoldDbfs) + " dBFS";
        SystemPeakHoldText = "Peak " + AudioLevelMath.FormatDbfs(_systemMeter.PeakHoldDbfs) + " dBFS";
        var microphoneRms = recording.IsPaused ? 0 : recording.MicrophoneRms;
        var systemRms = recording.IsPaused ? 0 : recording.SystemRms;
        MicRmsText = "RMS " + AudioLevelMath.FormatDbfs(AudioLevelMath.LinearToDbfs(microphoneRms)) + " dBFS";
        SystemRmsText = "RMS " + AudioLevelMath.FormatDbfs(AudioLevelMath.LinearToDbfs(systemRms)) + " dBFS";
        if (recording.IsPaused)
        {
            _microphoneSilence.Reset();
            _systemSilence.Reset();
            MicSilenceWarning = false;
            SystemSilenceWarning = false;
            StatusText = "PAUSADO";
            MicClipping = _microphoneMeter.IsClipping;
            SystemClipping = _systemMeter.IsClipping;
            return;
        }

        _microphoneNoiseFloor.Add(microphoneRms);
        _systemNoiseFloor.Add(systemRms);
        MicNoiseFloorText = "Piso (est.) " + AudioLevelMath.FormatDbfs(_microphoneNoiseFloor.EstimateDbfs) + " dBFS";
        SystemNoiseFloorText = "Piso (est.) " + AudioLevelMath.FormatDbfs(_systemNoiseFloor.EstimateDbfs) + " dBFS";
        _microphonePeaks.Append(microphonePeak);
        _systemPeaks.Append(systemPeak);
        MicrophoneWaveform = _microphonePeaks.Snapshot();
        SystemWaveform = _systemPeaks.Snapshot();
        MicClipping = _microphoneMeter.IsClipping;
        SystemClipping = _systemMeter.IsClipping;
        MicSilenceWarning = _microphoneSilence.Update(microphonePeak, recording: true, paused: false);
        SystemSilenceWarning = _systemSilence.Update(systemPeak, recording: true, paused: false);
        StatusText = "GRABANDO";
    }

    [RelayCommand]
    private void ResetMicrophoneClipping()
    {
        _microphoneMeter.ResetClipping();
        MicClipping = false;
    }

    [RelayCommand]
    private void ResetSystemClipping()
    {
        _systemMeter.ResetClipping();
        SystemClipping = false;
    }

    /// <summary>Configura la carpeta de destino validada por la vista (Settings) antes de arrancar.</summary>
    public void SetRecordingsDirectory(string? directory) => _recordingsDirectory = directory?.Trim();

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private void StartRecording()
    {
        if (_recording is not null) return;
        if (_transcriptionUiState.IsTranscribing)
        {
            _log.Log("Grabación bloqueada: hay una transcripción en curso.");
            return;
        }
        if (!ConsentGranted)
        {
            StatusText = "CONSENTIMIENTO REQUERIDO";
            _log.Log("Inicio bloqueado: falta confirmar autorización de todas las personas.");
            return;
        }
        if (SelectedMicrophone is null || SelectedOutput is null)
        {
            StatusText = "SELECCIONA DISPOSITIVOS";
            return;
        }
        var recordingsDirectory = _recordingsDirectory;
        if (string.IsNullOrWhiteSpace(recordingsDirectory) || !Directory.Exists(recordingsDirectory))
        {
            StatusText = "CONFIGURA CARPETA";
            _log.Log("Inicio bloqueado: la carpeta de grabaciones no está configurada o no existe.");
            return;
        }

        try
        {
            var directory = RecordingDirectoryFactory.Create(recordingsDirectory, ReferenceText ?? string.Empty, _services.Clock);
            _services.Security.RestrictSessionDirectory(directory);
            var session = _services.CaptureFactory.Create(directory, SelectedMicrophone.Id, SelectedOutput.Id);
            session.Faulted += ex => _uiDispatch(() =>
            {
                StatusText = "ERROR DE GRABACIÓN";
                CaptureFaultMessage = "Se cerraron ambas pistas; la sesión quedó marcada como incompleta. " + ex.Message;
                _log.Log("Fallo de captura: " + ex.Message);
                Faulted?.Invoke(ex);
            });
            try { session.Start(); _recording = session; _activeSessionDirectory = directory; }
            catch { session.Dispose(); throw; }

            CaptureFaultMessage = string.Empty;
            PauseLabel = "Ⅱ PAUSAR";
            StatusText = "GRABANDO";
            RaiseCanStart();
            _log.Log("Grabación iniciada. Micrófono: " + session.MicrophoneDeviceName + "; salida: " + session.SystemDeviceName);
            _log.Log("Guardando pistas en: " + directory + (_services.Capabilities.SupportsEncryptionAtRest
                ? " (cifradas)" : " (SIN cifrado real: modo desarrollo)"));
        }
        catch (Exception ex)
        {
            StatusText = "ERROR";
            _log.Log("No se pudo iniciar: " + ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private void PauseRecording()
    {
        if (_recording is not { } recording) return;
        try
        {
            if (recording.IsPaused)
            {
                recording.Resume();
                PauseLabel = "Ⅱ PAUSAR";
                StatusText = "GRABANDO";
            }
            else
            {
                recording.Pause();
                PauseLabel = "▶ REANUDAR";
                StatusText = "PAUSADO";
            }
        }
        catch (Exception ex) { _log.Log("No se pudo cambiar el estado: " + ex.Message); }
    }

    /// <summary>Detiene la captura de forma coordinada guardando ambas pistas.</summary>
    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private async Task StopRecordingAsync()
    {
        var sessionDirectory = _activeSessionDirectory;
        if (!await StopForCloseAsync()) return;
        _activeSessionDirectory = null;
        if (sessionDirectory is not null && RecordingStoppedSuccessfully is { } stopped)
            await stopped(sessionDirectory);
    }

    /// <summary>Stops and closes both tracks; false means the caller must keep the window open.</summary>
    public Task<bool> StopForCloseAsync()
    {
        if (_stopTask is not null) return _stopTask;
        if (_recording is not { } recording) return Task.FromResult(true);
        _log.Log("Cerrando ambas pistas...");
        return _stopTask = StopForCloseCoreAsync(recording);
    }

    /// <summary>
    /// Parada sincrónica e inmediata de la captura activa (pistas cerradas
    /// coordinadamente): la usa el cierre de la ventana, donde ya no hay
    /// oportunidad de esperar promesas.
    /// </summary>
    public void StopForClose()
    {
        var recording = _recording;
        if (recording is null) return;
        try
        {
            recording.Stop();
            _log.Log("Grabación finalizada al cerrar la ventana. Ambas pistas quedaron cerradas.");
        }
        catch (Exception ex) { _log.Log("Error al cerrar captura: " + ex.Message); }
        finally
        {
            recording.Dispose();
            _recording = null;
            ResetRecordingState();
            RecordingEnded?.Invoke();
        }
    }

    private async Task<bool> StopForCloseCoreAsync(IAudioCaptureSession recording)
    {
        var stopped = false;
        try
        {
            await Task.Run(recording.Stop);
            stopped = true;
            _log.Log("Grabación finalizada. Ambas pistas quedaron cerradas.");
        }
        catch (Exception ex) { _log.Log("Error al finalizar ambas pistas: " + ex.Message); }

        try { recording.Dispose(); }
        catch (Exception ex)
        {
            stopped = false;
            _log.Log("Error al liberar la captura: " + ex.Message);
        }
        finally
        {
            _recording = null;
            _uiDispatch(() =>
            {
                ResetRecordingState();
                RecordingEnded?.Invoke();
            });
            _stopTask = null;
        }
        return stopped;
    }
}

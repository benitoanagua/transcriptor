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
    private readonly PeakHistory _microphonePeaks = new(48);
    private readonly PeakHistory _systemPeaks = new(48);
    private IAudioCaptureSession? _recording;
    private IAudioLevelMonitor? _levelMonitor;
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
    public event Func<string, Task>? RecordingFailed;

    private void RaiseCanStart()
    {
        OnPropertyChanged(nameof(IsRecordingActive));
        OnPropertyChanged(nameof(IsAudioActive));
        OnPropertyChanged(nameof(CanStartRecording));
        OnPropertyChanged(nameof(CanStopRecording));
        OnPropertyChanged(nameof(RecordingControlsEnabled));
        OnPropertyChanged(nameof(CanRecordNow));
        OnPropertyChanged(nameof(NeedsConsentHelp));
        StartRecordingCommand.NotifyCanExecuteChanged();
        PauseRecordingCommand.NotifyCanExecuteChanged();
        StopRecordingCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartLevelTesting));
        StartLevelTestingCommand.NotifyCanExecuteChanged();
        RecordingStateChanged?.Invoke();
    }

    private void OnTranscriptionStateChanged()
    {
        OnPropertyChanged(nameof(CanStartRecording));
        OnPropertyChanged(nameof(CanStartLevelTesting));
        OnPropertyChanged(nameof(CanRecordNow));
        OnPropertyChanged(nameof(NeedsConsentHelp));
        StartRecordingCommand.NotifyCanExecuteChanged();
        StartLevelTestingCommand.NotifyCanExecuteChanged();
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
    [ObservableProperty] private string _pauseLabel = "Pausar";

    private string? _recordingsDirectory;

    public bool IsRecordingActive => _recording is not null;
    public bool IsTestingLevels => _levelMonitor is not null;
    public bool IsAudioActive => IsRecordingActive || IsTestingLevels;
    public bool CanStartRecording => !_transcriptionUiState.IsTranscribing && !IsRecordingActive;
    public bool CanRecordNow => CanStartRecording && ConsentGranted;
    public bool NeedsConsentHelp => !ConsentGranted && !IsRecordingActive;
    public bool CanStartLevelTesting => _services.LevelMonitorFactory is not null
        && !_transcriptionUiState.IsTranscribing && !IsRecordingActive && !IsTestingLevels;
    public bool CanStopLevelTesting => IsTestingLevels;
    public bool CanStopRecording => IsRecordingActive;
    public bool RecordingControlsEnabled => IsRecordingActive;
    public string? ActiveSessionDirectory => _activeSessionDirectory;
    public bool IsSystemLoopbackAvailable => _services.Capabilities.SupportsSystemLoopback;
    public bool HasMicrophone => SelectedMicrophone is not null;
    public bool IsSystemCaptureAvailable => IsSystemLoopbackAvailable && SelectedOutput is not null;
    public string MicrophoneStatusText => HasMicrophone ? "Micrófono disponible" : "Sin micrófono";
    public string LoopbackStatusText => IsSystemCaptureAvailable
        ? "Salida del sistema disponible"
        : IsSystemLoopbackAvailable ? "Sin dispositivo de salida" : "Loopback no disponible en esta plataforma";
    public string MicPeakValueText => MicPeakHoldText["Peak ".Length..];
    public string SystemPeakValueText => SystemPeakHoldText["Peak ".Length..];
    public string MicRmsValueText => MicRmsText["RMS ".Length..];
    public string SystemRmsValueText => SystemRmsText["RMS ".Length..];
    public string CaptureStatusLabel => StatusText switch
    {
        "LISTO" => "Listo para grabar",
        "GRABANDO" => "Grabando",
        "PAUSADO" => "En pausa",
        "PROBANDO NIVELES" => "Probando niveles",
        "CONSENTIMIENTO REQUERIDO" => "Falta tu autorización",
        "SELECCIONA DISPOSITIVOS" => "Selecciona los dispositivos",
        "CONFIGURA CARPETA" => "Configura la carpeta de grabaciones",
        "ERROR DE GRABACIÓN" => "Se detuvo la captura",
        "ERROR" => "Error",
        _ => StatusText
    };
    public string SystemUnavailableText => IsSystemCaptureAvailable
        ? string.Empty
        : IsSystemLoopbackAvailable ? "Selecciona un dispositivo de salida" : "No disponible en esta plataforma";
    public bool HasCaptureFault => !string.IsNullOrWhiteSpace(CaptureFaultMessage);

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(CaptureStatusLabel));
        // El estado de pausa se lee de la sesión: se avisa junto al rótulo para que
        // la luz de estado y los controles reaccionen en el mismo ciclo.
        OnPropertyChanged(nameof(IsPaused));
    }
    partial void OnConsentGrantedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRecordNow));
        OnPropertyChanged(nameof(NeedsConsentHelp));
    }
    partial void OnMicPeakHoldTextChanged(string value) => OnPropertyChanged(nameof(MicPeakValueText));
    partial void OnSystemPeakHoldTextChanged(string value) => OnPropertyChanged(nameof(SystemPeakValueText));
    partial void OnMicRmsTextChanged(string value) => OnPropertyChanged(nameof(MicRmsValueText));
    partial void OnSystemRmsTextChanged(string value) => OnPropertyChanged(nameof(SystemRmsValueText));
    partial void OnCaptureFaultMessageChanged(string value) => OnPropertyChanged(nameof(HasCaptureFault));
    partial void OnSelectedMicrophoneChanged(AudioDeviceOption? value)
    {
        OnPropertyChanged(nameof(HasMicrophone));
        OnPropertyChanged(nameof(MicrophoneStatusText));
    }
    partial void OnSelectedOutputChanged(AudioDeviceOption? value)
    {
        OnPropertyChanged(nameof(IsSystemCaptureAvailable));
        OnPropertyChanged(nameof(LoopbackStatusText));
        OnPropertyChanged(nameof(SystemUnavailableText));
    }

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
            if (IsTestingLevels) StopLevelTesting();
            var microphones = _services.Devices.GetCaptureDevices();
            var outputs = _services.Devices.GetOutputDevices();
            Microphones = microphones;
            Outputs = outputs;
            SelectedMicrophone = microphones.Count > 0 ? microphones[0] : null;
            SelectedOutput = outputs.Count > 0 ? outputs[0] : null;
            _log.Log($"Dispositivos detectados: {microphones.Count} entradas y {outputs.Count} salidas.");
        }
        catch (Exception ex) { _log.Log("No se pudieron enumerar los dispositivos: " + ex.Message); }
    }

    // ------------------------------------------------------------------ ciclo
    public bool IsPaused => _recording is { } r && r.IsPaused;

    /// <summary>Actualiza cronómetro y niveles de grabación o monitoreo temporal.</summary>
    public void Tick()
    {
        var recording = _recording;
        var monitor = _levelMonitor;
        if (recording is null && monitor is null) return;

        var isPaused = recording?.IsPaused ?? false;
        if (recording is not null)
        {
            var elapsed = recording.Elapsed;
            ElapsedText = $"{(long)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        var microphonePeak = isPaused ? 0 : recording?.MicrophonePeak ?? monitor?.MicrophonePeak ?? 0;
        var systemPeak = isPaused ? 0 : recording?.SystemPeak ?? monitor?.SystemPeak ?? 0;
        _microphoneMeter.Update(microphonePeak);
        _systemMeter.Update(systemPeak);
        MicLevel = AudioLevelMath.ToMeterPercent(_microphoneMeter.CurrentDbfs);
        SystemLevel = AudioLevelMath.ToMeterPercent(_systemMeter.CurrentDbfs);
        MicLevelText = AudioLevelMath.FormatDbfs(_microphoneMeter.CurrentDbfs) + " dBFS";
        SystemLevelText = AudioLevelMath.FormatDbfs(_systemMeter.CurrentDbfs) + " dBFS";
        MicPeakHoldText = "Peak " + AudioLevelMath.FormatDbfs(_microphoneMeter.PeakHoldDbfs) + " dBFS";
        SystemPeakHoldText = "Peak " + AudioLevelMath.FormatDbfs(_systemMeter.PeakHoldDbfs) + " dBFS";
        var microphoneRms = isPaused ? 0 : recording?.MicrophoneRms ?? monitor?.MicrophoneRms ?? 0;
        var systemRms = isPaused ? 0 : recording?.SystemRms ?? monitor?.SystemRms ?? 0;
        MicRmsText = "RMS " + AudioLevelMath.FormatDbfs(AudioLevelMath.LinearToDbfs(microphoneRms)) + " dBFS";
        SystemRmsText = "RMS " + AudioLevelMath.FormatDbfs(AudioLevelMath.LinearToDbfs(systemRms)) + " dBFS";
        if (isPaused)
        {
            _microphonePeaks.Append(0);
            _systemPeaks.Append(0);
            MicrophoneWaveform = _microphonePeaks.Snapshot();
            SystemWaveform = _systemPeaks.Snapshot();
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
        _microphonePeaks.Append(MicLevel / 100d);
        _systemPeaks.Append(SystemLevel / 100d);
        MicrophoneWaveform = _microphonePeaks.Snapshot();
        SystemWaveform = _systemPeaks.Snapshot();
        MicClipping = _microphoneMeter.IsClipping;
        SystemClipping = _systemMeter.IsClipping;
        MicSilenceWarning = _microphoneSilence.Update(microphonePeak, recording: recording is not null, paused: false);
        SystemSilenceWarning = _systemSilence.Update(systemPeak, recording: recording is not null, paused: false);
        StatusText = recording is null ? "PROBANDO NIVELES" : "GRABANDO";
    }

    [RelayCommand(CanExecute = nameof(CanStartLevelTesting))]
    private void StartLevelTesting()
    {
        if (_levelMonitor is not null || !CanStartLevelTesting) return;
        if (_services.LevelMonitorFactory is not { } factory) return;
        StopLevelTesting();
        IAudioLevelMonitor? monitor = null;
        try
        {
            monitor = factory.Create(
                SelectedMicrophone?.Id, SelectedOutput?.Id, IsSystemLoopbackAvailable);
            monitor.Faulted += OnLevelMonitorFaulted;
            monitor.Start();
            _levelMonitor = monitor;
            LevelTestStatusText = "Midiendo en vivo. No se crea sesión ni se guarda audio.";
            OnPropertyChanged(nameof(IsTestingLevels));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(CanStartLevelTesting));
            OnPropertyChanged(nameof(CanStopLevelTesting));
            StartLevelTestingCommand.NotifyCanExecuteChanged();
            StopLevelTestingCommand.NotifyCanExecuteChanged();
            StatusText = "PROBANDO NIVELES";
        }
        catch (Exception ex)
        {
            LevelTestStatusText = "No se pudo iniciar la prueba de niveles.";
            _log.Log("No se pudo iniciar la prueba de niveles: " + ex.Message);
            if (monitor is not null)
            {
                monitor.Faulted -= OnLevelMonitorFaulted;
                try { monitor.Stop(); }
                catch (Exception stopException)
                {
                    _log.Log("No se pudo detener el monitoreo tras un fallo de inicio: " + stopException.Message);
                }
                finally
                {
                    try { monitor.Dispose(); }
                    catch (Exception disposeException)
                    {
                        _log.Log("No se pudo liberar el monitoreo tras un fallo de inicio: " + disposeException.Message);
                    }
                }
            }
            OnPropertyChanged(nameof(IsTestingLevels));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(CanStartLevelTesting));
            OnPropertyChanged(nameof(CanStopLevelTesting));
            StartLevelTestingCommand.NotifyCanExecuteChanged();
            StopLevelTestingCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopLevelTesting))]
    private void StopLevelTesting()
    {
        var monitor = _levelMonitor;
        _levelMonitor = null;
        if (monitor is not null)
        {
            monitor.Faulted -= OnLevelMonitorFaulted;
            try { monitor.Stop(); }
            catch (Exception ex)
            {
                _log.Log("No se pudo detener correctamente la prueba de niveles: " + ex.Message);
            }
            finally
            {
                try { monitor.Dispose(); }
                catch (Exception ex) { _log.Log("No se pudo liberar la prueba de niveles: " + ex.Message); }
            }
        }
        if (monitor is not null)
            LevelTestStatusText = "Prueba de niveles detenida. No se guardó audio.";
        MicLevel = 0;
        SystemLevel = 0;
        MicLevelText = "En espera";
        SystemLevelText = "En espera";
        MicRmsText = "RMS −∞ dBFS";
        SystemRmsText = "RMS −∞ dBFS";
        _microphoneMeter.Reset();
        _systemMeter.Reset();
        _microphoneNoiseFloor.Reset();
        _systemNoiseFloor.Reset();
        _microphonePeaks.Clear();
        _systemPeaks.Clear();
        MicrophoneWaveform = _microphonePeaks.Snapshot();
        SystemWaveform = _systemPeaks.Snapshot();
        MicPeakHoldText = "Peak −∞ dBFS";
        SystemPeakHoldText = "Peak −∞ dBFS";
        MicNoiseFloorText = "Piso (est.) −∞ dBFS";
        SystemNoiseFloorText = "Piso (est.) −∞ dBFS";
        MicClipping = false;
        SystemClipping = false;
        MicSilenceWarning = false;
        SystemSilenceWarning = false;
        if (_recording is null)
            StatusText = "LISTO";
        OnPropertyChanged(nameof(IsTestingLevels));
        OnPropertyChanged(nameof(IsAudioActive));
        OnPropertyChanged(nameof(CanStartLevelTesting));
        OnPropertyChanged(nameof(CanStopLevelTesting));
        StartLevelTestingCommand.NotifyCanExecuteChanged();
        StopLevelTestingCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] private string _levelTestStatusText = string.Empty;
    public bool HasLevelTestStatus => !string.IsNullOrWhiteSpace(LevelTestStatusText);
    partial void OnLevelTestStatusTextChanged(string value) => OnPropertyChanged(nameof(HasLevelTestStatus));

    private void OnLevelMonitorFaulted(Exception exception) => _uiDispatch(() =>
    {
        _log.Log("Fallo en monitoreo de niveles: " + exception.Message);
        StopLevelTesting();
        LevelTestStatusText = "Se perdió el dispositivo de audio durante la prueba.";
    });

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

        StopLevelTesting();
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
                _ = FinalizeFaultedRecordingAsync(session, directory);
            });
            try { session.Start(); _recording = session; _activeSessionDirectory = directory; }
            catch { session.Dispose(); throw; }
            OnPropertyChanged(nameof(ActiveSessionDirectory));

            CaptureFaultMessage = string.Empty;
            PauseLabel = "Pausar";
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
                PauseLabel = "Pausar";
                StatusText = "GRABANDO";
            }
            else
            {
                recording.Pause();
                PauseLabel = "Reanudar";
                StatusText = "PAUSADO";
            }
        }
        catch (Exception ex) { _log.Log("No se pudo cambiar el estado: " + ex.Message); }
    }

    private async Task FinalizeFaultedRecordingAsync(IAudioCaptureSession session, string sessionDirectory)
    {
        // The platform adapter closes both tracks and writes the incomplete marker.
        // Await its stop/dispose path before refreshing the library, otherwise the
        // session could briefly appear complete or remain locked as an active capture.
        if (_recording is not null)
        {
            if (!ReferenceEquals(_recording, session)) return;
            await StopForCloseAsync();
        }
        // Start() can itself fail after the platform adapter has marked the
        // directory incomplete but before the session is assigned to _recording.
        // In that case StartRecording disposes the session in its catch block;
        // still publish the already-finalized directory to the library.
        StatusText = "ERROR DE GRABACIÓN";
        if (RecordingFailed is { } failed)
            await failed(sessionDirectory);
    }

    /// <summary>Detiene la captura de forma coordinada guardando ambas pistas.</summary>
    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private async Task StopRecordingAsync()
    {
        var sessionDirectory = _activeSessionDirectory;
        if (!await StopForCloseAsync()) return;
        _activeSessionDirectory = null;
        OnPropertyChanged(nameof(ActiveSessionDirectory));
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

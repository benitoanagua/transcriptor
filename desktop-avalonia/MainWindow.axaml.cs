using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Transcriptor.Rec;
using Transcriptor.Transcription;

namespace Transcriptor.Desktop;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly IClock _clock = SystemClock.Instance;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private AppSettings _settings;
    private RecordingSession? _recording;

    public MainWindow()
    {
        InitializeComponent();
        var loaded = _settingsStore.Load();
        _settings = loaded.Settings;
        RecordingsPathBox.Text = _settings.RecordingsDirectory;
        WhisperExecutableBox.Text = _settings.WhisperExecutablePath;
        WhisperModelBox.Text = _settings.WhisperModelPath;
        WhisperLanguageBox.Text = _settings.Language;
        if (!string.IsNullOrWhiteSpace(loaded.Warning)) Log(loaded.Warning);
        Log("Configuración cargada desde " + _settingsStore.SettingsPath);
        _timer.Tick += (_, _) => RefreshRecordingStatus();
        RefreshAudioDevices();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private void RefreshDevices_Click(object? sender, RoutedEventArgs e) => RefreshAudioDevices();
    private void SaveSettings_Click(object? sender, RoutedEventArgs e) => SaveSettings();

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
            MicrophoneDeviceId = (MicrophoneDeviceBox.SelectedItem as AudioDeviceOption)?.Id,
            OutputDeviceId = (OutputDeviceBox.SelectedItem as AudioDeviceOption)?.Id
        };
        _settingsStore.Save(_settings);
        Log("Configuración guardada.");
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

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogBox.Text = string.IsNullOrEmpty(LogBox.Text) ? line : LogBox.Text + Environment.NewLine + line;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        if (_recording is { } recording)
        {
            try { recording.Stop(); }
            catch (Exception ex) { Log("Error al cerrar captura: " + ex.Message); }
            finally { recording.Dispose(); _recording = null; }
        }
        base.OnClosed(e);
    }
}
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transcriptor.Transcription;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Lector de transcripciones y controlador de transcripción offline. Los
/// segmentos se cargan desde archivos locales (descifrados solo en memoria);
/// el bloqueo compartido impide operaciones incompatibles.
/// </summary>
public sealed partial class TranscriptViewModel : ObservableObject
{
    private const long MaximumTranscriptBytes = 64L * 1024 * 1024;
    private readonly UiServices _services;
    private readonly TranscriptionUiState _transcriptionUiState;
    private readonly ILogSink _log;
    private readonly IUiHost _host;
    private readonly Action<Action> _uiDispatch;
    private CancellationTokenSource? _transcriptionCancellation;
    private TaskCompletionSource? _transcriptionFinished;
    private LibraryViewModel? _library;
    private int _loadVersion;
    private bool _syncingActiveSegment;

    public TranscriptViewModel(
        UiServices services, TranscriptionUiState transcriptionUiState, ILogSink log,
        IUiHost host, Action<Action> uiDispatch)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _transcriptionUiState = transcriptionUiState ?? throw new ArgumentNullException(nameof(transcriptionUiState));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
        _transcriptionUiState.Changed += OnTranscriptionStateChanged;
    }

    public event Action? Finished;

    /// <summary>Snapshot de rutas conectado por el shell (Settings).</summary>
    public Func<(string Root, string Executable, string Model, string Language)> PathsSnapshot { get; set; } =
        () => (string.Empty, string.Empty, string.Empty, "es");

    /// <summary>Indica si hay una grabación activa (bloqueo recíproco).</summary>
    public Func<bool> IsRecordingActive { get; set; } = () => false;
    public Action<TimeSpan>? SeekTo { get; set; }

    public void AttachLibrary(LibraryViewModel library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        library.SelectionChanged += OnLibrarySelectionChanged;
        OnLibrarySelectionChanged(library.SelectedRecording);
    }

    // ------------------------------------------------------------------ estado
    [ObservableProperty] private string _transcriptionProgressText = "Configura Whisper en AJUSTES para transcribir";
    [ObservableProperty] private IReadOnlyList<TranscriptSegmentViewModel> _filteredSegments = Array.Empty<TranscriptSegmentViewModel>();
    [ObservableProperty] private TranscriptSegmentViewModel? _selectedSegment;
    [ObservableProperty] private TranscriptSegmentViewModel? _activeSegment;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _transcriptEmptyText = "Selecciona una sesión para consultar su transcripción.";
    [ObservableProperty] private string _searchCountText = "Sin coincidencias";
    [ObservableProperty] private bool _isLoadingTranscript;
    [ObservableProperty] private bool _autoScrollEnabled = true;

    public ObservableCollection<TranscriptSegmentViewModel> Segments { get; } = [];
    public bool IsTranscribing => _transcriptionUiState.IsTranscribing;
    public bool IsCloseBlocked => _transcriptionCancellation is not null;
    public bool CanCancelTranscription => _transcriptionCancellation is not null;
    public bool CanTranscribePending => !_transcriptionUiState.IsTranscribing && !IsRecordingActive();
    public bool CanTranscribeSelected => CanTranscribePending
        && _library?.SelectedRecording is { IsIncomplete: false };

    private void OnLibrarySelectionChanged(LibraryEntry? entry)
    {
        OnPropertyChanged(nameof(CanTranscribeSelected));
        TranscribeSelectedCommand.NotifyCanExecuteChanged();
        _ = LoadTranscriptAsync(entry);
    }
    public bool HasVisibleSegments => FilteredSegments.Count > 0;

    private void OnTranscriptionStateChanged()
    {
        OnPropertyChanged(nameof(IsTranscribing));
        OnPropertyChanged(nameof(CanTranscribePending));
        OnPropertyChanged(nameof(CanTranscribeSelected));
        TranscribePendingCommand.NotifyCanExecuteChanged();
        TranscribeSelectedCommand.NotifyCanExecuteChanged();
    }

    public void NotifyRecordingStateChanged()
    {
        OnPropertyChanged(nameof(CanTranscribePending));
        OnPropertyChanged(nameof(CanTranscribeSelected));
        TranscribePendingCommand.NotifyCanExecuteChanged();
        TranscribeSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => ApplyTranscriptSearch();

    partial void OnSelectedSegmentChanged(TranscriptSegmentViewModel? value)
    {
        if (!_syncingActiveSegment && value is not null)
            SeekTo?.Invoke(value.Segment.Start);
    }

    public void UpdatePlaybackPosition(TimeSpan position)
    {
        TranscriptSegmentViewModel? active = null;
        foreach (var segment in Segments)
        {
            var isActive = position >= segment.Segment.Start && position < segment.Segment.End;
            segment.IsActive = isActive;
            if (isActive) active = segment;
        }

        if (!AutoScrollEnabled || ReferenceEquals(active, ActiveSegment)) return;
        _syncingActiveSegment = true;
        try
        {
            ActiveSegment = active;
            SelectedSegment = active;
        }
        finally { _syncingActiveSegment = false; }
    }

    private async Task LoadTranscriptAsync(LibraryEntry? entry)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        IsLoadingTranscript = true;
        Segments.Clear();
        FilteredSegments = Array.Empty<TranscriptSegmentViewModel>();
        OnPropertyChanged(nameof(HasVisibleSegments));
        SelectedSegment = null;
        ActiveSegment = null;
        TranscriptEmptyText = entry is null ? "Selecciona una sesión para consultar su transcripción." : "Buscando transcripción…";
        try
        {
            var parsed = entry is null
                ? Array.Empty<TranscriptSegment>()
                : await Task.Run(() => ReadTranscript(entry));
            if (version != Volatile.Read(ref _loadVersion)) return;
            foreach (var segment in parsed) Segments.Add(new TranscriptSegmentViewModel(segment));
            ApplyTranscriptSearch();
            if (Segments.Count == 0) TranscriptEmptyText = "Esta sesión todavía no tiene una transcripción.";
            OnPropertyChanged(nameof(HasVisibleSegments));
            _log.Log(Segments.Count == 0
                ? "No hay transcripción para la sesión seleccionada."
                : $"Transcripción cargada: {Segments.Count} segmento(s).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException or ArgumentException)
        {
            if (version != Volatile.Read(ref _loadVersion)) return;
            TranscriptEmptyText = "No se pudo leer la transcripción. Consulta el registro.";
            _log.Log("No se pudo cargar la transcripción: " + ex.Message);
        }
        finally
        {
            if (version == Volatile.Read(ref _loadVersion)) IsLoadingTranscript = false;
        }
    }

    private static IReadOnlyList<TranscriptSegment> ReadTranscript(LibraryEntry entry)
    {
        var directory = entry.DirectoryPath;
        var combined = ReadFirstExisting(directory, "transcripcion.srt.trn", "transcripcion.srt");
        if (combined is not null) return SrtParser.Parse(combined);

        var microphone = ReadFirstExisting(directory, "yo.srt.trn", "yo.srt");
        var system = ReadFirstExisting(directory, "cliente.srt.trn", "cliente.srt");
        return (microphone is null ? [] : SrtParser.Parse(microphone, "Yo"))
            .Concat(system is null ? [] : SrtParser.Parse(system, "Cliente"))
            .OrderBy(segment => segment.Start)
            .ThenBy(segment => segment.Speaker, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ReadFirstExisting(string directory, params string[] fileNames)
    {
        foreach (var name in fileNames)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) continue;
            var info = new FileInfo(path);
            if (info.Length > MaximumTranscriptBytes)
                throw new InvalidDataException($"La transcripción supera el límite de {MaximumTranscriptBytes} bytes.");

            if (!path.EndsWith(".trn", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllText(path, Encoding.UTF8);
            using var input = File.OpenRead(path);
            using var output = new MemoryStream();
            EncryptedFileContainer.Decrypt(input, output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        return null;
    }

    private void ApplyTranscriptSearch()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        foreach (var segment in Segments)
            segment.IsMatch = query.Length > 0 && segment.Segment.Text.Contains(query, StringComparison.OrdinalIgnoreCase);
        var results = query.Length == 0
            ? Segments.ToArray()
            : Segments.Where(segment => segment.IsMatch).ToArray();
        FilteredSegments = results;
        OnPropertyChanged(nameof(HasVisibleSegments));
        if (Segments.Count > 0 && results.Length == 0)
            TranscriptEmptyText = "No hay segmentos que coincidan con la búsqueda.";
        else if (Segments.Count > 0)
            TranscriptEmptyText = string.Empty;
        SearchCountText = query.Length == 0 ? $"{Segments.Count} segmento(s)" : $"{results.Length} coincidencia(s)";
    }

    [RelayCommand]
    private void NextMatch() => SelectAdjacentMatch(forward: true);

    [RelayCommand]
    private void PreviousMatch() => SelectAdjacentMatch(forward: false);

    private void SelectAdjacentMatch(bool forward)
    {
        var matches = Segments.Where(segment => segment.IsMatch).ToArray();
        if (matches.Length == 0) return;
        var current = Array.IndexOf(matches, SelectedSegment);
        var next = current < 0
            ? (forward ? 0 : matches.Length - 1)
            : (current + (forward ? 1 : matches.Length - 1)) % matches.Length;
        SelectedSegment = matches[next];
    }

    [RelayCommand]
    private async Task CopySelectedSegmentsAsync()
    {
        if (SelectedSegment is null) return;
        var text = $"{SelectedSegment.Segment.Speaker}: {SelectedSegment.Segment.Text}";
        if (!await _host.CopyToClipboardAsync(text))
            _log.Log("No hay portapapeles disponible en esta plataforma.");
    }

    [RelayCommand]
    private async Task ExportTranscriptAsync(string? format)
    {
        if (Segments.Count == 0 || format is not ("srt" or "txt" or "md")) return;
        if (!await _host.ConfirmAsync(
            "Exportar transcripción en claro",
            "El archivo exportado quedará en claro y fuera del contenedor cifrado de la sesión. ¿Continuar?"))
            return;

        var path = await _host.PickSaveFileAsync($"Exportar transcripción .{format}", $"*.{format}");
        if (path is null) return;
        var content = format switch
        {
            "srt" => FormatSrt(),
            "txt" => FormatText(),
            "md" => FormatMarkdown(),
            _ => throw new InvalidOperationException("Formato de exportación no reconocido.")
        };
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _log.Log($"Transcripción exportada en claro: {path}");
    }

    private string FormatSrt() => string.Join(Environment.NewLine + Environment.NewLine,
        Segments.Select((item, index) =>
            $"{index + 1}{Environment.NewLine}{FormatTimestamp(item.Segment.Start)} --> {FormatTimestamp(item.Segment.End)}{Environment.NewLine}{item.Segment.Speaker}: {item.Segment.Text}"))
        + Environment.NewLine;

    private string FormatText() => string.Join(Environment.NewLine, Segments.Select(item =>
        $"[{item.Segment.Start:hh\\:mm\\:ss}] {item.Segment.Speaker}: {item.Segment.Text}"));

    private string FormatMarkdown() => string.Join(Environment.NewLine, Segments.Select(item =>
        $"- **{item.Segment.Speaker}** ({item.Segment.Start:hh\\:mm\\:ss}–{item.Segment.End:hh\\:mm\\:ss}): {item.Segment.Text}"));

    private static string FormatTimestamp(TimeSpan time) =>
        $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";

    [RelayCommand(CanExecute = nameof(CanTranscribePending))]
    private Task TranscribePendingAsync() =>
        RunTranscriptionAsync((engine, root, progress, token) => engine.ProcessPendingCallsAsync(root, progress, token), "sesiones pendientes");

    [RelayCommand(CanExecute = nameof(CanTranscribeSelected))]
    private Task TranscribeSelectedAsync()
    {
        var entry = _library?.SelectedRecording;
        if (entry is null) return Task.CompletedTask;
        if (entry.IsIncomplete)
        {
            _log.Log("La sesión seleccionada está incompleta y no se puede transcribir.");
            return Task.CompletedTask;
        }
        return RunTranscriptionAsync(
            (engine, root, progress, token) => engine.ProcessSessionAsync(root, entry.DirectoryPath, progress, token),
            entry.Title);
    }

    private async Task RunTranscriptionAsync(
        Func<TranscriptionEngine, string, IProgress<string>, CancellationToken, Task<int>> process,
        string operation)
    {
        if (IsRecordingActive())
        {
            _log.Log("Transcripción bloqueada: primero finaliza la grabación activa.");
            return;
        }
        if (!_transcriptionUiState.TryBegin()) return;

        var (root, executable, model, language) = PathsSnapshot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)
            || string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(model))
        {
            _transcriptionUiState.Complete();
            TranscriptionProgressText = "Configura una carpeta existente, whisper-cli y el modelo en AJUSTES.";
            _log.Log("Transcripción no iniciada: faltan rutas válidas de grabaciones, Whisper o modelo.");
            return;
        }

        _transcriptionCancellation = new CancellationTokenSource();
        _transcriptionFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OnPropertyChanged(nameof(CanCancelTranscription));
        CancelTranscriptionCommand.NotifyCanExecuteChanged();
        TranscriptionProgressText = "Preparando transcripción…";
        _log.Log($"Iniciando transcripción local de {operation}.");
        var progress = new Progress<string>(message => _uiDispatch(() =>
        {
            TranscriptionProgressText = message;
            _log.Log(message);
        }));
        try
        {
            var engine = _services.CreateTranscriptionEngine(executable, model, language);
            var processed = await process(engine, root, progress, _transcriptionCancellation.Token);
            TranscriptionProgressText = $"Proceso finalizado. Sesiones transcritas: {processed}.";
            _log.Log($"Transcripción finalizada. Sesiones procesadas: {processed}.");
        }
        catch (OperationCanceledException)
        {
            TranscriptionProgressText = "Transcripción cancelada. Los originales se conservaron.";
            _log.Log("Transcripción cancelada por el usuario.");
        }
        catch (Exception ex)
        {
            TranscriptionProgressText = "La transcripción terminó con errores. Consulta el registro.";
            _log.Log("Error de transcripción: " + ex.Message);
        }
        finally
        {
            _transcriptionFinished.TrySetResult();
            _transcriptionFinished = null;
            _transcriptionCancellation.Dispose();
            _transcriptionCancellation = null;
            OnPropertyChanged(nameof(CanCancelTranscription));
            CancelTranscriptionCommand.NotifyCanExecuteChanged();
            _transcriptionUiState.Complete();
            _library?.RefreshLibraryCommand.Execute(null);
            Finished?.Invoke();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelTranscription))]
    private void CancelTranscription() => _transcriptionCancellation?.Cancel();

    /// <summary>Ruta de cierre seguro: cancela y espera la finalización natural.</summary>
    public async Task CancelAndWaitForCloseAsync()
    {
        if (_transcriptionCancellation is null) return;
        _transcriptionCancellation.Cancel();
        if (_transcriptionFinished is not null) await _transcriptionFinished.Task;
    }
}

public sealed partial class TranscriptSegmentViewModel(TranscriptSegment segment) : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isMatch;
    public TranscriptSegment Segment { get; } = segment ?? throw new ArgumentNullException(nameof(segment));
    public string TimeRange => $"{Segment.Start:hh\\:mm\\:ss} → {Segment.End:hh\\:mm\\:ss}";
}

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transcriptor.Transcription;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Panel de biblioteca: enumeración de sesiones, filtro/búsqueda portable
/// vía <see cref="LibraryFilter"/> y selección de la sesión corriente. No
/// reproduce ni transcribe: expone la selección a
/// <see cref="SessionDetailViewModel"/> y avisa con
/// <see cref="SelectionChanged"/> en cada cambio.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private static readonly string[] TranscriptFileNames =
        ["transcripcion.srt.trn", "yo.srt.trn", "cliente.srt.trn", "transcripcion.srt", "yo.srt", "cliente.srt"];

    private readonly UiServices _services;
    private readonly TranscriptionUiState _transcriptionUiState;
    private readonly ILogSink _log;
    private readonly List<LibraryEntry> _allRecordings = [];
    private string? _persistedSelectionPath;
    private Func<string> _recordingsDirectory = () => string.Empty;

    public LibraryViewModel(UiServices services, TranscriptionUiState transcriptionUiState, ILogSink log)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _transcriptionUiState = transcriptionUiState ?? throw new ArgumentNullException(nameof(transcriptionUiState));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _sessionStore = services.CreateSessionStore();
        _transcriptionUiState.Changed += () => OnPropertyChanged(nameof(CanInteract));
    }

    private readonly ISessionStore _sessionStore;

    /// <summary>La selección (o su vaciado) cambia para actualizar el detalle.</summary>
    public event Action<LibraryEntry?>? SelectionChanged;

    // ------------------------------------------------------------------ observables
    [ObservableProperty] private string _librarySearchText = string.Empty;
    [ObservableProperty] private int _libraryFilterIndex;
    [ObservableProperty] private int _sortModeIndex;
    [ObservableProperty] private IReadOnlyList<LibraryEntry> _filteredRecordings = Array.Empty<LibraryEntry>();
    [ObservableProperty] private LibraryEntry? _selectedRecording;
    [ObservableProperty] private string _libraryCountText = "Biblioteca sin cargar";
    [ObservableProperty] private bool _isLoading;

    /// <summary>Bloqueo: la biblioteca se congela durante la transcripción.</summary>
    public bool CanInteract => !_transcriptionUiState.IsTranscribing;
    public bool HasSelectedRecording => SelectedRecording is not null;

    partial void OnLibrarySearchTextChanged(string value) => ApplyLibraryFilters();
    partial void OnLibraryFilterIndexChanged(int value) => ApplyLibraryFilters();
    partial void OnSortModeIndexChanged(int value) => ApplyLibraryFilters();

    partial void OnSelectedRecordingChanged(LibraryEntry? value)
    {
        _persistedSelectionPath = value?.DirectoryPath;
        OnPropertyChanged(nameof(HasSelectedRecording));
        SelectionChanged?.Invoke(value);
    }

    /// <summary>Carpeta raíz a escanear; la conecta el shell con la propia Settings.</summary>
    public void SetRecordingsDirectorySource(Func<string> recordingsDirectory)
        => _recordingsDirectory = recordingsDirectory ?? throw new ArgumentNullException(nameof(recordingsDirectory));

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        if (!CanInteract || IsLoading) return;
        IsLoading = true;
        LibraryCountText = "Cargando biblioteca…";
        try
        {
            var root = _recordingsDirectory();
            var recordings = await Task.Run(() => ReadEntries(root));
            _allRecordings.Clear();
            _allRecordings.AddRange(recordings);
            ApplyLibraryFilters();
            _log.Log($"Biblioteca actualizada: {_allRecordings.Count} sesión(es) reconocida(s).");
        }
        catch (Exception ex)
        {
            LibraryCountText = "No se pudo cargar la biblioteca.";
            _log.Log("No se pudo actualizar la biblioteca: " + ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<LibraryEntry> ReadEntries(string root)
    {
        var recordings = new List<LibraryEntry>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return recordings;
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
            var hasTranscript = TranscriptFileNames
                .Any(name => File.Exists(Path.Combine(directory, name)));
            var micSize = File.Exists(mic) ? new FileInfo(mic).Length : 0;
            var clientSize = File.Exists(client) ? new FileInfo(client).Length : 0;
            recordings.Add(new LibraryEntry(title, directory, mic, client, hasTranscript, RecordingIntegrity.IsIncomplete(directory), micSize, clientSize));
        }
        return recordings;
    }

    /// <summary>Selecciona la sesión por su ruta absoluta (post migración u otra operación).</summary>
    public void SelectEntry(string path)
    {
        SelectedRecording = FilteredRecordings.FirstOrDefault(item =>
            string.Equals(item.DirectoryPath, path, StringComparison.OrdinalIgnoreCase))
            ?? _allRecordings.FirstOrDefault(item =>
                string.Equals(item.DirectoryPath, path, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyLibraryFilters()
    {
        var filtered = LibrarySort.Apply(
            LibraryFilter.Apply(_allRecordings, LibrarySearchText, LibraryFilterIndex),
            SortModeIndex);
        FilteredRecordings = filtered;
        LibraryCountText = $"{FilteredRecordings.Count} de {_allRecordings.Count} sesión(es)";
        // Conserva la selección si sigue visible tras cambiar el filtro o el
        // contenido (identidad por ruta para no perder contexto al filtrar).
        if (_persistedSelectionPath is null
            || (SelectedRecording is not null && FilteredRecordings.Contains(SelectedRecording))) return;
        SelectedRecording = FilteredRecordings.FirstOrDefault(entry =>
            string.Equals(entry.DirectoryPath, _persistedSelectionPath, StringComparison.OrdinalIgnoreCase));
    }
}

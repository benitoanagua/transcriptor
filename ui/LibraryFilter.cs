namespace Transcriptor.UI;

/// <summary>
/// Datos mínimos de una sesión de biblioteca para filtrar sin depender de la vista.
/// </summary>
public interface ILibraryEntry
{
    string Title { get; }
    string DirectoryPath { get; }
    bool HasMicrophone { get; }
    bool HasClient { get; }
    bool HasTranscript { get; }
    bool IsIncomplete { get; }
}

/// <summary>
/// Filtrado y búsqueda de la biblioteca, portable y cubierto por pruebas:
/// búsqueda por título o ruta (sin distinguir mayúsculas) y filtros por
/// combinación de pistas, transcripción o captura incompleta.
/// </summary>
public static class LibraryFilter
{
    public const int All = 0;
    public const int BothTracks = 1;
    public const int MicrophoneOnly = 2;
    public const int SystemOnly = 3;
    public const int WithTranscript = 4;
    public const int Incomplete = 5;

    public static IReadOnlyList<T> Apply<T>(IEnumerable<T> entries, string query, int filterIndex)
        where T : ILibraryEntry
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var index = filterIndex;
        return entries.Where(entry =>
        {
            if (normalizedQuery.Length > 0
                && !entry.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                && !entry.DirectoryPath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                return false;
            return index switch
            {
                BothTracks => entry.HasMicrophone && entry.HasClient,
                MicrophoneOnly => entry.HasMicrophone && !entry.HasClient,
                SystemOnly => !entry.HasMicrophone && entry.HasClient,
                WithTranscript => entry.HasTranscript,
                Incomplete => entry.IsIncomplete,
                _ => true
            };
        }).ToList();
    }
}

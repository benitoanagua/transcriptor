using System.IO;

namespace Transcriptor.UI.ViewModels;

/// <summary>
/// Interacciones que por naturaleza pertenecen a la vista (diálogos modales,
/// selectores de archivos y portapapeles). Los ViewModels las consumen como
/// puerto; la ventana concreta las implementa.
/// </summary>
public interface IUiHost
{
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> PickFolderAsync(string title);
    Task<string?> PickFileAsync(string title, string? pattern);
    Task<string?> PickSaveFileAsync(string title, string pattern);
    Task OpenFolderAsync(string path);
    /// <summary>Copia texto al portapapeles del sistema; devuelve false si no hay portapapeles disponible.</summary>
    Task<bool> CopyToClipboardAsync(string text);
}

/// <summary>Entrada de biblioteca: pistas reconocidas, estado y metadatos baratos.</summary>
public sealed record LibraryEntry : ILibraryEntry
{
    public LibraryEntry(
        string title, string directoryPath, string microphonePath, string clientPath,
        bool hasTranscript, bool isIncomplete, long microphoneSizeBytes = 0, long clientSizeBytes = 0)
    {
        Title = title;
        DirectoryPath = directoryPath;
        MicrophonePath = microphonePath;
        ClientPath = clientPath;
        HasTranscript = hasTranscript;
        IsIncomplete = isIncomplete;
        MicrophoneSizeBytes = microphoneSizeBytes;
        ClientSizeBytes = clientSizeBytes;
    }

    public string Title { get; }
    public string DirectoryPath { get; }
    public string MicrophonePath { get; }
    public string ClientPath { get; }
    public bool HasTranscript { get; }
    public bool IsPending => !HasTranscript && !IsIncomplete;
    public bool IsIncomplete { get; }
    public long MicrophoneSizeBytes { get; }
    public long ClientSizeBytes { get; }
    public bool HasMicrophone => File.Exists(MicrophonePath);
    public bool HasClient => File.Exists(ClientPath);

    /// <summary>La pista de micrófono está en el contenedor cifrado (.trn), no en claro.</summary>
    public bool IsEncrypted => MicrophonePath.EndsWith(".trn", StringComparison.OrdinalIgnoreCase);

    /// <summary>Tamaños de ambas pistas en MB con una cifra decimal (metadato barato; sin descifrado).</summary>
    public string Sizes
    {
        get
        {
            const long oneMbInBytes = 1024 * 1024;
            var mic = MicrophoneSizeBytes <= 0 ? "—" : (MicrophoneSizeBytes / (double)oneMbInBytes).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " MB";
            var client = ClientSizeBytes <= 0 ? "—" : (ClientSizeBytes / (double)oneMbInBytes).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " MB";
            return $"MIC {mic} · SISTEMA {client}";
        }
    }

    public string Summary => $"{(HasMicrophone ? "MIC ✓" : "MIC —")} · {(HasClient ? "SISTEMA ✓" : "SISTEMA —")}"
        + (HasTranscript ? " · TRANSCRIPCIÓN" : "") + (IsIncomplete ? " · INCOMPLETA" : "");
}

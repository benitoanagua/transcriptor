using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Transcriptor.UI.ViewModels;

/// <summary>Destino de los mensajes de registro para los demás ViewModels.</summary>
public interface ILogSink
{
    void Log(string message);
}

/// <summary>
/// Panel REGISTRO: texto acotado vía <see cref="BoundedLogBuffer"/> con
/// comandos de copia y vaciado. No toca Avalonia; el portapapeles llega
/// por el puerto <see cref="IUiHost"/>.
/// </summary>
public sealed partial class LogViewModel : ObservableObject, ILogSink
{
    private readonly IUiHost _host;
    private readonly BoundedLogBuffer _buffer = new();

    public LogViewModel(IUiHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _buffer.Changed += () => LogText = _buffer.CurrentText;
    }

    public BoundedLogBuffer Buffer => _buffer;

    [ObservableProperty]
    private string _logText = string.Empty;

    public void Log(string message) => _buffer.Append($"[{DateTime.Now:HH:mm:ss}] {message}");

    [RelayCommand]
    private async Task CopyLogAsync()
    {
        var copied = await _host.CopyToClipboardAsync(LogText);
        Log(copied ? "Registro copiado al portapapeles." : "No hay portapapeles disponible en esta plataforma.");
    }

    [RelayCommand]
    private void ClearLog() => _buffer.Clear();
}

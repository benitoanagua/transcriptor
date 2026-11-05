namespace Transcriptor.UI;

/// <summary>
/// Registro acotado del panel REGISTRO: conserva como máximo
/// <see cref="DefaultCapacity"/> líneas descartando las más antiguas
/// (FIFO) y notifica cambios para refrescar la vista. Portable y
/// testeable sin Avalonia: el ViewModel lo usa como fuente única del
/// texto mostrado.
/// </summary>
public sealed class BoundedLogBuffer
{
    public const int DefaultCapacity = 2000;

    private readonly Queue<string> _lines = new();
    private readonly int _capacity;
    private string _text = string.Empty;

    /// <summary>Se dispara tras cada cambio del contenido (apéndice, vaciado).</summary>
    public event Action? Changed;

    /// <summary>Número de líneas retenidas.</summary>
    public int Count => _lines.Count;

    /// <summary>Capacidad máxima configurada.</summary>
    public int Capacity => _capacity;

    /// <summary>Texto actual (cada línea con su salto de línea final).</summary>
    public string CurrentText => _text;

    public BoundedLogBuffer(int capacity = DefaultCapacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    /// <summary>Añade una línea; si se alcanza la capacidad se descarta la más antigua.</summary>
    public void Append(string line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        if (_lines.Count == _capacity) _lines.Dequeue();
        _lines.Enqueue(line);
        Rebuild();
        Changed?.Invoke();
    }

    /// <summary>Elimina todo el contenido retenido.</summary>
    public void Clear()
    {
        if (_lines.Count == 0) return;
        _lines.Clear();
        _text = string.Empty;
        Changed?.Invoke();
    }

    private void Rebuild()
    {
        _text = string.Join(Environment.NewLine, _lines) + Environment.NewLine;
    }
}

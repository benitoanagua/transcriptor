using System.Diagnostics;
using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ DATOS FICTICIOS — SOLO DESARROLLO ⚠️
// Runner de Whisper simulado: no ejecuta ningún proceso. Lee los argumentos que
// TranscriptionEngine produce (-of prefijo) y escribe un SRT de ejemplo con
// hablantes Yo/Cliente, de modo que la pipa SRT → fusión → cifrado (inseguro)
// continúa igual que en producción. Progreso simulado por etapas en stderr.
public sealed class FakeWhisperRunner : IWhisperProcessRunner
{
    public async Task<WhisperProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        var outputPrefix = ExtractArgumentValue(startInfo, "-of")
            ?? throw new InvalidOperationException("El runner simulado requiere el argumento -of.");
        var track = Path.GetFileName(outputPrefix) is "cliente" ? "Cliente" : "Yo";

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        var srt = $"""
            1
            00:00:00,500 --> 00:00:01,900
            {track}: prueba de transcripción simulada

            2
            00:00:02,100 --> 00:00:03,600
            {track}: segunda línea del fake de desarrollo

            3
            00:00:03,900 --> 00:00:04,400
            {track}: cierre

            """;
        await File.WriteAllTextAsync(outputPrefix + ".srt", srt, cancellationToken);
        return new WhisperProcessResult(0, string.Empty, "[runner simulado] progreso: decodificar 100%");
    }

    private static string? ExtractArgumentValue(ProcessStartInfo startInfo, string flag)
    {
        var arguments = startInfo.ArgumentList;
        for (var index = 0; index < arguments.Count - 1; index++)
            if (arguments[index] == flag) return arguments[index + 1];
        return null;
    }
}

// ⚠️ SOLO DESARROLLO ⚠️
// Normalizador simulado: copia los bytes del WAV origen (el motor solo requiere
// la existencia del archivo normalizado en modo desarrollo).
public sealed class CopyingDevNormalizer : IAudioNormalizer
{
    public void Normalize(string sourcePath, string destinationPath)
    {
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }
}

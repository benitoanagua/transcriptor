using System.Diagnostics;
using NAudio.Wave;

namespace Transcriptor.Transcription;

public sealed class TranscriptionEngine(string whisperExecutable, string modelPath, string language)
{
    public async Task<int> ProcessPendingCallsAsync(string recordingsRoot, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(recordingsRoot))
            throw new DirectoryNotFoundException($"No existe la carpeta de grabaciones: {recordingsRoot}");
        RecordingAccessControl.ValidateDirectoryPath(recordingsRoot);
        recordingsRoot = Path.GetFullPath(recordingsRoot);
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("No se encontró el modelo Whisper.", modelPath);
        if (!File.Exists(whisperExecutable))
            throw new FileNotFoundException("No se encontró whisper-cli.", whisperExecutable);

        var processed = 0;
        var failures = new List<string>();

        foreach (var dateDirectory in Directory.EnumerateDirectories(recordingsRoot).Order(StringComparer.Ordinal))
        {
            if (IsReparsePoint(dateDirectory)) continue;

            foreach (var callDirectory in Directory.EnumerateDirectories(dateDirectory).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsReparsePoint(callDirectory)) continue;

                if (File.Exists(Path.Combine(callDirectory, "transcripcion.txt.trn")) || File.Exists(Path.Combine(callDirectory, "transcripcion.txt")))
                    continue;

                var micPath = ExistingTrack(callDirectory, "yo");
                var systemPath = ExistingTrack(callDirectory, "cliente");

                // Una llamada solo está completa cuando existen las dos pistas.
                if (micPath is null || systemPath is null)
                    continue;

                try
                {
                    // Las sesiones heredadas también deben proteger sus WAV temporales y
                    // archivos de trabajo antes de que Whisper o el motor escriban en ellas.
                    RecordingAccessControl.RestrictSessionDirectory(callDirectory);
                    progress?.Report($"Transcribiendo micrófono: {callDirectory}");
                    await TranscribeTrackAsync(micPath, "yo", callDirectory, cancellationToken);
                    progress?.Report($"Transcribiendo audio del sistema: {callDirectory}");
                    await TranscribeTrackAsync(systemPath, "cliente", callDirectory, cancellationToken);
                    var segments = SrtMerger.Merge(callDirectory);
                    SrtMerger.Write(callDirectory, segments);
                    EncryptTranscriptFiles(callDirectory);
                    processed++;
                    var completedMessage = $"Transcripción completada: {callDirectory}";
                    Console.WriteLine(completedMessage);
                    progress?.Report(completedMessage);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var failureMessage = $"Error en {callDirectory}: {exception.Message}";
                    failures.Add(failureMessage);
                    Console.Error.WriteLine(failureMessage);
                    progress?.Report(failureMessage);
                }
            }
        }

        var summary = $"Llamadas transcritas: {processed}. Errores: {failures.Count}.";
        Console.WriteLine(summary);
        progress?.Report(summary);
        if (failures.Count > 0)
            throw new AggregateException("Una o más llamadas no pudieron transcribirse.", failures.Select(message => new InvalidOperationException(message)));

        return processed;
    }

    private async Task TranscribeTrackAsync(string sourcePath, string trackName, string callDirectory, CancellationToken cancellationToken)
    {
        var normalizedPath = Path.Combine(callDirectory, $".{Guid.NewGuid():N}.normalized.wav");
        var sourceWavePath = sourcePath.EndsWith(".trn", StringComparison.OrdinalIgnoreCase)
            ? EncryptedAudioTrack.DecryptToWaveFile(sourcePath) : sourcePath;
        var outputPrefix = Path.Combine(callDirectory, trackName);

        try
        {
            using (var reader = new WaveFileReader(sourceWavePath))
            using (var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1)))
            {
                resampler.ResamplerQuality = 60;
                WaveFileWriter.CreateWaveFile(normalizedPath, resampler);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = whisperExecutable,
                WorkingDirectory = callDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add(modelPath);
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add(language);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(normalizedPath);
            startInfo.ArgumentList.Add("-osrt");
            startInfo.ArgumentList.Add("-of");
            startInfo.ArgumentList.Add(outputPrefix);

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("No se pudo iniciar whisper-cli.");

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var error = await standardError;
            _ = await standardOutput;

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"whisper-cli terminó con código {process.ExitCode}: {error}");

            if (!File.Exists(outputPrefix + ".srt"))
                throw new InvalidDataException($"Whisper no generó el archivo {trackName}.srt.");
        }
        finally
        {
            if (File.Exists(normalizedPath))
                File.Delete(normalizedPath);
            if (!string.Equals(sourceWavePath, sourcePath, StringComparison.OrdinalIgnoreCase) && File.Exists(sourceWavePath))
                File.Delete(sourceWavePath);
        }
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static string? ExistingTrack(string directory, string name)
    {
        var encrypted = Path.Combine(directory, name + ".trn");
        if (File.Exists(encrypted)) return encrypted;
        var legacy = Path.Combine(directory, name + ".wav");
        return File.Exists(legacy) ? legacy : null;
    }

    private static void EncryptTranscriptFiles(string directory)
    {
        foreach (var name in new[] { "yo.srt", "cliente.srt", "transcripcion.srt", "transcripcion.txt" })
        {
            var plaintext = Path.Combine(directory, name);
            if (!File.Exists(plaintext)) continue;
            var encrypted = plaintext + ".trn";
            EncryptedFileOperations.EncryptFileAtomically(plaintext, encrypted);
            var verification = EncryptedFileOperations.DecryptToTemporaryFile(encrypted);
            try
            {
                if (new FileInfo(verification).Length != new FileInfo(plaintext).Length)
                    throw new InvalidDataException("La verificación de la transcripción cifrada no coincide.");
            }
            finally { if (File.Exists(verification)) File.Delete(verification); }
            File.Delete(plaintext);
        }
    }

}

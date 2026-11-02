using System.Diagnostics;
using NAudio.Wave;

namespace Transcriptor.Transcription;

public sealed class TranscriptionEngine(string whisperExecutable, string modelPath, string language)
{
    public async Task<int> ProcessPendingCallsAsync(string recordingsRoot, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(recordingsRoot))
            throw new DirectoryNotFoundException($"No existe la carpeta de grabaciones: {recordingsRoot}");
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("No se encontró el modelo Whisper.", modelPath);
        if (!File.Exists(whisperExecutable))
            throw new FileNotFoundException("No se encontró whisper-cli.", whisperExecutable);

        var processed = 0;
        var failures = new List<string>();

        foreach (var dateDirectory in Directory.EnumerateDirectories(recordingsRoot).Order(StringComparer.Ordinal))
        {
            foreach (var callDirectory in Directory.EnumerateDirectories(dateDirectory).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(Path.Combine(callDirectory, "transcripcion.txt")))
                    continue;

                var micPath = Path.Combine(callDirectory, "yo.wav");
                var systemPath = Path.Combine(callDirectory, "cliente.wav");

                // Una llamada solo está completa cuando existen las dos pistas.
                if (!File.Exists(micPath) || !File.Exists(systemPath))
                    continue;

                try
                {
                    await TranscribeTrackAsync(micPath, "yo", callDirectory, cancellationToken);
                    await TranscribeTrackAsync(systemPath, "cliente", callDirectory, cancellationToken);
                    var segments = SrtMerger.Merge(callDirectory);
                    SrtMerger.Write(callDirectory, segments);
                    processed++;
                    Console.WriteLine($"Transcripción completada: {callDirectory}");
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failures.Add($"{callDirectory}: {exception.Message}");
                    Console.Error.WriteLine($"Error en {callDirectory}: {exception.Message}");
                }
            }
        }

        Console.WriteLine($"Llamadas transcritas: {processed}. Errores: {failures.Count}.");
        if (failures.Count > 0)
            throw new AggregateException("Una o más llamadas no pudieron transcribirse.", failures.Select(message => new InvalidOperationException(message)));

        return processed;
    }

    private async Task TranscribeTrackAsync(string sourcePath, string trackName, string callDirectory, CancellationToken cancellationToken)
    {
        var normalizedPath = Path.Combine(Path.GetTempPath(), $"transcriptor-{Guid.NewGuid():N}.wav");
        var outputPrefix = Path.Combine(callDirectory, trackName);

        try
        {
            using (var reader = new WaveFileReader(sourcePath))
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
        }
    }
}

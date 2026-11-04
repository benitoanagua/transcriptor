using System.Diagnostics;

namespace Transcriptor.Transcription;

public sealed class TranscriptionEngine
{
    private readonly string whisperExecutable;
    private readonly string modelPath;
    private readonly string language;
    private readonly IWhisperProcessRunner processRunner;
    private readonly IAudioNormalizer audioNormalizer;
    private readonly ISessionDirectorySecurity security;

    /// <summary>Convenience constructor with the default local process runner.</summary>
    public TranscriptionEngine(
        string whisperExecutable,
        string modelPath,
        string language,
        ISessionDirectorySecurity security,
        IAudioNormalizer audioNormalizer)
        : this(whisperExecutable, modelPath, language, new WhisperProcessRunner(), audioNormalizer, security)
    {
    }

    public TranscriptionEngine(
        string whisperExecutable,
        string modelPath,
        string language,
        IWhisperProcessRunner processRunner,
        IAudioNormalizer audioNormalizer,
        ISessionDirectorySecurity security)
    {
        this.whisperExecutable = whisperExecutable;
        this.modelPath = modelPath;
        this.language = language;
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.audioNormalizer = audioNormalizer ?? throw new ArgumentNullException(nameof(audioNormalizer));
        this.security = security ?? throw new ArgumentNullException(nameof(security));
    }
    private static readonly string[] TranscriptArtifacts = SessionLayout.TranscriptFileNames
        .Select(name => name + ".trn").ToArray();

    public async Task<int> ProcessPendingCallsAsync(string recordingsRoot, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(recordingsRoot))
            throw new DirectoryNotFoundException($"No existe la carpeta de grabaciones: {recordingsRoot}");
        security.ValidateDirectoryPath(recordingsRoot);
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

                if (RecordingIntegrity.IsIncomplete(callDirectory))
                {
                    progress?.Report($"Sesión omitida: la captura quedó marcada como incompleta: {callDirectory}");
                    continue;
                }

                var layout = new SessionLayout(callDirectory);
                var micPath = layout.ExistingAudioTrack(SessionLayout.MicrophoneTrackName);
                var systemPath = layout.ExistingAudioTrack(SessionLayout.SystemTrackName);
                if (micPath is null || systemPath is null)
                    continue;

                try
                {
                    using var sessionLease = TranscriptionSessionLease.TryAcquire(callDirectory);
                    if (sessionLease is null)
                    {
                        progress?.Report($"Sesión omitida: ya está siendo transcrita por otra ejecución: {callDirectory}");
                        continue;
                    }

                    security.RestrictSessionDirectory(callDirectory);
                    if (IsTranscriptComplete(callDirectory))
                        continue;

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

    internal static bool IsTranscriptComplete(string directory)
    {
        // El TXT cifrado por sí solo no es un marcador suficiente: un fallo
        // anterior pudo dejar el conjunto de artefactos incompleto.
        foreach (var artifact in TranscriptArtifacts)
        {
            var encrypted = new SessionLayout(directory).Transcript(artifact[..^4]);
            var plaintext = new SessionLayout(directory).Transcript(artifact[..^4], encrypted: false);
            if (!File.Exists(encrypted) || File.Exists(plaintext))
                return false;
        }

        // Autentica todos los artefactos antes de omitir la sesión. Un archivo
        // truncado o alterado no puede convertir una sesión fallida en completa.
        try
        {
            foreach (var artifact in TranscriptArtifacts)
                EncryptedFileOperations.VerifyEncryptedFile(new SessionLayout(directory).Transcript(artifact[..^4]));

            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Security.Cryptography.CryptographicException)
        {
            // Una salida dañada no debe bloquear la recuperación: el audio
            // original sigue disponible y la transcripción puede regenerarse.
            return false;
        }
    }

    internal async Task TranscribeTrackAsync(string sourcePath, string trackName, string callDirectory, CancellationToken cancellationToken)
    {
        var layout = new SessionLayout(callDirectory);
        var normalizedPath = layout.Temporary("normalized", ".wav");
        var sourceWavePath = sourcePath.EndsWith(".trn", StringComparison.OrdinalIgnoreCase)
            ? EncryptedAudioTrack.DecryptToWaveFile(sourcePath) : sourcePath;
        var outputPrefix = Path.Combine(layout.Directory, trackName);
        var outputSrt = outputPrefix + ".srt";

        try
        {
            // Evita que un SRT antiguo de un intento fallido se mezcle con la
            // salida de la ejecución actual.
            if (File.Exists(outputSrt))
                File.Delete(outputSrt);

            audioNormalizer.Normalize(sourceWavePath, normalizedPath);

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

            var result = await processRunner.RunAsync(startInfo, cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"whisper-cli terminó con código {result.ExitCode}: {result.StandardError}");

            if (!File.Exists(outputSrt))
                throw new InvalidDataException($"Whisper no generó el archivo {trackName}.srt.");
        }
        finally
        {
            TryDelete(normalizedPath);
            if (!string.Equals(sourceWavePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                TryDelete(sourceWavePath);
        }
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void EncryptTranscriptFiles(string directory)
    {
        // El marcador de finalización se publica al final, después de verificar
        // los otros tres artefactos y de eliminar sus originales en claro.
        var names = SessionLayout.TranscriptFileNames;
        foreach (var name in names)
        {
            var layout = new SessionLayout(directory);
            var plaintext = layout.Transcript(name, encrypted: false);
            var encrypted = layout.Transcript(name);
            if (!File.Exists(plaintext))
            {
                EncryptedFileOperations.VerifyEncryptedFile(encrypted);
                continue;
            }

            EncryptedFileOperations.EncryptAndVerifyFile(plaintext, encrypted, deletePlaintext: true);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* El borrado normal es best effort; no es borrado seguro. */ }
    }
}

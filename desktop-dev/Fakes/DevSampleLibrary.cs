using System.Buffers.Binary;
using Transcriptor.Transcription;

namespace Transcriptor.Dev;

// ⚠️ DATOS FICTICIOS — SOLO DESARROLLO ⚠️
// Biblioteca de muestra para desenvolver la interfaz en Linux/WSLg:
//  - una sesión completa (dos pistas "cifradas" de 2 s + artefactos de
//    transcripción cifrados con el protector inseguro de desarrollo),
//  - una sesión con solo micrófono,
//  - una sesión con marcador de similaridad .recording-incomplete,
//  - una sesión heredada en claro (WAV + SRT legibles) para probar migración.
// Todo vive bajo la carpeta temporal de desarrollo y se puede borrar sin riesgo.
public static class DevSampleLibrary
{
    public static void EnsureCreated(string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
            return;
        Create(root);
    }

    private static void Create(string root)
    {
        Directory.CreateDirectory(root);
        try
        {
            var complete = Path.Combine(root, "dev-samples", "01 llamada completa");
            Directory.CreateDirectory(complete);
            WriteEncryptedAudioTrack(complete, SessionLayout.MicrophoneTrackName, AudioTrackFormat.Pcm(16000, 1, 16), 2.0);
            WriteEncryptedAudioTrack(complete, SessionLayout.SystemTrackName, AudioTrackFormat.IeeeFloat(48000, 2), 2.0);
            WriteEncryptedTranscripts(complete);

            var onlyMicrophone = Path.Combine(root, "dev-samples", "02 solo micrófono");
            Directory.CreateDirectory(onlyMicrophone);
            WriteEncryptedAudioTrack(onlyMicrophone, SessionLayout.MicrophoneTrackName, AudioTrackFormat.Pcm(16000, 1, 16), 1.5);

            var incomplete = Path.Combine(root, "dev-samples", "03 cancelada");
            Directory.CreateDirectory(incomplete);
            WriteEncryptedAudioTrack(incomplete, SessionLayout.MicrophoneTrackName, AudioTrackFormat.Pcm(16000, 1, 16), 0.8);
            RecordingIntegrity.MarkIncomplete(incomplete, new IOException("Señal de desarrollo antes de detenerse."));

            var plaintext = Path.Combine(root, "dev-samples", "04 heredada en claro");
            Directory.CreateDirectory(plaintext);
            WritePlaintextWav(Path.Combine(plaintext, "yo.wav"), AudioTrackFormat.Pcm(16000, 1, 16), 1.0);
            WritePlaintextWav(Path.Combine(plaintext, "cliente.wav"), AudioTrackFormat.Pcm(16000, 1, 16), 1.0);
            File.WriteAllText(Path.Combine(plaintext, "transcripcion.srt"),
                """
                1
                00:00:00,000 --> 00:00:00,800
                Yo: sesión heredada sin cifrar

                2
                00:00:00,800 --> 00:00:01,0
                Cliente: segunda línea de muestra

                """);
            // Legacy sessions also carry their .txt plaintext; mirrors old layouts.
            File.WriteAllText(Path.Combine(plaintext, "transcripcion.txt"),
                    "[00:00:00] Yo: sesión heredada sin cifrar\n[00:00:00.800] Cliente: segunda línea de muestra\n");
        }
        catch
        {
            // El generador de muestra no debe bloquear el arranque de la app de desarrollo.
        }
    }

    private static void WriteEncryptedAudioTrack(string directory, string trackName, AudioTrackFormat format, double seconds)
    {
        var frames = (int)Math.Max(1, seconds * format.SampleRate);
        var layout = new SessionLayout(directory);
        using var writer = EncryptedAudioTrack.CreateWriter(layout.AudioTrack(trackName), format);
        WriteFrames(writer, format, frames);
    }

    private static void WriteEncryptedTranscripts(string directory)
    {
        var layout = new SessionLayout(directory);
        var srt = """
            1
            00:00:00,000 --> 00:00:01,000
            Yo: muestra de sesión completa

            2
            00:00:01,000 --> 00:00:02,000
            Cliente: respuesta simulada para el visor

            """;
        var transcription = "[00:00:00] Yo: muestra de sesión completa\n[00:00:01] Cliente: respuesta simulada para el visor\n";
        EncryptTranscript(layout, "transcripcion.srt", srt);
        EncryptTranscript(layout, "transcripcion.txt", transcription);
        EncryptTranscript(layout, "yo.srt", srt);
        EncryptTranscript(layout, "cliente.srt", srt);
    }

    private static void EncryptTranscript(SessionLayout layout, string name, string content)
    {
        var plain = layout.Transcript(name, encrypted: false);
        var encrypted = layout.Transcript(name);
        File.WriteAllText(plain, content);
        EncryptedFileOperations.EncryptAndVerifyFile(plain, encrypted, deletePlaintext: true);
    }

    private static void WriteFrames(EncryptedAudioTrackWriter writer, AudioTrackFormat format, int frames)
    {
        const int chunkSize = 64 * 1024;
        if (format.Encoding == AudioTrackFormat.PcmEncoding && format.BitsPerSample == 16)
        {
            var samples = new byte[frames * format.BlockAlign];
            for (var frame = 0; frame < frames; frame++)
                for (var channel = 0; channel < format.Channels; channel++)
                    BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan((frame * format.Channels + channel) * 2),
                        (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * 12000));
            for (var index = 0; index < samples.Length; index += chunkSize)
                writer.Write(samples, index, Math.Min(chunkSize, samples.Length - index));
        }
        else if (format.Encoding == AudioTrackFormat.IeeeFloatEncoding && format.BitsPerSample == 32)
        {
            var samples = new byte[frames * format.BlockAlign];
            for (var frame = 0; frame < frames; frame++)
                for (var channel = 0; channel < format.Channels; channel++)
                    BinaryPrimitives.WriteSingleLittleEndian(samples.AsSpan((frame * format.Channels + channel) * 4),
                        (float)(Math.Sin(2 * Math.PI * 330 * frame / format.SampleRate) * 0.35));
            for (var index = 0; index < samples.Length; index += chunkSize)
                writer.Write(samples, index, Math.Min(chunkSize, samples.Length - index));
        }
        else
        {
            throw new NotSupportedException("Formato de sample de desarrollo no soportado.");
        }
    }

    private static void WritePlaintextWav(string path, AudioTrackFormat format, double seconds)
    {
        var frames = (int)Math.Max(1, seconds * format.SampleRate);
        var dataLength = frames * format.BlockAlign;
        var samples = new byte[dataLength];
        for (var frame = 0; frame < frames; frame++)
            for (var channel = 0; channel < format.Channels; channel++)
                BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan((frame * format.Channels + channel) * 2),
                    (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * 11000));

        using var wave = File.Create(path);
        var preamble = new byte[44];
        "RIFF"u8.CopyTo(preamble);
        BinaryPrimitives.WriteUInt32LittleEndian(preamble.AsSpan(4), (uint)(dataLength + 36));
        "WAVE"u8.CopyTo(preamble.AsSpan(8));
        "fmt "u8.CopyTo(preamble.AsSpan(12));
        BinaryPrimitives.WriteInt32LittleEndian(preamble.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(20), format.Encoding);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(22), format.Channels);
        BinaryPrimitives.WriteInt32LittleEndian(preamble.AsSpan(24), format.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(preamble.AsSpan(28), format.AverageBytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(32), format.BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(preamble.AsSpan(34), format.BitsPerSample);
        "data"u8.CopyTo(preamble.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(preamble.AsSpan(40), (uint)dataLength);
        wave.Write(preamble);
        wave.Write(samples);
    }
}

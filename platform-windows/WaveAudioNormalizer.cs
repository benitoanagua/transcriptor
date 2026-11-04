using NAudio.Wave;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>Converts a source WAV into the mono 16 kHz PCM input expected by Whisper.cpp.</summary>
public sealed class WaveAudioNormalizer : IAudioNormalizer
{
    public void Normalize(string sourcePath, string destinationPath)
    {
        using var reader = new WaveFileReader(sourcePath);
        using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1))
        {
            ResamplerQuality = 60
        };
        WaveFileWriter.CreateWaveFile(destinationPath, resampler);
    }
}

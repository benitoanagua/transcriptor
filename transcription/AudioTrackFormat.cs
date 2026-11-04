using System.Buffers.Binary;

namespace Transcriptor.Transcription;

/// <summary>
/// Portable description of a PCM/IEEE-float audio track, independent of NAudio.
/// The values are stored verbatim in the 24-byte encrypted track header, so the
/// mapping from NAudio WaveFormat (platform layer) must be exact.
/// </summary>
public sealed record AudioTrackFormat(
    ushort Encoding,
    ushort Channels,
    int SampleRate,
    int AverageBytesPerSecond,
    ushort BlockAlign,
    ushort BitsPerSample)
{
    public const ushort PcmEncoding = 1;
    public const ushort IeeeFloatEncoding = 3;

    public static AudioTrackFormat Pcm(int sampleRate, int channels, int bitsPerSample) =>
        new(PcmEncoding, checked((ushort)channels), sampleRate, sampleRate * channels * ((bitsPerSample + 7) / 8),
            checked((ushort)(channels * ((bitsPerSample + 7) / 8))), checked((ushort)bitsPerSample));

    public static AudioTrackFormat IeeeFloat(int sampleRate, int channels) =>
        new(IeeeFloatEncoding, checked((ushort)channels), sampleRate, sampleRate * channels * 4,
            checked((ushort)(channels * 4)), 32);
}

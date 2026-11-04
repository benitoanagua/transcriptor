using NAudio.Wave;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>
/// Exact NAudio WaveFormat ↔ AudioTrackFormat mapping. The AudioTrackFormat values
/// are written verbatim into the 24-byte encrypted track header, so this conversion
/// must not normalize or reinterpret anything.
/// </summary>
public static class PlatformAudioFormats
{
    public static AudioTrackFormat ToAudioTrackFormat(this WaveFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var encoding = format.Encoding;
        if (encoding == WaveFormatEncoding.Extensible && format is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == DualFormats.Pcm) encoding = WaveFormatEncoding.Pcm;
            else if (extensible.SubFormat == DualFormats.Float) encoding = WaveFormatEncoding.IeeeFloat;
        }
        return new AudioTrackFormat(
            (ushort)encoding,
            checked((ushort)format.Channels),
            format.SampleRate,
            format.AverageBytesPerSecond,
            checked((ushort)format.BlockAlign),
            checked((ushort)format.BitsPerSample));
    }

    private static class DualFormats
    {
        public static readonly Guid Pcm = new("00000001-0000-0010-8000-00AA00389B71");
        public static readonly Guid Float = new("00000003-0000-0010-8000-00AA00389B71");
    }
}

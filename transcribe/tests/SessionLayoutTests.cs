using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class SessionLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TranscriptorSessionLayout", Guid.NewGuid().ToString("N"));
    private readonly SessionLayout _layout;

    public SessionLayoutTests()
    {
        Directory.CreateDirectory(_root);
        _layout = new SessionLayout(_root);
    }

    [Fact]
    public void Constructor_NormalizesSessionDirectory()
    {
        var layout = new SessionLayout(Path.Combine(_root, ".", "child", ".."));
        Assert.Equal(Path.GetFullPath(_root), layout.Directory, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("yo", "yo.trn")]
    [InlineData("cliente", "cliente.trn")]
    public void AudioTrack_UsesCanonicalEncryptedNames(string track, string expected)
    {
        Assert.Equal(Path.Combine(_root, expected), _layout.AudioTrack(track));
    }

    [Fact]
    public void ExistingAudioTrack_PrefersEncryptedAndFallsBackToLegacyWave()
    {
        var legacy = _layout.AudioTrack(SessionLayout.MicrophoneTrackName, encrypted: false);
        File.WriteAllBytes(legacy, [1, 2, 3]);
        Assert.Equal(legacy, _layout.ExistingAudioTrack(SessionLayout.MicrophoneTrackName));

        var encrypted = _layout.AudioTrack(SessionLayout.MicrophoneTrackName);
        File.WriteAllBytes(encrypted, [4, 5, 6]);
        Assert.Equal(encrypted, _layout.ExistingAudioTrack(SessionLayout.MicrophoneTrackName));
    }

    [Fact]
    public void ExistingAudioTrack_ReturnsNullWhenNoTrackExists()
    {
        Assert.Null(_layout.ExistingAudioTrack(SessionLayout.SystemTrackName));
    }

    [Fact]
    public void Transcript_OnlyAllowsKnownArtifacts()
    {
        Assert.Equal(Path.Combine(_root, "transcripcion.srt.trn"), _layout.Transcript("transcripcion.srt"));
        Assert.Equal(Path.Combine(_root, "transcripcion.srt"), _layout.Transcript("transcripcion.srt", encrypted: false));
        Assert.Throws<ArgumentException>(() => _layout.Transcript("../outside.txt"));
    }

    [Fact]
    public void IncompleteMarker_UsesCanonicalName()
    {
        Assert.Equal(Path.Combine(_root, ".recording-incomplete"), _layout.IncompleteMarker);
    }

    [Fact]
    public void Temporary_CreatesUniquePathsInsideSession()
    {
        var first = _layout.Temporary("normalized", ".wav");
        var second = _layout.Temporary("normalized", ".wav");

        Assert.NotEqual(first, second);
        Assert.Equal(_root, Path.GetDirectoryName(first), StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith(".", Path.GetFileName(first));
        Assert.EndsWith(".normalized.wav", first);
    }

    [Fact]
    public void AudioTrack_RejectsUnknownTrackNames()
    {
        Assert.Throws<ArgumentException>(() => _layout.AudioTrack("../yo"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

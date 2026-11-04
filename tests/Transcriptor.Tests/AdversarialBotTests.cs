using System.Text;
using Transcriptor.Desktop;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

/// <summary>
/// Deterministic adversarial personas. Each bot attacks a real application boundary;
/// no audio hardware, network service, or wall-clock timing is required.
/// </summary>
public sealed class AdversarialBotTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "TranscriptorAdversarialBots", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ConfigurationChaosBot_RoundTripsHostileButValidPreferences()
    {
        var path = Path.Combine(_root, "settings", "settings.json");
        var store = new SettingsStore(path);
        var payloads = new[]
        {
            new AppSettings { RecordingsDirectory = "  C:\\Calls\\日本語\\🎙  ", Language = " ES ", RetentionDays = int.MinValue },
            new AppSettings { WhisperExecutablePath = new string('x', 2048), Language = "PT-br", RetentionDays = int.MaxValue },
            new AppSettings { RecordingsDirectory = "relative/../recordings", Language = "\t", RetentionDays = 1 },
            new AppSettings { MicrophoneDeviceId = "  {device}:\r\n\\0  ", OutputDeviceId = "speaker/../id", RetentionDays = 3650 }
        };

        foreach (var payload in payloads)
        {
            store.Save(payload);
            var loaded = store.Load();

            Assert.Null(loaded.Warning);
            Assert.Equal(payload.RecordingsDirectory?.Trim() ?? string.Empty, loaded.Settings.RecordingsDirectory);
            Assert.Equal(payload.WhisperExecutablePath?.Trim() ?? string.Empty, loaded.Settings.WhisperExecutablePath);
            Assert.Equal(payload.Language?.Trim().ToLowerInvariant() is { Length: > 0 } language ? language : "es", loaded.Settings.Language);
            Assert.InRange(loaded.Settings.RetentionDays, 0, 3650);
        }
    }

    [Fact]
    public void PathTraversalBot_CannotDeleteOutsideOrEscapeThroughDotSegments()
    {
        var date = Path.Combine(_root, "recordings", "2026-01-01");
        var session = Path.Combine(date, "legitimate");
        var victim = Path.Combine(_root, "recordings", "victim");
        Directory.CreateDirectory(session);
        Directory.CreateDirectory(victim);
        File.WriteAllText(Path.Combine(session, "yo.wav"), "audio");
        var sentinel = Path.Combine(victim, "keep.txt");
        File.WriteAllText(sentinel, "must survive");

        Assert.Throws<InvalidOperationException>(() =>
            RecordingPrivacy.DeleteSession(Path.Combine(_root, "recordings"),
                Path.Combine(date, "..", "victim")));
        Assert.Throws<InvalidOperationException>(() =>
            RecordingPrivacy.DeleteSession(Path.Combine(_root, "recordings"), victim));

        Assert.True(File.Exists(sentinel));
        Assert.True(Directory.Exists(session));
    }

    [Fact]
    public void RetentionBoundaryBot_DoesNotDeleteSessionExactlyAtCutoff()
    {
        var session = Path.Combine(_root, "recordings", "2026-01-01", "boundary");
        Directory.CreateDirectory(session);
        var track = Path.Combine(session, "yo.wav");
        File.WriteAllText(track, "audio");
        var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var cutoff = now.AddDays(-7);
        File.SetLastWriteTimeUtc(track, cutoff);

        var deleted = RecordingPrivacy.ApplyRetention(Path.Combine(_root, "recordings"), 7, now);

        Assert.DoesNotContain(session, deleted);
        Assert.True(File.Exists(track));
    }

    [Fact]
    public void TranscriptPoisoningBot_RejectsSegmentThatEndsBeforeItStarts()
    {
        var path = Path.Combine(_root, "poisoned.srt");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, """
            1
            00:00:12,000 --> 00:00:11,999
            Este segmento tiene el tiempo invertido.
            """);

        Assert.Throws<InvalidDataException>(() => SrtMerger.Parse(path, "Atacante"));
    }

    [Fact]
    public void TranscriptFloodBot_HandlesManySegmentsAndReturnsStableChronologicalOrder()
    {
        Directory.CreateDirectory(_root);
        var microphone = new StringBuilder();
        var client = new StringBuilder();
        for (var index = 0; index < 250; index++)
        {
            microphone.AppendLine((index + 1).ToString());
            var microphoneSeconds = 249 - index;
            microphone.AppendLine($"00:{microphoneSeconds / 60:D2}:{microphoneSeconds % 60:D2},000 --> 00:{microphoneSeconds / 60:D2}:{microphoneSeconds % 60:D2},500");
            microphone.AppendLine($"Micrófono {index}");
            microphone.AppendLine();
            client.AppendLine((index + 1).ToString());
            client.AppendLine($"00:{index / 60:D2}:{index % 60:D2},000 --> 00:{index / 60:D2}:{index % 60:D2},500");
            client.AppendLine($"Sistema {index}");
            client.AppendLine();
        }

        File.WriteAllText(Path.Combine(_root, "yo.srt"), microphone.ToString());
        File.WriteAllText(Path.Combine(_root, "cliente.srt"), client.ToString());

        var merged = SrtMerger.Merge(_root);

        Assert.Equal(500, merged.Count);
        Assert.Equal(merged.OrderBy(segment => segment.Start).ThenBy(segment => segment.Speaker, StringComparer.Ordinal), merged);
        Assert.Contains(merged, segment => segment.Text == "Micrófono 249");
        Assert.Contains(merged, segment => segment.Text == "Sistema 249");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

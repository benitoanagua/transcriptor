using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class TranscriptionSessionLeaseTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "TranscriptorSessionLeaseTests", Guid.NewGuid().ToString("N"));

    public TranscriptionSessionLeaseTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void TryAcquire_RejectsSecondOwnerAndAllowsReacquisitionAfterDispose()
    {
        using (var first = TranscriptionSessionLease.TryAcquire(_directory))
        {
            Assert.NotNull(first);
            Assert.Null(TranscriptionSessionLease.TryAcquire(_directory));
        }

        using var next = TranscriptionSessionLease.TryAcquire(_directory);
        Assert.NotNull(next);
    }

    [Fact]
    public void TryAcquire_RejectsBlankDirectory()
    {
        Assert.Throws<ArgumentException>(() => TranscriptionSessionLease.TryAcquire("  "));
    }

    [Fact]
    public void TryAcquire_RejectsMissingDirectory()
    {
        var missing = Path.Combine(_directory, "does-not-exist");
        Assert.Throws<DirectoryNotFoundException>(() => TranscriptionSessionLease.TryAcquire(missing));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}

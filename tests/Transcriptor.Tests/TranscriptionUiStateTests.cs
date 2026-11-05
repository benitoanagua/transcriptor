using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class TranscriptionUiStateTests
{
    [Fact]
    public void TryBegin_RejectsDuplicateTranscription()
    {
        var state = new TranscriptionUiState();

        Assert.False(state.IsTranscribing);
        Assert.True(state.TryBegin());
        Assert.True(state.IsTranscribing);
        Assert.False(state.TryBegin());
    }

    [Fact]
    public void Complete_RestoresIdleState()
    {
        var state = new TranscriptionUiState();
        Assert.True(state.TryBegin());

        state.Complete();

        Assert.False(state.IsTranscribing);
        Assert.True(state.TryBegin());
    }

    [Fact]
    public void Complete_IsIdempotent()
    {
        var state = new TranscriptionUiState();

        state.Complete();
        state.Complete();

        Assert.False(state.IsTranscribing);
        Assert.True(state.TryBegin());
    }
}

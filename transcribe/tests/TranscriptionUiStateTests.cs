using Transcriptor.Desktop;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class TranscriptionUiStateTests
{
    [Fact]
    public void TryBegin_RejectsDuplicateTranscriptionAndDisablesConflictingActions()
    {
        var state = new TranscriptionUiState();

        Assert.True(state.TryBegin());
        Assert.False(state.TryBegin());
        Assert.True(state.IsTranscribing);
        Assert.False(state.CanStartRecording);
        Assert.False(state.CanDeleteSession);
        Assert.False(state.CanMigrateSession);
        Assert.False(state.CanRefreshLibrary);
        Assert.True(state.CanCancel);
    }

    [Fact]
    public void Complete_RestoresIdleActionsAfterSuccessOrCancellation()
    {
        var state = new TranscriptionUiState();
        Assert.True(state.TryBegin());

        state.Complete();

        Assert.False(state.IsTranscribing);
        Assert.True(state.CanStartRecording);
        Assert.True(state.CanDeleteSession);
        Assert.True(state.CanMigrateSession);
        Assert.True(state.CanRefreshLibrary);
        Assert.False(state.CanCancel);
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

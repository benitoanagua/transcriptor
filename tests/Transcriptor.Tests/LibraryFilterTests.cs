using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Tests;

public sealed class LibraryFilterTests
{
    private sealed class Entry(string title, string path, bool mic, bool client, bool transcript, bool incomplete)
        : ILibraryEntry
    {
        public string Title { get; } = title;
        public string DirectoryPath { get; } = path;
        public bool HasMicrophone { get; } = mic;
        public bool HasClient { get; } = client;
        public bool HasTranscript { get; } = transcript;
        public bool IsIncomplete { get; } = incomplete;
    }

    private static readonly Entry A = new("Llamada", "/r/2026-01-01/120000_zeta", true, true, false, false);
    private static readonly Entry B = new("Soporte", "/r/2026-01-02/090000_alpha", true, false, false, true);
    private static readonly Entry C = new("Cliente", "/r/2026-01-02/150000_mike", false, true, false, false);
    private static readonly Entry D = new("VL", "/r/2026-01-03/180000_beta", true, true, true, false);
    private static readonly Entry[] All = [A, B, C, D];

    [Fact]
    public void All_ReturnsEverythingWithoutQuery()
    {
        Assert.Equal(All, LibraryFilter.Apply(All, "", LibraryFilter.All));
        Assert.Equal(All, LibraryFilter.Apply(All, "   ", LibraryFilter.All));
        Assert.Equal(All, LibraryFilter.Apply(All, null!, LibraryFilter.All));
    }

    private static int[] IndexOf(IEnumerable<Entry> items) => items.Select(item => Array.IndexOf(All, item)).ToArray();

    [Theory]
    [InlineData("llamada", new[] { 0 })]
    [InlineData("soporte", new[] { 1 })]
    [InlineData("SOPORTE", new[] { 1 })]
    [InlineData("/2026-01-03", new[] { 3 })]
    [InlineData("no existe", new int[0])]
    [InlineData("  ", new[] { 0, 1, 2, 3 })]
    public void Query_MatchesTitleOrPathCaseInsensitive(string query, int[] expected)
        => Assert.Equal(expected, IndexOf(LibraryFilter.Apply(All, query, LibraryFilter.All)));

    [Theory]
    [InlineData(LibraryFilter.BothTracks, new[] { 0, 3 })]
    [InlineData(LibraryFilter.MicrophoneOnly, new[] { 1 })]
    [InlineData(LibraryFilter.SystemOnly, new[] { 2 })]
    [InlineData(LibraryFilter.WithTranscript, new[] { 3 })]
    [InlineData(LibraryFilter.Incomplete, new[] { 1 })]
    public void Filters_MatchByTrackCombinationAndStatus(int filter, int[] expected)
        => Assert.Equal(expected, IndexOf(LibraryFilter.Apply(All, "", filter)));

    [Fact]
    public void QueryAndFilter_Combine()
    {
        var result = LibraryFilter.Apply(All, "2026-01-02", LibraryFilter.MicrophoneOnly);
        Assert.Single(result, B);
    }

    [Theory]
    [InlineData(LibrarySort.RecentFirst, new[] { 3, 2, 1, 0 })]
    [InlineData(LibrarySort.OldestFirst, new[] { 0, 1, 2, 3 })]
    [InlineData(LibrarySort.Name, new[] { 1, 3, 2, 0 })]
    public void SortOrdersByDateOrName(int sortMode, int[] expected)
    {
        var result = LibrarySort.Apply(All, sortMode);
        Assert.Equal(expected, result.Select(item => Array.IndexOf(All, item)));
    }
}

using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The multiplayer scripts' scroll control off engine, over the chat pane's growing list. The
/// script's RAA follows new lines at the bottom and holds still once the reader scrolls up.
/// </summary>
public class OriginalScrollBarTests
{
    // The chat pane's control, KG, over a ten-line window.
    private static readonly OriginalScrollBar Chat = OriginalRaceResults.SplitsBar with { Rows = 10 };

    [Fact]
    public void AWindowAtTheBottomFollowsANewLine()
    {
        Assert.False(Chat.CanDown(2, 12));
        Assert.Equal(3, Chat.Follow(2, held: false, 13));
    }

    [Fact]
    public void AWindowScrolledUpToTheOldestLineHoldsStillAsLinesArrive()
    {
        Assert.True(Chat.CanDown(0, 12));
        Assert.Equal(0, Chat.Follow(0, held: true, 13));
        Assert.Equal(0, Chat.Follow(0, held: true, 64));
    }

    [Fact]
    public void AChatThatFitsShowsNoBarAndStandsAtItsFirstLine()
    {
        Assert.False(Chat.Scrolls(10));
        Assert.True(Chat.Scrolls(11));
        Assert.Equal(0, Chat.Follow(0, held: false, 7));
    }
}

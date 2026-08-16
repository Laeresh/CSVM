using System;
using System.Collections.Generic;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Board menu cursor rules off-engine: the harmless row is highlighted first, the cursor wraps the
/// way the launchscreen's does, confirm reports the highlighted row, and only a dismissable menu
/// answers back.
/// </summary>
public class BoardMenuTests
{
    [Fact]
    public void OpensOnTheFirstItem()
    {
        Assert.Equal(0, Pause().Index);
        Assert.Equal(0, Results().Index);
    }

    [Fact]
    public void ConfirmReportsTheHighlightedItem()
    {
        var menu = Pause();
        var fired = new List<BoardMenuItem>();
        menu.Activated += fired.Add;

        menu.Handle(move: 0, accept: true, back: false);
        menu.Handle(move: 1, accept: true, back: false);

        Assert.Equal(new[] { BoardMenuItem.Resume, BoardMenuItem.Restart }, fired);
    }

    [Fact]
    public void CursorWrapsBothWays()
    {
        var menu = Pause();

        Assert.True(menu.Handle(move: -1, accept: false, back: false));
        Assert.Equal(2, menu.Index);

        Assert.True(menu.Handle(move: 1, accept: false, back: false));
        Assert.Equal(0, menu.Index);
    }

    [Fact]
    public void HandleReportsOnlyRealCursorMoves()
    {
        var menu = Pause();

        Assert.False(menu.Handle(move: 0, accept: false, back: false));
        Assert.True(menu.Handle(move: 1, accept: false, back: false));
    }

    [Fact]
    public void ASingleItemMenuNeverMoves()
    {
        var menu = new BoardMenu(dismissable: false, (BoardMenuItem.Exit, "Quit Game"));

        Assert.False(menu.Handle(move: 1, accept: false, back: false));
        Assert.Equal(0, menu.Index);
    }

    [Fact]
    public void OnlyADismissableMenuAnswersBack()
    {
        int pauseDismissed = 0, resultsDismissed = 0;
        var pause = Pause();
        var results = Results();
        pause.Dismissed += () => pauseDismissed++;
        results.Dismissed += () => resultsDismissed++;

        pause.Handle(move: 0, accept: false, back: true);
        results.Handle(move: 0, accept: false, back: true);

        Assert.Equal(1, pauseDismissed);
        Assert.Equal(0, resultsDismissed);
    }

    [Fact]
    public void ConfirmWinsOverBackInTheSameFrame()
    {
        var menu = Pause();
        int activated = 0, dismissed = 0;
        menu.Activated += _ => activated++;
        menu.Dismissed += () => dismissed++;

        menu.Handle(move: 0, accept: true, back: true);

        Assert.Equal(1, activated);
        Assert.Equal(0, dismissed);
    }

    [Fact]
    public void TheCursorMovesBeforeTheConfirmItReports()
    {
        var menu = Pause();
        BoardMenuItem? fired = null;
        menu.Activated += item => fired = item;

        menu.Handle(move: 1, accept: true, back: false);

        Assert.Equal(BoardMenuItem.Restart, fired);
    }

    [Fact]
    public void ResetReturnsToTheHarmlessRow()
    {
        var menu = Pause();
        menu.Handle(move: -1, accept: false, back: false);

        menu.Reset();

        Assert.Equal(0, menu.Index);
    }

    [Fact]
    public void AnEmptyMenuIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new BoardMenu(dismissable: true));
    }

    private static BoardMenu Pause() => new(
        dismissable: true,
        (BoardMenuItem.Resume, "Resume"),
        (BoardMenuItem.Restart, "Restart"),
        (BoardMenuItem.Exit, "Exit to Menu"));

    private static BoardMenu Results() => new(
        dismissable: false,
        (BoardMenuItem.Restart, "Restart"),
        (BoardMenuItem.Exit, "Exit to Menu"));
}

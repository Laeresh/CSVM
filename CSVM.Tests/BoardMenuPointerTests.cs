using System;
using System.Collections.Generic;
using CSVM.UI.Boards;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The pointer rule both pause boards share, off-engine over four rows ten units tall. Entering a
/// row moves the cursor, and a press fires only when let go on the row it took hold of. A resting
/// pointer leaves the pad free. A board that settles its first sight keeps its first row focused
/// under a pointer that was already there.
/// </summary>
public class BoardMenuPointerTests
{
    private static readonly Func<float, float, int> RowAt = (_, y) => y >= 0f && y < 40f ? (int)(y / 10f) : -1;

    [Fact]
    public void EnteringARowMovesTheCursor()
    {
        var (menu, _) = Menu();
        var pointer = Primed(settles: false);

        pointer.Step(menu, (5f, 25f, false), RowAt);

        Assert.Equal(2, menu.Index);
    }

    [Fact]
    public void APointerMovingWithinItsRowLeavesThePadFree()
    {
        var (menu, _) = Menu();
        var pointer = Primed(settles: false);
        pointer.Step(menu, (5f, 25f, false), RowAt);

        menu.Handle(move: 1, accept: false, back: false);
        pointer.Step(menu, (6f, 26f, false), RowAt);

        Assert.Equal(3, menu.Index);
    }

    [Fact]
    public void AReleaseOnThePressedRowFiresIt()
    {
        var (menu, fired) = Menu();
        var pointer = Primed(settles: false);

        pointer.Step(menu, (5f, 15f, true), RowAt);
        Assert.True(pointer.Held);
        Assert.Empty(fired);
        pointer.Step(menu, (5f, 15f, false), RowAt);

        Assert.Equal(new[] { BoardMenuItem.Photo }, fired);
    }

    [Fact]
    public void AReleaseOffThePressedRowFiresNothing()
    {
        var (menu, fired) = Menu();
        var pointer = Primed(settles: false);

        pointer.Step(menu, (5f, 15f, true), RowAt);
        pointer.Step(menu, (5f, 80f, true), RowAt);
        pointer.Step(menu, (5f, 80f, false), RowAt);

        Assert.Empty(fired);
    }

    [Fact]
    public void TheReleaseFiresThePressedRowEvenIfThePadMovedTheCursor()
    {
        var (menu, fired) = Menu();
        var pointer = Primed(settles: false);

        pointer.Step(menu, (5f, 15f, true), RowAt);
        menu.Handle(move: 1, accept: false, back: false);
        pointer.Step(menu, (5f, 15f, false), RowAt);

        Assert.Equal(new[] { BoardMenuItem.Photo }, fired);
    }

    [Fact]
    public void AButtonHeldThroughThePrimeIsNotAClick()
    {
        var (menu, fired) = Menu();
        var pointer = new BoardMenuPointer();
        pointer.Prime(pressed: true);

        pointer.Step(menu, (5f, 35f, true), RowAt);
        pointer.Step(menu, (5f, 35f, false), RowAt);

        Assert.Empty(fired);
    }

    [Fact]
    public void ASettlingBoardKeepsItsFirstRowUnderAPointerAlreadyThere()
    {
        var (menu, _) = Menu();
        var pointer = Primed(settles: true);

        pointer.Step(menu, (5f, 25f, false), RowAt);
        Assert.Equal(0, menu.Index);
        pointer.Step(menu, (5f, 15f, false), RowAt);

        Assert.Equal(1, menu.Index);
    }

    [Fact]
    public void ASettlingBoardStillTakesAClickOnItsFirstSight()
    {
        var (menu, fired) = Menu();
        var pointer = Primed(settles: true);

        pointer.Step(menu, (5f, 25f, true), RowAt);
        pointer.Step(menu, (5f, 25f, false), RowAt);

        Assert.Equal(new[] { BoardMenuItem.Restart }, fired);
    }

    [Fact]
    public void NoPointerLeavesTheCursorWhereItStands()
    {
        var (menu, _) = Menu();
        var pointer = Primed(settles: false);
        pointer.Step(menu, (5f, 25f, false), RowAt);

        pointer.Step(menu, null, RowAt);

        Assert.Equal(2, menu.Index);
        Assert.Null(pointer.At);
    }

    private static BoardMenuPointer Primed(bool settles)
    {
        var pointer = new BoardMenuPointer { SettlesFirstSight = settles };
        pointer.Prime(pressed: false);
        return pointer;
    }

    private static (BoardMenu Menu, List<BoardMenuItem> Fired) Menu()
    {
        var menu = new BoardMenu(
            dismissable: true,
            (BoardMenuItem.Resume, "Resume"),
            (BoardMenuItem.Photo, "Photo Mode"),
            (BoardMenuItem.Restart, "Restart"),
            (BoardMenuItem.Exit, "Quit Game"));
        var fired = new List<BoardMenuItem>();
        menu.Activated += fired.Add;
        return (menu, fired);
    }
}

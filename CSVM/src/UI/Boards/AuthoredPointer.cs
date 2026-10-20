using System;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// A seat's mouse on a board drawn in the original's authored 800x600 space. The viewport's pointer
/// (<see cref="ScreenPointer"/>) maps back through <see cref="BoardFit"/> into authored pixels, with
/// the left button. The
/// Original race and pause boards read their pointer here. Module entry: docs/architecture/UI.md.
/// </summary>
public static class AuthoredPointer
{
    /// <summary>The pointer over <paramref name="board"/>'s viewport in authored pixels and whether
    /// its left button is down. Only a seat whose <paramref name="input"/> reads the keyboard holds
    /// one (MenuInput's rule for seat 0), so a pad player's board is driven by the pad alone. A
    /// board out of the tree has none either.</summary>
    public static (float X, float Y, bool Pressed)? Of(Control board, MenuInput? input)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (input is not { Keyboard: true } || !board.IsInsideTree())
        {
            return null;
        }

        if (ScreenPointer.Read(board.GetViewport()) is not { } pointer)
        {
            return null;
        }

        var size = board.GetViewportRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        var at = pointer.At;
        return ((at.X - fit.OriginX) / fit.Scale, (at.Y - fit.OriginY) / fit.Scale, pointer.Pressed);
    }
}

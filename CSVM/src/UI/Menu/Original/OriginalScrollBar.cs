using System;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// One of the multiplayer scripts' scroll controls (<c>@ctl@EO</c>) over a list of
/// <paramref name="Rows"/> visible rows. The up arrow stands at (<paramref name="X"/>,
/// <paramref name="Y"/>) and the down arrow at its foot, <paramref name="Height"/> below. The thumb
/// runs on the track between them, over <paramref name="Track"/> (the script's <c>KF</c>) where that
/// is opaque. The scripts deactivate it while the list fits, so nothing of it stands then.
/// Engine-free. Module entry: docs/architecture/UI.md.
/// </summary>
public readonly record struct OriginalScrollBar(float X, float Y, float Height, int Rows, BoardTint? Track = null)
{
    /// <summary>An arrow's width, a frame of <see cref="UpArt"/>.</summary>
    public const float ArrowWidth = 16f;

    /// <summary>An arrow's height.</summary>
    public const float ArrowHeight = 11f;

    /// <summary>The up arrow's four-frame strip, which the scripts' spinners wear too.</summary>
    public const string UpArt = "MP_B_SCROLLUP.PNG";

    /// <summary>The down arrow's four-frame strip.</summary>
    public const string DownArt = "MP_B_SCROLLDOWN.PNG";

    /// <summary>The thumb, drawn stretched to the window's share of the track.</summary>
    public const string ThumbArt = "MP_B_SCROLLBAR.PNG";

    /// <summary>The down arrow's top.</summary>
    public float DownY => Y + Height - ArrowHeight;

    /// <summary>Whether a list of <paramref name="count"/> rows outgrows the window, which is when
    /// the bar stands.</summary>
    public bool Scrolls(int count) => count > Rows;

    /// <summary>A first row clamped to the tops a list of <paramref name="count"/> can show.</summary>
    public int Clamp(int top, int count) => Math.Clamp(top, 0, Math.Max(0, count - Rows));

    /// <summary>Whether the up arrow can move a window at <paramref name="top"/>.</summary>
    public bool CanUp(int top) => top > 0;

    /// <summary>Whether the down arrow can move a window at <paramref name="top"/>.</summary>
    public bool CanDown(int top, int count) => top + Rows < count;

    /// <summary>The window as the pointer's wheel and thumb see it, its box running from
    /// <paramref name="listX"/> to the bar's right edge.</summary>
    public ListWindow Window(float listX, int count, int top)
    {
        float track = Height - (2f * ArrowHeight);
        float thumb = ListWindow.ThumbHeightFor(track, Rows, count, ArrowHeight);
        top = Clamp(top, count);
        return new ListWindow(listX, Y, X + ArrowWidth - listX, Height, X,
            ListWindow.ThumbYFor(Y + ArrowHeight, track, thumb, top, Math.Max(0, count - Rows)), ArrowWidth, thumb,
            Y + ArrowHeight, track, count, Rows, top);
    }

    /// <summary>The track and the thumb for a window at <paramref name="top"/>, nothing while the
    /// list fits.</summary>
    public void Compose(float listX, int count, int top, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        if (!Scrolls(count))
        {
            return;
        }

        var window = Window(listX, count, top);
        if (Track is { } track)
        {
            layers.Fills.Add(new BoardFill(X, window.TrackTop, ArrowWidth, window.TrackHeight, track.R, track.G, track.B));
        }

        layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, ThumbArt), window.ThumbX, window.ThumbY,
            Height: window.ThumbHeight));
    }

    /// <summary>An arrow as a shell row in the cursor's right column, live while it can move a
    /// window at <paramref name="top"/>.</summary>
    public OriginalRow ArrowRow(string key, bool down, int count, int top) =>
        new(key, string.Empty, OriginalRowKind.Button, X, down ? DownY : Y, ArrowWidth, ArrowHeight,
            down ? CanDown(top, count) : CanUp(top), 1, new BoardArt(BoardArtLibrary.Ui, down ? DownArt : UpArt, 4));

    /// <summary>An arrow as a picture, for a board with no shell rows. It takes the disabled frame
    /// where it cannot move the window, else the plaque frames for focus and press.</summary>
    public BoardPicture Arrow(bool down, bool enabled, bool focused, bool pressed) =>
        new(new BoardArt(BoardArtLibrary.Ui, down ? DownArt : UpArt, 4), X, down ? DownY : Y,
            enabled ? ComposedBoard.PlaqueFrame(4, focused, pressed) : 0);
}

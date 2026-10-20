using System;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// Where the theater screen stands in front of the seated player and how finely it is drawn,
/// engine-free. The screen is a flat quad in the XR origin's frame showing the root's whole 2D
/// layout. It stands <see cref="DistanceM"/> ahead of the seated eye and <see cref="DropM"/> below
/// it. It is sized so the authored 800x600 board <see cref="BoardFit"/> inscribes in that layout is
/// <see cref="HeightM"/> tall, each authored pixel drawn <see cref="AuthoredScale"/> texels
/// across. Which canvas layers it shows is <see cref="Hosts"/>. The reasoning behind the
/// defaults is in docs/architecture/UI.md on TheaterScreen.
/// </summary>
public readonly record struct TheaterLayout(float DistanceM, float HeightM, float DropM)
{
    /// <summary>Texels per authored board pixel on each axis. A whole number keeps nearest-filtered
    /// art on whole texels. Two puts 1200 rows on the board's 34 degrees, 35 per degree. A current
    /// headset shows 20 to 25 pixels per degree.</summary>
    public const int AuthoredScale = 2;

    /// <summary>Each eye's render of a cutscene: the authored board's 4:3 at <see cref="AuthoredScale"/>,
    /// the shape the letterbox card was authored for. It plays where a board stands, as finely drawn.
    /// TUNE: the flat game widens the same picture to a wider window.</summary>
    public static readonly Vector2I CutsceneSize =
        new(BoardFit.AuthoredWidth * AuthoredScale, BoardFit.AuthoredHeight * AuthoredScale);

    /// <summary>The proposed seat: the board 2.5 m ahead and 1.53 m tall (34 degrees), its centre
    /// 0.1 m under the eye. That is about 18 authored pixels per degree. TUNE at the headset.</summary>
    public static readonly TheaterLayout Default = new(2.5f, 1.53f, 0.1f);

    /// <summary>The screen's centre in the XR origin's frame, facing the seated eye.</summary>
    public Transform3D Pose => new(Basis.Identity, new Vector3(0f, -DropM, -DistanceM));

    /// <summary>The angle the board's height spans from the seated eye, in degrees.</summary>
    public float VerticalDeg =>
        Mathf.RadToDeg(MathF.Atan(((HeightM * 0.5f) + DropM) / DistanceM) + MathF.Atan(((HeightM * 0.5f) - DropM) / DistanceM));

    /// <summary>Whether the theater screen shows a canvas layer of this number. It shows the boards,
    /// menus, load screen and cinemas, and the readouts drawn over them. Everything under
    /// <see cref="HudLayers.Board"/> is flight chrome, which is not the screen's.</summary>
    public static bool Hosts(int layer) => layer >= HudLayers.Board;

    /// <summary>Whether a showing layer of this number brings the screen up in flight: a board or
    /// a cinema. The readouts over them never do on their own.</summary>
    public static bool Raises(int layer) => layer == HudLayers.Board || layer == HudLayers.Cinema;

    /// <summary>How many surface texels the root's 2D layout draws per layout pixel, so an authored
    /// board pixel is <see cref="AuthoredScale"/> texels whatever size the layout is.</summary>
    public static float SurfaceScale(Vector2 layout) => AuthoredScale / BoardFit.For(layout.X, layout.Y).Scale;

    /// <summary>The surface's size in texels for a layout of <paramref name="layout"/> pixels.</summary>
    public static Vector2I SurfaceSize(Vector2 layout)
    {
        float s = SurfaceScale(layout);
        return new Vector2I(Math.Max(1, Mathf.RoundToInt(layout.X * s)), Math.Max(1, Mathf.RoundToInt(layout.Y * s)));
    }

    /// <summary>The screen's size in metres for a layout of <paramref name="layout"/> pixels: the
    /// whole layout, at the scale that makes its inscribed board <see cref="HeightM"/> tall.</summary>
    public Vector2 SizeM(Vector2 layout) =>
        layout * (HeightM / (BoardFit.AuthoredHeight * BoardFit.For(layout.X, layout.Y).Scale));

    /// <summary>Where a cutscene plays on a screen showing <paramref name="layout"/>, in its (0, 0)
    /// to (1, 1) points: the 800x600 board <see cref="BoardFit"/> inscribes in that layout.</summary>
    public static Rect2 CutsceneRect(Vector2 layout)
    {
        if (layout.X < 1f || layout.Y < 1f)
        {
            return new Rect2(0f, 0f, 1f, 1f);
        }

        var fit = BoardFit.For(layout.X, layout.Y);
        return new Rect2(fit.OriginX / layout.X, fit.OriginY / layout.Y,
            BoardFit.AuthoredWidth * fit.Scale / layout.X, BoardFit.AuthoredHeight * fit.Scale / layout.Y);
    }

    /// <summary>Surface rows per degree of view on the board.</summary>
    public float RowsPerDegree() => BoardFit.AuthoredHeight * AuthoredScale / VerticalDeg;

    /// <summary>Where a ray from <paramref name="from"/> along <paramref name="dir"/> meets the screen
    /// showing <paramref name="layout"/>, both in the XR origin's frame. (0, 0) is its top left and
    /// (1, 1) its bottom right. Null for a ray parallel to it, pointing away, or passing beside it.</summary>
    public Vector2? Hit(Vector3 from, Vector3 dir, Vector2 layout)
    {
        var centre = Pose.Origin;
        if (MathF.Abs(dir.Z) < 1e-6f)
        {
            return null;
        }

        float t = (centre.Z - from.Z) / dir.Z;
        if (t <= 0f)
        {
            return null;
        }

        var at = from + (dir * t) - centre;
        var size = SizeM(layout);
        float u = (at.X / size.X) + 0.5f;
        float v = 0.5f - (at.Y / size.Y);
        return u is >= 0f and <= 1f && v is >= 0f and <= 1f ? new Vector2(u, v) : null;
    }

    /// <summary>Where a screen point <paramref name="uv"/> stands in the XR origin's frame, for a
    /// layout <paramref name="layout"/> in size: the inverse of <see cref="Hit"/>.</summary>
    public Vector3 PointAt(Vector2 uv, Vector2 layout)
    {
        var size = SizeM(layout);
        return Pose.Origin + new Vector3((uv.X - 0.5f) * size.X, (0.5f - uv.Y) * size.Y, 0f);
    }

    /// <summary>The point of the root's 2D layout a screen point <paramref name="uv"/> shows, for a
    /// layout <paramref name="layout"/> in size: what a pointer's mouse events carry.</summary>
    public static Vector2 LayoutPoint(Vector2 uv, Vector2 layout) => uv * layout;
}

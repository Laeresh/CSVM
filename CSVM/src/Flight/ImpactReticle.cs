using System.IO;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The gun aiming reticle (E37): the game's own <c>impact_point.png</c> pipper drawn at the
/// projected ballistic impact point of the SELECTED gun group's rounds at a fixed convergence
/// distance — deliberately NOT pinned to screen centre. Because the reticle marks where a round
/// fired <i>now</i> would be at that distance, and the rounds inherit the plane's velocity (which
/// lags the nose during a hard roll or pull), the pipper visibly TRAILS the nose in a hard manoeuvre
/// and sits where the rounds land in steady flight (user, 2026-07-22).
///
/// <para>Follows the <see cref="MarkerHud"/> pattern: a viewport-filling <see cref="Control"/> fed a
/// world impact point each frame by <see cref="FlightController"/> (which owns the ballistics and
/// integrates them exactly as <see cref="ProjectilePool"/> fires), projecting it through the live
/// camera at <see cref="_Draw"/> time — no cached projection, so the reticle never lags the chase
/// camera itself. The pipper keeps a fixed screen size (a HUD element), scaled by
/// <see cref="HudMetrics"/>; it does not shrink with range. Draw sizes are TUNE.</para>
/// </summary>
public sealed partial class ImpactReticle : Control
{
    private const float RefSize = 40f; // pipper draw size in px at the 1440p reference (TUNE)

    private Texture2D _texture = null!;
    private Camera3D _camera = null!;

    /// <summary>The world-space ballistic impact point to mark (set each frame by the controller).</summary>
    public Vector3 ImpactPoint { get; set; }

    /// <summary>Whether to draw this frame. False hides the pipper — no firable gun, crashed, or no
    /// valid firing solution. Set by <see cref="FlightController"/>.</summary>
    public bool Active { get; set; }

    /// <summary>Loads a single PNG from the extracted <c>rimage</c> UI set as a texture (the reticle
    /// pipper); null (with one log line) when the file is absent. These images carry their own alpha,
    /// so no colour-keying is needed — unlike the HUD font atlas.</summary>
    public static Texture2D? LoadTexture(string rimageDir, string file)
    {
        var path = Path.Combine(rimageDir, file);
        if (!File.Exists(path))
        {
            GD.Print($"[reticle] no {file} in {rimageDir} — gun reticle off (run ExtractRof.ps1)");
            return null;
        }
        var img = Image.LoadFromFile(path);
        return img != null ? ImageTexture.CreateFromImage(img) : null;
    }

    /// <summary>Builds the reticle over the loaded pipper texture and this player's camera. Add it to
    /// the HUD canvas; feed <see cref="ImpactPoint"/> and <see cref="Active"/> each frame.</summary>
    public static ImpactReticle Build(Texture2D texture, Camera3D camera)
    {
        return new ImpactReticle
        {
            _texture = texture,
            _camera = camera,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
    }

    public override void _Process(double delta)
    {
        // Track the (resizable / splitscreen) pane and repaint — the pipper moves every frame.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // A draw can land before _Process has sized us to the pane; HudMetrics would then read a
        // zero window and the rect would be degenerate. Nothing to draw at zero height anyway.
        if (!Active || Size.Y <= 0f)
        {
            return;
        }
        // A point behind the camera unprojects mirrored through centre — never draw it (as MarkerHud).
        if (_camera.IsPositionBehind(ImpactPoint))
        {
            return;
        }
        var sp = _camera.UnprojectPosition(ImpactPoint);
        float size = RefSize * HudMetrics.Scale(this);
        DrawTextureRect(_texture, new Rect2(sp.X - size / 2f, sp.Y - size / 2f, size, size), false);
    }
}

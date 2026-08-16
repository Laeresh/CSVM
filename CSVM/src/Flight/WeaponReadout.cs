using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The selected-weapon HUD text readout: two lines in the game's own <c>5pointhud</c> bitmap
/// font at the bottom centre of the pane, the currently-selected gun group and rocket type with
/// their live ammo. Text comes from the game's own message templates, resolved through
/// <see cref="Messages"/> and never hardcoded: <c>MSG_HUD_GUNGAUGE</c> / <c>MSG_HUD_MISSLES</c>.
/// <see cref="FlightController"/> pushes the state each frame.
/// ⚠ A null name hides that line (no guns / no hardpoints / no loadout).</summary>
public sealed partial class WeaponReadout : Control
{
    // Per-frame state set by the FlightController. Null name ⇒ that line is hidden.
    public string? GunGroupName;   // %1 of the gun line (the mount name, e.g. "Inner Wing Guns")
    public int GunAmmo;            // %2 of the gun line (the selected group's rounds)
    public string? MissileName;    // %1 of the missile line (the rocket display name)
    public int MissileAmmo;        // %2 of the missile line (the next pylon's rounds, per-pylon)

    private const string GunKey = "MSG_HUD_GUNGAUGE";
    private const string MissileKey = "MSG_HUD_MISSLES"; // the message table's own (mis)spelling
    private const float RefCellHeight = 22f;   // glyph-cell height in px at the 1440p reference (TUNE)
    private const float RefBottomMargin = 64f; // gap from the pane bottom to the lower line (TUNE)
    private const float RefLineGap = 4f;       // extra atlas px between the two stacked lines

    private HudFont _font = null!;
    private string _gunTemplate = "";
    private string _missileTemplate = "";

    /// <summary>Builds the readout over the loaded font + message table, resolving both templates
    /// once (a missing key stays visible as the raw key, per <see cref="Messages.Get"/>).</summary>
    public static WeaponReadout Build(HudFont font, Messages messages)
    {
        var readout = new WeaponReadout
        {
            _font = font,
            _gunTemplate = messages.Get(GunKey),
            _missileTemplate = messages.Get(MissileKey),
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Nearest, // a pixel font
        };
        readout.SetAnchorsPreset(LayoutPreset.FullRect);
        return readout;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        bool gun = GunGroupName != null, missile = MissileName != null;
        if (!gun && !missile)
        {
            return;
        }
        var vp = GetViewportRect().Size;
        float s = HudMetrics.Scale(this);
        float scale = _font.ScaleForHeight(RefCellHeight) * s;
        float lineStep = (_font.PixelHeight + RefLineGap) * scale;
        int lines = (gun ? 1 : 0) + (missile ? 1 : 0);
        // Bottom-anchored (like the dials) so a damped splitscreen pane keeps it on screen.
        float y = vp.Y - RefBottomMargin * s - lines * lineStep;
        if (gun)
        {
            DrawCentered(Messages.Fill(_gunTemplate, GunGroupName, GunAmmo.ToString()), vp.X, y, scale);
            y += lineStep;
        }
        if (missile)
        {
            DrawCentered(Messages.Fill(_missileTemplate, MissileName, MissileAmmo.ToString()), vp.X, y, scale);
        }
    }

    private void DrawCentered(string text, float viewportWidth, float y, float scale)
    {
        float x = (viewportWidth - _font.Measure(text, scale)) * 0.5f;
        _font.Draw(this, text, new Vector2(x, y), scale, bright: true);
    }
}

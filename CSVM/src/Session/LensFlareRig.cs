using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Draws the sun's lens flare (BL-165): a screen-space rig of four sprites strung along
/// the sun→screen-centre vector, plus a full-screen white wash whose opacity rises as the sun
/// nears the centre. Constructed once per session beside <see cref="WeatherRig"/> and ticked from
/// the same per-rig block of <c>GameSession._Process</c>, because everything here is anchored to
/// <i>a</i> camera and splitscreen needs one instance per pane.
///
/// <para><b>The whole spec is measured, not invented.</b> <c>CAP-13</c> (<c>CAP-13 C3.mp4</c>,
/// analysed 2026-08-07 — numbers and stills in <c>playtest/CAP-13/README.md</c>) fixes the element
/// inventory, their positions along the vector, their diameters, the wash falloff and the fade.
/// Where a constant below is a first-pass value awaiting numeric calibration against those stills,
/// it says so on the line.</para>
///
/// <para><b>Two gates, read from two different files, that must agree.</b> The flare needs (1) a
/// gamez node named <c>sun</c> in the chapter's <c>horizon</c> subtree and (2)
/// <c>LensFlareTexture</c> slot registrations in <c>support\&lt;chapter&gt;\init.gw</c>. Across the
/// whole install both are true of <b>C2 and C3 and no other chapter</b> — the sun texture ships in
/// those two chapters only as well. Nothing is hardcoded to a chapter name: if the two gates ever
/// disagree that is a real signal about the data, so it is logged rather than smoothed over.</para>
///
/// <para>⚠ <b>C2's flare is predicted, not verified.</b> The data says C2 should have one; we have
/// no footage of it. Only C3 was captured. Do not treat a C2 flare as matched to anything.</para>
///
/// <para>Unlike the cloud whiteout, the per-pane state is held here rather than on
/// <see cref="PlayerRig"/>: a flare instance is several nodes plus fade state, and putting a
/// Session-namespace type on the Flight-namespace rig would couple them for no gain. The whiteout
/// is a bare <c>ColorRect</c>, which is why it could live there.</para></summary>
public sealed class LensFlareRig
{
    // ── Measured from CAP-13, at 1280×720 ──────────────────────────────────────────────────────
    // Every pixel figure below is normalised by pane HEIGHT against this reference. Godot's default
    // KeepHeight aspect means vertical FOV stays 62° whatever the window or pane shape, so height
    // is the axis that maps to a fixed angle — normalising by it keeps each element the same
    // ANGULAR size as measured, in every pane, at every resolution. The raw measurement is kept in
    // the comments so a decoded FOV would make this a one-line conversion.
    private const float RefHeight = 720f;

    // The wash ("sun blindness"). A plain white alpha composite — settled by arithmetic, not
    // judgement: the dark fuselage (38,2,9) at α 0.66 predicts 181 and measures 179; the compass
    // strip (20,20,18) predicts 175 and measures 178. Two surfaces two orders of magnitude apart
    // in brightness, both within ~2/255.
    //
    // Opacity is ~linear in the sun's screen distance from centre. Fitting the measured table
    // (30 px→0.66, 78→0.63, ~180→0.40, 207→0.34, 320→0.16, 351→0.13) as a line through its two
    // ends reproduces the middle to within 0.05 and reaches zero at ~430 px, matching the recorded
    // "~0 somewhere past ~400 px".
    private const float WashMax = 0.66f;         // measured 0.65–0.68 at the sun near centre
    private const float WashFullPx = 30f;        // inside this the wash is at WashMax
    private const float WashSlopePerPx = 0.001651f;

    // The rig pops in COMPLETE when the sun core crosses into frame, and fades out in ~0.1–0.15 s
    // as it leaves (t=19.75→19.85). So: instant attack, timed release.
    private const float FadeOutSeconds = 0.125f;

    // ── Calibration of the ring intensities ────────────────────────────────────────────────────
    // Measured against CAP-13's own targets, and two things have to match or the comparison is
    // worthless:
    //
    // (1) THE POSE. CAP-13's ring Δlum were read off frames that already carried the wash, and the
    //     wash composites toward white — a true difference d reads as d·(1−α). Solved back from
    //     `measure.py`'s probe coordinates, its two ring poses sit 311 px and 340 px from centre.
    //     Ours is measured at 328 px, inside that band, rather than corrected by a fudge factor.
    // (2) THE STATISTIC. `measure.py`'s `sample()` reports the BRIGHTEST pixel in a window on the
    //     ring, not a median around the annulus. A median is the more robust locator but reads
    //     systematically lower, so calibrating a median up to a brightest-pixel target overshoots.
    //     The numbers below are brightest-pixel, matching.
    //
    // At 328 px, --no-fog, 1280×720: ring 0.50 → 11.0 (target 10–12), ring 0.90 → 17.3 (14–21),
    // ring 2.00 → 5.0 (3–9). Only Ring A needed moving (0.30 → 0.38); the other two landed
    // mid-band untouched and were deliberately NOT nudged, since the bands are the spread across
    // two poses, not error bars, and fitting to the middle would be fitting to my estimator.
    private static readonly Element[] Elements =
    {
        // Core — at the sun, "saturated-white disc, blue-cyan skirt", ~63 px half-max, blooming to
        // ~86 near centre. Measured Δlum: saturating, hence full intensity.
        new(0f, 63f, 1.00f),
        // Ring A — frac 0.50, ~100–104 px, soft wide dim ring. Measured Δlum 10–12; calibrated
        // 0.30 → 0.38, which reads 11.0 (see the calibration note below).
        new(0.50f, 102f, 0.38f),
        // Ring B — frac 0.90, ~42–48 px, the crispest and brightest. Measured Δlum 14–21;
        // reads 17.3, mid-band, uncalibrated.
        new(0.90f, 45f, 0.45f),
        // Ring C — frac 1.95–2.0, ~158–170 px, the faintest. Measured Δlum 3–9; reads 5.0,
        // mid-band, uncalibrated.
        new(2.0f, 164f, 0.18f),
    };

    private readonly SessionSpec _spec;
    private readonly List<Instance> _instances = new();

    public LensFlareRig(SessionSpec spec) => _spec = spec;

    /// <summary>How many panes got a flare — 0 when the chapter authors none, which is every
    /// chapter but C2 and C3.</summary>
    public int InstanceCount => _instances.Count;

    /// <summary>Reads the chapter's <c>LensFlareTexture</c> slot registrations out of the interp
    /// extraction (<c>extracted/interp.json</c>), returning the texture name per slot in slot
    /// order. Empty when the chapter registers none — which is the gate, not an error.
    ///
    /// <para>Mirrors <see cref="Clutter.TemplateNames"/>, which reads <c>adjust.gw</c> from the
    /// same file the same way; the only difference is the verb and that these lines carry an
    /// explicit slot index.</para></summary>
    public static List<string> FlareTextureNames(string interpPath, string chapter)
    {
        var bySlot = new SortedDictionary<int, string>();
        if (!File.Exists(interpPath))
            return new List<string>();
        var wanted = $"support\\{chapter.ToLowerInvariant()}\\init.gw";
        using var doc = JsonDocument.Parse(File.ReadAllBytes(interpPath));
        foreach (var script in doc.RootElement.EnumerateArray())
        {
            if (!script.TryGetProperty("name", out var n)
                || !string.Equals(n.GetString(), wanted, System.StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var line in script.GetProperty("lines").EnumerateArray())
            {
                var parts = (line.GetString() ?? "").Split(' ',
                    System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[0] == "LensFlareTexture"
                    && int.TryParse(parts[1], out int slot))
                    bySlot[slot] = parts[2];
            }
        }
        return new List<string>(bySlot.Values);
    }

    /// <summary>Builds one flare per rig, or nothing at all when either gate is closed. Safe to
    /// call for every chapter — the gates do the deciding.</summary>
    public void Build(IReadOnlyList<PlayerRig> rigs, TextureArchive textures,
        string interpPath, string chapter)
    {
        var names = FlareTextureNames(interpPath, chapter);
        // Gate 1: the registered textures. Gate 2: a `sun` node in this rig's dome, checked per rig
        // below (each pane builds its own horizon copy, so each has its own sun).
        bool anySun = false;
        foreach (var rig in rigs)
            if (FindSun(rig.Horizon) != null)
                anySun = true;

        if (names.Count == 0 || !anySun)
        {
            // Log only when the two gates DISAGREE — that is data telling us something we have not
            // decoded. Both closed is the ordinary case for six of the eight chapters.
            if (names.Count > 0 != anySun)
            {
                Log.Warn("world", $"lens flare gates disagree chapter={chapter} textures={names.Count} sun_node={anySun} flare=off");
            }
            return;
        }

        if (_spec.NoFlare)
        {
            GD.Print($"lens flare: --no-flare, {chapter} flare suppressed "
                     + $"(slots {string.Join(",", names)})");
            return;
        }

        foreach (var rig in rigs)
        {
            var sun = FindSun(rig.Horizon);
            if (sun == null)
                continue;

            var inst = new Instance { Rig = rig, Sun = sun };

            // Sprites: under the HUD, and ordered under the cloud whiteout on the same layer so
            // flying into a cloud swallows the flare with everything else.
            var spriteCanvas = new CanvasLayer { Layer = HudLayers.FlareSprites, Name = "flare" };
            var spriteRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            spriteRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            spriteCanvas.AddChild(spriteRoot);
            rig.HudParent.AddChild(spriteCanvas);
            // Godot adds children last-on-top within a layer; the whiteout canvas is added later
            // (WeatherRig runs after this), so it already wins. Nothing to reorder.

            for (int i = 0; i < Elements.Length && i < names.Count; i++)
            {
                var tex = textures.Find(names[i]);
                if (tex == null)
                    continue;
                var rect = new TextureRect
                {
                    Texture = tex,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    // Additive: the core is measured as *saturating* and blooming ~63→86 px as it
                    // nears centre, which is what an additive blend does and what an alpha blend
                    // toward a fixed colour does not; and Ring C vanishing over the bright gray
                    // deck murk is a faint additive ring over an already-bright background.
                    // ⚠ The one measurement this overrules: the ring annulus reads (194,226,254)
                    // against a 200 sky, i.e. R *below* the background, which additive cannot do.
                    // That dip is 6/255 on a compressed frame next to a saturated highlight — the
                    // regime where chroma subsampling and ringing live — so it is treated as
                    // capture noise. If calibration cannot hit the measured Δlum without blowing
                    // out the core, this is the assumption to revisit first (alpha-blend toward
                    // the texture colour is the alternative; only the blend mode and the
                    // intensities change).
                    Material = new CanvasItemMaterial
                    {
                        BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
                    },
                };
                spriteRoot.AddChild(rect);
                inst.Sprites.Add(rect);
            }

            // The wash: above the HUD, because the original's whitens the compass and the gauge
            // faces at the same α as world pixels.
            var washCanvas = new CanvasLayer { Layer = HudLayers.SunWash, Name = "sun_wash" };
            inst.Wash = new ColorRect
            {
                Color = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            inst.Wash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            washCanvas.AddChild(inst.Wash);
            rig.HudParent.AddChild(washCanvas);

            _instances.Add(inst);
        }

        if (_instances.Count > 0)
        {
            GD.Print($"lens flare [{chapter}]: {_instances.Count} rig(s), "
                     + $"slots {string.Join(",", names)}");
        }
    }

    /// <summary>Per-frame: place the elements, gate them, fade them. Called from the per-rig block
    /// of <c>GameSession._Process</c>, after the dome has been re-anchored.</summary>
    public void Tick(double delta)
    {
        foreach (var inst in _instances)
        {
            var cam = inst.Rig.Camera;
            var viewport = cam.GetViewport();
            var size = viewport.GetVisibleRect().Size;
            if (size.Y <= 0f)
                continue;

            // The dome is camera-anchored, so read the sun as a LOCAL offset from the dome root
            // rather than subtracting global positions: if the dome's re-anchoring ever runs after
            // this tick, a global read would lag by one frame of camera motion while the offset
            // stays exact.
            var sunGlobal = inst.Sun.GlobalPosition;

            bool behind = cam.IsPositionBehind(sunGlobal);
            var screen = cam.UnprojectPosition(sunGlobal);
            bool onScreen = !behind && viewport.GetVisibleRect().HasPoint(screen);

            // Line of sight, for the SPRITES only. One ray to the sun's centre against the
            // colliders the world already has. Everything CAP-13 records falls out of that:
            // terrain and the own plane carry colliders and therefore block; billboard sprites and
            // particles carry none, so the volcano's eruption puffs drifting across the sun cannot
            // occlude — which the footage says they must not. Partial cover with the centre still
            // clear passes (t=18.5, rings persist); full cover blocks it.
            // ⚠ Testing the CENTRE is the simpler of two readings the footage cannot distinguish
            // (a multi-sample disc test fits it equally well). It is chosen because it needs no
            // invented coverage threshold, not because it is decoded.
            bool losClear = true;
            if (onScreen)
            {
                var space = cam.GetWorld3D().DirectSpaceState;
                var query = PhysicsRayQueryParameters3D.Create(cam.GlobalPosition, sunGlobal);
                losClear = space.IntersectRay(query).Count == 0;
            }

            inst.SpriteFade = Step(inst.SpriteFade, onScreen && losClear, delta);
            inst.WashFade = Step(inst.WashFade, onScreen, delta);   // the wash ignores occlusion

            float scale = size.Y / RefHeight;
            var centre = size * 0.5f;
            var toCentre = centre - screen;

            for (int i = 0; i < inst.Sprites.Count; i++)
            {
                var rect = inst.Sprites[i];
                if (inst.SpriteFade <= 0f)
                {
                    rect.Visible = false;
                    continue;
                }
                var e = Elements[i];
                float dia = e.DiaPx * scale;
                var pos = screen + (toCentre * e.Frac);
                rect.Visible = true;
                rect.Size = new Vector2(dia, dia);
                rect.Position = pos - (rect.Size * 0.5f);
                float a = e.Intensity * inst.SpriteFade;
                rect.Modulate = new Color(a, a, a, 1f);
            }

            if (inst.Wash != null)
            {
                // Distance normalised to the reference height, so the falloff is the same angular
                // falloff in a splitscreen pane as in a full window.
                float distPx = toCentre.Length() / scale;
                float alpha = Mathf.Clamp(
                    WashMax - ((distPx - WashFullPx) * WashSlopePerPx), 0f, WashMax);
                inst.Wash.Color = new Color(1f, 1f, 1f, alpha * inst.WashFade);
            }
        }
    }

    /// <summary>Instant attack, timed release — the measured shape: the rig "pops in complete" when
    /// the sun core enters frame and fades over ~0.1–0.15 s as it leaves.</summary>
    private static float Step(float current, bool on, double delta)
        => on ? 1f : Mathf.Max(0f, current - ((float)delta / FadeOutSeconds));

    /// <summary>The <c>sun</c> node inside one rig's dome copy, or null when this chapter's horizon
    /// carries none. Scoped to the dome deliberately: the name is short enough that an unscoped
    /// search could pick up something unrelated elsewhere in the world.</summary>
    private static Node3D? FindSun(Node3D? horizon)
        => horizon?.FindChild("sun", recursive: true, owned: false) as Node3D;

    /// <summary>One flare element: where it sits along the sun→screen-centre vector, how wide it is
    /// at <see cref="RefHeight"/>, and how hard it is drawn. <c>Frac</c> 0 is the sun itself, 1 the
    /// screen centre — Ring C at 2.0 is therefore as far past the centre as the sun is short of
    /// it. Slot order is <c>init.gw</c>'s: slots 0–3 happen to ascend along the vector here,
    /// corroborated by the textures' own appearance (lflare1 is a filled core glow, lflare3 the
    /// crisp bright ring that Ring B is measured to be). That is a fact about this four-element
    /// rig, NOT a decoded rule about the format.</summary>
    private readonly record struct Element(float Frac, float DiaPx, float Intensity);

    private sealed class Instance
    {
        public readonly List<TextureRect> Sprites = new();
        public PlayerRig Rig = null!;
        public Node3D Sun = null!;
        public ColorRect? Wash;
        public float SpriteFade;
        public float WashFade;
    }
}

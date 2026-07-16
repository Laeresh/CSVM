using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Builds a renderable Godot node tree for one aircraft out of a planes.zbd GameZ model.
/// Exterior view only: picks the nearest LOD, skips cockpit/damage/destroyed variants.
///
/// Propellers have several representations under <c>dontmove</c> (see <see cref="PropParts"/>).
/// The default (exterior) build keeps the still <c>staticpropN</c> disc and drops the blur
/// layers; the <c>spinningProps</c> build (free flight) does the reverse — it hides the
/// static disc and keeps the blur discs so a <see cref="Flight.PropAnimator"/> can spin them.
/// Either way the <c>nitropropN</c> boost disc stays hidden (no nitro system yet).
/// </summary>
public sealed class PlaneBuilder
{
    // Non-prop subtrees that make no sense in an exterior view: cockpit interiors are
    // separate (differently-scaled) models; damage/destroyed are alternate states.
    // player_damage_off holds the intact duplicates (pdpNi) of the panels that
    // player_damage_on already provides as pdpN_h — the original engine shows exactly
    // one of the two groups (its vehicle-damage detail toggle); we model "damage on".
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit1", "cockpit2", "destroyed", "shadow", "player_damage_off",
    };

    private readonly GameZ _gamez;
    private readonly SceneBuilder _scene;
    private readonly bool _spinningProps;

    public int MeshInstanceCount => _scene.MeshInstanceCount;

    /// <param name="spinningProps">Free-flight build: hide the static propeller disc and
    /// keep the spinning blur layers (a PropAnimator drives them). Default (exterior view)
    /// keeps the static disc and hides the blur layers.</param>
    public PlaneBuilder(GameZ gamez, TextureArchive textures, bool spinningProps = false)
    {
        _gamez = gamez;
        // The propeller/rotor blur discs (rotorblur/zeprotorblur) are soft sprites — their
        // alpha peaks at ~26%, so the default 1-bit AlphaScissor cutout erases them entirely.
        // Alpha-BLEND them instead (as with the clouds) so the translucent disc shows.
        // cullBackfaces: aircraft interior structure (the gyro's frame lattice) faces
        // inward and must be culled from outside, as the original engine does.
        _scene = new SceneBuilder(gamez, textures, blendTexture: IsPropBlurTexture, cullBackfaces: true);
        _spinningProps = spinningProps;
    }

    private static bool IsPropBlurTexture(string tex) =>
        tex.Contains("blur", StringComparison.OrdinalIgnoreCase);

    // Damage-state panels pdp1..8 (exterior) / pcdpN (cockpit) start INACTIVE in the
    // original: its player_destruct_reset "plane_reset" anim deactivates them and
    // re-activates the healthy pdpN_h panels, which are real airframe sections (the
    // Bloodhawk's wingtips, the Kestrel's outer wing thirds) — pdpN_h must render or
    // the plane is missing those parts. Suffixed names (pdp2_h, pdp2i) don't match.
    private static bool IsDamagePanel(string name)
    {
        int start = name.StartsWith("pdp", StringComparison.OrdinalIgnoreCase) ? 3
            : name.StartsWith("pcdp", StringComparison.OrdinalIgnoreCase) ? 4 : -1;
        if (start < 0 || start == name.Length)
            return false;
        for (int i = start; i < name.Length; i++)
            if (!char.IsDigit(name[i]))
                return false;
        return true;
    }

    /// <summary>Builds the subtree rooted at the named node (e.g. "player_bhawk").</summary>
    public Node3D Build(string rootName)
    {
        var root = _gamez.FindByName(rootName)
            ?? throw new ArgumentException($"node '{rootName}' not found in GameZ data");
        return _scene.BuildSubtree(root, Skip)!;
    }

    private bool Skip(GameZNode node)
    {
        if (SkipNames.Contains(node.Name) || IsDamagePanel(node.Name))
            return true;
        // Skyhook arms (zeppelin docking): every plane has a *_hook subtree (blood_hook,
        // kest_hook, gyro_hook, …) whose *_hook.json anim RESET_STATE deactivates the
        // group + its arm/door nodes — retracted by default, extended only on call.
        if (node.Name.EndsWith("_hook", StringComparison.OrdinalIgnoreCase))
            return true;
        var kind = PropParts.Classify(node.Name);
        // Flight shows ONLY the spinning blur discs; the exterior viewer shows ONLY the static
        // disc. The nitro boost disc is hidden either way (no nitro system yet). Hiding it in
        // flight matters: nitropropN is a NON-spinning blur disc, so leaving it in overlaid a
        // fixed disc on the spinning ones, and its edge painted the shimmering seam.
        return _spinningProps
            ? kind is PropParts.Kind.Static or PropParts.Kind.Nitro
            : PropParts.IsDynamic(kind);
    }
}

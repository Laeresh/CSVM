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
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit1", "cockpit2", "destroyed", "shadow", "player_damage_on", "blood_hook",
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
        _scene = new SceneBuilder(gamez, textures, blendTexture: IsPropBlurTexture);
        _spinningProps = spinningProps;
    }

    private static bool IsPropBlurTexture(string tex) =>
        tex.Contains("blur", StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the subtree rooted at the named node (e.g. "player_bhawk").</summary>
    public Node3D Build(string rootName)
    {
        var root = _gamez.FindByName(rootName)
            ?? throw new ArgumentException($"node '{rootName}' not found in GameZ data");
        return _scene.BuildSubtree(root, Skip)!;
    }

    private bool Skip(GameZNode node)
    {
        if (SkipNames.Contains(node.Name))
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

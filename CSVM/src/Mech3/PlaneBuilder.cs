using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

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
    private readonly TextureArchive _textures;
    private readonly SceneBuilder _scene;
    private readonly bool _spinningProps;
    private readonly bool _withDamagePanels;
    private readonly List<Node3D> _wingFlares = new();
    private readonly List<Node3D> _damagePanels = new();
    private PaintScheme? _scheme;
    private PatternLibrary _patterns;
    private PlanePainter? _painter;
    private string? _skinPrefix;
    private StandardMaterial3D? _flareMaterial;

    /// <param name="spinningProps">Spins the blur layers instead of the static disc; implies
    /// damage panels.</param>
    /// <param name="damagePanels">Builds exterior pdpN panels hidden, for the --damage lab.</param>
    /// <param name="scheme">Paint livery (<see cref="PlanePainter"/>); null keeps shipped skins,
    /// per builder so two players can differ.</param>
    /// <param name="patterns"><see cref="PatternLibrary"/>'s region masks; empty paints nothing.</param>
    public PlaneBuilder(GameZ gamez, TextureArchive textures, bool spinningProps = false,
        bool damagePanels = false, PaintScheme? scheme = null, PatternLibrary? patterns = null)
    {
        _gamez = gamez;
        _textures = textures;
        _scheme = scheme;
        _patterns = patterns ?? PatternLibrary.Empty;
        _scene = new SceneBuilder(gamez, textures, blendTexture: IsPropBlurTexture, cullBackfaces: true,
            textureSubstitute: (name, tex) => _painter?.Substitute(name, tex) ?? tex);
        _spinningProps = spinningProps;
        _withDamagePanels = spinningProps || damagePanels;
    }

    /// <summary>The paint applied to this build, once <see cref="Build"/> has resolved the
    /// aircraft's skin prefix — null when built unpainted.</summary>
    public PlanePainter? Painter => _painter;

    public int MeshInstanceCount => _scene.MeshInstanceCount;

    /// <summary>The wingtip flare nodes, built hidden (reset state) and re-skinned with an
    /// additive amber tint. A <see cref="Flight.WingLightBlinker"/> flashes them in flight;
    /// the static viewer leaves them off. Populated by <see cref="Build"/>.</summary>
    public IReadOnlyList<Node3D> WingFlares => _wingFlares;

    /// <summary>Flight and damage-lab builds: the exterior damage-state
    /// panels — the torn-skin pdpN nodes, built HIDDEN (their reset state), plus their
    /// healthy pdpN_h twins, built visible. A <see cref="Flight.DamageVisuals"/> flips
    /// them as part HP crosses the vehicle def's injure_anims thresholds.</summary>
    public IReadOnlyList<Node3D> DamagePanels => _damagePanels;

    /// <summary>The aircraft's skin-texture prefix, known once <see cref="Build"/> has run.
    /// Null when the model carries no decal-placeholder material to read it from.</summary>
    public string? SkinPrefix => _skinPrefix;

    /// <summary>The plane-local offset of this aircraft's authored <c>cockpit_camera</c> marker
    /// (docs/formats/markers.md), read the same way <see cref="MarkerRig"/> reads weapon markers —
    /// accumulated from below the plane root, skipping the cockpit-interior/wreck subtrees that
    /// may carry their own same-named node. Zero before <see cref="Build"/> runs, and the
    /// original's own fallback for a plane with no such node
    /// (docs/org/cameraViews.md, "Where the first-person camera sits").</summary>
    public Vector3 CockpitCameraOffset { get; private set; }

    /// <summary>Builds the subtree rooted at the named node (e.g. "player_bhawk").</summary>
    public Node3D Build(string rootName)
    {
        var root = _gamez.FindByName(rootName)
            ?? throw new ArgumentException($"node '{rootName}' not found in GameZ data");
        EnsurePainter(root);
        CockpitCameraOffset = MarkerRig.FindNamedMarker(_gamez, rootName, "cockpit_camera");
        var built = _scene.BuildSubtree(root, Skip)!;
        CollectWingFlares(built);
        return built;
    }

    /// <summary>Builds the plane's 'destroyed' wreck-piece subtree (skipped by
    /// <see cref="Build"/>) for the crash breakup: a group of
    /// pieceN meshes. The returned root's transform is the accumulated plane-root →
    /// destroyed chain, so `planePose × root.Transform × piece.Transform` is each
    /// piece's crash-time world pose. Null when the plane has no such subtree.</summary>
    public Node3D? BuildDestroyed(string rootName)
    {
        var root = _gamez.FindByName(rootName);
        if (root == null)
            return null;
        EnsurePainter(root); // wreck pieces wear the same skins, so the same livery
        // depth-first for the 'destroyed' group, accumulating local transforms
        (GameZNode Node, Transform3D Acc)? found = null;
        void Search(GameZNode n, Transform3D acc)
        {
            if (found != null)
                return;
            acc *= n.Local ?? Transform3D.Identity;
            if (n.Name.Equals("destroyed", StringComparison.OrdinalIgnoreCase))
            {
                found = (n, acc);
                return;
            }
            foreach (int c in n.Children)
                Search(_gamez.Nodes[c], acc);
        }
        // accumulate below the root only (the built plane model itself carries the
        // root node's transform)
        foreach (int c in root.Children)
            Search(_gamez.Nodes[c], Transform3D.Identity);
        if (found == null)
            return null;
        var built = _scene.BuildSubtree(found.Value.Node, _ => false);
        if (built != null)
            built.Transform = found.Value.Acc;
        return built;
    }

    /// <summary>Re-liveries the already-built aircraft in place: a fresh painter, then every
    /// textured material re-resolved through it. The viewer's livery lab drives this from its
    /// sliders — repainting ~8 small skins is a few ms, where rebuilding the model for each
    /// slider pixel would not be interactive. Null paints nothing (back to the shipped skins).
    /// No-op before <see cref="Build"/>, which is what discovers the skin prefix.</summary>
    public void Repaint(PaintScheme? scheme)
    {
        _scheme = scheme;
        _painter = scheme != null && _skinPrefix != null
            ? new PlanePainter(_textures, _patterns, scheme, _skinPrefix)
            : null;
        _scene.Repaint();
    }

    // ⚠ Blur disc textures must alpha-blend, never scissor: their alpha peaks around 26%,
    // which a scissor cutout erases outright.
    private static bool IsPropBlurTexture(string tex) =>
        tex.Contains("blur", StringComparison.OrdinalIgnoreCase);

    // ⚠ pdpN_h (the healthy twin, see IsHealthyPanel) must always render: skipping all of
    // player_damage_on amputates real airframe sections, not just the torn-skin state.
    private static bool IsDamagePanel(string name, out bool cockpit)
    {
        cockpit = name.StartsWith("pcdp", StringComparison.OrdinalIgnoreCase);
        int start = cockpit ? 4
            : name.StartsWith("pdp", StringComparison.OrdinalIgnoreCase) ? 3 : -1;
        if (start < 0 || start == name.Length)
            return false;
        for (int i = start; i < name.Length; i++)
            if (!char.IsDigit(name[i]))
                return false;
        return true;
    }

    // pdpN_h — the healthy twin of an exterior damage panel.
    private static bool IsHealthyPanel(string name) =>
        name.EndsWith("_h", StringComparison.OrdinalIgnoreCase)
        && IsDamagePanel(name[..^2], out bool cockpit) && !cockpit;

    // The painter can only be built once the aircraft's root is known — its skin prefix is
    // read off the model's own material names. Built on the first Build/BuildDestroyed call
    // and reused, so both share one painted-texture cache.
    private void EnsurePainter(GameZNode root)
    {
        _skinPrefix ??= PlanePainter.PrefixFor(_gamez, root);
        if (_scheme == null || _painter != null)
            return;
        if (_skinPrefix == null)
        {
            GD.Print($"[paint] {root.Name}: no <prefix>_noselogo/_taillogo/_winglogo material — building unpainted");
            return;
        }
        _painter = new PlanePainter(_textures, _patterns, _scheme, _skinPrefix);
        GD.Print($"[paint] {root.Name} ({_skinPrefix}): {_scheme}"
            + (_painter.PatternMissesAircraft
                ? $" — pattern '{_scheme.FolderName}' ships no {_skinPrefix} skins, decals only"
                : ""));
    }

    // Hides and re-skins the wingtip flares (WingLights.cs), and in the same walk collects
    // the flight build's damage panels: torn pdpN hidden, healthy pdpN_h as built.
    private void CollectWingFlares(Node node)
    {
        if (node is Node3D n3d)
        {
            if (WingLights.IsFlare(n3d.Name))
            {
                n3d.Visible = false;
                foreach (var child in n3d.GetChildren())
                    if (child is MeshInstance3D mi && mi.Name.ToString() == "mesh")
                        mi.MaterialOverride = FlareMaterial();
                _wingFlares.Add(n3d);
            }
            else if (IsDamagePanel(n3d.Name, out bool cockpit) && !cockpit)
            {
                n3d.Visible = false; // torn skin waits for DamageVisuals to flip it on
                _damagePanels.Add(n3d);
            }
            else if (IsHealthyPanel(n3d.Name))
            {
                _damagePanels.Add(n3d);
            }
        }
        foreach (var child in node.GetChildren())
            CollectWingFlares(child);
    }

    // Shared additive glow material for every flare quad (colour/texture: WingLights.cs).
    // ⚠ No billboard: forcing the quad to face the camera flattened the star burst into a blob.
    private StandardMaterial3D FlareMaterial() => _flareMaterial ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoTexture = _textures.Find(WingLights.FlareTexture),
        AlbedoColor = WingLights.FlareColor,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        // The flare quad draws its texture exactly once; with the engine-default repeat on,
        // bilinear filtering at the UV border bleeds the opposite edge in (the same artifact
        // once seen as a tracer-tail streak).
        TextureRepeat = false,
    };

    private bool Skip(GameZNode node)
    {
        if (SkipNames.Contains(node.Name))
            return true;
        // Cockpit damage panels always skip; exterior pdpN skip only in the plain static
        // viewer — flight and damage-lab builds construct them hidden for DamageVisuals (10c).
        if (IsDamagePanel(node.Name, out bool cockpit) && (cockpit || !_withDamagePanels))
            return true;
        // Skyhook arms (zeppelin docking): every plane has a *_hook subtree (blood_hook,
        // kest_hook, gyro_hook, …) whose *_hook.json anim RESET_STATE deactivates the
        // group + its arm/door nodes — retracted by default, extended only on call.
        if (node.Name.EndsWith("_hook", StringComparison.OrdinalIgnoreCase))
            return true;
        var kind = PropParts.Classify(node.Name);
        // Exterior shows only the static disc.
        if (!_spinningProps)
            return PropParts.IsDynamic(kind);
        // ⚠ nitropropN stays hidden even here: a non-spinning blur disc overlaid on the
        // spinning ones shimmers.
        if (kind == PropParts.Kind.Nitro)
            return true;
        // staticprop-vs-staticrotor build rule: this module's docs/architecture.md entry.
        return kind == PropParts.Kind.Static
            && !node.Name.StartsWith("staticprop", StringComparison.OrdinalIgnoreCase);
    }
}

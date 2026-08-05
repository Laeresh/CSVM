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

    /// <param name="spinningProps">Free-flight build: hide the static propeller disc and
    /// keep the spinning blur layers (a PropAnimator drives them). Default (exterior view)
    /// keeps the static disc and hides the blur layers. Implies damage panels.</param>
    /// <param name="damagePanels">Build the exterior pdpN torn-skin panels (hidden) even
    /// with static props — the viewer's --damage lab flips them without flying.</param>
    /// <param name="scheme">Paint this aircraft in the given livery — its skins recoloured
    /// and its three decal placeholders swapped (see <see cref="PlanePainter"/>). Null builds
    /// the shipped unpainted skins, which is what every static view did before paint existed.
    /// Painting is per builder, so two players in the same aircraft can wear different
    /// liveries without disturbing the shared texture cache.</param>
    /// <param name="patterns">The original's per-pattern region masks from the UI resource
    /// archive (see <see cref="PatternLibrary"/>). An empty library paints nothing.</param>
    public PlaneBuilder(GameZ gamez, TextureArchive textures, bool spinningProps = false,
        bool damagePanels = false, PaintScheme? scheme = null, PatternLibrary? patterns = null)
    {
        _gamez = gamez;
        _textures = textures;
        _scheme = scheme;
        _patterns = patterns ?? PatternLibrary.Empty;
        // The propeller/rotor blur discs (rotorblur/zeprotorblur) are soft sprites — their
        // alpha peaks at ~26%, so the default 1-bit AlphaScissor cutout erases them entirely.
        // Alpha-BLEND them instead (as with the clouds) so the translucent disc shows.
        // cullBackfaces: aircraft interior structure (the gyro's frame lattice) faces
        // inward and must be culled from outside, as the original engine does.
        _scene = new SceneBuilder(gamez, textures, blendTexture: IsPropBlurTexture, cullBackfaces: true,
            textureSubstitute: (name, tex) => _painter?.Substitute(name, tex) ?? tex);
        _spinningProps = spinningProps;
        _withDamagePanels = spinningProps || damagePanels;
    }

    /// <summary>The paint applied to this build, once <see cref="Build"/> has resolved the
    /// aircraft's skin prefix — null when built unpainted.</summary>
    public PlanePainter? Painter => _painter;

    public int MeshInstanceCount => _scene.MeshInstanceCount;

    /// <summary>The wingtip flare nodes, built hidden (reset state) and re-skinned as
    /// additive billboards. A <see cref="Flight.WingLightBlinker"/> flashes them in flight;
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

    /// <summary>Builds the subtree rooted at the named node (e.g. "player_bhawk").</summary>
    public Node3D Build(string rootName)
    {
        var root = _gamez.FindByName(rootName)
            ?? throw new ArgumentException($"node '{rootName}' not found in GameZ data");
        EnsurePainter(root);
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

    private static bool IsPropBlurTexture(string tex) =>
        tex.Contains("blur", StringComparison.OrdinalIgnoreCase);

    // Damage-state panels pdp1..8 (exterior) / pcdpN (cockpit) start INACTIVE in the
    // original: its player_destruct_reset "plane_reset" anim deactivates them and
    // re-activates the healthy pdpN_h panels, which are real airframe sections (the
    // Bloodhawk's wingtips, the Kestrel's outer wing thirds) — pdpN_h must render or
    // the plane is missing those parts. Suffixed names (pdp2_h, pdp2i) don't match.
    // In flight builds the exterior pdpN panels are BUILT hidden instead of skipped,
    // so DamageVisuals can flip them at the injure_anims HP thresholds.
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

    /// <summary>pdpN_h — the healthy twin of an exterior damage panel.</summary>
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

    /// <summary>Finds the wingtip flare nodes in the built tree, hides them (reset state:
    /// the original starts them off and flashes them via wing_light.json's blink anim), and
    /// re-skins each glow quad as an additive camera-facing billboard so it reads from any
    /// angle — the source quads are one-sided (only showed from behind). See <see cref="WingLights"/>.
    /// The same walk collects the flight build's damage panels: torn-skin pdpN hidden
    /// (reset state), healthy pdpN_h twins as built.</summary>
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

    // Additive glow shared by every flare quad: unshaded, camera-facing, no depth write,
    // tinted the original's warm amber (wing_light.json LIGHT_STATE COLOR). The soft
    // oil_liteflare sprite (white core → transparent black) blends additively so its edges
    // add nothing and the core glows — same treatment as the point-sprite lights.
    private StandardMaterial3D FlareMaterial() => _flareMaterial ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoTexture = _textures.Find(WingLights.FlareTexture),
        AlbedoColor = WingLights.FlareColor,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        BillboardKeepScale = true,
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
        // Flight shows ONLY the spinning blur discs; the exterior viewer shows ONLY the static
        // disc. The nitro boost disc is hidden either way (no nitro system yet). Hiding it in
        // flight matters: nitropropN is a NON-spinning blur disc, so leaving it in overlaid a
        // fixed disc on the spinning ones, and its edge painted the shimmering seam.
        return _spinningProps
            ? kind is PropParts.Kind.Static or PropParts.Kind.Nitro
            : PropParts.IsDynamic(kind);
    }
}

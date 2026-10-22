using System;
using System.Collections.Generic;
using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Builds a renderable Godot node tree for one aircraft out of a planes.zbd GameZ model.
/// Exterior view only: picks the nearest LOD, skips cockpit/damage/destroyed variants.
///
/// Propellers have several representations under <c>dontmove</c> (see <see cref="PropParts"/>).
/// The default (exterior) build keeps the still <c>staticpropN</c> disc and drops the blur
/// layers. The <c>spinningProps</c> build (free flight) keeps BOTH, for
/// <c>Flight.Airframe.PropAnimator</c> to spin and the spinprops/stopprops pair to switch between.
/// The <c>nitropropN</c> boost disc is built hidden in a flight build, for the nitro_boost def.
/// </summary>
public sealed class PlaneBuilder
{
    /// <summary>The uniform scale the <c>cockpit1</c> interior is mounted at. TUNE, not decoded:
    /// the subtree is authored in its own units with the pilot's eye at its own origin, so the
    /// FRAMING is scale-invariant and this chooses only how the interior composites against world
    /// geometry. ⚠ The interior and the airframe are not a similarity apart, do not try to derive
    /// this from the model. First-person decode: docs/org/cameraViews.md.</summary>
    public const float InteriorScale = 0.04f;

    /// <summary>The fixed head-pitch offset <c>FUN_0042d980</c> applies about the same axis as
    /// elevation, in both first-person views: −4.70° = −0.08203 rad (bit pattern
    /// <c>0xbda7ff58</c>). Not head-look, a constant tilt baked into the view build. The camera
    /// reads it with <see cref="CockpitCameraOffset"/>, and the interior mount carries the same
    /// tilt so the gunsight stays on the guns. Decode: docs/org/cameraViews.md.</summary>
    public const float HeadPitchOffsetRad = -0.08203f;

    // TUNE, Enhanced only: metres the airframe reads the sun's shadow map out along its normal.
    // Without it a skin lit by a low sun shows shadow acne, which the soft filter's rotated disc
    // turns into a per-pixel dot grid. ⚠ Do not raise the sun's shared bias pair instead; that
    // detaches every contact shadow. Measurement: docs/verification.md SHOT-43.
    private const float AirframeShadowLookupOffset = 0.5f;

    // The flare mesh is a SphericalY facade, posed through the facade look-at like every other
    // population of that class (docs/org/cloudCards.md), keeping the node's scale.
    // ⚠ Never Godot's billboard mode: it is the camera basis, so a bank rolls the flare with the eye.
    // The fragment is StandardMaterial3D's unshaded additive path: texture times tint times COLOR.
    // The sampler never repeats, since the texture maps once and wrapping bleeds its opposite edge in.
    private const string FlareShaderCode = """
        shader_type spatial;
        render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
        uniform vec4 tint : source_color = vec4(1.0);

        #include "res://shaders/csky_facade.gdshaderinc"

        void vertex() {
            MODELVIEW_MATRIX = csky_facade_modelview(VIEW_MATRIX, MODEL_MATRIX, CAMERA_POSITION_WORLD);
        }

        void fragment() {
            vec4 tex = texture(albedo_tex, UV) * COLOR;
            ALBEDO = tint.rgb * tex.rgb;
            ALPHA = tint.a * tex.a;
        }
        """;

    // Non-prop subtrees that make no sense in an exterior view: cockpit interiors are
    // separate (differently-scaled) models; damage/destroyed are alternate states.
    // player_damage_off holds the intact duplicates (pdpNi) of the panels that
    // player_damage_on already provides as pdpN_h, the original engine shows exactly
    // one of the two groups (its vehicle-damage detail toggle); we model "damage on".
    // `cockpit2` is defensive: no shipped tree carries one.
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit1", "cockpit2", "destroyed", "shadow", "player_damage_off",
    };

    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly SceneBuilder _scene;
    private readonly SceneBuilder? _interiorScene;
    private readonly bool _spinningProps;
    private readonly bool _withDamagePanels;
    private readonly bool _withCockpitInterior;
    private readonly bool _withDockingHook;
    // InteriorScale times the caller's interiorScaleFactor: the stereo path's per-plane factor,
    // 1 everywhere else. The mount and the interior's depth bias both take it.
    private readonly float _interiorScale;
    private readonly List<Node3D> _wingFlares = new();
    private readonly List<Node3D> _damagePanels = new();
    private readonly List<Node3D> _cockpitDamagePanels = new();
    // The stereo interior's sight glass textures split into their glass and reticle layers, by name.
    private readonly Dictionary<string, GunsightReticle.Layers?> _gunsight = new();
    private PaintScheme? _scheme;
    private PatternLibrary _patterns;
    private PlanePainter? _painter;
    private string? _skinPrefix;
    private ShaderMaterial? _flareMaterial;

    /// <param name="spinningProps">Spins the blur layers instead of the static disc; implies damage panels.</param>
    /// <param name="damagePanels">Builds exterior pdpN panels hidden, for the --damage lab.</param>
    /// <param name="scheme">Paint livery (<see cref="PlanePainter"/>); null keeps shipped skins, per builder so two players can differ.</param><param name="painter">A painter already built for this airframe and this <paramref name="scheme"/>, so a second aeroplane of the pair wears the skins the first composed instead of composing them again (PERF-22). ⚠ Its scheme and skin prefix must be this build's, nothing here checks them.</param>
    /// <param name="patterns"><see cref="PatternLibrary"/>'s region masks; empty paints nothing.</param>
    /// <param name="cockpitInterior">Builds <see cref="CockpitInterior"/>; a human rig only, so an AI plane never pays for a cockpit nobody sits in.</param>
    /// <param name="dockingHook">Builds the airframe's <c>*_hook</c> group, parked at its archive-authored inactive bit; a human rig only.</param>
    public PlaneBuilder(GameZ gamez, TextureArchive textures, bool spinningProps = false,
        bool damagePanels = false, PaintScheme? scheme = null, PatternLibrary? patterns = null,
        bool cockpitInterior = false, bool dockingHook = false, PlanePainter? painter = null,
        bool raceGhost = false, bool xrInterior = false, float interiorScaleFactor = 1f)
    {
        _gamez = gamez;
        _interiorScale = InteriorScale * interiorScaleFactor;
        _textures = textures;
        _scheme = scheme;
        _painter = scheme != null ? painter : null;
        _patterns = patterns ?? PatternLibrary.Empty;
        _scene = new SceneBuilder(gamez, textures, blendTexture: IsPropBlurTexture, cullBackfaces: true,
            textureSubstitute: (name, tex) => _painter?.Substitute(name, tex) ?? tex, sunVertexLit: true)
        {
            // The interior keeps zero: its own pass measured no self-shadow noise at its scale.
            ShadowLookupOffset = AirframeShadowLookupOffset,
            // The race ghost keys the airframe alone; the interior is the owner's view and never fades.
            RaceGhostShader = raceGhost,
        };
        // ⚠ A builder of its own, never a field toggled on the airframe's: DepthBiasScale is baked
        // into cached meshes and materials, so one builder switching it mid-build would hand a
        // later caller a mesh biased for the wrong scale.
        if (cockpitInterior)
        {
            _interiorScene = new SceneBuilder(gamez, textures, blendTexture: IsInteriorBlendTexture,
                cullBackfaces: true,
                textureSubstitute: (name, tex) => InteriorTexture(name, _painter?.Substitute(name, tex) ?? tex, xrInterior),
                sunVertexLit: true)
            {
                DepthBiasScale = 1f / _interiorScale,
                // The panel is a wall of soft-alpha decals over dark instruments, which is where
                // the linear-space composite departs visibly from the original's (see the field).
                GammaBlendAlpha = true,
                NoAlphaCoverage = true,
                // The stereo path's interior, which XrCockpit lifts to the XR origin's frame.
                XrPreciseInterior = xrInterior,
            };
        }
        _spinningProps = spinningProps;
        _withDamagePanels = spinningProps || damagePanels;
        _withCockpitInterior = cockpitInterior;
        _withDockingHook = dockingHook;
    }

    /// <summary>The paint applied to this build, once <see cref="Build"/> has resolved the
    /// aircraft's skin prefix, null when built unpainted.</summary>
    public PlanePainter? Painter => _painter;

    public int MeshInstanceCount => _scene.MeshInstanceCount + (_interiorScene?.MeshInstanceCount ?? 0);

    /// <summary>The interior's polygons the build left out: the head plate's under the stereo path
    /// (<see cref="CockpitPlate"/>), none on every other build.</summary>
    public int InteriorOmittedPolygons => _interiorScene?.OmittedPolygonCount ?? 0;

    /// <summary>The collimated reticle overlays the stereo interior carries (<see cref="GunsightReticle"/>),
    /// one per sight glass; none on every other build.</summary>
    public int GunsightReticles { get; private set; }

    /// <summary>The wingtip flare nodes, built hidden (reset state) and re-skinned with an
    /// additive amber tint. A <c>Flight.Airframe.WingLightBlinker</c> flashes them in flight;
    /// the static viewer leaves them off. Populated by <see cref="Build"/>.</summary>
    public IReadOnlyList<Node3D> WingFlares => _wingFlares;

    /// <summary>Flight and damage-lab builds: the exterior damage-state
    /// panels, the torn-skin pdpN nodes, built HIDDEN (their reset state), plus their
    /// healthy pdpN_h twins, built visible. A <c>Flight.Airframe.DamageVisuals</c> flips
    /// them as part HP crosses the vehicle def's injure_anims thresholds.</summary>
    public IReadOnlyList<Node3D> DamagePanels => _damagePanels;

    /// <summary>Cockpit-interior builds only: the two torn-skin cockpit panels,
    /// <c>pcdp4</c>/<c>pcdp6</c>, built HIDDEN like their exterior
    /// counterparts. They carry no healthy twin (no <c>pcdp4_h</c>/<c>pcdp6_h</c> ships anywhere
    /// in <c>planes.zbd</c>), <c>Flight.Airframe.DamageVisuals</c> flips them off the SAME
    /// <c>pdpanel4</c>/<c>pdpanel6</c> injure entries that flip <c>pdp4</c>/<c>pdp6</c>, not a
    /// separate cockpit rule. Empty unless the builder was asked for a cockpit interior.</summary>
    public IReadOnlyList<Node3D> CockpitDamagePanels => _cockpitDamagePanels;

    /// <summary>The aircraft's skin-texture prefix, known once <see cref="Build"/> has run.
    /// Null when the model carries no decal-placeholder material to read it from.</summary>
    public string? SkinPrefix => _skinPrefix;

    /// <summary>The plane-local offset of this aircraft's authored <c>cockpit_camera</c> marker
    /// (docs/formats/markers.md), read as <see cref="MarkerRig.FindNamedMarker"/> reads it. A plane
    /// rig's <c>cockpit_camera</c> position replaces the marker's. Zero before <see cref="Build"/>
    /// runs, and the original's own fallback for a plane with no such node
    /// (docs/org/cameraViews.md, "Where the first-person camera sits").</summary>
    public Vector3 CockpitCameraOffset { get; private set; }

    /// <summary>The built <c>cockpit1</c> interior inside the model <see cref="Build"/> returned,
    /// hidden and mounted at <see cref="CockpitCameraOffset"/>; null unless the builder was asked
    /// for one. <c>Flight.Hud.CockpitVisibility</c> is what shows it, per view mode.</summary>
    public Node3D? CockpitInterior { get; private set; }

    /// <summary>The <c>cockpit</c> section of the rig <see cref="Build"/> applied, or null: no rig,
    /// no section, or a rig that failed its checks and was flown without.</summary>
    public RigCockpit? Cockpit { get; private set; }

    /// <summary>Where the eye sits in <see cref="CockpitInterior"/>'s own units: the
    /// <c>cockpit_camera</c> marker's numbers under the stereo path, the interior's origin on every
    /// other build. The flat view keeps the origin and its tilt (docs/org/cameraViews.md, "Where the
    /// eye sits in the interior").</summary>
    public Vector3 InteriorEye { get; private set; }

    /// <summary>The interior's own textured materials paired with the texture each resolved from,
    /// the same registry <see cref="Repaint"/> uses. <c>Flight.Hud.CockpitGauges</c> reads it to
    /// tell an indicator's light from its hilite bar by NAME rather than by guessing at the
    /// surface order, then overrides each driven surface with a copy of its own.</summary>
    public IReadOnlyList<(ShaderMaterial Material, string TextureName)> InteriorMaterials =>
        _interiorScene?.TexturedMaterials ?? Array.Empty<(ShaderMaterial, string)>();

    /// <summary>A <c>cockpit1</c> node whose visibility is a STATE something else drives, so a
    /// pristine cockpit must show none of it: the <c>bulNx</c> hole quads the
    /// <c>cockpit_bulletholes</c> defs light, and the two warning lamps, which
    /// <c>Flight.Hud.CockpitGauges</c> lights. Parking them still holds: a build with no rig
    /// driving it must render pristine, and the labs are such builds. ⚠ Everything else on the
    /// panel is always-drawn geometry that changes COLOUR, not visibility.</summary>
    public static bool IsInteriorDrivenState(string name)
    {
        if (name.EndsWith("_on", StringComparison.OrdinalIgnoreCase))
            return true;
        // ⚠ The QUADS, not the meshless bulletN groups above them: a parked group hides its own
        // leaves for good, and reset_bulletholes switches exactly these off and nothing else.
        if (!name.StartsWith("bul", StringComparison.OrdinalIgnoreCase))
            return false;
        int i = "bul".Length, digits = 0;
        while (i < name.Length && char.IsDigit(name[i]))
        {
            i++;
            digits++;
        }
        if (digits == 0 || i == name.Length)
            return false;
        for (; i < name.Length; i++)
            if (!char.IsLetter(name[i]))
                return false;
        return true;
    }

    /// <summary>The airframe's skyhook group node (<c>bal_hook</c>, <c>blood_hook</c>, …), the
    /// subtree a zeppelin hookup cutscene extends through that airframe's own
    /// <c>&lt;x&gt;_hook_extend</c> definition.</summary>
    public static bool IsDockingHook(string name) =>
        name.EndsWith("_hook", StringComparison.OrdinalIgnoreCase);

    /// <summary>The stereo interior's mount: untilted, at <paramref name="metresPerUnit"/>, with the
    /// interior's point at the marker's numbers standing on the marker. The original draws
    /// <c>cockpit1</c> unscaled under the plane root with the first-person camera. Its eye in the
    /// interior is therefore the marker read in interior units. Engine-free, so it unit-tests.</summary>
    public static Transform3D StereoMount(Vector3 marker, float metresPerUnit) =>
        new(Basis.Identity.Scaled(Vector3.One * metresPerUnit), marker - (marker * metresPerUnit));

    /// <summary>Builds the subtree rooted at the named node (e.g. "player_bhawk"), with the model
    /// pack's <c>rig.json</c> for that node applied (<see cref="PlaneRigPatch"/>). Throws
    /// <see cref="System.IO.InvalidDataException"/> for a rig that fails its checks, unless
    /// <see cref="PlaneRigPatch.PlayerNotice"/> is set: then it reports there and builds without the rig.</summary>
    public Node3D Build(string rootName)
    {
        var root = _gamez.FindByName(rootName)
            ?? throw new ArgumentException($"node '{rootName}' not found in GameZ data");
        EnsurePainter(root);
        // Every node under the root resolves its model pack replacement under this airframe.
        _scene.HdAirframe = rootName;
        int replacedBefore = _scene.ReplacedMeshCount;
        var built = _scene.BuildSubtree(root, Skip)!;
        if (_scene.ReplacedMeshCount > replacedBefore)
        {
            Log.Info("world", $"HD models: {_scene.ReplacedMeshCount - replacedBefore} mesh instance(s) of {rootName} drawn from the model pack");
        }

        FlareChoice? flares = null;
        Cockpit = null;
        try
        {
            // ⚠ Patch before anything reads a marker or classifies a part. The camera read, the
            // interior mount, the flare walk and every caller's animators and loadout bind follow it.
            if (PlaneRigPatch.Load(HdModels.Current, rootName) is { } rig)
            {
                flares = PlaneRigPatch.Apply(rig, built, _gamez, root);
                Cockpit = rig.Cockpit;
                CockpitCameraOffset = PlaneRigPatch.MarkerPosition(rig, "cockpit_camera")
                    ?? MarkerRig.FindNamedMarker(_gamez, rootName, "cockpit_camera");
            }
            else
            {
                CockpitCameraOffset = MarkerRig.FindNamedMarker(_gamez, rootName, "cockpit_camera");
            }
        }
        catch (System.IO.InvalidDataException e) when (PlaneRigPatch.PlayerNotice is { } notice)
        {
            // A player's launch flies the original airframe; Apply edits nothing before its last check.
            Log.Error("world", $"{e.Message}; {rootName} is built without its rig");
            notice($"The model pack's rig for {rootName} has an error, so that aircraft flies without it. The log names the file and the entry.");
            flares = null;
            CockpitCameraOffset = MarkerRig.FindNamedMarker(_gamez, rootName, "cockpit_camera");
        }
        catch
        {
            built.Free();
            throw;
        }

        // ⚠ Before the flare/panel walk, not after: that walk is what hides and collects the two
        // cockpit damage panels (pcdp4/pcdp6), which live inside the interior.
        MountCockpitInterior(built, root);
        CollectWingFlares(built, flares);
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
        _scene.HdAirframe = rootName;
        int replacedBefore = _scene.ReplacedMeshCount;
        var built = _scene.BuildSubtree(found.Value.Node, _ => false);
        if (built != null)
            built.Transform = found.Value.Acc;
        if (_scene.ReplacedMeshCount > replacedBefore)
        {
            Log.Info("world", $"HD models: {_scene.ReplacedMeshCount - replacedBefore} wreck piece mesh(es) of {rootName} drawn from the model pack");
        }
        return built;
    }

    /// <summary>Re-liveries the already-built aircraft in place: a fresh painter, then every
    /// textured material re-resolved through it. The viewer's livery lab drives this from its
    /// sliders, repainting ~8 small skins is a few ms, where rebuilding the model for each
    /// slider pixel would not be interactive. Null paints nothing (back to the shipped skins).
    /// No-op before <see cref="Build"/>, which is what discovers the skin prefix.</summary>
    public void Repaint(PaintScheme? scheme)
    {
        _scheme = scheme;
        _painter = scheme != null && _skinPrefix != null
            ? new PlanePainter(_textures, _patterns, scheme, _skinPrefix)
            : null;
        _scene.Repaint();
        _interiorScene?.Repaint();
    }

    // ⚠ Blur disc textures must alpha-blend, never scissor: their alpha peaks around 26%,
    // which a scissor cutout erases outright.
    private static bool IsPropBlurTexture(string tex) =>
        tex.Contains("blur", StringComparison.OrdinalIgnoreCase);

    // ⚠ compasstxt and horizonindicator must alpha-blend in the interior, never scissor. The
    // compass window fades the drum's ends under two quads sampling that atlas' black alpha ramp,
    // and the artificial horizon's glass carries a golden haze inside its upper rim on a ramp that
    // crosses the cutout threshold, so a scissor turns each into an opaque bar (docs/formats/hud.md).
    private static bool IsInteriorBlendTexture(string tex) =>
        IsPropBlurTexture(tex)
        || tex.StartsWith("compasstxt", StringComparison.OrdinalIgnoreCase)
        || tex.StartsWith("horizonindicator", StringComparison.OrdinalIgnoreCase);

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

    // pdpN_h, the healthy twin of an exterior damage panel.
    private static bool IsHealthyPanel(string name) =>
        name.EndsWith("_h", StringComparison.OrdinalIgnoreCase)
        && IsDamagePanel(name[..^2], out bool cockpit) && !cockpit;

    // The painter can only be built once the aircraft's root is known, its skin prefix is
    // read off the model's own material names. Built on the first Build/BuildDestroyed call
    // and reused, so both share one painted-texture cache.
    private void EnsurePainter(GameZNode root)
    {
        _skinPrefix ??= PlanePainter.PrefixFor(_gamez, root);
        if (_scheme == null || _painter != null)
            return;
        if (_skinPrefix == null)
        {
            Log.Info("world", $"[paint] {root.Name}: no <prefix>_noselogo/_taillogo/_winglogo material, building unpainted");
            return;
        }
        _painter = new PlanePainter(_textures, _patterns, _scheme, _skinPrefix);
        Log.Info("world", $"[paint] {root.Name} ({_skinPrefix}): {_scheme}{(_painter.PatternMissesAircraft ? $", pattern '{_scheme.FolderName}' ships no {_skinPrefix} skins, decals only" : "")}");
    }

    // Hides and re-skins the wingtip flares (WingLights.cs), and in the same walk collects
    // the flight build's damage panels: torn pdpN hidden, healthy pdpN_h as built. A rig's
    // flare choice adds its nodes to the name match and darkens the originals it replaced.
    private void CollectWingFlares(Node node, FlareChoice? rig)
    {
        if (node is Node3D n3d)
        {
            if (rig != null && rig.Displaced.Contains(n3d))
            {
                n3d.Visible = false;
            }
            else if (WingLights.IsFlare(n3d.Name) || (rig != null && rig.Flares.Contains(n3d)))
            {
                n3d.Visible = false;
                foreach (var child in n3d.GetChildren())
                    if (child is MeshInstance3D mi && mi.Name.ToString() == "mesh")
                        mi.MaterialOverride = FlareMaterial();
                _wingFlares.Add(n3d);
            }
            else if (PropParts.Classify(n3d.Name) == PropParts.Kind.Nitro)
            {
                n3d.Visible = false; // the nitro disc waits for the nitro_boost def to activate it
            }
            else if (IsDamagePanel(n3d.Name, out bool cockpit))
            {
                n3d.Visible = false; // torn skin waits for DamageVisuals to flip it on
                // pcdpN is kept off DamagePanels, that list is the exterior set the pairing walk
                // measures mesh-AABB centers over, and collected on its own list instead (B12);
                // DamageVisuals flips both off the same pdpanelN injure entries.
                (cockpit ? _cockpitDamagePanels : _damagePanels).Add(n3d);
            }
            else if (IsHealthyPanel(n3d.Name))
            {
                _damagePanels.Add(n3d);
            }
            else if (IsDockingHook(AnimRuntime.NameOf(n3d)))
            {
                // Retracted until the hookup cutscene calls <x>_hook_extend, whose own first
                // sequence is what activates the group. The archive ships the bit that way.
                n3d.Visible = ActiveInGameZ(n3d);
            }
        }
        foreach (var child in node.GetChildren())
            CollectWingFlares(child, rig);
    }

    // Builds the interior on its own pass, since the depth bias must know the mount scale. The flat
    // eye is cockpit1's origin, mounted on the cockpit_camera marker where FirstPersonPose stands.
    // ⚠ The flat mount also carries the head-pitch tilt, which nearly puts the sight on the guns.
    // Untilted, that sight rides 3.9° above the nose axis. Head-look is never applied to it.
    // The stereo interior takes the original's eye instead (StereoMount), which needs no tilt.
    private void MountCockpitInterior(Node3D built, GameZNode root)
    {
        if (!_withCockpitInterior || _interiorScene == null)
        {
            return;
        }
        foreach (int c in root.Children)
        {
            var node = _gamez.Nodes[c];
            if (!node.Name.Equals("cockpit1", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // A tracked head can lean past the plate behind the eye, so the stereo interior drops it.
            // The flat eye never sees its front, and the flat build keeps it untouched.
            _interiorScene.OmittedPolygons = _interiorScene.XrPreciseInterior ? CockpitPlate.Find(_gamez, node) : null;
            // The airframe's pack scope, so cockpit1.glb and the airframe's node files reach the
            // interior. Its replacements are this builder's own, which bakes the interior's bias.
            _interiorScene.HdAirframe = _scene.HdAirframe;
            int replacedBefore = _interiorScene.ReplacedMeshCount;
            // Exempt only THIS node from the skip list: `cockpit1` is on it unconditionally now
            // that the airframe pass never builds the interior, and it is the root here.
            if (_interiorScene.BuildSubtree(node, n => !ReferenceEquals(n, node) && Skip(n))
                is not { } n3d)
            {
                return;
            }
            if (_interiorScene.ReplacedMeshCount > replacedBefore)
            {
                Log.Info("world", $"HD models: {_interiorScene.ReplacedMeshCount - replacedBefore} cockpit interior mesh instance(s) of {root.Name} drawn from the model pack");
            }
            InteriorEye = _interiorScene.XrPreciseInterior ? CockpitCameraOffset : Vector3.Zero;
            n3d.Transform = _interiorScene.XrPreciseInterior
                ? StereoMount(CockpitCameraOffset, _interiorScale)
                : new Transform3D(
                    new Basis(Vector3.Right, HeadPitchOffsetRad)
                        .Scaled(Vector3.One * _interiorScale),
                    CockpitCameraOffset);
            n3d.Visible = false; // shown only while a first-person view is on the screen
            ParkInteriorStates(n3d);
            if (_interiorScene.XrPreciseInterior)
            {
                AddGunsightReticles(n3d, node);
            }

            built.AddChild(n3d);
            CockpitInterior = n3d;
            return;
        }
    }

    // The stereo interior's sight glass wears its texture without the reticle, which an overlay
    // draws collimated in its place (AddGunsightReticles). Every other texture and build is as given.
    private ImageTexture? InteriorTexture(string name, ImageTexture? tex, bool xrInterior)
    {
        if (!xrInterior || tex == null || !GunsightReticle.IsGlass(name))
        {
            return tex;
        }

        if (!_gunsight.TryGetValue(name, out var layers))
        {
            _gunsight[name] = layers = GunsightReticle.SplitTexture(tex);
        }

        return layers?.Glass ?? tex;
    }

    // One collimated reticle overlay per sight glass the build split a texture for.
    // It takes the glass's own depth bias, so it hides behind whatever hides the glass.
    private void AddGunsightReticles(Node3D interior, GameZNode root)
    {
        foreach (var glass in GunsightReticle.Find(_gamez, root))
        {
            if (_gunsight.GetValueOrDefault(glass.TextureName) is { } layers
                && GunsightReticle.Overlay(glass, layers, GunsightReticle.EyeReachM / _interiorScale, _interiorScene!.PriorityBias(glass.Priority), InteriorEye) is { } overlay)
            {
                interior.AddChild(overlay);
                GunsightReticles++;
            }
        }
    }

    // ⚠ The interior's off-states ship ACTIVE. A node the build script left NodeSetActive(false)
    // is off outright, but the state overlays are authored visible and hidden engine-side until
    // something drives them, so a pristine cockpit renders every windshield bullet hole and both
    // warning lamps unless they are parked here. See IsInteriorDrivenState.
    private void ParkInteriorStates(Node3D node)
    {
        if (IsInteriorDrivenState(AnimRuntime.NameOf(node)) || !ActiveInGameZ(node))
        {
            node.Visible = false;
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3d)
            {
                ParkInteriorStates(n3d);
            }
        }
    }

    // This built node's own gamez `flags.active`; true when the node carries no index to look up.
    private bool ActiveInGameZ(Node3D node)
    {
        if (!node.HasMeta(AnimRuntime.IndexMeta))
            return true;
        int index = (int)node.GetMeta(AnimRuntime.IndexMeta);
        return index < 0 || index >= _gamez.Nodes.Count || _gamez.Nodes[index].Active;
    }

    // Shared additive glow material for every flare quad (colour/texture: WingLights.cs).
    private ShaderMaterial FlareMaterial()
    {
        if (_flareMaterial == null)
        {
            _flareMaterial = new ShaderMaterial { Shader = ShaderTwins.Pooled(FlareShaderCode, "plane-flare") };
            if (_textures.Find(WingLights.FlareTexture) is { } tex)
                _flareMaterial.SetShaderParameter("albedo_tex", tex);
            _flareMaterial.SetShaderParameter("tint", WingLights.FlareColor);
        }
        return _flareMaterial;
    }

    private bool Skip(GameZNode node)
    {
        // ⚠ `cockpit1` is skipped here even when the caller asked for an interior: that subtree is
        // built by its own pass, on its own builder (MountCockpitInterior).
        if (SkipNames.Contains(node.Name))
            return true;
        // Cockpit damage panels (pcdpN) come with the interior and nothing else; exterior pdpN skip
        // only in the plain static viewer, flight and damage-lab builds construct them hidden for
        // DamageVisuals (10c). Both stay hidden until something flips them.
        if (IsDamagePanel(node.Name, out bool cockpit)
            && (cockpit ? !_withCockpitInterior : !_withDamagePanels))
            return true;
        // Skyhook arms: built only where a docking cutscene can call the airframe's own
        // <x>_hook_extend, since that definition ACTIVATES the group rather than creating it.
        if (!_withDockingHook && IsDockingHook(node.Name))
            return true;
        var kind = PropParts.Classify(node.Name);
        // Exterior shows only the static disc.
        if (!_spinningProps)
            return PropParts.IsDynamic(kind);
        // nitropropN is built hidden (CollectWingFlares): the nitro_boost def activates and fades
        // it in over the spinning discs, and nitro_decay parks it again. ⚠ staticpropN is kept
        // (see the class remarks); staticrotorN is not, that def naming propeller nodes only.
        return kind == PropParts.Kind.Static
            && !node.Name.StartsWith("staticprop", StringComparison.OrdinalIgnoreCase);
    }
}

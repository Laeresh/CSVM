using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Builds the whole world of a chapter gamez.zbd (terrain chunks, buildings, zeppelins,
/// horizon). World content lives in two places: the World node's children, and ~350
/// top-level subtrees referenced only through the World's spatial partition grid.
/// </summary>
public sealed class WorldBuilder
{
    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly SceneBuilder _scene;
    private GameZNode? _builtWorld; // the World node of the last Build (for CreateEdgeExtender)

    // Non-scenery world content: 'horizon' is the original skydome (built separately via
    // BuildHorizon — as part of the world it would swallow the scene), 'fvol1'..'fvol9'
    // are flight-boundary volumes, 'dzpaths' are colored path ribbons.
    // Internal: ClutterBuilder walks the same placed world with the same exclusions.
    internal static bool SkipWorldNode(GameZNode n) =>
        n.Name.Equals("horizon", StringComparison.OrdinalIgnoreCase)
        || n.Name.Equals("dzpaths", StringComparison.OrdinalIgnoreCase)
        || n.Name.StartsWith("fvol", StringComparison.OrdinalIgnoreCase);

    // A cloud- or sky-textured surface. Node names are unreliable for spotting these
    // (cloud layers turn up under generic names like 'g27517'), so we classify by texture.
    // Three callers share this rule: the collision exemption (below), the cloud alpha-blend
    // (passed to SceneBuilder) — clouds are neither solid nor hard-edged cutouts — and
    // MapEdgeExtender's ground-tile classifier (cloudlayer deck tiles are cell-sized too).
    internal static bool IsCloudOrSkyTexture(string tex) =>
        tex.StartsWith("cloud", StringComparison.OrdinalIgnoreCase)
        || tex.StartsWith("sky", StringComparison.OrdinalIgnoreCase);

    // The cloud SPRITES only: cloud1/cloud2 are soft vertical cards (2D billboards), while
    // 'cloudlayer' is the flat horizontal deck sheet — which must stay flat, not billboard.
    // These face the camera; SceneBuilder recenters their quads so they pivot correctly.
    private static bool IsCloudSpriteTexture(string tex) =>
        tex.StartsWith("cloud", StringComparison.OrdinalIgnoreCase)
        && !tex.StartsWith("cloudlayer", StringComparison.OrdinalIgnoreCase);

    // Light-source flare/fire sprites (validated in C1, user-reported 2026-07-18): single
    // flat quads the original renders camera-billboarded — `refinery_flare` 16 m + the 4 m
    // `gen_flare_yellow` lamps (oil_liteflare.tif), `docklight_flare` 9.6 m blue pier lights
    // (dock_liteflare.tif), the 19.2 m lighthouse `litehsflare` (poleflare.tif), `bflare`
    // (beflare5.tif). World-fixed they show edge-on/skewed — the user's "lamps not oriented
    // to the camera". Classified by texture like the clouds; also never solid, never dimmed.
    // Widened 2026-07-21 to "fire"/"flame" — the refinery's own gas flame (fire101.tif) isn't
    // "*flare*"-named but is exactly the same kind of always-lit, non-solid billboard sprite
    // (surveyed across all 8 chapters: fireflare1 already matched "flare"; fire101/fire102/
    // fire_barrel01 are the only new matches, nothing else in the install contains either
    // substring — so this can't accidentally catch unrelated scenery).
    internal static bool IsFlareTexture(string tex) =>
        tex.Contains("flare", StringComparison.OrdinalIgnoreCase)
        || tex.Contains("fire", StringComparison.OrdinalIgnoreCase)
        || tex.Contains("flame", StringComparison.OrdinalIgnoreCase);

    // Rendered but not solid: the plane should fly through cloud/sky geometry and through
    // every billboard sprite, not crash into them. Terrain, water, buildings, zeppelins and
    // trains stay solid.
    private bool NoCollisionNode(GameZNode n) =>
        MeshUsesTexture(n, IsNonSolidSkyTexture) || IsBillboardNode(n);

    // `IsCloudOrSkyTexture`'s `sky*` prefix is right for the blend rule but WRONG for
    // collision, because `skywal*` is a BUILDING WALL texture, not sky (found 2026-07-22).
    // `MeshUsesTexture` matches if ANY polygon carries the texture, so one `skywal01` face
    // was making a whole structure phantom: C4's sky-city `pod2_hi` (73 polys, 107×88×125 m)
    // and `pod6_hi` (143 polys), `g74` (64 polys, 395×135×275 m), and C1/C1B/C2/C3's `g456`
    // (181 polys, 113×49×102 m), `racmplx` and `rabdr` — every one of them mixing `skywal*`
    // with unmistakable building textures (`jim_floor01`, `jim_rail1`, `jim_roof01`,
    // `bhfbuild03`, `flaghut2`, `flagstand`). The player could fly straight through them.
    //
    // Narrowed HERE rather than in `IsCloudOrSkyTexture` on purpose: that predicate is shared
    // with the cloud alpha-blend rule and MapEdgeExtender's ground-tile filter, and this is a
    // collision-only defect. C4's real deck is 144 `Sky1.tif` quads, which still match and
    // stay exempt; the skydome is a separate build that opts out wholesale anyway.
    private static bool IsNonSolidSkyTexture(string tex) =>
        IsCloudOrSkyTexture(tex)
        && !tex.StartsWith("skywal", StringComparison.OrdinalIgnoreCase);

    // A billboard is a flat card the engine turns toward the camera — it has no solid side to
    // hit, and its collider is a phantom wall wherever the card happens to be facing. Asking
    // the gamez model itself (SceneBuilder.ClassifyBillboard) replaced a poly-count + texture-
    // name heuristic on 2026-07-22: that rule exempted only single-polygon *flare*-textured
    // quads, so a tree card or any multi-poly facade was fully solid. No Facade model in the
    // install exceeds 3 polygons, so this cannot exempt real geometry — in particular C2/C5's
    // `cblock*` city-block buildings are ModelType "Default" and keep their collision.
    //
    // The classifier is per MESH and this predicate is per NODE, hence the MeshIndex hop; the
    // exemption is inherited by the whole subtree (see SceneBuilder.BuildSubtree).
    private bool IsBillboardNode(GameZNode n)
    {
        if (n.MeshIndex < 0 || n.MeshIndex >= _gamez.Meshes.Count)
            return false;
        var mesh = _gamez.Meshes[n.MeshIndex];
        return SceneBuilder.ClassifyBillboard(mesh) is { } kind
            ? kind != SceneBuilder.BillboardKind.None
            // Legacy v0.6.1 extraction: no ModelType, so keep the old flare-sprite rule
            // rather than guessing — this is the exact set that was exempt before.
            : mesh.Polygons.Count == 1 && MeshUsesTexture(n, IsFlareTexture);
    }

    // The horizontal 'cloudlayer' deck (the opaque overcast ceiling/floor) as opposed to the
    // cloud1/cloud2 sprites. Split out of the static world (into CloudDeck) so PlaneViewer can
    // make it follow the player. In C1 it is 144 top-level 1024-unit tiles at y=960 covering
    // the whole map (each a partition-referenced Object3d leaf).
    private bool IsCloudLayerDeckNode(GameZNode n) => MeshUsesTexture(n,
        tex => tex.StartsWith("cloudlayer", StringComparison.OrdinalIgnoreCase));

    // True if any of the node's own-mesh polygons is skinned with a texture matching the predicate.
    private bool MeshUsesTexture(GameZNode n, Func<string, bool> match)
    {
        if (n.MeshIndex < 0 || n.MeshIndex >= _gamez.Meshes.Count)
            return false;
        foreach (var poly in _gamez.Meshes[n.MeshIndex].Polygons)
        {
            if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
                continue;
            var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
            if (tex != null && match(tex))
                return true;
        }
        return false;
    }

    public int MeshInstanceCount => _scene.MeshInstanceCount;
    public int ColliderCount => _scene.ColliderCount;

    /// <summary>Models animating their UVs — see <see cref="SceneBuilder.ScrollingModelCount"/>.
    /// Read after Build (and after BuildHorizon, which is where C1's daytime sky layer is).</summary>
    public int ScrollingModelCount => _scene.ScrollingModelCount;

    /// <summary>The cloudlayer deck as a separate node so the caller can make it follow the
    /// player (see PlaneViewer): the opaque overcast sheet tracks the plane and flips
    /// above/below at the cloud band, as in the original. A child of the world root at its
    /// original altitude; null if the world has no cloudlayer geometry.</summary>
    public Node3D? CloudDeck { get; private set; }

    /// <param name="collision">Attach static colliders to solid geometry so the flight
    /// loop can raycast against terrain and buildings. Off for static viewing.</param>
    /// <param name="scrollOverrides">Per-model UV scroll rates from the mission's interp boot
    /// script (<see cref="MissionSetup.ScrollByModel"/>). Null leaves every model on its own
    /// gamez <c>texture_scroll</c> field, which is what a chapter with no scroll statements
    /// gets either way.</param>
    public WorldBuilder(GameZ gamez, TextureArchive textures, bool collision = false,
        IReadOnlyDictionary<int, Vector2>? scrollOverrides = null)
    {
        _gamez = gamez;
        _textures = textures;
        // Clouds are the only cloud*/sky* surfaces with an alpha channel, so this blend rule
        // touches only them; the opaque Sky1.tif skydome walls and cloudlayer deck are unaffected.
        // The cloud sprites additionally billboard toward the camera (cloudlayer deck excluded).
        _scene = new SceneBuilder(gamez, textures, fullbright: true,
            generateCollision: collision, blendTexture: IsCloudOrSkyTexture,
            billboardTexture: IsCloudSpriteTexture, glowTexture: IsFlareTexture,
            scrollOverrides: scrollOverrides);
        _scene.Cycler = Cycler;
    }

    /// <summary>Drives the world's material texture flipbooks (animated water, surf, boat wakes,
    /// turbulence, the walking crowd). Built here so its frames resolve while the session's
    /// TextureArchive is open; the caller adds it to the scene tree. Empty on chapters whose
    /// materials carry no cycle, and it costs nothing then.</summary>
    public readonly TextureCycler Cycler = new() { Name = "TextureCycler" };

    public Node3D Build(string worldName = "world1")
    {
        GameZNode? world = null;
        foreach (var n in _gamez.Nodes)
            if (n.Kind == "World" && string.Equals(n.Name, worldName, StringComparison.OrdinalIgnoreCase))
            {
                world = n;
                break;
            }
        if (world == null)
            throw new ArgumentException($"world node '{worldName}' not found in GameZ data");

        var root = new Node3D { Name = worldName };
        // The cloudlayer deck is collected into its own node (kept a child of the world root at
        // identity, so its world-space tile geometry stays put) that PlaneViewer moves to follow
        // the player; everything else goes into the static world root.
        var deck = new Node3D { Name = "cloud_deck" };

        foreach (var childIndex in world.Children)
            Add(root, deck, childIndex);
        if (world.PartitionNodes != null)
            foreach (var idx in world.PartitionNodes)
                Add(root, deck, idx);

        if (deck.GetChildCount() > 0)
        {
            root.AddChild(deck);
            CloudDeck = deck;
        }

        _builtWorld = world;
        return root;
    }

    /// <summary>
    /// Map-edge continuation (Run-2 item 7): a rolling window of mirrored terrain tiles
    /// following the plane past the map boundary, so the world continues indefinitely under
    /// the fog like the original's tile-reload grid (see MapEdgeExtender for the model and
    /// the video evidence). Call after Build (and after the chapter's clutter build, so the
    /// extension grows the same trees); add the returned node to the world root and drive
    /// its Update(cameraPos) each frame. Null when the world carries no area/partition grid
    /// or no recognizable ground tiles. Extension ground is collidable exactly when the
    /// WorldBuilder was created with collision (it shares this SceneBuilder); extension
    /// clutter is never collidable, like the map's own clutter.
    /// </summary>
    public MapEdgeExtender? CreateEdgeExtender(ClutterBuilder? clutter = null) =>
        _builtWorld == null ? null
            : MapEdgeExtender.Create(_gamez, _scene, _builtWorld, clutter);


    private void Add(Node3D root, Node3D deck, int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _gamez.Nodes.Count)
            return;
        var node = _gamez.Nodes[nodeIndex];
        var built = _scene.BuildSubtree(node, SkipWorldNode, NoCollisionNode);
        if (built != null)
            (IsCloudLayerDeckNode(node) ? deck : root).AddChild(built);
    }

    /// <summary>
    /// Builds the original skydome (the world's 'horizon' subtree) as a separate node the
    /// caller anchors to the camera. The dome's verts are centered on the origin (~8.8 km
    /// radius) while the world area is x,z ∈ [-12288, 0], so the original engine must have
    /// translated it with the viewer — it is a backdrop, not scenery. Zones are day/night
    /// variants: zone2 = moon + stars + Sky1.tif dusk-gradient night sky (what the original
    /// shows at the C1 airfield, which always loads at night); zone1 = sky2.tif day haze
    /// dome with an unfinished flat-gray cap, likely never player-visible.
    /// Never collidable, never casts shadows.
    ///
    /// <para><b>The zone names are per chapter (2026-07-22).</b> C1–C4's horizon has
    /// <c>zone1</c>/<c>zone2</c> children, but C5's has <c>zone3</c>/<c>zone1</c> — so a bare
    /// <c>zone2</c> request there matched no child, the skip predicate below skipped both, and
    /// C5 built an empty dome. An absent zone therefore falls back to the horizon's first zone
    /// child, mirroring <see cref="Flight.WeatherState.ResolveZone"/>. In the normal path
    /// PlaneViewer has already resolved the zone against the mission's weather.json and this
    /// fallback is a no-op; it exists so a mission with no weather.json still gets a dome.</para>
    /// </summary>
    public Node3D? BuildHorizon(string zone = "zone2")
    {
        var horizon = _gamez.FindByName("horizon");
        if (horizon == null)
            return null;
        zone = ResolveHorizonZone(horizon, zone);
        bool SkipOtherZones(GameZNode n) =>
            n.Name.StartsWith("zone", StringComparison.OrdinalIgnoreCase)
            && !n.Name.Equals(zone, StringComparison.OrdinalIgnoreCase);
        var built = _scene.BuildSubtree(horizon, SkipOtherZones, collisionSkip: _ => true);
        if (built == null)
            return null;
        DisableShadows(built);
        BillboardMoon(built);
        DisableLightRangeFade(built);
        // NOT opted out of fog (user change with the 2026-07-17 fog remodel, and the
        // cylinder model is what makes that correct): high dome fragments stay clear via the
        // FOG_ALTITUDE fade, while the horizon band fogs toward the same gray as the terrain
        // fog wall. The `csky_fog_on = 0` walk this used to call was deleted 2026-07-22 —
        // it had been dead since that change.
        return built;
    }

    // The horizon's zone children in flat-list order, falling an absent request back to the
    // first one. Logged at most once per WorldBuilder: BuildHorizon is called once per
    // splitscreen rig, and four identical warnings would read as four separate faults.
    private bool _loggedHorizonZoneFallback;

    private string ResolveHorizonZone(GameZNode horizon, string zone)
    {
        var zones = new List<string>();
        foreach (var childIndex in horizon.Children)
            if (childIndex >= 0 && childIndex < _gamez.Nodes.Count
                && _gamez.Nodes[childIndex].Name is { } name
                && name.StartsWith("zone", StringComparison.OrdinalIgnoreCase))
                zones.Add(name);
        if (zones.Count == 0 || zones.Exists(z => z.Equals(zone, StringComparison.OrdinalIgnoreCase)))
            return zone;
        if (!_loggedHorizonZoneFallback)
        {
            _loggedHorizonZoneFallback = true;
            GD.Print($"horizon: no '{zone}' subtree (has {string.Join("/", zones)}) — "
                     + $"building '{zones[0]}'");
        }
        return zones[0];
    }

    // The dome's star point-lights sit ~22 km out (camera-anchored, 2.5× scaled) — far past
    // their data visibility range (4000 m), which is meant for in-world beacons. Opt them
    // out of the light shader's distance fade or the night sky goes starless.
    private static void DisableLightRangeFade(Node node)
    {
        if (node is MeshInstance3D mi && mi.Name == "lights")
            mi.SetInstanceShaderParameter("csky_light_fade", 0f);
        foreach (var child in node.GetChildren())
            DisableLightRangeFade(child);
    }

    // The source moon is an axis-aligned quad (constant z), which looks tilted and
    // foreshortened from most headings — but original-game screenshots show a round,
    // upright moon from any direction, so the engine must billboard it. Replace the
    // static quad with a camera-facing one of the same position and size.
    // Blending (from original screenshots): the moon shows crater detail (not additive)
    // with sky right up to its soft halo and no quad edge (not opaque) — so the engine
    // color-keys the uniform background (66,73,99) away; we reproduce that with an
    // alpha ramp on distance from the background color.
    private void BillboardMoon(Node3D built)
    {
        var moonNode = FindChildByName(built, "moon");
        var gzMoon = _gamez.FindByName("moon");
        if (moonNode == null || gzMoon == null
            || gzMoon.MeshIndex < 0 || gzMoon.MeshIndex >= _gamez.Meshes.Count)
            return;
        var verts = _gamez.Meshes[gzMoon.MeshIndex].Vertices;
        if (verts.Count == 0)
            return;
        Vector3 min = verts[0], max = verts[0];
        foreach (var v in verts)
        {
            min = min.Min(v);
            max = max.Max(v);
        }
        var size = max - min;
        float side = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));

        foreach (var child in moonNode.GetChildren())
            if (child is MeshInstance3D)
                child.QueueFree();

        var tex = _textures.Find("moon1.tif");
        if (tex == null)
            return;
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale = true, // billboards ignore inherited scale (the 2.5× dome anchor) without this
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoTexture = ColorKeyed(tex),
        };
        moonNode.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(side, side), Material = mat },
            Position = (min + max) * 0.5f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Name = "mesh",
        });
    }

    // Alpha from distance to the background color (sampled at a corner): background → 0,
    // the painted glow halo → partial, the moon disc → 1. Reproduces the original's
    // color-key so the sky shows through right up to the halo, with no hard quad edge.
    private static ImageTexture ColorKeyed(ImageTexture tex)
    {
        var img = tex.GetImage();
        img.ClearMipmaps();
        img.Convert(Image.Format.Rgba8);
        var bg = img.GetPixel(0, 0);
        const float ramp = 0.25f; // channels this far from the background are fully opaque
        for (int y = 0; y < img.GetHeight(); y++)
            for (int x = 0; x < img.GetWidth(); x++)
            {
                var c = img.GetPixel(x, y);
                float d = Mathf.Max(Mathf.Abs(c.R - bg.R),
                    Mathf.Max(Mathf.Abs(c.G - bg.G), Mathf.Abs(c.B - bg.B)));
                c.A = Mathf.Clamp(d / ramp, 0f, 1f);
                img.SetPixel(x, y, c);
            }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    private static Node3D? FindChildByName(Node3D root, string name)
    {
        if (root.Name.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
            return root;
        foreach (var child in root.GetChildren())
            if (child is Node3D n3d && FindChildByName(n3d, name) is { } found)
                return found;
        return null;
    }

    /// <summary>Builds the mission's danger-zone route ribbons — the 'dzpaths' subtree the
    /// world build skips (see <see cref="SkipWorldNode"/>). This is AI/route guide data the
    /// original never renders (the dzN completion points sit on these polylines); exposed only
    /// for --debug-dzpaths inspection. Never collidable. Null when the world has no dzpaths.</summary>
    public Node3D? BuildDzPaths()
    {
        var dzpaths = _gamez.FindByName("dzpaths");
        return dzpaths == null ? null
            : _scene.BuildSubtree(dzpaths, collisionSkip: _ => true);
    }

    // The dome would otherwise shadow the entire world (it covers the whole sky).
    private static void DisableShadows(Node node)
    {
        if (node is MeshInstance3D mi)
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        foreach (var child in node.GetChildren())
            DisableShadows(child);
    }

}

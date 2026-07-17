using System;
using Godot;

namespace CrimsonSkies.Mech3;

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

    // Non-scenery world content: 'horizon' is the original skydome (built separately via
    // BuildHorizon — as part of the world it would swallow the scene), 'fvol1'..'fvol9'
    // are flight-boundary volumes, 'dzpaths' are colored path ribbons.
    private static bool SkipWorldNode(GameZNode n) =>
        n.Name.Equals("horizon", StringComparison.OrdinalIgnoreCase)
        || n.Name.Equals("dzpaths", StringComparison.OrdinalIgnoreCase)
        || n.Name.StartsWith("fvol", StringComparison.OrdinalIgnoreCase);

    // A cloud- or sky-textured surface. Node names are unreliable for spotting these
    // (cloud layers turn up under generic names like 'g27517'), so we classify by texture.
    // Two callers share this rule: the collision exemption (below) and the cloud alpha-blend
    // (passed to SceneBuilder) — clouds are neither solid nor hard-edged cutouts.
    private static bool IsCloudOrSkyTexture(string tex) =>
        tex.StartsWith("cloud", StringComparison.OrdinalIgnoreCase)
        || tex.StartsWith("sky", StringComparison.OrdinalIgnoreCase);

    // The cloud SPRITES only: cloud1/cloud2 are soft vertical cards (2D billboards), while
    // 'cloudlayer' is the flat horizontal deck sheet — which must stay flat, not billboard.
    // These face the camera; SceneBuilder recenters their quads so they pivot correctly.
    private static bool IsCloudSpriteTexture(string tex) =>
        tex.StartsWith("cloud", StringComparison.OrdinalIgnoreCase)
        && !tex.StartsWith("cloudlayer", StringComparison.OrdinalIgnoreCase);

    // Rendered but not solid: the plane should fly through cloud/sky geometry, not crash
    // into it. Terrain, water, buildings, zeppelins, trains stay solid.
    private bool NoCollisionNode(GameZNode n) => MeshUsesTexture(n, IsCloudOrSkyTexture);

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

    /// <summary>The cloudlayer deck as a separate node so the caller can make it follow the
    /// player (see PlaneViewer): the opaque overcast sheet tracks the plane and flips
    /// above/below at the cloud band, as in the original. A child of the world root at its
    /// original altitude; null if the world has no cloudlayer geometry.</summary>
    public Node3D? CloudDeck { get; private set; }

    /// <param name="collision">Attach static colliders to solid geometry so the flight
    /// loop can raycast against terrain and buildings. Off for static viewing.</param>
    public WorldBuilder(GameZ gamez, TextureArchive textures, bool collision = false)
    {
        _gamez = gamez;
        _textures = textures;
        // Clouds are the only cloud*/sky* surfaces with an alpha channel, so this blend rule
        // touches only them; the opaque Sky1.tif skydome walls and cloudlayer deck are unaffected.
        // The cloud sprites additionally billboard toward the camera (cloudlayer deck excluded).
        _scene = new SceneBuilder(gamez, textures, fullbright: true,
            generateCollision: collision, blendTexture: IsCloudOrSkyTexture,
            billboardTexture: IsCloudSpriteTexture);
    }

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
        return root;
    }

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
    /// </summary>
    public Node3D? BuildHorizon(string zone = "zone2")
    {
        var horizon = _gamez.FindByName("horizon");
        if (horizon == null)
            return null;
        bool SkipOtherZones(GameZNode n) =>
            n.Name.StartsWith("zone", StringComparison.OrdinalIgnoreCase)
            && !n.Name.Equals(zone, StringComparison.OrdinalIgnoreCase);
        var built = _scene.BuildSubtree(horizon, SkipOtherZones, collisionSkip: _ => true);
        if (built == null)
            return null;
        DisableShadows(built);
        BillboardMoon(built);
        DisableFog(built);
        return built;
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

    // The dome would otherwise shadow the entire world (it covers the whole sky).
    private static void DisableShadows(Node node)
    {
        if (node is MeshInstance3D mi)
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        foreach (var child in node.GetChildren())
            DisableShadows(child);
    }

    // Opt the skydome out of distance fog (SceneBuilder's csky_fog_on instance uniform): it
    // is a camera-anchored backdrop ~22 km out, far past FOG_FAR, so fog would paint the whole
    // sky solid FOG_COLOR. Harmless on the moon/stars (StandardMaterial3D — they ignore it).
    private static void DisableFog(Node node)
    {
        return;
        if (node is MeshInstance3D mi)
            mi.SetInstanceShaderParameter("csky_fog_on", 0f);
        foreach (var child in node.GetChildren())
            DisableFog(child);
    }
}

using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>One <c>zone*</c> child of the gamez <c>horizon</c> node, and how many meshed nodes its
/// subtree carries — the dome <see cref="WorldBuilder.BuildHorizon"/> would build for it.
/// <see cref="MeshedNodes"/> 0 means the zone is a bare marker (<c>model_index: -1</c> with no
/// children), which is exactly what C1B, C2 and C3 ship for <c>zone2</c>.</summary>
public readonly record struct HorizonZone(string Name, int MeshedNodes)
{
    /// <summary>The zone has a dome to build.</summary>
    public bool BuildsGeometry => MeshedNodes > 0;
}

/// <summary>
/// Builds the whole world of a chapter gamez.zbd (terrain chunks, buildings, zeppelins,
/// horizon). World content lives in two places: the World node's children, and ~350
/// top-level subtrees referenced only through the World's spatial partition grid.
/// </summary>
public sealed class WorldBuilder
{
    /// <summary>Drives the world's material texture flipbooks (animated water, surf, boat wakes,
    /// turbulence, the walking crowd). Built here so its frames resolve while the session's
    /// TextureArchive is open; the caller adds it to the scene tree. Empty on chapters whose
    /// materials carry no cycle, and it costs nothing then.</summary>
    public readonly TextureCycler Cycler = new() { Name = "TextureCycler" };

    // The horizontal overcast DECK — the sheet covering the whole map at one altitude, as
    // opposed to the cloud1/cloud2 sprites. Split out of the static world (into CloudDeck) so
    // GameSession can make it follow the player. In C1 it is 144 top-level 1024-unit tiles at
    // y=960 covering the whole map (each a partition-referenced Object3d leaf).
    //
    // Classified STRUCTURALLY, not by texture. The old rule was
    // the texture prefix 'cloudlayer', which is right for C1/C1C/C2B and misses C4 entirely:
    // C4's deck is skinned Sky1.tif (144 parentless partition-referenced nodes g1720..g1863,
    // each a single flat 1024x1024 quad at y=1050 — bit-for-bit C1's signature at y=960), so
    // CloudDeck came back null there and the deck stayed world-fixed as the plane flew on.
    //
    // Widening the texture rule to 'sky*' is NOT the fix, and not for the reason the plan gave
    // (that it leans on Build skipping 'horizon'). 'skywal*' is a BUILDING texture inside the
    // world walk — 33 C4 nodes (the sky-city pods), 4 in C1/C2/C3, 2 in C1B — and three C4
    // terrain roots carry a skywal01 polygon alongside their cliff/rail/bridge ones. A 'sky*'
    // rule would drag those solid terrain chunks into the deck and make them follow the player.
    //
    // The structural signature instead: a walk root whose model is ONE flat horizontal quad,
    // bucketed with its co-altitude peers, where the bucket's footprint covers the World node's
    // own 'area' rect. Measured over all 8 chapters:
    //
    //   C1 / C1C / C2B   cloudlayer.tif  144 tiles  y=960   coverage 1.000
    //   C4               Sky1.tif        144 tiles  y=1050  coverage 1.000
    //   every other flat-tile bucket in the install (C2 water + resblock, C4 water,
    //   C5 water + cblock1/2/3)                             coverage <= 0.098
    //
    // A 10x margin either side of the 0.5 threshold. C1B and C3 have no flat-tile bucket at all
    // and correctly resolve no deck. Note the deck is NOT identifiable by sitting above the
    // world: C4's tallest non-tile root reaches y=1490, well over its own deck at 1050.
    //
    // Deck tiles are the ONE exception to the backface culling below: they build
    // forceDoubleSided (see Add). Every one of the 144 tiles carries show_backface: false in both
    // C1 (models 1004-1147) and C4, yet the player flies THROUGH the deck — Tick follows it in
    // X/Z and never re-orients it (WeatherRig.cs), so the same quad has to read as a ceiling from
    // below and a floor from above.
    //
    // The side culling lost is the UNDERSIDE: every tile's authored face points +Y, so the floor
    // seen from above was unaffected (a C1 camera at y=1400 renders bit-identical with and
    // without this) while the overcast CEILING — the ordinary in-flight view — vanished
    // completely. That is what the goldens caught: the four deck chapters moved, the four
    // deckless ones did not, and each diff is confined to the top of the frame.
    //
    // (The original may instead flip the sheet to face the plane. That can only happen at the
    // deck's own plane, where the quad is edge-on and covers no pixels, so the two are visually
    // identical — double-siding just needs no per-frame state or threshold.)
    //
    // Corroborating the structural classifier: the deck tiles are also the only world nodes
    // flagged terrain AND !altitude_surface AND !intersect_surface — 144 in C1/C1C/C2B, the 144
    // C4 tiles plus 20 model-less g0 placeholders, and zero in the four deckless chapters.
    private const float DeckCoverageFraction = 0.5f;

    private readonly GameZ _gamez;
    private readonly TextureArchive _textures;
    private readonly SceneBuilder _scene;
    // Walk-root indices belonging to the deck; filled by FindCloudDeck before the walk.
    private readonly HashSet<int> _deckNodes = new();
    // The built `cloudparent` cluster subtrees; filled by CollectCloudClusters after the walk.
    private readonly List<Node3D> _cloudClusters = new();
    // Entities the chapter parks at the world origin awaiting placement (see
    // HideUnplacedEntities): the gamez node and the subtree we built for it.
    private readonly List<(GameZNode Node, Node3D Built)> _parkedAtOrigin = new();
    // What HideUnplacedEntities switched off, still awaiting the verdict below.
    private readonly List<(GameZNode Node, Node3D Built)> _hiddenUnplaced = new();
    private GameZNode? _builtWorld; // the World node of the last Build (for CreateEdgeExtender)
    private int _deckAltitude;
    private float _deckCoverage;
    // The horizon's zone children in flat-list order, falling an absent request back to the
    // first one. Logged at most once per WorldBuilder: BuildHorizon is called once per
    // splitscreen rig, and four identical warnings would read as four separate faults.
    private bool _loggedHorizonZoneFallback;

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
        // Backface culling, like the original: 60% of world polygons are single-sided in the
        // data (the other 40% carry SHOW_BACKFACE) and only cull it makes them right. Without
        // it a back-to-back pair — two polygons over the SAME vertices, opposite winding,
        // different textures, which is how the Hollywood backlot's facade panels put sky on
        // the front and framing on the back — is exactly coplanar and z-fights, unfixable by
        // depth bias; and the camera-anchored skydome's near wall draws over distant terrain
        // and cloud banks.
        _scene = new SceneBuilder(gamez, textures, fullbright: true,
            generateCollision: collision, blendTexture: IsCloudOrSkyTexture,
            billboardTexture: IsCloudSpriteTexture, glowTexture: IsFlareTexture,
            cullBackfaces: true, scrollOverrides: scrollOverrides);
        _scene.Cycler = Cycler;
    }

    public int MeshInstanceCount => _scene.MeshInstanceCount;
    public int ColliderCount => _scene.ColliderCount;

    /// <summary>Models animating their UVs — see <see cref="SceneBuilder.ScrollingModelCount"/>.
    /// Read after Build (and after BuildHorizon, which is where C1's daytime sky layer is).</summary>
    public int ScrollingModelCount => _scene.ScrollingModelCount;

    /// <summary>Models built from their authored <c>lighting: false</c> / <c>fog: false</c>
    /// flags — see <see cref="SceneBuilder.UnlitModelCount"/>.</summary>
    public int UnlitModelCount => _scene.UnlitModelCount;
    public int UnfoggedModelCount => _scene.UnfoggedModelCount;

    /// <summary>Overlay-pass surfaces built, and overlay polygons declined for want of a biasable
    /// material — see <see cref="SceneBuilder.OverlayPassSurfaceCount"/>.</summary>
    public int OverlayPassSurfaceCount => _scene.OverlayPassSurfaceCount;
    public int OverlayPassDeclinedCount => _scene.OverlayPassDeclinedCount;

    /// <summary>The overcast deck as a separate node so the caller can make it follow the
    /// player (see GameSession): the opaque overcast sheet tracks the plane and flips
    /// above/below at the cloud band, as in the original. A child of the world root at its
    /// original altitude; null if the world has no map-covering deck (C1B/C2/C3/C5).</summary>
    public Node3D? CloudDeck { get; private set; }

    /// <summary>The world's placed <c>cloudparent</c> cluster subtrees, in walk order — the
    /// ambient cloud population that is ordinary world geometry rather than <c>fvol</c> clutter
    /// (C1 ships 28, C1B 70, C4 45; C2/C3 none). Censused so the caller can put them on the
    /// shared cloud-field visual layer with the clutter, since the two are one population to a
    /// camera's altitude gate (A7). Empty until <see cref="Build"/> has run.
    ///
    /// <para>⚠ Resolved by the node's ORIGINAL gamez name (<c>AnimRuntime.NameMeta</c>), never
    /// by <c>Node.Name</c>: all 28 of C1's are literally named <c>cloudparent</c>, so Godot's
    /// duplicate-sibling renaming is free to have touched the built name (WORLD-8).</para></summary>
    public IReadOnlyList<Node3D> CloudClusters => _cloudClusters;

    /// <summary>This world's shared scene builder — its mesh/material/shape caches and its
    /// fullbright world materials. Handed to <see cref="ClutterBuilder"/> so the clutter's 3D
    /// city-block decorations render as real world geometry (same shader, same fog, same depth
    /// bias) instead of through the billboard path, and share the caches with the placed world.</summary>
    internal SceneBuilder Scene => _scene;

    /// <summary>
    /// Every gamez node the <c>--node=</c> request names, in flat-list order. Matching is on the
    /// <b>source</b> name (what <see cref="AnimRuntime.NameMeta"/> carries), case-insensitively and
    /// with the data's <c>.flt</c> model suffix optional — Godot node names are sanitized and
    /// auto-renamed, so they are never the thing to match on. Duplicate names are normal in this
    /// data (C1 has seven <c>rock_zeppelin</c>s), which is why this returns the whole list.
    /// </summary>
    public static List<GameZNode> MatchNodes(GameZ gamez, string request)
    {
        var hits = new List<GameZNode>();
        foreach (var n in gamez.Nodes)
        {
            if (n.Name.Length == 0)
            {
                continue;
            }
            if (string.Equals(n.Name, request, StringComparison.OrdinalIgnoreCase)
                || (n.Name.EndsWith(".flt", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(n.Name[..^4], request, StringComparison.OrdinalIgnoreCase)))
            {
                hits.Add(n);
            }
        }
        return hits;
    }

    /// <summary>Distinct source names CONTAINING the request — what a miss offers instead of
    /// nothing, so a mistyped <c>--node=</c> is one line away from the right spelling.</summary>
    public static List<string> SuggestNodes(GameZ gamez, string request, int cap)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var n in gamez.Nodes)
        {
            if (n.Name.Length == 0 || !n.Name.Contains(request, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (seen.Add(n.Name) && names.Count < cap)
            {
                names.Add(n.Name);
            }
        }
        return names;
    }

    /// <summary>World-frame union of a built subtree's mesh AABBs, computed from the meshes and the
    /// node transforms rather than from <c>GlobalTransform</c> — so it is valid <b>before</b> the
    /// subtree joins the scene tree, where <c>GlobalTransform</c> returns identity and logs an error
    /// per call. Null when the subtree draws nothing.</summary>
    public static Aabb? DetachedWorldAabb(Node3D root) => SubtreeAabb(root, root.Transform);

    /// <summary>The <c>horizon</c> node's zone children in gamez order, each with the number of
    /// meshed nodes its subtree carries — i.e. how much dome <see cref="BuildHorizon"/> would
    /// actually build for it. Empty when the chapter ships no <c>horizon</c> node.
    ///
    /// <para>Read <b>before</b> the horizon build, because the zone the fog and the dome share is
    /// chosen from it: three chapters ship a <c>zone2</c> that is a bare marker
    /// (<c>model_index: -1</c>, no children), so requesting it renders no sky at all. The
    /// selection rule itself is <see cref="Flight.WeatherState.ResolveZone(string,
    /// IReadOnlyList{HorizonZone})"/> — it lives beside the fog so the pair cannot diverge.</para>
    ///
    /// <para>Static over a <see cref="GameZ"/> so it needs no built scene: the zone has to be
    /// settled before the world is built, and the census is testable off-engine that way.</para>
    /// </summary>
    public static IReadOnlyList<HorizonZone> HorizonZonesOf(GameZ gamez)
    {
        var zones = new List<HorizonZone>();
        var horizon = gamez.FindByName("horizon");
        if (horizon == null)
            return zones;
        foreach (var childIndex in horizon.Children)
        {
            if (childIndex < 0 || childIndex >= gamez.Nodes.Count)
                continue;
            var child = gamez.Nodes[childIndex];
            if (!child.Name.StartsWith("zone", StringComparison.OrdinalIgnoreCase))
                continue;
            zones.Add(new HorizonZone(child.Name, MeshedNodesIn(gamez, child)));
        }
        return zones;
    }

    /// <inheritdoc cref="HorizonZonesOf"/>
    public IReadOnlyList<HorizonZone> HorizonZones() => HorizonZonesOf(_gamez);

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
        // identity, so its world-space tile geometry stays put) that GameSession moves to follow
        // the player; everything else goes into the static world root.
        var deck = new Node3D { Name = "cloud_deck" };

        var roots = new List<int>(world.Children);
        if (world.PartitionNodes != null)
            roots.AddRange(world.PartitionNodes);

        FindCloudDeck(world, roots);
        RankConflicts(roots);
        foreach (var idx in roots)
            Add(root, deck, idx);

        if (deck.GetChildCount() > 0)
        {
            root.AddChild(deck);
            CloudDeck = deck;
            GD.Print($"cloud deck: {deck.GetChildCount()} tiles at y={_deckAltitude} "
                     + $"({_deckCoverage:P0} of the map)");
        }

        // The placed cloud clusters, censused after the walk (they are nested deep — C1's sit
        // at world1 → g0|g27816 → l2586 (Lod) → cloudparent — so there is no walk root to
        // recognise). Said out loud per chapter: "0 clusters" is a real answer for C2/C3 and
        // must not read the same as a census that stopped working.
        CollectCloudClusters(root);
        if (_cloudClusters.Count > 0)
        {
            GD.Print($"cloud clusters: {_cloudClusters.Count} placed 'cloudparent' subtree(s)");
        }

        _builtWorld = world;
        return root;
    }

    /// <summary>
    /// Builds ONE named subtree as a standalone stage instead of the whole world (<c>--node=</c>).
    /// The subtree is placed at its <b>world</b> transform — accumulated up the parent chain via
    /// <see cref="GameZ.WorldTransformOf"/> — so a node nested under a placed parent sits where the
    /// full world would have put it, not at the origin.
    ///
    /// <para>Deliberately unlike <see cref="Build"/> in three ways, each of which would otherwise
    /// erase the subject: no <see cref="SkipWorldNode"/> filter (the caller named this subtree, so
    /// even <c>horizon</c>/<c>dzpaths</c> build), no cloud-deck split, and <b>no
    /// origin-parked registration</b> — <see cref="HideUnplacedEntities"/> would switch off exactly
    /// the transformless vehicle a <c>--node=</c> run most often asks for.</para>
    /// </summary>
    public Node3D BuildNode(GameZ gamez, GameZNode node)
    {
        var root = new Node3D { Name = "world1" };
        var built = _scene.BuildSubtree(node, skip: null, collisionSkip: NoCollisionNode);
        if (built != null)
        {
            built.Transform = gamez.WorldTransformOf(node);
            root.AddChild(built);
        }
        return root;
    }

    /// <summary>
    /// Map-edge continuation: a rolling window of mirrored terrain tiles
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

    /// <summary>
    /// Switches off the entities nothing ever placed. Call once AFTER the animation runtime's
    /// bootstrap (<c>AnimRuntime.Bind</c>), which is what runs the mission's interp setup script
    /// and its ON_STARTUP definitions — i.e. every mechanism that legitimately places or hides
    /// one of these. Returns the names switched off.
    ///
    /// <para><b>Why this exists.</b> A chapter gamez holds every mission's content, and the
    /// chapter's build script (<c>support\&lt;ch&gt;\load.gw</c> in <c>interp.json</c>) loads each
    /// vehicle with a bare <c>LoadGameGen</c> + <c>AddChild &lt;worldName&gt;</c> and no placement
    /// whatsoever — so every zeppelin, car, boat and train car in the install starts life parked
    /// at the world origin. A mission then either switches it off (its <c>.gw</c> setup script) or
    /// places it (an ON_STARTUP <c>ObjectTranslateState</c>, e.g. C3/IA1's <c>cgzepstate</c> puts
    /// <c>cargozep1</c> at (-12412.9, 134.0, -10424.8)). Retail data misses a few: C5/IA1 switches
    /// off nine zeppelins but not <c>piratezep</c>, and C1C/IA1's four-line script leaves three.
    /// Those render as a heap of vehicles at the map corner — the reported "sunk zeppelin".</para>
    ///
    /// <para><b>Why the test is post-bootstrap.</b> Being still at the origin once every placement
    /// mechanism has run IS the definition of unplaced, so this cannot fight the data: anything the
    /// setup script hid is already invisible and is skipped, anything an ON_STARTUP
    /// OBJECT_TRANSLATE_STATE moved is no longer at the origin and is skipped (C3/IA1's
    /// <c>cargozep1</c> is the worked example). It also needs no list of zeppelin names —
    /// C2/IA1's real population here is police cars and boats, not a zeppelin at all.</para>
    ///
    /// <para><b>⚠ This is only half the rule — <see cref="RestorePlacedEntities"/> is the other
    /// half, and without it this method is WRONG.</b> A large class of entities is placed by
    /// OBJECT_MOTION_FROM_TO rather than by a translate: a motion over time, which at bootstrap has
    /// only been REGISTERED, so its target is still sitting on the origin here and is
    /// indistinguishable from unplaced content. Measured: on its own this sweep switched off 35
    /// entities in C2/IA1, among them four yachts, three sailboats and ten studebakers that drive
    /// away perfectly well a moment later, plus C1's <c>tanker_car</c>/<c>box_car</c>/<c>caboose</c>
    /// and C2's <c>rocket</c>. Hence hide-then-restore: everything suspicious goes off immediately
    /// (so none of it is ever seen), and anything that subsequently MOVES is put back.</para>
    ///
    /// <para>Do not decide this from the animation program instead — that approach is too
    /// fragile: sparing the anchors of ON_STARTUP / <c>startanims</c> definitions still misses the
    /// train cars (driven by events inside a definition anchored on <c>passenger_trengine</c>, not
    /// on themselves) and C2's rocket, would need the CallAnimation graph walked transitively, and
    /// could not spare index-referenced targets at all. Watching where the node actually ends up
    /// needs none of that and cannot disagree with the runtime.</para>
    /// </summary>
    public List<string> HideUnplacedEntities()
    {
        _hiddenUnplaced.Clear();
        var hidden = new List<string>();
        foreach (var (node, built) in _parkedAtOrigin)
        {
            if (!GodotObject.IsInstanceValid(built) || !built.Visible)
            {
                continue; // the mission's setup script already switched it off
            }
            if (!built.Transform.Origin.IsZeroApprox())
            {
                continue; // an ON_STARTUP translate placed it — this is real, shown content
            }
            built.Visible = false; // colliders follow (WorldCollision)
            _hiddenUnplaced.Add((node, built));
            hidden.Add(node.Name);
        }
        return hidden;
    }

    /// <summary>
    /// The other half of <see cref="HideUnplacedEntities"/>: restores anything that has since moved
    /// off the world origin, because moving is proof that a definition owns it after all. Call
    /// repeatedly (GameSession polls it once a second) — an entity leaves the origin whenever its
    /// motion happens to start, and for an OnCall definition that can be at any time, so there is
    /// no deadline after which it is safe to stop asking. Costs one vector compare per node still
    /// hidden, and each one drops out of the list for good once restored.
    /// </summary>
    public List<string> RestorePlacedEntities()
    {
        var restored = new List<string>();
        for (int i = _hiddenUnplaced.Count - 1; i >= 0; i--)
        {
            var (node, built) = _hiddenUnplaced[i];
            if (!GodotObject.IsInstanceValid(built))
            {
                _hiddenUnplaced.RemoveAt(i);
                continue;
            }
            if (built.Transform.Origin.IsZeroApprox())
            {
                continue; // still parked — leave it switched off
            }
            built.Visible = true;
            _hiddenUnplaced.RemoveAt(i);
            restored.Add(node.Name);
        }
        return restored;
    }

    /// <summary>
    /// Builds the original skydome (the world's 'horizon' subtree) as a separate node the
    /// caller anchors to the camera. The dome's verts are centered on the origin (~8.8 km
    /// radius) while the world area is x,z ∈ [-12288, 0], so the original engine must have
    /// translated it with the viewer — it is a backdrop, not scenery. Zones are day/night
    /// variants: zone2 = moon + stars + Sky1.tif dusk-gradient night sky (what the original
    /// shows at the C1 airfield, which always loads at night); zone1 = sky2.tif day haze dome.
    /// Never collidable, never casts shadows.
    ///
    /// <para><b>Every dome is a textured wall plus an untextured skirt.</b> The wall runs from
    /// local Y=0 (the camera's own altitude, hence the horizon line) up to a flat cap; the skirt
    /// is a cone from Y=0 down to −3.0…−11.7 km, closed by a flat disc. The skirt carries no
    /// texture — one <c>Colored</c> material whose colour is the zone's own <c>FOG_COLOR</c>, so
    /// below the horizon the dome IS the fog wall the terrain fades into and the join is
    /// invisible. Nothing here paints that; it only has to not be doubled
    /// (<see cref="GameZ.VertexColorsRestateMaterialColor"/>).</para>
    ///
    /// <para><b>The zone names are per chapter.</b> C1–C4's horizon has
    /// <c>zone1</c>/<c>zone2</c> children, but C5's has <c>zone3</c>/<c>zone1</c> — so a bare
    /// <c>zone2</c> request there matched no child, the skip predicate below skipped both, and
    /// C5 built an empty dome. An absent zone therefore falls back to the horizon's first zone
    /// child, mirroring <see cref="Flight.WeatherState.ResolveZone"/>. In the normal path
    /// GameSession has already resolved the zone against the mission's weather.json and this
    /// fallback is a no-op; it exists so a mission with no weather.json still gets a dome.</para>
    ///
    /// <para><b>An empty zone is not an absent one.</b> C1B, C2 and C3 ship a <c>zone2</c> child
    /// that is a bare marker, so it matches here and builds a dome of zero meshes — the caller
    /// keeps that from happening by picking the zone off <see cref="HorizonZonesOf"/> before it
    /// gets here (<see cref="Flight.WeatherState.PreferPopulatedHorizonZone"/>). This method
    /// builds what it is asked for, including nothing.</para>
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
        // Every horizon model in every chapter is authored `fog: false` (measured: 3-6 meshed
        // dome nodes per chapter, all of them), and it is honoured here like everywhere else
        // (`lighting: false` always was). The 2026-07 cylindrical-fog remodel force-fogged the
        // dome instead (`SceneBuilder.ForceFogged`, set only from here), reasoning that high
        // dome fragments would stay clear via the FOG_ALTITUDE fade while the horizon band
        // greyed toward the terrain fog wall. `B16` (`docs/PLAN-overcast-match.md`) found that
        // premise false at the dome's own authored size: every chapter's dome tops out
        // +982…+4108 m over the camera, while every broken scene's FOG_ALTITUDE band sits at
        // 9000-11000 m — the altitude term is 1.0 on every dome fragment under either fog
        // hypothesis, so the "high fragments stay clear" half of the deal never happened and the
        // dome only ever painted flat fog colour (BL-303's C3/C2B/C5 skies, and the C1 above-deck
        // gray band one zone over). Reverted: the dome now builds unfogged, as authored.
        var built = _scene.BuildSubtree(horizon, SkipOtherZones, collisionSkip: _ => true);
        if (built == null)
            return null;
        DisableShadows(built);
        BillboardMoon(built);
        DisableLightRangeFade(built);
        return built;
    }

    /// <summary>Builds the mission's danger-zone route ribbons — the 'dzpaths' subtree the
    /// world build skips (see <see cref="SkipWorldNode"/>). This is AI/route guide data the
    /// original never renders (the dzN completion points sit on these polylines); exposed only
    /// for --debug-dzpaths inspection. Never collidable. Null when the world has no dzpaths.</summary>
    public Node3D? BuildDzPaths()
    {
        var dzpaths = _gamez.FindByName("dzpaths");
        return dzpaths == null ? null : BuildDzPathTree(dzpaths);
    }

    // Internal: ClutterBuilder walks the same placed world with the same exclusions.
    internal static bool SkipWorldNode(GameZNode n) =>
        n.Name.Equals("horizon", StringComparison.OrdinalIgnoreCase)
        || n.Name.Equals("dzpaths", StringComparison.OrdinalIgnoreCase)
        || IsFogVolumeNode(n);

    /// <summary>A placed ambient cloud cluster — the gamez node the original names
    /// <c>cloudparent</c>, whose children are the individual cloud facades. This is the OTHER
    /// ambient cloud population, world-placed rather than <c>fvol</c>-scattered, and the two are
    /// gated together by camera altitude (<see cref="CloudClusters"/>, A7).</summary>
    internal static bool IsCloudClusterName(string name) =>
        name.StartsWith("cloudparent", StringComparison.OrdinalIgnoreCase);

    /// <summary>A <c>fvol1</c>…<c>fvol34</c> fog-volume node: an invisible box the world walk
    /// skips (above) and <see cref="FogVolumeSpec.VolumesOf"/> measures. One predicate for both,
    /// so the set that is excluded from the render and the set that is filled with cloud clutter
    /// can never drift apart.</summary>
    internal static bool IsFogVolumeNode(GameZNode n) =>
        n.Name.StartsWith("fvol", StringComparison.OrdinalIgnoreCase);

    // Non-scenery world content: 'horizon' is the original skydome (built separately via
    // BuildHorizon — as part of the world it would swallow the scene), 'fvol1'..'fvol9'
    // are flight-boundary volumes, 'dzpaths' are colored path ribbons.
    // Internal: ClutterBuilder walks the same placed world with the same exclusions.
    // A cloud- or sky-textured surface. Node names are unreliable for spotting these
    // (cloud layers turn up under generic names like 'g27517'), so we classify by texture.
    // Three callers share this rule: the collision exemption (below), the cloud alpha-blend
    // (passed to SceneBuilder) — clouds are neither solid nor hard-edged cutouts — and
    // MapEdgeExtender's ground-tile classifier (cloudlayer deck tiles are cell-sized too).
    internal static bool IsCloudOrSkyTexture(string tex) =>
        tex.StartsWith("cloud", StringComparison.OrdinalIgnoreCase)
        || tex.StartsWith("sky", StringComparison.OrdinalIgnoreCase);

    // Light-source flare/fire sprites (validated in C1, user-reported): single
    // flat quads the original renders camera-billboarded — `refinery_flare` 16 m + the 4 m
    // `gen_flare_yellow` lamps (oil_liteflare.tif), `docklight_flare` 9.6 m blue pier lights
    // (dock_liteflare.tif), the 19.2 m lighthouse `litehsflare` (poleflare.tif), `bflare`
    // (beflare5.tif). World-fixed they show edge-on/skewed — the user's "lamps not oriented
    // to the camera". Classified by texture like the clouds; also never solid, never dimmed.
    // Widened to "fire"/"flame" — the refinery's own gas flame (fire101.tif) isn't
    // "*flare*"-named but is exactly the same kind of always-lit, non-solid billboard sprite
    // (surveyed across all 8 chapters: fireflare1 already matched "flare"; fire101/fire102/
    // fire_barrel01 are the only new matches, nothing else in the install contains either
    // substring — so this can't accidentally catch unrelated scenery).
    internal static bool IsFlareTexture(string tex) =>
        tex.Contains("flare", StringComparison.OrdinalIgnoreCase)
        || tex.Contains("fire", StringComparison.OrdinalIgnoreCase)
        || tex.Contains("flame", StringComparison.OrdinalIgnoreCase);

    // The cloud SPRITES only: cloud1/cloud2 are soft vertical cards (2D billboards), while
    // 'cloudlayer' is the flat horizontal deck sheet — which must stay flat, not billboard.
    // These face the camera; SceneBuilder recenters their quads so they pivot correctly.
    //
    // Must stay complementary to the deck (see FindCloudDeck below) or the deck tiles would
    // spin to face the camera. It is, across all 8 chapters: the two deck textures in this
    // install are 'cloudlayer.tif' (excluded explicitly) and C4's 'Sky1.tif' (does not start
    // with "cloud"), so C4's deck was never billboarded even while it went unrecognised — the
    // symptom there was purely that it did not follow the plane.
    private static bool IsCloudSpriteTexture(string tex) =>
        tex.StartsWith("cloud", StringComparison.OrdinalIgnoreCase)
        && !tex.StartsWith("cloudlayer", StringComparison.OrdinalIgnoreCase);

    // `IsCloudOrSkyTexture`'s `sky*` prefix is right for the blend rule but WRONG for
    // collision, because `skywal*` is a BUILDING WALL texture, not sky.
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

    /// <summary>Union of the subtree's mesh AABBs, expressed in <paramref name="xf"/>'s frame.
    /// Null when the subtree draws nothing. Computed from the built meshes rather than read
    /// from the tree so it does not depend on the world being in the scene tree yet.</summary>
    private static Aabb? SubtreeAabb(Node3D node, Transform3D xf)
    {
        Aabb? total = null;
        if (node is MeshInstance3D { Mesh: not null } mi)
        {
            total = xf * mi.GetAabb();
        }
        foreach (var child in node.GetChildren())
        {
            if (child is not Node3D c3d)
            {
                continue;
            }
            if (SubtreeAabb(c3d, xf * c3d.Transform) is { } sub)
            {
                total = total?.Merge(sub) ?? sub;
            }
        }
        return total;
    }

    /// <summary>
    /// True when this walk root is an ENTITY parked at the world origin rather than map
    /// geometry: it carries no transform of its own (gamez <c>"Initial"</c> — see
    /// <see cref="GameZ.ParseTransform"/>, which leaves <see cref="GameZNode.Local"/> null,
    /// so the subtree builds at its parent's origin, and every walk root's parent is the
    /// identity World node), AND its geometry wraps around that origin.
    ///
    /// <para>Both halves are needed and the second one is the load-bearing half. Roughly 300
    /// roots per chapter carry no transform — nearly all of them terrain, whose vertices are
    /// already in world coordinates and therefore sit out in the map rather than around the
    /// origin. Measured across all 8 chapters, adding the "wraps the origin" test cuts 340/154/
    /// 316/252/304/436/344/411 transformless roots down to 21/7/6/47/5/15/8/13, and **every one
    /// of those 122 is a vehicle** — zeppelins, cars, boats, train cars, the Spruce Goose, an
    /// autogyro bus, life rings — with `terrain=false` on all 122 and no terrain node anywhere
    /// in the set. ⚠ Do NOT try this test on the gamez `child_bbox` field instead: that box is
    /// in the node's LOCAL frame, so every placed terrain tile trivially contains its own
    /// origin and the same query returns 464 hits, nearly all terrain.</para>
    /// </summary>
    private static bool IsParkedAtOrigin(GameZNode node, Node3D built)
    {
        if (node.Local != null)
        {
            return false; // authored somewhere specific; wherever that is, it is not "unplaced"
        }
        var aabb = SubtreeAabb(built, Transform3D.Identity);
        if (aabb == null)
        {
            return false; // no geometry at all (empty group node) — nothing to draw either way
        }
        var box = aabb.Value;
        // Strictly straddling the origin in x and z. Map geometry never does: every chapter's
        // world `area` is x,z in [-N, 0], so the origin is the map's CORNER and real terrain
        // only ever touches it, never surrounds it.
        return box.Position.X < 0f && box.End.X > 0f
            && box.Position.Z < 0f && box.End.Z > 0f;
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

    // Meshed nodes in a subtree, the node itself included: what BuildHorizon has to draw for a
    // zone. Counts MeshIndex >= 0 rather than polygons, because that is the same test SceneBuilder
    // uses to decide a node carries a model at all; the distinction this serves is 0 vs 3-6.
    private static int MeshedNodesIn(GameZ gamez, GameZNode node)
    {
        int meshed = node.MeshIndex >= 0 ? 1 : 0;
        foreach (var childIndex in node.Children)
            if (childIndex >= 0 && childIndex < gamez.Nodes.Count)
                meshed += MeshedNodesIn(gamez, gamez.Nodes[childIndex]);
        return meshed;
    }

    // Rendered but not solid: the plane should fly through cloud/sky geometry and through
    // every billboard sprite, not crash into them. Terrain, water, buildings, zeppelins and
    // trains stay solid.
    //
    // The gamez data has its own say too: flags.intersect_surface is the original's per-node
    // collision-participation flag, false on exactly the geometry that should never stop a plane
    // or a round — spinning props (spin/counterspin/propstill), wreck/debris pieces (part*/pt*/
    // piece*/zdtop*), fire/flake/ripple/splash effects, light glows, ropes, shadows, and the C3
    // spiderweb (whose approach-triggered fade is pure EXECUTION_BY_RANGE and needs no contact).
    // Surveyed install-wide: all 419 distinct false-flagged names are non-solid things, no
    // terrain/water/building is ever false, and no false node has a collidable-flagged mesh
    // descendant — so inheriting the exemption down the subtree (like the other two rules) is
    // safe.
    /// <summary>
    /// Ranks this world's nodes by its conflict graph and hands the result to the scene builder,
    /// which reads it for every <c>node_bias</c> it sets from here on. Runs before the build
    /// because the rank is an input to it, like the polygon priority.
    ///
    /// <para>The walk mirrors <see cref="Add"/> + <see cref="SceneBuilder.BuildSubtree"/> exactly
    /// — the <c>active</c> flag, <see cref="SkipWorldNode"/>, and nearest-LOD-only — so the graph
    /// is over the geometry that will actually be built. The origin-parked pile is left out: those
    /// entities are switched off a moment later (<see cref="HideUnplacedEntities"/>) so nothing in
    /// it is on screen to fight, and it is also what makes the ranking affordable — it is a single
    /// interpenetrating heap that alone stretches the longest conflict chain from 7 to 27, which
    /// no step size fits inside one priority level.</para>
    /// </summary>
    private void RankConflicts(List<int> roots)
    {
        var start = Time.GetTicksMsec();
        var nodes = new List<(GameZNode Node, Transform3D World)>();
        var subtree = new List<(GameZNode Node, Transform3D World)>();
        int excluded = 0;
        foreach (int idx in roots)
        {
            if (idx < 0 || idx >= _gamez.Nodes.Count)
                continue;
            var rootNode = _gamez.Nodes[idx];
            if (!rootNode.Active || SkipWorldNode(rootNode))
                continue;
            subtree.Clear();
            Collect(rootNode, Transform3D.Identity, subtree);
            if (rootNode.Local == null && WrapsOrigin(subtree))
            {
                excluded++;
                continue;
            }
            nodes.AddRange(subtree);
        }

        var report = ConflictRank.Compute(_gamez, nodes, excluded);
        _scene.ConflictRanks = report.Ranks;
        GD.Print($"draw order: {report.Pairs} conflicting node pair(s) over {nodes.Count} node(s) / "
                 + $"{report.Triangles} triangle(s) -> {report.MaxRank + 1} rank(s), {excluded} "
                 + $"origin-parked root(s) excluded, {Time.GetTicksMsec() - start} ms");
        if (report.MaxRank > SceneBuilder.ConflictRankCap)
        {
            GD.PushWarning($"draw order: conflict chain {report.MaxRank + 1} exceeds the "
                           + $"{SceneBuilder.ConflictRankCap + 1}-rank budget; the deepest layers "
                           + "share a bias and can z-fight");
        }
    }

    private void Collect(GameZNode node, Transform3D parent, List<(GameZNode, Transform3D)> into)
    {
        if (SkipWorldNode(node) || (node.Kind == "Lod" && node.LodRangeMin != 0f))
            return;
        var world = node.Local is { } local ? parent * local : parent;
        into.Add((node, world));
        foreach (int child in node.Children)
        {
            if (child >= 0 && child < _gamez.Nodes.Count)
                Collect(_gamez.Nodes[child], world, into);
        }
    }

    // The geometry half of IsParkedAtOrigin, from the gamez meshes instead of the built tree —
    // the same union of the same vertices, taken before there is a tree to walk.
    private bool WrapsOrigin(List<(GameZNode Node, Transform3D World)> subtree)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;
        foreach (var (node, world) in subtree)
        {
            if (node.MeshIndex < 0 || node.MeshIndex >= _gamez.Meshes.Count)
                continue;
            foreach (var v in _gamez.Meshes[node.MeshIndex].Vertices)
            {
                var p = world * v;
                min = min.Min(p);
                max = max.Max(p);
                any = true;
            }
        }
        return any && min.X < 0f && max.X > 0f && min.Z < 0f && max.Z > 0f;
    }

    private Node3D BuildDzPathTree(GameZNode node)
    {
        var built = new Node3D { Name = node.Name };
        if (node.Local is { } local)
            built.Transform = local;
        if (node.MeshIndex >= 0 && node.MeshIndex < _gamez.Meshes.Count)
        {
            var mesh = _gamez.Meshes[node.MeshIndex];
            var materialCounts = new Dictionary<int, int>();
            foreach (var poly in mesh.Polygons)
                materialCounts[poly.MaterialIndex] = materialCounts.TryGetValue(poly.MaterialIndex, out int count)
                    ? count + 1 : 1;
            int gateNumber = 0;
            foreach (var poly in mesh.Polygons)
            {
                bool gate = materialCounts[poly.MaterialIndex] == 2;
                Color color = gate
                    ? ++gateNumber == 1 ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.5f)
                    : new Color(1f, 1f, 1f, 0.35f);
                built.AddChild(new MeshInstance3D
                {
                    Name = gate ? gateNumber == 1 ? "gate_green" : "gate_red" : "route",
                    Mesh = DebugPolygonMesh(mesh, poly, color, line: !gate),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
        }
        foreach (int childIndex in node.Children)
        {
            if (childIndex >= 0 && childIndex < _gamez.Nodes.Count)
                built.AddChild(BuildDzPathTree(_gamez.Nodes[childIndex]));
        }
        return built;
    }

    private ArrayMesh DebugPolygonMesh(GameZMesh mesh, GameZPolygon poly, Color color, bool line)
    {
        var surface = new SurfaceTool();
        surface.Begin(line ? Mesh.PrimitiveType.LineStrip : Mesh.PrimitiveType.Triangles);
        if (line)
        {
            foreach (int index in poly.VertexIndices)
                surface.AddVertex(mesh.Vertices[index]);
        }
        else
        {
            void AddTriangle(int a, int b, int c)
            {
                surface.AddVertex(mesh.Vertices[poly.VertexIndices[a]]);
                surface.AddVertex(mesh.Vertices[poly.VertexIndices[b]]);
                surface.AddVertex(mesh.Vertices[poly.VertexIndices[c]]);
            }
            if (poly.TriangleStrip)
            {
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                    if ((i & 1) == 0) AddTriangle(i, i + 1, i + 2); else AddTriangle(i, i + 2, i + 1);
            }
            else
            {
                for (int i = 1; i + 1 < poly.VertexIndices.Count; i++)
                    AddTriangle(0, i, i + 1);
            }
        }
        surface.SetMaterial(new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        var result = new ArrayMesh();
        surface.Commit(result);
        return result;
    }

    private bool NoCollisionNode(GameZNode n) =>
        !n.IntersectSurface || MeshUsesTexture(n, IsNonSolidSkyTexture) || IsBillboardNode(n);

    // A billboard is a flat card the engine turns toward the camera — it has no solid side to
    // hit, and its collider is a phantom wall wherever the card happens to be facing. Asking
    // the gamez model itself (SceneBuilder.ClassifyBillboard) replaced a poly-count + texture-
    // name heuristic: that rule exempted only single-polygon *flare*-textured
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

    // True when the node's model is one flat horizontal quad, reporting its altitude and its
    // world-space x/z footprint. Deck tiles carry no transform ("Initial"), but a null Local is
    // treated as identity anyway, so a placed tile would still be measured where it sits.
    private bool FlatTile(GameZNode n, out float altitude, out float x0, out float z0,
        out float x1, out float z1)
    {
        altitude = x0 = z0 = x1 = z1 = 0f;
        if (n.MeshIndex < 0 || n.MeshIndex >= _gamez.Meshes.Count)
            return false;
        var mesh = _gamez.Meshes[n.MeshIndex];
        if (mesh.Polygons.Count != 1 || mesh.Vertices.Count != 4)
            return false;
        var xf = n.Local ?? Transform3D.Identity;
        var first = xf * mesh.Vertices[0];
        x0 = x1 = first.X;
        z0 = z1 = first.Z;
        for (int i = 1; i < 4; i++)
        {
            var v = xf * mesh.Vertices[i];
            if (Mathf.Abs(v.Y - first.Y) > 0.001f)
                return false; // tilted — a wall or a ramp, not a deck tile
            x0 = Mathf.Min(x0, v.X);
            x1 = Mathf.Max(x1, v.X);
            z0 = Mathf.Min(z0, v.Z);
            z1 = Mathf.Max(z1, v.Z);
        }
        altitude = first.Y;
        return true;
    }

    // Walks the built world for cloudparent subtrees. Stops descending at each hit: the whole
    // subtree is the cluster, and cloud clusters do not nest. Matched on the name the DATA
    // carries (AnimRuntime.NameMeta), never on Node.Name — see the CloudClusters remarks.
    private void CollectCloudClusters(Node node)
    {
        if (node is Node3D n3d && n3d.HasMeta(AnimRuntime.NameMeta)
            && IsCloudClusterName(n3d.GetMeta(AnimRuntime.NameMeta).AsString()))
        {
            _cloudClusters.Add(n3d);
            return;
        }
        foreach (var child in node.GetChildren())
        {
            CollectCloudClusters(child);
        }
    }

    /// <summary>Picks the deck out of the world's flat-quad roots: bucket them by altitude
    /// (1 m buckets — a deck's tiles are exactly coplanar) and take the bucket whose footprint
    /// covers at least <see cref="DeckCoverageFraction"/> of the map. Tiles are clipped to the
    /// map rect and their areas summed rather than unioned; the decks are non-overlapping grids
    /// and the margin over every other bucket is 10x, so the approximation cannot flip a
    /// verdict here.</summary>
    private void FindCloudDeck(GameZNode world, List<int> roots)
    {
        _deckNodes.Clear();
        if (!world.HasArea)
            return;
        float mapW = Mathf.Abs(world.AreaRight - world.AreaLeft);
        float mapH = Mathf.Abs(world.AreaBottom - world.AreaTop);
        if (mapW <= 0f || mapH <= 0f)
            return;
        float mapLeft = Mathf.Min(world.AreaLeft, world.AreaRight);
        float mapRight = Mathf.Max(world.AreaLeft, world.AreaRight);
        float mapTop = Mathf.Min(world.AreaTop, world.AreaBottom);
        float mapBottom = Mathf.Max(world.AreaTop, world.AreaBottom);

        var buckets = new Dictionary<int, (float Area, List<int> Nodes)>();
        var seen = new HashSet<int>();
        foreach (var idx in roots)
        {
            if (idx < 0 || idx >= _gamez.Nodes.Count || !seen.Add(idx))
                continue;
            var n = _gamez.Nodes[idx];
            if (SkipWorldNode(n) || !FlatTile(n, out float y, out float x0, out float z0,
                    out float x1, out float z1))
                continue;
            float w = Mathf.Min(x1, mapRight) - Mathf.Max(x0, mapLeft);
            float h = Mathf.Min(z1, mapBottom) - Mathf.Max(z0, mapTop);
            if (w <= 0f || h <= 0f)
                continue; // wholly outside the map rect
            int key = Mathf.RoundToInt(y);
            var slot = buckets.TryGetValue(key, out var got) ? got : (0f, new List<int>());
            slot.Item1 += w * h;
            slot.Item2.Add(idx);
            buckets[key] = slot;
        }

        float best = DeckCoverageFraction * mapW * mapH;
        foreach (var (alt, slot) in buckets)
            if (slot.Area > best)
            {
                best = slot.Area;
                _deckNodes.Clear();
                foreach (var idx in slot.Nodes)
                    _deckNodes.Add(idx);
                _deckAltitude = alt;
                _deckCoverage = slot.Area / (mapW * mapH);
            }
    }

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

    private void Add(Node3D root, Node3D deck, int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _gamez.Nodes.Count)
            return;
        var node = _gamez.Nodes[nodeIndex];
        if (!node.Active)
            return; // the build script's own NodeSetActive off — never built, like the original
        bool isDeck = _deckNodes.Contains(nodeIndex);
        // forceLit: PLAN-overcast-match C22. The deck tiles author `lighting: false` like the
        // dome and the cloudsprite field, but C21 traced the original's dark mottled underside
        // to the mission's own SUNLIGHT dimming, which is authored ON for the deck alone among
        // those three `lighting: false` populations — `SunIncidence` was calibrated on this
        // exact surface (see `Flight/Weather.cs`). Deck-local, beside the existing
        // forceDoubleSided override; never a change to the `lighting` gate or to
        // `csky_world_light` itself.
        var built = _scene.BuildSubtree(node, SkipWorldNode, NoCollisionNode,
            forceDoubleSided: isDeck, forceLit: isDeck);
        if (built != null)
        {
            if (IsParkedAtOrigin(node, built))
            {
                _parkedAtOrigin.Add((node, built));
            }
            (isDeck ? deck : root).AddChild(built);
        }
    }

    private string ResolveHorizonZone(GameZNode horizon, string zone)
    {
        var zones = HorizonZones();
        if (zones.Count == 0)
            return zone;
        foreach (var z in zones)
            if (z.Name.Equals(zone, StringComparison.OrdinalIgnoreCase))
                return zone;
        if (!_loggedHorizonZoneFallback)
        {
            _loggedHorizonZoneFallback = true;
            var names = new List<string>();
            foreach (var z in zones)
                names.Add(z.Name);
            GD.Print($"horizon: no '{zone}' subtree (has {string.Join("/", names)}) — "
                     + $"building '{zones[0].Name}'");
        }
        return zones[0].Name;
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
}

using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The <c>--stage=empty</c> test stage: a flat collidable ground plane under a grid drawn in
/// code, standing in for a chapter world so a flight or ballistics run boots quickly with
/// nothing else in the frame.
/// </summary>
public sealed class EmptyStage
{
    /// <summary>Half the ground plane's side, in metres, the stage is a 20 km square centred on
    /// the world origin, comfortably past the 40 km camera far plane's useful range and past any
    /// ballistic range in the weapons table (the longest is ~1 km).</summary>
    public const float HalfExtent = 10000f;

    /// <summary>Metres per grid square. One texture repeat covers one square, so this is also the
    /// scale a screenshot can be measured against.</summary>
    public const float CellMetres = 100f;

    /// <summary>Where the plane starts when nothing placed it: over the origin, high enough that a
    /// hands-off run has room to fly before the ground arrives.</summary>
    public const float SpawnAltitude = 300f;

    /// <summary>The name a <c>--ai=&lt;plane&gt;:&lt;net&gt;</c> or <c>--zep=…:net=</c> token spells to put
    /// a vehicle on <see cref="PatrolNet"/>, the way either flag spells a chapter <c>neindex</c>
    /// name. Reserved: no shipped chapter indexes it, so resolving the built-in first hides no
    /// authored net.</summary>
    public const string PatrolNetName = "grid";

    /// <summary>The patrol ring's radius, metres. The squadron ring's radius too, so a plane
    /// patrolling the net crosses the sides an <c>--ai=</c> sortie starts on.</summary>
    public const float PatrolRingRadius = 1000f;

    /// <summary>Nodes on the ring. Eight puts a 45 degree turn and a 765 m leg between neighbours,
    /// which every airframe in the table turns inside.</summary>
    public const int PatrolRingNodes = 8;

    /// <summary>The Dogfight spawn ring's radius, metres. Two seats on opposite entries open
    /// 1200 m apart and closing, so a match's first pass comes within seconds.</summary>
    public const float SpawnRingRadius = 600f;

    /// <summary>Entries on the Dogfight spawn ring: one whole 16-entry <c>net.zrd</c> block
    /// (docs/formats/net-spawns.md), so every seat a match admits opens on its own entry. A power of
    /// two, which the spread order in <see cref="SpawnRing"/> needs.</summary>
    public const int SpawnRingEntries = 16;

    /// <summary>How far right of the origin every ring entry's nose is aimed, degrees. Opposite
    /// seats then pass about 208 m abeam rather than ramming nose to nose hands-off.</summary>
    public const float SpawnRingSkewDeg = 10f;

    /// <summary>Team blocks after the free-for-all block in <see cref="SpawnTable"/>: one per lobby
    /// team, four, the count of a map that supports four-team play (docs/formats/net-spawns.md).
    /// </summary>
    public const int TeamBlockCount = 4;

    /// <summary>How far each team's base stands from the origin, metres. Well outside
    /// <see cref="SpawnRingRadius"/>, so no team opening shares ground with a free-for-all entry.
    /// </summary>
    public const float TeamBaseRadius = 1500f;

    /// <summary>Metres between neighbouring positions of a team block's square, and between the
    /// rungs of its altitude ladder.</summary>
    public const float TeamBlockSpacing = 100f;

    /// <summary>The bases the match arena stands, team 1's and team 2's: each a
    /// <c>cs_flag_n</c> flag with its carried twin and a <c>rearm_node_n</c>.</summary>
    public const int ArenaBases = 2;

    /// <summary>How far a base's rearm node stands from its flag toward the origin, and how high,
    /// metres. Clear of the flag's reach, so a pilot at one is not at the other.</summary>
    public static readonly Vector3 RearmOffset = new(0f, 50f, 300f);

    /// <summary>The freecam eye when nothing placed it: back and above the origin, looking at it.</summary>
    public static readonly Vector3 CameraPos = new(0f, 120f, 300f);

    // Not a file id: the ring comes from no ne0NNNNN, and a negative one cannot collide with a
    // chapter's, so a log line naming net#-1 says which net it is.
    private const int PatrolNetId = -1;

    private const int TextureSize = 256;      // one grid square
    private const float GroundThickness = 400f;

    private EmptyStage() { }

    /// <summary>The ring's own activation, attack and return radii: 2500 m, 1500 m and 700 m, none
    /// of them a value the mode machine would otherwise hold (2000 / the airframe's 2000 / 1200), so
    /// a plane assigned this net is visibly running on the NET's volumes. The 700 m return is the
    /// radius 46 of the 52 volume-carrying shipped nets author. The altitude bands stay zero, as
    /// they are on every shipped net with a consumer.</summary>
    public static AiVolumeSet PatrolVolumes { get; } = new(
        new AiVolume(2500f, 0f, 0f), new AiVolume(1500f, 0f, 0f), new AiVolume(700f, 0f, 0f));

    /// <summary>The stage's own patrol net: a closed ring of <see cref="PatrolRingNodes"/> nodes
    /// about the grid origin at <see cref="SpawnAltitude"/>, carrying <see cref="PatrolVolumes"/>.
    /// It is what makes the netted half of the AI fork reachable here, since this stage has no
    /// chapter and therefore no <c>neindex</c> to name. Unanchored, so the ring never rides
    /// anything and a run repeats. ⚠ Built in code, like the grid texture: the stage must boot with
    /// no chapter assets present.</summary>
    public static AiNet PatrolNet { get; } = BuildPatrolNet();

    /// <summary>The stage's own Dogfight spawn table, one free-for-all block of <c>net.zrd</c>
    /// records (position, heading in degrees). Its <see cref="SpawnRingEntries"/> entries sit on a
    /// <see cref="SpawnRingRadius"/> ring at <see cref="SpawnAltitude"/>, each aimed
    /// <see cref="SpawnRingSkewDeg"/> right of the origin. Every leading run is spread: entries 0 and
    /// 1 are opposite, 0 to 3 a compass cross, 0 to 7 its eight points. ⚠ Built in code: a match here
    /// must boot with no multiplayer map present.</summary>
    public static IReadOnlyList<(Vector3 Position, float HeadingDeg)> SpawnRing { get; } = BuildSpawnRing();

    /// <summary>The stage's whole Dogfight table, the shape a team match walks:
    /// <see cref="SpawnRing"/> as block 0, then <see cref="TeamBlockCount"/> team blocks. Block n is
    /// a staging stack at team n's base (<see cref="TeamBase"/>), facing the origin. Its square of
    /// four positions repeats on four rungs, all <see cref="TeamBlockSpacing"/> apart.
    /// ⚠ Built in code: a match here must boot with no multiplayer map present.</summary>
    public static IReadOnlyList<(Vector3 Position, float HeadingDeg)> SpawnTable { get; } = BuildSpawnTable();

    /// <summary>The match arena's named nodes, (name, position). Each of the
    /// <see cref="ArenaBases"/> bases stands its <c>cs_flag_n</c> on the ground with the carried
    /// <c>cs_flg_lightn</c> beside it. Its <c>rearm_node_n</c> stands at <see cref="RearmOffset"/>.
    /// The names are the ones the flag and rearm runtimes look a mission world up by.</summary>
    public static IReadOnlyList<(string Name, Vector3 Position)> ArenaNodes { get; } = BuildArenaNodes();

    /// <summary>The stage subtree, the caller adds it to the session root exactly as it adds a
    /// built world.</summary>
    public Node3D Root { get; private set; } = null!;

    public int MeshInstanceCount { get; private set; }

    public int ColliderCount { get; private set; }

    /// <summary>The match arena's subtree, holding <see cref="ArenaNodes"/>, or null when the
    /// stage was built without one.</summary>
    public Node3D? Arena { get; private set; }

    /// <param name="collision">Attach the ground collider (true in flight, so weapons and the
    /// airframe have something to hit; false for a plane-less look at the stage).</param>
    /// <param name="arena">Stand <see cref="ArenaNodes"/> on the stage, for a Dogfight.</param>
    public static EmptyStage Build(bool collision, bool arena = false)
    {
        var stage = new EmptyStage();
        var root = new Node3D { Name = "empty_stage" };
        stage.Root = root;

        var ground = new Node3D { Name = "ground" };
        // The same meta every built gamez node carries, so the node labels, the impact log and
        // anything else that reads a source name off a struck node reads "ground" here too.
        ground.SetMeta(AnimRuntime.NameMeta, "ground");
        root.AddChild(ground);

        var mesh = new PlaneMesh
        {
            Size = new Vector2(HalfExtent * 2f, HalfExtent * 2f),
            Material = GridMaterial(),
        };
        ground.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Name = "mesh",
            // A 20 km sheet under a shadow-casting sun would otherwise shadow-map itself.
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        stage.MeshInstanceCount = 1;

        if (collision)
        {
            var body = new StaticBody3D { Name = "col" };
            body.AddChild(new CollisionShape3D
            {
                // A BoxShape3D, not a WorldBoundaryShape3D or a trimesh: the weapon and airframe
                // raycasts want a definite thickness under the surface, not an infinite plane.
                Shape = new BoxShape3D { Size = new Vector3(HalfExtent * 2f, GroundThickness, HalfExtent * 2f) },
                // Sunk so the box's TOP face is the y=0 surface the quad draws.
                Position = new Vector3(0f, -GroundThickness * 0.5f, 0f),
            });
            ground.AddChild(body);
            stage.ColliderCount = 1;
        }

        Log.Info("world", $"stage empty: {HalfExtent * 2f / 1000f:0.#} km ground grid, cell={CellMetres:0} m, collision={(collision ? "on" : "off")}");
        if (arena)
        {
            stage.Arena = BuildArena();
            root.AddChild(stage.Arena);
            Log.Info("world", $"stage empty: match arena, {ArenaBases} bases with flags and rearm nodes, {TeamBlockCount} team blocks");
        }

        return stage;
    }

    /// <summary>Team <paramref name="team"/>'s base on the ground, <see cref="TeamBaseRadius"/> out.
    /// Team 1 stands due north of the origin, team 2 due south, team 3 east and team 4 west.</summary>
    public static Vector3 TeamBase(int team)
    {
        float angle = Mathf.DegToRad(TeamBearing(team));
        return new Vector3(Mathf.Sin(angle) * TeamBaseRadius, 0f, -Mathf.Cos(angle) * TeamBaseRadius);
    }

    /// <summary><see cref="PatrolNet"/> when <paramref name="idOrName"/> names it, else null, so a
    /// caller resolves the built-in before it falls back to a chapter's nets. Case-insensitive, the
    /// comparison <see cref="AiNets.ByName"/> makes.</summary>
    public static AiNet? ResolveNet(string idOrName) =>
        PatrolNetName.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ? PatrolNet : null;

    // Node 0 sits due north of the origin and the loop runs clockwise seen from above. The edge
    // list closes the ring, so the walk never reaches a dead end and never turns back: a route a
    // scripted run can watch for as long as it likes.
    private static AiNet BuildPatrolNet()
    {
        var nodes = new AiNetNode[PatrolRingNodes];
        var edges = new (int A, int B)[PatrolRingNodes];
        for (int i = 0; i < PatrolRingNodes; i++)
        {
            float angle = Mathf.Tau * i / PatrolRingNodes;
            nodes[i] = new AiNetNode(
                new Vector3(Mathf.Sin(angle) * PatrolRingRadius, SpawnAltitude,
                    -Mathf.Cos(angle) * PatrolRingRadius),
                Array.Empty<float>());
            edges[i] = (i, (i + 1) % PatrolRingNodes);
        }
        return new AiNet
        {
            Id = PatrolNetId,
            Name = PatrolNetName,
            Nodes = nodes,
            Edges = edges,
            Volumes = PatrolVolumes,
        };
    }

    // The ring is the patrol net's frame: slot 0 due north, the angle running clockwise seen from
    // above. Entry i takes the slot whose index is i's bits reversed, so each leading run halves
    // the gaps. A heading yaws the -Z nose left, so 180 minus the bearing faces the origin.
    private static (Vector3 Position, float HeadingDeg)[] BuildSpawnRing()
    {
        int bits = 0;
        while ((1 << bits) < SpawnRingEntries)
        {
            bits++;
        }
        var ring = new (Vector3 Position, float HeadingDeg)[SpawnRingEntries];
        for (int i = 0; i < SpawnRingEntries; i++)
        {
            int slot = 0;
            for (int b = 0; b < bits; b++)
            {
                if ((i & (1 << b)) != 0)
                {
                    slot |= 1 << (bits - 1 - b);
                }
            }
            float bearing = 360f * slot / SpawnRingEntries;
            float angle = Mathf.DegToRad(bearing);
            ring[i] = (new Vector3(Mathf.Sin(angle) * SpawnRingRadius, SpawnAltitude,
                -Mathf.Cos(angle) * SpawnRingRadius), Mathf.Wrap(180f - bearing - SpawnRingSkewDeg, -180f, 180f));
        }
        return ring;
    }

    // Opposing pairs first, so a two-team match faces across the origin.
    private static float TeamBearing(int team) => team switch { 1 => 0f, 2 => 180f, 3 => 90f, _ => 270f };

    // Bare nodes carrying the gamez name meta, which is what a lookup by node name reads. The
    // carried flag starts hidden, as the flag runtime keeps it while the flag is home.
    private static Node3D BuildArena()
    {
        var arena = new Node3D { Name = "arena" };
        foreach (var (name, position) in ArenaNodes)
        {
            var node = new Node3D { Name = name, Position = position };
            node.SetMeta(AnimRuntime.NameMeta, name);
            node.Visible = !name.StartsWith("cs_flg_light", StringComparison.Ordinal);
            arena.AddChild(node);
        }

        return arena;
    }

    private static (string Name, Vector3 Position)[] BuildArenaNodes()
    {
        var nodes = new List<(string, Vector3)>();
        for (int team = 1; team <= ArenaBases; team++)
        {
            var home = TeamBase(team);
            var inward = new Basis(Vector3.Up, Mathf.DegToRad(-TeamBearing(team))) * RearmOffset;
            string n = team.ToString(System.Globalization.CultureInfo.InvariantCulture);
            nodes.Add(("cs_flag_" + n, home));
            nodes.Add(("cs_flg_light" + n, home));
            nodes.Add(("rearm_node_" + n, home + inward));
        }

        return nodes.ToArray();
    }

    // Entry e of a team block sits at position e % 4 of the square on rung e / 4. A team's first
    // seats then open side by side on the lowest rung.
    private static (Vector3 Position, float HeadingDeg)[] BuildSpawnTable()
    {
        var table = new List<(Vector3 Position, float HeadingDeg)>(SpawnRing);
        for (int team = 1; team <= TeamBlockCount; team++)
        {
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(-TeamBearing(team)));
            float heading = Mathf.Wrap(180f - TeamBearing(team), -180f, 180f);
            for (int entry = 0; entry < SpawnRingEntries; entry++)
            {
                float across = ((entry % 2) - 0.5f) * TeamBlockSpacing;
                float along = (((entry / 2) % 2) - 0.5f) * TeamBlockSpacing;
                float rung = SpawnAltitude + ((entry / 4) * TeamBlockSpacing);
                table.Add((TeamBase(team) + (basis * new Vector3(across, 0f, along)) + (Vector3.Up * rung), heading));
            }
        }

        return table.ToArray();
    }

    private static StandardMaterial3D GridMaterial()
    {
        int repeats = Mathf.RoundToInt(HalfExtent * 2f / CellMetres);
        return new StandardMaterial3D
        {
            AlbedoTexture = GridTexture(),
            Uv1Scale = new Vector3(repeats, repeats, 1f),
            // Grazing angles across 200 repeats alias badly without mips + anisotropy.
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            Roughness = 1f,
            Metallic = 0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };
    }

    // One grid square: a dark field, a light square edge, and a quarter-cell minor rule.
    // ⚠ Draw it, never load it. This stage must boot with no chapter assets present.
    private static ImageTexture GridTexture()
    {
        var field = new Color(0.16f, 0.17f, 0.19f);
        var minor = new Color(0.24f, 0.26f, 0.29f);
        var major = new Color(0.44f, 0.47f, 0.53f);
        const int majorPx = 3;
        const int minorPx = 1;
        int quarter = TextureSize / 4;

        var img = Image.CreateEmpty(TextureSize, TextureSize, true, Image.Format.Rgba8);
        img.Fill(field);
        for (int i = 0; i < TextureSize; i++)
        {
            for (int j = 0; j < TextureSize; j++)
            {
                bool majorLine = i < majorPx || j < majorPx;
                bool minorLine = i % quarter < minorPx || j % quarter < minorPx;
                if (majorLine)
                {
                    img.SetPixel(i, j, major);
                }
                else if (minorLine)
                {
                    img.SetPixel(i, j, minor);
                }
            }
        }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }
}

using System;
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
    /// <summary>Half the ground plane's side, in metres — the stage is a 20 km square centred on
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

    /// <summary>The stage subtree — the caller adds it to the session root exactly as it adds a
    /// built world.</summary>
    public Node3D Root { get; private set; } = null!;

    public int MeshInstanceCount { get; private set; }

    public int ColliderCount { get; private set; }

    /// <param name="collision">Attach the ground collider (true in flight, so weapons and the
    /// airframe have something to hit; false for a plane-less look at the stage).</param>
    public static EmptyStage Build(bool collision)
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
        return stage;
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

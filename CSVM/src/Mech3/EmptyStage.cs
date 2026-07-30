using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The <c>--stage=empty</c> test stage: a flat, collidable ground plane under a grid drawn in
/// code, standing in for a chapter world. No gamez is loaded, no mission setup runs, no animation
/// program is bound — so a flight-model or ballistics run boots in a fraction of a chapter's time
/// and its frame contains nothing but the subject.
///
/// <para><b>The grid texture is generated here, pixel by pixel.</b> Nothing is committed and
/// nothing is read out of the player's install — the repo ships no assets, and a test stage is
/// exactly the place that rule is easiest to break by accident.</para>
///
/// <para>The ground is one <see cref="PlaneMesh"/> quad with a tiled grid albedo plus one
/// <see cref="StaticBody3D"/> box whose top face is <c>y = 0</c>. A box rather than a
/// <see cref="WorldBoundaryShape3D"/>, because the weapon and airframe raycasts want a surface
/// with a definite thickness under it, and rather than a trimesh because a 20 km quad's trimesh
/// has the same two triangles and none of the BVH.</para>
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

    /// <summary>The freecam eye when nothing placed it: back and above the origin, looking at it.</summary>
    public static readonly Vector3 CameraPos = new(0f, 120f, 300f);

    private const int TextureSize = 256;      // one grid square
    private const float GroundThickness = 400f;

    private EmptyStage() { }

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

    // One grid square: a dark field, a light square edge, and a quarter-cell minor rule. Drawn
    // rather than loaded — see the class remark on the asset rule.
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

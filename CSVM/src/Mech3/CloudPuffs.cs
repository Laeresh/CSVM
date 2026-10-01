using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Enhanced Graphics only: the rendered cloud puff pools that stand in for the authored
/// <c>cloud1</c>/<c>cloud2</c> masks. It also holds the material settings a cloud card shader
/// drawing them takes. Our own Blender renders, not game data, shipped under <c>data/cloud_puffs/</c> with
/// the script that makes them. Two pools: the translucent deck set for the <c>fvol</c> cards and a
/// fuller set for the placed cloud clusters. A card's puff, turn, mirror and size come from a hash of
/// its own position in <c>shaders/csky_cloud_puffs.gdshaderinc</c>, so no stream is drawn from.
/// ⚠ Do not read a pool on the faithful path; that path draws the authored masks alone.
/// </summary>
public static class CloudPuffs
{
    /// <summary>The shader include both cloud card shaders take for a pooled variant.</summary>
    public const string Include = "#include \"res://shaders/csky_cloud_puffs.gdshaderinc\"";

    /// <summary>The vertex() lines a pooled card appends after its facade pose. They take the puff,
    /// turn, mirror and size off its own position, the size scaling the quad in place. Four-space
    /// indented, no trailing newline.</summary>
    public const string PoseLines = """
            float puff_layer;
            vec3 puff_pose;
            float puff_scale = csky_puff_pose(MODEL_MATRIX[3].xyz, puff_layer, puff_pose);
            v_puff_layer = puff_layer;
            v_puff_pose = puff_pose;
            v_puff_card_uv = csky_puff_card_uv(VERTEX);
            MODELVIEW_MATRIX[0] *= puff_scale;
            MODELVIEW_MATRIX[1] *= puff_scale;
        """;

    /// <summary>The deck pool's layer count, <c>veil_1</c> to <c>veil_8</c>.</summary>
    public const int DeckCount = 8;

    /// <summary>The placed clusters' pool's layer count, <c>far_1</c> to <c>far_6</c>.</summary>
    public const int FarCount = 6;

    // TUNE: the largest in-plane turn a card's puff takes, in degrees either way. The puffs are lit
    // from their own top, so a larger turn tips the baked sun visibly off the sky's.
    private const float TurnDegrees = 20f;

    // TUNE: the size range a rendered puff is drawn at, as a factor on the card's authored scale.
    // Modest, so a field's coverage stays near the authored one.
    private const float SizeMin = 0.8f;
    private const float SizeMax = 1.25f;

    private static Texture2DArray? _deck;
    private static Texture2DArray? _far;
    private static bool _deckLoaded;
    private static bool _farLoaded;

    /// <summary>Gets the largest size factor a pooled card is drawn at, for its cull margin.</summary>
    public static float MaxSize => SizeMax;

    /// <summary>The deck pool, one mipmapped layer per puff, or null when a file is missing.
    /// Decoded once for the process.</summary>
    public static Texture2DArray? Deck()
    {
        if (!_deckLoaded)
        {
            _deckLoaded = true;
            _deck = Load("veil", DeckCount);
        }
        return _deck;
    }

    /// <summary>The placed cloud clusters' pool, one mipmapped layer per puff, or null when a
    /// file is missing. Decoded once for the process.</summary>
    public static Texture2DArray? Far()
    {
        if (!_farLoaded)
        {
            _farLoaded = true;
            _far = Load("far", FarCount);
        }
        return _far;
    }

    /// <summary>The authored mask's own colour, the alpha-weighted mean of its texels, and its
    /// peak opacity, which a puff takes as its tint. Null for an empty or missing mask.</summary>
    public static Color? MaskTint(Image? mask)
    {
        if (mask == null || mask.IsEmpty())
        {
            return null;
        }
        float r = 0f, g = 0f, b = 0f, weight = 0f, peak = 0f;
        for (int y = 0; y < mask.GetHeight(); y++)
        {
            for (int x = 0; x < mask.GetWidth(); x++)
            {
                var c = mask.GetPixel(x, y);
                r += c.R * c.A;
                g += c.G * c.A;
                b += c.B * c.A;
                weight += c.A;
                peak = Mathf.Max(peak, c.A);
            }
        }
        return weight > 0f ? new Color(r / weight, g / weight, b / weight, peak) : null;
    }

    /// <summary>Sets a pooled card material's pool, tint and pose range.</summary>
    public static void Apply(ShaderMaterial mat, Texture2DArray pool, Color tint)
    {
        mat.SetShaderParameter("puff_tex", pool);
        mat.SetShaderParameter("puff_layers", (float)pool.GetLayers());
        mat.SetShaderParameter("puff_tint", tint);
        mat.SetShaderParameter("puff_turn_deg", TurnDegrees);
        mat.SetShaderParameter("puff_size", new Vector2(SizeMin, SizeMax));
    }

    private static Texture2DArray? Load(string stem, int count)
    {
        var images = new Godot.Collections.Array<Image>();
        for (int i = 1; i <= count; i++)
        {
            string path = $"res://data/cloud_puffs/{stem}_{i}.png.bin";
            var image = new Image();
            if (!FileAccess.FileExists(path) || image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path)) != Error.Ok)
            {
                Log.Warn("world", $"cloud puffs: unreadable path={path}, the authored masks stay");
                return null;
            }
            image.GenerateMipmaps();
            images.Add(image);
        }
        var pool = new Texture2DArray();
        if (pool.CreateFromImages(images) != Error.Ok)
        {
            Log.Warn("world", $"cloud puffs: the {stem} set differs in size or format, the authored masks stay");
            return null;
        }
        return pool;
    }
}

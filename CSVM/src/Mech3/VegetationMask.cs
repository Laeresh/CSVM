using System;
using System.IO;
using CSVM.Utils;

namespace CSVM.Mech3;

/// <summary>How a brush stroke moves the vegetation mask under it.</summary>
public enum VegetationBrush
{
    /// <summary>Toward the densest value, by the stroke's strength.</summary>
    Thicken,

    /// <summary>Toward bare, by the stroke's strength.</summary>
    Thin,

    /// <summary>Bare inside the radius.</summary>
    Clear,

    /// <summary>Back to neutral inside the radius, which undoes paint.</summary>
    Neutral,
}

/// <summary>
/// A chapter's vegetation mask: a square of bytes over the world node's area that gates and scales
/// the density of listed vegetation under Rich. It never places a stamp. A byte over 128 is a
/// density factor, so 128 is neutral, 0 bare and 255 nearly twice the neutral density.
/// The file is the raw bytes, row 0 along the area's lowest Z and column 0 along its lowest X.
/// It sits beside the sidecar under <c>CSVM/data</c>, and the export's <c>data/*.bin</c> filter ships it.
/// The density rule a Rich bake applies per listed stamp is <see cref="Keeps"/>, decoded in Mech3.md.
/// </summary>
public sealed class VegetationMask
{
    /// <summary>The side of a new mask in texels, about 12 m a texel over a 12 km chapter.</summary>
    public const int NewSide = 1024;

    /// <summary>The byte that leaves the faithful density alone.</summary>
    public const byte NeutralByte = 128;

    /// <summary>The share of the remake-added stamps a neutral mask keeps, so a thickened mask can show more
    /// of them without placing any. An original stamp is kept whole at neutral or above.</summary>
    public const float AddedNeutralShare = 0.5f;

    private readonly byte[] _texels;

    private VegetationMask(int side, float x0, float z0, float x1, float z1, byte[] texels)
    {
        Side = side;
        MinX = Math.Min(x0, x1);
        MaxX = Math.Max(x0, x1);
        MinZ = Math.Min(z0, z1);
        MaxZ = Math.Max(z0, z1);
        _texels = texels;
    }

    /// <summary>The texels along each side.</summary>
    public int Side { get; }

    /// <summary>The world area the mask covers, in Godot X and Z.</summary>
    public float MinX { get; }

    public float MaxX { get; }

    public float MinZ { get; }

    public float MaxZ { get; }

    /// <summary>The path a chapter's mask takes relative to <c>CSVM/data</c>, the sidecar's <c>mask</c> value.</summary>
    public static string PathFor(string chapter) => $"vegetation/{chapter}.bin";

    /// <summary>A neutral mask over the world area from the world node's <c>area</c> corners.</summary>
    public static VegetationMask Neutral(float left, float top, float right, float bottom, int side = NewSide)
    {
        var texels = new byte[side * side];
        Array.Fill(texels, NeutralByte);
        return new VegetationMask(side, left, top, right, bottom, texels);
    }

    /// <summary>A mask from a file's bytes over the world area. Throws <see cref="InvalidDataException"/>
    /// when the length is not a square of at least two texels a side.</summary>
    public static VegetationMask FromBytes(byte[] bytes, float left, float top, float right, float bottom)
    {
        int side = (int)Math.Round(Math.Sqrt(bytes.Length));
        if (side < 2 || side * side != bytes.Length)
            throw new InvalidDataException($"a vegetation mask is a square of bytes, and {bytes.Length} bytes is not one");
        return new VegetationMask(side, left, top, right, bottom, (byte[])bytes.Clone());
    }

    /// <summary>The mask at <paramref name="dataPath"/> under <c>res://data</c>. An empty path, a missing file
    /// or a file that is not a mask gives a neutral one, the last two with a <c>world</c> warning.</summary>
    public static VegetationMask Load(string dataPath, float left, float top, float right, float bottom)
    {
        if (dataPath.Length == 0)
            return Neutral(left, top, right, bottom);
        string res = $"res://data/{dataPath}";
        if (!Godot.FileAccess.FileExists(res))
        {
            Log.Warn("world", $"vegetation mask {res} not found, the chapter's mask is neutral");
            return Neutral(left, top, right, bottom);
        }
        try
        {
            return FromBytes(Godot.FileAccess.GetFileAsBytes(res), left, top, right, bottom);
        }
        catch (InvalidDataException ex)
        {
            Log.Warn("world", $"vegetation mask {res}: {ex.Message}, the chapter's mask is neutral");
            return Neutral(left, top, right, bottom);
        }
    }

    /// <summary>Whether a listed stamp survives the mask. <paramref name="density"/> is <see cref="At"/>
    /// at the stamp and <paramref name="draw"/> its uniform draw in [0, 1) from the Rich stream.
    /// A stamp the remake adds passes <paramref name="added"/> true. Original ignores the mask and never asks.
    /// ⚠ Keep the draw a double: a draw just under 1 rounds to 1f and would drop a stamp at neutral.</summary>
    public static bool Keeps(float density, double draw, bool added) =>
        draw < density * (added ? AddedNeutralShare : 1f);

    /// <summary>A copy of the texels, the file's bytes.</summary>
    public byte[] ToBytes() => (byte[])_texels.Clone();

    /// <summary>An independent copy, for a lab's revert.</summary>
    public VegetationMask Clone() => new(Side, MinX, MinZ, MaxX, MaxZ, ToBytes());

    /// <summary>Whether every texel is neutral, so the mask changes nothing.</summary>
    public bool IsNeutral() => Array.TrueForAll(_texels, b => b == NeutralByte);

    /// <summary>The density factor at a world position, bilinear between texel centres: 1 is neutral,
    /// 0 bare. Outside the area it is neutral.</summary>
    public float At(float x, float z)
    {
        if (x < MinX || x > MaxX || z < MinZ || z > MaxZ)
            return 1f;
        float u = ((x - MinX) / (MaxX - MinX) * Side) - 0.5f;
        float v = ((z - MinZ) / (MaxZ - MinZ) * Side) - 0.5f;
        int i0 = Math.Clamp((int)MathF.Floor(u), 0, Side - 1);
        int j0 = Math.Clamp((int)MathF.Floor(v), 0, Side - 1);
        int i1 = Math.Min(i0 + 1, Side - 1);
        int j1 = Math.Min(j0 + 1, Side - 1);
        float fu = Math.Clamp(u - i0, 0f, 1f);
        float fv = Math.Clamp(v - j0, 0f, 1f);
        float top = Lerp(_texels[(j0 * Side) + i0], _texels[(j0 * Side) + i1], fu);
        float bottom = Lerp(_texels[(j1 * Side) + i0], _texels[(j1 * Side) + i1], fu);
        return Lerp(top, bottom, fv) / NeutralByte;
    }

    /// <summary>The byte of the texel holding a world position, unsampled: the ground cover's gate, which wants
    /// the strip clearance's edge where the texels put it. Neutral outside the area.</summary>
    public byte TexelAt(float x, float z)
    {
        if (x < MinX || x > MaxX || z < MinZ || z > MaxZ)
            return NeutralByte;
        int i = Math.Clamp((int)((x - MinX) / (MaxX - MinX) * Side), 0, Side - 1);
        int j = Math.Clamp((int)((z - MinZ) / (MaxZ - MinZ) * Side), 0, Side - 1);
        return _texels[(j * Side) + i];
    }

    /// <summary>One brush dab at a world position. Thicken and Thin move each texel by
    /// <paramref name="strength"/> (0 to 1) times a falloff from 1 at the centre to 0 at the radius.
    /// Clear and Neutral set every texel inside the radius. Returns whether any texel changed.</summary>
    public bool Stroke(float x, float z, float radius, float strength, VegetationBrush brush)
    {
        if (radius <= 0f)
            return false;
        float texelX = (MaxX - MinX) / Side;
        float texelZ = (MaxZ - MinZ) / Side;
        int iMin = Math.Max(0, (int)MathF.Floor((x - radius - MinX) / texelX));
        int iMax = Math.Min(Side - 1, (int)MathF.Floor((x + radius - MinX) / texelX));
        int jMin = Math.Max(0, (int)MathF.Floor((z - radius - MinZ) / texelZ));
        int jMax = Math.Min(Side - 1, (int)MathF.Floor((z + radius - MinZ) / texelZ));
        float s = Math.Clamp(strength, 0f, 1f);
        bool changed = false;
        for (int j = jMin; j <= jMax; j++)
        {
            for (int i = iMin; i <= iMax; i++)
            {
                float dx = MinX + ((i + 0.5f) * texelX) - x;
                float dz = MinZ + ((j + 0.5f) * texelZ) - z;
                float d2 = ((dx * dx) + (dz * dz)) / (radius * radius);
                if (d2 >= 1f)
                    continue;
                ref byte texel = ref _texels[(j * Side) + i];
                float falloff = (1f - d2) * (1f - d2);
                // Ceiling up and floor down, so a faint dab still moves a texel by one step.
                int next = brush switch
                {
                    VegetationBrush.Thicken => texel + (int)MathF.Ceiling((255 - texel) * s * falloff),
                    VegetationBrush.Thin => texel - (int)MathF.Ceiling(texel * s * falloff),
                    VegetationBrush.Clear => 0,
                    _ => NeutralByte,
                };
                byte held = (byte)Math.Clamp(next, 0, 255);
                changed |= held != texel;
                texel = held;
            }
        }

        return changed;
    }

    /// <summary>Writes the texels to <paramref name="diskPath"/> through <see cref="AtomicFile"/>, creating
    /// its folder.</summary>
    public void Save(string diskPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(diskPath))!);
        AtomicFile.WriteAllBytes(diskPath, _texels);
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}

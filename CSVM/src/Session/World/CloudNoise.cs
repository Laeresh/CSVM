using System;
using System.Threading.Tasks;
using Godot;

namespace CSVM.Session.World;

/// <summary>
/// The volumetric clouds' tiling 3D noise, baked once per process. The 128³ shape texture holds the
/// Perlin-Worley base in red and Worley sums in green to alpha. The 32³ detail texture holds Worley
/// sums. Worley cells give the billows their round, cauliflower edges.
/// </summary>
internal static class CloudNoise
{
    private static ImageTexture3D? _shape;
    private static ImageTexture3D? _detail;
    private static ImageTexture3D? _low;
    private static ImageTexture? _tops;

    /// <summary>Gets the 128³ shape texture.</summary>
    public static ImageTexture3D Shape => _shape ??= Build(128, ShapeTexel);

    /// <summary>Gets the 64³ low texture: two Perlin warps and a Perlin height field.</summary>
    public static ImageTexture3D Low => _low ??= Build(64, LowTexel);

    /// <summary>Gets the 512² tops texture: a Perlin height field over four cells, fine enough that
    /// its linear filter leaves no terraces on the crowns.</summary>
    public static ImageTexture Tops => _tops ??= BuildTops(512);

    /// <summary>Gets the 32³ detail texture.</summary>
    public static ImageTexture3D Detail => _detail ??= Build(32, DetailTexel);

    private static ImageTexture3D Build(int n, Func<float, float, float, (float R, float G, float B, float A)> texel)
    {
        var started = DateTime.UtcNow;
        var data = new byte[n * n * n * 4];
        Parallel.For(0, n, z =>
        {
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var (r, g, b, a) = texel((x + 0.5f) / n, (y + 0.5f) / n, (z + 0.5f) / n);
                    int i = (((z * n) + y) * n + x) * 4;
                    data[i] = ToByte(r);
                    data[i + 1] = ToByte(g);
                    data[i + 2] = ToByte(b);
                    data[i + 3] = ToByte(a);
                }
            }
        });
        var slices = new Godot.Collections.Array<Image>();
        int slice = n * n * 4;
        for (int z = 0; z < n; z++)
            slices.Add(Image.CreateFromData(n, n, false, Image.Format.Rgba8, data.AsSpan(z * slice, slice).ToArray()));
        var texture = new ImageTexture3D();
        texture.Create(Image.Format.Rgba8, n, n, n, false, slices);
        CSVM.Utils.Log.Info("world", $"volumetric clouds: baked {n}^3 noise in {(DateTime.UtcNow - started).TotalMilliseconds:0} ms");
        return texture;
    }

    private static ImageTexture BuildTops(int n)
    {
        var data = new byte[n * n * 2];
        Parallel.For(0, n, y =>
        {
            for (int x = 0; x < n; x++)
            {
                // Sixteen bits, since a crown's height reads straight off it.
                float v = Unit01(PerlinFbm((x + 0.5f) / n, (y + 0.5f) / n, 0.5f, 4, 1, 404u));
                int q = Math.Clamp((int)MathF.Round(v * 65535f), 0, 65535);
                int i = ((y * n) + x) * 2;
                data[i] = (byte)(q >> 8);
                data[i + 1] = (byte)(q & 255);
            }
        });
        return ImageTexture.CreateFromImage(Image.CreateFromData(n, n, false, Image.Format.Rg8, data));
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    private static (float, float, float, float) ShapeTexel(float x, float y, float z)
    {
        float perlin = Math.Clamp((PerlinFbm(x, y, z, 4, 5, 101u) * 0.5f) + 0.5f, 0f, 1f);
        float worley = WorleyFbm(x, y, z, 4);
        // Perlin's broad mass dilated by the Worley cells, so its edges bulge into billows.
        float perlinWorley = worley + (perlin * (1f - worley));
        return (perlinWorley, WorleyFbm(x, y, z, 8), WorleyFbm(x, y, z, 16), WorleyFbm(x, y, z, 32));
    }

    private static (float, float, float, float) LowTexel(float x, float y, float z) =>
        (Unit01(PerlinFbm(x, y, z, 2, 1, 202u)), Unit01(PerlinFbm(x, y, z, 2, 1, 303u)), Unit01(PerlinFbm(x, y, z, 4, 3, 404u)), 1f);

    private static float Unit01(float v) => Math.Clamp((v * 0.5f) + 0.5f, 0f, 1f);

    private static (float, float, float, float) DetailTexel(float x, float y, float z) =>
        (WorleyFbm(x, y, z, 2), WorleyFbm(x, y, z, 4), WorleyFbm(x, y, z, 8), 1f);

    private static float WorleyFbm(float x, float y, float z, int freq) =>
        (Worley(x, y, z, freq, 11u) * 0.625f) + (Worley(x, y, z, freq * 2, 23u) * 0.25f) + (Worley(x, y, z, freq * 4, 37u) * 0.125f);

    // Inverted cellular noise over `freq` cells per tile, wrapping: 1 at a feature point, 0 a cell away.
    private static float Worley(float x, float y, float z, int freq, uint seed)
    {
        float px = x * freq, py = y * freq, pz = z * freq;
        int cx = (int)MathF.Floor(px), cy = (int)MathF.Floor(py), cz = (int)MathF.Floor(pz);
        float best = 9f;
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ix = cx + dx, iy = cy + dy, iz = cz + dz;
                    uint h = Hash(Mod(ix, freq), Mod(iy, freq), Mod(iz, freq), seed);
                    float fx = ix + Unit(h) - px;
                    float fy = iy + Unit(Mix(h, 1u)) - py;
                    float fz = iz + Unit(Mix(h, 2u)) - pz;
                    best = MathF.Min(best, (fx * fx) + (fy * fy) + (fz * fz));
                }
            }
        }
        return 1f - Math.Clamp(MathF.Sqrt(best), 0f, 1f);
    }

    private static float PerlinFbm(float x, float y, float z, int freq, int octaves, uint seed)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Perlin(x, y, z, freq << o, seed) * amp;
            norm += amp;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    // Gradient noise over `freq` cells per tile, wrapping, in about [-1, 1].
    private static float Perlin(float x, float y, float z, int freq, uint seed)
    {
        float px = x * freq, py = y * freq, pz = z * freq;
        int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py), iz = (int)MathF.Floor(pz);
        float fx = px - ix, fy = py - iy, fz = pz - iz;
        float ux = Fade(fx), uy = Fade(fy), uz = Fade(fz);
        float Corner(int dx, int dy, int dz)
        {
            uint h = Hash(Mod(ix + dx, freq), Mod(iy + dy, freq), Mod(iz + dz, freq), seed);
            float gx = (Unit(h) * 2f) - 1f, gy = (Unit(Mix(h, 1u)) * 2f) - 1f, gz = (Unit(Mix(h, 2u)) * 2f) - 1f;
            float len = MathF.Max(MathF.Sqrt((gx * gx) + (gy * gy) + (gz * gz)), 1e-4f);
            return ((gx * (fx - dx)) + (gy * (fy - dy)) + (gz * (fz - dz))) / len;
        }
        float x00 = Lerp(Corner(0, 0, 0), Corner(1, 0, 0), ux);
        float x10 = Lerp(Corner(0, 1, 0), Corner(1, 1, 0), ux);
        float x01 = Lerp(Corner(0, 0, 1), Corner(1, 0, 1), ux);
        float x11 = Lerp(Corner(0, 1, 1), Corner(1, 1, 1), ux);
        return Lerp(Lerp(x00, x10, uy), Lerp(x01, x11, uy), uz) * 1.4f;
    }

    private static float Fade(float t) => t * t * t * ((t * ((t * 6f) - 15f)) + 10f);

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    private static uint Hash(int x, int y, int z, uint seed) =>
        Mix(((uint)x * 2376512323u) ^ ((uint)y * 3625334849u) ^ ((uint)z * 1911520717u) ^ seed, 0u);

    private static uint Mix(uint h, uint salt)
    {
        h ^= salt * 2654435769u;
        h ^= h >> 16;
        h *= 2146121005u;
        h ^= h >> 15;
        h *= 2221713035u;
        h ^= h >> 16;
        return h;
    }

    private static float Unit(uint h) => (h >> 8) * (1f / 16777216f);
}

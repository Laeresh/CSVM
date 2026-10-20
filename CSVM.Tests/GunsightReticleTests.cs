using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The stereo interior's collimated gunsight reticle (<see cref="GunsightReticle"/>). Each player
/// airframe carries one sight glass polygon, and the authored eye's view of it maps its UVs. Its
/// texture splits into the glass, kept at its depth, and the reticle, drawn at infinity.
/// </summary>
public class GunsightReticleTests
{
    private static readonly Lazy<GameZ> Planes = new(() =>
        GameZ.Load(SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "planes.zip"))));

    /// <summary>The eleven player airframes, all of which carry the one glass.</summary>
    public static TheoryData<string> Airframes => new()
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro", "player_avenger",
        "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel", "player_peacemaker",
    };

    [ExtractedDataTheory]
    [MemberData(nameof(Airframes))]
    public void EachInteriorCarriesOneGlassWhoseUvsTheLookReproduces(string plane)
    {
        var gamez = Planes.Value;
        var root = gamez.FindByName(plane) ?? throw new InvalidOperationException($"{plane} not in planes.zbd");
        GameZNode? interior = null;
        foreach (int child in root.Children)
        {
            if (gamez.Nodes[child].Name.Equals("cockpit1", StringComparison.OrdinalIgnoreCase))
            {
                interior = gamez.Nodes[child];
            }
        }

        var glasses = GunsightReticle.Find(gamez, interior!);
        var glass = Assert.Single(glasses);
        var look = GunsightReticle.Look.For(glass.Corners, glass.Uvs);
        Assert.NotNull(look);
        // Within a tenth of a 128-texel step: the Hoplite's strip authors its middle pair 0.0006 off.
        for (int k = 0; k < glass.Corners.Count; k++)
        {
            Assert.True(look!.Value.At(glass.Corners[k]).DistanceTo(glass.Uvs[k]) < 1e-3f, $"{plane} corner {k} off its UV");
            Assert.True(Mathf.Abs(look.Value.N.Dot(glass.Corners[k]) - look.Value.H) < 1e-3f, $"{plane} corner {k} off the plane");
            Assert.True(glass.Corners[k].Z < 0f, $"{plane} corner {k} is not ahead of the eye");
        }
    }

    [ExtractedDataFact]
    public void TheShippedGlassTextureSplitsIntoTheReticleAlone()
    {
        var png = ReadTexture("cockpit6.png");
        var image = PngImage.Decode(png) ?? throw new InvalidOperationException("cockpit6.png did not decode");
        int count = GunsightReticle.Split(image.Rgba, image.Width, image.Height, out var glass, out var reticle, out byte tint);
        var mask = GunsightReticle.ReticleMask(image.Rgba, image.Width, image.Height, out _);

        Assert.Equal(38, tint);
        Assert.Equal(902, count);
        int w = image.Width;
        Assert.True(mask[(62 * w) + 65], "the centre cross is reticle");
        Assert.False(mask[(125 * w) + 64], "the sight body stays on the glass");
        Assert.False(mask[(2 * w) + 2], "a frame corner stays on the glass");
        for (int i = 0; i < mask.Length; i++)
        {
            Assert.Equal(mask[i] ? tint : image.Rgba[(4 * i) + 3], glass![(4 * i) + 3]);
            Assert.Equal(mask[i] ? image.Rgba[(4 * i) + 3] : (byte)0, reticle![(4 * i) + 3]);
        }

        // The red index mark atop the ring, which the headset draws over the scene, and the scale's centre.
        var mark = GunsightReticle.IndexMark(image.Rgba, mask);
        int marked = 0;
        for (int i = 0; i < mark.Length; i++)
        {
            if (mark[i])
            {
                marked++;
                Assert.InRange(i % w, 62, 68);
                Assert.InRange(i / w, 1, 4);
            }
        }

        Assert.Equal(20, marked);
        Assert.Equal(new Vector2(65.5f / 128f, 62.5f / 128f), GunsightReticle.Centre(mask, w, image.Height));
    }

    [Fact]
    public void OnlyARedReticleTexelIsTheIndexMark()
    {
        // Red, a white line, red off the reticle, and a dull red just under the margin.
        byte[] rgba = { 247, 36, 0, 255, 255, 255, 255, 255, 247, 36, 0, 255, 120, 80, 80, 255 };
        var mark = GunsightReticle.IndexMark(rgba, new[] { true, true, false, true });
        Assert.Equal(new[] { true, false, false, false }, mark);
    }

    [Fact]
    public void TheCentreIsWhereTheCrossesLinesMeet()
    {
        const int size = 9;
        var mask = new bool[size * size];
        for (int k = 1; k < size - 1; k++)
        {
            mask[(3 * size) + k] = true; // the horizontal line on row 3
            mask[(k * size) + 5] = true; // the vertical line on column 5
        }

        mask[(7 * size) + 1] = true; // a stray texel
        Assert.Equal(new Vector2(5.5f / size, 3.5f / size), GunsightReticle.Centre(mask, size, size));
    }

    [Fact]
    public void TheReticleIsTheCentreGroupAndEveryGroupClearOfTheEdge()
    {
        const int size = 16;
        var rgba = new byte[size * size * 4];
        void Alpha(int x, int y, byte a) => rgba[(4 * ((y * size) + x)) + 3] = a;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool ring = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                Alpha(x, y, ring ? (byte)128 : (byte)40);
            }
        }

        for (int k = 4; k <= 11; k++)
        {
            Alpha(k, 7, 200); // the cross
            Alpha(7, k, 200);
        }

        Alpha(10, 3, 200); // a mark clear of the edge
        for (int x = 3; x <= 12; x++)
        {
            Alpha(x, 14, 255); // the sight body, on the second ring
        }

        Assert.Equal(15 + 1, GunsightReticle.Split(rgba, size, size, out _, out _, out byte tint));
        Assert.Equal(40, tint);
    }

    [Fact]
    public void AnEyeOffTheOriginSeesTheGlassShiftedWhileTheReticleKeepsItsDirection()
    {
        // A 2-unit pane 10 units ahead, u across it left to right, v down it.
        var corners = new List<Vector3> { new(-1f, -1f, -10f), new(1f, -1f, -10f), new(1f, 1f, -10f), new(-1f, 1f, -10f) };
        var uvs = new List<Vector2> { new(0f, 1f), new(1f, 1f), new(1f, 0f), new(0f, 0f) };
        var look = GunsightReticle.Look.For(corners, uvs)!.Value;
        var ahead = new Vector3(0f, 0f, -1f);

        Assert.True(look.Seen(Vector3.Zero, ahead).DistanceTo(new Vector2(0.5f, 0.5f)) < 1e-6f);
        // Half a unit to the right, straight ahead meets the glass three quarters across.
        Assert.True(look.Seen(new Vector3(0.5f, 0f, 0f), ahead).DistanceTo(new Vector2(0.75f, 0.5f)) < 1e-6f);
        // The overlay draws, along any ray, the texel the authored eye sees along it.
        var right = new Vector3(0.1f, 0.05f, -1f);
        Assert.True(look.Seen(Vector3.Zero, right).DistanceTo(new Vector2(0.5f + 0.5f, 0.5f - 0.25f)) < 1e-5f);
    }

    [Fact]
    public void OnlyTheSightGlassTexturesAreGlass()
    {
        Assert.True(GunsightReticle.IsGlass("cockpit6.tif"));
        Assert.True(GunsightReticle.IsGlass("CROSSHAIR.tif"));
        Assert.True(GunsightReticle.IsGlass("cockpit6"));
        Assert.False(GunsightReticle.IsGlass("cockpit60.tif"));
        Assert.False(GunsightReticle.IsGlass("cockpit7.tif"));
    }

    // The sight glass texture from C1's archive as the game reads it (SessionPaths.ChapterTextures).
    private static byte[] ReadTexture(string name)
    {
        string archive = SessionPaths.ChapterTextures(TestData.DataRoot!, "C1");
        if (Directory.Exists(archive))
        {
            return File.ReadAllBytes(Path.Combine(archive, name));
        }

        using var zip = ZipFile.OpenRead(archive);
        var entry = zip.GetEntry(name) ?? throw new FileNotFoundException(name, archive);
        using var stream = entry.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}

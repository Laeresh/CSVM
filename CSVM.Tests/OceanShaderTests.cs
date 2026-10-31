using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CSVM.Effects;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The wave ocean's generated shader text (<see cref="OceanShader"/>). At the default sea it is the
/// text the ocean's constants wrote, byte for byte, so no golden moves until a chapter's sea is
/// saved. Every rate a sea can move stays a whole number of cycles per csky_time wrap.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class OceanShaderTests
{
    private const double Wrap = 3600.0;

    /// <summary>The default sea's text with LF line endings, at each priority level and zone gate a mask
    /// can hand the generator. Each hash is the text the constants generated.</summary>
    [Theory]
    [InlineData(-11, false, "5693703E73EB20C00D91742F75CAE88AAD5BA161306776DCBFB886FAE00A8B2D")]
    [InlineData(-11, true, "BAF0F591C5C7A970A8B2B361B08B4BBF9965D8F2AA999FC19A0F9F96E82F15D0")]
    [InlineData(-2, false, "14B203103A930EAD3478A838F40FB026C7759234178E7044CABB7CB81890F225")]
    [InlineData(-2, true, "6E02F07749F28F5D531790770F7A3D4A5E11A5D6D94988814FC5449A2E471E10")]
    [InlineData(-1, false, "A122C9A20FC398C6DBA891D5622F6E9699E4A092A9E5AF4110FA98353284DAB7")]
    [InlineData(-1, true, "C55E0E983F518DC9DC736F8A4E46FECBAF5B2E290232B7750127D548C424B714")]
    [InlineData(0, false, "E53B09E4BD25AADA220E2DE677B578AF387CBACABA3AB3026CFE2D6DE07B1079")]
    [InlineData(0, true, "E6D53DCE8D55E82E3F85B052245FA924F898875A711265AE50DCB73454C21AF4")]
    [InlineData(1, false, "4E16D047720B9A4F33E2D23D3F15D8E82E8B051250B715761780EEC7A3F5F4AD")]
    [InlineData(1, true, "24F485CB434EB36355C69499067566C736A5C9EAEFEB289093DD596D18908B2E")]
    [InlineData(2, false, "3228EFD5AA512A088C17A70B35DCE6ECBAB8385AA7E4D4FFC7A8F960F2F84CAF")]
    [InlineData(2, true, "4192E851E82C9ED011A0BFA71626FB231152964E108A53B1D993EB59A33F7E54")]
    [InlineData(3, false, "1B7251F5AF559498BD571FCAC7DC6032891866690EF87D040F4CFD12570FD772")]
    [InlineData(3, true, "9301AB17D8656FD76AB8BAF02647DD3D8E88509B18A14AAE7F5DABDD68A7EFB5")]
    public void TheDefaultSeaWritesTheConstantsText(int level, bool zoned, string sha256)
    {
        string text = Lf(OceanShader.Code(SeaState.Default, level, zoned));
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }

    /// <summary>The same rule as a readable diff: the first line that differs from the fixture names itself.</summary>
    [Fact]
    public void TheDefaultSeaMatchesTheFixtureLineByLine()
    {
        string[] want = Lf(File.ReadAllText(TestData.Fixture("ocean", "default_l0_zoned.gdshader.txt"))).Split('\n');
        string[] got = Lf(OceanShader.Code(SeaState.Default, 0, true)).Split('\n');
        for (int i = 0; i < Math.Min(want.Length, got.Length); i++)
            Assert.True(want[i] == got[i], $"line {i + 1} differs:\n want {want[i]}\n got  {got[i]}");
        Assert.Equal(want.Length, got.Length);
    }

    /// <summary>Height, foam strength and roughness are uniforms the ocean sets, so they leave the
    /// text alone and a lab drag on them compiles nothing.</summary>
    [Fact]
    public void TheUniformFieldsLeaveTheTextAlone()
    {
        var sea = SeaState.Default with { Height = 1.4f, FoamStrength = 0.3f, RoughNear = 0.5f, RoughFar = 0.6f };
        Assert.Equal(OceanShader.Code(SeaState.Default, 0, false), OceanShader.Code(sea, 0, false));
    }

    /// <summary>Every other field reaches the text.</summary>
    [Theory]
    [InlineData("length", 1.5f)]
    [InlineData("wind", 80f)]
    [InlineData("sharpness", 0.4f)]
    [InlineData("bend_m", 90f)]
    [InlineData("bend_rad", 5f)]
    [InlineData("detail_slope", 0.12f)]
    [InlineData("detail_drift", 2f)]
    [InlineData("foam_threshold", 1f)]
    [InlineData("foam_patch", 2f)]
    [InlineData("foam_cover", 0.45f)]
    [InlineData("swell_from", 40f)]
    [InlineData("swell_full", 120f)]
    [InlineData("look_from", 20f)]
    [InlineData("look_full", 90f)]
    [InlineData("tint_b", 1.2f)]
    public void AShaderFieldChangesTheText(string key, float value)
    {
        var sea = SeaState.Find(key)!.With(SeaState.Default, value);
        Assert.NotEqual(OceanShader.Code(SeaState.Default, 0, false), OceanShader.Code(sea, 0, false));
    }

    /// <summary>A tint multiplies the open sea's colour alone; an untinted sea writes no tint term.</summary>
    [Fact]
    public void ATintMultipliesTheOpenSeaOnly()
    {
        string tinted = OceanShader.Code(SeaState.Default with { TintR = 0.9f, TintG = 1f, TintB = 1.1f }, 0, false);
        Assert.Contains("vec3 tex_mean = textureLod(albedo_tex, vec2(0.5), 16.0).rgb * vec3(0.9, 1.0, 1.1);", tinted);
        Assert.Contains("vec3 tex_mean = textureLod(albedo_tex, vec2(0.5), 16.0).rgb;", OceanShader.Code(SeaState.Default, 0, false));
    }

    /// <summary>A length scale moves every wave's omega, which is rounded again so the csky_time
    /// wrap lands on a whole number of cycles of every wave.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(1.37f)]
    [InlineData(2f)]
    public void EverySwellWaveRunsWholeCyclesPerWrap(float length)
    {
        string text = OceanShader.Code(SeaState.Default with { Length = length }, 0, false);
        var omegas = Vec2s(text, "SWELL_QW").Select(v => v.Y).ToArray();
        Assert.Equal(8, omegas.Length);
        foreach (double omega in omegas)
            AssertWhole(omega * Wrap / (2 * Math.PI), $"swell omega {omega} at length x{length}");
        var lengths = OceanShader.SwellLengths(SeaState.Default with { Length = length }).ToArray();
        Assert.Equal(152f * length, lengths[0], 3);
    }

    /// <summary>A detail drift stays a whole number of noise periods per wrap at any scale. A drift of
    /// zero holds the detail still rather than flooring at one period.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(0.35f)]
    [InlineData(2.6f)]
    [InlineData(0f)]
    public void EveryDetailLayerDriftsWholePeriodsPerWrap(float drift)
    {
        string text = OceanShader.Code(SeaState.Default with { DetailDrift = drift }, 0, false);
        var layers = Vec4s(text, "DETAIL_L");
        Assert.Equal(5, layers.Length);
        foreach (var l in layers)
        {
            AssertWhole(l.W * Wrap / 1024.0, $"detail drift {l.W} cells/s at x{drift}");
            if (drift == 0f)
                Assert.Equal(0.0, l.W);
            else
                Assert.True(l.W > 0.0, $"a moving drift never rounds to a stop ({l.W})");
        }
    }

    /// <summary>Each foam patch octave drifts a whole period of its own cells per wrap at any patch size.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(0.6f)]
    [InlineData(3.3f)]
    public void EveryFoamPatchOctaveDriftsWholePeriodsPerWrap(float patch)
    {
        string text = OceanShader.Code(SeaState.Default with { FoamPatch = patch }, 0, false);
        var terms = Regex.Matches(text, @"foam_noise\(pw \* ([0-9.]+) - vec2\(csky_time \* \(([0-9.]+) / 3600\.0\), 0\.0\), ([0-9.]+)\)");
        Assert.Equal(2, terms.Count);
        foreach (Match m in terms)
        {
            double periods = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            AssertWhole(periods, $"patch periods per wrap at x{patch}");
            Assert.Equal(periods, double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The rounding helpers alone: at least one cycle for any wave, none only for a held drift.</summary>
    [Fact]
    public void TheRoundingHelpersHoldTheirFloors()
    {
        Assert.Equal(1f, OceanShader.SwellCyclesPerWrap(1e9f));
        Assert.Equal(1.0, OceanShader.DetailTurnsPerWrap(6f, 0.01f));
        Assert.Equal(0.0, OceanShader.DetailTurnsPerWrap(6f, 0f));
        Assert.Equal(1f, OceanShader.PatchPeriodsPerWrap(1e9f, 1.5f));
    }

    /// <summary>The shader reads csky_time, never Godot's TIME, at any sea: a network peer and a
    /// fixed-step capture share the clock.</summary>
    [Fact]
    public void TheShaderReadsTheSessionClockOnly()
    {
        var rough = SeaState.Default with { Height = 2f, Length = 1.5f, DetailDrift = 3f, FoamPatch = 0.25f };
        foreach (var sea in new[] { SeaState.Default, rough })
        {
            string text = OceanShader.Code(sea, 0, true);
            Assert.DoesNotMatch(@"\bTIME\b", text);
            Assert.Contains("csky_time", text);
        }
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void AssertWhole(double value, string what) =>
        Assert.True(Math.Abs(value - Math.Round(value)) < 0.01, $"{what}: {value} per wrap is not whole");

    private static (double X, double Y)[] Vec2s(string text, string name) =>
        ConstArray(text, name, "vec2").Select(p => (p[0], p[1])).ToArray();

    private static (double X, double Y, double Z, double W)[] Vec4s(string text, string name) =>
        ConstArray(text, name, "vec4").Select(p => (p[0], p[1], p[2], p[3])).ToArray();

    // The components of a const array the generator wrote, one row per element.
    private static double[][] ConstArray(string text, string name, string type)
    {
        var line = Regex.Match(text, $@"const {type} {name}\[\d+\] = {type}\[\d+\]\((.*)\);");
        Assert.True(line.Success, $"{name} is in the text");
        return Regex.Matches(line.Groups[1].Value, $@"{type}\(([^)]*)\)")
            .Select(m => m.Groups[1].Value.Split(',').Select(s => double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray())
            .ToArray();
    }
}

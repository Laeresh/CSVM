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
    [InlineData(-11, false, "DCB433AD5B7F5B75A28493C713A49BB1D82352B67261CA8338D3C119E39F9E47")]
    [InlineData(-11, true, "61E0F749F0FB4AE70AC3C1882E0F8C1199D0976608EA0EC0613072A79AA07FC3")]
    [InlineData(-2, false, "8DBA0619295406617B2CDA7A2E4A92C07305C4E1C9BF7C83E4AE2F77AB2EDE44")]
    [InlineData(-2, true, "5D021A56F4A084250627B3A73F0A10DCAAB22288B7BAB4FEAD2D1A570EECB263")]
    [InlineData(-1, false, "9DBC65338AD55AA38733311F0458936A88335A6EB1637B1A3930056FD95A594F")]
    [InlineData(-1, true, "EC2EF21C164C2B4905C29E1AA099DD5C2EB62317FA62525AF5FE7AEAF37FF14E")]
    [InlineData(0, false, "21818332798D84639D40486414129E562297C4323C9B8113432F64120A793CC3")]
    [InlineData(0, true, "8F95175C3A08ADB252E1EC2D9E344A86B3BEB1CCFBDC413178E75C473995C7EB")]
    [InlineData(1, false, "9635C3B7BDB6EB117D35EA7BF8E3F96548E3F51C403136EA2ABFA707C250B583")]
    [InlineData(1, true, "C278F2C2F7FE2FC5F3786493D63FC72E0B5FD7C1506E99A1E42C3D1BBE74E56B")]
    [InlineData(2, false, "907D529BD19A95D7665E65FDA74F03B223B1CF8462D72E94B5D93EDB892B61F0")]
    [InlineData(2, true, "A9DF78BCCB2816912A67EE0D7DF9006EC79C955ECE8EAFA4CBAD53C357B5B1C9")]
    [InlineData(3, false, "5518AE9B25D229CF317F421EF544D2BC60EEB17ADBF38D43E29A246AAAC05C7B")]
    [InlineData(3, true, "B70ADA02E8301471609750F4C91BD196ABAE4BD877AAA4964893D11DFCF35E10")]
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

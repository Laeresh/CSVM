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
    [InlineData(-11, false, "5D59C0216F44CBBCBC2583FC6890019C5FFA54AC6BA76A9C7BD8CEE01BB7EFDB")]
    [InlineData(-11, true, "F332F260CB171FDB3F967B54147108EC1414DAD92C347D89BFF1890D82218525")]
    [InlineData(-2, false, "03C8D24929A7F8E1C7CC925BF71A647C259371272AEE89CE9879B9AA6E4A9E52")]
    [InlineData(-2, true, "ECB7C46D120183E131B82A499A1FC1B210E7BB94A376E0BE8C32021B05498457")]
    [InlineData(-1, false, "510ADC04610BBF8EB00C7B939D15A68221C4E387C5BE303D1269CBD976ED5FAA")]
    [InlineData(-1, true, "39E2F446FBC14374688EC814BA93323ADCBAD0611B91F08CFCBBBF6CDACE0F67")]
    [InlineData(0, false, "BA8AD7B30773E4FCE61FABAAB42834D26E1D2EF3EABB65CD25CD5105FD108E9B")]
    [InlineData(0, true, "E8769663CCF5F6BEADB05A6ABA3ECFE0E5E99889BEA9627F7CE9D4589C00F0D7")]
    [InlineData(1, false, "D2D3E9C2F6005EA0572AB8C5393DABC3AC3C5AD513F73F0AD6B0D6B9024734FC")]
    [InlineData(1, true, "7CF5A8BCE7BA45FE7631E0C51B6A7CEA6A184BD7C19C584A4F60246DB45F81D5")]
    [InlineData(2, false, "D24E8DCD9F1D96EBFB5A789E88FE21137E83578F99BB93AF06158305206818BD")]
    [InlineData(2, true, "B8A9B1BD5AC1956E334E8D264060F3F5D0916809C260097A77A3DDE21654A7F7")]
    [InlineData(3, false, "D517F67E508ADA9BCD771FEEC2D14E9900816AED47405E29E465F322CE399A01")]
    [InlineData(3, true, "26EDCE7C44D59ED727F88E06684D53D988852911C982953747898B38E2173D55")]
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
        Assert.Equal(1f, OceanShader.SurfLinesPerWrap(40f, 0.001f));
        Assert.Equal(0f, OceanShader.SurfLinesPerWrap(12f, 0f));
    }

    /// <summary>A sea with surf writes the surf into the foam, its lines passing a whole number per wrap at
    /// about the set speed. Without surf the text holds none, and its spacing and speed leave the text alone.</summary>
    [Theory]
    [InlineData(0.7f, 0.6f, 12f, 1.5f, 450.0)]
    [InlineData(0.8f, 0f, 12f, 1.5f, 450.0)]
    [InlineData(0.3f, 0.9f, 10f, 2f, 720.0)]
    [InlineData(0f, 0.5f, 7f, 1.3f, 669.0)]
    public void ASeaWithSurfWritesItsLinesWholePerWrap(float wash, float lines, float spacing, float speed, double perWrap)
    {
        var sea = SeaState.Default with { SurfWash = wash, SurfLines = lines, SurfSpacing = spacing, SurfSpeed = speed };
        string text = OceanShader.Code(sea, 0, true);
        Assert.Contains("foam_mix = max(foam_mix, cover * surf_keep);", text);
        var arrive = Regex.Match(text, @"float arrive = csky_time \* \(([0-9.]+) / 3600\.0\)");
        var phase = Regex.Match(text, @"float cyc = m\.g \* ([0-9.]+) \+ arrive;");
        Assert.True(arrive.Success && phase.Success, "the surf's phase is in the text");
        Assert.Equal(160.0 / spacing, double.Parse(phase.Groups[1].Value, CultureInfo.InvariantCulture), 4);
        double written = double.Parse(arrive.Groups[1].Value, CultureInfo.InvariantCulture);
        AssertWhole(written, $"surf lines at {speed} m/s every {spacing} m");
        Assert.Equal(perWrap, written);
        Assert.InRange(written * spacing / Wrap, speed - (spacing / (2 * Wrap)), speed + (spacing / (2 * Wrap)));

        string plain = OceanShader.Code(SeaState.Default, 0, true);
        Assert.DoesNotContain("surf_keep", plain);
        Assert.Equal(plain, OceanShader.Code(SeaState.Default with { SurfSpacing = spacing, SurfSpeed = speed + 1f }, 0, true));
    }

    /// <summary>The surf's foam moves only at whole rates per wrap. Every drifting noise drifts whole
    /// periods of its own cells, the warp turns whole turns, and every other csky_time rate is whole.</summary>
    [Fact]
    public void TheSurfsChurnRunsWholePerWrap()
    {
        string text = OceanShader.Code(SeaState.Default with { SurfWash = 0.7f, SurfLines = 0.6f }, 0, true);
        var drifts = Regex.Matches(text, @"- vec2\(csky_time \* \(([0-9.]+) / 3600\.0\), 0\.0\)(?: \+ vec2\([-0-9., ]+\))?, ([0-9.]+)\)");
        Assert.Equal(8, drifts.Count);
        foreach (Match m in drifts)
        {
            double periods = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            AssertWhole(periods, "a drifting noise's periods per wrap");
            Assert.Equal(periods, double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
        }
        var turn = Regex.Match(text, @"float turn = 6\.2831853 \* csky_time \* \(([0-9.]+) / 3600\.0\)");
        Assert.True(turn.Success, "the warp's turn is in the text");
        Assert.Equal(90.0, double.Parse(turn.Groups[1].Value, CultureInfo.InvariantCulture));
        var rates = Regex.Matches(text, @"csky_time \* \(([0-9.]+) / 3600\.0\)");
        Assert.Equal(10, rates.Count);
        foreach (Match m in rates)
            AssertWhole(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), "a surf rate per wrap");
        Assert.Contains("float surge = 0.25 * sin(6.2831853 * (arrive - 0.1));", text);
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

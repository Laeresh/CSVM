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
    [InlineData(-11, false, "71FF21AEB062DD0999F1D3E14A45C6BEE8A16C8BA879BFE735F4B3A948338166")]
    [InlineData(-11, true, "F8A5C316E545F98EC249AB352D5799F6B54E860A524B654B661BE111797DC82D")]
    [InlineData(-2, false, "218B403A3DC46E354F508BEE19D55637F3DDEE492209B3D767061D1012F7A1BE")]
    [InlineData(-2, true, "A0B451EF1FB25369D7679B68BE91AE7B2FA4CA8D722904E89479E4B65A33546C")]
    [InlineData(-1, false, "20420050DCE9AD4C6A491809A15A9CA3B967AE67D358F7A51BA85956DE30C3D3")]
    [InlineData(-1, true, "26271EA2B849EB49DC799D4AB24DAAC495F293C0AE05E11C198764298777489F")]
    [InlineData(0, false, "C5D0FE6C96BE76FEA793103C8DF781CEE8ABFAE44DB0242A5F0108E740D86AB6")]
    [InlineData(0, true, "0DA9D85FE5E9B4617F56B27771D34320F9B733C9E2E341649664F795C032CF4B")]
    [InlineData(1, false, "11FBBBE23C29B199D275F7489A9BDFADBCE982B0A25159764CFC990A5DE533EE")]
    [InlineData(1, true, "918606C4D02045FEE325DB07F56C1BBF9EA206556C0112C4C7E0CE2C7B5C3052")]
    [InlineData(2, false, "56EE625E9FA97CEA3CC394B7FD7D8AD185FFFF7695459983A732A2F3E016C341")]
    [InlineData(2, true, "69EFC642CA46ADFE1456D009AA133CE4A66E681EBF7399B2EA01085B9862A1A9")]
    [InlineData(3, false, "2F8FD602A8708AB7DE9492CEBCA0836A0115BFC857F4F826ECC8A638AB2271AD")]
    [InlineData(3, true, "3F8FCE9468EFEC3A298082CE6EBBDC1753C587851BF1CDB0BE6B963F18BEC991")]
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
        var arrive = Regex.Match(text, @"float arrive = csky_surf_arrive\(p, \(([0-9.]+) / 3600\.0\)\);");
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
        var rates = Regex.Matches(text, @"(?:csky_time \* |csky_surf_arrive\(p, )\(([0-9.]+) / 3600\.0\)");
        Assert.Equal(10, rates.Count);
        foreach (Match m in rates)
            AssertWhole(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), "a surf rate per wrap");
        Assert.Contains("float surge = 0.25 * sin(6.2831853 * (arrive - 0.1));", text);
    }

    /// <summary>The cliff spray's C# phase is the include's. The body it mirrors is unchanged. The noise is
    /// zero on its lattice and continuous across a cell edge, and the phase gains a cycle a line.</summary>
    [Fact]
    public void TheSpraysPhaseMirrorsTheInclude()
    {
        string include = File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "shaders", "csky_ocean.gdshaderinc"));
        Assert.Contains("return csky_time * lines_per_sec + 0.795775 * foam_noise(p * 0.025, 65536.0);", include);
        Assert.Contains("uvec2 v = uvec2(ivec2(c)) * 1664525u + 1013904223u;", include);
        Assert.Equal(0f, OceanShader.FoamNoise(3f, -7f, 65536f));
        float peak = 0f;
        for (int i = 0; i < 400; i++)
        {
            float y = (i * 0.173f) - 30f, n = OceanShader.FoamNoise(5f - 1e-4f, y, 65536f);
            Assert.InRange(n - OceanShader.FoamNoise(5f + 1e-4f, y, 65536f), -2e-3f, 2e-3f);
            peak = Math.Max(peak, Math.Abs(OceanShader.FoamNoise(i * 0.37f, y, 65536f)));
        }
        Assert.InRange(peak, 0.1f, 1f);
        float rate = 450f / 3600f, a = OceanShader.SurfArrive(-7845f, -3350f, 100f, rate);
        Assert.Equal(a + 1f, OceanShader.SurfArrive(-7845f, -3350f, 108f, rate), 3);
    }

    /// <summary>The spray sheet draws only with the ocean standing (csky_ocean_on, 0 under Original and at
    /// flat water) and the sea breaking surf. It never draws in a spyglass disc, and keeps the surf's phase.</summary>
    [Fact]
    public void TheSpraySheetIsGatedAndTimedByTheSurf()
    {
        string text = OceanShader.SprayCode();
        Assert.Contains(CSVM.Mech3.SceneBuilder.OceanInclude, text);
        Assert.Contains($"if (csky_ocean_on < 0.5 || csky_ocean_surf.y < 0.5 || {CSVM.Mech3.SceneBuilder.FlatSeaEye} || v_s > GONE) {{", text);
        Assert.Contains("v_s = fract(csky_surf_arrive(origin.xz, csky_ocean_surf.x) - 0.25 - LAG);", text);
        Assert.DoesNotContain("TIME", text.Replace("csky_time", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>The surf's phase is one function in the shared include, which the ocean and the coasts' swash
    /// both call. Its body with the ocean's rate put in is the line the ocean wrote before the move, so the
    /// ocean's pixels hold. The rate the coasts read is the ocean's own.</summary>
    [Theory]
    [InlineData(12f, 1.5f, "450.0")]
    [InlineData(10f, 2f, "720.0")]
    public void TheSurfsPhaseIsOneSharedFunction(float spacing, float speed, string perWrap)
    {
        string include = File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "shaders", "csky_ocean.gdshaderinc"));
        var body = Regex.Match(include, @"float csky_surf_arrive\(vec2 p, float lines_per_sec\) \{\s*return (.+);\s*\}");
        Assert.True(body.Success, "the include defines the surf's phase");
        var sea = SeaState.Default with { SurfWash = 0.7f, SurfLines = 0.6f, SurfSpacing = spacing, SurfSpeed = speed };
        string text = OceanShader.Code(sea, 0, true);
        string rate = $"({perWrap} / 3600.0)";
        Assert.Contains($"float arrive = csky_surf_arrive(p, {rate});", text);
        Assert.Equal($"csky_time * {rate} + 0.795775 * foam_noise(p * 0.025, 65536.0)", body.Groups[1].Value.Replace("lines_per_sec", rate, StringComparison.Ordinal));
        Assert.Contains(CSVM.Mech3.SceneBuilder.OceanInclude, text);
        Assert.DoesNotContain("float foam_noise(", text);
        Assert.DoesNotContain("vec2 sea_hash(", text);
        var global = OceanShader.SurfGlobal(sea);
        Assert.Equal(float.Parse(perWrap, CultureInfo.InvariantCulture) / 3600f, global.X);
        Assert.Equal(1f, global.Y);
        Assert.InRange(global.Z, 1f, OceanMaskRaster.InlandReach);
        Assert.Equal(OceanMaskRaster.InlandReach, global.W);
        Assert.Equal(0f, OceanShader.SurfGlobal(SeaState.Default).Y);
    }

    /// <summary>The swash's own film drifts whole periods per wrap, and it takes only fragments within the
    /// sea-level band the mask calls coplanar.</summary>
    [Fact]
    public void TheSwashRunsWholePerWrapAtSeaLevel()
    {
        string include = File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "shaders", "csky_ocean.gdshaderinc"));
        var drifts = Regex.Matches(include, @"- vec2\(csky_time \* \(([0-9.]+) / 3600\.0\), 0\.0\), ([0-9.]+)\)");
        Assert.Single(drifts);
        AssertWhole(double.Parse(drifts[0].Groups[1].Value, CultureInfo.InvariantCulture), "the swash film's drift");
        Assert.Equal(drifts[0].Groups[1].Value, drifts[0].Groups[2].Value);
        Assert.Contains($"abs(world.y) >= {OceanMaskRaster.SeaLevelBand.ToString("0.0###", CultureInfo.InvariantCulture)})", include);
        Assert.DoesNotMatch(@"\bTIME\b", include);
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

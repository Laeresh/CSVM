using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Flight.Hud;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The <c>--xr-tune</c> lab's engine-free parts (<see cref="VrCockpitTuning"/>,
/// <see cref="VrTuneRepeat"/>): a step's arithmetic and clamp, the plane order and the held input's
/// repeat. Then the writer, which puts one plane's values into <c>vr_cockpits.json</c> and leaves
/// every other byte of the file alone.
/// </summary>
[Trait("Tier", "Quick")]
public class VrCockpitTuningTests
{
    private static readonly string Shipped = Path.Combine(TestData.RepoRoot, "CSVM", "data", "vr_cockpits.json");

    [Fact]
    public void AScaleStepIsAMillimetrePerUnitAndFiveDownReachTheMeasuredAlternative()
    {
        var up = VrCockpitTuning.Step(VrCockpit.Default, VrTuneKnob.Scale, 1);
        var down = VrCockpitTuning.Step(VrCockpit.Default, VrTuneKnob.Scale, -5);

        Assert.Equal(1.025f, up.InteriorScaleFactor);
        Assert.Equal(0.041f, up.InteriorScale, 6);
        Assert.Equal(0.875f, down.InteriorScaleFactor);
        Assert.Equal(0f, down.EyeOffsetZ);
    }

    [Fact]
    public void ManySmallStepsLandOnTheFilesGridWithNoDrift()
    {
        var at = VrCockpit.Default;
        for (int i = 0; i < 37; i++)
        {
            at = VrCockpitTuning.Step(at, VrTuneKnob.Aft, -1);
            at = VrCockpitTuning.Step(at, VrTuneKnob.Up, 1);
            at = VrCockpitTuning.Step(at, VrTuneKnob.Scale, 1);
        }

        Assert.Equal(-0.37f, at.EyeOffsetZ);
        Assert.Equal(0.37f, at.EyeOffsetY);
        Assert.Equal(1.925f, at.InteriorScaleFactor);
        Assert.Equal("-0.37", VrCockpitTuning.Format(at.EyeOffsetZ));
        Assert.Equal("1.925", VrCockpitTuning.Format(at.InteriorScaleFactor));
    }

    [Fact]
    public void AStepClampsToTheRangeTheFileAccepts()
    {
        var small = VrCockpitTuning.Step(new VrCockpit(0.3f, 0f), VrTuneKnob.Scale, -10);
        var big = VrCockpitTuning.Step(new VrCockpit(3.99f, 0f), VrTuneKnob.Scale, 10);
        var far = VrCockpitTuning.Step(new VrCockpit(1f, 0.995f, -0.995f), VrTuneKnob.Aft, 5);
        var low = VrCockpitTuning.Step(far, VrTuneKnob.Up, -5);

        Assert.Equal(0.25f, small.InteriorScaleFactor);
        Assert.Equal(4f, big.InteriorScaleFactor);
        Assert.Equal(1f, far.EyeOffsetZ);
        Assert.Equal(-1f, low.EyeOffsetY);
        Assert.Equal(VrCockpit.Default, VrCockpitTuning.Step(VrCockpit.Default, VrTuneKnob.Plane, 3));
    }

    [Theory]
    [InlineData(1f, "1.0")]
    [InlineData(0.875f, "0.875")]
    [InlineData(0.06f, "0.06")]
    [InlineData(-0.1f, "-0.1")]
    [InlineData(-0f, "0.0")]
    [InlineData(1.0249999f, "1.025")]
    public void ANumberIsWrittenOnTheFilesGridInTheInvariantCulture(float value, string written)
    {
        Assert.Equal(written, VrCockpitTuning.Format(value));
    }

    [Fact]
    public void ThePlaneStepWalksTheFilesOrderAndWraps()
    {
        var names = new[] { "player_bhawk", "player_pfighter", "player_fury" };

        Assert.Equal(1, VrCockpitTuning.NextPlane(names, "player_bhawk", 1));
        Assert.Equal(2, VrCockpitTuning.NextPlane(names, "PLAYER_BHAWK", -1));
        Assert.Equal(0, VrCockpitTuning.NextPlane(names, "player_fury", 1));
        Assert.Equal(0, VrCockpitTuning.NextPlane(names, "player_custom", -1));
        Assert.Null(VrCockpitTuning.NextPlane(Array.Empty<string>(), "player_fury", 1));
    }

    [Fact]
    public void AHeldPushStepsOnceThenRepeatsAfterTheDelay()
    {
        var repeat = new VrTuneRepeat();
        const float dt = 0.0625f;
        var steps = new List<int>();
        for (int i = 0; i < 20; i++)
        {
            steps.Add(repeat.Step(0.9f, dt));
        }

        // Frames of 1/16 s: the push's own frame, then the first frame held 0.45 s (0.5 s). After it,
        // the first frame past each further 0.12 s (0.57, 0.69, 0.81, 0.93, 1.05, 1.17).
        Assert.Equal(new[] { 0, 8, 10, 12, 13, 15, 17, 19 }, steps.Select((s, i) => (s, i)).Where(p => p.s == 1).Select(p => p.i));
        Assert.All(steps, s => Assert.True(s >= 0));
    }

    [Fact]
    public void APushHoldsUnderTheEngageLevelAndEndsUnderTheReleaseLevel()
    {
        var repeat = new VrTuneRepeat();

        Assert.Equal(0, repeat.Step(0.5f, 0.01f));
        Assert.Equal(-1, repeat.Step(-0.7f, 0.01f));
        Assert.Equal(0, repeat.Step(-0.4f, 0.01f));
        Assert.Equal(0, repeat.Step(-0.3f, 0.01f));
        Assert.Equal(-1, repeat.Step(-0.65f, 0.01f));
        Assert.Equal(1, repeat.Step(0.65f, 0.01f));
    }

    [Fact]
    public void WithoutRepeatAHeldPushStepsOnce()
    {
        var once = new VrTuneRepeat(repeats: false);
        int total = 0;
        for (int i = 0; i < 200; i++)
        {
            total += once.Step(-1f, 0.01f);
        }

        once.Clear();
        total += once.Step(-1f, 0.01f);

        Assert.Equal(-2, total);
    }

    [Fact]
    public void RewritingEveryCommittedPlaneWithItsOwnValuesLeavesTheFileByteIdentical()
    {
        byte[] file = File.ReadAllBytes(Shipped);
        var table = VrCockpits.Parse(file, new List<string>());

        Assert.Equal(11, table.Names.Count);
        foreach (var plane in table.Names)
        {
            Assert.True(VrCockpitTuning.TryRewrite(file, plane, table.For(plane), out var rewritten, out var why), why);
            Assert.Equal(file, rewritten);
        }
    }

    [Fact]
    public void ASavedPlaneChangesOnlyItsOwnNumbersAndKeepsTheNotes()
    {
        byte[] file = File.ReadAllBytes(Shipped);
        var entry = new VrCockpit(0.875f, 0.06f);

        Assert.True(VrCockpitTuning.TryRewrite(file, "player_warhawk", entry, out var rewritten, out var why), why);

        string before = Encoding.UTF8.GetString(file);
        string after = Encoding.UTF8.GetString(rewritten);
        var problems = new List<string>();
        var table = VrCockpits.Parse(rewritten, problems);
        Assert.Empty(problems);
        Assert.Equal(entry, table.For("player_warhawk"));
        Assert.Equal(VrCockpit.Default, table.For("player_bhawk"));
        const string Was = "\"player_warhawk\": {\n      \"interiorScaleFactor\": 1.0,\n      \"eyeOffsetZ\": 0.0,\n";
        const string Now = "\"player_warhawk\": {\n      \"interiorScaleFactor\": 0.875,\n      \"eyeOffsetZ\": 0.06,\n";
        string beforeLf = before.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains(Was, beforeLf, StringComparison.Ordinal);
        Assert.Equal(beforeLf.Replace(Was, Now, StringComparison.Ordinal), after.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(before.Count(c => c == '\r'), after.Count(c => c == '\r'));
    }

    [Fact]
    public void AnUpOffsetIsAddedAfterTheLastFieldOnTheEntrysOwnLayout()
    {
        string json = "{\r\n  \"planes\": {\r\n    \"player_bhawk\": { \"interiorScaleFactor\": 1.0, \"eyeOffsetZ\": 0.0 },\r\n"
            + "    \"player_warhawk\": {\r\n      \"interiorScaleFactor\": 1.0,\r\n      \"eyeOffsetZ\": 0.0,\r\n      \"_proposed\": \"x\"\r\n    }\r\n  }\r\n}\r\n";

        string bhawk = Rewrite(json, "player_bhawk", new VrCockpit(1f, -0.1f, 0.02f));
        string warhawk = Rewrite(json, "player_warhawk", new VrCockpit(1f, 0.06f, -0.03f));

        Assert.Equal(json.Replace("\"eyeOffsetZ\": 0.0 },", "\"eyeOffsetZ\": -0.1, \"eyeOffsetY\": 0.02 },", StringComparison.Ordinal), bhawk);
        Assert.Equal(json.Replace("\"eyeOffsetZ\": 0.0,\r\n      \"_proposed\"", "\"eyeOffsetZ\": 0.06,\r\n      \"eyeOffsetY\": -0.03,\r\n      \"_proposed\"", StringComparison.Ordinal), warhawk);
    }

    [Fact]
    public void APresentUpOffsetIsRewrittenInPlaceEvenAtZero()
    {
        string json = """{ "planes": { "player_fury": { "interiorScaleFactor": 0.9, "eyeOffsetZ": 0.0, "eyeOffsetY": 0.05 } } }""";

        Assert.Equal(
            """{ "planes": { "player_fury": { "interiorScaleFactor": 0.9, "eyeOffsetZ": 0.0, "eyeOffsetY": 0.0 } } }""",
            Rewrite(json, "player_fury", new VrCockpit(0.9f, 0f, 0f)));
    }

    [Fact]
    public void APlaneTheFileLacksIsAddedAfterTheLastWithItsIndent()
    {
        string json = "{\n  \"_about\": \"notes\",\n  \"planes\": {\n    \"player_bhawk\": { \"interiorScaleFactor\": 1.0, \"eyeOffsetZ\": 0.0 }\n  }\n}\n";

        string after = Rewrite(json, "player_kestrel", new VrCockpit(0.95f, -0.02f));

        Assert.Equal(
            "{\n  \"_about\": \"notes\",\n  \"planes\": {\n    \"player_bhawk\": { \"interiorScaleFactor\": 1.0, \"eyeOffsetZ\": 0.0 },\n"
            + "    \"player_kestrel\": { \"interiorScaleFactor\": 0.95, \"eyeOffsetZ\": -0.02 }\n  }\n}\n",
            after);
        Assert.Equal("""{ "planes": { "player_fury": { "interiorScaleFactor": 1.0, "eyeOffsetZ": 0.0 } } }""",
            Rewrite("""{ "planes": {} }""", "player_fury", VrCockpit.Default));
    }

    [Fact]
    public void AnEntryWithOnlyNotesTakesTheFieldsAfterItsBrace()
    {
        string json = """{ "planes": { "player_fury": { "_proposed": "try 0.9" } } }""";

        string after = Rewrite(json, "player_fury", new VrCockpit(0.9f, 0f));

        Assert.Equal("""{ "planes": { "player_fury": { "interiorScaleFactor": 0.9, "eyeOffsetZ": 0.0, "_proposed": "try 0.9" } } }""", after);
    }

    [Fact]
    public void AByteOrderMarkStaysAndNonAsciiNotesSurvive()
    {
        string json = """{ "_about": "a note with an arrow → and a dash —", "planes": { "player_fury": { "interiorScaleFactor": 1.0, "eyeOffsetZ": 0.0 } } }""";
        byte[] file = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(json)).ToArray();

        Assert.True(VrCockpitTuning.TryRewrite(file, "player_fury", new VrCockpit(1.1f, 0f), out var rewritten, out var why), why);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, rewritten.Take(3));
        Assert.Equal(json.Replace("\"interiorScaleFactor\": 1.0", "\"interiorScaleFactor\": 1.1", StringComparison.Ordinal),
            Encoding.UTF8.GetString(rewritten, 3, rewritten.Length - 3));
    }

    [Theory]
    [InlineData("""{ "player_fury": { "eyeOffsetZ": 0.1 } }""", "planes")]
    [InlineData("""{ "planes": { "player_fury": """, "JSON")]
    [InlineData("""{ "planes": { "player_fury": 1.5 } }""", "other entries")]
    public void AFileThatCannotTakeTheEntryIsRefusedAndLeftAlone(string json, string named)
    {
        byte[] file = Encoding.UTF8.GetBytes(json);

        Assert.False(VrCockpitTuning.TryRewrite(file, "player_fury", new VrCockpit(0.9f, 0f), out var rewritten, out var why));

        Assert.Contains(named, why, StringComparison.Ordinal);
        Assert.Same(file, rewritten);
    }

    [Fact]
    public void TheReadoutNamesThePlaneEachFieldItsStepAndWhetherItIsSaved()
    {
        string text = VrCockpitTuning.Readout("player_warhawk", 4, 11, new VrCockpit(0.9f, 0.06f, -0.01f), VrCockpit.Default,
            VrTuneKnob.Aft, "saved player_warhawk");

        Assert.Contains("player_warhawk (4 of 11), unsaved", text, StringComparison.Ordinal);
        Assert.Contains("scale 0.9 (36.0 mm per unit), step 0.025", text, StringComparison.Ordinal);
        Assert.Contains("> eye aft 0.06 m, step 0.01", text, StringComparison.Ordinal);
        Assert.Contains("eye up -0.01 m", text, StringComparison.Ordinal);
        Assert.Contains("file: scale 1.0, aft 0.0, up 0.0", text, StringComparison.Ordinal);
        Assert.EndsWith("saved player_warhawk", text, StringComparison.Ordinal);
        Assert.DoesNotContain("unsaved", VrCockpitTuning.Readout("player_fury", 3, 11, VrCockpit.Default, VrCockpit.Default, VrTuneKnob.Scale, string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void TheIdleReadoutIsOneLineWithThePlaneAndItsValues()
    {
        string idle = VrCockpitTuning.Summary("player_kestrel", 10, 11, new VrCockpit(0.95f, -0.12f), VrCockpit.Default, string.Empty);

        Assert.Equal("tune player_kestrel (10 of 11): scale 0.95, aft -0.12, up 0.0, unsaved  [right grip]", idle);
        Assert.EndsWith("\nsave failed: x", VrCockpitTuning.Summary("player_kestrel", 10, 11, VrCockpit.Default, VrCockpit.Default, "save failed: x"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheLabIsOffUnlessTheFlagAsksForIt()
    {
        Assert.False(CSVM.Spec.SessionSpec.Parse(new[] { "--fly", "--xr-sim" }).XrTune);
        Assert.True(CSVM.Spec.SessionSpec.Parse(new[] { "--fly", "--xr-tune" }).XrTune);
    }

    private static string Rewrite(string json, string plane, VrCockpit entry)
    {
        Assert.True(VrCockpitTuning.TryRewrite(Encoding.UTF8.GetBytes(json), plane, entry, out var rewritten, out var why), why);
        return Encoding.UTF8.GetString(rewritten);
    }
}

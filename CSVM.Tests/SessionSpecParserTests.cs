using System.Globalization;
using System.Threading;
using CSVM;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The argument value grammars. All seven were <c>private</c> inside <c>GameSession</c> and so had
/// no coverage at all; they are the parts of the launch surface most likely to be wrong in a way
/// nothing downstream notices, because a mis-parsed vector or hold sequence still produces a
/// perfectly valid session.
/// </summary>
public class SessionSpecParserTests
{
    [Fact]
    public void AVectorIsThreeInvariantCultureFloats()
    {
        Assert.Equal(new Vector3(1.5f, -2f, 30.25f), SessionSpec.ParseVec3("1.5,-2,30.25"));
    }

    /// <summary>A German machine writes `0,15` for nought point one five; the parse is pinned to the
    /// invariant culture so a command line means the same thing on every machine — and so a captured
    /// baseline does not differ from itself across locales.</summary>
    [Fact]
    public void AVectorParsesTheSameUnderAnyCulture()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(new Vector3(1.5f, 2.5f, 3.5f), SessionSpec.ParseVec3("1.5,2.5,3.5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    [Fact]
    public void APlaneListIsCommaSeparatedAndTrimmed()
    {
        Assert.Equal(new[] { "player_bhawk" }, SessionSpec.ParsePlanes("player_bhawk"));
        Assert.Equal(new[] { "player_bhawk", "player_fury" },
            SessionSpec.ParsePlanes(" player_bhawk , player_fury "));
        // Empty entries are dropped rather than becoming a nameless player.
        Assert.Equal(new[] { "player_bhawk" }, SessionSpec.ParsePlanes("player_bhawk,,"));
        Assert.Empty(SessionSpec.ParsePlanes(""));
    }

    /// <summary>5 has no perspective of its own (the middle of the pad is the chase camera), and
    /// anything outside 1–9 is a typo — both give 0, which the caller reports.</summary>
    [Theory]
    [InlineData("1", 1)]
    [InlineData("4", 4)]
    [InlineData("9", 9)]
    [InlineData("5", 0)]
    [InlineData("0", 0)]
    [InlineData("10", 0)]
    [InlineData("", 0)]
    [InlineData("two", 0)]
    public void TheNumpadViewDigitRejectsWhatHasNoPerspective(string arg, int expected)
        => Assert.Equal(expected, SessionSpec.ParseView(arg));

    [Fact]
    public void AHoldIsOneScriptedSequencePerPlayer()
    {
        var sets = SessionSpec.ParseHold("0,0,0,1@2;0.5,0,0,1|0,1,0,0.5");
        Assert.Equal(2, sets.Length);

        // Player one: two segments, the first held for two seconds, the last held forever.
        Assert.Equal(2, sets[0].Length);
        Assert.Equal(2f, sets[0][0].Item2);
        Assert.Equal(1f, sets[0][0].Item1.Throttle);
        Assert.Equal(0f, sets[0][1].Item2);
        Assert.Equal(0.5f, sets[0][1].Item1.Pitch);

        // Player two: the plain constant-input form, which is one segment with no duration.
        Assert.Single(sets[1]);
        Assert.Equal(1f, sets[1][0].Item1.Roll);
        Assert.Equal(0.5f, sets[1][0].Item1.Throttle);
    }

    [Fact]
    public void AHoldReadsPitchRollYawThrottleInThatOrder()
    {
        var input = SessionSpec.ParseHold("0.1,0.2,0.3,0.4")[0][0].Item1;
        Assert.Equal(0.1f, input.Pitch);
        Assert.Equal(0.2f, input.Roll);
        Assert.Equal(0.3f, input.Yaw);
        Assert.Equal(0.4f, input.Throttle);
    }

    /// <summary>Three colour slots — body, dark trim, light trim — and a missing one repeats the
    /// last given, so a single triple paints the whole aircraft.</summary>
    [Fact]
    public void APaintColourRepeatsTheLastSlotGiven()
    {
        var one = SessionSpec.ParsePaintColors("255,0,0");
        Assert.Equal(3, one.Length);
        Assert.Equal(one[0], one[1]);
        Assert.Equal(one[0], one[2]);
        Assert.Equal(PaintScheme.FromBytes(255, 0, 0), one[0]);

        var two = SessionSpec.ParsePaintColors("255,0,0/0,255,0");
        Assert.Equal(PaintScheme.FromBytes(255, 0, 0), two[0]);
        Assert.Equal(PaintScheme.FromBytes(0, 255, 0), two[1]);
        Assert.Equal(two[1], two[2]);
    }

    [Fact]
    public void APaintDecalRepeatsTheLastSlotGiven()
    {
        Assert.Equal(new[] { 4, 4, 4 }, SessionSpec.ParsePaintDecals("4"));
        Assert.Equal(new[] { 1, 2, 2 }, SessionSpec.ParsePaintDecals("1,2"));
        Assert.Equal(new[] { 1, 2, 3 }, SessionSpec.ParsePaintDecals("1,2,3"));
    }

    /// <summary>A damage preset is part:fraction; a value above 1 reads as a percent, and everything
    /// is clamped into 0–1 so a typo cannot ask for negative or 300% damage.</summary>
    [Fact]
    public void ADamagePresetTakesFractionsOrPercents()
    {
        var preset = SessionSpec.ParseDamagePreset("nose:0.25, leftwing:40, tail:300, rudder:-2");
        Assert.Equal(4, preset.Count);
        Assert.Equal(("nose", 0.25f), preset[0]);
        Assert.Equal(("leftwing", 0.4f), preset[1]);
        Assert.Equal(("tail", 1f), preset[2]);
        Assert.Equal(("rudder", 0f), preset[3]);
    }

    /// <summary>A malformed pair is skipped and handed back as data when the caller asks, which is
    /// how the spec reports it without a Godot runtime to print into.</summary>
    [Fact]
    public void AMalformedDamagePairIsSkippedAndReported()
    {
        var rejected = new System.Collections.Generic.List<string>();
        var preset = SessionSpec.ParseDamagePreset("nose:0.25,garbage,tail:notanumber", rejected);

        Assert.Single(preset);
        Assert.Equal(new[] { "garbage", "tail:notanumber" }, rejected);
    }

    /// <summary>The same value reaching the spec through its flag, so the wiring is covered and not
    /// just the grammar.</summary>
    [Fact]
    public void TheFlagsReachTheseParsers()
    {
        var s = SessionSpec.Parse(new[]
        {
            "--damage=nose:50", "--paint-decal=1,2", "--hold=0,0,0,1", "--pos=1,2,3",
        });
        Assert.Equal(("nose", 0.5f), Assert.Single(s.DamagePreset!));
        Assert.Equal(new[] { 1, 2, 2 }, s.PaintDecalOverride);
        Assert.Single(s.HoldSets!);
        Assert.Equal(new Vector3(1, 2, 3), s.Pos);
    }

    /// <summary>The B5/E16 grammar: `--ai=plane[:net][:accent=id],…` splits into
    /// (plane, net, accent) entries, a bare name has a null net and accent, an empty net after
    /// the colon reads as none, and `accent=` binds wherever it appears among the segments.</summary>
    [Fact]
    public void AiEntriesCarryTheirOptionalNetReference()
    {
        var s = SessionSpec.Parse(new[] { "--ai=player_fury:M4ReinfAce,player_bhawk,ebrigand:10,edevast:" });
        Assert.Equal(4, s.AiPlanes!.Count);
        Assert.Equal(("player_fury", "M4ReinfAce", null), s.AiPlanes[0]);
        Assert.Equal(("player_bhawk", null, null), s.AiPlanes[1]);
        Assert.Equal(("ebrigand", "10", null), s.AiPlanes[2]);
        Assert.Equal(("edevast", null, null), s.AiPlanes[3]);

        var a = SessionSpec.Parse(new[] { "--ai=player_fury:M4ReinfAce:accent=12,ebrigand:accent=14,edevast:accent=12:7" });
        Assert.Equal(("player_fury", "M4ReinfAce", 12), a.AiPlanes![0]);
        Assert.Equal(("ebrigand", null, 14), a.AiPlanes[1]);
        Assert.Equal(("edevast", "7", 12), a.AiPlanes[2]);
    }

    /// <summary>The D14 gunnery arm: bare `--ai-attack` reads skill 5, a value clamps to the
    /// 1–9 rating scale, and an absent flag stays null (no gunner armed).</summary>
    [Fact]
    public void AiAttackCarriesAClampedSkillRating()
    {
        Assert.Null(SessionSpec.Parse(new[] { "--fly" }).AiAttackSkill);
        Assert.Equal(5, SessionSpec.Parse(new[] { "--ai-attack" }).AiAttackSkill);
        Assert.Equal(9, SessionSpec.Parse(new[] { "--ai-attack=9" }).AiAttackSkill);
        Assert.Equal(1, SessionSpec.Parse(new[] { "--ai-attack=0" }).AiAttackSkill);
        Assert.Equal(9, SessionSpec.Parse(new[] { "--ai-attack=42" }).AiAttackSkill);
    }

    [Fact]
    public void AMalformedDamagePresetIsReportedThroughTheSpecsWarnings()
    {
        var s = SessionSpec.Parse(new[] { "--damage=garbage" });
        Assert.Contains(s.Warnings, w => w.Message.Contains("cannot parse 'garbage'"));
        // An empty category means a bare console line rather than a log category.
        Assert.Contains(s.Warnings, w => w.Category.Length == 0);
    }

    /// <summary>PLAN-perf-hitches B5: `[alloc:]&lt;ms&gt;[@frame]` — a bare `&lt;ms&gt;` is the
    /// busy-wait form at the default frame, `@frame` overrides it, and `alloc:` switches to the
    /// allocation-burst form without disturbing either the magnitude or the frame.</summary>
    [Fact]
    public void HitchInjectParsesMagnitudeFormAndFrame()
    {
        Assert.Equal((50f, false, SessionSpec.Parse(new[] { "--hitch-inject=50" }).HitchInjectFrame),
            SessionSpec.ParseHitchInject("50"));
        Assert.Equal((50f, false, 120), SessionSpec.ParseHitchInject("50@120"));
        Assert.Equal((50f, true, 120), SessionSpec.ParseHitchInject("alloc:50@120"));
        Assert.Equal((5f, true, SessionSpec.Parse(new[] { "--hitch-inject=alloc:5" }).HitchInjectFrame),
            SessionSpec.ParseHitchInject("alloc:5"));
    }

    /// <summary>The same grammar reaching the spec through its flag: absent by default (so B4's
    /// detector runs unperturbed), and each field lands where the injector call site reads it.</summary>
    [Fact]
    public void TheHitchInjectFlagReachesTheSpec()
    {
        Assert.Null(SessionSpec.Parse(new[] { "--fly" }).HitchInjectMs);

        var s = SessionSpec.Parse(new[] { "--hitch-inject=50@120" });
        Assert.Equal(50f, s.HitchInjectMs);
        Assert.False(s.HitchInjectAlloc);
        Assert.Equal(120, s.HitchInjectFrame);

        var a = SessionSpec.Parse(new[] { "--hitch-inject=alloc:5@30" });
        Assert.Equal(5f, a.HitchInjectMs);
        Assert.True(a.HitchInjectAlloc);
        Assert.Equal(30, a.HitchInjectFrame);
    }
}

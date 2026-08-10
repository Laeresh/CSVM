using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The puffer's two camera-distance bands (see <c>docs/org/puffer.md</c>): both parsers must wire
/// <c>NEAR_FADE</c>/<c>unk_range</c> and <c>FADE_RANGE</c>|<c>FAR_FADE</c>/<c>fade_range</c>
/// through, an unauthored state must keep the puffer object's own ctor defaults (near
/// <c>(0, 0)</c>, far <c>(FLT_MAX, FLT_MAX)</c> — i.e. no fade and no cull), and the reader's two
/// spellings of the far band must land in the same place.
///
/// <para>⚠ The values below are copied from the shipped readers, descending near pairs and all.
/// <c>NEAR_FADE [40, 5]</c> is not a typo and must not be "repaired" into <c>[5, 40]</c>: index 0
/// is the hard cull cutoff and index 1 is where alpha would reach 1, an order settled in
/// <c>crimson.exe</c> at six independent points and NOT inferable from the authored numbers.</para>
///
/// <para>The alpha arithmetic those fields feed is asserted in the <c>puffer-distance-fade</c>
/// engine suite, which needs a live <c>Puffer</c>; these tests cover only the two parsers.</para>
/// </summary>
public class PufferDistanceFadeTests
{
    [Fact]
    public void ReaderParsesBothBands()
    {
        // C3 waterfalls.zrd.json's spew_puffer, verbatim: FADE_RANGE [300, 400], NEAR_FADE [40, 5].
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "spew_puffer" },
                "NUMBER", new List<object?> { 5f },
                "FADE_RANGE", new List<object?> { 300f, 400f },
                "NEAR_FADE", new List<object?> { 40f, 5f },
            },
        };

        var state = PufferState.FindInReader(reader, "spew_puffer");

        Assert.NotNull(state);
        Assert.Equal(40f, state!.NearFadeStart);
        Assert.Equal(5f, state.NearFadeEnd);
        Assert.Equal(300f, state.FarFadeStart);
        Assert.Equal(400f, state.FarFadeEnd);
    }

    [Fact]
    public void ReaderAcceptsFarFadeAsAnAliasOfFadeRange()
    {
        // C3 volcanosmoke.zrd.json is the ONE reader block in the install that spells the far band
        // FAR_FADE rather than FADE_RANGE (574 use the latter). Its near pair is also the only
        // ascending one, which is what makes it the single puffer that reaches the near RAMP.
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "volcanosmoke" },
                "NUMBER", new List<object?> { 3f },
                "FAR_FADE", new List<object?> { 2000f, 3000f },
                "NEAR_FADE", new List<object?> { 1f, 75f },
            },
        };

        var state = PufferState.FindInReader(reader, "volcanosmoke");

        Assert.NotNull(state);
        Assert.Equal(2000f, state!.FarFadeStart);
        Assert.Equal(3000f, state.FarFadeEnd);
        Assert.Equal(1f, state.NearFadeStart);
        Assert.Equal(75f, state.NearFadeEnd);
    }

    [Fact]
    public void ReaderWithNoFadeKeysKeepsTheEngineCtorDefaults()
    {
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "plain_puffer" },
                "NUMBER", new List<object?> { 5f },
            },
        };

        var state = PufferState.FindInReader(reader, "plain_puffer");

        Assert.NotNull(state);
        Assert.Equal(0f, state!.NearFadeStart);
        Assert.Equal(0f, state.NearFadeEnd);
        Assert.Equal(float.MaxValue, state.FarFadeStart);
        Assert.Equal(float.MaxValue, state.FarFadeEnd);
    }

    [Fact]
    public void CompiledEventParsesBothBands()
    {
        // The compiled surface spells NEAR_FADE `unk_range`: C1's
        // black_smoke_ball_01-large_black_smokeball.json carries {70, 20} there, and its reader
        // block authors NEAR_FADE [70, 20] — which is how the field was identified.
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "large_black_smokeball",
            ["number"] = 5f,
            ["unk_range"] = new Dictionary<string, object?> { ["min"] = 70f, ["max"] = 20f },
            ["fade_range"] = new Dictionary<string, object?> { ["min"] = 400f, ["max"] = 600f },
        });

        var state = PufferState.FromAnimEvent(d);

        Assert.Equal(70f, state.NearFadeStart);
        Assert.Equal(20f, state.NearFadeEnd);
        Assert.Equal(400f, state.FarFadeStart);
        Assert.Equal(600f, state.FarFadeEnd);
    }

    [Fact]
    public void CompiledEventWithNullFadeKeysKeepsTheEngineCtorDefaults()
    {
        // Both keys are present-and-null on the great majority of the install's 4,535 events; the
        // parser must read that as unauthored, not as zero — a zero far band would discard every
        // particle of every puffer that says nothing about distance.
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "steampuffer",
            ["number"] = 5f,
            ["unk_range"] = null,
            ["fade_range"] = null,
        });

        var state = PufferState.FromAnimEvent(d);

        Assert.Equal(0f, state.NearFadeStart);
        Assert.Equal(0f, state.NearFadeEnd);
        Assert.Equal(float.MaxValue, state.FarFadeStart);
        Assert.Equal(float.MaxValue, state.FarFadeEnd);
    }
}

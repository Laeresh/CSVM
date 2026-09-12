using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <c>PufferState.StartAgeMin</c>/<c>StartAgeMax</c> (see <c>docs/org/puffer.md</c>): both
/// parsers must wire <c>START_AGE_RANGE</c>/<c>start_age_range</c> through, and an unauthored
/// state must keep both at 0 with <see cref="PufferState.HasStartAgeRange"/> false, the flag the
/// spawn paths gate their extra <c>Rand()</c> draw on, so the ~2,900 puffers that don't author the
/// key consume no extra draw.
/// </summary>
public class PufferStartAgeTests
{
    [Fact]
    public void ReaderParsesStartAgeRange()
    {
        // Mirrors zepskinfire.zrd.json's fire_at_zepskin3: START_AGE_RANGE (-1.0, 0.1).
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "test_puffer" },
                "NUMBER", new List<object?> { 5f },
                "START_AGE_RANGE", new List<object?> { -1f, 0.1f },
                "LIFETIME_RANGE", new List<object?> { 1f, 6f },
            },
        };

        var state = PufferState.FindInReader(reader, "test_puffer");

        Assert.NotNull(state);
        Assert.Equal(-1f, state!.StartAgeMin);
        Assert.Equal(0.1f, state.StartAgeMax);
        Assert.True(state.HasStartAgeRange);
    }

    [Fact]
    public void ReaderWithNoStartAgeRangeDefaultsToZeroAndUnauthored()
    {
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "plain_puffer" },
                "NUMBER", new List<object?> { 5f },
                "LIFETIME_RANGE", new List<object?> { 1f, 6f },
            },
        };

        var state = PufferState.FindInReader(reader, "plain_puffer");

        Assert.NotNull(state);
        Assert.Equal(0f, state!.StartAgeMin);
        Assert.Equal(0f, state.StartAgeMax);
        Assert.False(state.HasStartAgeRange);
    }

    [Fact]
    public void CompiledEventParsesStartAgeRange()
    {
        // Mirrors the compiled zep_skin_fire3-zepskinfire_3.json payload's start_age_range object.
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "fire_at_zepskin3",
            ["number"] = 5f,
            ["start_age_range"] = new Dictionary<string, object?> { ["min"] = -1f, ["max"] = 0.1f },
            ["lifetime_range"] = new Dictionary<string, object?> { ["min"] = 1f, ["max"] = 6f },
        });

        var state = PufferState.FromAnimEvent(d);

        Assert.Equal(-1f, state.StartAgeMin);
        Assert.Equal(0.1f, state.StartAgeMax);
        Assert.True(state.HasStartAgeRange);
    }

    [Fact]
    public void CompiledEventWithNoStartAgeRangeDefaultsToZeroAndUnauthored()
    {
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "steampuffer",
            ["number"] = 5f,
            ["lifetime_range"] = new Dictionary<string, object?> { ["min"] = 0.5f, ["max"] = 4.5f },
        });

        var state = PufferState.FromAnimEvent(d);

        Assert.Equal(0f, state.StartAgeMin);
        Assert.Equal(0f, state.StartAgeMax);
        Assert.False(state.HasStartAgeRange);
    }
}

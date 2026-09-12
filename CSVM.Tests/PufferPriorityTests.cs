using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The puffer's `PRIORITY` (see `docs/org/puffer.md`): both parsers must wire it through, and an
/// unauthored state must keep the puffer object's own ctor default of 0 (factor 1, i.e. no size
/// change). The size arithmetic itself, `1 + 0.02·PRIORITY` folded into `BaseSize` at spawn, is
/// asserted in the `puffer-priority-size` engine suite, which needs a live `Puffer`; these tests
/// cover only the two parsers.
/// </summary>
public class PufferPriorityTests
{
    [Fact]
    public void ReaderParsesPriority()
    {
        // C1 waterfalls.zrd.json's splash puffer, verbatim: PRIORITY [2].
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "splash_puffer" },
                "NUMBER", new List<object?> { 5f },
                "PRIORITY", new List<object?> { 2f },
            },
        };

        var state = PufferState.FindInReader(reader, "splash_puffer");

        Assert.NotNull(state);
        Assert.Equal(2f, state!.Priority);
    }

    [Fact]
    public void ReaderWithNoPriorityKeepsTheEngineCtorDefault()
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
        Assert.Equal(0f, state!.Priority);
    }

    [Fact]
    public void CompiledEventParsesPriority()
    {
        // C3's spew_puffer (waterfalls.zrd.json / spew-spew_water.json): PRIORITY 1.0.
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "spew_puffer",
            ["number"] = 5f,
            ["priority"] = 1f,
        });

        var state = PufferState.FromAnimEvent(d);

        Assert.Equal(1f, state.Priority);
    }

    [Fact]
    public void CompiledEventWithNullPriorityKeepsTheEngineCtorDefault()
    {
        // priority is present-and-null on the great majority of the install's compiled events.
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "steampuffer",
            ["number"] = 5f,
            ["priority"] = null,
        });

        var state = PufferState.FromAnimEvent(d);

        Assert.Equal(0f, state.Priority);
    }
}

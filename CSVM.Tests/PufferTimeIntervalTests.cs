using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <c>PufferState.TimeInterval</c>'s two unauthored answers, which are different numbers for
/// different reasons (see <c>docs/org/puffer.md</c>). A state that authors no interval emits at the
/// puffer constructor's own 1 s; a <c>DISTANCE_INTERVAL</c> state falls back to CSVM's synthetic
/// 0.1 s sputter while its host holds still, which is our invention and must not follow the
/// constructor. Both parsers have to keep them apart, and a compiled event's zero is the shape the
/// engine's own setter reads as "never authored".
/// </summary>
public class PufferTimeIntervalTests
{
    [Fact]
    public void ReaderTakesTheAuthoredTimeInterval()
    {
        var state = PufferState.FindInReader(Reader(
            "NAME", new List<object?> { "fierypuffer" },
            "NUMBER", new List<object?> { 18f },
            "TIME_INTERVAL", new List<object?> { 0.2f }), "fierypuffer");

        Assert.NotNull(state);
        Assert.Equal(0.2f, state!.TimeInterval);
    }

    [Fact]
    public void ReaderWithNoIntervalAtAllTakesTheConstructorsOneSecond()
    {
        var state = PufferState.FindInReader(Reader(
            "NAME", new List<object?> { "plain_puffer" },
            "NUMBER", new List<object?> { 1f }), "plain_puffer");

        Assert.NotNull(state);
        Assert.Equal(PufferState.TimeIntervalDefault, state!.TimeInterval);
        Assert.Equal(1f, state.TimeInterval);
    }

    [Fact]
    public void ReaderDistanceStateKeepsTheStillHostSputter()
    {
        // pufftrails.json's smokepuffer shape: DISTANCE_INTERVAL and no TIME_INTERVAL, which is
        // every one of the install's 146 reader blocks that omit the key.
        var state = PufferState.FindInReader(Reader(
            "NAME", new List<object?> { "smokepuffer" },
            "DISTANCE_INTERVAL", new List<object?> { 2f }), "smokepuffer");

        Assert.NotNull(state);
        Assert.Equal(2f, state!.DistanceInterval);
        Assert.Equal(PufferState.StillHostSputterInterval, state.TimeInterval);
        Assert.Equal(0.1f, state.TimeInterval);
    }

    [Fact]
    public void CompiledEventTakesAFlaggedTimeInterval()
    {
        var state = PufferState.FromAnimEvent(new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "fire_n_smoke",
            ["interval_garbage"] = new Dictionary<string, object?>
            {
                ["interval_type"] = "Time",
                ["interval_value"] = 0.1f,
                ["has_interval_value"] = true,
            },
        }));

        Assert.Equal(0.1f, state.TimeInterval);
        Assert.Equal(0f, state.DistanceInterval);
    }

    [Fact]
    public void CompiledEventWithAZeroIntervalTakesTheConstructorsOneSecond()
    {
        // truck1dust_puffer's shape: the compiled form carries a garbage 0 with both flags clear,
        // and the engine's setter refuses a zero, leaving the constructor's 1 s standing.
        var state = PufferState.FromAnimEvent(new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "truck1dust_puffer",
            ["interval_garbage"] = new Dictionary<string, object?>
            {
                ["interval_type"] = "Time",
                ["interval_value"] = 0f,
                ["has_interval_value"] = false,
            },
        }));

        Assert.Equal(1f, state.TimeInterval);
    }

    [Fact]
    public void CompiledDistanceEventKeepsTheStillHostSputter()
    {
        var state = PufferState.FromAnimEvent(new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "trailpuffer2",
            ["interval_garbage"] = new Dictionary<string, object?>
            {
                ["interval_type"] = "Distance",
                ["interval_value"] = 2f,
                ["has_interval_type"] = true,
            },
        }));

        Assert.Equal(2f, state.DistanceInterval);
        Assert.Equal(0.1f, state.TimeInterval);
    }

    [Fact]
    public void AReaderScopeDistanceEventKeepsItsMetresThroughTheNormalizer()
    {
        // The reader front-end's PUFFER_STATE normalizer dropped DISTANCE_INTERVAL, which left the
        // event with no interval at all; unauthored now means 1 s, so the metres have to survive.
        var defs = AnimDefs.LoadArchive(TestData.Fixture("zrdr"));
        var puffer = defs.Find(d => d.Name == "probe_tower")!
            .Sequences.Find(s => s.Name == "probe_spin")!
            .Events.Find(e => e.Kind == "PufferState")!.Data;

        var state = PufferState.FromAnimEvent(puffer);

        Assert.Equal(3f, state.DistanceInterval);
        Assert.Equal(PufferState.StillHostSputterInterval, state.TimeInterval);
    }

    private static List<object?> Reader(params object?[] body) =>
        new() { "PUFFER_STATE", new List<object?>(body) };
}

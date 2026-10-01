using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using Xunit;

namespace CSVM.Tests;

/// <summary>Which machine answers a <c>PLAYER_RANGE</c> gate. A gate reaching a <c>CALLBACK</c>
/// through its call closure is the host's, and every other gate is each machine's own. A deciding
/// end reports only the changes of its verdict.</summary>
public class RangeGateAuthorityTests
{
    [Fact]
    public void AGateWhoseCalleeRaisesACodeIsTheHosts()
    {
        var drop = Def("drop", Call("pilot"), Call("camera"));
        var pilot = Def("pilot", Event("ObjectActiveState"));
        var camera = Def("camera", Call("letterbox"));
        var letterbox = Def("letterbox", Callback(2));
        var gates = Bound(drop, pilot, camera, letterbox);

        Assert.True(gates.HostDecides(drop));
        Assert.True(gates.HostDecides(letterbox));
        Assert.False(gates.HostDecides(pilot));
    }

    [Fact]
    public void AWashThatRaisesNoCodeStaysEachMachines()
    {
        var wash = Def("he_ground_effect", Call("flash"), Event("Sound"));
        var flash = Def("flash", Event("FbfxColorFromTo"));
        var gates = Bound(wash, flash);

        Assert.False(gates.HostDecides(wash));
    }

    [Fact]
    public void ACallCycleEndsTheWalk()
    {
        var ping = Def("ping", Call("pong"));
        var pong = Def("pong", Call("ping"));
        var gates = Bound(ping, pong);

        Assert.False(gates.HostDecides(ping));
    }

    [Fact]
    public void ACodeInTheResetStateDoesNotCount()
    {
        var def = Def("reset_only");
        def.ResetState = Seq(Callback(11));
        var gates = Bound(def);

        Assert.False(gates.HostDecides(def));
    }

    [Fact]
    public void ADecidingEndReportsOnlyChanges()
    {
        var gates = Bound();
        var told = new List<(string Gate, bool Passed)>();
        gates.Decided = (gate, passed) => told.Add((gate, passed));

        Assert.False(gates.Decide("drop/marker/4096", false));
        Assert.True(gates.Decide("drop/marker/4096", true));
        Assert.True(gates.Decide("drop/marker/4096", true));
        Assert.False(gates.Decide("drop/marker/4096", false));
        Assert.True(gates.Decide("other/marker/4096", true));

        Assert.Equal(
            new[] { ("drop/marker/4096", true), ("drop/marker/4096", false), ("other/marker/4096", true) },
            told);
    }

    [Fact]
    public void TheGateNameCarriesDefinitionAnchorAndRadius()
    {
        var def = Def("blacke_drop");

        Assert.Equal("blacke_drop/blacke_marker/4096", RangeGateAuthority.GateName(def, "blacke_marker", 4096f));
    }

    private static RangeGateAuthority Bound(params AnimDefinition[] defs)
    {
        var byName = defs.GroupBy(d => d.AnimName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AnimDefinition>)g.ToList(), StringComparer.OrdinalIgnoreCase);
        var gates = new RangeGateAuthority();
        gates.Bind(name => byName.TryGetValue(name, out var list) ? list : Array.Empty<AnimDefinition>());
        return gates;
    }

    private static AnimDefinition Def(string anim, params AnimEvent[] events)
    {
        var def = new AnimDefinition { AnimName = anim, Name = anim };
        def.Sequences.Add(Seq(events));
        return def;
    }

    private static AnimSequence Seq(params AnimEvent[] events)
    {
        var seq = new AnimSequence();
        seq.Events.AddRange(events);
        return seq;
    }

    private static AnimEvent Call(string callee) =>
        new() { Kind = "CallAnimation", Data = new AnimData(new Dictionary<string, object?> { ["name"] = callee }) };

    private static AnimEvent Callback(int code) =>
        new() { Kind = "Callback", Data = new AnimData(new Dictionary<string, object?> { ["value"] = (double)code }) };

    private static AnimEvent Event(string kind) => new() { Kind = kind };
}

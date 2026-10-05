using System.Linq;
using CSVM.Flight.Ai;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A bot's rearm run off-engine. <see cref="AiRearmOrder"/> starts on low guns or a badly damaged
/// hull and lines up on a base's open side. It flies the final leg through the node, and hands back
/// once the base has restored it and it has flown clear.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class AiRearmOrderTests
{
    private static readonly Vector3 Node = new(0f, 100f, 0f);

    [Fact]
    public void ARunStartsOnLowGunsOrABadlyDamagedHullAndNotAbove()
    {
        Assert.True(AiRearmOrder.Wants(AiRearmOrder.LowAmmoShare, 1f));
        Assert.True(AiRearmOrder.Wants(1f, AiRearmOrder.DamagedHullShare));
        Assert.True(AiRearmOrder.Wants(0f, 0f));

        // ABLE-TO-FAIL CONTROL: just above both thresholds nothing calls for a run.
        Assert.False(AiRearmOrder.Wants(AiRearmOrder.LowAmmoShare + 0.01f, AiRearmOrder.DamagedHullShare + 0.01f));
    }

    [Fact]
    public void TheLoadShareSumsEverySlotAndCannotRunLowWithoutCapacityOrWithInfiniteAmmo()
    {
        var slots = new IAmmoSlot[] { new Slot(50, 200), new Slot(0, 200) };
        Assert.Equal(0.125f, AiRearmOrder.LoadShare(slots, infinite: false), 4);
        Assert.Equal(1f, AiRearmOrder.LoadShare(slots, infinite: true));
        Assert.Equal(1f, AiRearmOrder.LoadShare(System.Array.Empty<IAmmoSlot>(), infinite: false));
    }

    [Fact]
    public void TheOpenBearingIsTheMiddleOfTheBayNearestThePilotWhoseLegIsClear()
    {
        // The MP1 bay as measured: open near the node from 135 to 225 degrees. The climb to the gate
        // is clear at 135 and from 180 to 225. A pilot on the far side, or at 150, comes in along 180.
        var bay = Clear(9, 10, 11, 12, 13, 14, 15);
        var leg = Clear(9, 12, 13, 14, 15);
        Assert.Equal(180f, Heading(AiRearmOrder.OpenBearing(bay, leg, AiRearmOrder.BearingDir(1, 24))), 3);
        Assert.Equal(180f, Heading(AiRearmOrder.OpenBearing(bay, leg, AiRearmOrder.BearingDir(10, 24))), 3);

        // With the middle's own leg blocked, the clear bearing nearest the pilot stands.
        Assert.Equal(135f, Heading(AiRearmOrder.OpenBearing(bay, Clear(9, 15), AiRearmOrder.BearingDir(10, 24))), 3);

        // An arc of two centres between them, and an arc across north wraps.
        Assert.Equal(172.5f, Heading(AiRearmOrder.OpenBearing(Clear(11, 12), Clear(11, 12), AiRearmOrder.BearingDir(11, 24))), 3);
        Assert.Equal(0f, Heading(AiRearmOrder.OpenBearing(Clear(23, 0, 1), Clear(23, 0, 1), AiRearmOrder.BearingDir(1, 24))), 3);

        // Of two arcs, the one nearer the pilot.
        var two = Clear(0, 1, 2, 12);
        Assert.Equal(180f, Heading(AiRearmOrder.OpenBearing(two, two, AiRearmOrder.BearingDir(10, 24))), 3);

        // ABLE-TO-FAIL CONTROL: with every bearing clear, or none, the pilot's own bearing stands.
        var own = AiRearmOrder.BearingDir(7, 24);
        var all = Enumerable.Repeat(true, 24).ToArray();
        Assert.Equal(Heading(own), Heading(AiRearmOrder.OpenBearing(all, all, own)), 3);
        Assert.Equal(Heading(own), Heading(AiRearmOrder.OpenBearing(new bool[24], new bool[24], own)), 3);
    }

    [Fact]
    public void ARunSearchesTheOpenSideWithTheWorldProbe()
    {
        // Every bearing but due south (+Z, heading 180) is walled off.
        var order = new AiRearmOrder((from, to) => (to - from).Normalized().Z < 0.99f);
        order.Update(new Vector3(800f, 300f, 800f), Vector3.Zero, 0f, 1f, Node, restored: false);

        Assert.Equal(AiRearmLeg.Gate, order.Leg);
        Assert.Equal(0f, order.Approach.DistanceTo(Vector3.Back), 3);
    }

    [Fact]
    public void ARunFliesTheGateThenTheLevelFinalLegThroughTheNodeAndHandsBackClear()
    {
        var order = new AiRearmOrder();
        var inbound = new Vector3(0f, 0f, -90f);
        order.Update(new Vector3(0f, 300f, 4000f), inbound, 0f, 1f, Node, restored: false);
        Assert.Equal(AiRearmLeg.Gate, order.Leg);
        Assert.Contains("guns 0.00", order.Reason, System.StringComparison.Ordinal);
        var gate = new Vector3(0f, Node.Y + AiRearmOrder.GateAboveM, AiRearmOrder.FinalLegM);
        Assert.Equal(gate, order.Aim(new Vector3(0f, 300f, 4000f)));

        // ABLE-TO-FAIL CONTROL: crossing the corridor outbound, it keeps flying to the gate.
        var lined = gate + new Vector3(0f, 0f, 100f);
        order.Update(lined, -inbound, 0f, 1f, Node, restored: false);
        Assert.Equal(AiRearmLeg.Gate, order.Leg);
        order.Update(lined, inbound, 0f, 1f, Node, restored: false);
        Assert.Equal(AiRearmLeg.Final, order.Leg);

        // The aim leads by LeadM and descends along the leg to the node's height.
        var aim = order.Aim(lined);
        Assert.Equal(lined.Z - AiRearmOrder.LeadM, aim.Z, 3);
        Assert.InRange(aim.Y, Node.Y, gate.Y - 1f);

        // The last stretch is level at the node's height, and the aim runs on past the node.
        Assert.Equal(new Vector3(0f, 100f, 500f - AiRearmOrder.LeadM), order.Aim(new Vector3(0f, 100f, 500f)));
        Assert.Equal(new Vector3(0f, 100f, 100f - AiRearmOrder.LeadM), order.Aim(new Vector3(0f, 100f, 100f)));

        order.Update(new Vector3(0f, 100f, 20f), Vector3.Zero, 0f, 1f, Node, restored: true);
        Assert.Equal(AiRearmLeg.Clear, order.Leg);
        Assert.Equal(100f, order.Aim(new Vector3(0f, 100f, -10f)).Y);
        Assert.Equal(100f + AiRearmOrder.ClearClimbM, order.Aim(new Vector3(0f, 100f, -100f)).Y);

        // Restored, its supplies are full: the run ends only once it is clear of the base.
        order.Update(new Vector3(0f, 100f, -140f), Vector3.Zero, 1f, 1f, Node, restored: false);
        Assert.Equal(AiRearmLeg.Clear, order.Leg);
        order.Update(new Vector3(0f, 100f, -151f), Vector3.Zero, 1f, 1f, Node, restored: false);
        Assert.Equal(AiRearmLeg.None, order.Leg);
        Assert.False(order.Flying);
    }

    [Fact]
    public void AMissedPassPlansAgainAndALostBaseEndsTheRun()
    {
        var order = new AiRearmOrder();
        order.Update(new Vector3(0f, 100f, 400f), Vector3.Zero, 0f, 1f, Node, restored: false);
        Assert.Equal(AiRearmLeg.Final, order.Leg);

        // Past the node by more than the miss distance, unrestored: it turns back from this side.
        order.Update(new Vector3(0f, 100f, -200f), Vector3.Zero, 0f, 1f, Node, restored: false);
        Assert.Equal(AiRearmLeg.Final, order.Leg);
        Assert.Equal(0f, order.Approach.DistanceTo(Vector3.Forward), 3);

        // A base that moved is planned afresh; one that offers nothing ends the run.
        var moved = Node + new Vector3(40f, 0f, 0f);
        order.Update(new Vector3(0f, 100f, -200f), Vector3.Zero, 0f, 1f, moved, restored: false);
        Assert.Equal(moved, order.Base);
        order.Update(new Vector3(0f, 100f, -200f), Vector3.Zero, 0f, 1f, null, restored: false);
        Assert.Equal(AiRearmLeg.None, order.Leg);
    }

    [Fact]
    public void NoBaseOrFullSuppliesStartNothingAndClearDropsARun()
    {
        var order = new AiRearmOrder();
        order.Update(new Vector3(0f, 300f, 3000f), Vector3.Zero, 0f, 0f, null, restored: false);
        Assert.Equal(AiRearmLeg.None, order.Leg);

        // ABLE-TO-FAIL CONTROL: above both thresholds, with a base standing, there is no run.
        order.Update(new Vector3(0f, 300f, 3000f), Vector3.Zero, 0.5f, 0.5f, Node, restored: false);
        Assert.Equal(AiRearmLeg.None, order.Leg);

        order.Update(new Vector3(0f, 300f, 3000f), Vector3.Zero, 0.5f, 0.2f, Node, restored: false);
        Assert.True(order.Flying);
        order.Clear();
        Assert.False(order.Flying);
        Assert.Equal(string.Empty, order.Reason);
    }

    [Fact]
    public void TheNearestServingBaseSkipsOtherTeamsAndBasesThatOfferNothing()
    {
        var own = new RearmBases(RearmRule.OwnTeam, RearmBases.InitialRadiusSquared);
        var far = new Vector3(0f, 100f, 5000f);
        var near = new Vector3(0f, 100f, 100f);
        var bases = new[] { new RearmBase(1, far), new RearmBase(2, near), new RearmBase(1, null) };

        Assert.Equal(far, own.NearestServing(Vector3.Zero, 1, bases));
        Assert.Null(own.NearestServing(Vector3.Zero, 3, bases));

        // ABLE-TO-FAIL CONTROL: in a Deathmatch every base serves, so the nearer one wins.
        var any = new RearmBases(RearmRule.AnyBase, RearmBases.InitialRadiusSquared);
        Assert.Equal(near, any.NearestServing(Vector3.Zero, 1, bases));
    }

    // A bearing's heading in degrees, 0 to 360, so due south reads 180 whichever way it rounds.
    private static float Heading(Vector3 dir) => Mathf.PosMod(AiPilot.HeadingDegOf(dir), 360f);

    private static bool[] Clear(params int[] open)
    {
        var clear = new bool[AiRearmOrder.Bearings];
        foreach (int i in open)
        {
            clear[i] = true;
        }

        return clear;
    }

    private sealed class Slot : IAmmoSlot
    {
        public Slot(int ammo, int capacity)
        {
            Ammo = ammo;
            Capacity = capacity;
        }

        public int Ammo { get; set; }

        public int Capacity { get; }
    }
}

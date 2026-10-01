using System;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A custom plane over the wire. Its build and the host's plane rules round-trip, and the rules
/// refuse what the original's Ready check refuses. A saved plane survives the trip and comes back.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class NetPlaneMessagesTests
{
    [Fact]
    public void APlaneBuildRoundTripsEveryFieldAndAStockPickReadsAsNone()
    {
        Assert.Equal(48, PlaneBuildMessage.Size);
        Span<byte> buffer = stackalloc byte[PlaneBuildMessage.Size];
        var build = Busy();
        foreach (var sent in new[]
        {
            new PlaneBuildMessage(PlaneBuildMessage.Mine, build),
            new PlaneBuildMessage(2, build),
            new PlaneBuildMessage(1, null),
        })
        {
            Assert.Equal(PlaneBuildMessage.Size, sent.Write(buffer));
            Assert.True(PlaneBuildMessage.TryRead(buffer, out var got));
            Assert.Equal(sent, got);
        }

        Assert.Equal(0x5D, (int)NetMessageType.PlaneBuild);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.PlaneBuild));

        // ABLE-TO-FAIL CONTROL: one changed field is another build.
        var other = build.Copy();
        other.Decals[2] = 9;
        Assert.NotEqual(build, other);
        Assert.Equal(build, build.Copy());
    }

    [Fact]
    public void ThePlaneRulesRoundTripBothTicksAndTheWholeList()
    {
        Assert.Equal(12, LobbyPlaneRulesMessage.Size);
        Span<byte> buffer = stackalloc byte[LobbyPlaneRulesMessage.Size];
        var rules = new NetPlaneRules(true, true, 0)
            .With(0, true).With(NetPlaneRules.GunFlag + 4, true).With(NetPlaneRules.NitroFlag, true);
        var sent = new LobbyPlaneRulesMessage(7, rules);
        Assert.Equal(LobbyPlaneRulesMessage.Size, sent.Write(buffer));
        Assert.True(LobbyPlaneRulesMessage.TryRead(buffer, out var got));
        Assert.Equal(sent, got);
        Assert.Equal(0x5E, (int)NetMessageType.LobbyPlaneRules);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.LobbyPlaneRules));

        // ABLE-TO-FAIL CONTROL: a flag outside the 34 is not kept.
        Assert.Equal(rules, rules.With(NetPlaneRules.Flags, true));
        Assert.False(LobbyPlaneRulesMessage.TryRead(buffer[..8], out _));
    }

    [Fact]
    public void TheOutlawListPacksLowestFlagFirstInFiveBytes()
    {
        var rules = new NetPlaneRules(false, true, 0).With(0, true).With(9, true).With(NetPlaneRules.NitroFlag, true);
        Span<byte> list = stackalloc byte[NetPlaneRules.ListBytes];
        rules.PackList(list);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x00, 0x00, 0x02 }, list.ToArray());
        Assert.Equal(rules.Outlawed, NetPlaneRules.UnpackList(list));
    }

    [Fact]
    public void TheRulesRefuseInTheOriginalsOrder()
    {
        var none = default(NetPlaneRules);
        var custom = Busy();
        Assert.Equal(PlaneRefusal.CustomBarred, none.Refuses(custom.Airframe, custom));
        Assert.Equal(PlaneRefusal.None, none.Refuses(3, null));

        var allowed = none with { AllowCustom = true };
        Assert.Equal(PlaneRefusal.None, allowed.Refuses(custom.Airframe, custom));

        // A list with Outlaw Components clear refuses nothing.
        var listed = allowed.With(NetPlaneRules.NitroFlag, true).With(custom.Airframe, true);
        Assert.Equal(PlaneRefusal.None, listed.Refuses(custom.Airframe, custom));

        var outlawing = listed with { Outlawing = true };
        Assert.Equal(PlaneRefusal.Engine, outlawing.Refuses(custom.Airframe, custom));
        Assert.Equal(PlaneRefusal.Airframe, outlawing.With(NetPlaneRules.NitroFlag, false).Refuses(custom.Airframe, custom));

        var guns = allowed with { Outlawing = true };
        Assert.Equal(PlaneRefusal.Gun, guns.With(NetPlaneRules.GunFlag + custom.Guns[0], true).Refuses(custom.Airframe, custom));

        // ABLE-TO-FAIL CONTROL: a calibre the plane leaves empty is no refusal.
        Assert.Equal(PlaneRefusal.None, guns.With(NetPlaneRules.GunFlag + 4, true).Refuses(custom.Airframe, custom));
    }

    [Fact]
    public void AStockPickIsJudgedOnItsTemplatesGunsAndAMissingEngineIsRefused()
    {
        var rules = new NetPlaneRules(false, true, 0).With(NetPlaneRules.GunFlag + 4, true);

        // Airframe 6's template mounts a calibre 4 gun, airframe 0's does not.
        Assert.Equal(PlaneRefusal.Gun, rules.Refuses(6, null));
        Assert.Equal(PlaneRefusal.None, rules.Refuses(0, null));
        Assert.Equal(new byte[] { 4, 0, 5, 0 }, NetPlaneBuild.Stock(6).Guns);

        var bare = new NetPlaneBuild { Airframe = 2 };
        Assert.Equal(PlaneRefusal.Engine, (rules with { AllowCustom = true }).Refuses(2, bare));
    }

    [Fact]
    public void OneOutlawedAmmoOrRocketSetsEveryGunOrPylonToNone()
    {
        var fit = CoopFit.Of(new[] { 1, 2, -1, 4 }, new[] { 3, 0, 12 });
        var rules = new NetPlaneRules(false, true, 0).With(NetPlaneRules.AmmoFlag + 0, true).With(NetPlaneRules.RocketFlag + 1, true);
        Assert.True(rules.AmmoOutlawed(fit));
        Assert.True(rules.RocketsOutlawed(fit));

        var flown = rules.Enforce(fit);

        // Slot 2 left unset flies slug, and cell 1 left unset flies high explosive, row 1.
        Assert.Equal(new[] { 4, 4, 4, 4 }, Enumerable.Range(0, CoopFit.GunSlots).Select(flown.AmmoAt));
        Assert.Equal(Enumerable.Repeat(12, CoopFit.Cells), Enumerable.Range(0, CoopFit.Cells).Select(flown.OrdnanceAt));

        // ABLE-TO-FAIL CONTROL: an outlawed ammo and rocket the fit does not use change nothing, nor
        // does a list with Outlaw Components clear.
        var unused = new NetPlaneRules(false, true, 0).With(NetPlaneRules.AmmoFlag + 3, true).With(NetPlaneRules.RocketFlag + 5, true);
        Assert.False(unused.AmmoOutlawed(fit));
        Assert.Equal(fit, unused.Enforce(fit));
        Assert.Equal(fit, (rules with { Outlawing = false }).Enforce(fit));
        var allAmmo = new NetPlaneRules(false, true, 0).With(NetPlaneRules.AllAmmoFlag, true).Enforce(fit);
        Assert.Equal(new[] { 4, 4, 4, 4 }, Enumerable.Range(0, CoopFit.GunSlots).Select(allAmmo.AmmoAt));
        Assert.Equal(3, allAmmo.OrdnanceAt(0));
    }

    [Fact]
    public void ASavedPlaneCrossesTheWireAndComesBackWhole()
    {
        var def = new CustomPlaneDef
        {
            Name = "Loopback Bee",
            Airframe = 1,
            Engine = 4,
            ArmourNose = 3,
            ArmourTail = 2,
            ArmourLeftWing = 4,
            ArmourRightWing = 1,
            LeftHardpoints = 2,
            RightHardpoints = 3,
            PaintPattern = 5,
            NoseDecal = 7,
            TailDecal = CustomPlaneDef.KeepDecal,
            WingDecal = 3,
        };
        def.Guns[0] = new GunChoice(2, true);
        def.Guns[1] = new GunChoice(null, false);
        def.Guns[2] = new GunChoice(1, false);
        def.Guns[3] = new GunChoice(3, true);
        def.PaintColours[1] = 12;
        def.PaintShades[2] = 3;

        var build = CustomPlaneWire.Build(def)!;
        Assert.Equal(NetPlaneBuild.EmptyGun, build.Guns[1]);
        Assert.Equal(0b1001, build.Twins);
        var back = CustomPlaneWire.Def(build)!;

        Assert.Equal(build, CustomPlaneWire.Build(back));
        Assert.Equal(def.Name, back.Name);
        Assert.Equal(def.Engine, back.Engine);
        Assert.Equal(def.ArmourLeftWing, back.ArmourLeftWing);
        Assert.Equal(def.RightHardpoints, back.RightHardpoints);
        Assert.Equal(def.Guns, back.Guns);
        Assert.Equal(def.PaintColours, back.PaintColours);
        Assert.Equal(def.PaintShades, back.PaintShades);
        Assert.Equal(def.TailDecal, back.TailDecal);

        // ABLE-TO-FAIL CONTROL: no plane is a stock seat both ways.
        Assert.Null(CustomPlaneWire.Build(null));
        Assert.Null(CustomPlaneWire.Def(null));
    }

    // A build with every field away from its default.
    private static NetPlaneBuild Busy()
    {
        var build = new NetPlaneBuild
        {
            Name = "Busy Bee",
            Airframe = 4,
            Engine = 3,
            LeftHardpoints = 4,
            RightHardpoints = 1,
            Twins = 0b0101,
            PaintPattern = 11,
        };
        build.Armour[0] = 12;
        build.Armour[3] = 5;
        build.Guns[0] = 2;
        build.Guns[1] = 1;
        build.Guns[2] = 3;
        build.Colours[0] = 25;
        build.Shades[1] = 4;
        build.Decals[0] = 49;
        build.Decals[1] = 0;
        return build;
    }
}

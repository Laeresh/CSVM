using System;
using System.Globalization;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Net;
using CSVM.Session.Launch;
using CSVM.UI.Hangar;
using CSVM.UI.Menu;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>Custom planes in a lobby Dogfight over loopback doors. The host's Allow Custom Planes
/// and Outlaw Components gate each pick at Ready. An admitted custom plane is built the same on
/// every machine. The flight rig is <see cref="NetCombatSuites"/>'s, one session per peer.</summary>
internal static class NetCustomPlaneSuites
{
    private const ulong HostSeed = 0xC0FFEE42UL;

    // Steps enough for a lobby word to cross a perfect loopback link and be answered.
    private const int SettleSteps = 12;

    // Steps the host's session and both doors run before the guest's door hears the opener.
    private const int OpenerSteps = 120;

    [Suite("net-custom-planes",
        "a lobby Dogfight host and guest over loopback doors: Allow Custom Planes and Outlaw "
        + "Components refuse a guest's custom plane at Ready as the original does, an outlawed "
        + "ammunition or rocket refuses Ready once and resets every gun or pylon to none, and an "
        + "admitted custom plane on either seat is built with the "
        + "same armour, engine, pylons, guns and paint on the host and the guest")]
    internal static void CustomPlanesCross(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _);
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            Gates(ctx);
            Flight(ctx, spec);
        }
        finally
        {
            ambient.Restore();
        }
    }

    // The guest's custom plane: a nitro engine, armour on every panel, two pylons a wing, a twin
    // gun and its own paint. Every field sits inside the decoded ranges, so it round-trips whole.
    internal static CustomPlaneDef GuestPlane()
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
            RightHardpoints = 2,
            PaintPattern = 5,
            NoseDecal = 7,
            TailDecal = 12,
            WingDecal = 3,
        };
        def.Guns[0] = new GunChoice(2, true);
        def.Guns[1] = new GunChoice(1, false);
        def.Guns[2] = new GunChoice(null, false);
        def.Guns[3] = new GunChoice(3, false);
        for (int slot = 0; slot < def.PaintColours.Length; slot++)
        {
            def.PaintColours[slot] = 4 + (5 * slot);
            def.PaintShades[slot] = slot + 1;
        }

        return def;
    }

    // The host's custom plane, on another airframe with a plain engine and one-sided pylons.
    internal static CustomPlaneDef HostPlane()
    {
        var def = new CustomPlaneDef
        {
            Name = "Host Wasp",
            Airframe = 6,
            Engine = 2,
            ArmourNose = 1,
            ArmourTail = 1,
            LeftHardpoints = 3,
            RightHardpoints = 1,
            PaintPattern = 9,
            NoseDecal = 20,
        };
        def.Guns[0] = new GunChoice(4, false);
        def.Guns[1] = new GunChoice(0, true);
        def.Guns[2] = new GunChoice(null, false);
        def.Guns[3] = new GunChoice(null, false);
        return def;
    }

    // The rules alone, over two doors: each option refuses what the original's Ready check refuses.
    private static void Gates(TestContext ctx)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(4201));
        var hostDoor = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[1]);
        var guestDoor = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[1]);
        try
        {
            if (Join(ctx, "gates", hostDoor, guestDoor) is not var (host, guest))
            {
                return;
            }

            var build = CustomPlaneWire.Build(GuestPlane())!;
            guest.Pick(1, default);
            ctx.Check(guest.Refusal == PlaneRefusal.None && guest.SetReady(true),
                $"ABLE-TO-FAIL CONTROL: [gates] a stock pick is Ready in a new lobby ({guest.Refusal})");
            guest.PickCustom(build, default);
            StepDoors(SettleSteps, hostDoor, guestDoor);
            ctx.Check(!guest.Rules.AllowCustom && guest.Refusal == PlaneRefusal.CustomBarred && !guest.SetReady(true),
                $"[gates] a new lobby allows no custom planes, so the guest's custom pick is refused at Ready ({guest.Refusal})");
            StepDoors(SettleSteps, hostDoor, guestDoor);
            ctx.Check(host.Players.Count == 2 && !host.Players[1].Ready,
                $"[gates] and the host's row for the guest is not Ready ({Rows(host)})");

            host.SetAllowCustomPlanes(true);
            StepDoors(SettleSteps, hostDoor, guestDoor);
            bool readied = guest.SetReady(true);
            StepDoors(SettleSteps, hostDoor, guestDoor);
            ctx.Check(guest.Rules.AllowCustom && readied && host.Players[1].Ready,
                $"[gates] with Allow Custom Planes checked the guest's custom plane is Ready on both ends ({guest.Refusal}, {Rows(host)})");

            host.SetOutlawComponents(true);
            Outlaw(ctx, hostDoor, guestDoor, NetPlaneRules.AirframeFlag + build.Airframe, PlaneRefusal.Airframe, "its airframe");
            Outlaw(ctx, hostDoor, guestDoor, NetPlaneRules.NitroFlag, PlaneRefusal.Engine, "nitro-boosted engines");
            Outlaw(ctx, hostDoor, guestDoor, NetPlaneRules.GunFlag + build.Guns[3], PlaneRefusal.Gun, "one of its gun calibres");
            Outlaw(ctx, hostDoor, guestDoor, NetPlaneRules.GunFlag + 4, PlaneRefusal.None, "a calibre it does not mount");

            host.SetOutlawed(NetPlaneRules.AirframeFlag + build.Airframe, true);
            host.SetOutlawComponents(false);
            StepDoors(SettleSteps, hostDoor, guestDoor);
            ctx.Check(guest.Refusal == PlaneRefusal.None,
                $"[gates] an outlaw list counts only while Outlaw Components is checked ({guest.Refusal})");
            host.SetOutlawed(NetPlaneRules.AirframeFlag + build.Airframe, false);

            var fit = CoopFit.Of(new[] { 1, 2, 3, 0 }, new[] { 3, 0 });
            guest.Refit(fit);
            host.SetOutlawComponents(true);
            host.SetOutlawed(NetPlaneRules.AmmoFlag + 2, true);
            host.SetOutlawed(NetPlaneRules.RocketFlag + 1, true);
            StepDoors(SettleSteps, hostDoor, guestDoor);
            var flown = guest.LaunchFit;
            ctx.Check(flown.AmmoAt(0) == NetPlaneRules.NoAmmo && flown.AmmoAt(2) == NetPlaneRules.NoAmmo
                      && flown.OrdnanceAt(0) == NetPlaneRules.NoRocket + 1,
                $"[gates] one outlawed ammunition and an unpicked pylon's outlawed high explosive fly every gun and pylon as none ({Fit(flown)})");
            bool first = guest.SetReady(true);
            string why = string.Join("+", guest.ReadyRefusals);
            ctx.Check(!first && why == "Ammo+Rockets" && guest.SetReady(true),
                $"[gates] and Ready is refused once for both, then takes on the reset fit ({why})");
        }
        finally
        {
            guestDoor.Discard();
            hostDoor.Discard();
        }
    }

    // One outlaw flag set, the guest's refusal read, and the flag cleared again.
    private static void Outlaw(TestContext ctx, NetPlayFeature hostDoor, NetPlayFeature guestDoor, int flag,
        PlaneRefusal expected, string what)
    {
        var host = hostDoor.Dogfight!;
        var guest = guestDoor.Dogfight!;
        host.SetOutlawed(flag, true);
        StepDoors(SettleSteps, hostDoor, guestDoor);
        var refusal = guest.Refusal;
        bool readied = guest.SetReady(true);
        StepDoors(SettleSteps, hostDoor, guestDoor);
        bool hostReady = host.Players[1].Ready;
        ctx.Check(refusal == expected && readied == (expected == PlaneRefusal.None) && hostReady == readied,
            $"[gates] outlawing {what} reads {expected} at the guest's Ready, and the host agrees ({refusal}, Ready {readied}/{hostReady})");
        host.SetOutlawed(flag, false);
        StepDoors(SettleSteps, hostDoor, guestDoor);
    }

    // Both seats flying custom planes, launched through both doors as the launcher launches them.
    private static void Flight(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(4202));
        var hostDoor = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[1]);
        var guestDoor = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[1]);
        NetCombatSuites.Ends? hostEnd = null;
        NetCombatSuites.Ends? guestEnd = null;
        try
        {
            if (Join(ctx, "flight", hostDoor, guestDoor) is not var (host, guest))
            {
                return;
            }

            var guestPlane = GuestPlane();
            var hostPlane = HostPlane();
            host.SetAllowCustomPlanes(true);
            StepDoors(SettleSteps, hostDoor, guestDoor);
            guest.PickCustom(CustomPlaneWire.Build(guestPlane)!, default);
            ctx.Check(guest.SetReady(true) && host.SetReady(true), $"[flight] both ends mark Ready on custom planes ({guest.Refusal})");
            StepDoors(SettleSteps, hostDoor, guestDoor);
            ctx.Check(host.CanLaunch, $"[flight] and the host may launch ({Rows(host)})");

            var rules = host.Rules;
            var hostLaunch = hostDoor.BuildLaunch();
            if (hostLaunch == null)
            {
                ctx.Check(false, $"[flight] the host's door hands its wire out ({hostDoor.Stage})");
                return;
            }

            // The launcher's host half: the field, each seat's fit and plane, then the opener.
            var planes = new[] { PlanePickerRoster.AirframeNode(hostPlane.Airframe) };
            var customs = new CustomPlaneDef?[] { hostPlane };
            var (roster, seatFits) = Launcher.VersusLaunchField(hostLaunch.Transport, planes, new LoadoutChoice?[] { null },
                StockLoadouts.Load(), rules);
            var builds = Launcher.SeatBuildsFor(roster, customs, hostLaunch.Transport, rules);
            hostDoor.TellSeatFits(seatFits);
            hostDoor.TellSeatBuilds(builds);
            ctx.Check(roster.Length == 2 && builds[1] != null && builds[1]!.Equals(guest.Build),
                $"[flight] the host's field takes the guest's custom plane off its pick ({builds[1]?.Name ?? "stock"})");
            hostEnd = NetCombatSuites.Ends.Open(ctx, spec.WithSeatedAircraft(planes[0], hostPlane, null),
                hostLaunch.Transport, isHost: true, HostSeed, roster, PlanePickerRoster.StockAirframes,
                seatBuild: s => Launcher.SeatBuildFor(s, builds, null));
            for (int i = 0; i < OpenerSteps && !guestDoor.DogfightLaunchDue; i++)
            {
                hostEnd.Session._PhysicsProcess(GameClock.FixedDt);
                StepDoors(1, hostDoor, guestDoor);
            }

            var guestLaunch = guestDoor.DogfightLaunchDue ? guestDoor.BuildLaunch() : null;
            if (guestLaunch == null)
            {
                ctx.Check(false, $"[flight] the guest's door hears the host's opener");
                return;
            }

            // The guest's own seat flies its pick read back off the wire, as its lobby screen's exit
            // hands it over.
            var own = CustomPlaneWire.Def(guest.Build);
            guestEnd = NetCombatSuites.Ends.Open(ctx,
                spec.WithSeatedAircraft(PlanePickerRoster.AirframeNode(guest.Airframe), own, null),
                guestLaunch.Transport, isHost: false, HostSeed + 1, null, PlanePickerRoster.StockAirframes,
                seatBuild: s => Launcher.SeatBuildFor(s, null, guestDoor));
            ctx.Check(hostEnd.Built && guestEnd.Built, $"[flight] both sessions build ({hostEnd.Built}, {guestEnd.Built})");
            if (!hostEnd.Built || !guestEnd.Built)
            {
                return;
            }

            NetStartSuites.UntilStarted(hostEnd.Session, guestEnd.Session);
            string guestOwn = Rig(guestEnd.Session, 1);
            string guestOnHost = Rig(hostEnd.Session, 1);
            string hostOwn = Rig(hostEnd.Session, 0);
            string hostOnGuest = Rig(guestEnd.Session, 0);
            ctx.Note($"guest seat: {guestOwn}");
            ctx.Note($"host seat: {hostOwn}");
            ctx.Check(guestOwn.Contains("nitro=True", StringComparison.Ordinal) && guestOwn.Contains("pylons=4", StringComparison.Ordinal)
                      && guestOwn.Contains("decals=7/12/3", StringComparison.Ordinal),
                $"ABLE-TO-FAIL CONTROL: the guest's own seat flies its custom plane, nitro, four pylons and its decals");
            ctx.Check(guestOnHost == guestOwn, $"[flight] the host builds the guest's custom plane as the guest does ({guestOnHost})");
            ctx.Check(hostOwn.Contains("pylons=4", StringComparison.Ordinal) && hostOwn.Contains("decals=20/", StringComparison.Ordinal),
                $"ABLE-TO-FAIL CONTROL: the host's own seat flies its custom plane");
            ctx.Check(hostOnGuest == hostOwn, $"[flight] the guest builds the host's custom plane as the host does ({hostOnGuest})");
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            guestDoor.Discard();
            hostDoor.Discard();
        }
    }

    // A host's Dogfight lobby and a guest in it, each with a lobby screen standing on it.
    private static (DogfightLobby Host, DogfightLobby Guest)? Join(TestContext ctx, string what, NetPlayFeature hostDoor,
        NetPlayFeature guestDoor)
    {
        hostDoor.OpenDogfightHost(1);
        guestDoor.OpenJoin();
        StepDoors(SettleSteps, hostDoor, guestDoor);
        if (hostDoor.Dogfight is not { } host || guestDoor.Dogfight is not { } guest || !guestDoor.IsDogfightGuest)
        {
            ctx.Check(false, $"[{what}] the guest joins the host's Dogfight ({guestDoor.Stage})");
            return null;
        }

        host.Show();
        guest.Show();
        StepDoors(SettleSteps, hostDoor, guestDoor);
        return (host, guest);
    }

    // What a seat's aeroplane is built from on one machine: its damage parts, engine, weapons and
    // paint. Two machines building the same plane read the same line.
    private static string Rig(GameSession session, int seat)
    {
        if (seat >= session.SeatRigs.Count || session.SeatRigs[seat].Controller is not { } plane)
        {
            return "no rig";
        }

        var inv = CultureInfo.InvariantCulture;
        string parts = plane.Damage is { } damage
            ? string.Join(",", damage.Parts.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => string.Format(inv, "{0}={1:0.##}/{2:0.##}", p.Key, p.Value.Def.MaxHp, p.Value.Def.MaxArmor)))
            : "none";
        string guns = plane.Loadout is { } loadout
            ? string.Join(",", loadout.Guns.OrderBy(g => g.Slot).Select(g => string.Format(inv, "{0}:{1}x{2}:{3}",
                g.Slot, g.Weapon.Id, g.MuzzleCount, g.Capacity)))
            : "none";
        int pylons = plane.Loadout?.Hardpoints.Count ?? -1;
        string paint = plane.Scheme is { } s
            ? string.Format(inv, "{0}:{1}:{2}:{3} decals={4}/{5}/{6}", s.Pattern, s.Color1.ToHtml(), s.Color2.ToHtml(),
                s.Color3.ToHtml(), s.NoseDecal, s.TailDecal, s.WingDecal)
            : "no scheme";
        return string.Format(inv, "nitro={0} pylons={1} guns=[{2}] parts=[{3}] paint={4}", plane.Nitro.Installed, pylons, guns,
            parts, paint);
    }

    private static string Rows(DogfightLobby lobby) => string.Join(",", lobby.Players.Select(p => p.Ready));

    private static string Fit(CoopFit fit) => string.Join(",", Enumerable.Range(0, CoopFit.GunSlots).Select(fit.AmmoAt))
        + " | " + string.Join(",", Enumerable.Range(0, 2).Select(fit.OrdnanceAt));

    private static void StepDoors(int steps, params NetPlayFeature[] doors)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var door in doors)
            {
                door.Step(GameClock.FixedDt);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Campaign;
using CSVM.Session.Launch;
using CSVM.UI.Hangar;
using CSVM.UI.Menu;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>The co-op plane picks over the loopback. Every seat picks its own plane of the host's
/// hangar, one seat to a plane, settled in seat order. The host's campaign and each guest's are the
/// features the menus hold, stepped here the way the campaign screen steps them, with no flight.
/// </summary>
internal static class NetCoopGuestPlaneSuites
{
    // Steps each door is given for a reliable word to land on the lossy link.
    private const int SettleSteps = 60;
    private const string HostPilot = "Host";

    // The host's hangar in order: its own plane, the saved wingman plane, and two more. The
    // wingman's airframe is its own, so a wingman moved off it shows in the node it binds.
    private const int SeatedAt = 0;
    private const int WingAt = 1;
    private const int OspreyAt = 2;
    private const int KestrelAt = 3;
    private const string WingPlane = "Wing Hawk";
    private const int WingAirframe = 3;
    private const string Kestrel = "Kestrel";
    private const int KestrelAirframe = 7;
    private const int KestrelPaint = 9;
    private static readonly int[] KestrelAmmo = { 3, 3, 3, 3 };

    [Suite("net-coop-guest-planes",
        "the co-op plane picks over a lossy loopback, a host and three guests: a guest with no pick "
        + "opens on the first free plane, a guest's pick of a free plane flies it on every machine "
        + "with the plane's fit and build, a pick of a plane the host or an earlier seat holds is "
        + "refused naming that seat and the host settles a stale one away, on a same-moment clash the "
        + "earlier seat keeps the plane and the later one is told who took it, the wingman keeps its "
        + "saved plane while no human holds it and else takes a free one or a stock Devastator on a "
        + "wingman mission only, and a splitscreen second seat follows the same rule")]
    internal static void EverySeatPicksItsOwnPlane(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        var missions = CampaignSequence.Load(ctx.ZrdrPath).ToArray();
        var solo = missions.Cast<CampaignMission?>().FirstOrDefault(m => !m!.Value.Wingman)
            ?? throw new SuiteSkippedException($"cm_sequence carries no mission without a wingman");
        var wing = missions.Cast<CampaignMission?>().FirstOrDefault(m => m!.Value.Wingman && m.Value.ChapterFolder.Length > 0)
            ?? throw new SuiteSkippedException($"cm_sequence carries no wingman mission with a chapter");

        string root = Path.Combine(ctx.ScratchDir, "net-coop-guest-planes");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        try
        {
            var store = SeedHost(root);
            NetworkGuests(ctx, root, store, solo, wing);
            Splitscreen(ctx, root, store, solo);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // One host seat and three network guests.
    private static void NetworkGuests(TestContext ctx, string root, CampaignProfileStore store,
        CampaignMission solo, CampaignMission wing)
    {
        var mesh = LoopbackTransport.Mesh(4, new LoopbackConditions(0.03, 0.01, 0.25), new Random(4242));
        var rig = new Rig(OpenHost(ctx, root, store, solo), mesh);
        try
        {
            rig.HostDoor.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            rig.HostDoor.Offer(solo.Seq, HostPilot, 1);
            foreach (var guest in rig.GuestDoors)
            {
                guest.OpenJoin();
            }

            rig.Pump();
            rig.HostDoor.ShowCoop(NetCoopScreen.FlightCheck, solo.Seq, 0, CampaignFeature.HangarAirframes(rig.Host.Profile));
            rig.Pump();
            rig.SortBySeat();
            ctx.Check(rig.HostDoor.CoopGuests.Count == 3 && rig.Guests.All(g => g.IsGuest),
                $"three guests join the host's campaign door and open their campaigns ({rig.HostDoor.CoopGuests.Count} seated)");
            if (rig.HostDoor.CoopGuests.Count != 3 || !rig.Guests.All(g => g.IsGuest))
            {
                return;
            }

            ctx.Check(rig.Guests.Select(g => g.GuestPlane).SequenceEqual(new[] { WingAt, OspreyAt, KestrelAt }) && rig.Agrees(),
                $"a guest with no pick opens on the first plane nobody holds, in seat order ({rig.Reading})");

            FreePick(ctx, rig);
            Refused(ctx, rig);
            Clash(ctx, rig);
            Wingman(ctx, rig, root, solo, wing);
            LaunchAgrees(ctx, rig);
        }
        finally
        {
            rig.Discard();
        }
    }

    // A guest's pick of a free plane flies it with the plane's own fit and build. The host, its
    // launch words and the guest all agree.
    private static void FreePick(TestContext ctx, Rig rig)
    {
        ctx.Check(rig.Pick(3, rig.StockOf(3)) < 0, $"ABLE-TO-FAIL CONTROL: the third guest's stock Devastator is never held");
        rig.Pump();
        rig.ClearLost();
        int holder = rig.Pick(1, KestrelAt);
        rig.Pump();
        var seat = rig.HostDoor.CoopGuests[0];
        var mine = rig.Guests[0];
        ctx.Check(holder < 0 && mine.GuestPlane == KestrelAt && rig.Host.SeatPlanes[1] == KestrelAt && rig.Agrees(),
            $"the first guest's pick of the Kestrel, which the third guest freed, stands on every machine ({rig.Reading})");
        ctx.Check(seat.Airframe == KestrelAirframe && seat.Fit.AmmoAt(0) == KestrelAmmo[0] && seat.Build is { PaintPattern: KestrelPaint },
            $"the host seats that guest on the Kestrel's airframe, fit and build ({seat.Airframe}, ammo {seat.Fit.AmmoAt(0)}, paint {seat.Build?.PaintPattern})");
        ctx.Check(mine.GuestAirframe == KestrelAirframe && mine.GuestCoopFit.AmmoAt(0) == KestrelAmmo[0]
                  && mine.GuestBuildOf(mine.Field.Plane(0)!) is { PaintPattern: KestrelPaint },
            $"and the guest's own campaign flies the same ({mine.GuestAirframe}, paint {mine.GuestBuildOf(mine.Field.Plane(0)!)?.PaintPattern})");
        ctx.Check(rig.Guests[1].Field.HolderOf(0, rig.Guests[1].Profile!.Planes[KestrelAt]) == 1 && rig.NoneLost(),
            $"every other guest sees the Kestrel held by seat 1, and no pick moved ({rig.LostReading})");
    }

    // A pick of a plane the host or an earlier seat holds is refused naming that seat. A pick made
    // on a stale view reaches the host anyway, which keeps its own plane and settles the pick away.
    private static void Refused(TestContext ctx, Rig rig)
    {
        var second = rig.Guests[1];
        int byGuest = rig.Pick(2, KestrelAt);
        int byHost = rig.Pick(2, SeatedAt);
        ctx.Check(byGuest == 1 && byHost == 0 && second.GuestPlane == OspreyAt,
            $"the second guest's picks of the Kestrel and of the host's plane are refused, and it stays on the Osprey ({byGuest}, {byHost}, {second.GuestPlane})");
        ctx.Check(second.SeatRefusal(byHost).StartsWith("P1 ", StringComparison.Ordinal) && second.SeatRefusal(byGuest).StartsWith("P2 ", StringComparison.Ordinal),
            $"each refusal names the player holding the plane (\"{second.SeatRefusal(byGuest)}\")");

        rig.ClearLost();
        rig.Guests[2].CommitPlanes(SeatedAt, null);
        rig.Pump();
        ctx.Check(rig.Host.SeatPlanes[0] == SeatedAt && rig.Lost[2] == 0 && rig.Guests[2].GuestPlane == WingAt && rig.Agrees(),
            $"a stale pick of the host's plane leaves the host on it and moves the guest to the first free plane, told seat 0 took it ({rig.Lost[2]}, {rig.Reading})");
    }

    // Two guests pick one free plane in the same step, and the earlier seat keeps it. The later one
    // is told who took it and moves to the plane the host gave it.
    private static void Clash(TestContext ctx, Rig rig)
    {
        rig.Pick(1, rig.StockOf(1));
        rig.Pump();
        rig.ClearLost();
        int second = rig.Pick(2, KestrelAt);
        int third = rig.Pick(3, KestrelAt);
        ctx.Check(second < 0 && third < 0, $"ABLE-TO-FAIL CONTROL: both guests' views show the Kestrel free when they pick it");
        rig.Pump();
        ctx.Check(rig.Guests[1].GuestPlane == KestrelAt && rig.Host.SeatPlanes[2] == KestrelAt && rig.Lost[1] < 0,
            $"the earlier seat keeps the Kestrel and is never told otherwise ({rig.Reading}, {rig.LostReading})");
        ctx.Check(rig.Lost[2] == 2 && rig.Guests[2].GuestPlane is >= 0 and not KestrelAt && rig.Agrees(),
            $"the later seat is told seat 2 took it and flies a free plane instead ({rig.LostReading}, {rig.Reading})");
    }

    // The wingman holds a plane on a wingman mission only, after every human. It keeps its saved
    // plane while no human holds it, else takes the first free one, else a stock Devastator.
    private static void Wingman(TestContext ctx, Rig rig, string root, CampaignMission solo, CampaignMission wing)
    {
        var host = rig.Host;
        if (rig.Guests[2].GuestPlane != WingAt)
        {
            rig.Pick(3, WingAt);
            rig.Pump();
        }

        ctx.Check(rig.Guests[2].GuestPlane == WingAt && !host.MissionHasWingman && host.FlownWingman == WingAt && host.WingmanOverride == null,
            $"on a mission with no wingman a guest flies the wingman's saved plane and the wingman holds nothing ({rig.Reading})");
        host.SetMission(wing.Seq);
        ctx.Check(host.MissionHasWingman && host.FlownWingman == OspreyAt && host.WingmanOverride is { Airframe: 2 },
            $"on a wingman mission the wingman takes the first free plane while a guest holds its own ({host.FlownWingman}, {host.WingmanOverride?.Airframe})");

        rig.Pick(1, OspreyAt);
        rig.Pump();
        var stock = host.FlownWingmanPlane;
        var told = host.WingmanOverride;
        var exit = host.BuildExit(new[] { Array.Empty<int>() });
        var rest = new OwnedPlane();
        ctx.Check(host.FlownWingman == CoopPlanePool.Stock && stock is { Airframe: CoopPlanePool.StockAirframe } && host.Field.IsStock(stock)
                  && told is { Airframe: CoopPlanePool.StockAirframe } flown && flown.Fit == CoopFit.Of(rest.Ammo, rest.Ordnance)
                  && exit?.Wingman == told,
            $"with every plane held the wingman flies a stock Devastator at rest, which the launch carries ({rig.Reading}, {told?.Airframe})");
        StockWingmanBinds(ctx, root, wing, told);

        rig.Pick(3, rig.StockOf(3));
        rig.Pump();
        ctx.Check(host.FlownWingman == WingAt && host.WingmanOverride == null,
            $"and the wingman takes its saved plane back once no human holds it ({host.FlownWingman})");
        host.SetMission(solo.Seq);
    }

    // The mission's director binds the settled wingman in place of the saved plane's airframe.
    private static void StockWingmanBinds(TestContext ctx, string root, CampaignMission wing, CoopWingmanMessage? told)
    {
        string chapter = wing.ChapterFolder.ToUpperInvariant();
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, wing.MissionFolder.ToUpperInvariant());
        if (!Directory.Exists(zrdr) && !File.Exists(zrdr))
        {
            ctx.Check(false, $"the wingman mission's script is on disk ({zrdr})");
            return;
        }

        var saved = CampaignDirector.TryCreate(Spec(root, wing, null), ctx.ZrdrPath, zrdr);
        var settled = CampaignDirector.TryCreate(Spec(root, wing, told), ctx.ZrdrPath, zrdr);
        ctx.Check(saved?.WingmanNode == PlanePickerRoster.AirframeNode(WingAirframe),
            $"ABLE-TO-FAIL CONTROL: with nothing settled the director binds the saved wingman plane ({saved?.WingmanNode})");
        ctx.Check(settled?.WingmanNode == PlanePickerRoster.AirframeNode(CoopPlanePool.StockAirframe),
            $"and with the stock Devastator settled it binds the Devastator ({settled?.WingmanNode})");
    }

    // Every guest Ready opens the host's launch. Its field, fits and builds seat each guest on the
    // plane the host settled, as each guest's own launch does.
    private static void LaunchAgrees(TestContext ctx, Rig rig)
    {
        foreach (var (door, campaign) in rig.GuestDoors.Zip(rig.Guests))
        {
            door.Pick.Set(campaign.GuestAirframe, true, campaign.GuestCoopFit);
        }

        rig.Pump();
        var launch = rig.HostDoor.CoopAllReady ? rig.HostDoor.BuildLaunch() : null;
        ctx.Check(launch != null, $"every guest's Ready opens the host's launch");
        if (launch == null)
        {
            return;
        }

        var own = new[] { PlanePickerRoster.AirframeNode(rig.Host.Profile!.Planes[SeatedAt].Airframe) };
        var (roster, fits) = Launcher.CoopLaunchField(rig.HostDoor, launch.Transport, own, new LoadoutChoice?[] { null }, StockLoadouts.Load());
        var builds = Launcher.CoopSeatBuilds(roster, new CustomPlaneDef?[] { null }, rig.HostDoor, launch.Transport);
        var seats = new List<string>();
        bool agrees = roster.Length == 4;
        for (int g = 0; g < rig.Guests.Length && agrees; g++)
        {
            int seat = Array.FindIndex(roster, s => s.PeerId == rig.GuestWires[g].LocalPeer);
            var campaign = rig.Guests[g];
            var plane = campaign.Field.Plane(0)!;
            var exit = campaign.BuildExit(new[] { Array.Empty<int>() });
            string node = PlanePickerRoster.AirframeNode(plane.Airframe);
            seats.Add(seat >= 0 ? roster[seat].PlaneNode : "-");
            agrees &= seat == g + 1 && roster[seat].PlaneNode == node && exit?.Seats[0].PlaneNode == node
                && builds[seat]?.PaintPattern == exit?.Seats[0].Custom?.PaintPattern
                && (campaign.GuestPlane != KestrelAt || (builds[seat] is { PaintPattern: KestrelPaint } && fits[seat].AmmoAt(0) == KestrelAmmo[0]));
        }

        ctx.Check(agrees, $"the host's launch and each guest's own seat every guest on its settled plane and build ({string.Join(", ", seats)})");
    }

    // The host with a second local seat and one network guest, which sits in seat 2.
    private static void Splitscreen(TestContext ctx, string root, CampaignProfileStore store, CampaignMission solo)
    {
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(4243));
        var rig = new Rig(OpenHost(ctx, root, store, solo), mesh);
        var field = rig.Host.Field;
        try
        {
            field.SetPlayers(2);
            rig.HostDoor.OpenCoopHost(NetPlayFeature.CoopHumans - 2);
            rig.HostDoor.Offer(solo.Seq, HostPilot, 2);
            rig.GuestDoors[0].OpenJoin();
            rig.Pump();
            rig.HostDoor.ShowCoop(NetCoopScreen.FlightCheck, solo.Seq, 0, CampaignFeature.HangarAirframes(rig.Host.Profile));
            rig.Pump();
            var guest = rig.Guests[0];
            ctx.Check(field.Plane(1)?.Name == WingPlane && guest.IsGuest && rig.GuestDoors[0].CoopFlow?.Slot == 2 && guest.GuestPlane == OspreyAt,
                $"the local second seat opens on the first free plane and the network guest, in seat 2, on the next ({field.Plane(1)?.Name}, guest {guest.GuestPlane})");
            ctx.Check(field.Holder(1, SeatedAt) == 0 && field.Holder(1, OspreyAt) == 2 && !field.Choose(1, OspreyAt)
                      && guest.Field.HolderOf(0, guest.Profile!.Planes[WingAt]) == 1,
                $"the local seat is refused the host's plane and the network guest's, and the guest the local seat's");

            rig.ClearLost();
            bool local = field.Choose(1, KestrelAt);
            int remote = rig.Pick(1, KestrelAt);
            rig.Pump();
            ctx.Check(local && remote < 0 && field.Plane(1)?.Name == Kestrel && rig.Host.SeatPlanes[1] == KestrelAt
                      && rig.Lost[0] == 1 && guest.GuestPlane is >= 0 and not KestrelAt,
                $"on a same-moment pick the local seat, the earlier, keeps the Kestrel and the network guest is told seat 1 took it ({rig.Reading}, {rig.LostReading})");
        }
        finally
        {
            rig.Discard();
        }
    }

    // The host's profile: its own plane, the saved wingman plane, the Osprey and the Kestrel, the
    // last with its own fit and a hangar build.
    private static CampaignProfileStore SeedHost(string root)
    {
        var store = new CampaignProfileStore(Path.Combine(root, "Profiles"));
        var profile = CampaignProfileDef.NewProfile(HostPilot);
        profile.Planes[WingAt] = new OwnedPlane { Name = WingPlane, Airframe = WingAirframe };
        profile.Planes.Add(new OwnedPlane { Name = "Osprey", Airframe = 2 });
        profile.Planes.Add(new OwnedPlane { Name = Kestrel, Airframe = KestrelAirframe, Ammo = (int[])KestrelAmmo.Clone() });
        store.Save(profile);
        new CustomPlaneStore(Path.Combine(root, "Planes")).Save(new CustomPlaneDef { Name = Kestrel, Airframe = KestrelAirframe, PaintPattern = KestrelPaint });
        return store;
    }

    private static CampaignFeature OpenHost(TestContext ctx, string root, CampaignProfileStore store, CampaignMission mission)
    {
        var host = new CampaignFeature(UiStrings.Empty, PlanePickerRoster.AirframeNode);
        host.Open(store, new CustomPlaneStore(Path.Combine(root, "Planes")), StockLoadouts.Load(), ctx.DataRoot);
        host.SeatProfile(HostPilot);
        host.SetMission(mission.Seq);
        return host;
    }

    private static SessionSpec Spec(string root, CampaignMission mission, CoopWingmanMessage? wingman) =>
        SessionSpec.FromCampaign(
            SessionSpec.Parse(new[] { "--mute", "--no-pads", $"--profiles={Path.Combine(root, "Profiles")}" }),
            HostPilot, mission.Seq, new[] { PlanePickerRoster.AirframeNode(CoopPlanePool.StockAirframe) }, 1, wingman: wingman);

    private static NetPlayFeature Door(LoopbackTransport wire) => new((_, _, _) => wire, (_, _) => wire);

    // The host's campaign, its door, and each guest's door and campaign, stepped as the campaign
    // screen steps them. The host settles every pick and names its hangar, and each guest follows.
    private sealed class Rig
    {
        public Rig(CampaignFeature host, IReadOnlyList<LoopbackTransport> mesh)
        {
            Host = host;
            HostDoor = Door(mesh[0]);
            GuestWires = mesh.Skip(1).ToArray();
            GuestDoors = GuestWires.Select(Door).ToArray();
            Guests = GuestDoors.Select(_ => new CampaignFeature(UiStrings.Empty, PlanePickerRoster.AirframeNode)).ToArray();
            Lost = GuestDoors.Select(_ => -1).ToArray();
        }

        public CampaignFeature Host { get; }

        public NetPlayFeature HostDoor { get; }

        // Each guest's wire, door and campaign, in seat order once SortBySeat has run.
        public LoopbackTransport[] GuestWires { get; }

        public NetPlayFeature[] GuestDoors { get; }

        public CampaignFeature[] Guests { get; }

        // The seat each guest was last told took its pick, or -1.
        public int[] Lost { get; }

        public string Reading => string.Join(", ", Host.SeatPlanes);

        public string LostReading => string.Join(", ", Lost);

        public void ClearLost() => Array.Fill(Lost, -1);

        // The joins land in whatever order the lossy link delivers them, and the host seats them so.
        public void SortBySeat()
        {
            int[] seats = GuestDoors.Select(d => (int?)d.CoopFlow?.Slot ?? int.MaxValue).ToArray();
            int[] order = Enumerable.Range(0, seats.Length).OrderBy(i => seats[i]).ToArray();
            var wires = order.Select(i => GuestWires[i]).ToArray();
            var doors = order.Select(i => GuestDoors[i]).ToArray();
            var guests = order.Select(i => Guests[i]).ToArray();
            wires.CopyTo(GuestWires, 0);
            doors.CopyTo(GuestDoors, 0);
            guests.CopyTo(Guests, 0);
            ClearLost();
        }

        public bool NoneLost() => Lost.All(seat => seat < 0);

        // The stock Devastator's entry in guest `guest`'s list, its last.
        public int StockOf(int guest) => Guests[guest - 1].Profile!.Planes.Count - 1;

        // Guest `guest`'s plane selection: the seat its field says holds the plane, else the pick.
        public int Pick(int guest, int plane)
        {
            var campaign = Guests[guest - 1];
            int holder = campaign.Field.HolderOf(0, campaign.Profile!.Planes[plane]);
            if (holder < 0)
            {
                campaign.CommitPlanes(plane, null);
            }

            return holder;
        }

        // Whether every seat's plane is its own, and each guest flies what the host settled for it.
        public bool Agrees()
        {
            var planes = Host.SeatPlanes;
            var held = planes.Where(p => p >= 0).ToArray();
            return planes.Count == Guests.Length + Host.Field.Players && held.Distinct().Count() == held.Length
                && Guests.Select((g, i) => g.GuestPlane == planes[Host.Field.Players + i]).All(same => same);
        }

        public void Pump()
        {
            for (int i = 0; i < SettleSteps; i++)
            {
                HostDoor.Step(GameClock.FixedDt);
                Host.SetRemotePicks(HostDoor.CoopGuestPlanes);
                HostDoor.OfferCoopHangar(Host.CoopHangar(), Host.SeatPlanes);
                for (int g = 0; g < Guests.Length; g++)
                {
                    GuestDoors[g].Step(GameClock.FixedDt);
                    Follow(g);
                }
            }
        }

        public void Discard()
        {
            foreach (var door in GuestDoors.Prepend(HostDoor))
            {
                door.Discard();
            }

            foreach (var campaign in Guests.Prepend(Host))
            {
                campaign.Discard();
            }
        }

        private void Follow(int g)
        {
            var door = GuestDoors[g];
            var campaign = Guests[g];
            if (door.CoopFlow is not { } flow || door.CoopHangar.Count == 0)
            {
                return;
            }

            if (!campaign.IsGuest)
            {
                campaign.OpenGuest(HostPilot, flow.Progress, door.CoopHangar, flow.Slot, plane: door.Pick.Plane, fit: door.Pick.Fit);
            }

            int lostTo = campaign.FollowHost(flow.Progress, door.CoopHangar);
            if (lostTo >= 0)
            {
                Lost[g] = lostTo;
            }

            door.Pick.Set(campaign.GuestAirframe, door.Pick.Ready, campaign.GuestCoopFit);
            door.Pick.Choose(campaign.GuestPlane);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Campaign;
using CSVM.Session.Launch;
using CSVM.UI.Campaign;
using CSVM.UI.Hangar;
using CSVM.UI.Menu;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>The co-op allocation over the loopback. Which of the host's campaign planes each guest
/// flies, as the host's door tells it and the host's launch seats it. The host's campaign is a
/// scratch profile, and the doors are the ones the menus hold, stepped with no flight.</summary>
internal static class NetCoopGuestPlaneSuites
{
    // Steps each door is given for a reliable word to land on the lossy link.
    private const int SettleSteps = 60;
    private const int MissionSeq = 3;

    // Two planes beyond a new profile's own pair, which the host and the wingman hold. Kestrel has
    // a hangar build with its own paint, so a guest seated in it shows the build crossing.
    private const string Kestrel = "Kestrel";
    private const string Osprey = "Osprey";
    private const int KestrelPaint = 9;
    private static readonly int[] KestrelAmmo = { 3, 3, 3, 3 };

    [Suite("net-coop-guest-planes",
        "the co-op allocation over a lossy loopback: three guests of a host whose hangar holds two "
        + "planes beyond its own and its wingman's each hear a distinct seat, two of them the spare "
        + "planes with their own fit and build and the third a stock Devastator, the host's roster, "
        + "each guest's campaign and the host's launch field and builds agree with what each guest "
        + "heard whatever airframe the guest picked, and a splitscreen seat takes the same "
        + "allocator's answer, with a network guest after two local seats taking the third seat's")]
    internal static void GuestsFlyTheHostsUniquePlanes(TestContext ctx)
    {
        string root = Path.Combine(ctx.ScratchDir, "net-coop-guest-planes");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        try
        {
            var flow = OpenHost(root);
            var planes = flow.Feature.CoopSeatPlanes(NetPlayFeature.CoopHumans);
            NetworkGuests(ctx, flow, planes);
            Splitscreen(ctx, flow, planes);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // One host seat and three network guests, each guest's word checked against every other view.
    private static void NetworkGuests(TestContext ctx, CampaignFlow flow, IReadOnlyList<CoopSeatPlaneMessage> planes)
    {
        var mesh = LoopbackTransport.Mesh(4, new LoopbackConditions(0.03, 0.01, 0.25), new Random(4242));
        var doors = mesh.Select(Door).ToArray();
        var guests = doors.Skip(1).ToArray();
        try
        {
            var host = doors[0];
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(MissionSeq, "Host", 1);
            foreach (var guest in guests)
            {
                guest.OpenJoin();
            }

            Pump(doors);
            ctx.Check(guests.All(g => g.IsCoopGuest) && host.CoopGuests.Count == 3,
                $"three guests join the host's campaign door ({host.CoopGuests.Count} seated)");
            if (host.CoopGuests.Count != 3)
            {
                return;
            }

            host.ShowCoop(NetCoopScreen.FlightCheck, MissionSeq, 0, CampaignFeature.HangarAirframes(flow.Profile));
            host.OfferCoopPlanes(planes);
            Pump(doors);

            var heard = guests.Select(g => g.CoopSeatPlane).ToArray();
            string reading = string.Join(", ", heard.Select(w => w is { } word ? $"{word.Seat}:{(word.Hangar ? word.Name : "stock")}" : "-"));
            ctx.Check(heard.All(w => w != null) && heard.Select(w => (int)w!.Value.Seat).OrderBy(s => s).SequenceEqual(new[] { 1, 2, 3 }),
                $"each guest hears the plane of its own seat ({reading})");
            if (heard.Any(w => w == null))
            {
                return;
            }

            var words = heard.Select(w => w!.Value).ToArray();
            var hangar = words.Where(w => w.Hangar).Select(w => w.Name).ToArray();
            ctx.Check(hangar.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(new[] { Kestrel, Osprey }),
                $"two guests fly the host's two spare planes, one each ({string.Join(", ", hangar)})");
            ctx.Check(!words.Any(w => w.Hangar && Reserved(flow.Profile!).Contains(w.Name)),
                $"and nobody is handed the host's own plane or its wingman's ({string.Join(", ", Reserved(flow.Profile!))})");
            ctx.Check(words.Count(w => !w.Hangar) == 1 && words.Single(w => !w.Hangar) is { Seat: 3, Airframe: (byte)CoopPlanePool.StockAirframe },
                $"the guest beyond the pool flies a stock Devastator ({reading})");

            var kestrel = flow.Profile!.Planes.Single(p => p.Name == Kestrel);
            var kestrelWord = words.Single(w => w.Name == Kestrel);
            ctx.Check(kestrelWord.Fit == CoopFit.Of(kestrel.Ammo, kestrel.Ordnance) && kestrelWord.Fit.AmmoAt(0) == KestrelAmmo[0]
                      && kestrelWord.Build is { PaintPattern: KestrelPaint },
                $"the Kestrel's guest carries the host's fit and hangar build for it (ammo {kestrelWord.Fit.AmmoAt(0)}, paint {kestrelWord.Build?.PaintPattern})");

            Agree(ctx, host, guests, mesh, words);
            LaunchAgrees(ctx, host, guests, mesh, words);
        }
        finally
        {
            foreach (var door in doors)
            {
                door.Discard();
            }
        }
    }

    // The host's roster, the host's own record of each seat, and the guest's campaign built on the
    // word all name the same plane.
    private static void Agree(TestContext ctx, NetPlayFeature host, NetPlayFeature[] guests,
        IReadOnlyList<LoopbackTransport> mesh, CoopSeatPlaneMessage[] words)
    {
        var seated = host.CoopGuests;
        bool rosterAgrees = true;
        bool campaignAgrees = true;
        for (int g = 0; g < guests.Length; g++)
        {
            var word = words[g];
            int peer = mesh[g + 1].LocalPeer;
            rosterAgrees &= host.HostFlow.PlaneOf(word.Seat) == word
                && seated.Any(c => c.Peer == peer && c.Slot == word.Seat && c.Airframe == word.Airframe
                    && c.Fit == word.Fit && Equals(c.Build, word.Build));

            var campaign = new CampaignFeature(UiStrings.Empty, PlanePickerRoster.AirframeNode);
            campaign.OpenGuest("Host", 0, guests[g].CoopSeatPlane);
            campaignAgrees &= campaign.GuestAirframe == word.Airframe && campaign.GuestCoopFit == word.Fit
                && (!word.Hangar || campaign.Profile!.Planes[0].Name == word.Name)
                && campaign.GuestBuild?.PaintPattern == word.Build?.PaintPattern
                && (campaign.GuestBuild == null) == (word.Build == null);
        }

        ctx.Check(rosterAgrees, $"the host seats each guest in the plane that guest heard, fit and build included");
        ctx.Check(campaignAgrees, $"and each guest's campaign opens on that same plane, fit and build");
    }

    // Every guest picks the starter, which the host ignores. Its launch field seats each guest's
    // slot in the allocated airframe and fit, and its builds carry the allocated build.
    private static void LaunchAgrees(TestContext ctx, NetPlayFeature host, NetPlayFeature[] guests,
        IReadOnlyList<LoopbackTransport> mesh, CoopSeatPlaneMessage[] words)
    {
        foreach (var guest in guests)
        {
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
        }

        Pump(guests.Prepend(host).ToArray());
        ctx.Check(host.CoopAllReady, $"every guest's Ready opens the host's launch");
        var launch = host.BuildLaunch();
        if (launch == null)
        {
            ctx.Check(false, $"the host's door builds its launch");
            return;
        }

        var own = new[] { PlanePickerRoster.AirframeNode(CoopPlanePool.StockAirframe) };
        var (roster, fits) = Launcher.CoopLaunchField(host, launch.Transport, own, new LoadoutChoice?[] { null }, StockLoadouts.Load());
        var builds = Launcher.CoopSeatBuilds(roster, new CustomPlaneDef?[] { null }, host, launch.Transport);
        var seats = new List<string>();
        bool agrees = roster.Length == 4;
        for (int g = 0; g < guests.Length && agrees; g++)
        {
            var word = words[g];
            int seat = Array.FindIndex(roster, s => s.PeerId == mesh[g + 1].LocalPeer);
            seats.Add(seat >= 0 ? roster[seat].PlaneNode : "-");
            agrees &= seat == word.Seat && roster[seat].PlaneNode == PlanePickerRoster.AirframeNode(word.Airframe)
                && fits[seat] == word.Fit && Equals(builds[seat], word.Build);
        }

        ctx.Check(agrees, $"the host's launch seats each guest in its allocated airframe, fit and build ({string.Join(", ", seats)})");
        ctx.Check(roster.Any(s => s.PlaneNode == PlanePickerRoster.AirframeNode(words.Single(w => w.Name == Kestrel).Airframe)),
            $"ABLE-TO-FAIL CONTROL: a guest that picked the Devastator is seated in the Kestrel's airframe");
    }

    // The splitscreen field reads the same allocator, and a network guest after two local seats
    // takes the seat a third local player would.
    private static void Splitscreen(TestContext ctx, CampaignFlow flow, IReadOnlyList<CoopSeatPlaneMessage> planes)
    {
        flow.SetPlayers(NetPlayFeature.CoopHumans);
        var field = Enumerable.Range(1, 3).Select(p => flow.Field.Plane(p)).ToArray();
        bool same = true;
        for (int p = 1; p <= 3; p++)
        {
            var plane = field[p - 1];
            same &= plane != null && (planes[p].Hangar
                ? plane.Name == planes[p].Name && !flow.Field.IsStock(plane)
                : flow.Field.IsStock(plane) && plane.Airframe == planes[p].Airframe);
        }

        ctx.Check(same, $"a splitscreen field's seats 2 to 4 fly what the allocation gives seats 1 to 3 ({string.Join(", ", field.Select(p => p?.Name ?? "-"))})");

        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(4243));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 2);
            host.Offer(MissionSeq, "Host", 2);
            guest.OpenJoin();
            Pump(host, guest);
            host.ShowCoop(NetCoopScreen.FlightCheck, MissionSeq, 0, CampaignFeature.HangarAirframes(flow.Profile));
            host.OfferCoopPlanes(planes);
            Pump(host, guest);
            flow.SetPlayers(2);
            var local = flow.Field.Plane(1);
            var word = guest.CoopSeatPlane;
            ctx.Check(word is { Seat: 2 } heard && heard.Name == planes[2].Name
                      && local is { } second && second.Name == planes[1].Name && second.Name != heard.Name,
                $"with two local seats the network guest takes seat 3's plane and the local second seat keeps its own ({local?.Name}, guest {word?.Name})");
        }
        finally
        {
            guest.Discard();
            host.Discard();
        }
    }

    // A new profile with two spare planes, saved and read back the way the cabin seats it.
    private static CampaignFlow OpenHost(string root)
    {
        var store = new CampaignProfileStore(Path.Combine(root, "Profiles"));
        var planes = new CustomPlaneStore(Path.Combine(root, "Planes"));
        var profile = CampaignProfileDef.NewProfile("Host");
        profile.Planes.Add(new OwnedPlane { Name = Kestrel, Airframe = 7, Ammo = (int[])KestrelAmmo.Clone() });
        profile.Planes.Add(new OwnedPlane { Name = Osprey, Airframe = 2 });
        store.Save(profile);
        planes.Save(new CustomPlaneDef { Name = Kestrel, Airframe = 7, PaintPattern = KestrelPaint });
        var flow = new CampaignFlow(store, UiStrings.Empty, planes: planes);
        flow.SelectProfile(store.Load("Host")!);
        return flow;
    }

    private static string[] Reserved(CampaignProfileDef profile) =>
        new[] { profile.Planes[profile.SelectedPlane].Name, profile.Planes[profile.WingmanPlane].Name };

    private static NetPlayFeature Door(LoopbackTransport wire) => new((_, _, _) => wire, (_, _) => wire);

    private static void Pump(params NetPlayFeature[] doors)
    {
        for (int i = 0; i < SettleSteps; i++)
        {
            foreach (var door in doors)
            {
                door.Step(GameClock.FixedDt);
            }
        }
    }
}

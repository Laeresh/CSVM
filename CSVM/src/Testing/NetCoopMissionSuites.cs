using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Campaign;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;
using Ends = CSVM.Testing.NetCombatSuites.Ends;

namespace CSVM.Testing;

/// <summary>A campaign mission flown together, whole sessions and doors in one process over a
/// lossy loopback. Each end runs the door the menus hold and the session the launcher builds from
/// it, stepped the way the launcher steps them. The host's field and fits come from the
/// launcher's own helpers, so a claim here is about the path a player's launch takes.</summary>
internal static class NetCoopMissionSuites
{
    private const ulong HostSeed = 0xC0DE0C24UL;

    // Steps each end is given for a reliable word to land, on a link with 30 ms of latency.
    private const int SettleSteps = 30;

    // A guest's skip crosses twice, the ask to the host and its word back, on the lossy link.
    private const int SkipWindowSteps = 240;

    // The longest a doomed field is given to reach its ending, wrecks falling included.
    private const int EndingSteps = 60 * 90;

    // The longest a guest's door is stepped for its host's opener to arrive.
    private const int OpenerSteps = 600;

    private const string GuestName = "Lucy";

    // Lucy's place in the Voice list, Black Swan.
    private const int GuestVoice = 2;

    // CM21: OBJECTIVE28 wakes the Cabbie and releases his taxi. OBJECTIVE20 removes him within
    // 1000 m of its point, which is read off the host's own script.
    private const string Cabbie = "autogyro_1";
    private const int CabbieWake = 28;
    private const int CabbieDelivered = 20;

    // The longest the Cabbie's rooftop taxi run is given to hand him to the flight model.
    private const int CabbieTaxiSteps = 60 * 60;

    // CM09's own opening film, which alone flies the wingman prop on its scripts and leaves it
    // wherever the last one stops. It plays for about 53 s; the budget is well past that.
    private const string IntroFilm = "mission_intro_animation";
    private const int IntroSteps = 60 * 120;

    // The prop sits at the world origin until the film's first script poses it. It then flies
    // thousands of metres out, so this only rules out a prop that never moved.
    private const float PropFlownM = 1000f;

    // How long each seat is watched after the film, and how far it has to fly in that time. A seat
    // cruises near 90 m/s, so a halted or pinned aeroplane is the only way to fall short.
    private const int TravelSteps = 120;
    private const float SeatTravelM = 50f;

    // The placed return carries the pose as floats, so the copy lands on it to rounding alone.
    private const float ReturnPoseM = 0.5f;
    private const float ReturnHeadingDeg = 1f;

    // How far a host's copy of a guest's plane may sit from the guest's own: half a grid slot. A
    // copy following the guest's other plane misses by more. The copy draws about 0.13 s behind
    // (read-behind and link), some 12 m at cruise.
    private const float SeatReplicaM = StartGrid.SlotSpacingDefault * 0.5f;

    // The persist-log suite's host profile, and C3's suspension bridge: CM01 (M01) drops it and CM02
    // (M05) opens on it. In CM01 its fuel truck's chain reaction drops it by animation, given until
    // well past the chain's EVENT_OFFSET stagger.
    private const string LogHost = "LogHost";
    private const string BridgeChapter = "C3";
    private const string BridgeEarlierMission = "M01";
    private const string BridgeLaterMission = "M05";
    private const string BridgeDef = "susp_bridge";
    private const string BridgeTruckDef = "bridge_truck01";
    private const int ChainSteps = 60 * 4;

    // C3's grass huts, a PERSIST_LOG def standing in CM01's open; the guard towers are not in its world.
    // A guest's held guns are given ShotSteps to bring one down on the host. A hut's death squashes it
    // for 5 s and switches it off at 4.5 s, so its pose is read past both.
    private const string HutDef = "grasshut";
    private const int ShotSteps = 60 * 10;
    private const int HutDeathSteps = 60 * 6;

    // How far off the struck collider the guest's held aeroplane is pinned, well inside gun range.
    private const float PerchM = 150f;

    // CM13: OBJECTIVE1 wakes one second in, and its WAKE_ANIM runs the Kingdom gate's own death.
    private const string GateChapter = "C2";
    private const string GateMission = "M03";
    private const string GateDef = "kkgate";
    private const int GateWakeSteps = 60 * 10;

    // The wingman suite's host profile and its wingman pick, the Fury, which no profile-less director
    // binds. The armour-piercing ammunition rides the wire and outranks the Fury's AI def's guns.
    private const string WingHost = "WingHost";
    private const byte WingAirframe = 7;
    private static readonly int[] WingAmmo = { 2, 2, 2, 2 };

    // What each end flies. The host picks dumdum and Lucy magnesium, and the third end keeps the
    // starter's stock slug, so a seat's rounds say whose fit reached it.
    private static readonly int[] HostAmmo = { 1, 1, 1, 1 };
    private static readonly int[] LucyAmmo = { 3, 3, 3, 3 };

    [Suite("net-coop-mission",
        "a co-op campaign mission flown by a host and two guests, one process, lossy loopback: each "
        + "guest is seated under its player name or its player number and in its chosen voice, the "
        + "host's seat in Nathan Zachary's, each pane marks its partners' aeroplanes by those names "
        + "and the nameless guest's as Unknown, "
        + "every machine builds each "
        + "seat with its own pilot's ammunition, a guest's director has no profile store and the "
        + "user's profiles are untouched, a guest walking out through its pause sheet leaves at "
        + "once, the host alone ends the mission and a guest holds its result, Retry clears every "
        + "Ready, and a dropped host ends the guest's flight with Host left the game")]
    internal static void ACoopMissionFlownTogether(TestContext ctx)
    {
        var mission = Mission(ctx);
        var stock = StockLoadouts.Load();
        string profiles = Path.Combine(ProjectSettings.GlobalizePath("user://"), "Profiles");
        var before = Snapshot(profiles);

        var mesh = LoopbackTransport.Mesh(3, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2403));
        // The star a real host is: each guest links to the host alone.
        mesh[1].Disconnect(2);
        var host = Door(mesh[0]);
        var lucy = Door(mesh[1]);
        var third = Door(mesh[2]);
        lucy.Identity.PlayerName = GuestName;
        lucy.Identity.Voice = GuestVoice;
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            lucy.OpenJoin();
            third.OpenJoin();
            Pump(SettleSteps, host, lucy, third);
            ctx.Check(lucy.IsCoopGuest && third.IsCoopGuest && host.CoopGuests.Count == 2,
                $"both guests join the host's campaign door ({lucy.Stage}, {third.Stage}, {host.CoopGuests.Count} seated)");
            if (!lucy.IsCoopGuest || !third.IsCoopGuest)
            {
                return;
            }

            FirstFlight(ctx, mission, stock, mesh, host, lucy, third);
            SecondFlight(ctx, mission, stock, mesh, host, third);
        }
        finally
        {
            ambient.Restore();
            third.Discard();
            lucy.Discard();
            host.Discard();
        }

        var after = Snapshot(profiles);
        ctx.Check(before.Count == after.Count
                  && before.All(f => after.TryGetValue(f.Key, out var now) && now.AsSpan().SequenceEqual(f.Value)),
            $"no file under the user's profile store was written, added or removed ({before.Count} before, {after.Count} after)");
    }

    [Suite("net-coop-restart",
        "a co-op mission restarted from the host's pause sheet, host and one guest over a lossy "
        + "loopback: the guest's sheet offers no Restart while the host's does, and the host's "
        + "restart relaunches both machines into the same mission through the door, with no second "
        + "session bound on the carrier and the guest's seat flying in the new field")]
    internal static void BCoopRestartTakesTheGuestAlong(TestContext ctx)
    {
        var mission = Mission(ctx);
        var stock = StockLoadouts.Load();
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2404));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        guest.Identity.PlayerName = GuestName;
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            if (!guest.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"the guest joins the host's campaign door and is Ready ({guest.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out _);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"the first flight builds (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            ctx.Check(hostEnd.Session.Boards?.PauseRestart != null && hostEnd.Session.RestartOffered,
                $"ABLE-TO-FAIL CONTROL: the host's pause sheet offers Restart");
            ctx.Check(guestEnd.Session.Boards?.PauseRestart == null && !guestEnd.Session.RestartOffered,
                $"a guest's pause sheet offers no Restart in a network session");
            (hostEnd, guestEnd) = Restart(ctx, mission, stock, hostEnd, guestEnd, host, guest);
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
        }
    }

    [Suite("net-coop-guest-seats",
        "a co-op guest with two players at its machine, host and guest over a lossy loopback: the host "
        + "seats the machine twice side by side, its launch waits on each player's Ready, the join names "
        + "the guest both seats, the guest builds a pane per seat and flies both while the host builds "
        + "both as remote aeroplanes, and each of the guest's planes flies on the host as it flies on "
        + "the guest")]
    internal static void DCoopGuestFliesTwoSeats(TestContext ctx)
    {
        var mission = Mission(ctx);
        var stock = StockLoadouts.Load();
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2406));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        guest.Identity.PlayerName = GuestName;
        guest.LocalSeats = 2;
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            ctx.Check(guest.IsCoopGuest && guest.CoopSeats == 2 && host.CoopGuests.Select(g => g.Slot).SequenceEqual(new[] { 1, 2 }),
                $"the host seats the guest's two players side by side ({guest.CoopSeats} seat(s), slots {string.Join(",", host.CoopGuests.Select(g => g.Slot))})");

            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            ctx.Check(!host.CoopAllReady && guest.CoopReadyAt(0),
                $"ABLE-TO-FAIL CONTROL: the first player's Ready alone holds the launch ({string.Join(", ", host.CoopGuests.Select(g => g.Ready))})");
            guest.PickOf(1).Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            ctx.Check(host.CoopAllReady, $"both players Ready open the launch");
            if (!host.CoopAllReady)
            {
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out _);
            guestEnd = FollowSeats(ctx, mission, stock, guest, 2, hostEnd, host);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"both sessions build (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            var ends = new[] { hostEnd, guestEnd };
            var doors = new[] { host, guest };
            SkipAcross(ctx, "the host", hostEnd, ends, doors);
            Fly(SettleSteps, ends, doors);
            GuestSeatsFly(ctx, hostEnd, guestEnd, ends, doors);
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
        }
    }

    [Suite("net-coop-crash-return",
        "a co-op campaign mission under --no-crash-loss, host and one guest over a lossy loopback: "
        + "the guest's crash plays a wreck on the host, and its return from that crash brings the "
        + "host's copy out of the wreck once, at the pose and heading the guest returned at, after "
        + "which the copy flies on beside the guest")]
    internal static void DCoopGuestReturnsFromItsCrash(TestContext ctx)
    {
        var mission = Mission(ctx);
        var stock = StockLoadouts.Load();
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2407));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            if (!guest.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"the guest joins the host's campaign door and is Ready ({guest.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, host.BuildLaunch()!, HostAmmo, out _, keepFlying: true);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host, keepFlying: true);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"both sessions build (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            var ends = new[] { hostEnd, guestEnd };
            var doors = new[] { host, guest };
            SkipAcross(ctx, "the host", hostEnd, ends, doors);
            Fly(SettleSteps, ends, doors);
            GuestReturns(ctx, hostEnd, guestEnd, ends, doors);
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
        }
    }

    [Suite("net-coop-wingman",
        "a co-op campaign mission whose host profile flies its wingman in a picked aeroplane, host and "
        + "one guest over a lossy loopback: the host's launch names the pick to the guest, and the "
        + "guest builds the host-owned wingman in the same airframe, def, damage parts and fit as the "
        + "host rather than the Devastator a profile-less director binds, its guns firing the picked "
        + "ammunition over its AI def's own; every other roster block flies the same def and weapons "
        + "on both machines")]
    internal static void CCoopWingmanFliesTheHostsPick(TestContext ctx)
    {
        var mission = WingmanMission(ctx);
        var stock = StockLoadouts.Load();
        string store = Path.Combine(Path.GetTempPath(), $"csvm-coop-wingman-{System.Environment.ProcessId}");
        var profile = CampaignProfileDef.NewProfile(WingHost);
        profile.Planes[profile.WingmanPlane] = new OwnedPlane
        {
            Name = "Picked Wing",
            Airframe = WingAirframe,
            Ammo = (int[])WingAmmo.Clone(),
        };
        new CampaignProfileStore(store).Save(profile);

        var devastator = CampaignDirector.CoopWingmanOf(CampaignProfileDef.NewProfile(CampaignDirector.CoopGuestPilot));
        ctx.Check(devastator.Airframe == CoopGuestPick.StarterAirframe && devastator.Airframe != WingAirframe,
            $"ABLE-TO-FAIL CONTROL: a profile-less director's wingman is the Devastator, not the host's pick ({devastator.Airframe} vs {WingAirframe})");

        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2405));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, WingHost, 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            if (!guest.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"the guest joins the host's campaign door and is Ready ({guest.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, host.BuildLaunch()!, HostAmmo, out _, WingHost, store);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"both campaign sessions build (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            var told = guest.CoopWingman;
            ctx.Check(told is { Airframe: WingAirframe } named && named.Fit == CoopFit.Of(WingAmmo, null),
                $"the host's launch names its profile's wingman airframe and fit to the guest ({told?.Airframe}, {told?.Fit})");
            WingmanMatches(ctx, hostEnd, guestEnd);
            RosterMatches(ctx, hostEnd, guestEnd);
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
            if (Directory.Exists(store))
            {
                Directory.Delete(store, recursive: true);
            }
        }
    }

    [Suite("net-coop-persist-log",
        "CM02 (C3/M05) flown by a host whose profile's persist log holds CM01's dropped suspension "
        + "bridge, and two guests over a lossy loopback: the host's launch names the log to each guest, "
        + "the guest that takes it opens its world with the bridge down in the dropped pose before its "
        + "first step, as the host's opens, with no profile store of its own, while a guest built "
        + "without the log stands the bridge whole")]
    internal static void CCoopGuestOpensOnTheHostsPersistLog(TestContext ctx)
    {
        var mission = BridgeMission(ctx, BridgeLaterMission);
        var stock = StockLoadouts.Load();
        string store = Path.Combine(Path.GetTempPath(), $"csvm-coop-persist-{System.Environment.ProcessId}");
        var profile = CampaignProfileDef.NewProfile(LogHost);
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, BridgeChapter);
        var bridgeNodes = (ctx.Decode?.Gamez(gamezPath) ?? GameZ.Load(gamezPath)).Nodes
            .Where(n => n.Name.Equals(BridgeDef, StringComparison.OrdinalIgnoreCase)).Select(n => n.Index).ToArray();
        int? carriedSeq = CampaignSequence.PreviousInSameChapter(CampaignSequence.Load(ctx.ZrdrPath), mission.Seq)?.Seq;
        ctx.Check(bridgeNodes.Length > 0 && carriedSeq != null,
            $"the chapter gamez names the bridge and {BridgeChapter}/{BridgeLaterMission} has an earlier mission to carry it from (nodes {string.Join(",", bridgeNodes)}, seq {carriedSeq})");
        if (bridgeNodes.Length == 0 || carriedSeq is not { } earlier)
        {
            return;
        }

        profile.PersistLog.Merge(mission.Campaign, earlier,
            bridgeNodes.Select(n => new PersistedObject(n, BridgeDef, BridgeDef, true, 0f)));
        new CampaignProfileStore(store).Save(profile);

        var mesh = LoopbackTransport.Mesh(3, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2407));
        mesh[1].Disconnect(2);
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        var bare = Door(mesh[2]);
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        Ends? bareEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, LogHost, 1);
            guest.OpenJoin();
            bare.OpenJoin();
            Pump(SettleSteps, host, guest, bare);
            Select(host, mission);
            Pump(SettleSteps, host, guest, bare);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            bare.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest, bare);
            if (!guest.IsCoopGuest || !bare.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"both guests join the host's campaign door and are Ready ({guest.Stage}, {bare.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, host.BuildLaunch()!, HostAmmo, out _, LogHost, store);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host);
            bareEnd = Follow(ctx, mission, stock, bare, default, hostEnd, host, hostLog: false);
            if (!hostEnd.Built || !guestEnd.Built || !bareEnd.Built)
            {
                ctx.Check(false, $"all three campaign sessions build (host {hostEnd.Built}, guest {guestEnd.Built}, control {bareEnd.Built})");
                return;
            }

            ctx.Check(guest.CoopPersisted?.Count(s => s.Destroyed && bridgeNodes.Contains(s.Node)) == bridgeNodes.Length,
                $"the host's launch names the carried bridge to the guest ({guest.CoopPersisted?.Count ?? -1} state(s))");
            BridgeOpensDown(ctx, hostEnd, guestEnd, bareEnd);
        }
        finally
        {
            bareEnd?.Close();
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            bare.Discard();
            guest.Discard();
            host.Discard();
            if (Directory.Exists(store))
            {
                Directory.Delete(store, recursive: true);
            }
        }
    }

    [Suite("net-coop-chain-kill",
        "CM01 (C3/M01) flown by a host and one guest over a lossy loopback: the host's kill of the "
        + "fuel truck parked on the suspension bridge crosses as a pool event, and the bridge its chain "
        + "reaction drops by animation, which no pool event carries, falls on the guest too in its own "
        + "replay of the truck's death, so both machines end with the same pools destroyed")]
    internal static void CCoopChainKillFallsOnEveryMachine(TestContext ctx)
    {
        var mission = BridgeMission(ctx, BridgeEarlierMission);
        var stock = StockLoadouts.Load();
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2408));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            if (!guest.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"the guest joins the host's campaign door and is Ready ({guest.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out _);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"both campaign sessions build (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            ChainKillMatches(ctx, new[] { hostEnd, guestEnd }, new[] { host, guest });
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
        }
    }

    [Suite("net-coop-guest-shot-kill",
        "CM01 (C3/M01) flown by a host and one guest over a lossy loopback: the guest's own guns fire "
        + "on a grass hut, a PERSIST_LOG object, the host decides the kill from the rounds it rebuilds "
        + "and credits the guest's seat, and both machines end with the same pools destroyed, the hut "
        + "in one destroyed pose, and the same persist-log capture, the one the host commits on a win")]
    internal static void CCoopGuestShotKillsOnEveryMachine(TestContext ctx) =>
        CoopPair(ctx, MissionAt(ctx, BridgeChapter, BridgeEarlierMission), 2409, GuestShotKillMatches);

    [Suite("net-coop-scripted-break",
        "CM13 (C2/M03) flown by a host and one guest over a lossy loopback: OBJECTIVE1's WAKE_ANIM "
        + "blows the gate open by animation, which spends no pool and so sends no pool event, and the "
        + "guest's replay of the host's wake breaks it too, so both machines end with the same pools "
        + "destroyed and the gate in one pose")]
    internal static void CCoopScriptedBreakFallsOnEveryMachine(TestContext ctx) =>
        CoopPair(ctx, MissionAt(ctx, GateChapter, GateMission), 2410, ScriptedBreakMatches);

    [Suite("net-coop-direct-kill",
        "CM01 (C3/M01) flown by a host and one guest over a lossy loopback: the host's own hit kills "
        + "the suspension bridge, a PERSIST_LOG object, the kill crosses as a pool event, and the "
        + "guest runs the bridge's death once, ending with the host's pools destroyed, the bridge in "
        + "the host's dropped pose, and the same persist-log capture")]
    internal static void CCoopDirectKillFallsOnEveryMachine(TestContext ctx) =>
        CoopPair(ctx, MissionAt(ctx, BridgeChapter, BridgeEarlierMission), 2411, DirectKillMatches);

    [Suite("net-coop-cabbie-delivered",
        "CM21 flown by a host and one guest over a lossy loopback: the host's wake of OBJECTIVE28 "
        + "puts the Cabbie (autogyro_1) in play on both machines, and once the host's Cabbie is "
        + "within 1000 m of OBJECTIVE20's point the host's director completes it and removes him, "
        + "and the guest's copy leaves the world off the host's presence event while the guest's "
        + "own director, which evaluates nothing, removes nothing")]
    internal static void DCoopCabbieLeavesEveryMachine(TestContext ctx)
    {
        var mission = CabbieMission(ctx);
        var stock = StockLoadouts.Load();
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2406));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            if (!guest.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"the guest joins the host's campaign door and is Ready ({guest.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out _);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"both campaign sessions build (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            CabbieLeaves(ctx, hostEnd, guestEnd, host, guest);
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
        }
    }

    [Suite("net-coop-intro-field",
        "CM09 flown by a host and one guest over a lossy loopback, its bespoke opening film played "
        + "out on both machines with the world's animation stepped: the film's wingman prop "
        + "(piratefighter) flies in it on each machine, and once the film hands off, neither machine "
        + "still draws that prop, every drawn human aeroplane is one of the two seats, and each seat "
        + "flies on rather than hanging where the film left it")]
    internal static void ECoopIntroLeavesNoStrayAeroplane(TestContext ctx)
    {
        var mission = IntroMission(ctx);
        var stock = StockLoadouts.Load();
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(2407));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        var report = new System.Text.StringBuilder();
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            if (!guest.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"the guest joins the host's campaign door and is Ready ({guest.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out _);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"both campaign sessions build (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            IntroHandsOffClean(ctx, new[] { hostEnd, guestEnd }, new[] { host, guest }, report);
        }
        finally
        {
            ctx.WriteArtifact("test-net-coop-intro-field.txt", report.ToString());
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
        }
    }

    // A host and one guest on one campaign mission over a lossy loopback, both sessions built and
    // handed to a leg. Neither has stepped its world yet.
    private static void CoopPair(TestContext ctx, CampaignMission mission, int seed,
        Action<TestContext, Ends[], NetPlayFeature[]> leg)
    {
        var stock = StockLoadouts.Load();
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(seed));
        var host = Door(mesh[0]);
        var guest = Door(mesh[1]);
        var ambient = NetCombatSuites.Ambient.Save();
        Ends? hostEnd = null;
        Ends? guestEnd = null;
        try
        {
            host.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
            host.Offer(mission.Seq, "Host", 1);
            guest.OpenJoin();
            Pump(SettleSteps, host, guest);
            Select(host, mission);
            Pump(SettleSteps, host, guest);
            guest.Pick.Set(CoopGuestPick.StarterAirframe, true);
            Pump(SettleSteps, host, guest);
            if (!guest.IsCoopGuest || !host.CoopAllReady)
            {
                ctx.Check(false, $"the guest joins the host's campaign door and is Ready ({guest.Stage}, ready {host.CoopAllReady})");
                return;
            }

            hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out _);
            guestEnd = Follow(ctx, mission, stock, guest, default, hostEnd, host);
            if (!hostEnd.Built || !guestEnd.Built)
            {
                ctx.Check(false, $"both campaign sessions build (host {hostEnd.Built}, guest {guestEnd.Built})");
                return;
            }

            leg(ctx, new[] { hostEnd, guestEnd }, new[] { host, guest });
        }
        finally
        {
            guestEnd?.Close();
            hostEnd?.Close();
            ambient.Restore();
            guest.Discard();
            host.Discard();
        }
    }

    // Both machines play the film to its own end, then fly on. What each machine draws afterwards
    // is read off its own tree, since the stray was seen on one machine alone.
    private static void IntroHandsOffClean(TestContext ctx, Ends[] ends, NetPlayFeature[] doors,
        System.Text.StringBuilder report)
    {
        var runtimes = ends.Select(e => e.Session.Wire.World?.World).ToArray();
        var props = runtimes.Select(r => r?.FindNodes(AircraftStage.PropNode) is { Count: > 0 } found ? found[0] : null)
            .ToArray();
        if (props.Any(p => p == null))
        {
            ctx.Check(false, $"both machines stage the intro's '{AircraftStage.PropNode}' in a world runtime ({props.Count(p => p != null)} of {ends.Length})");
            return;
        }

        ctx.Check(ends.All(e => e.Session.Cutscene is { Playing: true, Anim: IntroFilm }),
            $"ABLE-TO-FAIL CONTROL: both machines open on '{IntroFilm}' ({string.Join(", ", ends.Select(e => e.Session.Cutscene?.Anim))})");
        var flown = new float[ends.Length];
        int steps = 0;
        for (; steps < IntroSteps && ends.Any(e => e.Session.Cutscene is { Playing: true }); steps++)
        {
            FlyAnimated(ends, doors, runtimes);
            for (int e = 0; e < ends.Length; e++)
            {
                if (props[e]!.IsVisibleInTree())
                {
                    flown[e] = Mathf.Max(flown[e], AnimRuntime.WorldTransform(props[e]!, out _).Origin.Length());
                }
            }
        }

        report.AppendLine($"film over after {steps} step(s); the prop drawn up to {string.Join(" / ", flown.Select(f => $"{f:0} m"))} off the origin");
        ctx.Check(ends.All(e => e.Session.Cutscene is not { Playing: true }),
            $"the film hands off by itself on both machines ({steps} step(s))");
        ctx.Check(flown.All(f => f > PropFlownM),
            $"ABLE-TO-FAIL CONTROL: the film's wingman prop flies in it on both machines ({string.Join(" / ", flown.Select(f => $"{f:0} m"))} off the origin)");

        Fly(SettleSteps, ends, doors);
        var from = ends.Select(Drawn).ToArray();
        for (int i = 0; i < TravelSteps; i++)
        {
            FlyAnimated(ends, doors, runtimes);
        }

        for (int e = 0; e < ends.Length; e++)
        {
            string who = e == 0 ? "host" : "guest";
            var seats = ends[e].Session.SeatRigs.Select(r => r.Controller).ToArray();
            var humans = Drawn(ends[e]).Where(d => d.Rig.IsHumanPiloted).ToArray();
            var travel = humans.Select(h => from[e].FirstOrDefault(f => ReferenceEquals(f.Rig, h.Rig)).At.DistanceTo(h.At))
                .ToArray();
            string reading = string.Join(", ", humans.Select((h, i) =>
                $"{h.Rig.Name} seat {Array.IndexOf(seats, h.Rig)} {travel[i]:0} m"));
            report.AppendLine($"{who}: prop drawn {props[e]!.IsVisibleInTree()} at {AnimRuntime.WorldTransform(props[e]!, out _).Origin}; {reading}");
            ctx.Check(!props[e]!.IsVisibleInTree(),
                $"the {who} draws no '{AircraftStage.PropNode}' once the film has handed off (at {AnimRuntime.WorldTransform(props[e]!, out _).Origin})");
            ctx.Check(humans.Length == seats.Length && humans.All(h => seats.Contains(h.Rig)),
                $"every human aeroplane the {who} draws is one of its {seats.Length} seats ({reading})");
            ctx.Check(travel.All(t => t > SeatTravelM),
                $"and each flies on over {TravelSteps} steps on the {who}, none hanging where the film left it ({reading})");
        }
    }

    // Every FlightController drawn in one machine's tree, with where it is drawn.
    private static List<(FlightController Rig, Vector3 At)> Drawn(Ends end)
    {
        var found = new List<(FlightController, Vector3)>();
        var stack = new Stack<Node>();
        stack.Push(end.Session);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is FlightController { PlaneModel.Visible: true } rig && rig.IsVisibleInTree())
            {
                found.Add((rig, rig.GlobalTransform.Origin));
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return found;
    }

    // A step on every end as a frame runs it: the fixed step, the door, the world's animation and
    // the cutscene host last. The animation and the cutscene tick belong to the frame, not the step.
    private static void FlyAnimated(Ends[] ends, NetPlayFeature[] doors, AnimRuntime?[] runtimes)
    {
        for (int e = 0; e < ends.Length; e++)
        {
            ends[e].Session._PhysicsProcess(GameClock.FixedDt);
            doors[e].Step(GameClock.FixedDt);
            runtimes[e]?.Advance(GameClock.FixedDt);
            ends[e].Session.Cutscene?.Tick();
        }
    }

    private static CampaignMission IntroMission(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        var mission = CampaignSequence.Load(ctx.ZrdrPath).Cast<CampaignMission?>()
            .FirstOrDefault(m => m!.Value.ChapterFolder.Equals("C1", StringComparison.OrdinalIgnoreCase)
                                 && m.Value.MissionFolder.Equals("M04", StringComparison.OrdinalIgnoreCase))
            ?? throw new SuiteSkippedException($"cm_sequence carries no C1/M04");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, "C1"), $"C1 textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, "C1"), $"C1 gamez");
        return mission;
    }


    // The host alone decides the delivery. Its removal is a presence change, which is the one
    // message that takes the guest's copy out.
    private static void CabbieLeaves(TestContext ctx, Ends hostEnd, Ends guestEnd, NetPlayFeature hostDoor,
        NetPlayFeature guestDoor)
    {
        var ends = new[] { hostEnd, guestEnd };
        var doors = new[] { hostDoor, guestDoor };
        if (ends.Any(e => e.Session.Cutscene is { Playing: true }))
        {
            hostEnd.Session.Cutscene?.Skip();
            for (int i = 0; i < SkipWindowSteps && ends.Any(e => e.Session.Cutscene is { Playing: true }); i++)
            {
                FlyOnce(ends, doors);
            }
        }

        // A step on each end admits the roster, which is what hands the guest's copies to the host.
        // The humans then hold where they stand, so nothing but the script moves during the legs.
        Fly(SettleSteps, ends, doors);
        foreach (var end in ends)
        {
            foreach (var rig in end.Session.Rigs)
            {
                if (rig.Controller is { } pilot)
                {
                    pilot.Held = true;
                }
            }
        }

        var hostDirector = hostEnd.Session.Campaign;
        var guestDirector = guestEnd.Session.Campaign;
        var mine = hostDirector?.Roster.GetValueOrDefault(Cabbie);
        var theirs = guestDirector?.Roster.GetValueOrDefault(Cabbie);
        if (hostDirector?.Graph is not { } graph || guestDirector?.Graph is not { } replica
            || mine == null || theirs == null)
        {
            ctx.Check(false, $"both machines spawn '{Cabbie}' under a campaign director (host {mine != null}, guest {theirs != null})");
            return;
        }

        ctx.Check(mine.Inert && theirs.Inert && theirs.RemoteOwned && !replica.CompletedOf(CabbieDelivered),
            $"'{Cabbie}' ships deactivated on both machines, the guest's copy owned by the host (remote {theirs.RemoteOwned})");
        graph.Wake(CabbieWake);
        int steps = 0;
        for (; steps < CabbieTaxiSteps && (theirs.Inert || hostDirector.Paths?.IsPlaced(Cabbie) == true); steps++)
        {
            FlyOnce(ends, doors);
        }

        ctx.Check(mine.InPlay && theirs.InPlay && !graph.CompletedOf(CabbieDelivered),
            $"ABLE-TO-FAIL CONTROL: the host's wake puts him in play on both machines and off his taxi path, undelivered ({steps} step(s))");

        var delivery = hostDirector.Script.Objectives.FirstOrDefault(d => d.Number == CabbieDelivered)?.Travelers;
        if (delivery?.WherePoint is not { Length: 3 } p || !delivery.DeleteOnSuccess)
        {
            throw new SuiteSkippedException($"C5/M01 OBJECTIVE{CabbieDelivered} no longer authors the Cabbie's delivery");
        }

        mine.WarpTo(new Vector3(p[0], p[1], p[2]), 0f, 0f);
        mine.Held = true;
        steps = 0;
        // A lossy link may deliver the removal and the replayed objective steps apart. The window
        // therefore waits on every condition the checks below read.
        for (; steps < SkipWindowSteps && (!graph.CompletedOf(CabbieDelivered) || !theirs.Inert || !replica.CompletedOf(CabbieDelivered));
             steps++)
        {
            FlyOnce(ends, doors);
        }

        ctx.Check(graph.CompletedOf(CabbieDelivered) && mine.Deactivated && hostDirector.Removed.Contains(Cabbie),
            $"on the point the host completes OBJECTIVE{CabbieDelivered} and removes him");
        ctx.Check(theirs.Inert && replica.CompletedOf(CabbieDelivered),
            $"and the guest's copy leaves the world with the host's objective replayed ({steps} step(s))");
        ctx.Check(guestDirector.Removed.Count == 0,
            $"…taken out by the host's word, never by a removal the guest's own director decided");
    }

    private static CampaignMission CabbieMission(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        var mission = CampaignSequence.Load(ctx.ZrdrPath).Cast<CampaignMission?>()
            .FirstOrDefault(m => m!.Value.ChapterFolder.Equals("C5", StringComparison.OrdinalIgnoreCase)
                                 && m.Value.MissionFolder.Equals("M01", StringComparison.OrdinalIgnoreCase))
            ?? throw new SuiteSkippedException($"cm_sequence carries no C5/M01");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, "C5"), $"C5 textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, "C5"), $"C5 gamez");
        return mission;
    }

    // The host-owned wingman on both machines: the picked airframe, one def, one set of damage
    // parts and one fit. The host's own copy is the able-to-fail control that the profile was read.
    private static void WingmanMatches(TestContext ctx, Ends host, Ends guest)
    {
        string want = Flight.Hangar.StockAirframes.Node(WingAirframe);
        var hostWing = host.Session.Campaign?.Roster.GetValueOrDefault(CampaignDirector.WingmanName);
        var guestWing = guest.Session.Campaign?.Roster.GetValueOrDefault(CampaignDirector.WingmanName);
        ctx.Check(host.Session.Campaign?.WingmanNode == want && hostWing != null,
            $"ABLE-TO-FAIL CONTROL: the host flies its wingman as its profile's pick {want} ({host.Session.Campaign?.WingmanNode}, spawned {hostWing != null})");
        ctx.Check(guest.Session.Campaign?.WingmanNode == want && guestWing != null,
            $"the guest binds the host's pick {want} for the wingman, not a default ({guest.Session.Campaign?.WingmanNode}, spawned {guestWing != null})");
        if (hostWing == null || guestWing == null)
        {
            return;
        }

        string mine = Airframe(hostWing);
        string theirs = Airframe(guestWing);
        ctx.Check(mine == theirs,
            $"the guest's wingman carries the host's def, damage parts and fit (host {mine} | guest {theirs})");

        // The pick, not the def's own block: the AI def's gun is a wep_130, the pick a matrix id.
        var picked = StockLoadouts.Load().ForModel(want)?.Guns
            .Select(g => StockLoadouts.GunWeaponId(g.Caliber, CampaignLoadout.AmmoNames[WingAmmo[g.Slot - 1]]))
            .ToArray() ?? Array.Empty<string>();
        var flown = hostWing.Loadout?.Guns.Select(g => g.Weapon.Id).ToArray() ?? Array.Empty<string>();
        ctx.Check(picked.Length > 0 && flown.SequenceEqual(picked),
            $"the host's wingman fires the picked ammunition [{string.Join(",", picked)}] rather than its AI def's guns ([{string.Join(",", flown)}])");
    }

    // Every block both machines spawned flies one def on both, so no other host-owned aeroplane
    // takes a default on the guest either.
    private static void RosterMatches(TestContext ctx, Ends host, Ends guest)
    {
        var mine = host.Session.Campaign?.Roster ?? new Dictionary<string, FlightController>();
        var theirs = guest.Session.Campaign?.Roster ?? new Dictionary<string, FlightController>();
        var differ = mine.Keys.Where(theirs.ContainsKey)
            .Where(name => Airframe(mine[name]) != Airframe(theirs[name]))
            .Select(name => $"{name}: {Airframe(mine[name])} vs {Airframe(theirs[name])}")
            .ToArray();
        ctx.Check(mine.Count > 1 && mine.Keys.All(theirs.ContainsKey) && differ.Length == 0,
            $"all {mine.Count} roster block(s) spawn on the guest with the host's def ({theirs.Count} there; {string.Join("; ", differ)})");
    }

    // One aeroplane's identity as hits and damage see it: its def, its AI def, its damage parts and
    // their pools. Then the weapon every gun and pylon it bound fires.
    private static string Airframe(FlightController rig)
    {
        string parts = rig.Damage is { } damage
            ? string.Join(",", damage.Parts.Keys.OrderBy(k => k, StringComparer.Ordinal))
              + Log.Format($"@{damage.WholeHealthMax:0.#}/{damage.WholeArmorMax:0.#}")
            : "none";
        string weapons = rig.Loadout is { } loadout
            ? string.Join(",", loadout.Guns.Select(g => g.Weapon.Id).Concat(loadout.Hardpoints.Select(h => $"{h.Index}:{h.Weapon.Id}")))
            : "";
        return $"{rig.Stats?.DefName}/{rig.Stats?.AiDefName}/{parts}/{weapons}";
    }

    private static CampaignMission WingmanMission(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        var mission = CampaignSequence.Load(ctx.ZrdrPath).Cast<CampaignMission?>()
            .FirstOrDefault(m => m!.Value.ChapterFolder.Length > 0 && m.Value.Wingman)
            ?? throw new SuiteSkippedException($"cm_sequence carries no wingman mission with a chapter");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, chapter), $"{chapter} gamez");
        return mission;
    }

    private static CampaignMission BridgeMission(TestContext ctx, string folder) =>
        MissionAt(ctx, BridgeChapter, folder);

    private static CampaignMission MissionAt(TestContext ctx, string chapter, string folder)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        var mission = CampaignSequence.Load(ctx.ZrdrPath).Cast<CampaignMission?>()
            .FirstOrDefault(m => m!.Value.ChapterFolder.Equals(chapter, StringComparison.OrdinalIgnoreCase)
                                 && m.Value.MissionFolder.Equals(folder, StringComparison.OrdinalIgnoreCase))
            ?? throw new SuiteSkippedException($"cm_sequence carries no {chapter}/{folder}");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, chapter), $"{chapter} gamez");
        return mission;
    }

    // Read as the sessions stand built, before any machine has stepped its world once.
    private static void BridgeOpensDown(TestContext ctx, Ends host, Ends guest, Ends bare)
    {
        var (hostBridge, hostDown) = Bridge(host);
        var (guestBridge, guestDown) = Bridge(guest);
        var (bareBridge, bareDown) = Bridge(bare);
        string reading = $"host {hostBridge?.Status}/down {hostDown}, guest {guestBridge?.Status}/down {guestDown}, control {bareBridge?.Status}/down {bareDown}";
        ctx.Check(hostBridge is { Status: DestructibleRegistry.State.Destroyed } && hostDown,
            $"the host opens {BridgeLaterMission} with the bridge down off its own profile's log ({reading})");
        ctx.Check(guestBridge is { Status: DestructibleRegistry.State.Destroyed, Health: <= 0f } && guestDown,
            $"the guest opens {BridgeLaterMission} with the bridge down in the dropped pose before its first step ({reading})");
        ctx.Check(bareBridge is { Status: DestructibleRegistry.State.Healthy } && !bareDown,
            $"ABLE-TO-FAIL CONTROL: a guest built without the host's log stands the bridge whole ({reading})");
        ctx.Check(guest.Session.Campaign is { HasStore: false },
            $"the guest's director takes the log with no profile store to write it to");
    }

    // The bridge's live pool, the chapter's one, and whether the deck's dropped pieces are hidden.
    private static (DestructibleRegistry.Instance? Pool, bool Down) Bridge(Ends end)
    {
        if (end.Session.Wire.World?.World is not { } runtime || FirstPool(runtime, BridgeDef) is not { } pool)
        {
            return (null, false);
        }

        bool Hidden(string piece) => runtime.FindNodes(piece, pool.Anchor).Any(n => !n.Visible);
        return (runtime.Destructibles.Resolve(pool.Anchor) ?? pool, Hidden("rope1") && Hidden("part5"));
    }

    private static DestructibleRegistry.Instance? FirstPool(AnimRuntime runtime, string def) =>
        runtime.Destructibles.All.FirstOrDefault(i => i.Def.Name.Equals(def, StringComparison.OrdinalIgnoreCase));

    // The live path. The truck's death crosses as a pool event. No pool event carries the bridge kill
    // its chain reaction makes by animation, so the guest's bridge falls only in its own replay.
    private static void ChainKillMatches(TestContext ctx, Ends[] ends, NetPlayFeature[] doors)
    {
        Fly(SettleSteps, ends, doors);
        var runtimes = ends.Select(e => e.Session.Wire.World?.World).ToArray();
        if (runtimes[0] is not { } hostWorld || runtimes[1] is not { } guestWorld)
        {
            ctx.Check(false, $"both machines carry a world runtime");
            return;
        }

        var (standing, _) = Bridge(ends[1]);
        ctx.Check(standing is { Status: DestructibleRegistry.State.Healthy },
            $"ABLE-TO-FAIL CONTROL: the guest's bridge stands before the host's kill ({standing?.Status})");
        var truck = hostWorld.Destructibles.All.FirstOrDefault(i => i.Def.Name.Equals(BridgeTruckDef, StringComparison.OrdinalIgnoreCase));
        int before = DestroyedPools(hostWorld).Count;
        bool struck = truck != null && hostWorld.DamageAt(truck.Anchor, truck.MaxHealth + 1f);
        // The chain's calls are staggered in the world's animation, which a frame steps (INSTR-97).
        for (int i = 0; i < ChainSteps; i++)
        {
            FlyAnimated(ends, doors, runtimes);
        }

        var hostDead = DestroyedPools(hostWorld);
        var guestDead = DestroyedPools(guestWorld);
        var (hostBridge, hostDown) = Bridge(ends[0]);
        var (guestBridge, guestDown) = Bridge(ends[1]);
        string reading = $"host {hostDead.Count} destroyed ({before} before), guest {guestDead.Count}; host only [{string.Join(", ", hostDead.Except(guestDead))}], guest only [{string.Join(", ", guestDead.Except(hostDead))}]; bridge host {hostBridge?.Status}/down {hostDown}, guest {guestBridge?.Status}/down {guestDown}";
        ctx.Check(struck && hostBridge is { Status: DestructibleRegistry.State.Destroyed },
            $"the host's kill of {BridgeTruckDef} drops the bridge by its chain reaction ({reading})");
        ctx.Check(guestBridge is { Status: DestructibleRegistry.State.Destroyed } && guestDown == hostDown,
            $"the guest's bridge drops too, in its own replay of the truck's death, in the host's pose ({reading})");
        ctx.Check(hostDead.SetEquals(guestDead),
            $"both machines end the chain with the same pools destroyed ({reading})");
        ctx.Note($"live chain kill: {reading}");
    }

    private static HashSet<string> DestroyedPools(AnimRuntime world) =>
        world.Destructibles.All
            .Where(i => (world.Destructibles.Resolve(i.Anchor) ?? i).Status == DestructibleRegistry.State.Destroyed)
            .Select(i => $"{i.Def.Name}@{i.Anchor.Name}")
            .ToHashSet();

    // The guest's own guns on a grass hut. Its rounds spend nothing on its own world, so the host's
    // rebuild of them decides the kill, and the pool event carries it back.
    private static void GuestShotKillMatches(TestContext ctx, Ends[] ends, NetPlayFeature[] doors)
    {
        var runtimes = ends.Select(e => e.Session.Wire.World?.World).ToArray();
        SkipFilm(ends, doors, runtimes);
        Fly(SettleSteps, ends, doors);
        var gunner = ends[1].Session.SeatRigs[1].Controller;
        var copy = ends[0].Session.SeatRigs[1].Controller;
        if (runtimes[0] is not { } hostWorld || runtimes[1] is not { } guestWorld || gunner == null || copy == null)
        {
            ctx.Check(false, $"both machines carry a world runtime and the guest's seat");
            return;
        }

        // The first standing hut a ray reaches, and the host's pool at the same index. Hut names repeat.
        (Vector3 From, Vector3 At)? aim = null;
        var guestPool = guestWorld.Destructibles.All.FirstOrDefault(i =>
            i.Def.Name.StartsWith(HutDef, StringComparison.OrdinalIgnoreCase) && i.Def.PersistLog && !i.Dormant
            && Live(guestWorld, i).Status == DestructibleRegistry.State.Healthy && (aim = Aim(guestWorld, i, gunner)) != null);
        var hostPool = guestPool == null ? null
            : hostWorld.Destructibles.All.ElementAtOrDefault(guestWorld.Destructibles.All.ToList().IndexOf(guestPool));
        ctx.Check(aim != null && hostPool != null
                  && NetWorldLink.PoolKey(hostPool) == NetWorldLink.PoolKey(guestPool!),
            $"a standing PERSIST_LOG grass hut stands in the guest's sights, with its pool on the host ({guestPool?.Anchor.Name})");
        if (aim is not { } perch || guestPool == null || hostPool == null)
        {
            return;
        }

        var hostLive = Live(hostWorld, hostPool);
        string standing = Pose(guestPool);
        ctx.Check(hostLive.Status == DestructibleRegistry.State.Healthy
                  && Live(guestWorld, guestPool).Status == DestructibleRegistry.State.Healthy,
            $"ABLE-TO-FAIL CONTROL: the hut stands on both machines before the guest fires");
        gunner.Held = true;
        gunner.PlaceHeld(perch.From, perch.At);
        gunner.AutoFire = true;
        int steps = 0;
        for (; steps < ShotSteps && hostLive.Status != DestructibleRegistry.State.Destroyed; steps++)
        {
            FlyAnimated(ends, doors, runtimes);
        }

        gunner.AutoFire = false;
        // A guest that spent its own rounds would stamp its seat on its own pool before the event came.
        var guestLive = Live(guestWorld, guestPool);
        ctx.Check(guestWorld.DamageReplicated && guestLive.LastShooter != gunner.PlayerIndex,
            $"the guest's own rounds spend nothing on its hut, which dies only off the host's pool event (shooter {guestLive.LastShooter}, the guest's seat {gunner.PlayerIndex})");
        ctx.Check(hostLive.Status == DestructibleRegistry.State.Destroyed && hostLive.LastShooter == copy.PlayerIndex,
            $"the guest's rounds bring the hut down on the host, credited to the guest's seat, after {steps} step(s) ({hostLive.Status}, shooter {hostLive.LastShooter} of {copy.PlayerIndex})");
        for (int i = 0; i < HutDeathSteps; i++)
        {
            FlyAnimated(ends, doors, runtimes);
        }

        SameWorld(ctx, "the guest's kill", new[] { hostWorld, guestWorld }, new[] { hostPool, guestPool }, standing);
    }

    // A break no pool spend makes: the host's objective wakes the gate's own death definition. Only
    // the guest's replay of that wake can break its copy.
    private static void ScriptedBreakMatches(TestContext ctx, Ends[] ends, NetPlayFeature[] doors)
    {
        var runtimes = ends.Select(e => e.Session.Wire.World?.World).ToArray();
        var pools = runtimes.Select(r => r == null ? null : FirstPool(r, GateDef)).ToArray();
        if (runtimes[0] is not { } hostWorld || runtimes[1] is not { } guestWorld || pools[0] is not { } hostPool
            || pools[1] is not { } guestPool)
        {
            ctx.Check(false, $"both machines register the '{GateDef}' pool");
            return;
        }

        var hostLive = Live(hostWorld, hostPool);
        var guestLive = Live(guestWorld, guestPool);
        string standing = Pose(guestPool);
        ctx.Check(guestLive.Status == DestructibleRegistry.State.Healthy && hostLive.Status == DestructibleRegistry.State.Healthy,
            $"ABLE-TO-FAIL CONTROL: the gate stands on both machines before the host's objective breaks it (host {hostLive.Status}, guest {guestLive.Status})");
        int sent = 0;
        void Count(DestructibleRegistry.Instance inst) => sent += ReferenceEquals(inst, hostLive) ? 1 : 0;
        hostWorld.DestructibleDamaged += Count;
        try
        {
            SkipFilm(ends, doors, runtimes);
            int steps = 0;
            for (; steps < GateWakeSteps && hostLive.Status != DestructibleRegistry.State.Destroyed; steps++)
            {
                FlyAnimated(ends, doors, runtimes);
            }

            ctx.Check(hostLive.Status == DestructibleRegistry.State.Destroyed && sent == 0,
                $"the host's objective breaks the gate by animation with no pool event after {steps} step(s) ({hostLive.Status}, {sent} event(s))");
            for (int i = 0; i < ChainSteps; i++)
            {
                FlyAnimated(ends, doors, runtimes);
            }
        }
        finally
        {
            hostWorld.DestructibleDamaged -= Count;
        }

        SameWorld(ctx, "the objective's break", new[] { hostWorld, guestWorld }, new[] { hostPool, guestPool }, standing);
    }

    // The host's own round on a PERSIST_LOG object. The pool event is the guest's only word of it,
    // and its death must run there once, not again from a chain.
    private static void DirectKillMatches(TestContext ctx, Ends[] ends, NetPlayFeature[] doors)
    {
        Fly(SettleSteps, ends, doors);
        var runtimes = ends.Select(e => e.Session.Wire.World?.World).ToArray();
        var pools = runtimes.Select(r => r == null ? null : FirstPool(r, BridgeDef)).ToArray();
        if (runtimes[0] is not { } hostWorld || runtimes[1] is not { } guestWorld || pools[0] is not { } hostPool
            || pools[1] is not { } guestPool)
        {
            ctx.Check(false, $"both machines register the bridge's pool");
            return;
        }

        var hostLive = Live(hostWorld, hostPool);
        var guestLive = Live(guestWorld, guestPool);
        string standing = Pose(guestPool);
        ctx.Check(guestLive.Status == DestructibleRegistry.State.Healthy && hostLive.Def.PersistLog,
            $"ABLE-TO-FAIL CONTROL: the guest's bridge stands before the host's hit, and its def is PERSIST_LOG ({guestLive.Status}, {hostLive.Def.PersistLog})");
        int deaths = 0;
        void Count(DestructibleRegistry.Instance inst) =>
            deaths += ReferenceEquals(inst, guestLive) && inst.Status == DestructibleRegistry.State.Destroyed ? 1 : 0;
        guestWorld.DestructibleDamaged += Count;
        try
        {
            bool struck = hostWorld.DamageAt(hostLive.DamageNode, hostLive.MaxHealth + 1f,
                ends[0].Session.SeatRigs[0].Controller?.PlayerIndex ?? -1);
            ctx.Check(struck && hostLive.Status == DestructibleRegistry.State.Destroyed,
                $"the host's own hit kills the bridge there ({hostLive.Status})");
            for (int i = 0; i < ChainSteps; i++)
            {
                FlyAnimated(ends, doors, runtimes);
            }
        }
        finally
        {
            guestWorld.DestructibleDamaged -= Count;
        }

        ctx.Check(deaths == 1, $"the guest runs the bridge's death once off the pool event ({deaths} death(s))");
        SameWorld(ctx, "the host's kill", new[] { hostWorld, guestWorld }, new[] { hostPool, guestPool }, standing);
    }

    // Both machines once a break has settled: the same pools destroyed, and the object in one pose
    // that is not its standing one. The capture matches too, the one the host commits on a win.
    private static void SameWorld(TestContext ctx, string what, AnimRuntime[] worlds,
        DestructibleRegistry.Instance[] pools, string standing)
    {
        var dead = worlds.Select(DestroyedPools).ToArray();
        var poses = pools.Select(Pose).ToArray();
        var logs = worlds.Select(w => string.Join(";", CampaignPersistLog.Capture(w)
            .Select(s => Log.Format($"{s.Node}:{s.Destroyed}:{s.Health:0.##}")).OrderBy(s => s, StringComparer.Ordinal))).ToArray();
        string reading = $"host {dead[0].Count} destroyed, guest {dead[1].Count}; host only [{string.Join(", ", dead[0].Except(dead[1]))}], guest only [{string.Join(", ", dead[1].Except(dead[0]))}]; hidden host [{poses[0]}], guest [{poses[1]}]; capture host [{logs[0]}], guest [{logs[1]}]";
        ctx.Check(worlds.Zip(pools).All(p => Live(p.First, p.Second).Status == DestructibleRegistry.State.Destroyed),
            $"{what} leaves the object destroyed on both machines ({reading})");
        ctx.Check(poses[0] == poses[1] && poses[1] != standing,
            $"in one pose on both machines, not the standing one [{standing}] ({reading})");
        ctx.Check(dead[0].SetEquals(dead[1]), $"both machines hold the same pools destroyed ({reading})");
        ctx.Check(logs[0] == logs[1] && (!pools[0].Def.PersistLog || logs[0].Length > 0),
            $"and the same persist-log capture, the object in it when its def persists ({reading})");
        ctx.Note($"{what}: {reading}");
    }

    private static DestructibleRegistry.Instance Live(AnimRuntime world, DestructibleRegistry.Instance pool) =>
        world.Destructibles.Resolve(pool.Anchor) ?? pool;

    // Every node under the pool's anchor that is switched off, sorted: the pose a death leaves.
    private static string Pose(DestructibleRegistry.Instance pool) =>
        string.Join(",", Under(pool.Anchor).OfType<Node3D>().Where(n => !n.Visible).Select(n => n.Name.ToString())
            .OrderBy(n => n, StringComparer.Ordinal));

    // A point on a collider the pool answers for, and a perch back along the ray that found it. The
    // rays cross the piece's drawn bounds on a grid, highest first, since its origin can sit inside
    // another object's collision.
    private static (Vector3 From, Vector3 At)? Aim(AnimRuntime runtime, DestructibleRegistry.Instance pool, Node3D inWorld)
    {
        if (inWorld.GetWorld3D()?.DirectSpaceState is not { } space)
        {
            return null;
        }

        var live = Live(runtime, pool);
        foreach (var body in Under(pool.Anchor).OfType<CollisionObject3D>())
        {
            if (!ReferenceEquals(runtime.Destructibles.Resolve(body), live)
                || body.GetParent() is not { } piece)
            {
                continue;
            }

            var boxes = Under(piece).OfType<VisualInstance3D>().Select(v => v.GlobalTransform * v.GetAabb()).ToArray();
            if (boxes.Length == 0)
            {
                continue;
            }

            var b = boxes.Aggregate((x, y) => x.Merge(y));
            foreach (var across in new[] { Vector3.Right, Vector3.Back, Vector3.Down })
            {
                for (int i = 7; i > 0; i--)
                {
                    for (int j = 1; j < 8; j++)
                    {
                        var at = b.Position + (b.Size * GridPoint(across, i / 8f, j / 8f));
                        var from = at - (across * PerchM);
                        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, at + (across * PerchM)));
                        if (hit.Count > 0 && ReferenceEquals(runtime.Destructibles.Resolve(hit["collider"].As<Node>()), live))
                        {
                            return (from, (Vector3)hit["position"]);
                        }
                    }
                }
            }
        }

        return null;
    }

    // A point inside a unit box: the two axes across the ray at u and v, the ray's own axis at its middle.
    private static Vector3 GridPoint(Vector3 across, float u, float v) =>
        across.X != 0f ? new Vector3(0.5f, u, v) : across.Z != 0f ? new Vector3(u, v, 0.5f) : new Vector3(u, 0.5f, v);

    private static IEnumerable<Node> Under(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    // The host's skip of a playing opening film, stepped until no machine still plays it.
    private static void SkipFilm(Ends[] ends, NetPlayFeature[] doors, AnimRuntime?[] runtimes)
    {
        ends[0].Session.Cutscene?.Skip();
        for (int i = 0; i < SkipWindowSteps && ends.Any(e => e.Session.Cutscene is { Playing: true }); i++)
        {
            FlyAnimated(ends, doors, runtimes);
        }
    }

    // The host's restart in the launcher's order: its session freed, the door's relaunch, the new
    // field on the same carrier. The guest then flies on until the new round reaches it, and
    // follows the way its menu does.
    private static (Ends Host, Ends Guest) Restart(TestContext ctx, CampaignMission mission, StockLoadouts stock,
        Ends hostEnd, Ends guestEnd, NetPlayFeature hostDoor, NetPlayFeature guestDoor)
    {
        hostEnd.Close();
        var launch = NetFlight.CoopRelaunch(hostDoor);
        ctx.Check(!NetFlight.CoopGuestFlightOver(guestDoor),
            $"ABLE-TO-FAIL CONTROL: the guest's flight goes on until the host's new round crosses the link");
        Ends? next = null;
        string threw = "";
        try
        {
            next = launch == null ? null : Launch(ctx, mission, stock, hostDoor, launch, HostAmmo, out _);
        }
        catch (InvalidOperationException e)
        {
            threw = e.Message;
        }

        ctx.Check(next is { Built: true } && threw.Length == 0,
            $"the host's restart builds its new flight on the lobby's carrier with no exception (launch {launch != null}, \"{threw}\")");
        if (next == null)
        {
            return (hostEnd, guestEnd);
        }

        // The host steps first, so its new session's first sends are on the wire while the guest's
        // old session is still bound. None of them may land in that session.
        int steps = 0;
        var hostPlane = next.Session.SeatRigs[0].Controller;
        var hostFrom = hostPlane?.GlobalPosition ?? Vector3.Zero;
        for (; steps < OpenerSteps && !NetFlight.CoopGuestFlightOver(guestDoor); steps++)
        {
            next.Session._PhysicsProcess(GameClock.FixedDt);
            hostDoor.Step(GameClock.FixedDt);
            guestEnd.Session._PhysicsProcess(GameClock.FixedDt);
            guestDoor.Step(GameClock.FixedDt);
        }

        ctx.Check(NetFlight.CoopGuestFlightOver(guestDoor) && guestDoor.Stage == NetDoorStage.Joined,
            $"the host's new round ends the guest's flight after {steps} step(s), its link still up ({guestDoor.Stage})");
        // The restarted mission waits for the guest's new world, so the host flies nowhere meanwhile.
        float held = hostPlane == null ? -1f : hostPlane.GlobalPosition.DistanceTo(hostFrom);
        ctx.Check(next.Session.StartHeld && held is >= 0f and < 0.01f,
            $"the host's restarted mission holds its start while the guest's new world is unbuilt ({next.Session.StartHeld}, {held:0.00} m)");
        guestDoor.LeaveCoopMission();
        guestEnd.Close();
        guestDoor.Reclaim();
        var again = Follow(ctx, mission, stock, guestDoor, default, next, hostDoor);
        ctx.Check(again.Built && guestDoor.CoopFlow?.MissionSeq == mission.Seq,
            $"the guest follows into the restarted mission (built {again.Built}, seq {guestDoor.CoopFlow?.MissionSeq} of {mission.Seq})");
        if (!again.Built)
        {
            return (next, again);
        }

        Fly(SettleSteps, new[] { next, again }, new[] { hostDoor, guestDoor });
        ctx.Check(!next.Session.StartHeld && !again.Session.StartHeld
                  && next.Session.Wire.StartGate?.Release == NetStartRelease.Everyone,
            $"and both machines start once it has loaded ({next.Session.Wire.StartGate?.Release}, {again.Session.Wire.StartGate?.Release})");
        ctx.Check(next.Session.NetSeats.Count == 2 && again.Session.NetSeats.Count == 2
                  && next.Session.SeatRigs[1].Controller is { Inert: false },
            $"the guest's seat stands in the restarted field on both machines and flies on the host ({next.Session.NetSeats.Count}, {again.Session.NetSeats.Count} seat(s))");
        ctx.Check(hostDoor.CoopGuests.All(g => !g.Left) && guestDoor.Stage == NetDoorStage.Joined,
            $"and the guest was never taken as walking out, nor its door failed ({guestDoor.Stage}, \"{guestDoor.Fault}\")");
        return (next, again);
    }

    // Three seats: the fits and names reach every machine, Lucy walks out, and the host's loss
    // ends the mission on the guest that stayed.
    private static void FirstFlight(TestContext ctx, CampaignMission mission, StockLoadouts stock,
        IReadOnlyList<LoopbackTransport> mesh, NetPlayFeature host, NetPlayFeature lucy, NetPlayFeature third)
    {
        var lucyFit = CoopFit.Of(LucyAmmo, null);
        Select(host, mission);
        Pump(SettleSteps, host, lucy, third);
        lucy.Pick.Set(CoopGuestPick.StarterAirframe, true, lucyFit);
        Pump(SettleSteps, host, lucy, third);
        ctx.Check(!host.CoopAllReady,
            $"ABLE-TO-FAIL CONTROL: one guest Ready holds the launch ({string.Join(", ", host.CoopGuests.Select(g => g.Ready))})");
        third.Pick.Set(CoopGuestPick.StarterAirframe, true);
        Pump(SettleSteps, host, lucy, third);
        ctx.Check(host.CoopAllReady, $"both guests Ready opens the launch");

        var hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out var seatFits);
        Ends? lucyEnd = null;
        Ends? thirdEnd = null;
        try
        {
            lucyEnd = Follow(ctx, mission, stock, lucy, lucyFit, hostEnd, host);
            thirdEnd = Follow(ctx, mission, stock, third, default, hostEnd, host);
            ctx.Check(hostEnd.Built && lucyEnd.Built && thirdEnd.Built,
                $"three campaign sessions build in one process (host {hostEnd.Built}, Lucy {lucyEnd.Built}, third {thirdEnd.Built})");
            if (!hostEnd.Built || !lucyEnd.Built || !thirdEnd.Built)
            {
                return;
            }

            var ends = new[] { hostEnd, lucyEnd, thirdEnd };
            var doors = new[] { host, lucy, third };
            // The mission's opening film holds the world, and nothing in this rig plays it out. Lucy
            // skips it, and one guest's skip has to end it on every machine.
            SkipAcross(ctx, "Lucy", lucyEnd, ends, doors);
            Fly(SettleSteps, ends, doors);
            Starts(ctx, ends);
            Names(ctx, ends);
            Fits(ctx, ends, seatFits);
            Stores(ctx, lucyEnd, thirdEnd, mission);
            WalkOut(ctx, hostEnd, thirdEnd, lucyEnd, lucy, host, third);
            Lost(ctx, hostEnd, thirdEnd, host, third);
            RetryClearsReady(ctx, mission, hostEnd, thirdEnd, host, third);

            // Lucy leaves the game, which drops her from the next flight's field.
            lucy.Close();
            mesh[1].Disconnect(0);
            Pump(SettleSteps, host, third);
        }
        finally
        {
            thirdEnd?.Close();
            lucyEnd?.Close();
            hostEnd?.Close();
        }
    }

    // Two seats, and the host's link cut mid-flight.
    private static void SecondFlight(TestContext ctx, CampaignMission mission, StockLoadouts stock,
        IReadOnlyList<LoopbackTransport> mesh, NetPlayFeature host, NetPlayFeature third)
    {
        third.Pick.Set(CoopGuestPick.StarterAirframe, true);
        Pump(SettleSteps, host, third);
        ctx.Check(host.CoopAllReady && host.CoopGuests.Count == 1,
            $"after the retry the one guest left is Ready again ({host.CoopGuests.Count} seated)");

        var hostEnd = Launch(ctx, mission, stock, host, HostAmmo, out _);
        Ends? thirdEnd = null;
        try
        {
            thirdEnd = Follow(ctx, mission, stock, third, default, hostEnd, host);
            if (!hostEnd.Built || !thirdEnd.Built)
            {
                ctx.Check(false, $"the retried flight builds (host {hostEnd.Built}, guest {thirdEnd.Built})");
                return;
            }

            var ends = new[] { hostEnd, thirdEnd };
            var doors = new[] { host, third };
            SkipAcross(ctx, "the host", hostEnd, ends, doors);
            Fly(SettleSteps, ends, doors);
            ctx.Check(third.Stage == NetDoorStage.Joined && !NetFlight.CoopGuestFlightOver(third),
                $"ABLE-TO-FAIL CONTROL: with the host on the wire the guest's flight goes on ({third.Stage})");
            ctx.Check(thirdEnd.Session.NetSeats[1].Callsign == "P2",
                $"a guest with no player name is seated under its player number in the new field ({thirdEnd.Session.NetSeats[1].Callsign})");

            mesh[0].Disconnect(2);
            Fly(SettleSteps, ends, doors);
            ctx.Check(third.Stage == NetDoorStage.Failed && third.Fault == CoopDoorText.HostLeft,
                $"a dropped host fails the guest's door with \"{CoopDoorText.HostLeft}\" ({third.Stage}, \"{third.Fault}\")");
            ctx.Check(NetFlight.CoopGuestFlightOver(third),
                $"and the launcher's rule ends the guest's flight, which returns it to the Connection page");
            ctx.Check(hostEnd.Session.SeatRigs[1].Controller is { Inert: true }
                      && Shows(hostEnd, CoopDoorText.Left("P2")) && hostEnd.Session.Campaign?.Result == null,
                $"while the host takes the guest's seat out, says \"{CoopDoorText.Left("P2")}\", and flies on");
        }
        finally
        {
            thirdEnd?.Close();
            hostEnd.Close();
        }
    }

    // The field starts abreast on every machine, one grid slot a seat, so no two humans open inside
    // each other. A machine flying one seat of its own still lays out the whole field.
    private static void Starts(TestContext ctx, Ends[] ends)
    {
        var gaps = ends.Select(e => Enumerable.Range(1, e.Session.SeatRigs.Count - 1)
            .Select(s => Gap(e, s - 1, s)).Min()).ToArray();
        string reading = string.Join(", ", gaps.Select(g => $"{g:0} m"));
        ctx.Check(gaps.All(g => g > StartGrid.SlotSpacingDefault * 0.5f),
            $"every machine opens its three seats at least half a grid slot apart (closest pair {reading})");
        ctx.Check(ends.All(e => e.Session.SeatRigs.All(r => r.Controller is { InPlay: true })),
            $"and no human is down anywhere once the opening film is skipped");
    }

    // One machine's skip of the opening film, which every machine is playing. A guest's is an ask
    // that ends nothing before the host's word returns. The host's ends its own at once and the
    // guests' only over the link. Either way it has to end the film everywhere.
    private static void SkipAcross(TestContext ctx, string who, Ends skipper, Ends[] ends, NetPlayFeature[] doors)
    {
        bool allPlaying = ends.All(e => e.Session.Cutscene is { Playing: true });
        bool skipped = skipper.Session.Cutscene?.Skip() == true;
        var still = ends.Where(e => e.Session.Cutscene is { Playing: true }).ToArray();
        bool isHost = ReferenceEquals(skipper, ends[0]);
        ctx.Check(allPlaying && skipped && still.Length == (isHost ? ends.Length - 1 : ends.Length),
            $"ABLE-TO-FAIL CONTROL: every machine plays the opening film and {who}'s skip leaves {still.Length} of {ends.Length} still playing before the link carries it");
        int steps = 0;
        for (; steps < SkipWindowSteps && ends.Any(e => e.Session.Cutscene is { Playing: true }); steps++)
        {
            FlyOnce(ends, doors);
        }

        ctx.Check(ends.All(e => e.Session.Cutscene is not { Playing: true }),
            $"{who}'s skip ends the opening film on all {ends.Length} machines ({steps} step(s))");
    }

    private static float Gap(Ends end, int a, int b) =>
        end.Session.SeatRigs[a].Controller!.WorldPosition.DistanceTo(end.Session.SeatRigs[b].Controller!.WorldPosition);

    // Every machine seats Lucy under her own name. The third guest, who has none, is seated
    // under its player number.
    private static void Names(TestContext ctx, Ends[] ends)
    {
        var names = ends.Select(e => string.Join(",", e.Session.NetSeats.Select(s => s.Callsign))).ToArray();
        ctx.Check(ends.All(e => e.Session.NetSeats.Count == 3
                                && e.Session.NetSeats[1].Callsign == GuestName
                                && e.Session.NetSeats[2].Callsign == "P3"),
            $"each machine seats the named guest as {GuestName} and the unnamed one as P3 ({string.Join(" | ", names)})");
        // Each machine flies the seat of its own place in the field, which PilotNames reads by.
        var peers = ends.Select(e => e.Session).ToArray();
        NetTeamSuites.PilotNames(ctx, peers,
            "each machine's pane reads its co-op partners' aeroplanes by their callsigns, the nameless one as Unknown");
        // The host here answers no Player Information either. The roster carries both namelessnesses
        // to every machine, and the other panes read the third guest as "Unknown" rather than P3.
        var unnamed = ends.Select(e => string.Join(",", e.Session.NetSeats.Select(s => s.Unnamed))).ToArray();
        ctx.Check(ends.All(e => e.Session.NetSeats.Select(s => s.Unnamed).SequenceEqual(new[] { true, false, true })),
            $"every machine's roster marks the host and the third guest as nameless and {GuestName} as named ({string.Join(" | ", unnamed)})");
        var third = new[] { 0, 1 }.Select(m => NetTeamSuites.RefOf(NetTeamSuites.Cycles(peers[m], m),
            s => ReferenceEquals(s, peers[m].SeatRigs[2].Controller))?.DisplayName ?? "missing").ToArray();
        ctx.Check(third.All(n => n == "Unknown"),
            $"the host's and {GuestName}'s panes read the nameless guest's aeroplane as Unknown, not P3 ({string.Join(" | ", third)})");
        // The host's seat is the scripted player and speaks as Nathan Zachary. Lucy speaks in the
        // voice she chose, and the third guest chose none.
        var voices = ends.Select(e => string.Join(",", e.Session.NetSeats.Select(s => s.Voice))).ToArray();
        byte[] want = { PilotVoices.Wire(PilotVoices.CoopHost), PilotVoices.Wire(GuestVoice), CoopPickMessage.NoVoice };
        ctx.Check(ends.All(e => e.Session.NetSeats.Select(s => s.Voice).SequenceEqual(want)),
            $"each machine seats the host as Nathan Zachary, {GuestName} in her chosen voice and the third with none ({string.Join(" | ", voices)})");
    }

    // A seat's rounds on every machine are its own pilot's pick. The third guest flies stock, so
    // its slug is the control a fit that never arrived would also read.
    private static void Fits(TestContext ctx, Ends[] ends, CoopFit[] seatFits)
    {
        string[] want = { AmmoName(HostAmmo[0]), AmmoName(LucyAmmo[0]) };
        var read = ends.Select(e => Enumerable.Range(0, 3).Select(s => Ammo(e, s)).ToArray()).ToArray();
        string reading = string.Join(" | ", read.Select(r => string.Join(",", r)));
        ctx.Check(seatFits.Length == 3 && seatFits[1] == CoopFit.Of(LucyAmmo, null),
            $"the host's launch carries each seat's fit, Lucy's pick among them ({seatFits.Length} seat(s))");
        ctx.Check(read.All(r => r[0] == want[0] && r[1] == want[1]),
            $"every machine builds the host's seat on {want[0]} and Lucy's on {want[1]} ({reading})");
        ctx.Check(read.All(r => r[2] is { } stock && stock != want[0] && stock != want[1]),
            $"ABLE-TO-FAIL CONTROL: the stock seat reads neither pick on any machine ({reading})");
    }

    // A guest's director keeps its result in memory with no store behind it.
    private static void Stores(TestContext ctx, Ends lucy, Ends third, CampaignMission mission)
    {
        var guests = new[] { lucy.Session.Campaign, third.Session.Campaign };
        ctx.Check(guests.All(c => c is { HasStore: false, PilotName: CampaignDirector.CoopGuestPilot }),
            $"each guest's director flies as {CampaignDirector.CoopGuestPilot} with no profile store ({string.Join(", ", guests.Select(c => $"{c?.PilotName}/{c?.HasStore}"))})");

        // ABLE-TO-FAIL CONTROL: a director handed a store says so.
        string dir = Path.Combine(Path.GetTempPath(), $"csvm-coop-store-{System.Environment.ProcessId}");
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder.ToUpperInvariant(),
            mission.MissionFolder.ToUpperInvariant());
        var stored = CampaignDirector.Create(ObjectiveScript.Load(zrdr), mission,
            CampaignProfileDef.NewProfile("Control"), new CampaignProfileStore(dir));
        ctx.Check(stored.HasStore, $"ABLE-TO-FAIL CONTROL: a director over a store reports one");
    }

    // Lucy leaves through her pause sheet, in the launcher's order: her word, her session freed,
    // her wire back to her door. Her link stands, so her word is what takes her seat out.
    private static void WalkOut(TestContext ctx, Ends host, Ends third, Ends lucyEnd, NetPlayFeature lucy,
        NetPlayFeature hostDoor, NetPlayFeature thirdDoor)
    {
        ctx.Check(host.Session.SeatRigs[1].Controller is { Inert: false },
            $"ABLE-TO-FAIL CONTROL: Lucy's seat flies on the host before she leaves");
        lucy.LeaveCoopMission();
        lucyEnd.Close();
        lucy.Reclaim();
        int steps = 0;
        while (steps < SettleSteps && host.Session.SeatRigs[1].Controller is { Inert: false })
        {
            FlyOnce(new[] { host, third }, new[] { hostDoor, thirdDoor });
            lucy.Step(GameClock.FixedDt);
            steps++;
        }

        Fly(SettleSteps, new[] { host, third }, new[] { hostDoor, thirdDoor });
        ctx.Check(host.Session.SeatRigs[1].Controller is { Inert: true },
            $"the host takes Lucy's seat out {steps} step(s) after she leaves, with her link still up");
        ctx.Check(Shows(host, CoopDoorText.Left(GuestName)) && Shows(third, CoopDoorText.Left(GuestName)),
            $"and the host and the other guest both show \"{CoopDoorText.Left(GuestName)}\"");
        ctx.Check(third.Session.SeatRigs[1].Controller is { Inert: true }
                  && host.Session.Campaign?.Result == null && third.Session.Campaign?.Result == null,
            $"the other guest's copy of her aeroplane leaves too, and the mission goes on");
    }

    // The field is lost when its last flying human is. The guest going down first ends nothing,
    // and the host's loss then ends the mission on both machines.
    private static void Lost(TestContext ctx, Ends host, Ends third, NetPlayFeature hostDoor,
        NetPlayFeature thirdDoor)
    {
        var ends = new[] { host, third };
        var doors = new[] { hostDoor, thirdDoor };
        third.Session.SeatRigs[2].Controller!.DebugForceCrash();
        Fly(SettleSteps * 4, ends, doors);
        ctx.Check(host.Session.Campaign?.Result == null && third.Session.Campaign?.Result == null,
            $"ABLE-TO-FAIL CONTROL: the guest's own loss ends nothing while the host flies");

        host.Session.SeatRigs[0].Controller!.DebugForceCrash();
        int steps = 0;
        while (steps < EndingSteps && (host.Session.Campaign?.Result == null || third.Session.Campaign?.Result == null))
        {
            FlyOnce(ends, doors);
            steps++;
        }

        var mine = host.Session.Campaign?.Result;
        var theirs = third.Session.Campaign?.Result;
        ctx.Check(mine?.Outcome == MissionOutcome.Lost && theirs?.Outcome == MissionOutcome.Lost,
            $"the host's loss, with the seat that left not waited on, ends the mission Lost on both machines after {steps} step(s) (host {mine?.Outcome}, guest {theirs?.Outcome})");
    }

    // The host's debrief reaches the guest, and its Retry puts both back through selection.
    private static void RetryClearsReady(TestContext ctx, CampaignMission mission, Ends host, Ends third,
        NetPlayFeature hostDoor, NetPlayFeature thirdDoor)
    {
        var result = third.Session.Campaign?.Result;
        host.Close();
        hostDoor.Reclaim();
        hostDoor.HostFlow.ShowResult(false, 0, 0);
        hostDoor.ShowCoop(NetCoopScreen.Debrief, mission.Seq, 0, 1 << CoopGuestPick.StarterAirframe);
        for (int i = 0; i < SettleSteps; i++)
        {
            third.Session._PhysicsProcess(GameClock.FixedDt);
            hostDoor.Step(GameClock.FixedDt);
            thirdDoor.Step(GameClock.FixedDt);
        }

        ctx.Check(NetFlight.CoopGuestFlightOver(thirdDoor) && thirdDoor.CoopFlow is { Screen: NetCoopScreen.Debrief, Won: false },
            $"the guest's flight ends on the host's debrief, which names the loss ({thirdDoor.CoopFlow?.Screen}, won {thirdDoor.CoopFlow?.Won})");
        ctx.Check(result?.Outcome == MissionOutcome.Lost,
            $"and the guest carries the host's result to it ({result?.Outcome})");
        third.Close();
        thirdDoor.Reclaim();

        hostDoor.ShowCoop(NetCoopScreen.Briefing, mission.Seq, 0, 1 << CoopGuestPick.StarterAirframe);
        Pump(SettleSteps, hostDoor, thirdDoor);
        ctx.Check(thirdDoor.CoopFlow is { Screen: NetCoopScreen.Briefing } && !thirdDoor.Pick.Ready && !hostDoor.CoopAllReady,
            $"the host's Retry puts the guest back on the briefing with its Ready cleared ({thirdDoor.CoopFlow?.Screen}, ready {thirdDoor.Pick.Ready})");
        hostDoor.ShowCoop(NetCoopScreen.FlightCheck, mission.Seq, 0, 1 << CoopGuestPick.StarterAirframe);
    }

    // The host's launch: the door names the flight and the launcher's helper builds the field and
    // the fits. The fits go out before the session's opener.
    private static Ends Launch(TestContext ctx, CampaignMission mission, StockLoadouts stock,
        NetPlayFeature door, int[] ammo, out CoopFit[] seatFits) =>
        Launch(ctx, mission, stock, door, door.BuildLaunch()!, ammo, out seatFits);

    private static Ends Launch(TestContext ctx, CampaignMission mission, StockLoadouts stock,
        NetPlayFeature door, MenuNetLaunch launch, int[] ammo, out CoopFit[] seatFits,
        string profile = "", string? profilesDir = null, bool keepFlying = false)
    {
        var own = CampaignLoadout.For(CoopFit.Of(ammo, null), stock);
        var planes = new[] { Flight.Hangar.StockAirframes.Node(CoopGuestPick.StarterAirframe) };
        (var roster, seatFits) = SeatFields.CoopLaunchField(door, launch.Transport, planes, new[] { own }, stock);
        door.TellSeatFits(seatFits);
        door.TellCoopWingman(SeatFields.CoopWingmanFor(profile, profilesDir));
        door.TellCoopPersisted(SeatFields.CoopPersistedFor(profile, profilesDir));
        var fits = seatFits;
        return NetCombatSuites.Ends.Open(ctx, Spec(mission, own, profile, profilesDir, keepFlying), launch.Transport,
            isHost: true, HostSeed, roster, Flight.Hangar.StockAirframes.Nodes,
            seat => SeatFields.CoopSeatFitFor(seat, fits, null, stock));
    }

    // A guest's launch: its door waits for the host's opener while the host flies. It then builds a
    // session with no profile, its own fit on its own seat and the host's word on every other.
    // Without hostLog it builds as a guest that never heard the host's persist log, a suite's control.
    private static Ends Follow(TestContext ctx, CampaignMission mission, StockLoadouts stock,
        NetPlayFeature door, CoopFit fit, NetCombatSuites.Ends host, NetPlayFeature hostDoor, bool hostLog = true,
        bool keepFlying = false)
    {
        for (int i = 0; i < OpenerSteps && !door.CoopLaunchDue; i++)
        {
            if (host.Built)
            {
                host.Session._PhysicsProcess(GameClock.FixedDt);
            }

            hostDoor.Step(GameClock.FixedDt);
            door.Step(GameClock.FixedDt);
        }

        ctx.Check(door.CoopLaunchDue, $"a guest's door hears the host's opener");
        var launch = door.BuildLaunch()!;
        return NetCombatSuites.Ends.Open(ctx, Spec(mission, CampaignLoadout.For(fit, stock), keepFlying: keepFlying), launch.Transport,
            isHost: false, HostSeed + 1, null, Flight.Hangar.StockAirframes.Nodes,
            seat => SeatFields.CoopSeatFitFor(seat, null, door, stock), () => door.CoopWingman,
            coopPersisted: hostLog ? () => door.CoopPersisted : null);
    }

    // A guest flying several seats launches as a guest with one: one pane per seat its host gave it.
    private static Ends FollowSeats(TestContext ctx, CampaignMission mission, StockLoadouts stock,
        NetPlayFeature door, int seats, NetCombatSuites.Ends host, NetPlayFeature hostDoor)
    {
        for (int i = 0; i < OpenerSteps && !door.CoopLaunchDue; i++)
        {
            host.Session._PhysicsProcess(GameClock.FixedDt);
            hostDoor.Step(GameClock.FixedDt);
            door.Step(GameClock.FixedDt);
        }

        ctx.Check(door.CoopLaunchDue, $"a guest's door hears the host's opener");
        var launch = door.BuildLaunch()!;
        string plane = Flight.Hangar.StockAirframes.Node(CoopGuestPick.StarterAirframe);
        var spec = SessionSpec.FromCampaign(SessionSpec.Parse(new[] { "--mute", "--no-pads" }), "", mission.Seq,
            Enumerable.Repeat(plane, seats).ToArray(), seats, Enumerable.Repeat<LoadoutChoice?>(null, seats).ToArray());
        return NetCombatSuites.Ends.Open(ctx, spec, launch.Transport, isHost: false, HostSeed + 1, null,
            Flight.Hangar.StockAirframes.Nodes, seat => SeatFields.CoopSeatFitFor(seat, null, door, stock),
            () => door.CoopWingman, coopPersisted: () => door.CoopPersisted);
    }

    // The guest's two seats on both machines: flown by the guest, chased from samples on the host.
    // The host's copy of each tracks where the guest flies it.
    private static void GuestSeatsFly(TestContext ctx, Ends host, Ends guest, Ends[] ends, NetPlayFeature[] doors)
    {
        var hostSeats = host.Session.NetSeats;
        var guestSeats = guest.Session.NetSeats;
        ctx.Check(hostSeats.Count == 3 && guestSeats.Count == 3
                  && hostSeats[1].PeerId == hostSeats[2].PeerId && !hostSeats[1].FlownHere && !hostSeats[2].FlownHere,
            $"the host's field holds the guest's machine at seats 1 and 2 ({hostSeats.Count} seat(s))");
        ctx.Check(guest.Session.Wire.Link is { LocalSeat: 1, LocalSeatCount: 2 } && guestSeats[1].FlownHere && guestSeats[2].FlownHere
                  && !guestSeats[0].FlownHere,
            $"the join names the guest both seats ({guest.Session.Wire.Link?.LocalSeat}, {guest.Session.Wire.Link?.LocalSeatCount})");
        ctx.Check(guest.Session.SeatRigs[1].Controller is { RemotePoses: null, InPlay: true }
                  && guest.Session.SeatRigs[2].Controller is { RemotePoses: null, InPlay: true },
            $"the guest flies both its seats itself, each in play");
        ctx.Check(host.Session.SeatRigs[1].Controller is { RemotePoses: not null, InPlay: true }
                  && host.Session.SeatRigs[2].Controller is { RemotePoses: not null, InPlay: true },
            $"the host builds both as remote aeroplanes, each in play");

        var from = new[] { 1, 2 }.Select(s => host.Session.SeatRigs[s].Controller!.WorldPosition).ToArray();
        Fly(TravelSteps, ends, doors);
        var travel = new[] { 1, 2 }.Select((s, i) => host.Session.SeatRigs[s].Controller!.WorldPosition.DistanceTo(from[i])).ToArray();
        var miss = new[] { 1, 2 }.Select(s => host.Session.SeatRigs[s].Controller!.WorldPosition
            .DistanceTo(guest.Session.SeatRigs[s].Controller!.WorldPosition)).ToArray();
        string reading = $"travel {travel[0]:0} m and {travel[1]:0} m, behind the guest's own by {miss[0]:0} m and {miss[1]:0} m";
        ctx.Check(travel.All(t => t > SeatTravelM),
            $"each of the guest's planes flies on over {TravelSteps} steps on the host ({reading})");
        ctx.Check(miss.All(m => m < SeatReplicaM),
            $"and the host's copy of each stays within {SeatReplicaM:0} m of where the guest flies it ({reading})");
        float crossed = host.Session.SeatRigs[1].Controller!.WorldPosition.DistanceTo(guest.Session.SeatRigs[2].Controller!.WorldPosition);
        ctx.Check(crossed > SeatReplicaM,
            $"ABLE-TO-FAIL CONTROL: a copy read against the guest's other plane misses by more ({crossed:0} m)");
        ctx.Note($"the guest's seats on the host: {reading}; crossed {crossed:0} m");
    }

    // The guest crashes and returns. Its copy on the host is a wreck while it is down. The return
    // is the one placement that copy takes, so a second word placing it would count here.
    private static void GuestReturns(TestContext ctx, Ends host, Ends guest, Ends[] ends, NetPlayFeature[] doors)
    {
        var own = guest.Session.SeatRigs[1].Controller!;
        var copy = host.Session.SeatRigs[1].Controller!;
        ctx.Check(own is { RemoteOwned: false, InPlay: true } && copy is { RemoteOwned: true, InPlay: true },
            $"the guest flies its seat and the host chases it as a remote aeroplane, both in play");

        own.DebugForceCrash();
        int steps = 0;
        for (; steps < SkipWindowSteps && !copy.Crashed; steps++)
        {
            FlyOnce(ends, doors);
        }

        bool wreck = copy.Crashed && !copy.InPlay;
        ctx.Check(wreck && own.Crashed && !own.CrashIsFinal && own.RespawnOffered,
            $"ABLE-TO-FAIL CONTROL: the guest's death report plays its wreck on the host {steps} step(s) on, and --no-crash-loss leaves the guest's crash returnable (copy crashed={copy.Crashed} in play={copy.InPlay}, final={own.CrashIsFinal}, offered={own.RespawnOffered})");

        Vector3? returned = null;
        Vector3? placed = null;
        float ownYaw = 0f;
        float copyYaw = 0f;
        own.Respawned += () =>
        {
            returned = own.WorldPosition;
            ownYaw = Yaw(own);
        };
        copy.Respawned += () =>
        {
            placed = copy.WorldPosition;
            copyYaw = Yaw(copy);
        };
        int ownBefore = own.RespawnCount;
        int copyBefore = copy.RespawnCount;
        own.HoldActionForTest(InputAction.Respawn, true);
        FlyOnce(ends, doors);
        own.HoldActionForTest(InputAction.Respawn, false);
        for (steps = 0; steps < SkipWindowSteps && placed == null; steps++)
        {
            FlyOnce(ends, doors);
        }

        float off = returned is { } at && placed is { } there ? at.DistanceTo(there) : float.PositiveInfinity;
        float turn = Mathf.Abs(Mathf.Wrap(copyYaw - ownYaw, -180f, 180f));
        ctx.Check(returned != null && placed != null && off < ReturnPoseM && turn < ReturnHeadingDeg,
            $"the guest's return reaches the host {steps} step(s) on and places the copy where the guest returned ({off:0.000} m off, heading {turn:0.00} deg off)");

        Fly(TravelSteps, ends, doors);
        float travel = placed is { } from ? copy.WorldPosition.DistanceTo(from) : 0f;
        float gap = copy.WorldPosition.DistanceTo(own.WorldPosition);
        ctx.Check(!copy.Crashed && copy.InPlay && travel > SeatTravelM && gap < SeatReplicaM,
            $"the copy leaves its wreck and flies on from there beside the guest ({travel:0} m flown in {TravelSteps} steps, {gap:0.0} m from the guest, crashed={copy.Crashed} in play={copy.InPlay})");
        ctx.Check(own.RespawnCount - ownBefore == 1 && copy.RespawnCount - copyBefore == 1,
            $"one return on the guest places the host's copy once, with no second placement ({own.RespawnCount - ownBefore} on the guest, {copy.RespawnCount - copyBefore} on the host)");
        ctx.Check(host.Session.Campaign?.Result == null && guest.Session.Campaign?.Result == null,
            $"and the mission flies on on both machines");
        ctx.Note($"the guest's return on the host: wreck control {wreck}, placed {steps} step(s) on, {off:0.000} m and {turn:0.00} deg off the return, then {travel:0} m flown and {gap:0.0} m from the guest; respawns {own.RespawnCount - ownBefore} guest / {copy.RespawnCount - copyBefore} host");
    }

    // The nose's yaw in degrees, the heading the placed return carries.
    private static float Yaw(FlightController pilot) =>
        Mathf.RadToDeg(Mathf.Atan2(-pilot.NoseDirection.X, -pilot.NoseDirection.Z));

    // A co-op launch with no profile behind it unless a suite's own store names one. Only a
    // guest's is so in play, so no suite writes to the user's store. keepFlying is --no-crash-loss.
    private static SessionSpec Spec(CampaignMission mission, LoadoutChoice? fit, string profile = "",
        string? profilesDir = null, bool keepFlying = false) =>
        SessionSpec.FromCampaign(
            SessionSpec.Parse(new[] { "--mute", "--no-pads" }
                .Concat(profilesDir == null ? Array.Empty<string>() : new[] { $"--profiles={profilesDir}" })
                .Concat(keepFlying ? new[] { "--no-crash-loss" } : Array.Empty<string>())),
            profile, mission.Seq,
            new[] { Flight.Hangar.StockAirframes.Node(CoopGuestPick.StarterAirframe) }, 1, new[] { fit });

    // The host's boards on the flight check of the suite's mission, with the starter offered.
    private static void Select(NetPlayFeature host, CampaignMission mission) =>
        host.ShowCoop(NetCoopScreen.FlightCheck, mission.Seq, 0, 1 << CoopGuestPick.StarterAirframe);

    private static CampaignMission Mission(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        var mission = CampaignSequence.Load(ctx.ZrdrPath).Cast<CampaignMission?>()
            .FirstOrDefault(m => m!.Value.ChapterFolder.Length > 0)
            ?? throw new SuiteSkippedException($"cm_sequence carries no mission with a chapter");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, chapter), $"{chapter} gamez");
        return mission;
    }

    private static NetPlayFeature Door(LoopbackTransport wire) =>
        new((_, _, _) => wire, (_, _) => wire);

    // Each door steps its own wire while no flight carries it.
    private static void Pump(int steps, params NetPlayFeature[] doors)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var door in doors)
            {
                door.Step(GameClock.FixedDt);
            }
        }
    }

    // A flight's step on every end, the way the launcher runs one. The session's fixed step comes
    // first, then the door, then the host's word on any guest that walked out.
    private static void Fly(int steps, Ends[] ends, NetPlayFeature[] doors)
    {
        for (int i = 0; i < steps; i++)
        {
            FlyOnce(ends, doors);
        }
    }

    private static void FlyOnce(Ends[] ends, NetPlayFeature[] doors)
    {
        for (int e = 0; e < ends.Length; e++)
        {
            ends[e].Session._PhysicsProcess(GameClock.FixedDt);
            doors[e].Step(GameClock.FixedDt);
            if (doors[e].IsCoopHost)
            {
                foreach (var guest in doors[e].CoopGuests.Where(g => g.Left))
                {
                    ends[e].Session.Wire.TakeGuestLeft(guest.Peer);
                }
            }
        }
    }

    // The first gun's ammunition on one machine's copy of a seat.
    private static string? Ammo(Ends end, int seat) =>
        seat < end.Session.SeatRigs.Count && end.Session.SeatRigs[seat].Controller?.Loadout?.Def is { Guns.Count: > 0 } def
            ? def.Guns[0].Ammo
            : null;

    private static string AmmoName(int stored) => CampaignLoadout.AmmoNames[stored];

    // Whether any local pane's message stack shows the line.
    private static bool Shows(Ends end, string line) =>
        end.Session.Rigs.Any(r => r.Controller?.MessageStack is { } stack
            && Enumerable.Range(0, HudMessages.Slots).Any(s => stack.LineAt(s) == line));

    private static Dictionary<string, byte[]> Snapshot(string dir)
    {
        var files = new Dictionary<string, byte[]>();
        if (!Directory.Exists(dir))
        {
            return files;
        }

        foreach (string path in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            files[Path.GetRelativePath(dir, path)] = File.ReadAllBytes(path);
        }

        return files;
    }
}

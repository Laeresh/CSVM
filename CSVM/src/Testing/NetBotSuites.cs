using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Overlays;
using CSVM.Utils;
using Godot;

using static CSVM.Testing.BotSuiteHelper;

namespace CSVM.Testing;

/// <summary>A bot seat between whole sessions in one process. The host flies it with an AI pilot,
/// and the guest sees it as a remote seat fed by the host's state samples. Its hits, its death and
/// its score take the seat paths, so a bot's kill scores the bot. The rig is
/// <see cref="NetCombatSuites"/>'s, one session per peer under its own world. The bot is seated
/// as a scripted host seats one, in the roster its launch context carries. The host's roster AI
/// takes the same far-field reading as a bot, against the same people.</summary>
internal static class NetBotSuites
{
    private const string MpMission = "MP1";

    private const ulong HostSeed = 0xB07B07B0UL;

    private const int HostSeat = 0;
    private const int GuestSeat = 1;
    private const int BotSeat = 2;
    private const int SecondBotSeat = 3;

    // How long a check that something does NOT happen keeps watching, in sim steps. That is a
    // second past the quick crash camera and the grant that would follow it.
    private const int HeldSteps = 60;

    // The most steps any one wait is given. A reliable payload crosses the lossy link in a few
    // dozen at worst. A death adds its crash, the ask and the grant of the return.
    private const int WaitSteps = 240;

    // How long the bot is watched for the tracking reading, in sim steps. Then the alignment search
    // over the buffer's read-behind and the link's latency, as in net-ai-world.
    private const int FlightSteps = 180;
    private const int MaxLagSteps = 30;

    // The tracking bar in metres, mean over the window at the best whole-step lag. A whole-step
    // grid leaves up to half a step of misalignment, about a metre at these speeds.
    private const float MeanErrorBar = 3f;

    // How far ahead of its quarry the bot is set down for the targeting reading, in metres. Well
    // inside the shortest attack radius an airframe authors.
    private const float QuarryRange = 300f;

    // How far the far-field reading moves the guest from the host's plane, in metres. Then how far
    // beside the guest it sets the bot down. The plant switches at 1000 m.
    private const float FarFieldOffset = 3000f;
    private const float BesideOffset = 150f;

    // How close the host's copy of the guest must stand to the guest's own pose after a move, in
    // metres. It is the buffer's read-behind at flying speed, with room.
    private const float SettledGap = 60f;

    // The crash camera cut short, so a downed seat is back in half a second.
    private const float QuickRespawn = 0.5f;

    // A routed kill's damage share, the debug kill key's own: far past any hull's pools.
    private const float LethalScale = 1e6f;

    private static readonly string[] Airframes = { "player_pfighter", "player_fbrand" };

    [Suite("net-bot-seat",
        "a host with a bot seat and a guest seat in one process over a clean loopback: the host flies "
        + "the bot with an armed AI pilot under its seat index, never as world AI, and the guest "
        + "builds it as a remote seat whose path traces the host's; each end's pane names it by its "
        + "callsign in the target pool and on the hostile tracker; beside the guest and over a "
        + "kilometre from the host's plane the bot flies the full plant, not the far-field one; the "
        + "bot's gunner picks a seat as "
        + "its quarry; a hit by the bot lands on the guest's own aeroplane and a hit on it lands on "
        + "the host's, mirrored back; the bot's kill of the guest scores the bot, the guest's kill "
        + "of the bot scores the guest, both boards agree, and the downed bot comes back")]
    internal static void BotSeatOnACleanLink(TestContext ctx) =>
        Run(ctx, LoopbackConditions.Perfect, 5101, "clean");

    [Suite("net-bot-seat-lossy",
        "net-bot-seat's readings over a 30 ms, 25 per cent lossy loopback, each waiting on every "
        + "condition it reads: both ends name the bot by its callsign, the guest's copy of the bot "
        + "still traces the host's path, a bot near "
        + "the guest alone flies the full plant, hits by "
        + "and on the bot land on their owners, both kills score as on the clean link and both "
        + "boards agree")]
    internal static void BotSeatOnALossyLink(TestContext ctx) =>
        Run(ctx, new LoopbackConditions(0.03, 0.01, 0.25), 5102, "lossy");

    // Clean link only. Every reading below is a grant, a score or the match state, all on the one
    // reliable ordered channel. The lossy twin of net-bot-seat already carries a bot's grant over loss.
    [Suite("net-bot-respawn",
        "two host and guest pairs in one process, each with two bots: with Auto Respawn off a "
        + "downed bot comes back after the crash camera time at the host's granted rotation "
        + "entry on both machines while the guest's own seat waits for Fire Guns, and its pilot "
        + "starts over with no quarry, no chase and the new placement's course; with one life a "
        + "downed bot stays down on both machines, the host granting it nothing, a living bot "
        + "keeps the match running as an opponent, and the last bot's death ends it on "
        + "nobody left to fight")]
    internal static void BotsRespawnAndSpendLives(TestContext ctx)
    {
        var pressed = NetCombatSuites.MatchSpec(ctx, out var table, "--vs-no-respawn");
        var limited = NetCombatSuites.MatchSpec(ctx, out _, "--vs-lives=1");
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            if (Field(ctx, pressed, 5103, "no auto respawn", ends) is { } waiting)
            {
                ReturnsUnasked(ctx, waiting, table);
            }

            if (Field(ctx, limited, 5104, "one life", ends) is { } spent)
            {
                SpendsItsLife(ctx, spent);
            }
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }

            ambient.Restore();
        }
    }

    [Suite("net-ai-far-field",
        "a host with one roster AI and a guest in one process over a clean loopback: an AI over a "
        + "kilometre from the host's plane and beside the guest's flies the full plant, the same AI "
        + "a kilometre from both flies the far-field one, and once the guest has left the session "
        + "an AI beside its inert aeroplane flies the far-field plant")]
    internal static void RosterAiMeasuresEveryPerson(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _, "--ai=player_pfighter");
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(5105));
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = HostSeat, FlownHere = true, Callsign = "host", PlaneNode = Airframes[0] },
            new() { PeerId = 1, SeatIndex = GuestSeat, Callsign = "guest", PlaneNode = Airframes[1] },
        };
        NetSeats.Validate(roster, hostPeer: 0);
        var ambient = NetCombatSuites.Ambient.Save();
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? guest = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, roster, Airframes);
            guest = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null, Airframes);
            ctx.Check(host.Built && guest.Built,
                $"both sessions build in one process (host {host.Built}, guest {guest.Built})");
            if (!host.Built || !guest.Built)
            {
                return;
            }

            NetStartSuites.UntilStarted(host.Session, guest.Session);
            var peers = new[] { host.Session, guest.Session };
            Lockstep(1, peers);
            var ai = host.Session.Wire.World?.AiAt(0);
            ctx.Check(ai is { RemoteOwned: false, Pilot: not null },
                $"the host flies its roster AI itself ({FlightController.TargetLabel(ai)})");
            if (ai == null)
            {
                return;
            }

            var hostPlane = host.Session.SeatRigs[HostSeat].Controller!;
            var guestCopy = host.Session.SeatRigs[GuestSeat].Controller!;
            var guestOwn = guest.Session.SeatRigs[GuestSeat].Controller!;
            Lift(hostPlane);
            var away = hostPlane.WorldPosition + new Vector3(FarFieldOffset, 0f, 0f);
            guestOwn.RespawnAt(away, away + (Vector3.Right * 100f));
            guestOwn.ArmSpawnTimers();
            int steps = StepUntil(() => Horizontal(guestCopy, hostPlane) > FarFieldOffset * 0.8f
                && guestCopy.WorldPosition.DistanceTo(guestOwn.WorldPosition) < SettledGap, peers);

            var beside = guestCopy.WorldPosition + new Vector3(0f, 0f, BesideOffset);
            ai.RespawnAt(beside, beside + (Vector3.Right * 100f));
            Lockstep(1, peers);
            float toHost = Horizontal(ai, hostPlane);
            float toGuest = Horizontal(ai, guestCopy);
            ctx.Check(toHost > 1000f && toGuest < 1000f && !ai.FarFieldPlant,
                $"an AI {toHost:0} m from the host's plane and {toGuest:0} m from the guest's flies the full plant (far-field {ai.FarFieldPlant}, {steps} step(s) for the guest's move)");

            // ABLE-TO-FAIL CONTROL. The same AI a kilometre from every person takes the speed-hold
            // plant, so the reading above is the measurement and not a branch never taken.
            var alone = guestCopy.WorldPosition + new Vector3(0f, 0f, FarFieldOffset);
            ai.RespawnAt(alone, alone + (Vector3.Right * 100f));
            Lockstep(1, peers);
            ctx.Check(ai.FarFieldPlant,
                $"ABLE-TO-FAIL CONTROL: {Horizontal(ai, guestCopy):0} m from the guest and {Horizontal(ai, hostPlane):0} m from the host, the AI flies the far-field plant ({ai.FarFieldPlant})");

            // The guest's link drops. Its inert aeroplane stays where it was, and is no person.
            mesh[0].Disconnect(mesh[1].LocalPeer);
            var hostOnly = new[] { host.Session };
            int left = StepUntil(() => host.Session.Wire.HasLeft(GuestSeat), hostOnly);
            ai.RespawnAt(beside, beside + (Vector3.Right * 100f));
            Lockstep(1, hostOnly);
            ctx.Check(host.Session.Wire.HasLeft(GuestSeat) && Horizontal(ai, guestCopy) < 1000f && ai.FarFieldPlant,
                $"once the guest has left ({left} step(s)), an AI {Horizontal(ai, guestCopy):0} m from its inert aeroplane and {Horizontal(ai, hostPlane):0} m from the host's flies the far-field plant ({ai.FarFieldPlant})");
        }
        finally
        {
            guest?.Close();
            host?.Close();
            ambient.Restore();
        }
    }

    private static void Run(TestContext ctx, LoopbackConditions conditions, int seed, string cell)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, MpMission);
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{ctx.Chapter}/{MpMission} zrdr");
        var spec = SessionSpec.Parse(new[]
        {
            "--vs", $"--chapter={ctx.Chapter}", $"--mission={MpMission}", "--players=1", "--mute", "--no-pads",
        });
        var gun = WeaponDefs.Load(ctx.ZrdrPath).All.FirstOrDefault(w => w.IsCannon && w.HealthDamage is > 0f)
            ?? throw new SuiteSkippedException($"the weapon catalogue holds no cannon with health damage");

        var mesh = LoopbackTransport.Mesh(2, conditions, new Random(seed));
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = HostSeat, FlownHere = true, Callsign = "host", PlaneNode = Airframes[0] },
            new() { PeerId = 1, SeatIndex = GuestSeat, Callsign = "guest", PlaneNode = Airframes[1] },
            NetSeats.Bot(0, BotSeat, "bot", Airframes[0]),
        };
        NetSeats.Validate(roster, hostPeer: 0);

        var ambient = NetCombatSuites.Ambient.Save();
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? guest = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, roster, Airframes);
            guest = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null, Airframes);
            ctx.Check(host.Built && guest.Built,
                $"[{cell}] both sessions build in one process (host {host.Built}, guest {guest.Built})");
            if (!host.Built || !guest.Built)
            {
                return;
            }

            NetStartSuites.UntilStarted(host.Session, guest.Session);
            var peers = new[] { host.Session, guest.Session };
            Lockstep(1, peers);
            if (!Seated(ctx, cell, peers))
            {
                return;
            }

            Marked(ctx, cell, peers);
            Tracking(ctx, cell, peers);
            FarField(ctx, cell, peers);
            Targeting(ctx, cell, peers);
            Hits(ctx, cell, peers, gun);
            Kills(ctx, cell, peers);
        }
        finally
        {
            guest?.Close();
            host?.Close();
            ambient.Restore();
        }
    }

    // What each end built for the bot's seat: an AI-flown aeroplane on the host and a remote seat
    // on the guest. Neither end's world link admits it as host-flown AI.
    private static bool Seated(TestContext ctx, string cell, GameSession[] peers)
    {
        var bot = peers[0].SeatRigs[BotSeat].Controller;
        var copy = peers[1].SeatRigs[BotSeat].Controller;
        if (bot == null || copy == null)
        {
            ctx.Check(false, $"[{cell}] both ends build an aeroplane for the bot's seat (host {bot != null}, guest {copy != null})");
            return false;
        }

        ctx.Check(!bot.IsHumanPiloted && !bot.RemoteOwned && bot.Pilot is { Gunner: not null, Machine: not null }
                  && bot.PlayerIndex == BotSeat,
            $"[{cell}] the host flies the bot itself with an armed AI pilot under its seat index (human {bot.IsHumanPiloted}, remote {bot.RemoteOwned}, shooter {bot.PlayerIndex})");
        ctx.Check(copy.RemoteOwned && copy.Pilot == null && !copy.IsHumanPiloted && copy.PlayerIndex == BotSeat
                  && peers[1].NetSeats[BotSeat] is { IsBot: true, FlownHere: false },
            $"[{cell}] the guest builds the bot as a remote seat it does not fly, AI-flown on every machine (remote {copy.RemoteOwned}, pilot {copy.Pilot != null}, shooter {copy.PlayerIndex})");
        ctx.Check(peers.All(p => p.Wire.World is { Admitted: 0 }),
            $"[{cell}] and neither end's world link admits it as host-flown AI ({string.Join(", ", peers.Select(p => p.Wire.World?.Admitted))} admitted)");

        // ABLE-TO-FAIL CONTROL. The guest's own seat on the same two machines is the reverse: flown
        // on the guest, a remote copy on the host. The bot's reading is then the roster, not a
        // build that made every rig alike.
        var guestOwn = peers[1].SeatRigs[GuestSeat].Controller;
        var guestCopy = peers[0].SeatRigs[GuestSeat].Controller;
        ctx.Check(guestOwn is { RemoteOwned: false, IsHumanPiloted: true, Pilot: null }
                  && guestCopy is { RemoteOwned: true, IsHumanPiloted: true },
            $"ABLE-TO-FAIL CONTROL: [{cell}] the guest's own seat is a person's, flown on the guest and copied on the host");
        return true;
    }

    // Each end's aeroplane for the bot carries its callsign. The pane on that end names it so in the
    // target pool and on the hostile tracker, not by its node name. The pool is built here as the
    // pane's rendered frame builds it, since a suite steps no rendered frame.
    private static void Marked(TestContext ctx, string cell, GameSession[] peers)
    {
        var ends = new[] { (End: "host", Bot: peers[0].SeatRigs[BotSeat].Controller!, Pane: peers[0].SeatRigs[HostSeat].Controller!),
            (End: "guest", Bot: peers[1].SeatRigs[BotSeat].Controller!, Pane: peers[1].SeatRigs[GuestSeat].Controller!) };
        string callsign = peers[0].NetSeats[BotSeat].Callsign;
        foreach (var (end, bot, pane) in ends)
        {
            var scan = new AimCandidateSet();
            pane.Projectiles?.CollectAircraft(scan);
            var pool = new TargetPool();
            pool.Rebuild(scan, null, pane.Team, pane);
            string? listed = pool.Enemy.Where(t => ReferenceEquals(t.Source, bot)).Select(t => t.DisplayName).FirstOrDefault();
            ctx.Check(bot.PilotName == callsign && listed == callsign && TargetHud.TrackedTag(bot) == callsign,
                $"[{cell}] the {end}'s aeroplane for the bot carries '{callsign}' as its pilot name, its pane's pool names it so and its tracker tags it so ({bot.PilotName}, {listed ?? "<not listed>"}, {TargetHud.TrackedTag(bot)})");
        }

        // ABLE-TO-FAIL CONTROL. The node name the tracker read before reads otherwise, so the line
        // above is the callsign and not a name the node happened to share.
        var node = TargetHud.HostileTag(ends[1].Bot.Name);
        ctx.Check(node != callsign,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the bot's node name tags it '{node}', not '{callsign}'");
    }

    // The guest's copy of the bot follows the path the host's AI flies, through the seat's state
    // samples alone.
    private static void Tracking(TestContext ctx, string cell, GameSession[] peers)
    {
        var bot = peers[0].SeatRigs[BotSeat].Controller!;
        var copy = peers[1].SeatRigs[BotSeat].Controller!;
        var hostSeat = peers[0].SeatRigs[HostSeat].Controller!;
        var own = new List<Vector3>();
        var other = new List<Vector3>();
        var shown = new List<Vector3>();
        int received = copy.RemotePoses!.Tally.Accepted;
        for (int i = 0; i < FlightSteps; i++)
        {
            Lockstep(1, peers);
            own.Add(bot.WorldPosition);
            other.Add(hostSeat.WorldPosition);
            shown.Add(copy.WorldPosition);
        }

        float flown = own[0].DistanceTo(own[^1]);
        float right = Track(own, shown);
        float wrong = Track(other, shown);
        int samples = copy.RemotePoses.Tally.Accepted - received;
        ctx.Note($"[{cell}] bot tracking: {right:0.00} m mean against the bot's path, {wrong:0.0} m against the host seat's, {flown:0} m flown, {samples} sample(s) received");
        ctx.Check(flown > 100f && samples > 0 && right < MeanErrorBar,
            $"[{cell}] the guest's copy of the bot traces the host's own path to {right:0.00} m mean over {flown:0} m flown ({samples} sample(s))");
        // ABLE-TO-FAIL CONTROL. The same metric against the host's own seat, which flies elsewhere.
        // A metric that cannot tell two aeroplanes apart would pass the line above over nothing.
        ctx.Check(wrong > right * 5f,
            $"ABLE-TO-FAIL CONTROL: [{cell}] against the host seat's path the metric reads {wrong:0.0} m");
    }

    // The bot's plant measures against every person in the field. The guest is flown 3 km from
    // the host's plane, and the bot set down beside the host's copy of it flies the full plant.
    private static void FarField(TestContext ctx, string cell, GameSession[] peers)
    {
        var (host, guest) = (peers[0], peers[1]);
        var bot = host.SeatRigs[BotSeat].Controller!;
        var hostPlane = host.SeatRigs[HostSeat].Controller!;
        var guestCopy = host.SeatRigs[GuestSeat].Controller!;
        var guestOwn = guest.SeatRigs[GuestSeat].Controller!;
        Lift(hostPlane);
        var away = hostPlane.WorldPosition + new Vector3(FarFieldOffset, 0f, 0f);
        guestOwn.RespawnAt(away, away + (Vector3.Right * 100f));
        guestOwn.ArmSpawnTimers();
        // Until the host's copy has settled onto the guest's own pose, not merely left the host.
        int steps = StepUntil(() => Horizontal(guestCopy, hostPlane) > FarFieldOffset * 0.8f
            && guestCopy.WorldPosition.DistanceTo(guestOwn.WorldPosition) < SettledGap, peers);

        var beside = guestCopy.WorldPosition + new Vector3(0f, 0f, BesideOffset);
        bot.RespawnAt(beside, beside + (Vector3.Right * 100f));
        Lockstep(1, peers);
        float toHost = Horizontal(bot, hostPlane);
        float toGuest = Horizontal(bot, guestCopy);
        ctx.Check(toHost > 1000f && toGuest < 1000f && !bot.FarFieldPlant,
            $"[{cell}] a bot {toHost:0} m from the host's plane and {toGuest:0} m from the guest's flies the full plant (far-field {bot.FarFieldPlant}, {steps} step(s) for the guest's move)");

        // ABLE-TO-FAIL CONTROL. The same bot a kilometre from every person takes the speed-hold
        // plant. The reading above is therefore the measurement, not a branch never taken.
        var alone = guestCopy.WorldPosition + new Vector3(0f, 0f, FarFieldOffset);
        bot.RespawnAt(alone, alone + (Vector3.Right * 100f));
        Lockstep(1, peers);
        ctx.Check(bot.FarFieldPlant,
            $"ABLE-TO-FAIL CONTROL: [{cell}] {Horizontal(bot, guestCopy):0} m from the guest and {Horizontal(bot, hostPlane):0} m from the host, the bot flies the far-field plant ({bot.FarFieldPlant})");
    }

    private static float Horizontal(FlightController a, FlightController b)
    {
        var d = a.WorldPosition - b.WorldPosition;
        return Mathf.Sqrt((d.X * d.X) + (d.Z * d.Z));
    }

    // The bot's gunner ranks the seats and picks a hostile one. The bot is set down behind the host's
    // seat first, so a quarry stands inside its attack radius whatever the spawn table. Its
    // acquisition is then held off, so no round of its own lands in the readings after.
    private static void Targeting(TestContext ctx, string cell, GameSession[] peers)
    {
        var host = peers[0];
        var bot = host.SeatRigs[BotSeat].Controller!;
        var quarry = host.SeatRigs[HostSeat].Controller!;
        Lift(quarry);
        Lift(peers[1].SeatRigs[GuestSeat].Controller!);
        var behind = quarry.WorldPosition - (quarry.NoseDirection * QuarryRange);
        bot.RespawnAt(behind, quarry.WorldPosition);
        var gunner = bot.Pilot!.Gunner!;
        int steps = StepUntil(() => gunner.Target != null, peers);
        var picked = gunner.Target as FlightController;
        bool aSeat = picked != null && host.SeatRigs.Any(r => ReferenceEquals(r.Controller, picked));
        ctx.Check(aSeat && !ReferenceEquals(picked, bot) && AimAssist.Hostile(bot.Team, picked!.Team),
            $"[{cell}] the bot's gunner takes a hostile seat as its quarry ({FlightController.TargetLabel(gunner.Target)}, team {picked?.Team} against {bot.Team}, {steps} step(s))");

        gunner.AutoTarget = false;
        gunner.Target = null;
    }

    // The hit-authority fork with a bot on it. The host decides a round the bot fired, which lands
    // on the guest's own aeroplane. A round the guest fired at the bot lands on the host's.
    private static void Hits(TestContext ctx, string cell, GameSession[] peers, WeaponDef gun)
    {
        var (host, guest) = (peers[0], peers[1]);
        var bot = host.SeatRigs[BotSeat].Controller!;
        var botCopy = guest.SeatRigs[BotSeat].Controller!;
        var guestOwn = guest.SeatRigs[GuestSeat].Controller!;
        var guestCopy = host.SeatRigs[GuestSeat].Controller!;

        float copyBefore = Ledger(guestCopy);
        float ownBefore = Ledger(guestOwn);
        guestCopy.Body!.TakeProjectileHit(gun, guestCopy.WorldPosition, 0, bot.PlayerIndex);
        ctx.Check(Mathf.IsEqualApprox(Ledger(guestCopy), copyBefore),
            $"[{cell}] the host spends nothing on its copy of the guest for the bot's round ({Ledger(guestCopy):0.0} of {copyBefore:0.0})");
        int steps = StepUntil(() => Ledger(guestOwn) < ownBefore, peers);
        ctx.Check(Ledger(guestOwn) < ownBefore,
            $"[{cell}] and the guest's own aeroplane takes the bot's hit ({ownBefore:0.0} to {Ledger(guestOwn):0.0}, {steps} step(s))");

        float botBefore = Ledger(bot);
        float botCopyBefore = Ledger(botCopy);
        botCopy.Body!.TakeProjectileHit(gun, botCopy.WorldPosition, 0, guestOwn.PlayerIndex);
        ctx.Check(Mathf.IsEqualApprox(Ledger(botCopy), botCopyBefore),
            $"[{cell}] the guest spends nothing on its copy of the bot for its own round ({Ledger(botCopy):0.0} of {botCopyBefore:0.0})");
        steps = StepUntil(() => Ledger(bot) < botBefore && Mirrors(botCopy, bot), peers);
        ctx.Check(Ledger(bot) < botBefore,
            $"[{cell}] the host applies the guest's hit to the bot it owns ({botBefore:0.0} to {Ledger(bot):0.0}, {steps} step(s))");
        ctx.Check(Mirrors(botCopy, bot),
            $"[{cell}] and the bot's ledger reaches the guest's copy ({Ledger(botCopy):0.0} against {Ledger(bot):0.0})");

        // ABLE-TO-FAIL CONTROL. The host's own round on its own bot spends at once: both aeroplanes
        // are this machine's. The silences above are the routing, not a hit that does nothing.
        float waiting = Ledger(bot);
        bot.Body!.TakeProjectileHit(gun, bot.WorldPosition, 0, host.SeatRigs[HostSeat].Controller!.PlayerIndex);
        ctx.Check(Ledger(bot) < waiting,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the host's own round on its bot spends at once ({waiting:0.0} to {Ledger(bot):0.0})");
    }

    // Both directions of a kill with the bot in it, each as a lethal claim through the router, the
    // debug kill key's path. The host alone scores, and the guest's board is written from it.
    private static void Kills(TestContext ctx, string cell, GameSession[] peers)
    {
        var (host, guest) = (peers[0], peers[1]);
        foreach (var rig in peers.SelectMany(p => p.SeatRigs))
        {
            if (rig.Controller is { } pilot)
            {
                pilot.AutoRespawnAfter = QuickRespawn;
            }
        }

        int kill = host.Dogfight!.Match.Scores.Kill;
        var bot = host.SeatRigs[BotSeat].Controller!;
        var botCopy = guest.SeatRigs[BotSeat].Controller!;
        var guestOwn = guest.SeatRigs[GuestSeat].Controller!;
        var guestCopy = host.SeatRigs[GuestSeat].Controller!;
        Lift(host.SeatRigs[HostSeat].Controller!);
        Lift(guestOwn);
        Lift(bot);

        // The bot kills the guest. The host decides its round, and the guest's machine, which flies
        // the victim, reports the death with the bot's shooter id as the killer.
        var before = Board(host);
        var lethal = DebugKillTarget.LethalWeapon(bot)!;
        guestCopy.Body!.TakeProjectileHit(lethal, guestCopy.WorldPosition, 0, bot.PlayerIndex, LethalScale);
        var after = Expect(before, killer: BotSeat, victim: GuestSeat, kill);
        int steps = StepUntil(() => guestOwn.Crashed && Board(host).SequenceEqual(after) && Board(guest).SequenceEqual(after), peers);
        ctx.Check(guestOwn.Crashed && Board(host).SequenceEqual(after),
            $"[{cell}] the bot's kill of the guest scores the bot on the host ({Scoreboard(host)}, {steps} step(s))");
        ctx.Check(Board(guest).SequenceEqual(Board(host)) && Scoreboard(guest) == Scoreboard(host),
            $"[{cell}] and the guest's standings are the host's ({Scoreboard(guest)})");
        ctx.Check(PaneLines(guestOwn).Contains("Destroyed by bot", StringComparison.Ordinal),
            $"[{cell}] and the guest's pane names the bot by its callsign ({PaneLines(guestOwn)})");

        steps = StepUntil(() => peers.All(p => p.SeatRigs.All(r => r.Controller is { Crashed: false })), peers);
        ctx.Check(peers.All(p => p.SeatRigs.All(r => r.Controller is { Crashed: false })),
            $"[{cell}] every seat flies again on both machines before the next kill ({steps} step(s))");

        // The guest kills the bot. The guest decides its round and claims it on the host, which
        // owns the bot. The bot dies there and reports the guest's seat as the killer.
        Lift(host.SeatRigs[HostSeat].Controller!);
        Lift(guestOwn);
        Lift(bot);
        before = Board(host);
        var guestLethal = DebugKillTarget.LethalWeapon(guestOwn)!;
        botCopy.Body!.TakeProjectileHit(guestLethal, botCopy.WorldPosition, 0, guestOwn.PlayerIndex, LethalScale);
        ctx.Check(!botCopy.Crashed,
            $"[{cell}] the guest's lethal round crashes nothing of its own copy of the bot at once");
        after = Expect(before, killer: GuestSeat, victim: BotSeat, kill);
        steps = StepUntil(() => bot.Crashed && botCopy.Crashed
            && Board(host).SequenceEqual(after) && Board(guest).SequenceEqual(after), peers);
        ctx.Check(bot.Crashed && botCopy.Crashed,
            $"[{cell}] the bot dies on the host that owns it and on the guest's copy (host {bot.Crashed}, guest {botCopy.Crashed}, {steps} step(s))");
        ctx.Check(Board(host).SequenceEqual(after),
            $"[{cell}] the guest's kill of the bot scores the guest on the host ({Scoreboard(host)})");
        ctx.Check(Board(guest).SequenceEqual(Board(host)) && Scoreboard(guest) == Scoreboard(host),
            $"[{cell}] and the guest's standings are the host's ({Scoreboard(guest)})");

        // A downed bot is granted its return through the host's rotation like any seat, and flies
        // again on its AI pilot.
        var botPilot = bot.Pilot;
        steps = StepUntil(() => !bot.Crashed && !botCopy.Crashed, peers);
        var at = bot.WorldPosition;
        Lockstep(30, peers);
        ctx.Check(!bot.Crashed && !botCopy.Crashed && ReferenceEquals(bot.Pilot, botPilot)
                  && bot.WorldPosition.DistanceTo(at) > 10f,
            $"[{cell}] the downed bot comes back on both machines and flies on its pilot ({Downs(peers)}, {steps} step(s))");
        ctx.Check(peers.All(p => p.Wire.World is { Admitted: 0 }),
            $"[{cell}] and its return admits nothing to the world link ({string.Join(", ", peers.Select(p => p.Wire.World?.Admitted))})");
    }

    // A host with two bots and a guest on one launch, started. Every gunner is held off so no bot
    // round adds a death, and every aeroplane is lifted clear of the ground by its owner. Null
    // when either session failed to build.
    private static GameSession[]? Field(TestContext ctx, SessionSpec spec, int seed, string cell,
        List<NetCombatSuites.Ends> ends)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(seed));
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = HostSeat, FlownHere = true, Callsign = "host", PlaneNode = Airframes[0] },
            new() { PeerId = 1, SeatIndex = GuestSeat, Callsign = "guest", PlaneNode = Airframes[1] },
            NetSeats.Bot(0, BotSeat, "bot", Airframes[0]),
            NetSeats.Bot(0, SecondBotSeat, "other bot", Airframes[1]),
        };
        NetSeats.Validate(roster, hostPeer: 0);
        var host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, roster, Airframes);
        ends.Add(host);
        var guest = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null, Airframes);
        ends.Add(guest);
        ctx.Check(host.Built && guest.Built,
            $"[{cell}] both sessions build in one process (host {host.Built}, guest {guest.Built})");
        if (!host.Built || !guest.Built)
        {
            return null;
        }

        NetStartSuites.UntilStarted(host.Session, guest.Session);
        var peers = new[] { host.Session, guest.Session };
        Lockstep(1, peers);
        foreach (int seat in new[] { BotSeat, SecondBotSeat })
        {
            if (host.Session.SeatRigs[seat].Controller?.Pilot?.Gunner is { } gunner)
            {
                gunner.AutoTarget = false;
                gunner.Target = null;
            }
        }

        Lift(host.Session.SeatRigs[HostSeat].Controller!);
        Lift(host.Session.SeatRigs[BotSeat].Controller!);
        Lift(host.Session.SeatRigs[SecondBotSeat].Controller!);
        Lift(guest.Session.SeatRigs[GuestSeat].Controller!);
        Lockstep(1, peers);
        return peers;
    }

    // Auto Respawn off. The bot and the guest's own seat go down on the same step. The bot is back
    // on the host's granted entry after the crash camera time a person's auto-respawn waits. The
    // guest's seat still waits for its pilot's Fire Guns. The bot's pilot is given a quarry, a
    // chase and a stale course before it dies, and none of it survives the return.
    private static void ReturnsUnasked(TestContext ctx, GameSession[] peers, IReadOnlyList<SpawnPoint> table)
    {
        const string cell = "no auto respawn";
        var (host, guest) = (peers[0], peers[1]);
        var bot = host.SeatRigs[BotSeat].Controller!;
        var botCopy = guest.SeatRigs[BotSeat].Controller!;
        var guestOwn = guest.SeatRigs[GuestSeat].Controller!;
        var guestCopy = host.SeatRigs[GuestSeat].Controller!;
        var hostPlane = host.SeatRigs[HostSeat].Controller!;
        ctx.Check(!bot.RespawnOnFire && bot.AutoRespawnAfter == VersusDirector.RespawnDelay,
            $"[{cell}] the bot's seat respawns on the crash camera's {bot.AutoRespawnAfter} s without waiting for Fire Guns (waits {bot.RespawnOnFire})");
        ctx.Check(guestOwn.RespawnOnFire && guestOwn.AutoRespawnAfter == bot.AutoRespawnAfter,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the guest's own seat on the same launch waits for Fire Guns after the same {guestOwn.AutoRespawnAfter} s (waits {guestOwn.RespawnOnFire})");

        var pilot = bot.Pilot!;
        var gunner = pilot.Gunner!;
        var machine = pilot.Machine!;
        gunner.TakeTarget(hostPlane, default, 0d);
        machine.Enter(AiMode.Pursue, "a chase the suite stands up");
        pilot.TargetHeadingDeg = AiPilot.HeadingDegOf(bot.NoseDirection) + 90f;
        pilot.TargetAltitude = 1f;
        ctx.Check(gunner.Target != null && machine is { Mode: AiMode.Pursue, PursuitAnchor: not null },
            $"ABLE-TO-FAIL CONTROL: [{cell}] the bot dies with a quarry ({FlightController.TargetLabel(gunner.Target)}) and a chase ({AiModeMachine.NameOf(machine.Mode)}) standing");

        int before = host.Dogfight!.SpawnsTaken;
        int guestBefore = guest.Dogfight!.SpawnsTaken;
        bot.DebugForceCrash(guestCopy.PlayerIndex);
        guestOwn.DebugForceCrash(hostPlane.PlayerIndex);
        int due = Mathf.RoundToInt(VersusDirector.RespawnDelay / GameClock.FixedDt);
        int down = 0;
        while (bot.Crashed && down < due + WaitSteps)
        {
            Lockstep(1, peers);
            down++;
        }

        // Read on the step the grant placed it, before the pilot has flown a metre of its own.
        int entry = host.Dogfight.SpawnEntries[BotSeat];
        int standing = EntryAt(table, bot);
        float heading = AiPilot.HeadingDegOf(bot.NoseDirection);
        float headingError = Mathf.Abs(Mathf.Wrap(pilot.TargetHeadingDeg - heading, -180f, 180f));
        string course = $"course {pilot.TargetHeadingDeg:0.0} against a nose of {heading:0.0}, altitude {pilot.TargetAltitude:0} at {bot.WorldPosition.Y:0} m";
        ctx.Check(!bot.Crashed && down >= due - 1 && down <= due + 2,
            $"[{cell}] the downed bot returns on its own after the crash camera time ({down} step(s) against {due})");
        ctx.Check(entry >= 0 && entry < table.Count && standing == entry && host.Dogfight.SpawnsTaken == before + 1,
            $"[{cell}] on the rotation entry the host granted (entry {entry}, aeroplane on {standing}, {host.Dogfight.SpawnsTaken - before} grant(s))");
        ctx.Check(gunner.Target == null && gunner.TargetRankFor == null && !gunner.WantsFire,
            $"[{cell}] its gunner comes back with no quarry ({FlightController.TargetLabel(gunner.Target)})");
        ctx.Check(machine is { Mode: AiMode.Patrol, PursuitAnchor: null, Executor: null, Evading: false } && !pilot.IsStunned,
            $"[{cell}] and its mode machine with no chase, reaction or stun standing ({AiModeMachine.NameOf(machine.Mode)}, anchor {machine.PursuitAnchor?.ToString() ?? "none"})");
        ctx.Check(headingError < 0.5f && Mathf.Abs(pilot.TargetAltitude - bot.WorldPosition.Y) < 0.5f
                  && Mathf.IsEqualApprox(pilot.Throttle, bot.Throttle),
            $"[{cell}] and its pilot holds the course it was placed on ({course}, throttle {pilot.Throttle:0.00} on a lever of {bot.Throttle:0.00})");

        int steps = StepUntil(() => !botCopy.Crashed && guest.Dogfight.SpawnsTaken > guestBefore, peers);
        ctx.Check(!botCopy.Crashed && guest.Dogfight.SpawnEntries[BotSeat] == entry,
            $"[{cell}] the guest places its copy of the bot on the same entry ({guest.Dogfight.SpawnEntries[BotSeat]} against {entry}, {steps} step(s))");

        var at = bot.WorldPosition;
        Lockstep(HeldSteps, peers);
        ctx.Check(ReferenceEquals(bot.Pilot, pilot) && !bot.Crashed && bot.WorldPosition.DistanceTo(at) > 10f,
            $"[{cell}] and the bot flies on the same pilot ({bot.WorldPosition.DistanceTo(at):0} m since its return)");
        ctx.Check(guestOwn.Crashed && guestCopy.Crashed,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the guest's own seat, downed on the same step, is still down on both machines ({Downs(peers)})");
    }

    // One life each. The first bot's death spends its seat, which stays down on both machines with
    // no grant from the host. Three pilots with lives keep the match running. The guest's
    // death then leaves the host and the second bot, and the match runs on, so a living bot is an
    // opponent. The second bot's death leaves the host alone, which is reason 4 on both machines.
    private static void SpendsItsLife(TestContext ctx, GameSession[] peers)
    {
        const string cell = "one life";
        var (host, guest) = (peers[0], peers[1]);
        foreach (var rig in peers.SelectMany(p => p.SeatRigs))
        {
            if (rig.Controller is { } pilot)
            {
                pilot.AutoRespawnAfter = QuickRespawn;
            }
        }

        ctx.Check(peers.All(p => p.Dogfight!.Match.Lives == 1),
            $"[{cell}] both machines run one life ({string.Join(", ", peers.Select(p => p.Dogfight!.Match.Lives))})");
        var bot = host.SeatRigs[BotSeat].Controller!;
        var other = host.SeatRigs[SecondBotSeat].Controller!;
        var guestOwn = guest.SeatRigs[GuestSeat].Controller!;

        int grants = host.Dogfight!.SpawnsTaken;
        // Every death here is unattributed. Three kills to one pilot reach the launch's kill target,
        // which would end the match on its score before the field ran out.
        bot.DebugForceCrash();
        int steps = StepUntil(() => peers.All(p => p.Dogfight!.Match.OutOfLives(BotSeat)), peers);
        Lockstep(HeldSteps, peers);
        ctx.Check(peers.All(p => p.SeatRigs[BotSeat].Controller is { Crashed: true, Spectating: true }),
            $"[{cell}] the downed bot stays down on both machines, out of lives ({Downs(peers)}, {steps} step(s) for the score)");
        ctx.Check(host.Dogfight.SpawnsTaken == grants,
            $"[{cell}] and the host grants it no return ({host.Dogfight.SpawnsTaken - grants} grant(s))");
        ctx.Check(peers.All(p => p.Dogfight!.End == NetMatchEnd.Running && !p.Dogfight.Match.Completed),
            $"[{cell}] with three pilots left the match runs on both machines ({Endings(peers)})");

        guestOwn.DebugForceCrash();
        steps = StepUntil(() => peers.All(p => p.Dogfight!.Match.OutOfLives(GuestSeat)), peers);
        Lockstep(HeldSteps, peers);
        ctx.Check(peers.All(p => p.Dogfight!.Match.OutOfLives(GuestSeat))
                  && peers.All(p => p.Dogfight!.End == NetMatchEnd.Running && !p.Dogfight.Match.Completed),
            $"[{cell}] with the guest spent, the second bot is the host's opponent and the match runs on both machines ({Endings(peers)}, {steps} step(s))");

        other.DebugForceCrash();
        steps = StepUntil(() => peers.All(p => p.Dogfight!.End == NetMatchEnd.NobodyLeft), peers);
        ctx.Check(peers.All(p => p.Dogfight!.Match.Completed && p.Dogfight.End == NetMatchEnd.NobodyLeft),
            $"[{cell}] the second bot's death leaves the host alone and ends the match on nobody left to fight on both machines ({Endings(peers)}, {steps} step(s))");
    }

    private static string Endings(GameSession[] peers) =>
        string.Join(", ", peers.Select(p => p.Dogfight!.End));

    // The board as one (score, kills, deaths) per seat, the host's count or a guest's mirror of it.
    private static (int Score, int Kills, int Deaths)[] Board(GameSession session)
    {
        var match = session.Dogfight!.Match;
        return Enumerable.Range(0, match.PlayerCount)
            .Select(s => (match.ScoreOf(s), match.KillsOf(s), match.DeathsOf(s))).ToArray();
    }

    // The board one charged kill moves to. The killer takes a kill's score and a kill. The victim
    // takes a death and loses nothing off its score, as player.zrd's values have it.
    private static (int Score, int Kills, int Deaths)[] Expect((int Score, int Kills, int Deaths)[] before,
        int killer, int victim, int kill)
    {
        var after = ((int Score, int Kills, int Deaths)[])before.Clone();
        after[killer] = (after[killer].Score + kill, after[killer].Kills + 1, after[killer].Deaths);
        after[victim] = (after[victim].Score, after[victim].Kills, after[victim].Deaths + 1);
        return after;
    }

    private static string Downs(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", p.SeatRigs.Select(r =>
            r.Controller is { } c ? (c.Crashed ? "down" : "up") : "-"))));

    // Whether the copy's whole pools stand where the owner's do, as fractions, within the wire's
    // 16-bit step.
    private static bool Mirrors(FlightController copy, FlightController owner)
    {
        const float Step = 1.5f / DamagePools.Full;
        var c = copy.Damage!;
        var o = owner.Damage!;
        return Mathf.Abs(Share(c.WholeArmor, c.WholeArmorMax) - Share(o.WholeArmor, o.WholeArmorMax)) <= Step
            && Mathf.Abs(Share(c.WholeHealth, c.WholeHealthMax) - Share(o.WholeHealth, o.WholeHealthMax)) <= Step;
    }

    private static float Share(float current, float max) => max > 0f ? current / max : 1f;

    // Both damage pools together. A round spends armour before health, so a single strike on a
    // pristine airframe moves the armour alone.
    private static float Ledger(FlightController rig) =>
        rig.Damage!.WholeArmor + rig.Damage.WholeHealth;

    // Mean distance between the guest's shown path and the host's own, at the best whole-step lag.
    private static float Track(IReadOnlyList<Vector3> own, IReadOnlyList<Vector3> shown)
    {
        float best = float.MaxValue;
        for (int lag = 0; lag <= MaxLagSteps; lag++)
        {
            float sum = 0f;
            int n = 0;
            for (int i = MaxLagSteps; i < shown.Count; i++)
            {
                sum += shown[i].DistanceTo(own[i - lag]);
                n++;
            }

            best = Mathf.Min(best, sum / n);
        }

        return best;
    }

    // Steps both sessions until the condition holds, and answers how many steps that took, or
    // the whole budget when it never did.
    private static int StepUntil(Func<bool> done, GameSession[] peers)
    {
        for (int step = 1; step <= WaitSteps; step++)
        {
            Lockstep(1, peers);
            if (done())
            {
                return step;
            }
        }

        return WaitSteps;
    }

    // Both sessions through the same number of fixed steps, host first, the order a listen
    // server runs in.
    private static void Lockstep(int steps, GameSession[] peers)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in peers)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }
}

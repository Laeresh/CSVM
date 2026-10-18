using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Bots in Zeppelin vs Zeppelin, between a host and a guest in one process on the chapter's
/// <c>MP3</c> map and <see cref="NetCombatSuites"/>'s rig. The host flies lobby team 2 alone, listed
/// first, so its hull is <c>multiplayer1zep</c>. The guest and two bots fly team 1 and
/// <c>multiplayer2zep</c>. The host flies both bots and scores every part.</summary>
internal static class NetZvzBotSuites
{
    private const ulong HostSeed = 0xB07C0DE5UL;

    private const int HostSeat = 0;
    private const int GuestSeat = 1;
    private const int GunBot = 2;
    private const int TorpedoBot = 3;

    private const string EnemyHull = "multiplayer1zep";
    private const string OwnHull = "multiplayer2zep";

    private const int SettleSteps = 20;

    // The longest the gun bot is given to fly in from its start and open fire, two minutes of sim.
    private const int MaxFlightSteps = 7200;

    // The longest its rounds are given to land once its guns open, a minute of sim. The physics
    // space is brought in step every few steps (docs/verification.md INSTR-13).
    private const int MaxLandingSteps = 3600;
    private const int SyncEvery = 5;

    // How long the torpedo bot is watched between launches: the wingman rule's 20 s lockout.
    private const int LockoutSteps = 1200;

    // How long the torpedo bot is given to take a gas bag again once its torpedo is ready.
    private const int ReturnSteps = 120;

    // How far beyond the enemy hull the gun bot starts, facing away. That is past its 2000 m attack
    // radius, so the Deathmatch ranking alone never offers it a hull part.
    private const float StartRange = 2600f;

    private const float QuickRespawn = 0.5f;

    // A part's death and its score on the guest, with room to spare.
    private const int PartSteps = 10;

    // How long the control watches the bot with the objective off: a second of sim.
    private const int ControlSteps = 60;

    private const float Overkill = 100000f;

    // The people loop well over every hill and kilometres from both hulls' routes, as in
    // net-zeppelin-vs-zeppelin.
    private const float ParkX = -2000f;
    private const float ParkZ = -6000f;
    private const float ParkHeight = 1500f;
    private const float ParkSpacing = 600f;

    private const int ScoreTarget = 30;

    private const string TrackedFlight = "--hold=0.3,0,0,1";

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    [Suite("net-zvz-bot",
        "a host and a guest on the chapter's MP3 map with two bots on the guest's team: a stock gun "
        + "bot started past its attack radius takes the enemy hull's nearest cannon out of the hull, "
        + "else turret, else engine, flies in until its guns open and its own rounds land on a "
        + "cannon it picked, and the host maps a kill of that cannon in the bot's name to the bot's "
        + "seat on both machines, while no part of its own hull is ever its target or carries its "
        + "rounds; a Black Hat Warhawk bot carrying torpedoes takes the enemy hull's nearest gas bag, "
        + "flies in until its rocketeer launches a torpedo at it, guns a cannon, turret or engine of "
        + "that hull while the torpedo is locked out, and takes a gas bag again once it is ready; an enemy's hit on the bots' own hull turns the gun bot onto that enemy; and the "
        + "ABLE-TO-FAIL CONTROL, the gun bot from the same start with the objective off, takes no "
        + "hull part")]
    internal static void BotsAttackTheEnemyHull(TestContext ctx)
    {
        var spec = Spec(ctx);
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            if (Open(ctx, spec, ends) is not { } peers)
            {
                return;
            }

            Park(peers);
            Lockstep(SettleSteps, peers);
            Flight(ctx, peers);
            Torpedoes(ctx, peers);
            Defends(ctx, peers);
            Control(ctx, peers);
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

    private static SessionSpec Spec(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, SessionSpec.ZvzMission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{ctx.Chapter}/{SessionSpec.ZvzMission} zrdr");
        return SessionSpec.Parse(new[]
        {
            "--vs", $"--chapter={ctx.Chapter}", $"--mission={SessionSpec.ZvzMission}", "--players=1", "--mute",
            "--no-pads", "--zvz", TrackedFlight, $"--vs-kills={ScoreTarget}",
        });
    }

    // Both sessions on a clean loopback, the host carrying the roster with both bots. Null when a
    // build fails or the hulls are not seated one per team.
    private static GameSession[]? Open(TestContext ctx, SessionSpec spec, List<NetCombatSuites.Ends> ends)
    {
        string warhawk = InstantAction.PlaneNodeFor("Warhawk") ?? "";
        int blackHat = InstantAction.Militias.ToList().FindIndex(m => m.Name == "Black Hat");
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = HostSeat, FlownHere = true, Callsign = "host", PlaneNode = "player_pfighter", TeamId = 2 },
            new() { PeerId = 1, SeatIndex = GuestSeat, Callsign = "guest", PlaneNode = "player_fbrand", TeamId = 1 },
            NetSeats.Bot(0, GunBot, "gunner", "player_pfighter", team: 1),
            NetSeats.Bot(0, TorpedoBot, "torpedo", warhawk, team: 1) with
            {
                Militia = (byte)blackHat,
                Accent = (byte)InstantAction.Militias[blackHat].AccentId,
            },
        };
        NetSeats.Validate(roster, hostPeer: 0);
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(4156));
        ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, roster, teamNames: TeamNames));
        ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null, teamNames: TeamNames));
        ctx.Check(ends.All(e => e.Built), $"both sessions build in one process ({string.Join(", ", ends.Select(e => e.Built))})");
        if (!ends.All(e => e.Built))
        {
            return null;
        }

        var peers = ends.Select(e => e.Session).ToArray();
        ctx.Check(peers.All(p => p.Dogfight?.ZvzPlay is { } zvz && zvz.Rules.TeamOfHull(0) == 2 && zvz.Rules.TeamOfHull(1) == 1
                                 && p.ZeppelinHulls?.NodeAt(0) == EnemyHull && p.ZeppelinHulls.NodeAt(1) == OwnHull),
            $"both machines fly {EnemyHull} for team 2 and {OwnHull} for the bots' team 1");
        var bots = new[] { GunBot, TorpedoBot }.Select(s => peers[0].SeatRigs[s].Controller).ToArray();
        ctx.Check(bots.All(b => b is { IsBotSeat: true, Pilot.Gunner: not null }),
            $"the host flies both bot seats on an armed AI pilot");
        return peers.All(p => p.Dogfight?.ZvzPlay != null) && bots.All(b => b?.Pilot?.Gunner != null) ? peers : null;
    }

    // Quick returns, and both people moved far off their opening blocks. The torpedo bot is parked with
    // its gunner held off until its own reading, so the gun bot's readings are its alone.
    private static void Park(GameSession[] peers)
    {
        foreach (var rig in peers.SelectMany(p => p.SeatRigs))
        {
            if (rig.Controller is { } pilot)
            {
                pilot.AutoRespawnAfter = QuickRespawn;
            }
        }

        for (int seat = 0; seat < 2; seat++)
        {
            var high = new Vector3(ParkX - (ParkSpacing * seat), ParkHeight, ParkZ);
            peers[seat].SeatRigs[seat].Controller!.RespawnAt(high, high + (Vector3.Right * 100f));
        }

        var torpedo = peers[0].SeatRigs[TorpedoBot].Controller!;
        torpedo.Pilot!.Gunner!.AutoTarget = false;
        var away = new Vector3(ParkX, ParkHeight, ParkZ + 2000f);
        torpedo.RespawnAt(away, away + (Vector3.Right * 100f));
    }

    // The Black Hat Warhawk bot starts past its attack radius and takes the enemy's nearest live gas
    // bag. It flies in and its rocketeer's torpedo match launches at the bag. It guns the hull while
    // the lockout runs, and takes a gas bag again once the torpedo is ready.
    private static void Torpedoes(TestContext ctx, GameSession[] peers)
    {
        var host = peers[0];
        var bot = host.SeatRigs[TorpedoBot].Controller!;
        var gunner = bot.Pilot!.Gunner!;
        gunner.AutoTarget = true;
        Start(host, bot);
        Lockstep(1, peers);
        var want = Nearest(host, bot, HullPartKind.Gasbag);
        int carried = Torpedoes(bot);
        ctx.Check(carried > 0 && want != null && ReferenceEquals(gunner.Target, want),
            $"the Black Hat Warhawk bot, carrying {carried} torpedo(es), takes {EnemyHull}'s nearest gas bag from {Gap(host, bot):0} m ({TargetPool.TargetLabel(gunner.Target)} against {TargetPool.TargetLabel(want)})");

        // The target the launch step started on: by the step's end the lockout already has the bot on
        // the gun order.
        int steps = 0;
        object? launchedAt = null;
        for (; steps < MaxFlightSteps && Torpedoes(bot) == carried; steps++)
        {
            launchedAt = gunner.Target;
            Lockstep(1, peers);
        }

        ctx.Check(Torpedoes(bot) < carried && launchedAt is DestructibleRegistry.Instance { Gasbag: true },
            $"and flies in until its rocketeer launches a torpedo at {TargetPool.TargetLabel(launchedAt)}, in {steps} step(s) ({carried} to {Torpedoes(bot)})");
        // The gasbag gate hides the bags while the torpedo is locked out, so the bot guns the hull.
        var enemy = Parts(host, 0);
        object? gunned = null;
        int watched = 0;
        for (; watched < LockoutSteps && bot.Pilot.Rocketeer!.LockoutRemaining > 0f; watched++)
        {
            Lockstep(1, peers);
            if (gunner.Target is { } held && enemy.Contains(held) && held is not DestructibleRegistry.Instance { Gasbag: true })
            {
                gunned ??= held;
            }
        }

        ctx.Check(watched > 0 && gunned != null,
            $"and while its torpedo is locked out it guns a cannon, turret or engine of {EnemyHull} ({TargetPool.TargetLabel(gunned)} in {watched} step(s) of lockout)");
        int back = 0;
        for (; back < ReturnSteps && gunner.Target is not DestructibleRegistry.Instance { Gasbag: true }; back++)
        {
            Lockstep(1, peers);
        }

        ctx.Check(gunner.Target is DestructibleRegistry.Instance { Gasbag: true },
            $"and once its torpedo is ready again it is back on a gas bag, in {back} step(s) ({TargetPool.TargetLabel(gunner.Target)})");
    }

    // The gun bot starts past its attack radius and takes the enemy hull's first standing class of
    // part. It flies in until its guns open on that hull and its own rounds land on a cannon it
    // picked. A natural kill takes longer than a suite can fly. So that cannon's kill is claimed in
    // its name on the host, which reads whether the host maps a bot's kill to its seat.
    private static void Flight(TestContext ctx, GameSession[] peers)
    {
        var host = peers[0];
        var bot = host.SeatRigs[GunBot].Controller!;
        var gunner = bot.Pilot!.Gunner!;
        var own = Parts(host, 1);
        var enemy = Parts(host, 0);
        var cannons = Cannons(host);
        var picked = new HashSet<object>();
        var scores = host.Dogfight!.Match.Scores;
        Start(host, bot);
        Lockstep(1, peers);
        var first = Nearest(host, bot, HullPartKind.Cannon) ?? Nearest(host, bot, HullPartKind.Turret) ?? Nearest(host, bot, HullPartKind.Engine);
        ctx.Check(first != null && ReferenceEquals(gunner.Target, first),
            $"the gun bot {Gap(host, bot):0} m beyond {EnemyHull} takes its nearest cannon out of the hull, else turret, else engine ({TargetPool.TargetLabel(gunner.Target)} against {TargetPool.TargetLabel(first)})");

        bool ownTargeted = false;
        int steps = 0;
        for (; steps < MaxFlightSteps && !(gunner.WantsFire && gunner.Target != null && enemy.Contains(gunner.Target)); steps++)
        {
            Lockstep(1, peers);
            Watch();
        }

        ctx.Check(gunner.WantsFire && gunner.Target != null && enemy.Contains(gunner.Target),
            $"and flies in until its guns open on {TargetPool.TargetLabel(gunner.Target)} at {bot.WorldPosition.DistanceTo(Where(gunner.Target)):0} m, in {steps} step(s)");

        // Its own rounds then land on a cannon it picked, the physics space kept in step with the hull.
        int landing = 0;
        for (; landing < MaxLandingSteps && Hit() == null; landing++)
        {
            if (landing % SyncEvery == 0)
            {
                ctx.SyncPhysics();
            }

            Lockstep(1, peers);
            Watch();
        }

        var cannon = Hit();
        ctx.Check(cannon != null,
            $"and its own rounds land on a cannon of {EnemyHull} it picked, in {landing} more step(s) ({(cannon == null ? "none" : $"{AnimRuntime.NameOf(cannon.Anchor)} {cannon.Health.ToString("0.#", CultureInfo.InvariantCulture)}/{cannon.MaxHealth:0}")})");
        if (cannon != null && host.Wire.World?.World is { } world)
        {
            world.DamageAt(cannon.Anchor, Overkill, bot.PlayerIndex);
            Lockstep(PartSteps, peers);
            ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(GunBot) == scores.GasbagKill),
                $"the host maps a kill of that cannon in the bot's name to the bot's seat, {scores.GasbagKill} on both machines ({Scores(peers)})");
        }

        var struck = Struck(own, bot).ToArray();
        ctx.Check(!ownTargeted && struck.Length == 0,
            $"and no part of {OwnHull} was ever its target or took its round (targeted {ownTargeted}, struck {struck.Length})");

        void Watch()
        {
            ownTargeted |= gunner.Target != null && own.Contains(gunner.Target);
            if (gunner.Target != null)
            {
                picked.Add(gunner.Target);
            }
        }

        DestructibleRegistry.Instance? Hit() =>
            Struck(enemy, bot).FirstOrDefault(p => picked.Contains(p) && cannons.Contains(p) && p.Status != DestructibleRegistry.State.Destroyed);
    }

    // The host's round on an engine of the bots' own hull names it the hull's attacker, and the gun
    // bot turns onto it. Before the hit its target is a hull part.
    private static void Defends(TestContext ctx, GameSession[] peers)
    {
        var host = peers[0];
        var bot = host.SeatRigs[GunBot].Controller!;
        var gunner = bot.Pilot!.Gunner!;
        var raider = host.SeatRigs[HostSeat].Controller!;
        // Set down whole again: the enemy hull's turrets have hurt it, and a hurt bot breaks off.
        Start(host, bot);
        Lockstep(1, peers);
        bool onHull = gunner.Target is DestructibleRegistry.Instance or TurretController;
        var engine = Parts(host, 1).OfType<DestructibleRegistry.Instance>()
            .FirstOrDefault(p => !p.Gasbag && p.Status != DestructibleRegistry.State.Destroyed && p.Health > 1f);
        if (engine != null && host.Wire.World?.World is { } world)
        {
            world.DamageAt(engine.Anchor, 1f, raider.PlayerIndex);
        }

        Lockstep(2, peers);
        ctx.Check(onHull && engine != null && ReferenceEquals(gunner.Target, raider),
            $"a hit by the host on {OwnHull}'s {(engine == null ? "none" : AnimRuntime.NameOf(engine.Anchor))} turns the bot from its hull part (was {onHull}) onto the host ({TargetPool.TargetLabel(gunner.Target)})");
    }

    // The ABLE-TO-FAIL CONTROL: the same bot from the same start with the objective off. Past its
    // attack radius the Deathmatch ranking offers it no hull part, where the objective took one on
    // the first step.
    private static void Control(TestContext ctx, GameSession[] peers)
    {
        var host = peers[0];
        var zvz = host.Dogfight!.ZvzPlay!;
        var bot = host.SeatRigs[GunBot].Controller!;
        var gunner = bot.Pilot!.Gunner!;
        var parts = Parts(host, 0).Concat(Parts(host, 1)).ToHashSet();
        zvz.BotObjective = false;
        try
        {
            Start(host, bot);
            bool targeted = false;
            for (int step = 0; step < ControlSteps; step++)
            {
                Lockstep(1, peers);
                targeted |= gunner.Target != null && parts.Contains(gunner.Target);
            }

            ctx.Check(!targeted,
                $"ABLE-TO-FAIL CONTROL: with the objective off the same bot from the same start takes no hull part in {ControlSteps} step(s) ({TargetPool.TargetLabel(gunner.Target)})");
        }
        finally
        {
            zvz.BotObjective = true;
        }
    }
    // A bot set down past the enemy hull on the line from its own, at the hull's height. It faces
    // away from both, and its pilot starts over.
    private static void Start(GameSession host, FlightController bot)
    {
        var enemy = host.ZeppelinHulls!.HullPositionAt(0)!.Value;
        var own = host.ZeppelinHulls.HullPositionAt(1)!.Value;
        var away = new Vector3(enemy.X - own.X, 0f, enemy.Z - own.Z).Normalized();
        var at = enemy + (away * StartRange);
        bot.Pilot!.Gunner!.Target = null;
        bot.RespawnAt(at, at + (away * 100f));
    }

    private static float Gap(GameSession host, FlightController bot) =>
        bot.WorldPosition.DistanceTo(host.ZeppelinHulls!.HullPositionAt(0)!.Value);

    // The sources of every part of hull index, standing or not.
    private static HashSet<object> Parts(GameSession host, int hull)
    {
        var into = new List<HullPart>();
        host.ZeppelinHulls!.CollectHullParts(hull, into);
        return into.Select(p => p.Source).ToHashSet();
    }

    private static object? Nearest(GameSession host, FlightController bot, HullPartKind kind)
    {
        var into = new List<HullPart>();
        host.ZeppelinHulls!.CollectHullParts(0, into);
        return into.Where(p => p.Live && p.Kind == kind)
            .OrderBy(p => p.Position.DistanceSquaredTo(bot.WorldPosition))
            .Select(p => p.Source).FirstOrDefault();
    }

    // Every broadside cannon pool of the enemy hull, out or stowed.
    private static List<DestructibleRegistry.Instance> Cannons(GameSession host)
    {
        var into = new List<HullPart>();
        host.ZeppelinHulls!.CollectHullParts(0, into);
        return into.Where(p => p.Kind == HullPartKind.Cannon).Select(p => p.Source).OfType<DestructibleRegistry.Instance>().ToList();
    }

    private static Vector3 Where(object? target) =>
        TargetPool.TryTargetGeometry(target, out var at, out _, out _, out _) ? at : Vector3.Zero;
    // The pools of a hull whose last hit was the bot's round.
    private static IEnumerable<DestructibleRegistry.Instance> Struck(HashSet<object> hull, FlightController bot) =>
        hull.OfType<DestructibleRegistry.Instance>().Where(p => p.LastShooter == bot.PlayerIndex);
    // The DAMAGES_ZEPPELIN rounds left on the bot's pylons.
    private static int Torpedoes(FlightController bot) =>
        bot.Loadout?.Hardpoints.Where(hp => hp.Weapon.DamagesZeppelin).Sum(hp => hp.Ammo) ?? 0;
    private static void Lockstep(int steps, params GameSession[] sessions)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in sessions)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }

    private static string Scores(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", Enumerable.Range(0, 4).Select(p.Dogfight!.Match.ScoreOf))));
}

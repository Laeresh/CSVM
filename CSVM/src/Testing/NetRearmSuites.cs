using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The multiplayer rearm bases between two whole sessions in one process, on
/// <see cref="NetCombatSuites"/>'s rig. The guest's own aeroplane is damaged, emptied and put at a
/// base. Its own machine decides the rearm, as the original's does for its aircraft.</summary>
internal static class NetRearmSuites
{
    private const ulong HostSeed = 0xC0FFEE90UL;

    private const int SettleSteps = 20;

    // A part's death, its health on the guest and each machine's poll of it, with room to spare.
    private const int PartSteps = 10;

    // Steps for the rearm's full-hull report to reach the host.
    private const int ReportSteps = 4;

    private const float Overkill = 100000f;

    // How far below a base's node the aeroplane is put: inside the 25 m radius, clear of the node.
    private const float UnderNode = 10f;

    // Far enough from every base to be outside its radius.
    private const float Away = 400f;

    // Steady loops at full throttle, which keep a waiting pilot at the height it was put.
    private const string TrackedFlight = "--hold=0.3,0,0,1";

    private const string Rearmed = "Rearmed!";

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels", [3] = "Green Hornets" };

    [Suite("net-rearm-deathmatch",
        "two sessions on the chapter's MP1 map as a team Deathmatch, the host on lobby team 1 and the "
        + "guest on team 3, which owns no rearm node: both machines list rearm_node_1 and rearm_node_2, "
        + "which serve any pilot; the guest's damaged, emptied aeroplane put at the base comes out at "
        + "full health, armour and ammunition with its selected pylon kept, its own pane alone posts "
        + "Rearmed!, and the host's copy hears the full hull; damaged again inside the radius it stays "
        + "damaged, and it rearms again only after leaving and coming back")]
    internal static void ADeathmatchBaseServesAnyPilot(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _, TrackedFlight);
        RunPair(ctx, spec, new[] { 1, 3 }, 9001, peers =>
        {
            if (!Bases(ctx, peers, RearmRule.AnyBase, "rearm_node_1 and rearm_node_2"))
            {
                return;
            }

            var guest = peers[1].SeatRigs[1].Controller!;
            var baseAt = peers[1].Dogfight!.RearmPlay!.BaseAt(0)!.Value.Position;
            var inside = baseAt + (Vector3.Down * UnderNode);

            Place(guest, inside);
            int pylon = Strip(guest);
            Lockstep(1, peers);
            Restored(ctx, peers, guest, pylon, "the team 3 guest at a base no node of its team names");

            Lockstep(ReportSteps, peers);
            ctx.Check(peers[0].RepairsTaken == 1 && peers[1].RepairsTaken == 0,
                $"and the host's copy of the guest hears its full hull once, the guest nothing from itself ({peers[0].RepairsTaken}/{peers[1].RepairsTaken})");
            OncePerEntry(ctx, peers, guest, baseAt);

            Place(guest, inside + (Vector3.Right * Away));
            Lockstep(2, peers);
            Place(guest, inside);
            Strip(guest);
            Lockstep(1, peers);
            ctx.Check(peers[1].Dogfight!.RearmPlay!.Rearms == 2 && guest.Damage!.SummaryHealthFraction >= 1f,
                $"after leaving the radius, a second entry rearms it again ({peers[1].Dogfight!.RearmPlay!.Rearms} rearms, hull {guest.Damage!.SummaryHealthFraction:0.##})");
        });
    }

    [Suite("net-rearm-zeppelins",
        "two sessions on the chapter's MP3 map as Zeppelin vs Zeppelin, the host on lobby team 2 and "
        + "the guest on team 1: both machines list zep_rearm_node_1 for team 2 and zep_rearm_node_2 "
        + "for team 1, each marker reading Rearm under its side's team name; the guest's damaged, emptied aeroplane is refused at team 2's node and restored "
        + "in full at its own, once per entry; and once its own hull is lost its node restores nothing "
        + "while the host still rearms at its living hull's node")]
    internal static void AZeppelinRearmsOnlyItsOwnSideWhileItLives(TestContext ctx)
    {
        var spec = ZvzSpec(ctx);
        RunPair(ctx, spec, new[] { 2, 1 }, 9002, peers =>
        {
            if (!Bases(ctx, peers, RearmRule.OwnTeam, "zep_rearm_node_1 and zep_rearm_node_2")
                || peers.Any(p => p.Dogfight?.ZvzPlay == null))
            {
                return;
            }

            var hulls = peers[0].ZeppelinHulls!;
            var rearm = peers[1].Dogfight!.RearmPlay!;
            ctx.Check(peers.All(p => p.Dogfight!.RearmPlay!.BaseAt(0)!.Value.Team == 2 && p.Dogfight!.RearmPlay!.BaseAt(1)!.Value.Team == 1),
                $"zep_rearm_node_1 serves team 2, the side of hull 0, and zep_rearm_node_2 team 1 on both machines ({string.Join(" | ", peers.Select(p => $"{p.Dogfight!.RearmPlay!.BaseAt(0)?.Team}/{p.Dogfight!.RearmPlay!.BaseAt(1)?.Team}"))})");
            var nearest = Enumerable.Range(0, 2).Select(i =>
            {
                var at = rearm.BaseAt(i)!.Value.Position;
                return at.DistanceTo(hulls.HullPositionAt(0)!.Value) < at.DistanceTo(hulls.HullPositionAt(1)!.Value) ? 0 : 1;
            }).ToArray();
            ctx.Note($"zep_rearm_node_1 rides hull {nearest[0]}, zep_rearm_node_2 hull {nearest[1]}");
            var markers = new List<string>();
            bool named = true;
            for (int machine = 0; machine < peers.Length; machine++)
            {
                var pool = NetTeamSuites.Cycles(peers[machine], machine);
                foreach (var (node, name) in new[] { ("zep_rearm_node_1", "Blue Angels"), ("zep_rearm_node_2", "Red Squadron") })
                {
                    var found = NetTeamSuites.RefOf(pool, s => s is ObjectiveSite site && site.Node.Equals(node, StringComparison.OrdinalIgnoreCase));
                    named &= found is { Category: "Rearm" } f && f.DisplayName == name;
                    markers.Add($"m{machine}:{node} {(found is { } g ? $"{g.Category}|{g.DisplayName}" : "missing")}");
                }
            }

            ctx.Check(named, $"every pane reads each hull's base as Rearm under its side's team name ({string.Join(", ", markers)})");

            var host = peers[0].SeatRigs[0].Controller!;
            var guest = peers[1].SeatRigs[1].Controller!;
            Place(guest, Under(rearm, 0));
            int pylon = Strip(guest);
            Lockstep(2, peers);
            ctx.Check(rearm.Rearms == 0 && guest.Damage!.SummaryHealthFraction < 1f && Emptied(guest),
                $"the team 1 guest at team 2's node is refused: still damaged and empty ({rearm.Rearms} rearms, hull {guest.Damage!.SummaryHealthFraction:0.##})");

            Place(guest, Under(rearm, 1));
            Strip(guest);
            Lockstep(1, peers);
            Restored(ctx, peers, guest, pylon, "the team 1 guest at its own hull's node");
            OncePerEntry(ctx, peers, guest, rearm.BaseAt(1)!.Value.Position);

            // Hull 1 flies team 1's side. Three of its bags down kill it and end the match.
            foreach (var bag in new[] { "gasbag1", "gasbag2", "gasbag3" })
            {
                Kill(peers, "multiplayer2zep", bag, seat: 0);
            }

            ctx.Check(peers.All(p => p.ZeppelinHulls!.IsDead("multiplayer2zep")),
                $"three bags down, multiplayer2zep is lost on both machines ({string.Join(", ", peers.Select(p => p.ZeppelinHulls!.IsDead("multiplayer2zep")))})");
            int before = rearm.Rearms;
            Place(guest, rearm.BaseAt(1)!.Value.Position + (Vector3.Right * Away));
            Lockstep(2, peers);
            Place(guest, Under(rearm, 1));
            Strip(guest);
            Lockstep(2, peers);
            ctx.Check(rearm.Rearms == before && guest.Damage!.SummaryHealthFraction < 1f,
                $"with its hull lost, the guest's own node restores nothing ({rearm.Rearms - before} rearms, hull {guest.Damage!.SummaryHealthFraction:0.##})");

            var hostRearm = peers[0].Dogfight!.RearmPlay!;
            Place(host, Under(hostRearm, 0));
            Strip(host);
            Lockstep(1, peers);
            ctx.Check(hostRearm.Rearms == 1 && host.Damage!.SummaryHealthFraction >= 1f,
                $"ABLE-TO-FAIL CONTROL: the host's team 2 pilot still rearms at its living hull's node ({hostRearm.Rearms} rearms, hull {host.Damage!.SummaryHealthFraction:0.##})");
        });
    }

    [Suite("net-lobby-deathmatch-mp1",
        "a free-for-all Deathmatch launched through the menu's own spec, the host's command line "
        + "naming --mission=IA1 and the guest's none: both fly the chapter's MP1, every seat opens on "
        + "its own entry of net.zrd's free-for-all block, the same on both machines, and the rearm "
        + "bases mp1.gw leaves on stand in the match and restore the guest's damaged, emptied aeroplane")]
    internal static void ALobbyDeathmatchFliesMp1WithItsBases(TestContext ctx)
    {
        string[] args = { "--mute", "--no-pads", TrackedFlight };
        var hostSpec = SessionSpec.FromMenu(SessionSpec.Parse(args.Append("--mission=IA1").ToArray()), ctx.Chapter,
            new[] { "player_pfighter" }, MenuMode.Versus);
        var guestSpec = SessionSpec.FromMenu(SessionSpec.Parse(args), ctx.Chapter, new[] { "player_pfighter" }, MenuMode.Versus);
        ctx.Check(hostSpec.Mission == SessionSpec.DeathmatchMission && guestSpec.Mission == SessionSpec.DeathmatchMission
                  && hostSpec is { Versus: true, CaptureTheFlag: false, ZeppelinVsZeppelin: false },
            $"both machines' menu launches fly {ctx.Chapter}'s MP1 as a Deathmatch whatever their command lines name ({hostSpec.Mission}, {guestSpec.Mission})");

        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, SessionSpec.DeathmatchMission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{ctx.Chapter}/{SessionSpec.DeathmatchMission} zrdr");
        var block = SpawnPoints.LoadNetFreeForAll(missionZrdr);
        if (block is not { Count: >= 2 })
        {
            throw new SuiteSkippedException($"{ctx.Chapter}/{SessionSpec.DeathmatchMission} authors no usable net.zrd block");
        }

        RunPair(ctx, hostSpec, new[] { 0, 0 }, 9003, peers =>
        {
            if (!Bases(ctx, peers, RearmRule.AnyBase, "rearm_node_1 and rearm_node_2"))
            {
                return;
            }

            var guest = peers[1].SeatRigs[1].Controller!;
            Place(guest, peers[1].Dogfight!.RearmPlay!.BaseAt(0)!.Value.Position + (Vector3.Down * UnderNode));
            int pylon = Strip(guest);
            Lockstep(1, peers);
            Restored(ctx, peers, guest, pylon, "the guest at MP1's base");
        }, guestSpec, opened: peers =>
        {
            var entries = peers.Select(p => p.SeatRigs.Select(r => BlockEntry(block, r.Controller)).ToArray()).ToArray();
            ctx.Check(entries[0].All(i => i >= 0) && entries[0].Distinct().Count() == entries[0].Length
                      && entries.All(e => e.SequenceEqual(entries[0])),
                $"every seat opens on its own entry of net.zrd block 0, the same on both machines ({string.Join(" | ", entries.Select(e => string.Join(",", e)))})");
        });
    }

    private static SessionSpec ZvzSpec(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, SessionSpec.ZvzMission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{ctx.Chapter}/{SessionSpec.ZvzMission} zrdr");
        return SessionSpec.Parse(new[]
        {
            "--vs", $"--chapter={ctx.Chapter}", $"--mission={SessionSpec.ZvzMission}", "--players=1", "--mute",
            "--no-pads", "--zvz", TrackedFlight,
        });
    }

    // Which free-for-all entry a placed aeroplane stands on, horizontally, or -1.
    private static int BlockEntry(IReadOnlyList<SpawnPoint> block, Node3D? placed)
    {
        for (int i = 0; placed != null && i < block.Count; i++)
        {
            var d = block[i].Position - placed.GlobalPosition;
            if (Mathf.Abs(d.X) < 1f && Mathf.Abs(d.Z) < 1f)
            {
                return i;
            }
        }

        return -1;
    }

    // Two sessions on lobby teams, each seat parked high and apart before the body runs. The guest
    // builds from its own spec when one is given, and the opening placement is read before any step.
    private static void RunPair(TestContext ctx, SessionSpec spec, int[] teams, int meshSeed, Action<GameSession[]> body,
        SessionSpec? guestSpec = null, Action<GameSession[]>? opened = null)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(meshSeed));
        var roster = NetCombatSuites.Roster(2).Select((seat, i) => seat with { TeamId = teams[i] }).ToArray();
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 2; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, i == 0 ? spec : guestSpec ?? spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i,
                    i == 0 ? roster : null, teamNames: TeamNames));
            }

            ctx.Check(ends.All(e => e.Built), $"two sessions build in one process ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            opened?.Invoke(peers);
            Lockstep(SettleSteps, peers);
            body(peers);
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

    // Both machines list the same two bases under the mode's rule.
    private static bool Bases(TestContext ctx, GameSession[] peers, RearmRule rule, string names)
    {
        ctx.Check(peers.All(p => p.Dogfight?.RearmPlay is { BaseCount: 2 } r && r.Rules.Rule == rule
                                 && Mathf.IsEqualApprox(r.Rules.RadiusSquared, RearmBases.InitialRadiusSquared)),
            $"both machines list {names} under {rule}, at player.zrd's radius, which it leaves at 25 m ({string.Join(" | ", peers.Select(p => p.Dogfight?.RearmPlay is { } r ? $"{r.BaseCount} {r.Rules.Rule} {r.Rules.RadiusSquared:0}" : "none"))})");
        if (peers.Any(p => p.Dogfight?.RearmPlay is not { BaseCount: 2 }))
        {
            return false;
        }

        var at = peers[1].Dogfight!.RearmPlay!;
        ctx.Note($"bases at {at.BaseAt(0)!.Value.Position} and {at.BaseAt(1)!.Value.Position}");
        return true;
    }

    // Damaged to half, every gun and pylon emptied, and the last pylon selected. Answers that pylon.
    private static int Strip(FlightController pilot)
    {
        pilot.Damage?.ScalePools(0.5f, 0.5f);
        foreach (var gun in pilot.Loadout?.Guns ?? Enumerable.Empty<GunGroup>())
        {
            gun.Ammo = 0;
        }

        foreach (var pylon in pilot.Loadout?.Hardpoints ?? Enumerable.Empty<Hardpoint>())
        {
            pylon.Ammo = 0;
        }

        int last = (pilot.Loadout?.Hardpoints.Count ?? 0) - 1;
        pilot.SelectPylon(Math.Max(last, 0));
        return pilot.SelectedPylon;
    }

    private static bool Emptied(FlightController pilot) =>
        pilot.Loadout is { } fit && fit.Guns.All(g => g.Ammo == 0) && fit.Hardpoints.All(h => h.Ammo == 0);

    private static bool Full(FlightController pilot) =>
        pilot.Loadout is { } fit && fit.Guns.All(g => g.Ammo == g.Capacity) && fit.Hardpoints.All(h => h.Ammo == h.Capacity);

    // The restore read on the guest, its own pane's line, and the host's pane left alone.
    private static void Restored(TestContext ctx, GameSession[] peers, FlightController guest, int pylon, string where)
    {
        var damage = guest.Damage!;
        bool whole = Mathf.IsEqualApprox(damage.WholeHealth, damage.WholeHealthMax)
            && Mathf.IsEqualApprox(damage.WholeArmor, damage.WholeArmorMax) && damage.WorstFraction >= 1f;
        ctx.Check(peers[1].Dogfight!.RearmPlay!.Rearms == 1 && whole && Full(guest) && guest.Loadout!.Hardpoints.Count > 0,
            $"{where} is restored to full health, armour and every gun and pylon ({peers[1].Dogfight!.RearmPlay!.Rearms} rearms, health {damage.WholeHealth:0}/{damage.WholeHealthMax:0}, armour {damage.WholeArmor:0}/{damage.WholeArmorMax:0}, full {Full(guest)})");
        ctx.Check(guest.SelectedPylon == pylon && pylon > 0,
            $"and keeps its selected pylon rather than the first ({guest.SelectedPylon}, selected {pylon})");
        string? own = guest.MessageStack?.LineAt(0);
        string? hosts = peers[0].SeatRigs[0].Controller!.MessageStack?.LineAt(0);
        ctx.Check(own == Rearmed && hosts != Rearmed,
            $"its own pane posts \"{Rearmed}\" and the host's pane does not ('{own}' / '{hosts}')");
    }

    // Damaged again while still inside the radius, the latch holds and nothing is restored.
    private static void OncePerEntry(TestContext ctx, GameSession[] peers, FlightController guest, Vector3 baseAt)
    {
        int before = peers[1].Dogfight!.RearmPlay!.Rearms;
        guest.Damage!.ScalePools(0.5f, 0.5f);
        Lockstep(2, peers);
        float off = guest.WorldPosition.DistanceSquaredTo(baseAt);
        ctx.Check(off <= RearmBases.InitialRadiusSquared && peers[1].Dogfight!.RearmPlay!.Rearms == before && guest.Damage.SummaryHealthFraction < 1f,
            $"damaged again inside the radius, it stays damaged: once per entry ({Mathf.Sqrt(off):0.#} m off, {peers[1].Dogfight!.RearmPlay!.Rearms - before} more rearms, hull {guest.Damage.SummaryHealthFraction:0.##})");
    }

    // Put at a point with a fresh collision window, so walls and hulls cannot end the flight.
    private static void Place(FlightController pilot, Vector3 at)
    {
        pilot.RespawnAt(at, at + (Vector3.Right * 100f));
        pilot.ArmSpawnTimers();
    }

    private static Vector3 Under(Session.World.RearmRuntime rearm, int index) =>
        rearm.BaseAt(index)!.Value.Position + (Vector3.Down * UnderNode);

    // A part killed on the host in one seat's name, as the host's copy of that seat's round would.
    private static void Kill(GameSession[] peers, string hull, string part, int seat)
    {
        var host = peers[0];
        var pool = host.NetWorld?.World?.Destructibles.All.FirstOrDefault(inst =>
            string.Equals(inst.Owner, hull, StringComparison.OrdinalIgnoreCase)
            && string.Equals(AnimRuntime.NameOf(inst.Anchor), part, StringComparison.OrdinalIgnoreCase));
        if (pool != null && host.NetWorld?.World is { } world)
        {
            world.DamageAt(pool.Anchor, Overkill, host.SeatRigs[seat].Controller!.PlayerIndex);
        }

        Lockstep(PartSteps, peers);
    }

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
}

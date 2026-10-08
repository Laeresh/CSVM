using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Race presence, the rules a race session puts on its pilots. No aircraft rams another and
/// nobody is armed. Another pilot is labelled but never a target, and draws as a ghost that fades by
/// the drawing camera's distance. Each rule is checked on a pair built through the session's roster
/// with the race flag set, against the same pair built without it.</summary>
internal static class RacePresenceSuites
{
    private const float Dt = 1f / 60f;

    // High over an empty host world, so nothing but the other aircraft is there to strike.
    private const float AltitudeM = 3000f;

    // The flown seat starts this far from the parked one, nose on, at this speed.
    private const float GapM = 60f;
    private const float SpeedMps = 100f;

    // The flown seat sinks about 2 m over the gap at that speed. It starts this much higher, so it
    // crosses the parked one's origin rather than passing under its belly.
    private const float SinkAllowanceM = 2f;

    // How many steps the flown seat gets to reach and clear the parked one.
    private const int RamFrames = 150;

    // How close the flown seat's origin must pass the parked one's to count as flying through it.
    // That is inside half a fuselage.
    private const float ThroughM = 2f;

    [Suite("race-presence",
        "two seats built through the session roster with the race flag set and without it: in the "
        + "race a seat flown nose on through a parked one passes through with no damage, no crash and "
        + "neither body on the aircraft layer, while without the flag the same pass rams (the "
        + "control); a race seat holding both triggers is unarmed and puts no round in the pool "
        + "while the control fires; the other race pilot is off every target cycle yet labelled, "
        + "while the control offers it on the Enemy cycle; and only the race build stamps the ghost "
        + "on the other airframe and compiles the ghost into its shaders")]
    internal static void RacePresence(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.MessagesPath, $"string table");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        // ⚠ Do not remove this eviction. A cached collidable world from an earlier suite shares the
        // host's one physics space, and would be struck instead of the other seat.
        ctx.EvictCollidableWorlds();
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        try
        {
            var race = FlyPair(ctx, planesGamez, textures, racing: true);
            var plain = FlyPair(ctx, planesGamez, textures, racing: false);
            ctx.Check(race.PassedThrough && !race.Rammed && race.BodiesOff,
                $"in the race the flown seat passes {race.Closest:0.00} m from the parked one's origin with no ram: health {race.Health}, crashed={race.Crashed}, bodies off the aircraft layer={race.BodiesOff}");
            ctx.Check(plain.Rammed,
                $"ABLE-TO-FAIL CONTROL: without the race flag the same pass rams, stopped {plain.Closest:0.00} m short: health {plain.Health}, crashed={plain.Crashed}");
            ctx.Check(race.Unarmed && race.Rounds == 0,
                $"a race seat holding both triggers is unarmed, no fire control and no weapon gauge, and puts {race.Rounds} round(s) in the pool");
            ctx.Check(!plain.Unarmed && plain.Rounds > 0,
                $"ABLE-TO-FAIL CONTROL: the plain seat holding the same triggers fires {plain.Rounds} round(s)");
            ctx.Check(!race.Offered,
                $"in the race the other pilot is on no target cycle: enemy {race.EnemyCount}, ally {race.AllyCount}");
            ctx.Check(race.Labelled,
                $"…yet its pane's race labels carry it, as '{race.Label}'");
            ctx.Check(plain.Offered && !plain.Labelled,
                $"ABLE-TO-FAIL CONTROL: without the flag the other pilot is on the Enemy cycle ({plain.EnemyCount}) and takes no race label");
            ctx.Check(race.Stamped > 0 && race.GhostShaders > 0 && race.GhostCompiles,
                $"the race build stamps {race.Stamped} instance(s) of the other airframe as seat 2's and compiles the ghost into {race.GhostShaders} shader(s)");
            ctx.Check(plain.Stamped == 0 && plain.GhostShaders == 0,
                $"the plain build stamps {plain.Stamped} instance(s) and carries the ghost in {plain.GhostShaders} shader(s), so its shader text is unchanged");
        }
        finally
        {
            textures.Dispose();
        }

        ctx.Note($"a race seat neither rams, fires nor targets another, and only the race ghosts it");
    }

    [Suite("race-ghost-fade",
        "the race ghost's law at 30, 60 and 100 m from the drawing camera: the full ghost inside "
        + "40 m, solid beyond 80 m, halfway between at 60 m; and the shader line carries the same "
        + "three constants the law reads, so what the suite measures is what the shader draws")]
    internal static void RaceGhostFade(TestContext ctx)
    {
        float near = RaceGhost.Alpha(30f);
        float mid = RaceGhost.Alpha(60f);
        float far = RaceGhost.Alpha(100f);
        float halfway = (RaceGhost.GhostAlpha + 1f) * 0.5f;
        ctx.Check(Mathf.IsEqualApprox(near, RaceGhost.GhostAlpha),
            $"at 30 m the aircraft is the full ghost: alpha {near:0.000} of {RaceGhost.GhostAlpha:0.000}");
        ctx.Check(Mathf.Abs(mid - halfway) < 1e-4f && mid > near && mid < far,
            $"at 60 m it is halfway through the fade: alpha {mid:0.000}, expected {halfway:0.000}");
        ctx.Check(Mathf.IsEqualApprox(far, 1f),
            $"at 100 m it is solid: alpha {far:0.000}");
        ctx.Check(RaceGhost.Alpha(RaceGhost.GhostWithinM) <= near + 1e-4f
                && Mathf.IsEqualApprox(RaceGhost.Alpha(RaceGhost.SolidBeyondM), 1f),
            $"the ramp starts at {RaceGhost.GhostWithinM:0} m and ends at {RaceGhost.SolidBeyondM:0} m");
        string line = RaceGhost.VertexLineFor(enhanced: false);
        ctx.Check(line.Contains("40.0, 80.0, 0.35,", System.StringComparison.Ordinal),
            $"the shader line passes the law's constants: {line.Trim()}");
        float nearEnhanced = RaceGhost.Alpha(30f, enhanced: true);
        string enhancedLine = RaceGhost.VertexLineFor(enhanced: true);
        ctx.Check(Mathf.IsEqualApprox(nearEnhanced, RaceGhost.EnhancedGhostAlpha)
                && nearEnhanced > near && Mathf.IsEqualApprox(RaceGhost.Alpha(100f, enhanced: true), 1f)
                && enhancedLine.Contains("40.0, 80.0, 0.55,", System.StringComparison.Ordinal),
            $"Enhanced keeps more of the ghost: alpha {nearEnhanced:0.000} at 30 m, {enhancedLine.Trim()}");
        ctx.Note($"the ghost is {RaceGhost.GhostAlpha:0.00} within {RaceGhost.GhostWithinM:0} m and solid beyond {RaceGhost.SolidBeyondM:0} m");
    }

    // One pair, built and flown. Seat 0's target pool and seat 1's airframe are read first. Then
    // seat 0 flies nose on through the parked seat 1, and holds both triggers from a fresh start.
    // Each pair takes a pool of its own: a freed rig stays on the roster that registered it.
    private static PairResult FlyPair(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        bool racing)
    {
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rigs = new PlayerRig[2];
        for (int i = 0; i < rigs.Length; i++)
        {
            var camera = new Camera3D { CullMask = SplitScreen.PlayerCullMask(i) };
            pane.AddChild(camera);
            rigs[i] = new PlayerRig { Index = i, Camera = camera, HudParent = pane, Viewport = pane };
        }

        var spec = SessionSpec.Parse(new[] { "--hold=0,0,0,1" });
        var roster = new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host,
            SuiteConstants.AircraftResources(ctx, planesGamez, textures,
                Messages.Load(ctx.MessagesPath), _ => new CamParams()),
            new FlightWorldBindings
            {
                Projectiles = pool,
                Gamez = planesGamez,
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"),
                Racing = racing,
            },
            new HumanRosterBindings { RigCount = rigs.Length, Rigs = rigs, PauseState = new PauseState() },
            new HeadOnStarts());
        try
        {
            roster.BuildPlayers(rigs);
            var flown = rigs[0].Controller!;
            var parked = rigs[1].Controller!;
            parked.Held = true;
            // The targets first: a ram can bring the parked seat down, and a dead aircraft leaves
            // every cycle for a reason of its own.
            var result = new PairResult();
            Targets(flown, parked, pool, ref result);
            Ghost(parked, ref result);
            Ram(flown, parked, ref result);
            Fire(flown, pool, ref result);
            return result;
        }
        finally
        {
            foreach (var rig in rigs)
            {
                rig.Controller?.Free();
            }

            pane.Free();
            pool.Free();
        }
    }

    // Seat 0 flies at the parked seat until it is past it; the closest pass and both airframes'
    // state are kept. A ram shows as health off either ledger or a crash on either side.
    private static void Ram(FlightController flown, FlightController parked, ref PairResult r)
    {
        var target = parked.WorldPosition;
        r.Closest = float.MaxValue;
        for (int i = 0; i < RamFrames; i++)
        {
            flown.SimStep(Dt);
            parked.SimStep(Dt);
            float d = flown.WorldPosition.DistanceTo(target);
            if (d < r.Closest)
            {
                r.Closest = d;
                r.ClosestOffset = flown.WorldPosition - target;
            }
            if (flown.Crashed || flown.WorldPosition.Z < target.Z - 30f)
            {
                break;
            }
        }

        float lost = Lost(flown) + Lost(parked);
        r.Crashed = flown.Crashed || parked.Crashed;
        r.Rammed = r.Crashed || lost > 0f;
        r.Health = $"{Lost(flown):0.#}+{Lost(parked):0.#} lost";
        r.PassedThrough = r.Closest < ThroughM;
        r.BodiesOff = flown.Body?.CollisionLayer == 0 && parked.Body?.CollisionLayer == 0;
    }

    private static float Lost(FlightController rig) =>
        rig.Damage is { } d ? d.WholeHealthMax - d.WholeHealth : 0f;

    // Both triggers held through the scripted switches for a second, from a fresh airframe.
    private static void Fire(FlightController flown, ProjectilePool pool, ref PairResult r)
    {
        flown.Rerun();
        flown.AutoFire = true;
        flown.AutoFireRockets = true;
        var rounds = new List<(Vector3 Pos, Vector3 Velocity)>();
        int most = 0;
        for (int i = 0; i < 60; i++)
        {
            flown.SimStep(Dt);
            rounds.Clear();
            pool.CollectLiveRounds(rounds);
            most = Mathf.Max(most, rounds.Count);
        }

        flown.AutoFire = false;
        flown.AutoFireRockets = false;
        r.Rounds = most;
        r.Unarmed = flown.Loadout == null && flown.PilotHud.Gauges is not { GunGauge: not null }
            && flown.PilotHud.Gauges is not { MissileGauge: not null };
    }

    // Seat 0's own target pool over the session's aircraft roster, and its HUD's race labels.
    private static void Targets(FlightController flown, FlightController parked, ProjectilePool pool,
        ref PairResult r)
    {
        var scan = new AimCandidateSet();
        pool.CollectAircraft(scan);
        var sel = flown.Targeting!;
        sel.Rebuild(scan, null, flown.Team, flown, flown.WorldPosition, flown.GlobalBasis);
        sel.NextEnemy();
        r.EnemyCount = sel.Pool.Enemy.Count;
        r.AllyCount = sel.Pool.Ally.Count;
        r.Offered = ReferenceEquals(sel.Current?.Source, parked);
        foreach (var cls in new[] { TargetClass.Enemy, TargetClass.Ally, TargetClass.NonAircraft })
        {
            foreach (var t in sel.Pool.Of(cls))
            {
                r.Offered |= ReferenceEquals(t.Source, parked);
            }
        }

        var pilots = new List<FlightController>();
        TargetHud.CollectRacePilots(flown, scan, pilots);
        bool marks = flown.PilotHud.TargetHud is { RaceMarks: true };
        r.Labelled = marks && pilots.Contains(parked);
        r.Label = TargetPool.AircraftDisplayName(parked, flown.Team) ?? "";
    }

    // The parked airframe's instances: how many carry seat 2's ghost stamp, and how many surfaces
    // were built with the ghost. A shader that parses lists its uniforms; a broken one lists none.
    private static void Ghost(FlightController parked, ref PairResult r)
    {
        var armed = new Vector4(SplitScreen.FirstPersonLayer(1), 0f, 0f, 1f);
        var pending = new Stack<Node>();
        pending.Push(parked.PlaneModel!);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            foreach (var child in node.GetChildren())
            {
                pending.Push(child);
            }

            if (node is not MeshInstance3D mi || mi.Mesh == null)
            {
                continue;
            }

            if (mi.GetInstanceShaderParameter(RaceGhost.Param).VariantType == Variant.Type.Vector4
                && mi.GetInstanceShaderParameter(RaceGhost.Param).AsVector4() == armed)
            {
                r.Stamped++;
            }

            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                if (mi.GetActiveMaterial(s) is ShaderMaterial { Shader: { } shader }
                    && shader.Code.Contains(RaceGhost.VertexLine.Trim(), System.StringComparison.Ordinal))
                {
                    r.GhostShaders++;
                    r.GhostCompiles |= shader.GetShaderUniformList().Count > 0;
                }
            }
        }
    }

    private struct PairResult
    {
        public float Closest;
        public Vector3 ClosestOffset;
        public bool PassedThrough;
        public bool Rammed;
        public bool Crashed;
        public bool BodiesOff;
        public string Health;
        public int Rounds;
        public bool Unarmed;
        public bool Offered;
        public bool Labelled;
        public string Label;
        public int EnemyCount;
        public int AllyCount;
        public int Stamped;
        public int GhostShaders;
        public bool GhostCompiles;
    }

    // Seat 0 nose on at seat 1 across the gap, both level at the same height and speed.
    private sealed class HeadOnStarts : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var flown = new Vector3(0f, AltitudeM + SinkAllowanceM, 0f);
            var parked = new Vector3(0f, AltitudeM, -GapM);
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                starts[i] = i == 0
                    ? new FlightStart(flown, flown + Vector3.Forward, 1f, SpeedMps)
                    : new FlightStart(parked, parked + Vector3.Back, 1f, SpeedMps);
            }

            return starts;
        }
    }
}

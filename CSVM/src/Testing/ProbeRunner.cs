using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The `--dump-*`/`--run-tests`/`--*-test`/`--destroy=` probe wrappers the Launcher and
/// GameSession quit into: each reads a <see cref="SessionSpec"/> (passed per call, since a
/// menu launch can replace the caller's spec between calls) plus the base paths settled once in
/// `_Ready`, produces a report to stdout and `./.scratch/`, and hands back a verdict for the
/// caller's exit code. No reference back to the host node — the two probes that need one
/// (<see cref="RunTestSuites"/>, for its throwaway world host; <see cref="RunEffectsTest"/>, for
/// the effect-play point) take exactly the camera/node they need as parameters.</summary>
public sealed class ProbeRunner
{
    private readonly string _repoRoot;
    private readonly string _dataRoot;
    private readonly string _zrdrPath;
    private readonly string _soundsPath;
    private readonly string _interpPath;
    private readonly string _messagesPath;
    private readonly string _planesGamezPath;

    public ProbeRunner(string repoRoot, string dataRoot, string zrdrPath, string soundsPath,
        string interpPath, string messagesPath, string planesGamezPath)
    {
        _repoRoot = repoRoot;
        _dataRoot = dataRoot;
        _zrdrPath = zrdrPath;
        _soundsPath = soundsPath;
        _interpPath = interpPath;
        _messagesPath = messagesPath;
        _planesGamezPath = planesGamezPath;
    }

    /// <summary>Applies the <c>--rocket=&lt;wep_id&gt;</c> testing override: replaces every hardpoint's
    /// ordnance with the named weapon, resetting each pylon's capacity/ammo to that weapon's
    /// <c>CLUSTER_SIZE</c>. A no-op (with a warning) if the id is unknown. Must run before the pylon
    /// models are mounted and the controller's ordnance-type list is built. All 11 stock loadouts
    /// carry HE (wep_06), so this is the only way to exercise a different pylon model.</summary>
    public static void ApplyRocketOverride(Loadout loadout, WeaponDefs weapons, string wepId, bool verbose)
    {
        if (weapons.Get(wepId) is not { } weapon)
        {
            GD.PushWarning($"--rocket='{wepId}' is not a known weapon id — hardpoints keep their stock ordnance");
            return;
        }
        int per = weapon.ClusterSize ?? 0;
        foreach (var hp in loadout.Hardpoints)
        {
            hp.Weapon = weapon;
            hp.Capacity = per;
            hp.Ammo = per;
        }
        if (verbose)
        {
            GD.Print($"--rocket: hardpoints -> {weapon.Id} ({weapon.Name}), " +
                     $"flyout model '{weapon.Flyout?.Model ?? "-"}', {per}/pylon");
        }
    }

    /// <summary>--destroy=&lt;name&gt; (F42): kill every destructible whose def name, animation name or
    /// anchor <c>cs_name</c> contains <paramref name="name"/> (case-insensitive), so a --screenshot
    /// captures the destruction with nobody at the controls. Reuses the weapon-hit path exactly
    /// (<see cref="Mech3.AnimRuntime.DamageAt"/> — the healthy→destroyed swap, debris and effects the
    /// same as a rocket kill); it just spends more than the object's HP. Resolves each match to its
    /// authoritative instance and dedupes by anchor, so a wildcard def that binds one physical object
    /// through several pools is killed once. Returns how many distinct objects were destroyed.</summary>
    public static int TriggerDestroy(Mech3.AnimRuntime runtime, string name, out Aabb bounds)
    {
        bounds = default;
        static string AnchorName(Node3D n) =>
            n.HasMeta(Mech3.AnimRuntime.NameMeta) ? n.GetMeta(Mech3.AnimRuntime.NameMeta).AsString()
                                                  : n.Name.ToString();

        // Distinct physical objects to kill, keyed by authoritative anchor so a def bound through
        // both its reader wildcard and its compiled twin counts once.
        var targets = new Dictionary<ulong, Mech3.DestructibleRegistry.Instance>();
        foreach (var inst in runtime.Destructibles.All)
        {
            bool match =
                inst.Def.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                || (inst.Def.AnimName?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false)
                || AnchorName(inst.Anchor).Contains(name, StringComparison.OrdinalIgnoreCase);
            if (!match)
            {
                continue;
            }
            var target = runtime.Destructibles.Resolve(inst.Anchor) ?? inst;
            targets[target.Anchor.GetInstanceId()] = target;
        }

        if (targets.Count == 0)
        {
            // No match: list a sample of what IS destructible here so the tester can correct the name
            // without a separate --damage-test run (that report is still the full list).
            var sample = new List<string>();
            var seen = new HashSet<string>();
            foreach (var inst in runtime.Destructibles.All)
            {
                if (seen.Add(inst.Def.Name))
                {
                    sample.Add(inst.Def.Name);
                }
                if (sample.Count >= 20)
                {
                    break;
                }
            }
            GD.Print($"--destroy='{name}': no destructible matched. "
                     + $"{runtime.Destructibles.DistinctAnchors} object(s) present; some def names: "
                     + string.Join(", ", sample) + " (--damage-test lists them all)");
            return 0;
        }

        // A cap so naming a common wildcard (many towers/panels) can't start hundreds of death
        // sequences in one frame; the framed object is what the screenshot needs. Loud when it bites.
        const int cap = 64;
        int killed = 0;
        bool haveBounds = false;
        foreach (var target in targets.Values)
        {
            if (killed >= cap)
            {
                GD.Print($"--destroy='{name}': capped at {cap} of {targets.Count} matches "
                         + "(name a more specific def/node to kill fewer)");
                break;
            }
            if (target.Status == Mech3.DestructibleRegistry.State.Destroyed)
            {
                continue;
            }
            // The first killed object's world-space bounds, captured before the kill: the caller
            // auto-frames the freecam on it (the anchor's own origin is often far from its geometry).
            // MeshInstance-based, so it merges the healthy + destroyed variants either way.
            if (!haveBounds)
            {
                bounds = UI.OrbitCamera.MergedAabb(target.Anchor);
                haveBounds = true;
            }
            // Spend more than the whole health pool so a single call kills it outright (DamageAt runs
            // the death sequence at zero). Feeding the anchor node is exactly how --damage-hd drives it.
            runtime.DamageAt(target.Anchor, target.MaxHealth + 1f);
            killed++;
        }
        var c = bounds.GetCenter();
        string at = haveBounds ? $" near ({c.X:0}, {c.Y:0}, {c.Z:0})" : "";
        GD.Print($"--destroy='{name}': destroyed {killed} object(s){at} "
                 + $"({string.Join(", ", targets.Values.Take(killed).Select(t => t.Def.Name).Distinct())})");
        return killed;
    }

    /// <summary>--dump-markers[=plane]: print each player airframe's firepoint / pylon / target
    /// rig — name, plane-frame position, gun-pair grouping and shared mounts — to stdout and
    /// <c>./.scratch/markers_dump.txt</c>, then quit (see <see cref="Mech3.MarkerRig"/>). This is
    /// the committed instrument the <c>docs/formats/markers.md</c> tables regenerate from, so the
    /// user can see and name every mount when handing back the airframe gun-group table (item A3).
    /// An optional value filters to one plane by model node (<c>player_bhawk</c>) or display name
    /// (<c>Bloodhawk</c>), matched case-insensitively as a substring.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    public bool DumpMarkers(SessionSpec spec)
    {
        // Windowed launches shouldn't steal focus for a report that renders nothing and quits.
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        var r = Probes.Markers(_planesGamezPath, spec.DumpMarkersPlane);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-markers: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        // ./.scratch/ inside the workspace, per PROJECT_CONTEXT.md — never the OS temp dir.
        WriteScratch("markers_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/markers_dump.txt");
        return true;
    }

    /// <summary>Writes one report into the workspace scratch folder, by absolute path. Relative
    /// paths resolve against the process working directory, not the repo, so a run launched from
    /// anywhere else would silently scatter its artifacts.</summary>
    public void WriteScratch(string fileName, string text)
    {
        var scratch = Path.Combine(_repoRoot, ".scratch");
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, fileName), text);
    }

    /// <summary>--run-tests[=filter]: boot the engine, run the registered assertion suites, print
    /// the PASS/FAIL/SKIP table plus <c>./.scratch/test-report.json</c>, and quit with a nonzero
    /// exit code if any suite failed (see <see cref="TestHarness"/>). <paramref name="parent"/>
    /// hosts the throwaway world node a suite may build; the caller adopts the fixed-step clock
    /// this creates via <paramref name="clock"/> and quits the tree with the returned code.</summary>
    public int RunTestSuites(SessionSpec spec, Node parent, Camera3D camera, out GameClock clock)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        // A suite that ticks the sim must see the same clock a session gives it. Fixed-step, since
        // a test run is deterministic by nature: sim state is a function of the step count.
        clock = new GameClock { Mode = GameClock.RunMode.FixedStep };
        GameClock.Current = clock;
        var host = new Node3D { Name = "TestHost" };
        parent.AddChild(host);
        var ctx = new TestContext
        {
            RepoRoot = _repoRoot,
            DataRoot = _dataRoot,
            Chapter = spec.Chapter,
            Mission = spec.Mission,
            ZrdrPath = _zrdrPath,
            MessagesPath = _messagesPath,
            PlanesGamezPath = _planesGamezPath,
            InterpPath = _interpPath,
            SoundsPath = _soundsPath,
            PlaneName = spec.PlaneName,
            Mute = spec.Mute,
            LoadoutOverride = spec.LoadoutOverride,
            Host = host,
            Camera = camera,
        };
        if (DisplayServer.GetName() == "headless")
        {
            // LOG-8's sibling: the dummy renderer compiles no shaders, so a shader error cannot
            // occur — and therefore cannot be screened. Say so rather than letting the clean error
            // census read as proof.
            Log.Warn("test", $"headless display — no shaders compiled, so the error screen cannot see a shader error");
        }
        int code = TestHarness.Run(ctx, spec.RunTestsFilter);
        host.Free();
        GameClock.Current = null;
        return code;
    }

    /// <summary>--dump-weapons[=id|name]: load the typed <see cref="Flight.WeaponDefs"/> reader
    /// (B11) over <c>weapons.json</c>, print one line per def (id, name, key ballistics, flags,
    /// bindings) to stdout and <c>./.scratch/weapons_dump.txt</c>, and report any unmapped keys,
    /// then quit. The committed verification instrument the weapons.md table is checked against —
    /// a clean run (no UNHANDLED lines) is the B11 pass. An optional value filters by id
    /// (<c>wep_06</c>) or <c>NAME</c> substring, matched case-insensitively.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    public bool DumpWeapons(SessionSpec spec)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        var r = Probes.Weapons(_zrdrPath, _messagesPath, spec.DumpWeaponsFilter);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-weapons: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        WriteScratch("weapons_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/weapons_dump.txt");
        return true;
    }

    /// <summary>--dump-flight[=plane]: step a throwaway <see cref="FlightModel"/> through the
    /// manoeuvres the original was recorded flying and print its numbers beside the video-decoded
    /// ones (<c>analysis/video-flight-calibration/</c>), to stdout and
    /// <c>./.scratch/flight_dump.txt</c>, then quit. The instrument behind the
    /// <c>flight-envelope</c> suite, and the only way to see what a flight-constant change did to
    /// the whole envelope rather than to the one number that was edited.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    public bool DumpFlight(SessionSpec spec)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        string plane = spec.DumpFlightPlane.Length > 0 ? spec.DumpFlightPlane : spec.PlaneName;
        var r = Probes.FlightEnvelope(_zrdrPath, plane);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-flight: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        WriteScratch("flight_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/flight_dump.txt");
        return true;
    }

    /// <summary>--dump-loadout[=plane]: for each plane in <c>stock_loadouts.json</c>, build its
    /// model and bind the stock loadout (<see cref="Flight.Loadout"/>, B12), reporting the resolved
    /// gun groups (mount, weapon, per-group ammo, muzzle nodes) and hardpoints — or the loud error
    /// if a marker doesn't resolve. Writes to stdout and <c>./.scratch/loadout_dump.txt</c>, then
    /// quits. <c>--loadout=&lt;def&gt;</c> binds that def's loadout instead of each plane's own (a
    /// cross-binding test — e.g. binding a def that wants <c>firepoint8</c> to the Kestrel proves
    /// the missing-marker error fires). An optional value filters by def / model / display.
    /// Combined with <c>--weapon-lab</c> (B4), binds each plane's <see cref="Flight.Loadout.ForRig"/>
    /// full-rig loadout instead of the stock one, so the report lists mounts the stock file never
    /// names.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    public bool DumpLoadout(SessionSpec spec)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        var r = Probes.Loadouts(_zrdrPath, _messagesPath, _planesGamezPath, _dataRoot,
            spec.DumpLoadoutFilter, spec.LoadoutOverride, forRig: spec.WeaponLab);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-loadout: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        WriteScratch("loadout_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/loadout_dump.txt");
        return true;
    }

    /// <summary>--dump-mips[=name]: for every base texture in the chapter's archive that ships an
    /// authored <c>_1</c>/<c>_2</c> level, report the mip chain the texture archive built — level
    /// size, mean luminance and the share of pixels above 128 — beside the authored artwork, and
    /// say whether the installed level IS that artwork. Writes to stdout and
    /// <c>./.scratch/mips_dump.txt</c>, then quits. Run it once per <c>--mips=</c> policy: the
    /// generated chain averages the bright pixels away, the authored one keeps them.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    public bool DumpMips(SessionSpec spec)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        var r = Probes.MipChains(SessionPaths.ChapterTextures(_dataRoot, spec.Chapter),
            spec.Chapter, spec.DumpMipsFilter);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-mips: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        WriteScratch("mips_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/mips_dump.txt");
        return r.Ok;
    }

    /// <summary>The D32 headless verify: play every impact/destruction effect through the
    /// world-effects runtime at the camera point and report whether each RESOLVES (its def is bound)
    /// and whether it BUILDS a puffer (WORLD-12 — a started def whose factory/textures are missing
    /// renders nothing). Each effect is stopped before the next so effects sharing a template root
    /// (the gun family shares <c>gunhit</c>) get an independent count. Reports to stdout and
    /// <c>./.scratch/effects_test.txt</c>. <paramref name="effectAnimNames"/> is the caller's
    /// static name table (<c>WorldEffectsFactory.EffectAnimNames</c>).
    ///
    /// <para>With <paramref name="stage"/> (the world-effects template stage) it also reports the
    /// MESH half (`BL-061`): how many of the stage's mesh instances become visible while the effect
    /// plays, on which template root, and how far from the play point. A puffer count cannot see
    /// this — the two halves fail independently, and an effect whose meshes never show, or show at
    /// the STAGE origin, reads as a full pass on puffers alone. The whole stage is counted because
    /// exactly one effect plays at a time (each is <c>StopAll</c>ed before the next), so every
    /// visible mesh in the window belongs to it.
    ///
    /// <para>⚠ Sampled EVERY tick and reported as the peak, never as one final reading: the data
    /// turns its own meshes off inside the window (<c>large_fireball</c> deactivates
    /// <c>flame_ball_01</c> 0.3 s in, well before the 0.5 s the puffer count needs), so a
    /// single sample at the end reports a working effect as a blank one. The residual line after
    /// the stop is the opposite question — what is still lit once the effect is over.</para></summary>
    public void RunEffectsTest(SessionSpec spec, Camera3D camera, Mech3.AnimRuntime effects,
        string[] effectAnimNames, Node3D? stage = null)
    {
        // Play each effect at the player point so range-gated ones (gunhit's PLAYER_RANGE) pass.
        var p = camera.GlobalPosition;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"effects-test: chapter {spec.Chapter}, {effectAnimNames.Length} effect name(s), "
                      + $"point ({p.X:0},{p.Y:0},{p.Z:0})");
        if (stage != null)
        {
            // The stage's BASE state, read off each mesh's own visibility flag rather than
            // visible-in-tree (every root is hidden here by construction, so in-tree would read
            // zero for all of them and say nothing). This is what a revealed root would show if its
            // def touched nothing: the gamez base state the original's template copy carries.
            sb.AppendLine($"  {"(stage at rest)",-22} {BaseStateOfStage(stage)}");
        }
        int resolved = 0, puffered = 0, meshed = 0;
        var leaked = new SortedSet<string>(StringComparer.Ordinal);
        var revealedDark = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in effectAnimNames)
        {
            int before = effects.PuffersBuilt;
            var peak = new Dictionary<string, MeshPeak>(StringComparer.Ordinal);
            bool matched = effects.PlayEffectAt(name, p);
            // Fixed ticks so the t=0 PUFFER_STATE emits and any one-CallSequence-deep puffer
            // (large_black_smokeball's p1trail) reaches its first batch.
            for (int f = 0; f < 30; f++)
            {
                effects.Advance(1f / 60f);
                if (stage != null)
                    SampleMeshes(stage, p, peak);
            }

            int built = effects.PuffersBuilt - before;
            int litMeshes = Lit(peak);
            if (litMeshes > 0)
                meshed++;
            string half = built > 0 ? $"puffer[{built}]" : "no puffer";
            string meshHalf = stage == null ? "" : $" mesh[{litMeshes}] {Describe(peak)}";
            if (!matched)
                sb.AppendLine($"  {name,-22} UNRESOLVED — no def bound");
            else if (built > 0 || litMeshes > 0)
            {
                if (built > 0)
                    puffered++;
                sb.AppendLine($"  {name,-22} {half}{meshHalf} rendered");
            }
            else
                sb.AppendLine($"  {name,-22} started, built no puffer{meshHalf} (light/container effect)");
            if (matched)
                resolved++;
            // Full reset before the next name: these effects share puffer names (trailpuffer2) and
            // template roots, so a lingering instance would let the next effect read as "no puffer".
            effects.StopAll();
            if (stage == null)
                continue;
            // One tick past the stop, so a deferred hide lands: what is STILL lit now belongs to an
            // effect that is over — a template left burning at a hit site for the rest of the
            // session, which is the mesh half's other failure mode.
            effects.Advance(1f / 60f);
            var after = new Dictionary<string, MeshPeak>(StringComparer.Ordinal);
            SampleMeshes(stage, p, after);
            foreach (var (root, row) in after)
            {
                if (row.Visible > 0)
                    leaked.Add($"{root} ({row.Visible} mesh, after {name})");
                else if (row.Revealed && row.Total > 0)
                    revealedDark.Add($"{root} (after {name})");
            }
        }

        sb.AppendLine($"effects-test: {resolved}/{effectAnimNames.Length} resolved, "
                      + $"{puffered} built a puffer, {resolved - puffered} started but built none"
                      + (stage == null ? "" : $"; {meshed} showed template mesh(es)"));
        if (stage != null)
        {
            sb.AppendLine(leaked.Count == 0
                ? "  no template mesh left lit after its effect was stopped"
                : $"  ⚠ still lit after the stop: {string.Join(", ", leaked)}");
            // A root left REVEALED with every mesh under it off draws nothing — the data's own
            // OBJECT_ACTIVE_STATEs turned its pieces off — so this is a note, not a defect. It is
            // printed because "nothing shows" and "nothing is left revealed" are separate claims.
            if (revealedDark.Count > 0)
                sb.AppendLine($"  (revealed but dark afterwards: {string.Join(", ", revealedDark)})");
        }

        if (effects.UnhandledEventCounts.Count > 0)
        {
            sb.AppendLine("  reasons a start built no puffer: "
                          + string.Join(", ", effects.UnhandledEventCounts
                              .OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
        }
        GD.Print(sb.ToString());
        WriteScratch("effects_test.txt", sb.ToString());
    }


    /// <summary>--damage-test[=name] / --damage-hd=N: sweep one live destructible instance per
    /// distinct def through its damage stages (or through discrete weapon hits) and report what
    /// each check found — see <see cref="Probes.Damage"/>, which the <c>damage-stages</c> /
    /// <c>damage-hd</c> suites assert on. Reports to stdout, <c>./.scratch/damage_test.txt</c> and
    /// <c>./.scratch/world_colliders.txt</c>.</summary>
    public void RunDamageTest(SessionSpec spec, Mech3.AnimRuntime runtime)
    {
        var r = Probes.Damage(runtime, spec.Chapter, spec.DamageTestFilter, spec.DamageHd);
        GD.Print(r.Text);
        WriteScratch("damage_test.txt", r.Text);
        if (r.CollidableMeshes > 0)
        {
            WriteScratch("world_colliders.txt", r.CollidersText);
            GD.Print($"damage-test: {r.CollidableMeshes} collidable meshes → ./.scratch/world_colliders.txt");
        }
        GD.Print($"{r.Summary} → ./.scratch/damage_test.txt");
    }

    private static int Lit(Dictionary<string, MeshPeak> rows) => rows.Values.Sum(r => r.Visible);

    /// <summary>Each mesh-bearing template root's base state: how many of its meshes carry their own
    /// visibility flag, out of how many it has. Read once, before anything plays.</summary>
    private static string BaseStateOfStage(Node3D stage)
    {
        var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pool in stage.GetChildren())
        {
            foreach (var child in pool.GetChildren())
            {
                if (child is not Node3D root)
                    continue;
                int self = 0, total = 0;
                CountSelfVisibleMeshes(root, true, ref self, ref total);
                if (total > 0)
                    rows[root.Name] = $"{root.Name} {self}/{total} self-visible";
            }
        }

        return rows.Count == 0 ? "no mesh-bearing template root" : string.Join("; ", rows.Values);
    }

    /// <summary>Counts meshes that would draw if the template ROOT were revealed — the root's own
    /// flag is skipped and every flag below it honoured, since the root's is the engine's to set
    /// (<c>ShowPlacedTemplates</c>) and everything under it is the data's.</summary>
    private static void CountSelfVisibleMeshes(Node node, bool shown, ref int selfVisible,
        ref int total)
    {
        if (node is MeshInstance3D { Mesh: not null } mi && mi.Mesh.GetSurfaceCount() > 0)
        {
            total++;
            if (shown)
                selfVisible++;
        }

        foreach (var child in node.GetChildren())
        {
            // A hidden branch still contributes its TOTAL — "0 of 8" and "0 of 0" are different
            // answers — so the walk continues rather than stopping at the first hidden node.
            CountSelfVisibleMeshes(child, shown && (child is not Node3D c || c.Visible),
                ref selfVisible, ref total);
        }
    }

    /// <summary>The per-root reading, printed for every root a run TOUCHED (revealed, or lit a mesh)
    /// that has meshes at all. A revealed root showing none of its own meshes and a root nobody
    /// revealed are different failures and read differently here; a root with no geometry (most of
    /// the puffer hosts) is neither, and is left out.</summary>
    private static string Describe(Dictionary<string, MeshPeak> rows)
    {
        var text = rows.Where(r => r.Value.Total > 0 && (r.Value.Revealed || r.Value.Visible > 0))
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => $"{r.Key} {r.Value.Visible}/{r.Value.Total} @{r.Value.Distance:0.0} m");
        return string.Join("; ", text);
    }

    /// <summary>Folds one frame's stage state into <paramref name="peak"/>, keeping each root's
    /// best reading. Distances are to the play point, so "renders at the call site" and "renders at
    /// the stage origin" (kilometres away in a chapter world) are different readings rather than the
    /// same count. Roots are named, so the line says WHICH template showed — a pooled stage holds
    /// several copies of one name, and they are folded together on purpose: which SLOT a call took
    /// is the pool's business (`BL-225`), not this census's.</summary>
    private static void SampleMeshes(Node3D stage, Vector3 point, Dictionary<string, MeshPeak> peak)
    {
        foreach (var pool in stage.GetChildren())
        {
            foreach (var child in pool.GetChildren())
            {
                if (child is not Node3D root)
                    continue;
                int vis = 0, total = 0;
                CountMeshes(root, ref vis, ref total);
                var was = peak.TryGetValue(root.Name, out var prev) ? prev : default;
                if (vis >= was.Visible)
                {
                    peak[root.Name] = new MeshPeak(vis, Math.Max(total, was.Total),
                        root.GlobalPosition.DistanceTo(point), was.Revealed || root.Visible);
                }
                else if (root.Visible && !was.Revealed)
                {
                    peak[root.Name] = was with { Revealed = true };
                }
            }
        }
    }

    private static void CountMeshes(Node node, ref int visible, ref int total)
    {
        if (node is MeshInstance3D { Mesh: not null } mi && mi.Mesh.GetSurfaceCount() > 0)
        {
            total++;
            if (mi.IsVisibleInTree())
                visible++;
        }

        foreach (var child in node.GetChildren())
        {
            CountMeshes(child, ref visible, ref total);
        }
    }

    /// <summary>The high-water mark of one template root's mesh half: how many of its meshes were
    /// visible-in-tree at once, out of how many it carries, and how far the root sat from the play
    /// point when it peaked. Visible-IN-TREE, not <c>Visible</c>: a mesh whose own flag is set under
    /// a hidden template root draws nothing, and that difference IS the bug this census exists to
    /// catch.</summary>
    private readonly record struct MeshPeak(int Visible, int Total, float Distance, bool Revealed);
}

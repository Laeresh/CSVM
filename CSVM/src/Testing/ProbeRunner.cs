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
    /// user can see and name every mount when handing back the airframe gun-group table.
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
            // The dummy renderer compiles no shaders, so a shader error cannot occur — and
            // therefore cannot be screened. Say so rather than letting the clean error census read
            // as proof.
            Log.Warn("test", $"headless display — no shaders compiled, so the error screen cannot see a shader error");
        }
        int code = TestHarness.Run(ctx, spec.RunTestsFilter);
        host.Free();
        GameClock.Current = null;
        return code;
    }

    /// <summary>--dump-weapons[=id|name]: load the typed <see cref="Flight.WeaponDefs"/> reader
    /// over <c>weapons.json</c>, print one line per def (id, name, key ballistics, flags,
    /// bindings) to stdout and <c>./.scratch/weapons_dump.txt</c>, and report any unmapped keys,
    /// then quit. The committed verification instrument the weapons.md table is checked against —
    /// a clean run (no UNHANDLED lines) is the pass. An optional value filters by id
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
    /// model and bind the stock loadout (<see cref="Flight.Loadout"/>), reporting the resolved
    /// gun groups (mount, weapon, per-group ammo, muzzle nodes) and hardpoints — or the loud error
    /// if a marker doesn't resolve. Writes to stdout and <c>./.scratch/loadout_dump.txt</c>, then
    /// quits. <c>--loadout=&lt;def&gt;</c> binds that def's loadout instead of each plane's own (a
    /// cross-binding test — e.g. binding a def that wants <c>firepoint8</c> to the Kestrel proves
    /// the missing-marker error fires). An optional value filters by def / model / display.
    /// Combined with <c>--weapon-lab</c>, binds each plane's <see cref="Flight.Loadout.ForRig"/>
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

    /// <summary>--effects-test: the world-effects headless verify — see <see cref="Probes.Effects"/>, whose
    /// census the <c>effect-template-mesh</c> suite counts through.
    /// Plays every effect at the camera point so range-gated ones (gunhit's PLAYER_RANGE) pass;
    /// <paramref name="effectAnimNames"/> is the caller's static name table
    /// (<c>EffectCatalogue.WorldEffectAnimNames</c>: the fixed table plus the playable
    /// <c>touchdown_*</c> vector slots), <paramref name="stage"/> its template stage
    /// for the MESH half. Reports to stdout and <c>./.scratch/effects_test.txt</c>.</summary>
    public void RunEffectsTest(SessionSpec spec, Camera3D camera, Mech3.AnimRuntime effects,
        IReadOnlyList<string> effectAnimNames, Node3D? stage = null)
    {
        var r = Probes.Effects(effects, effectAnimNames, camera.GlobalPosition, stage, spec.Chapter);
        GD.Print(r.Text);
        WriteScratch("effects_test.txt", r.Text);
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

}

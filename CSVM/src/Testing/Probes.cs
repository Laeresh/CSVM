using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The assertion cores behind the <c>--dump-*</c> / <c>--damage-test</c> inspection reports.
///
/// <para>Each probe does the work once and returns <b>both</b> halves: the human-readable report
/// text the <c>--dump-*</c> flag prints and writes, and a structured verdict (counts, per-row
/// booleans, failure strings) a <c>--run-tests</c> suite asserts on. That split is the point —
/// before it, the verdicts lived as <c>✓</c>/<c>✗</c> glyphs inside a formatted string, and the
/// only way to automate them would have been to parse the report back.</para>
///
/// <para>Every probe renders numbers with <see cref="CultureInfo.InvariantCulture"/>: a German
/// machine otherwise writes <c>HEALTH 0,01</c> into a committed verification artifact.</para>
/// </summary>
public static class Probes
{
    /// <summary>How many defs one sweep reports on. A wildcard NAME binds many identical towers,
    /// so one representative per def is enough; the cap keeps a destructible-heavy chapter a
    /// readable report. <b>It caps the swept rows, never the registry totals</b> — a census reads
    /// <see cref="DamageResult.TotalInstances"/>.</summary>
    public const int SweepCap = 16;

    private const float Mph = 0.44704f;         // m/s per mph
    private const float Ft = 0.3048f;           // m per foot
    private const float EnvDt = 1f / 60f;       // the sim step --det pins every session to

    // ---- markers -----------------------------------------------------------------------------

    public static MarkersResult Markers(string planesGamezPath, string filter)
    {
        var r = new MarkersResult();
        GameZ gamez;
        try
        {
            gamez = GameZ.Load(planesGamezPath);
        }
        catch (Exception e)
        {
            r.Error = $"could not load planes gamez ({planesGamezPath}): {e.Message}";
            r.Summary = $"markers dump: {r.Error}";
            return r;
        }

        var wanted = new List<(string Model, string Display)>();
        foreach (var plane in MarkerRig.PlayerAirframes)
        {
            if (filter.Length == 0
                || plane.Model.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || plane.Display.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                wanted.Add(plane);
            }
        }
        if (wanted.Count == 0)
        {
            var names = new List<string>();
            foreach (var p in MarkerRig.PlayerAirframes)
            {
                names.Add($"{p.Display} ({p.Model})");
            }
            r.Error = $"'{filter}' matched no airframe. Available: " + string.Join(", ", names);
            r.Summary = $"markers dump: {r.Error}";
            return r;
        }
        r.Requested = wanted.Count;

        var sb = new StringBuilder();
        sb.AppendLine($"# Aircraft marker rig — {planesGamezPath}");
        sb.AppendLine("# Positions are plane frame, metres (nose -Z, right +X, up +Y). See docs/formats/markers.md.");
        sb.AppendLine();
        foreach (var (model, display) in wanted)
        {
            var rig = MarkerRig.Extract(gamez, model);
            if (rig == null)
            {
                // Recorded, not swallowed: a missing airframe used to reduce the final count with
                // nothing naming it.
                r.Missing.Add(model);
                sb.AppendLine($"=== {display} ({model}) — root node not found ===").AppendLine();
                continue;
            }
            sb.Append(rig.Format(display)).AppendLine();
            r.Done++;
        }
        r.Text = sb.ToString();
        r.Summary = r.Missing.Count == 0
            ? $"markers dump: {r.Done}/{r.Requested} airframe(s)"
            : $"markers dump: {r.Done}/{r.Requested} airframe(s), MISSING {string.Join(", ", r.Missing)}";
        return r;
    }

    // ---- weapons -----------------------------------------------------------------------------

    public static WeaponsResult Weapons(string zrdrPath, string messagesPath, string filter)
    {
        // A committed verification artifact must read the same everywhere: a German machine
        // otherwise writes "6,25" where an invariant one writes "6.25".
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        var r = new WeaponsResult();
        WeaponDefs weapons;
        try
        {
            var messages = Messages.Load(messagesPath);
            weapons = WeaponDefs.Load(zrdrPath, messages);
        }
        catch (Exception e)
        {
            r.Error = $"could not load weapons.json ({zrdrPath}): {e.Message}";
            r.Summary = $"weapons dump: {r.Error}";
            return r;
        }
        r.Total = weapons.All.Count;
        r.EmptyClipSound = weapons.EmptyClipSound;

        var sb = new StringBuilder();
        sb.AppendLine($"# weapons.json — {zrdrPath}");
        sb.AppendLine($"# {weapons.All.Count} BALLISTICS entries; empty-clip sound = {weapons.EmptyClipSound}");
        sb.AppendLine("# See docs/formats/weapons.md.");
        sb.AppendLine();

        foreach (var w in weapons.All)
        {
            if (filter.Length > 0
                && !w.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !w.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            r.Shown++;
            var flags = new List<string>();
            if (w.IsCannon) { flags.Add("CANNON"); }
            if (w.IsRocket) { flags.Add("ROCKET"); }
            if (w.HighExplosive) { flags.Add("HE"); }
            if (w.Sonic) { flags.Add("SONIC"); }
            if (w.Flash) { flags.Add("FLASH"); }
            if (w.BeeperSeeker) { flags.Add("BEEPER_SEEKER"); }
            if (w.Rear) { flags.Add("REAR"); }
            if (w.Torpedo) { flags.Add("TORPEDO"); }
            if (w.Targetable) { flags.Add("TARGETABLE"); }
            if (w.DamagesZeppelin) { flags.Add("DMG_ZEP"); }
            if (w.ShakesCamera) { flags.Add("SHAKE"); }
            if (w.Crater) { flags.Add("CRATER"); }
            sb.Append($"{w.Id}  {w.Name,-8}  \"{w.DisplayName}\"");
            sb.Append($"\n    cal={Opt(w.Caliber)} rate={w.FireRate} vel={Opt(w.Velocity)} range={Opt(w.Range)}"
                      + $" acc={Opt(w.Acceleration)} turn={Opt(w.TurnRate)} spread={Opt(w.CannonSpread)}");
            sb.Append($"\n    dmg armor={Opt(w.ArmorDamage)} health={Opt(w.HealthDamage)} combined={Opt(w.Damage)}"
                      + $" | cluster={Opt(w.ClusterSize)} ammo_limit={Opt(w.AmmoLimit)}");
            sb.Append($"\n    lock={Opt(w.LockOn)} det_dist={Opt(w.DetonationDistance)} proximity={Opt(w.ImpactProximity)}"
                      + $" priority={Opt(w.Priority)}");
            sb.Append($"\n    flags: [{string.Join(", ", flags)}]");
            sb.Append($"\n    fire={FmtEffect(w.Fire)} flyout={FmtFlyout(w.Flyout)} looped={w.LoopedSoundName ?? "-"}");
            sb.Append("\n    impact:");
            foreach (var kv in w.Impact)
            {
                sb.Append($" {kv.Key}={FmtEffect(kv.Value)}");
            }
            if (w.Impact.Count == 0)
            {
                sb.Append(" (none)");
            }
            if (w.UnhandledKeys.Count > 0)
            {
                r.UnhandledTotal += w.UnhandledKeys.Count;
                sb.Append($"\n    !! UNHANDLED KEYS: {string.Join(", ", w.UnhandledKeys)}");
            }
            sb.AppendLine();
            sb.AppendLine();
        }
        r.Text = sb.ToString();
        r.Summary = r.UnhandledTotal == 0
            ? $"weapons dump: {r.Shown} entr(y/ies) of {r.Total}, NO unhandled keys"
            : $"weapons dump: {r.Shown} entr(y/ies) of {r.Total}, {r.UnhandledTotal} UNHANDLED key(s) — see the !! lines above";
        return r;
    }

    // ---- loadouts ----------------------------------------------------------------------------

    public static LoadoutResult Loadouts(string zrdrPath, string messagesPath, string planesGamezPath,
        string dataRoot, string filter, string? loadoutOverride, bool forRig = false)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        var r = new LoadoutResult();
        StockLoadouts stock;
        WeaponDefs weapons;
        GameZ planesGamez;
        TextureArchive textures;
        try
        {
            stock = StockLoadouts.Load();
            weapons = WeaponDefs.Load(zrdrPath, Messages.Load(messagesPath));
            planesGamez = GameZ.Load(planesGamezPath);
            // Any texture archive resolves the (meshless) markers; the C1 set is the viewer default.
            textures = new TextureArchive(SessionPaths.ChapterTextures(dataRoot, "C1"));
        }
        catch (Exception e)
        {
            r.Error = $"could not load inputs: {e.Message}";
            r.Summary = $"loadout dump: {r.Error}";
            return r;
        }

        var sb = new StringBuilder();
        sb.AppendLine(forRig
            ? "# Full-rig lab loadouts (Loadout.ForRig) — every firepoint/pylon, seeded from stock_loadouts.json"
            : "# Stock loadouts bound to models — CSVM/data/stock_loadouts.json");
        if (loadoutOverride != null)
        {
            sb.AppendLine($"# --loadout override: binding every plane to '{loadoutOverride}'");
        }
        sb.AppendLine();

        using (textures)
        {
            foreach (var def in stock.All.Values)
            {
                if (filter.Length > 0
                    && !def.Def.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    && !def.Model.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    && !def.Display.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var bindDef = loadoutOverride != null ? stock.For(loadoutOverride) : def;
                sb.Append($"=== {def.Display} ({def.Def} / {def.Model}) ===");
                if (bindDef == null)
                {
                    string why = $"--loadout='{loadoutOverride}' is not a known loadout def";
                    sb.AppendLine($"\n  !! {why}");
                    sb.AppendLine();
                    r.Failures.Add($"{def.Def}: {why}");
                    r.Failed++;
                    continue;
                }
                Node3D? plane = null;
                try
                {
                    plane = new PlaneBuilder(planesGamez, textures).Build(def.Model);
                    var loadout = forRig ? Loadout.ForRig(plane, weapons, bindDef) : Loadout.Bind(bindDef, plane, weapons);
                    sb.Append("\n  guns:");
                    foreach (var g in loadout.Guns)
                    {
                        var names = new List<string>();
                        foreach (var m in g.Muzzles)
                        {
                            names.Add(m.HasMeta(AnimRuntime.NameMeta) ? m.GetMeta(AnimRuntime.NameMeta).AsString() : m.Name);
                        }
                        sb.Append($"\n    slot{g.Slot} {g.Mount,-22} {g.Weapon.Id} ({g.Weapon.Name})"
                                  + $" ammo {g.Ammo}{(g.IsTurret ? "  [TURRET, inert]" : "")}"
                                  + $"  muzzles: {string.Join(", ", names)}");
                    }
                    if (loadout.Hardpoints.Count > 0)
                    {
                        var hp = loadout.Hardpoints[0];
                        int total = 0;
                        foreach (var h in loadout.Hardpoints)
                        {
                            total += h.Capacity;
                        }
                        sb.Append($"\n  hardpoints: {loadout.Hardpoints.Count} x {hp.Weapon.Id} ({hp.Weapon.Name}),"
                                  + $" {hp.Capacity} per pylon = {total} total  (pylon1..pylon{loadout.Hardpoints.Count})");
                    }
                    else
                    {
                        sb.Append("\n  hardpoints: none");
                    }
                    sb.AppendLine();
                    r.Bound++;
                }
                catch (Exception e)
                {
                    sb.AppendLine($"\n  !! {e.Message}");
                    r.Failures.Add($"{def.Def}: {e.Message}");
                    r.Failed++;
                }
                finally
                {
                    plane?.Free();
                }
                sb.AppendLine();
            }
        }
        r.Text = sb.ToString();
        r.Summary = r.Failed == 0
            ? $"loadout dump: {r.Bound} plane(s) bound, every marker resolved"
            : $"loadout dump: {r.Bound} ok, {r.Failed} FAILED — see the !! lines above";
        return r;
    }

    // ---- mip chains ----------------------------------------------------------------------------

    /// <summary>What a chapter's texture archive actually installed as levels 1 and 2, beside the
    /// authored <c>_1</c>/<c>_2</c> siblings the chapter ships — one row per level, with the
    /// measurement <c>BL-055</c> turns on: mean luminance, and the share of pixels above 128 (the
    /// street lights the artists kept and a box filter averages away).
    ///
    /// <para>The chain is built through <see cref="TextureArchive.BuildMipped"/>, i.e. the code a
    /// material's lookup runs, under whatever <c>--mips=</c> policy this run set — so the
    /// <c>installed == authored</c> column is an able-to-fail check on the policy actually taking
    /// effect, not on the levels merely existing on disk.</para></summary>
    public static MipResult MipChains(string texturesPath, string chapter, string filter)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        var r = new MipResult();
        TextureArchive textures;
        try
        {
            textures = new TextureArchive(texturesPath);
        }
        catch (Exception e)
        {
            r.Error = $"could not open the texture archive ({texturesPath}): {e.Message}";
            r.Summary = $"mips dump: {r.Error}";
            return r;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Texture mip chains — {chapter}, {texturesPath}");
        sb.AppendLine($"# policy: --mips={TextureArchive.Mips.ToString().ToLowerInvariant()}");
        sb.AppendLine($"# {textures.AuthoredMipBases.Count} base texture(s) ship an authored level; "
                      + $"{textures.AuthoredMipsAvailable} level(s) in all.");
        sb.AppendLine("# 'px>128' is the share of pixels above luminance 128 — what the artists kept "
                      + "and a box filter averages away.");
        sb.AppendLine();

        using (textures)
        {
            foreach (var name in textures.AuthoredMipBases)
            {
                if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var img = textures.BuildMipped(name, out int authored);
                if (img == null)
                {
                    r.Failures.Add($"{name}: the archive holds it but it would not build");
                    sb.AppendLine($"{name}: FAILED TO BUILD").AppendLine();
                    continue;
                }
                r.Textures++;
                r.LevelsInstalled += authored;
                sb.AppendLine($"=== {name} {img.GetWidth()}x{img.GetHeight()}, "
                              + $"{img.GetMipmapCount()} mip level(s), {authored} authored");
                sb.AppendLine($"  L0            {Luma(MipLevel(img, 0))}");
                for (int level = 1; level <= 2 && level <= img.GetMipmapCount(); level++)
                {
                    var sibling = textures.FindImage($"{name}_{level}");
                    if (sibling == null)
                    {
                        sb.AppendLine($"  L{level}            {Luma(MipLevel(img, level))}   (no authored level)");
                        continue;
                    }
                    r.LevelsShipped++;
                    var installed = MipLevel(img, level);
                    bool same = SameLuma(installed, sibling);
                    if (same)
                    {
                        r.LevelsMatching++;
                    }
                    else
                    {
                        r.Mismatched.Add($"{name}_{level}");
                    }
                    sb.AppendLine($"  L{level} installed  {Luma(installed)}   {(same ? "== authored" : "!= AUTHORED")}");
                    sb.AppendLine($"  L{level} authored   {Luma(sibling)}");
                }
                sb.AppendLine();
            }
        }
        r.Text = sb.ToString();
        r.Summary = r.Failures.Count > 0
            ? $"mips dump: {r.Textures} texture(s), {r.Failures.Count} FAILED to build"
            : $"mips dump: {r.Textures} texture(s), {r.LevelsMatching}/{r.LevelsShipped} level(s) "
              + $"match the authored artwork ({r.LevelsInstalled} installed as authored)";
        return r;
    }

    // ---- destructible damage -----------------------------------------------------------------

    /// <summary>Every enabled collision shape under a subtree. SceneBuilder attaches a
    /// <c>StaticBody3D</c> named <c>col</c> with one <c>CollisionShape3D</c> per collidable mesh, and
    /// <c>SetSubtreeActive</c> toggles that shape's <c>Disabled</c> as it swaps healthy→destroyed.
    /// The swap targets nodes through the compiled symbol table, which can resolve to geometry
    /// OUTSIDE the small anim anchor, so a census under the anchor misses it — pass
    /// <see cref="WorldRootOf"/> and compare the two sets around one kill.
    /// <para><b>Report the two directions separately, never the signed sum</b> (verification
    /// WORLD-10): a death both switches the healthy collider off and brings wreck colliders on, and the
    /// net can be positive while the real removal happened.</para></summary>
    public static HashSet<CollisionShape3D> EnabledColliders(Node root)
    {
        var set = new HashSet<CollisionShape3D>();
        void Walk(Node n)
        {
            if (n is CollisionShape3D cs && !cs.Disabled)
            {
                set.Add(cs);
            }
            foreach (var c in n.GetChildren())
            {
                Walk(c);
            }
        }
        Walk(root);
        return set;
    }

    /// <summary>Every collider under a subtree that is enabled while nothing is drawn there — the
    /// invisible-wall census. Reports the offending shape's owning node chain, leaf-first.
    ///
    /// <para>This is the generic tripwire for the whole class the C1/IA1 zeppelin belonged to
    /// (<see cref="Mech3.WorldCollision"/>): visibility is inherited and <c>Disabled</c> is not, so
    /// any code that writes the two separately eventually disagrees with itself. Run over a built
    /// world it needs no knowledge of which entity a mission hides.</para></summary>
    public static List<string> InvisibleEnabledColliders(Node root)
    {
        var found = new List<string>();

        // Leaf-first ancestor names, cut at the first invisible one (bracketed), so the report
        // names the node that actually hid the geometry rather than the path to the world root.
        static string Chain(Node3D leaf)
        {
            var parts = new List<string>();
            for (Node? n = leaf; n != null && parts.Count < 12; n = n.GetParent())
            {
                bool hidden = n is Node3D { Visible: false };
                parts.Add(hidden ? $"[{n.Name}]" : n.Name.ToString());
                if (hidden)
                {
                    break;
                }
            }
            return string.Join(" < ", parts);
        }

        void Walk(Node n)
        {
            if (n is CollisionShape3D { Disabled: false } cs && !cs.IsVisibleInTree())
            {
                found.Add(Chain(cs));
            }
            foreach (var c in n.GetChildren())
            {
                Walk(c);
            }
        }
        Walk(root);
        return found;
    }

    /// <summary>The top <see cref="Node3D"/> above a node — the world subtree root, so a census
    /// walks placed + partition geometry and not the UI or the Window.</summary>
    public static Node3D WorldRootOf(Node3D n)
    {
        var t = n;
        while (t.GetParent() is Node3D p)
        {
            t = p;
        }
        return t;
    }

    /// <summary>Counts the <c>healthy</c>/<c>destroyed</c> variant nodes under a subtree and how
    /// many of each are shown — the death-swap diagnostic. Names only: the swap mechanism itself
    /// keys off the definition's own <c>OBJECT_ACTIVE_STATE</c>, never a name scan.</summary>
    public static void CountVariants(Node3D root, out int healthyVisible, out int healthyAll,
        out int destroyedVisible, out int destroyedAll)
    {
        int hVis = 0, hAll = 0, dVis = 0, dAll = 0;
        void Walk(Node3D n)
        {
            string cs = n.HasMeta(AnimRuntime.NameMeta)
                ? n.GetMeta(AnimRuntime.NameMeta).AsString()
                : n.Name.ToString();
            if (cs.Contains("healthy", StringComparison.OrdinalIgnoreCase))
            {
                hAll++;
                if (n.Visible) { hVis++; }
            }
            if (cs.Contains("destroyed", StringComparison.OrdinalIgnoreCase))
            {
                dAll++;
                if (n.Visible) { dVis++; }
            }
            foreach (var c in n.GetChildren())
            {
                if (c is Node3D c3)
                {
                    Walk(c3);
                }
            }
        }
        Walk(root);
        healthyVisible = hVis;
        healthyAll = hAll;
        destroyedVisible = dVis;
        destroyedAll = dAll;
    }

    /// <summary>Sweeps one live destructible instance per distinct def (optionally filtered) and
    /// records what its damage/death did. Two modes: <paramref name="damageHd"/> &gt; 0 spends that
    /// much HEALTH_DAMAGE per discrete weapon hit (death swap, colliders, debris, sound, the
    /// collide gate and the reset/rekill idempotency check); otherwise the HP is swept continuously
    /// from full to zero to find which stage effect fires at which health.
    ///
    /// <para>Requires the world subtree to be in the scene tree with
    /// <see cref="AnimRuntime.ManualAdvance"/> set: it ticks the clock past the death schedule to
    /// see the debris launch, and a global-transform read on an out-of-tree node returns
    /// identity.</para></summary>
    public static DamageResult Damage(AnimRuntime runtime, string chapter, string filter, float damageHd)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        static bool HasDamage(AnimDefinition d) => d.Sequences.Any(s =>
            string.Equals(s.Name, "DAMAGE_SEQUENCE", StringComparison.OrdinalIgnoreCase));

        var result = new DamageResult
        {
            TotalInstances = runtime.Destructibles.Count,
            DistinctAnchors = runtime.Destructibles.DistinctAnchors,
        };

        // One representative instance per distinct def — a wildcard NAME binds many identical
        // towers, and sweeping every one would just repeat the same result and start hundreds of
        // effects.
        var chosen = new List<DestructibleRegistry.Instance>();
        var seenDefs = new HashSet<AnimDefinition>();
        foreach (var inst in runtime.Destructibles.All)
        {
            // Continuous-sweep mode only makes sense for staged DAMAGE_SEQUENCE defs; the
            // discrete-kill mode applies to EVERY destructible — the doors and gates instant-die
            // with no stages, so gating them out would hide exactly the collider cases.
            if (damageHd <= 0f && !HasDamage(inst.Def))
            {
                continue;
            }
            if (filter.Length > 0
                && !inst.Def.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (seenDefs.Add(inst.Def))
            {
                chosen.Add(inst);
            }
            if (chosen.Count >= SweepCap)
            {
                result.Capped = true;
                break;
            }
        }

        var sb = new StringBuilder();
        string mode = damageHd > 0f ? $"weapon hits, {damageHd:0.##} HEALTH_DAMAGE each" : "continuous HP sweep";
        string kind = damageHd > 0f ? "destructible def(s)" : "DAMAGE_SEQUENCE def(s)";
        sb.AppendLine($"damage-test: chapter {chapter}, filter '{filter}', mode = {mode} — "
            + $"{chosen.Count} {kind} of {result.TotalInstances} destructible instance(s)");
        foreach (var inst in chosen)
        {
            // Discrete-hit mode: spend a fixed HEALTH_DAMAGE per hit through DamageAt and count
            // hits to destruction. DamageAt resolves a struck node to its AUTHORITATIVE instance, so
            // drive the resolved one (the compiled def wins a shared node) — driving the picked
            // reader twin would damage the compiled instance and never see HP fall.
            var target = damageHd > 0f ? (runtime.Destructibles.Resolve(inst.Anchor) ?? inst) : inst;
            var row = new DamageRow
            {
                Def = target.Def.Name,
                MaxHealth = target.MaxHealth,
                Source = target.Def.Archive != null ? "compiled" : "reader",
                Activation = target.Def.Activation.ToString(),
            };
            var fired = new List<(string At, string Effect)>();
            var started = new List<(string? Anim, Node3D? Anchor)>();
            int hit = 0;
            float atHp = target.MaxHealth;
            void OnStarted(AnimDefinition def, Node3D? anchor)
            {
                fired.Add((damageHd > 0f ? $"hit {hit}" : $"HP≤{atHp:0.##}", def.AnimName ?? def.Name));
                started.Add((def.AnimName, anchor));
            }

            target.Health = target.MaxHealth;
            target.Status = DestructibleRegistry.State.Healthy;
            target.DamageStage = 0;
            var colBefore = damageHd > 0f ? EnabledColliders(WorldRootOf(target.Anchor)) : new HashSet<CollisionShape3D>();
            int debrisBefore = runtime.BallisticMotionsLaunched;
            int soundsBefore = runtime.OneShotSoundsPlayed;
            runtime.OnInstanceStarted += OnStarted;
            if (damageHd > 0f)
            {
                int cap = (int)(target.MaxHealth / damageHd) + 4;   // a few past the expected kill
                while (target.Status != DestructibleRegistry.State.Destroyed && hit < cap)
                {
                    hit++;
                    runtime.DamageAt(target.Anchor, damageHd);
                }
            }
            else
            {
                // Fine enough to land on the round-fraction thresholds exactly (0.60/0.30 of HEALTH …).
                const int steps = 240;
                for (int i = 0; i <= steps; i++)
                {
                    atHp = target.MaxHealth * (1f - i / (float)steps);
                    target.Health = atHp;
                    runtime.ApplyDamageStages(target);
                }
            }
            runtime.OnInstanceStarted -= OnStarted;
            row.Hits = hit;
            row.Destroyed = target.Status == DestructibleRegistry.State.Destroyed;
            row.StagesFired = fired.Count;

            // Walk-up resolution check: resolving from a deep descendant of the anchor — the kind of
            // node a projectile's raycast actually strikes (a collider sits under the mesh under the
            // anchor) — must land back on this same destructible.
            Node3D deep = target.Anchor;
            while (deep.GetChildCount() > 0 && deep.GetChild(0) is Node3D child)
            {
                deep = child;
            }
            var back = runtime.Destructibles.Resolve(deep);
            row.Resolved = back?.Anchor == target.Anchor;
            string resolve = row.Resolved ? "resolve✓" : $"resolve✗({back?.Def.Name ?? "null"})";

            // Death-swap check: once killed, the healthy subtree should be hidden and the destroyed
            // subtree shown. Scan the anchor's descendants by cs_name — a test diagnostic (the
            // mechanism keys off the def's own OBJECT_ACTIVE_STATE, not names).
            string swap = "";
            if (damageHd > 0f && row.Destroyed)
            {
                CountVariants(target.Anchor, out int hVis, out int hAll, out int dVis, out int dAll);
                if (hAll > 0 || dAll > 0)
                {
                    swap = $"swap[healthy {hVis}/{hAll} shown, destroyed {dVis}/{dAll} shown]; ";
                }
                // Collider census, split by direction: how many colliders this kill switched OFF (the
                // healthy door/building collision that stops blocking flight) vs ON (the wreck/debris
                // the death — and any chained animation — brings solid). A net count hides the door
                // removal when the death also spawns a solid wreck.
                var colAfter = EnabledColliders(WorldRootOf(target.Anchor));
                row.CollidersOff = colBefore.Count(cs => !colAfter.Contains(cs));
                row.CollidersOn = colAfter.Count(cs => !colBefore.Contains(cs));
                swap += $"col[off {row.CollidersOff}, on {row.CollidersOn}]; ";
                // Debris tumble: the death's ballistic OBJECT_MOTION bodies — the wreck pieces that
                // arc out under gravity and tumble. They are SCHEDULED (the water tower's at t=2.2 s),
                // so advance the death forward past the schedule to let them launch — done AFTER
                // swap/col so those stay the immediate post-death state (pre-tick).
                for (int i = 0; i < 7; i++)
                {
                    runtime.Advance(0.5f);   // 3.5 s — past the ~2.2 s schedule, into the tumble
                }
                row.Debris = runtime.BallisticMotionsLaunched - debrisBefore;
                swap += $"debris[{row.Debris} launched]; ";
                // One-shot SOUND: the death/damage sequence's explosion audio. Audio cannot be
                // screenshot-verified, so a nonzero count across the kill+advance is the headless
                // proof the destruction sounded. Zero when run muted or on a def whose death
                // authors no Sound event.
                row.Sounds = runtime.OneShotSoundsPlayed - soundsBefore;
                swap += $"snd[{row.Sounds} played]; ";
            }
            // Stop the effects this run started, AFTER the debris tick so the pieces actually launch
            // first: reader-wildcard and compiled per-instance defs bind the SAME tower nodes, so a
            // leftover live effect would make the twin's identical CALL_ANIMATION a no-op and read as
            // "no stage effect fired".
            foreach (var (anim, anchor) in started)
            {
                runtime.Stop(anim, anchor);
            }

            // Collide-gate probe: reset and apply a plane COLLISION via CollideDamageAt. Only a
            // WeaponOrCollideHit destructible (the facades/windows/agyrobus) accepts it and breaks;
            // a WeaponHit object (tower, gate) ignores the collision and stands.
            string collide = "";
            if (damageHd > 0f)
            {
                target.Health = target.MaxHealth;
                target.Status = DestructibleRegistry.State.Healthy;
                target.DamageStage = 0;
                bool accepted = runtime.CollideDamageAt(target.Anchor, target.MaxHealth + 1f);
                bool broke = target.Status == DestructibleRegistry.State.Destroyed;
                row.CollideAccepted = accepted;
                collide = $"collide[{(accepted ? (broke ? "✓ broke" : "✓ hit, survived") : "✗ ignored")}, {row.Activation}]; ";
            }

            // Reset/restore check: from a destroyed state, ResetDestructible returns the object to
            // healthy (full HP, healthy subtree visible, destroyed hidden, debris flown home), and an
            // identical second kill takes the same hits — proving destroy→reset→destroy is idempotent.
            string reset = "";
            if (damageHd > 0f)
            {
                int cap2 = (int)(target.MaxHealth / damageHd) + 4;
                while (target.Status != DestructibleRegistry.State.Destroyed && cap2-- > 0)
                {
                    runtime.DamageAt(target.Anchor, damageHd);   // ensure dead before resetting
                }
                runtime.ResetDestructible(target);
                bool backHp = target.Status == DestructibleRegistry.State.Healthy
                    && target.Health >= target.MaxHealth - 1e-3f;
                CountVariants(target.Anchor, out int hVis, out int hAll, out int dVis, out int dAll);
                bool backVis = hAll == 0 || (hVis == hAll && dVis == 0);   // healthy shown, destroyed hidden
                int rekap = (int)(target.MaxHealth / damageHd) + 4;
                int hits2 = 0;
                while (target.Status != DestructibleRegistry.State.Destroyed && hits2 < rekap)
                {
                    hits2++;
                    runtime.DamageAt(target.Anchor, damageHd);
                }
                row.ResetHealthy = backHp && backVis;
                row.RekillMatched = hits2 == hit;
                reset = $"reset[healthy={(row.ResetHealthy == true ? "✓" : "✗")} (h{hVis}/{hAll},d{dVis}/{dAll}), "
                    + $"rekill {hits2}h {(row.RekillMatched == true ? "✓" : $"✗ vs {hit}")}]; ";
            }

            string stages = fired.Count == 0
                ? "no stage effect fired"
                : string.Join(", ", fired.Select(f => $"{f.At} → {f.Effect}"));
            string outcome = damageHd > 0f
                ? (row.Destroyed
                    ? $"DESTROYED in {hit} hit(s); {swap}{collide}{reset}"
                    : $"SURVIVED {hit} hit(s); {collide}{reset}")
                : "";
            row.Line = $"  {row.Def} (HEALTH {row.MaxHealth:0.##}, {row.Source}) {resolve}: {outcome}{stages}";
            sb.AppendLine(row.Line);
            result.Rows.Add(row);
        }
        result.Text = sb.ToString();

        // The world's collidable-geometry inventory, so we can confirm destructible roles
        // (healthy / destroyed / door*) are among the solid geometry. Counts per owning-mesh
        // cs_name — a per-name census is enough to see what is solid; positions are not needed.
        if (chosen.Count > 0)
        {
            var byName = new SortedDictionary<string, int>();
            int cols = 0;
            void Walk(Node n, string parentName)
            {
                string name = n is Node3D n3 && n3.HasMeta(AnimRuntime.NameMeta)
                    ? n3.GetMeta(AnimRuntime.NameMeta).AsString()
                    : n.Name.ToString();
                if (n is StaticBody3D body && body.Name.ToString() == "col")
                {
                    cols++;
                    byName.TryGetValue(parentName, out int c);
                    byName[parentName] = c + 1;
                }
                foreach (var c in n.GetChildren())
                {
                    Walk(c, name);
                }
            }
            Walk(WorldRootOf(chosen[0].Anchor), "");
            var inv = new StringBuilder($"{cols} collidable meshes, by owner cs_name:\n");
            foreach (var (nm, c) in byName)
            {
                inv.AppendLine($"  {c,4}  {nm}");
            }
            result.CollidableMeshes = cols;
            result.CollidersText = inv.ToString();
        }
        result.Summary = $"damage-test: {result.Rows.Count} def(s) swept of "
                         + $"{result.TotalInstances} instance(s) across {result.DistinctAnchors} node group(s)";
        // The one-shot death sounds this sweep fired are fire-and-forget nodes swept in
        // WorldSounds.Tick — but this harness pumps no frames, so free them here or they leak.
        runtime.Sounds?.FlushOneShots();
        return result;
    }

    // ---- flight envelope ---------------------------------------------------------------------

    /// <summary>Steps a throwaway <see cref="FlightModel"/> through the manoeuvres the original was
    /// measured flying, and reports both numbers side by side.
    ///
    /// <para>This is the only instrument that can answer "did a flight-constant change break the
    /// calibration?" — the constants interact (thrust sets speed, speed sets the yaw <c>eff</c>, so
    /// a thrust change moves yaw authority), and a screenshot cannot see any of it. No world, no
    /// scene, no game assets beyond the zrdr readers: it constructs the model directly and
    /// integrates it at the fixed <c>--det</c> step.</para>
    ///
    /// <para>⚠ Every target here is the <b>Bloodhawk's</b>. It is the only airframe the original was
    /// recorded flying, so another plane's run reports its numbers with nothing to assert against —
    /// which is honest, not a gap to fill by scaling the Bloodhawk's.</para></summary>
    public static FlightEnvelopeResult FlightEnvelope(string zrdrPath, string planeNodeName)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        var r = new FlightEnvelopeResult();
        PlaneStats stats;
        try
        {
            stats = PlaneStats.Load(zrdrPath, planeNodeName);
        }
        catch (Exception e)
        {
            r.Error = $"could not load plane stats for '{planeNodeName}' ({zrdrPath}): {e.Message}";
            r.Summary = $"flight envelope: {r.Error}";
            return r;
        }

        bool bhawk = planeNodeName.Equals("player_bhawk", StringComparison.OrdinalIgnoreCase);
        float fd = stats.FdSpeed;
        float thrustAccel = stats.EnginePower * Config.GetFloat("flightModel.thrustConst", 184f)
                            / (stats.VehWeight / 1000f);

        void Row(string name, string what, string unit, double model, double? measured,
                 double tol, string detail = "", bool info = false)
        {
            r.Rows.Add(new FlightRow
            {
                Name = name,
                What = what,
                Unit = unit,
                Model = model,
                Measured = bhawk ? measured : null,
                Tolerance = tol,
                Detail = detail,
                Informational = info,
            });
        }

        // --- level full-throttle equilibrium. Drag is normalized so drag(fd_speed) = max thrust,
        // so this must land on fd_speed for every airframe by construction; it is here because that
        // construction is exactly what a thrust change could break silently.
        var m = Fresh(stats, Level(), 0.5f * fd, 1f);
        Run(m, 1f, 180f, pitch: 0f);
        Row("level-top-speed", "level full throttle held to equilibrium", "mph",
            m.Speed / Mph, 300.4, 4.0, $"fd_speed = {fd / Mph:0.0} mph");

        // --- acceleration. THE measurement that sets ThrustConst: one constant fixes both this and
        // the terminal dive below, and the two agree, which is what makes the drag shape credible.
        m = Fresh(stats, Level(), 150f * Mph, 1f);
        double tAccel = RunUntil(m, 1f, 30f, () => m.Speed >= 290f * Mph);
        Row("accel-150-290", "level full throttle, 150 -> 290 mph", "s", tAccel, 3.76, 0.40);

        // --- terminal dive. Nose (and path) 70.7° down, full throttle, held to terminal — the angle
        // the original's "vertical" dive clip actually came out at, so this compares like with like.
        m = Fresh(stats, Pitched(-70.7f), 0.9f * fd, 1f);
        Run(m, 1f, 120f, pitch: 0f);
        double pathDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(m.VelocityDir.Y, -1f, 1f)));
        Row("terminal-dive", "70.7° dive at full throttle, held to terminal", "mph",
            m.Speed / Mph, 355.2, 6.0,
            $"settled path {pathDeg:0.0}°, {m.Speed / fd:0.000} x fd_speed");

        // --- roll. Accumulated body roll rate: no other axis is commanded, so this is the 360° the
        // stopwatch and the video's ADI bank centroid both timed.
        m = Fresh(stats, Level(), fd, 1f);
        double tRoll = RunUntil(m, 1f, 30f, RollAccum(m), roll: 1f);
        Row("roll-360", "full aileron from level cruise, 360°", "s", tRoll, 2.05, 0.25);

        // --- pitch. Steady rate after the 1/damp spin-up, at three speeds: ours is
        // speed-independent by construction, and the video says the original's is too, so the point
        // of the three is to catch anything else (stall, lift, eff) leaking into pitch at the ends.
        var pitchRates = new List<double>();
        foreach (float mph in new[] { 120f, 200f, 280f })
        {
            m = Fresh(stats, Level(), mph * Mph, 1f);
            Run(m, 1f, 2f, pitch: 1f);
            pitchRates.Add(Mathf.RadToDeg(m.BodyRates.X));
        }
        Row("pitch-rate", "sustained full-elevator body pitch rate", "°/s",
            pitchRates[1], 33.0, 3.0,
            $"at 120/200/280 mph = {pitchRates[0]:0.0}/{pitchRates[1]:0.0}/{pitchRates[2]:0.0} °/s");

        // --- yaw. The one axis 'eff' scales, so it is the axis a thrust change moves: faster
        // acceleration holds the plane nearer fd_speed, where eff is at its floor.
        m = Fresh(stats, Level(), 290f * Mph, 1f);
        double sumSpeed = 0, samples = 0;
        double tYaw = RunUntil(m, 1f, 60f, YawAccum(m), yaw: 1f,
                               onStep: () => { sumSpeed += m.Speed; samples++; });
        Row("yaw-360", "full rudder from 290 mph, 360°", "s", tYaw, 28.6, 3.0,
            samples > 0 ? $"mean speed {sumSpeed / samples / Mph:0.0} mph" : "");

        // --- altitude cap (BL-094/CAP-03). A fixed 22° nose-up hold from level cruise (pitch input
        // stays at 0 throughout — the attitude is set once via Pitched, matching the original clip's
        // fixed pull rather than a continuous full-elevator input, which would loop instead of climb).
        // Without the clamp this settles into the model's accepted "steep-climb equilibrium" artifact
        // and never stops climbing; with it, altitude must stop at the resting cap.
        m = Fresh(stats, Pitched(22f), fd, 1f);
        Run(m, 1f, 240f, pitch: 0f);
        Row("altitude-cap", "22° nose-up hold at full throttle, altitude settled against the clamp", "ft",
            m.Position.Y / Ft, 6571.6, 100.0,
            $"{m.Speed / Mph:0.0} mph at settle (original 173.7 mph — the existing stall model owns "
            + "whatever bleed shape follows the clamp, not asserted here)");

        // --- level speed 15 m under the cap (BL-094/CAP-03): the clamp must be a no-op this close to
        // the line — CAP-03 measured level equilibrium flat to ±0.3 mph right up to 1988 m.
        m = Fresh(stats, Level(), 0.5f * fd, 1f);
        m.Position = new Vector3(0f, 1988f, 0f);
        Run(m, 1f, 180f, pitch: 0f);
        Row("level-speed-near-cap", "level full throttle at 1988 m, held to equilibrium", "mph",
            m.Speed / Mph, 300.4, 4.0, "altitude clamp must not leak below the cap");

        // --- 1/8 throttle. Both rows are INFORMATIONAL: the original's throttle→thrust curve is
        // undecoded, so a miss here indicts that curve or the low-speed drag blend and cannot say
        // which. Asserting it would fail the build over an unscoped question.
        m = Fresh(stats, Level(), 0.9f * fd, 0.125f);
        Run(m, 0.125f, 300f, pitch: 0f);
        double idlePath = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(m.VelocityDir.Y, -1f, 1f)));
        Row("eighth-throttle-speed", "1/8 throttle held to equilibrium", "mph",
            m.Speed / Mph, 137.9, 6.0,
            $"{m.Speed / fd:0.000} x fd_speed (original 0.459), settled path {idlePath:0.0}° "
            + "— it ends up below lift speed and sinks, so this is not a level equilibrium",
            info: true);

        m = Fresh(stats, Level(), 290f * Mph, 0.125f);
        double tDecel = RunUntil(m, 0.125f, 60f, () => m.Speed <= 150f * Mph);
        Row("decel-290-150", "throttle cut to 1/8, 290 -> 150 mph", "s", tDecel, 7.04, 1.0,
            "", info: true);

        // --- zoom climb. INFORMATIONAL, and it is the row that exposes the model's largest known
        // gap: the original bled to 104 mph reaching its apex where we arrive still fast, because we
        // model no induced drag at all — a hard pull costs us nothing. The altitude is close; the
        // energy is not. (Its stick history is also unknown: a held full pull is a loop, and the
        // same session's loop passed 180° in ~6 s, so 10.5 s to apex was some other input.)
        m = Fresh(stats, Level(), 300f * Mph, 1f);
        float apex = 0f, minSpeed = float.MaxValue;
        double tApex = RunUntil(m, 1f, 30f,
            () => m.Position.Y < apex - 1f, pitch: 1f,
            onStep: () =>
            {
                apex = Mathf.Max(apex, m.Position.Y);
                minSpeed = Mathf.Min(minSpeed, m.Speed);
            });
        Row("zoom-climb", "full pull from 300 mph level, altitude gained", "ft",
            apex / Ft, 1635.0, 200.0,
            $"min speed {minSpeed / Mph:0.0} mph (original 104 — we model no induced drag), "
            + $"apex at {tApex:0.0} s (original 10.5)",
            info: true);

        var sb = new StringBuilder();
        sb.AppendLine($"# flight envelope — {planeNodeName} ({stats.DefName})");
        sb.AppendLine($"# fd_speed {fd:0.#} m/s ({fd / Mph:0.0} mph)  weight {stats.VehWeight:0} kg  "
                      + $"engine power {stats.EnginePower:0.###}  gravity {stats.Gravity:0.#} m/s²");
        sb.AppendLine($"# max thrust accel {thrustAccel:0.00} m/s²  stepped at {EnvDt * 1000f:0.0} ms");
        sb.AppendLine(bhawk
            ? "# 'original' = decoded from cockpit-gauge video, analysis/video-flight-calibration/"
            : $"# no measured original for {planeNodeName} — the Bloodhawk is the only airframe on video");
        sb.AppendLine();
        sb.AppendLine($"{"scenario",-22} {"unit",-5} {"model",10} {"original",10} {"err",8}  verdict");
        foreach (var row in r.Rows)
        {
            string verdict = row.Asserted ? (row.Ok ? "ok" : "!! FAIL") : "(not asserted)";
            sb.AppendLine($"{row.Name,-22} {row.Unit,-5} {row.Model,10:0.00} "
                          + $"{(row.Measured?.ToString("0.00") ?? "-"),10} "
                          + $"{(row.ErrorPct is { } p ? $"{p:+0.0;-0.0}%" : "-"),8}  {verdict}");
            sb.AppendLine($"{"",-22} {row.What}{(row.Detail.Length > 0 ? $" — {row.Detail}" : "")}");
        }
        r.Text = sb.ToString();
        r.Summary = r.Failed == 0
            ? $"flight envelope: {r.Asserted} scenario(s) asserted against the original, all within tolerance"
            : $"flight envelope: {r.Failed} of {r.Asserted} asserted scenario(s) FAILED — see the !! lines above";
        return r;
    }

    private static Basis Level() => Basis.Identity;

    /// <summary>Attitude with the nose <paramref name="deg"/>° above the horizon (negative = dive),
    /// wings level. Verified by the report's own settled-path readout rather than assumed.</summary>
    private static Basis Pitched(float deg) => Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(deg));

    /// <summary>A model parked at an attitude and speed, with the flight path along the nose —
    /// <see cref="FlightModel.Reset"/>'s own convention, so a scenario starts trimmed.</summary>
    private static FlightModel Fresh(PlaneStats stats, Basis attitude, float speed, float throttle)
    {
        var m = new FlightModel(stats);
        m.Reset(Vector3.Zero, attitude, speed, throttle);
        return m;
    }

    private static void Run(FlightModel m, float throttle, float seconds,
                            float pitch = 0f, float roll = 0f, float yaw = 0f)
    {
        for (float t = 0f; t < seconds; t += EnvDt)
        {
            m.Step(new FlightInput { Pitch = pitch, Roll = roll, Yaw = yaw, Throttle = throttle }, EnvDt);
        }
    }

    /// <summary>Steps until <paramref name="done"/> or <paramref name="limit"/>, returning the
    /// elapsed sim seconds (the limit itself if it never finished — a scenario that ran out of time
    /// reports as far off rather than as a hang).</summary>
    private static double RunUntil(FlightModel m, float throttle, float limit, Func<bool> done,
                                   float pitch = 0f, float roll = 0f, float yaw = 0f,
                                   Action? onStep = null)
    {
        for (float t = 0f; t < limit; t += EnvDt)
        {
            m.Step(new FlightInput { Pitch = pitch, Roll = roll, Yaw = yaw, Throttle = throttle }, EnvDt);
            onStep?.Invoke();
            if (done())
            {
                return t + EnvDt;
            }
        }
        return limit;
    }

    /// <summary>One level of a mip chain as a standalone image. Godot stores the chain as one buffer
    /// with the levels end to end, so a level is a slice at its own offset.</summary>
    private static Image MipLevel(Image img, int level)
    {
        if (level == 0)
        {
            return img.GetMipmapCount() == 0 ? img
                : Image.CreateFromData(img.GetWidth(), img.GetHeight(), false, img.GetFormat(),
                    img.GetData()[..(int)img.GetMipmapOffset(1)]);
        }
        var data = img.GetData();
        int start = (int)img.GetMipmapOffset(level);
        int end = level + 1 <= img.GetMipmapCount() ? (int)img.GetMipmapOffset(level + 1) : data.Length;
        return Image.CreateFromData(Mathf.Max(1, img.GetWidth() >> level),
            Mathf.Max(1, img.GetHeight() >> level), false, img.GetFormat(), data[start..end]);
    }

    /// <summary>Mean luminance and the share of pixels above 128, formatted as one column pair.
    /// Rec.601 luma, the weighting <c>analysis/item9-depth-bias/CBLOCK-LOD.md</c> §1b measured with,
    /// so the two numbers are comparable to the ones in that file.</summary>
    private static string Luma(Image image)
    {
        var (mean, bright) = LumaStats(image);
        return $"{image.GetWidth(),4}x{image.GetHeight(),-4} mean {mean,6:0.00}  px>128 {bright,6:0.000}%";
    }

    private static (double Mean, double Bright) LumaStats(Image image)
    {
        var img = image;
        if (img.GetFormat() != Image.Format.Rgb8 && img.GetFormat() != Image.Format.Rgba8)
        {
            img = (Image)image.Duplicate();
            img.Convert(Image.Format.Rgb8);
        }
        int stride = img.GetFormat() == Image.Format.Rgba8 ? 4 : 3;
        var data = img.GetData();
        double sum = 0;
        int bright = 0, n = 0;
        for (int i = 0; i + stride <= data.Length; i += stride)
        {
            double y = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
            sum += y;
            if (y > 128.0)
            {
                bright++;
            }
            n++;
        }
        return n == 0 ? (0, 0) : (sum / n, bright * 100.0 / n);
    }

    // Whether an installed level carries the authored artwork. Compared on the two numbers the
    // report prints rather than on bytes: the level is stored in the base's pixel format, so an
    // authored PNG that decoded to another format is converted on the way in and byte equality
    // would fail on a chain that is nonetheless exactly the artwork.
    private static bool SameLuma(Image installed, Image authored)
    {
        var a = LumaStats(installed);
        var b = LumaStats(authored);
        return Math.Abs(a.Mean - b.Mean) < 0.005 && Math.Abs(a.Bright - b.Bright) < 0.0005;
    }

    /// <summary>Predicate that integrates the body roll rate and trips at a full turn — the rate is
    /// what the stopwatch and the video's bank readout both timed, and nothing else is commanded.</summary>
    private static Func<bool> RollAccum(FlightModel m)
    {
        double turned = 0;
        return () => (turned += Math.Abs(m.BodyRates.Z) * EnvDt) >= Math.Tau;
    }

    private static Func<bool> YawAccum(FlightModel m)
    {
        double turned = 0;
        return () => (turned += Math.Abs(m.BodyRates.Y) * EnvDt) >= Math.Tau;
    }

    // ---- shared formatting -------------------------------------------------------------------

    private static string Opt<T>(T? v) where T : struct => v.HasValue ? v.Value.ToString() ?? "-" : "-";

    private static string FmtEffect(WeaponEffect? e)
    {
        if (e == null)
        {
            return "-";
        }
        var parts = new List<string>();
        if (e.Animation != null) { parts.Add($"anim:{e.Animation}"); }
        if (e.SurfaceAnimation != null) { parts.Add($"surf:{e.SurfaceAnimation}"); }
        if (e.Effect != null) { parts.Add($"fx:{e.Effect}"); }
        if (e.Sound != null) { parts.Add($"snd:{e.Sound}"); }
        return "{" + string.Join("/", parts) + "}";
    }

    private static string FmtFlyout(WeaponFlyout? f)
    {
        if (f == null)
        {
            return "-";
        }
        var parts = new List<string>();
        if (f.Model != null) { parts.Add($"model:{f.Model}"); }
        if (f.ModelAnimation != null) { parts.Add($"anim:{f.ModelAnimation}"); }
        if (f.Sound != null) { parts.Add($"snd:{f.Sound}"); }
        return "{" + string.Join("/", parts) + "}";
    }

    /// <summary>Airframe marker rig: how many of the known player airframes have a rig in
    /// planes.zbd, and which were asked for but not found.</summary>
    public sealed class MarkersResult
    {
        public readonly List<string> Missing = new();
        public string Text = "";
        public string Summary = "";
        public string? Error;
        public int Requested;
        public int Done;
        public bool Ok => Error == null && Requested > 0 && Done == Requested;
    }

    /// <summary>weapons.json read through the typed reader: entry count and any key the reader
    /// does not map.</summary>
    public sealed class WeaponsResult
    {
        public string Text = "";
        public string Summary = "";
        public string? Error;
        public int Shown;
        public int Total;
        public int UnhandledTotal;
        public string? EmptyClipSound;
        public bool Ok => Error == null && Shown > 0 && UnhandledTotal == 0;
    }

    /// <summary>What a chapter's mip chains hold against what its archive ships: how many authored
    /// levels were installed, how many of the shipped levels the installed chain actually carries,
    /// and which ones it does not.</summary>
    public sealed class MipResult
    {
        public readonly List<string> Mismatched = new();
        public readonly List<string> Failures = new();
        public string Text = "";
        public string Summary = "";
        public string? Error;
        /// <summary>Base textures swept — those shipping at least one authored level.</summary>
        public int Textures;
        /// <summary>Authored levels the archive holds for the swept textures.</summary>
        public int LevelsShipped;
        /// <summary>Levels the archive reported installing from authored artwork.</summary>
        public int LevelsInstalled;
        /// <summary>Shipped levels whose installed pixels are the authored ones.</summary>
        public int LevelsMatching;
        public bool Ok => Error == null && Failures.Count == 0 && Textures > 0;
    }

    /// <summary>Stock loadouts bound to their built models: how many bound and every binding
    /// failure's message (a marker that does not resolve, an unknown weapon id).</summary>
    public sealed class LoadoutResult
    {
        public readonly List<string> Failures = new();
        public string Text = "";
        public string Summary = "";
        public string? Error;
        public int Bound;
        public int Failed;
        public bool Ok => Error == null && Bound > 0 && Failed == 0;
    }

    /// <summary>One destructible def's sweep — the checks that used to be <c>✓</c>/<c>✗</c> glyphs
    /// inside the report line, as fields.</summary>
    public sealed class DamageRow
    {
        public string Def = "";
        public float MaxHealth;
        public string Source = "";
        public string Activation = "";
        public bool Resolved;          // a deep descendant resolves back to this instance
        public bool Destroyed;
        public int Hits;
        public int StagesFired;
        public int CollidersOff;
        public int CollidersOn;
        public int Debris;
        public int Sounds;
        public bool? ResetHealthy;     // null when the mode never reset (continuous sweep)
        public bool? RekillMatched;
        public bool? CollideAccepted;
        public string Line = "";
    }

    /// <summary>The destructible sweep as a whole: the swept rows plus the uncapped registry
    /// totals (the swept list is capped — a census must read these, not count rows).</summary>
    public sealed class DamageResult
    {
        public readonly List<DamageRow> Rows = new();
        public string Text = "";
        public string Summary = "";
        public string CollidersText = "";
        public int CollidableMeshes;
        public int TotalInstances;
        public int DistinctAnchors;
        public bool Capped;
        public bool Ok => Rows.Count > 0 && Rows.All(r => r.Resolved);
    }

    /// <summary>One flight scenario: what the model does, and what the original did.
    ///
    /// <para><see cref="Measured"/> is the original's own value, decoded from cockpit-gauge video
    /// (see <c>analysis/video-flight-calibration/</c>) — a golden number, not a guess. A row with no
    /// <see cref="Measured"/> value, or one flagged <see cref="Informational"/>, is reported but not
    /// asserted: either nothing was measured to compare against, or the comparison is a known open
    /// gap that must not gate a build until it is scoped.</para></summary>
    public sealed class FlightRow
    {
        public string Name = "";
        public string What = "";
        public string Unit = "";
        public double Model;
        public double? Measured;
        public double Tolerance;
        public bool Informational;
        public string Detail = "";

        public bool Asserted => !Informational && Measured != null;
        public bool Ok => !Asserted || Math.Abs(Model - Measured!.Value) <= Tolerance;

        /// <summary>Signed miss against the original, as a percentage — the shape that tells a
        /// scale error (constant %) from drift (sign-random).</summary>
        public double? ErrorPct =>
            Measured is { } msd && msd != 0 ? (Model - msd) / msd * 100.0 : null;
    }

    /// <summary>The flown envelope of one airframe against the original's measured values.</summary>
    public sealed class FlightEnvelopeResult
    {
        public readonly List<FlightRow> Rows = new();
        public string Text = "";
        public string Summary = "";
        public string? Error;

        public int Asserted => Rows.Count(r => r.Asserted);
        public int Failed => Rows.Count(r => !r.Ok);
        public bool Ok => Error == null && Asserted > 0 && Failed == 0;
    }
}

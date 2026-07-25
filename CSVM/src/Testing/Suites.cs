using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The registered <c>--run-tests</c> suites. Each one asserts on a <see cref="Probes"/> verdict or
/// on live engine state; none of them re-implements a check the inspection reports already do.
///
/// <para>Expected counts here are <b>golden numbers measured against the retail install</b> — the
/// data is a fixed input, so 48 weapon defs and 267 C1 destructibles are invariants, not
/// guesses. A suite whose data is absent skips rather than passing.</para>
/// </summary>
public static class Suites
{
    /// <summary>Destructible instances / distinct node groups per chapter, at each chapter's
    /// default mission. Instances exceed node groups where a reader wildcard def and its compiled
    /// per-instance twin bind the same nodes.</summary>
    private static readonly (string Chapter, int Instances, int Anchors)[] Census =
    {
        ("C1", 267, 196),
        ("C1B", 108, 108),
        ("C1C", 107, 107),
        ("C2", 574, 201),
        ("C2B", 104, 104),
        ("C3", 501, 202),
        ("C4", 225, 194),
        ("C5", 568, 292),
    };

    private const int PlayerAirframes = 11;
    private const int WeaponDefCount = 48;

    public static void Register(List<TestHarness.Suite> into)
    {
        into.Add(new TestHarness.Suite("weapons-defs",
            "every weapons.json BALLISTICS entry reads through the typed reader", WeaponsDefs));
        into.Add(new TestHarness.Suite("markers-rig",
            "every player airframe has a firepoint/pylon rig in planes.zbd", MarkersRig));
        into.Add(new TestHarness.Suite("loadout-bind",
            "every stock loadout binds to its model with every marker resolved", LoadoutBind));
        into.Add(new TestHarness.Suite("weapons-fire",
            "all 48 weapons mount and fire from a built plane", WeaponsFire));
        into.Add(new TestHarness.Suite("damage-stages",
            "each DAMAGE_SEQUENCE def fires its stage effects across an HP sweep", DamageStages));
        into.Add(new TestHarness.Suite("damage-hd",
            "weapon hits destroy, swap, drop colliders, and survive destroy→reset→destroy", DamageHd));
        into.Add(new TestHarness.Suite("destructible-census",
            "per-chapter destructible registry totals", DestructibleCensus));
    }

    // ---- pure data -----------------------------------------------------------------------------

    private static void WeaponsDefs(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.Weapons(ctx.ZrdrPath, ctx.MessagesPath, "");
        ctx.Check(r.Error == null, $"weapons.json loads error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.Same(WeaponDefCount, r.Total, $"weapon defs");
        ctx.Same(0, r.UnhandledTotal, $"unhandled weapon keys");
        ctx.Check(!string.IsNullOrEmpty(r.EmptyClipSound), $"empty-clip sound resolves value={r.EmptyClipSound ?? "-"}");
        ctx.Note($"{r.Summary}");
    }

    private static void MarkersRig(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var r = Probes.Markers(ctx.PlanesGamezPath, "");
        ctx.Check(r.Error == null, $"planes gamez loads error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.Same(PlayerAirframes, r.Requested, $"known player airframes");
        ctx.Same(PlayerAirframes, r.Done, $"airframes with a marker rig");
        ctx.Check(r.Missing.Count == 0, $"no airframe missing its root node missing={string.Join(",", r.Missing)}");
    }

    private static void LoadoutBind(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.Loadouts(ctx.ZrdrPath, ctx.MessagesPath, ctx.PlanesGamezPath, ctx.DataRoot,
            "", ctx.LoadoutOverride);
        ctx.Check(r.Error == null, $"loadout inputs load error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        if (ctx.LoadoutOverride != null)
        {
            ctx.Note($"cross-binding every plane to loadout={ctx.LoadoutOverride} — not the stock check");
        }
        ctx.Same(PlayerAirframes, r.Bound, $"stock loadouts bound");
        ctx.Same(0, r.Failed, $"loadout binding failures");
        foreach (string f in r.Failures)
        {
            ctx.Check(false, $"loadout binding {f}");
        }
    }

    // ---- needs a built plane in the tree --------------------------------------------------------

    private static void WeaponsFire(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, Messages.Load(ctx.MessagesPath));
        // The lab holds the archive past construction (it bakes the target material and the
        // impact stand-ins), so it is disposed only after the self-test has run.
        var textures = new TextureArchive(texturesPath);
        Node3D? plane = null;
        UI.WeaponLab? lab = null;
        try
        {
            plane = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ctx.Host.AddChild(plane);
            Loadout? loadout = null;
            foreach (var def in StockLoadouts.Load().All.Values)
            {
                if (def.Model == ctx.PlaneName)
                {
                    loadout = Loadout.Bind(def, plane, weapons);
                    break;
                }
            }
            ctx.Check(loadout != null, $"stock loadout found for plane={ctx.PlaneName}");
            lab = new UI.WeaponLab(plane, weapons, loadout, textures, ctx.Camera, ctx.PlaneName);
            ctx.Host.AddChild(lab);
            var result = lab.SelfTest();
            ctx.Same(WeaponDefCount, result.Total, $"weapons offered to the self-test");
            ctx.Same(WeaponDefCount, result.Ok, $"weapons that mounted and fired");
            ctx.Same(0, result.Errors, $"weapons that threw");
            // A skip is a success-looking outcome in the report — a weapon with no mount on this
            // plane never fires, and nothing else would notice.
            ctx.Same(0, result.Skipped, $"weapons with no mount");
        }
        finally
        {
            lab?.Free();
            plane?.Free();
            textures.Dispose();
        }
    }

    // ---- needs a chapter world ------------------------------------------------------------------

    private static void DamageStages(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var r = Probes.Damage(world.Runtime, ctx.Chapter, "", damageHd: 0f);
            ctx.WriteArtifact($"test-damage-stages-{ctx.Chapter}.txt", r.Text);
            ctx.Check(r.Rows.Count > 0, $"chapter has DAMAGE_SEQUENCE defs chapter={ctx.Chapter} rows={r.Rows.Count}");
            foreach (var row in r.Rows)
            {
                ctx.Check(row.Resolved, $"deep descendant resolves back to its destructible def={row.Def}");
                ctx.Check(row.StagesFired > 0, $"HP sweep fires a stage effect def={row.Def} stages={row.StagesFired}");
            }
            ctx.Note($"{r.Summary}");
        });
    }

    private static void DamageHd(TestContext ctx)
    {
        // Collision forced on: the collider census measures which destructible geometry is solid
        // and whether the death removes it, and a world built without collision censuses zero.
        ctx.WithWorld(ctx.Chapter, collision: true, world =>
        {
            var r = Probes.Damage(world.Runtime, ctx.Chapter, "", damageHd: 25f);
            ctx.WriteArtifact($"test-damage-hd-{ctx.Chapter}.txt", r.Text);
            ctx.Check(r.Rows.Count > 0, $"chapter has destructibles chapter={ctx.Chapter} rows={r.Rows.Count}");
            int destroyed = 0;
            foreach (var row in r.Rows)
            {
                ctx.Check(row.Resolved, $"deep descendant resolves back to its destructible def={row.Def}");
                ctx.Check(row.Destroyed, $"enough weapon hits destroy it def={row.Def} hits={row.Hits}");
                if (!row.Destroyed)
                {
                    continue;
                }
                destroyed++;
                ctx.Check(row.ResetHealthy == true, $"reset restores it def={row.Def}");
                ctx.Check(row.RekillMatched == true, $"rekill takes the same hits def={row.Def} hits={row.Hits}");
            }
            ctx.Same(r.Rows.Count, destroyed, $"destructibles destroyed by weapon hits");
            ctx.Note($"{r.Summary}");
            ctx.Note($"colliders world={r.CollidableMeshes} swept={r.Rows.Count} capped={r.Capped}");
        });
    }

    private static void DestructibleCensus(TestContext ctx)
    {
        foreach (var (chapter, instances, anchors) in Census)
        {
            ctx.WithWorld(chapter, collision: false, world =>
            {
                // The registry totals, never the swept rows — the sweep is capped at
                // Probes.SweepCap and would silently under-count.
                var registry = world.Runtime.Destructibles;
                ctx.Same(instances, registry.Count, $"{chapter} destructible instances");
                ctx.Same(anchors, registry.DistinctAnchors, $"{chapter} destructible node groups");
            });
        }
    }
}

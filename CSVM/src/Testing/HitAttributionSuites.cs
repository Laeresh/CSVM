using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>Which pool a weapon hit on C3's suspension bridge and guard towers reaches. The
/// original answers a hit from the struck node's own handler slot. Load stamps that slot over the
/// animation root's subtree alone (docs/formats/destructibles.md, "Which node takes the hit").</summary>
internal static class HitAttributionSuites
{
    private const string BridgeDef = "susp_bridge";
    private const string BridgeRoot = "rope1";
    private const string BridgeBase = "dbase";
    private const string TowerPrefix = "g_tower";

    // How far above a tower's drawn box the plumb ray starts. The tallest tower model is 11.6 m,
    // so 60 m starts every ray well clear of the tower's own top.
    private const float StandOffM = 60f;

    // How far out along X the rays at dbase start. The bridge group is 17 m wide across X.
    private const float BridgeStandOffM = 50f;

    /// <summary>C3 over its BUILT, collidable world. In C3/M01 only colliders under the bridge's
    /// animation root answer for its pool. A ray across the bridge strikes dbase, which answers for
    /// none. That mission's boot script switches the guard towers' area off; in C3/M02 a ray reaches
    /// each.</summary>
    [Suite("c3-hit-attribution",
        "C3/M01's suspension bridge answers a hit only on rope1, its def's animation root; a ray across the bridge strikes dbase, which answers for no pool; the g_tower guard towers answer only on their own standing model, which C3/M01's area switch hides and C3/M02 leaves standing for a ray to reach")]
    internal static void C3HitAttribution(TestContext ctx)
    {
        var report = new StringBuilder();
        ctx.WithWorld("C3", collision: true, mission: "M01", world =>
        {
            ctx.SyncPhysics();
            report.AppendLine("M01");
            Bridge(ctx, world.Runtime, report);
            Towers(ctx, world.Runtime, standing: false, report);
        });
        ctx.WithWorld("C3", collision: true, mission: "M02", world =>
        {
            ctx.SyncPhysics();
            report.AppendLine("M02");
            Towers(ctx, world.Runtime, standing: true, report);
        });
        ctx.WriteArtifact("test-c3-hit-attribution.txt", report.ToString());
    }

    private static void Bridge(TestContext ctx, AnimRuntime runtime, StringBuilder report)
    {
        var bridge = runtime.Destructibles.All.FirstOrDefault(i =>
            i.Def.Name.Equals(BridgeDef, StringComparison.OrdinalIgnoreCase) && i.Def.Archive != null);
        ctx.Check(bridge != null, $"C3/M01 ships the compiled {BridgeDef} pool");
        if (bridge == null)
        {
            return;
        }

        var pool = PoolOf(runtime, bridge);
        var root = pool.DamageNode;
        ctx.Check(AnimRuntime.NameOf(root) == BridgeRoot,
            $"the bridge's damage node is {BridgeRoot} (got {AnimRuntime.NameOf(root)})");
        int onRoot = 0, rootAnswers = 0, elsewhereAnswers = 0;
        foreach (var body in Bodies(bridge.Anchor))
        {
            bool underRoot = body == root || root.IsAncestorOf(body);
            bool answers = ReferenceEquals(runtime.Destructibles.Resolve(body), pool);
            report.AppendLine(Invariant($"bridge {Path(body, bridge.Anchor)}: answers={answers}"));
            onRoot += underRoot ? 1 : 0;
            rootAnswers += underRoot && answers ? 1 : 0;
            elsewhereAnswers += !underRoot && answers ? 1 : 0;
        }

        ctx.Check(onRoot > 0 && rootAnswers == onRoot,
            $"every collider under {BridgeRoot} answers for the bridge ({rootAnswers} of {onRoot})");
        ctx.Same(0, elsewhereAnswers, $"no collider outside {BridgeRoot} answers for the bridge");
        BaseStopsTheRound(ctx, runtime, root, report);
    }

    // Rays along X through rope1's origin, from either side: the cast the issue measured, which met
    // dbase about 7.7 m out before reaching the rope.
    private static void BaseStopsTheRound(TestContext ctx, AnimRuntime runtime, Node3D rope, StringBuilder report)
    {
        if (rope.GetWorld3D()?.DirectSpaceState is not { } space)
        {
            ctx.Check(false, $"the bridge stands in a physics space");
            return;
        }

        int onBase = 0, baseAnswers = 0;
        var at = rope.GlobalPosition;
        foreach (var side in new[] { Vector3.Right, Vector3.Left })
        {
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(at + (side * BridgeStandOffM), at));
            var struck = hit.Count > 0 ? hit["collider"].As<Node>() : null;
            string piece = struck == null ? "nothing" : PieceOf(struck);
            var resolved = struck == null ? null : runtime.Destructibles.Resolve(struck);
            report.AppendLine(Invariant($"ray from {(side.X > 0 ? "+X" : "-X")} -> {piece} -> {resolved?.Def.Name ?? "no pool"}"));
            onBase += piece == BridgeBase ? 1 : 0;
            baseAnswers += piece == BridgeBase && resolved != null ? 1 : 0;
        }

        ctx.Same(2, onBase, $"a ray across the standing bridge from either side strikes {BridgeBase} first");
        ctx.Same(0, baseAnswers, $"and {BridgeBase} answers for no pool");
    }

    // Every compiled g_tower pool answers for its standing model's colliders. A plumb ray reaches
    // one exactly when the mission leaves the tower's area switched on.
    private static void Towers(TestContext ctx, AnimRuntime runtime, bool standing, StringBuilder report)
    {
        var towers = runtime.Destructibles.All
            .Where(i => i.Def.Archive != null && i.Def.Name.StartsWith(TowerPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        ctx.Check(towers.Count > 0, $"C3 ships compiled g_tower pools ({towers.Count})");
        int answering = 0, standingCount = 0, reached = 0;
        foreach (var tower in towers)
        {
            var pool = PoolOf(runtime, tower);
            var bodies = Bodies(tower.DamageNode).ToList();
            bool answers = bodies.Count > 0 && bodies.All(b => ReferenceEquals(runtime.Destructibles.Resolve(b), pool));
            string hiddenBy = string.Join(",", Climb(tower.DamageNode).OfType<Node3D>().Where(n => !n.Visible).Select(n => n.Name.ToString()));
            var (struck, hit) = Plumb(runtime, tower, pool);
            report.AppendLine(Invariant($"{tower.Def.Name}: {bodies.Count} colliders under {AnimRuntime.NameOf(tower.DamageNode)}, answer={answers}, hidden by [{hiddenBy}], from above -> {struck}"));
            answering += answers ? 1 : 0;
            standingCount += tower.DamageNode.IsVisibleInTree() ? 1 : 0;
            reached += hit ? 1 : 0;
        }

        ctx.Same(towers.Count, answering, $"every tower's standing model carries colliders its own pool answers for");
        ctx.Same(standing ? towers.Count : 0, standingCount, $"the mission's boot script leaves {(standing ? "every" : "no")} tower standing");
        ctx.Same(standing ? towers.Count : 0, reached, $"and a ray from above reaches {(standing ? "every" : "no")} tower's own pool");
    }

    // A ray down through the centre of the tower's drawn box: the first collider it meets, and
    // whether that collider answers for the tower's pool.
    private static (string What, bool Hit) Plumb(AnimRuntime runtime, DestructibleRegistry.Instance tower,
        DestructibleRegistry.Instance pool)
    {
        var boxes = tower.DamageNode.FindChildren("*", "VisualInstance3D", true, false).OfType<VisualInstance3D>()
            .Select(v => v.GlobalTransform * v.GetAabb()).ToList();
        if (boxes.Count == 0 || tower.Anchor.GetWorld3D()?.DirectSpaceState is not { } space)
        {
            return ("no mesh", false);
        }

        var box = boxes.Aggregate((a, b) => a.Merge(b));
        var mid = box.GetCenter();
        var from = new Vector3(mid.X, box.End.Y + StandOffM, mid.Z);
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, new Vector3(mid.X, box.Position.Y - 1f, mid.Z)));
        if (hit.Count == 0 || hit["collider"].As<Node>() is not { } struck)
        {
            return ("nothing", false);
        }

        var resolved = runtime.Destructibles.Resolve(struck);
        return (Invariant($"{PieceOf(struck)}/{struck.Name} -> {resolved?.Def.Name ?? "no pool"}"), ReferenceEquals(resolved, pool));
    }

    // Resolve's answers are compared against this, so it must be Resolve's pick. A reader def and its
    // compiled twin can both bind one tower, and only the compiled one answers a hit.
    private static DestructibleRegistry.Instance PoolOf(AnimRuntime runtime, DestructibleRegistry.Instance inst) =>
        runtime.Destructibles.Resolve(inst.DamageNode) ?? inst;

    // The data name of the world node a collider body was carved from.
    private static string PieceOf(Node body) => body.GetParent() is Node3D p ? AnimRuntime.NameOf(p) : "?";

    private static IEnumerable<CollisionObject3D> Bodies(Node root) =>
        root.FindChildren("*", "CollisionObject3D", true, false).OfType<CollisionObject3D>();

    private static IEnumerable<Node> Climb(Node node)
    {
        for (var n = node; n != null; n = n.GetParent())
        {
            yield return n;
        }
    }

    private static string Path(Node body, Node anchor) =>
        string.Join("/", Climb(body).TakeWhile(n => !ReferenceEquals(n, anchor)).Reverse()
            .Select(n => n is Node3D n3 ? AnimRuntime.NameOf(n3) : n.Name.ToString()));

    private static string Invariant(FormattableString s) => FormattableString.Invariant(s);
}

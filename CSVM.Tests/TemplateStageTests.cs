using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The off-engine charter for <c>src/Mech3/Anim/TemplateStage.cs</c>:
/// slot-cursor wrap, the modulo fallback for callees staged shallower than their
/// caller's slot, <c>Recycles</c> counting in both wrap flavours, the caller-slot
/// stickiness, placement and the move tolerance — asserted against a plain token node type with
/// no Godot engine anywhere in the path (the Godot structs used are pure math). The integration
/// tier stays the in-engine <c>effect-template-mesh</c>/<c>effects-census</c>/
/// <c>damage-template-pool</c> suites; nothing here re-implements those scene/visibility checks.
/// <see cref="TestNode"/> carries no overridden <c>Equals</c>, so identity is reference equality —
/// the same discipline the engine instantiation gets from its instance-id comparer.
/// </summary>
public class TemplateStageTests
{
    // ---- TakeNextSlot: the cursor cycles the staged slots in order and wraps ----

    [Fact]
    public void TakeNextSlotCyclesSlotsInOrderAndWraps()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 3);

        Assert.Same(copies[0], Assert.Single(h.Stage.TakeNextSlot(def)));
        Assert.Same(copies[1], Assert.Single(h.Stage.TakeNextSlot(def)));
        Assert.Same(copies[2], Assert.Single(h.Stage.TakeNextSlot(def)));
        Assert.Same(copies[0], Assert.Single(h.Stage.TakeNextSlot(def))); // the wrap
        Assert.Equal(0, h.Stage.Recycles); // nothing was live — a clean wrap is not a recycle
    }

    [Fact]
    public void TakeNextSlotCountsARecycleOnlyWhenTheWrappedCopyIsStillLive()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 2);

        h.Live.Add((def, copies[0]));
        Assert.Same(copies[0], Assert.Single(h.Stage.TakeNextSlot(def))); // slot 0, live at take
        Assert.Equal(1, h.Stage.Recycles);
        Assert.Same(copies[1], Assert.Single(h.Stage.TakeNextSlot(def))); // slot 1, free
        Assert.Equal(1, h.Stage.Recycles);
        Assert.Same(copies[0], Assert.Single(h.Stage.TakeNextSlot(def))); // slot 0 again, still live
        Assert.Equal(2, h.Stage.Recycles);
        Assert.Single(h.Printed.Where(l => l.Contains("recycled slot"))); // named once per effect
    }

    [Fact]
    public void TakeNextSlotUnpooledOrSingleCopyReturnsEveryAnchor()
    {
        var unpooled = new Harness(pooled: false);
        var (def, _) = unpooled.PooledRoot("boom", slots: 3);
        Assert.Equal(3, unpooled.Stage.TakeNextSlot(def).Count);

        var h = new Harness();
        var (single, one) = h.PooledRoot("flash", slots: 1);
        Assert.Same(one[0], Assert.Single(h.Stage.TakeNextSlot(single)));
        Assert.Equal(0, h.Stage.Recycles);
        Assert.Equal(0, unpooled.Stage.Recycles);
    }

    // ---- RootsFor: a call resolves to the copy in ITS OWN slot ----

    [Fact]
    public void RootsForReturnsTheCallersOwnSlotCopy()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 3);
        var caller = h.NodeInSlot(copies[1]); // anchored inside slot 1's copy

        Assert.Same(copies[1], Assert.Single(h.Stage.RootsFor(def, caller)));
    }

    [Fact]
    public void RootsForFallsBackByModuloWhenTheCalleeIsStagedShallower()
    {
        // A slot-5 caller reaching a callee staged in only 2 slots must get ONE copy (5 % 2 = 1),
        // never "all of them" — every branch returning one call's copies is what ended the
        // shared-template collapse.
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 2);
        var deepContainer = h.SlotContainer(5);
        var caller = h.NodeUnder(deepContainer);

        Assert.Same(copies[1], Assert.Single(h.Stage.RootsFor(def, caller)));
    }

    [Fact]
    public void RootsForOffPoolAnchorWithNoClaimReturnsEveryCopy()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 2);
        var stray = h.Node("pdp1"); // no slot ancestry, no claim

        Assert.Equal(2, h.Stage.RootsFor(def, stray).Count);
    }

    // ---- the caller-slot claim: sticky per (root, anchor) ----

    [Fact]
    public void CallerSlotClaimIsStickyPerAnchor()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("planeflakes", slots: 2);
        var panel1 = h.Node("pdp1");
        var panel2 = h.Node("pdp2");

        h.Stage.AssignCallerSlot(def, panel1);
        h.Stage.AssignCallerSlot(def, panel2);
        Assert.Same(copies[0], Assert.Single(h.Stage.RootsFor(def, panel1)));
        Assert.Same(copies[1], Assert.Single(h.Stage.RootsFor(def, panel2)));

        // A re-call from the same anchor restarts ITS OWN copy, never a sibling's.
        h.Stage.AssignCallerSlot(def, panel1);
        Assert.Same(copies[0], Assert.Single(h.Stage.RootsFor(def, panel1)));
    }

    [Fact]
    public void ASecondCallSiteOnOneAnchorTakesItsOwnCopy()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("planeflakes", slots: 3);
        var panel = h.Node("pdp7");
        object first = new(), second = new(), third = new();

        // The anchor's own first site keeps the sticky (root, anchor) slot and starts on the call
        // anchor, so it claims no copy of its own; each later site does.
        Assert.Null(h.Stage.AssignCallerSlot(def, panel, first));
        Assert.Same(copies[1], h.Stage.AssignCallerSlot(def, panel, second));
        Assert.Same(copies[2], h.Stage.AssignCallerSlot(def, panel, third));
        Assert.Same(copies[0], Assert.Single(h.Stage.RootsFor(def, panel)));

        // Sticky per site as well as per anchor: a re-call answers with the copy it already holds.
        Assert.Null(h.Stage.AssignCallerSlot(def, panel, first));
        Assert.Same(copies[1], h.Stage.AssignCallerSlot(def, panel, second));
        Assert.Equal(0, h.Stage.Recycles);
    }

    [Fact]
    public void CallerSlotsBeyondTheStagedCopiesWrapAndCountARecycle()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("planeflakes", slots: 2);
        h.Stage.AssignCallerSlot(def, h.Node("pdp1"));
        h.Stage.AssignCallerSlot(def, h.Node("pdp2"));
        Assert.Equal(0, h.Stage.Recycles);

        var panel3 = h.Node("pdp3");
        h.Stage.AssignCallerSlot(def, panel3); // claim 2 over 2 copies — the wrap
        Assert.Equal(1, h.Stage.Recycles);
        Assert.Single(h.Printed.Where(l => l.Contains("wrapped")));
        // The claim wraps through RootsFor's modulo back onto slot 0's copy.
        Assert.Same(copies[0], Assert.Single(h.Stage.RootsFor(def, panel3)));
    }

    [Fact]
    public void CallerSlotClaimNoOpsOffPoolSingleCopyAndInSlotAnchors()
    {
        var unpooled = new Harness(pooled: false);
        var (offPool, _) = unpooled.PooledRoot("planeflakes", slots: 2);
        unpooled.Stage.AssignCallerSlot(offPool, unpooled.Node("pdp1")); // off the pool: no claim
        Assert.Equal(2, unpooled.Stage.RootsFor(offPool, unpooled.Node("pdp1")).Count);

        var h = new Harness();
        var (def, copies) = h.PooledRoot("planeflakes", slots: 2);

        var (single, _) = h.PooledRoot("gimmeflakes", slots: 1);
        h.Stage.AssignCallerSlot(single, h.Node("pdp2")); // single copy: no claim

        var inSlot = h.NodeInSlot(copies[0]);
        h.Stage.AssignCallerSlot(def, inSlot); // already in a slot: no claim

        Assert.Equal(2, h.Stage.RootsFor(def, h.Node("pdp1")).Count); // nothing claimed anywhere
        Assert.Equal(0, h.Stage.Recycles);
    }

    [Fact]
    public void CallerSlotDebugLineNamesTheClaim()
    {
        var h = new Harness { Debug = true };
        var (def, _) = h.PooledRoot("planeflakes", slots: 2);
        h.Stage.AssignCallerSlot(def, h.Node("pdp1"));

        var line = Assert.Single(h.Printed);
        Assert.Contains("caller slot 0 of 'planeflakes'", line);
        Assert.Contains("'pdp1'", line);
    }

    // ---- placement + the one named move tolerance ----

    [Fact]
    public void PlaceOnSetsOriginAndLevelsOnlyWhenAsked()
    {
        var h = new Harness();
        var root = h.Node("he_ring1");
        var tilted = new Basis(Vector3.Up, Mathf.DegToRad(45f));
        root.Xf = new Transform3D(tilted, new Vector3(1, 2, 3));

        h.Stage.PlaceOn(new[] { root }, new Vector3(10, 0, -5));
        Assert.True(root.Placed);
        Assert.Equal(new Vector3(10, 0, -5), root.Xf.Origin);
        Assert.True(root.Xf.Basis.IsEqualApprox(tilted)); // un-leveled placement keeps the basis

        h.Stage.PlaceOn(new[] { root }, new Vector3(20, 0, 0), level: true);
        Assert.True(root.Xf.Basis.IsEqualApprox(Basis.Identity)); // level:true squares the basis to identity
    }

    [Fact]
    public void IsAtHonoursTheOneMoveTolerance()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 1);
        var site = h.Node("dbase");
        site.Xf = new Transform3D(Basis.Identity, new Vector3(100, 0, 100));
        h.Stage.PlaceOn(new[] { copies[0] }, site.Xf.Origin);

        Assert.True(h.Stage.IsAt(def, site, Vector3.Zero));
        site.Xf = new Transform3D(Basis.Identity, new Vector3(100.4f, 0, 100)); // inside 0.5 m
        Assert.True(h.Stage.IsAt(def, site, Vector3.Zero));
        site.Xf = new Transform3D(Basis.Identity, new Vector3(101, 0, 100)); // a genuine move
        Assert.False(h.Stage.IsAt(def, site, Vector3.Zero));
    }

    // ---- SlotOf memoizes the ancestry walk ----

    [Fact]
    public void SlotOfWalksAncestryOncePerNode()
    {
        var h = new Harness();
        var container = h.SlotContainer(2);
        var node = h.NodeUnder(container);

        Assert.Equal(2, h.Stage.SlotOf(node));
        Assert.Equal(2, h.Stage.SlotOf(node));
        Assert.Equal(1, h.SlotWalks);
    }

    // ---- RootsOf + the shared-root hold ----

    [Fact]
    public void RootsOfIsSlotScopedInsideThePoolAndAnchorWideOutside()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 2);

        Assert.Same(copies[1], Assert.Single(h.Stage.RootsOf(def, h.NodeInSlot(copies[1]))));
        Assert.Equal(2, h.Stage.RootsOf(def, h.Node("stray")).Count);
    }

    [Fact]
    public void SharedWithLiveInstanceSeesAnotherInstanceOnTheSameRootAndSkipsItself()
    {
        var h = new Harness();
        var (defA, copies) = h.PooledRoot("boom", slots: 1);
        var defB = Def("rear_flash_effect");
        h.Roots["rear_flash_effect"] = new List<TestNode> { copies[0] }; // caller and callee share the root

        var roots = new List<TestNode?> { copies[0] };
        h.Instances.Add((defA, copies[0]));
        Assert.False(h.Stage.SharedWithLiveInstance(defA, copies[0], roots)); // its own instance is skipped

        h.Instances.Add((defB, copies[0]));
        Assert.True(h.Stage.SharedWithLiveInstance(defA, copies[0], roots));
    }

    // ---- reveal / retire / sweep: the deferral holds and their drain ----

    [Fact]
    public void RevealTouchesNothingWhenTheStageDoesNotStageItsTemplatesHidden()
    {
        var h = new Harness();
        var (def, copies) = h.PooledRoot("boom", slots: 2);
        h.Live.Add((def, copies[0]));

        h.Stage.Reveal(def, copies[0], visible: true); // Shown is off — the world runtime's shape
        Assert.False(copies[0].Visible);
    }

    [Fact]
    public void RevealLightsOnlyTheCallsOwnSlotCopy()
    {
        var h = new Harness(shown: true);
        var (def, copies) = h.PooledRoot("boom", slots: 2);
        h.Live.Add((def, copies[0]));

        h.Stage.Reveal(def, copies[0], visible: true);
        Assert.True(copies[0].Visible);
        Assert.False(copies[1].Visible); // a sibling blast still burning is not blanked
    }

    [Fact]
    public void RevealingADefThatIsAlreadyFinishedSchedulesItsOwnHide()
    {
        // `biggun_flying_parts`: one CALL_ANIMATION, finished inside Start, so it never reaches the
        // retire walk — the reveal must schedule the hide itself or the copy stays lit forever.
        var h = new Harness(shown: true);
        var (def, copies) = h.PooledRoot("biggun_flying_parts", slots: 1);

        h.Stage.Reveal(def, copies[0], visible: true); // nothing live, nothing animating
        Assert.False(copies[0].Visible);
    }

    [Fact]
    public void RetireWhenIdleHoldsTheCopyLitWhileSomethingStillAnimatesItAndTheSweepDrainsIt()
    {
        var h = new Harness(shown: true);
        var (def, copies) = h.PooledRoot("he_ring1", slots: 1);
        h.Live.Add((def, copies[0]));
        h.Stage.Reveal(def, copies[0], visible: true);

        // The ring's scale/opacity motions outlive the sequence that launched them.
        h.Animating.Add(copies[0]);
        h.Stage.RetireWhenIdle(def, copies[0]);
        Assert.True(copies[0].Visible);
        h.Stage.Sweep();
        Assert.True(copies[0].Visible); // still held

        h.Animating.Clear();
        h.Stage.Sweep();
        Assert.False(copies[0].Visible); // the deferral drains
        h.Stage.Sweep();
        Assert.False(copies[0].Visible); // and the entry is gone, not re-fired
    }

    [Fact]
    public void RetireWhenIdleHoldsForAnotherLiveInstanceOnTheSameRoot()
    {
        // The rear muzzle flash's shape: caller and callee share one root, so hiding on the
        // caller's finish would blank the flash the callee just revealed there.
        var h = new Harness(shown: true);
        var (defA, copies) = h.PooledRoot("rear_flash_control", slots: 1);
        var defB = Def("rear_flash_effect");
        h.Roots["rear_flash_effect"] = new List<TestNode> { copies[0] };
        h.Live.Add((defA, copies[0]));
        h.Stage.Reveal(defA, copies[0], visible: true);

        h.Instances.Add((defB, copies[0]));
        h.Stage.RetireWhenIdle(defA, copies[0]);
        Assert.True(copies[0].Visible);

        h.Instances.Clear();
        h.Stage.Sweep();
        Assert.False(copies[0].Visible);
    }

    [Fact]
    public void ARevealSettlesAPendingHideSoAReplayIsNotSweptDark()
    {
        var h = new Harness(shown: true);
        var (def, copies) = h.PooledRoot("he_ring1", slots: 1);
        h.Live.Add((def, copies[0]));
        h.Animating.Add(copies[0]);
        h.Stage.RetireWhenIdle(def, copies[0]); // deferred: this effect is over
        h.Animating.Clear();

        h.Stage.Reveal(def, copies[0], visible: true); // …but the copy is replayed first
        h.Stage.Sweep();
        Assert.True(copies[0].Visible); // the stale hide was about an effect that is over
    }

    // ---- place-exempt names: an airframe-scoped NAME is never a movable template ----

    [Fact]
    public void PlaceAtNeverMovesAPlaceExemptCallee()
    {
        // The Devastator trap: the crash defs' authored NAME (player_pfighter) resolves to the
        // aircraft's own model root on that one airframe. Placing it would TopLevel-pin the plane
        // at the call site while the FlightController flies on without it.
        var h = new Harness(placeExempt: new[] { "player_pfighter" });
        var model = h.Node("player_pfighter");
        h.Roots["player_pfighter"] = new List<TestNode> { model };
        var site = h.Node("pdp5");
        site.Xf = new Transform3D(Basis.Identity, new Vector3(50, 0, 0));

        h.Stage.PlaceAt(Def("player_pfighter"), site, Vector3.Zero);

        Assert.False(model.Placed);
        // Never moved → never "moved away": a poll-idiom re-call must not restart it while live.
        Assert.True(h.Stage.IsAt(Def("player_pfighter"), site, Vector3.Zero));
    }

    [Fact]
    public void AssignCallerSlotSkipsAPlaceExemptCallee()
    {
        var h = new Harness(placeExempt: new[] { "player_pfighter" });
        var (def, _) = h.PooledRoot("player_pfighter", slots: 2);
        var panel = h.Node("pdp1");

        h.Stage.AssignCallerSlot(def, panel);

        // No claim was made, so resolution stays def-wide — the exempt family's node ops keep
        // reaching everything the NAME resolves, exactly as authored.
        Assert.Equal(2, h.Stage.RootsFor(def, panel).Count);
    }

    // ---- the reveal ritual only writes on staged pool copies ----

    [Fact]
    public void RevealOnAPooledStageTouchesOnlySlotCopies()
    {
        // Retirement runs the hide half for EVERY ended def, template or not. A def whose
        // resolved roots include a live scene node outside the pool (the crash rig's airframe
        // model, its `player` scaffold, the wreck) must not have it blanked or lit by the ritual.
        var h = new Harness(shown: true);
        var copy = h.NodeUnder(h.SlotContainer(0));
        var model = h.Node("player_pfighter");
        model.Visible = true;
        h.Roots["boom"] = new List<TestNode> { copy, model };
        var def = Def("boom");
        var panel = h.Node("pdp1");
        h.Live.Add((def, panel));

        h.Stage.Reveal(def, panel, visible: true);
        Assert.True(copy.Visible);

        h.Stage.Reveal(def, panel, visible: false);
        Assert.False(copy.Visible);
        Assert.True(model.Visible);
    }

    private static AnimDefinition Def(string name) => new() { Name = name };

    // The token adapter: reference-equality nodes, a parent-chain slot walk, transform
    // reads/writes on plain fields, prints collected, and the runtime hooks backed by
    // dictionaries — each the one-line stand-in for the engine adapter's one-line hook.
    private sealed class Harness
    {
        public readonly Dictionary<string, List<TestNode>> Roots = new(StringComparer.OrdinalIgnoreCase);

        public readonly HashSet<(AnimDefinition Def, TestNode? Anchor)> Live = new();

        public readonly List<(AnimDefinition Def, TestNode? Anchor)> Instances = new();

        public readonly HashSet<TestNode> Animating = new();

        public readonly List<string> Printed = new();

        public int SlotWalks;

        public bool Debug;

        /// <summary>The stage's three policy flags are sealed at construction,
        /// so a harness picks the role it is asserting rather than flipping a property
        /// mid-test — the same shape production has, where <c>WorldEffectsFactory</c> builds a
        /// sealed stage and hands it to the runtime.</summary>
        public Harness(bool pooled = true, bool shown = false,
            IEnumerable<string>? placeExempt = null)
        {
            Stage = new TemplateStage<TestNode>(
                EqualityComparer<TestNode>.Default,
                n =>
                {
                    SlotWalks++;
                    for (var p = n; p != null; p = p.Parent)
                        if (p.SlotMark >= 0)
                            return p.SlotMark;
                    return -1;
                },
                n => n != null && n.Valid,
                n => n.Xf,
                (n, xf) =>
                {
                    n.Placed = true;
                    n.Xf = xf;
                },
                (n, visible) => n.Visible = visible,
                Printed.Add,
                () => Debug,
                pooled,
                shown,
                placesCalled: false,
                placeExempt: placeExempt);
            Stage.Wire(
                (name, _) => Roots.TryGetValue(name, out var r) ? new List<TestNode>(r) : new List<TestNode>(),
                def => (Roots.TryGetValue(def.Name, out var r) ? r : new List<TestNode>()).Cast<TestNode?>().ToList(),
                (def, anchor) => anchor != null && Live.Contains((def, anchor)),
                () => Instances.Select(i => (i.Def, i.Anchor)),
                _ => false,
                roots => roots.Any(r => r != null && Animating.Contains(r)),
                n => n.Label,
                _ => { },
                () => { },
                _ => { });
        }

        public TemplateStage<TestNode> Stage { get; }

        public TestNode Node(string label) => new() { Label = label };

        public TestNode SlotContainer(int slot) => new() { Label = $"pool{slot}", SlotMark = slot };

        public TestNode NodeUnder(TestNode parent) => new() { Label = parent.Label + "_child", Parent = parent };

        public TestNode NodeInSlot(TestNode copy) => NodeUnder(copy);

        /// <summary>Stages <paramref name="slots"/> copies of one template root, each under its own
        /// slot container — the shape WorldEffectsFactory builds.</summary>
        public (AnimDefinition Def, List<TestNode> Copies) PooledRoot(string name, int slots)
        {
            var copies = new List<TestNode>();
            for (int slot = 0; slot < slots; slot++)
                copies.Add(NodeUnder(SlotContainer(slot)));
            foreach (var (copy, i) in copies.Select((c, i) => (c, i)))
                copy.Label = $"{name}#{i}";
            Roots[name] = copies;
            return (Def(name), copies);
        }
    }

    private sealed class TestNode
    {
        public string Label = "";

        public TestNode? Parent;

        public int SlotMark = -1;

        public bool Valid = true;

        public Transform3D Xf = Transform3D.Identity;

        public bool Placed;

        public bool Visible;
    }
}

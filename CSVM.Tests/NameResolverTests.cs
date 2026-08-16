using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The off-engine charter for <c>src/Mech3/Anim/NameResolver.cs</c>: the wildcard matcher, the
/// memoized <c>FindAll</c>, the three-tier scope chain, the symbol-table authority and the
/// anchoring rules, asserted against a plain token node type — no Godot, no chapter world.
/// ⚠ <see cref="TestNode"/> deliberately has no overridden <c>Equals</c>, so a generic resolver
/// cannot be shown to inherit a node type's own equality. Fixture shapes mirror the cases named
/// in the resolver's own doc comments; the values are invented.
/// </summary>
public class NameResolverTests
{
    // ---- '#' matches a run of digits, including zero ----

    [Fact]
    public void HashMatchesDigitRunIncludingZero()
    {
        var bare = Node("air_gen");
        var numbered = Node("air_gen12");
        var lettered = Node("air_genx");
        var resolver = Build(
            (bare, "air_gen", null),
            (numbered, "air_gen12", null),
            (lettered, "air_genx", null));

        var found = resolver.FindAll("air_gen#", null);

        Assert.Contains(bare, found);
        Assert.Contains(numbered, found);
        Assert.DoesNotContain(lettered, found);
    }

    // ---- '*' matches any run of characters, including the '**' template idiom ----

    [Fact]
    public void StarMatchesAnyRunOfCharacters()
    {
        var t1 = Node("fly_trail1");
        var t10 = Node("fly_trail10"); // two-char run: '*' must not quietly mean "one character"
        var unrelated = Node("healthy");
        var resolver = Build(
            (t1, "fly_trail1", null), (t10, "fly_trail10", null), (unrelated, "healthy", null));

        var found = resolver.FindAll("fly_trail*", null);

        Assert.Contains(t1, found);
        Assert.Contains(t10, found);
        Assert.DoesNotContain(unrelated, found);
    }

    [Fact]
    public void DoubleStarTemplateFormMatchesAnyRun()
    {
        var node = Node("call_hetrails_up");
        var resolver = Build((node, "call_hetrails_up", null));

        Assert.Contains(node, resolver.FindAll("call_**", null));
    }

    // ---- plain names compare case-insensitively ----

    [Fact]
    public void MatchIsCaseInsensitive()
    {
        var node = Node("Air_Gen");
        var resolver = Build((node, "Air_Gen", null));

        Assert.Contains(node, resolver.FindAll("air_gen", null));
        Assert.Contains(node, resolver.FindAll("AIR_GEN", null));
    }

    // ---- a '.flt' model-file suffix matches the pattern with or without it ----

    [Fact]
    public void FltSuffixMatchesPatternWithOrWithoutSuffix()
    {
        var node = Node("ap_radiotwr.flt");
        var resolver = Build((node, "ap_radiotwr.flt", null));

        Assert.Contains(node, resolver.FindAll("ap_radiotwr", null));
        Assert.Contains(node, resolver.FindAll("ap_radiotwr.flt", null));
    }

    // ---- scope restriction reads the Add-time parent snapshot, not a live tree ----

    [Fact]
    public void ScopeRestrictsToItsOwnSubtreeWhenTwoTemplatesShareAChildName()
    {
        // he_trails/ap_trails: two staged effect templates whose fly_trail1 children share a name
        // (docs/architecture.md's AnimRuntime entry) — scoping must tell the copies apart.
        var heTrails = Node("he_trails");
        var heTrail1 = Node("fly_trail1");
        var apTrails = Node("ap_trails");
        var apTrail1 = Node("fly_trail1");
        var resolver = Build(
            (heTrails, "he_trails", null),
            (heTrail1, "fly_trail1", heTrails),
            (apTrails, "ap_trails", null),
            (apTrail1, "fly_trail1", apTrails));

        Assert.Equal(new[] { heTrail1 }, resolver.FindAll("fly_trail1", heTrails));
        Assert.Equal(new[] { apTrail1 }, resolver.FindAll("fly_trail1", apTrails));

        var global = resolver.FindAll("fly_trail1", null);
        Assert.Equal(2, global.Count);
        Assert.Contains(heTrail1, global);
        Assert.Contains(apTrail1, global);
    }

    [Fact]
    public void ScopeIncludesTheScopeNodeItself()
    {
        var root = Node("m_build01");
        var resolver = Build((root, "m_build01", null));

        Assert.Equal(new[] { root }, resolver.FindAll("m_build*", root));
    }

    // ---- FindAll's memoized result is the same list instance on repeat, read-only-list semantics ----

    [Fact]
    public void FindAllMemoizesTheSameListInstanceForARepeatQuery()
    {
        var node = Node("healthy");
        var resolver = Build((node, "healthy", null));

        var first = resolver.FindAll("healthy", null);
        var second = resolver.FindAll("healthy", null);

        Assert.Same(first, second);
    }

    [Fact]
    public void ClearFindCacheRevisitsAPatternAddedAfterTheFirstQuery()
    {
        var resolver = new NameResolver<TestNode>();

        Assert.Empty(resolver.FindAll("late_arrival", null));

        resolver.Add(Node("late_arrival"), "late_arrival", null);
        Assert.Empty(resolver.FindAll("late_arrival", null)); // still the stale memoized miss

        resolver.ClearFindCache();
        Assert.Single(resolver.FindAll("late_arrival", null));
    }

    // ---- a NAME path walks one scoped segment at a time (through the public tier surface:
    // ResolvePath itself is private, which is what makes the tier order structural) ----

    [Fact]
    public void PathResolutionWalksEachSegmentInsideThePreviousMatches()
    {
        var root = Node("m_build01");
        var door = Node("door1");
        var hinge = Node("hinge1");
        var resolver = Build(
            (root, "m_build01", null),
            (door, "door1", root),
            (hinge, "hinge1", door));

        var found = resolver.ResolveScoped(
            new List<string> { "m_build01", "door1", "hinge1" }, Def("bld"), null);

        Assert.Equal(new[] { hinge }, found);
    }

    // ---- the three-tier scope order: anchor subtree, own template roots, global ----

    [Fact]
    public void AnchorSubtreeWinsOverOwnRootAndGlobal()
    {
        // fly_trail1 exists under the anchor, under the def's own staged root AND as a stray
        // global copy: the anchor's copy wins outright, and the own-root hook is never consulted.
        var anchor = Node("he_ring");
        var mine = Node("fly_trail1");
        var ownRoot = Node("he_trails");
        var ownTrail = Node("fly_trail1");
        var stray = Node("fly_trail1");
        int hookCalls = 0;
        var resolver = new NameResolver<TestNode>(
            ownRootsOf: (_, _) =>
            {
                hookCalls++;
                return new[] { ownRoot };
            });
        resolver.Add(anchor, "he_ring", null);
        resolver.Add(mine, "fly_trail1", anchor);
        resolver.Add(ownRoot, "he_trails", null);
        resolver.Add(ownTrail, "fly_trail1", ownRoot);
        resolver.Add(stray, "fly_trail1", null);

        var found = resolver.ResolveScoped(new List<string> { "fly_trail1" }, Def("he_trails"), anchor);

        Assert.Equal(new[] { mine }, found);
        Assert.Equal(0, hookCalls); // the own-root tier is consulted only on an anchor miss
    }

    [Fact]
    public void OwnRootTierIsConsultedOnAnchorMissBeforeGlobal()
    {
        // The anchor subtree misses; the def's own placed copy must win over the stray global
        // match (the shape real chapter data produces: a copy parked at the stage origin).
        var anchor = Node("he_ring");
        var ownRoot = Node("he_trails");
        var ownTrail = Node("fly_trail1");
        var stray = Node("fly_trail1");
        var resolver = new NameResolver<TestNode>(ownRootsOf: (_, _) => new[] { ownRoot });
        resolver.Add(anchor, "he_ring", null);
        resolver.Add(ownRoot, "he_trails", null);
        resolver.Add(ownTrail, "fly_trail1", ownRoot);
        resolver.Add(stray, "fly_trail1", null);

        var found = resolver.ResolveScoped(new List<string> { "fly_trail1" }, Def("he_trails"), anchor);

        Assert.Equal(new[] { ownTrail }, found);
    }

    [Fact]
    public void GlobalTierIsSkippedUnderLocalNodesOnly()
    {
        // The name resolves only globally: a LOCAL_NODES_ONLY def gets nothing, the same def
        // without the flag falls through to the whole index.
        var anchor = Node("he_ring");
        var stray = Node("fly_trail1");
        var resolver = new NameResolver<TestNode>();
        resolver.Add(anchor, "he_ring", null);
        resolver.Add(stray, "fly_trail1", null);
        var path = new List<string> { "fly_trail1" };

        Assert.Empty(resolver.ResolveScoped(path, Def("he_trails", localNodesOnly: true), anchor));
        Assert.Equal(new[] { stray }, resolver.ResolveScoped(path, Def("he_trails"), anchor));
    }

    [Fact]
    public void DeadAnchorFallsToTheLocalOnlyGlobalResolve()
    {
        // A dead anchor (liveness predicate false) cannot scope anything: the resolve drops to
        // the plain whole-index walk — which also ignores LOCAL_NODES_ONLY, exactly like the
        // null-anchor path it shares.
        var anchor = Node("he_ring");
        var mine = Node("fly_trail1");
        var stray = Node("fly_trail1");
        var resolver = new NameResolver<TestNode>(isLive: n => !ReferenceEquals(n, anchor));
        resolver.Add(anchor, "he_ring", null);
        resolver.Add(mine, "fly_trail1", anchor);
        resolver.Add(stray, "fly_trail1", null);

        var found = resolver.ResolveScoped(
            new List<string> { "fly_trail1" }, Def("he_trails", localNodesOnly: true), anchor);

        Assert.Equal(2, found.Count); // the anchor no longer narrows — both copies, index order
        Assert.Contains(mine, found);
        Assert.Contains(stray, found);
    }

    // ---- symbol authority: an exact gamez-index binding beats ambiguous name matching ----

    [Fact]
    public void SymbolLookupBeatsAmbiguousNameMatch()
    {
        // C1's `caboose` shape: name matching resolves the real consist AND an unrelated
        // `caboose.flt` (the .flt double-match); the compiled symbol table names exactly one.
        var consist = Node("caboose");
        var decoy = Node("caboose.flt");
        var resolver = new NameResolver<TestNode>();
        resolver.Add(consist, "caboose", null, gamezIndex: 7);
        resolver.Add(decoy, "caboose.flt", null, gamezIndex: 8);
        var def = Def("train");
        def.NodeRefs["caboose"] = 7;

        Assert.Equal(2, resolver.FindAll("caboose", null).Count); // the name ambiguity is real
        Assert.True(resolver.SymbolClaims(def, "caboose", out var node));
        Assert.Same(consist, node);
    }

    // ---- twin narrowing: a shared NAME resolves to the instance holding the def's symbol root ----

    [Fact]
    public void TwinNarrowingKeepsTheDefsOwnInstance()
    {
        // C1's two hangars are both `air_gen`; the def whose symbol table roots under eairg31
        // (`air_gen#1`) must anchor on that instance alone, or the pair cross-binds.
        var (resolver, a1, _) = TwinHangars(a1Index: 31, a2Index: 42);
        var def = Def("air_gen", rootName: "air_gen");
        def.NodeRefs["air_gen"] = 31;

        Assert.Equal(new[] { a1 }, resolver.Anchors(def));
    }

    // ---- narrowing is tri-state: undecidable (null) leaves the name match standing ----

    [Fact]
    public void NarrowingLeavesBothTwinsForAReaderDef()
    {
        var (resolver, a1, a2) = TwinHangars(a1Index: 31, a2Index: 42);
        var def = Def("air_gen", rootName: "air_gen"); // reader def: no symbol table

        Assert.Equal(new[] { a1, a2 }, resolver.Anchors(def));
    }

    [Fact]
    public void NarrowingLeavesBothTwinsWhenTheIndexWasNeverBuilt()
    {
        var (resolver, a1, a2) = TwinHangars(a1Index: 31, a2Index: 42);
        var def = Def("air_gen", rootName: "air_gen");
        def.NodeRefs["air_gen"] = 99; // no built node carries this index

        Assert.Equal(new[] { a1, a2 }, resolver.Anchors(def));
    }

    [Fact]
    public void NarrowingLeavesBothTwinsWhenTheRootIsOutsideEveryCandidate()
    {
        var (resolver, a1, a2) = TwinHangars(a1Index: 31, a2Index: 42);
        var foreign = Node("elsewhere");
        resolver.Add(foreign, "elsewhere", null, gamezIndex: 77);
        var def = Def("air_gen", rootName: "elsewhere");
        def.NodeRefs["elsewhere"] = 77;

        Assert.Equal(new[] { a1, a2 }, resolver.Anchors(def));
    }

    // ---- the ANIMATION_ROOT_NAME lift and its policy inputs ----

    [Fact]
    public void RootLiftAnchorsOnTheParentsOfRootMatches()
    {
        var b1 = Node("apbuild01.flt");
        var b2 = Node("apbuild02.flt");
        var resolver = Build(
            (b1, "apbuild01.flt", null), (Node("healthy"), "healthy", b1),
            (b2, "apbuild02.flt", null), (Node("healthy"), "healthy", b2));
        var def = Def("m_build**", rootName: "healthy"); // NAME matches nothing here

        Assert.Equal(new[] { b1, b2 }, resolver.Anchors(def));
    }

    [Fact]
    public void RootLiftRefusesAboveMaxRootLift()
    {
        // Above the cap the root is a generic per-object name ('healthy'): the def belongs to
        // game objects, not world nodes, and must not anchor at all.
        var resolver = new NameResolver<TestNode> { MaxRootLift = 2 };
        for (int i = 0; i < 3; i++)
        {
            var parent = Node($"b{i}");
            resolver.Add(parent, $"b{i}", null);
            resolver.Add(Node("healthy"), "healthy", parent);
        }
        var def = Def("m_build**", rootName: "healthy");

        Assert.Empty(resolver.Anchors(def));
    }

    [Fact]
    public void SuppressRootLiftRefusesEvenUnderTheCap()
    {
        var parent = Node("b0");
        var resolver = new NameResolver<TestNode> { SuppressRootLift = true };
        resolver.Add(parent, "b0", null);
        resolver.Add(Node("healthy"), "healthy", parent);
        var def = Def("m_build**", rootName: "healthy");

        Assert.Empty(resolver.Anchors(def));
    }

    // ---- NameResolveFallback: the by-index map stays empty, name resolution stands alone ----

    [Fact]
    public void NameResolveFallbackLeavesSymbolLookupsEmpty()
    {
        var resolver = new NameResolver<TestNode> { NameResolveFallback = true };
        var trail = Node("fly_trail1");
        resolver.Add(trail, "fly_trail1", null, gamezIndex: 400); // colliding index spaces: never mapped
        var def = Def("he_trails");
        def.NodeRefs["fly_trail1"] = 400;

        Assert.True(resolver.SymbolClaims(def, "fly_trail1", out var bound)); // the symbol still claims it
        Assert.Null(bound);                                                   // but binds to nothing
        Assert.Contains(trail, resolver.FindAll("fly_trail1", null));         // names still resolve
    }

    // ---- multi-target NAME1 defs anchor through their authored paths (M4 F18) ----

    [Fact]
    public void MultiTargetDefAnchorsThroughItsOwnPathsOnly()
    {
        // The zeppelin sub-part shape: piratezep's rtur*/ltur* turrets. The path scoping is
        // what keeps the def off ANOTHER zeppelin's same-named turrets — a bare "ltur*" name
        // match would grab mp1zep's too.
        var piratezep = Node("piratezep");
        var rtur1 = Node("rtur1");
        var ltur1 = Node("ltur1");
        var mp1zep = Node("mp1zep");
        var decoy = Node("ltur1");
        var resolver = Build(
            (piratezep, "piratezep", null),
            (rtur1, "rtur1", piratezep),
            (ltur1, "ltur1", piratezep),
            (mp1zep, "mp1zep", null),
            (decoy, "ltur1", mp1zep));
        var def = Def(""); // NAME1 defs parse with an empty NAME
        def.MultiTargets.Add(("pzrtur*", new[] { "piratezep", "rtur*" }));
        def.MultiTargets.Add(("pzltur*", new[] { "piratezep", "ltur*" }));

        var anchors = resolver.Anchors(def);

        Assert.Equal(2, anchors.Count);
        Assert.Contains(rtur1, anchors);
        Assert.Contains(ltur1, anchors);
        Assert.DoesNotContain(decoy, anchors);
    }

    [Fact]
    public void EmptyNameWithoutTargetsStaysRefusedEvenWithAGenericRoot()
    {
        // The pre-F18 refusal survives for the defs it was written for: an empty-NAME def
        // carrying only a generic ANIMATION_ROOT_NAME must not root-lift onto every instance.
        var parent = Node("b0");
        var resolver = new NameResolver<TestNode>();
        resolver.Add(parent, "b0", null);
        resolver.Add(Node("healthy"), "healthy", parent);

        Assert.Empty(resolver.Anchors(Def("", rootName: "healthy")));
    }

    private static TestNode Node(string label) => new() { Label = label };

    private static AnimDefinition Def(string name, string? rootName = null, bool localNodesOnly = false) =>
        new() { Name = name, RootName = rootName, LocalNodesOnly = localNodesOnly };

    // C1's twin-hangar shape: two instance roots, each owning a child that shares the NAME
    // `air_gen`, distinguishable only by gamez index.
    private static (NameResolver<TestNode> Resolver, TestNode A1, TestNode A2) TwinHangars(
        int a1Index, int a2Index)
    {
        var eairg31 = Node("eairg31");
        var eairg32 = Node("eairg32");
        var a1 = Node("air_gen");
        var a2 = Node("air_gen");
        var resolver = new NameResolver<TestNode>();
        resolver.Add(eairg31, "eairg31", null);
        resolver.Add(a1, "air_gen", eairg31, a1Index);
        resolver.Add(eairg32, "eairg32", null);
        resolver.Add(a2, "air_gen", eairg32, a2Index);
        return (resolver, a1, a2);
    }

    private static NameResolver<TestNode> Build(params (TestNode Node, string Name, TestNode? Parent)[] rows)
    {
        var resolver = new NameResolver<TestNode>();
        foreach (var (node, name, parent) in rows)
        {
            resolver.Add(node, name, parent);
        }
        return resolver;
    }

    private sealed class TestNode
    {
        public string Label = "";
    }
}

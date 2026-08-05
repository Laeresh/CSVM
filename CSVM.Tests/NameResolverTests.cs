using System.Collections.Generic;
using CSVM.Mech3.Anim;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The off-engine charter for <c>src/Mech3/Anim/NameResolver.cs</c>: the wildcard matcher, the
/// memoized <c>FindAll</c>, and <c>ResolvePath</c>, asserted against a plain token node type — no
/// Godot, no chapter world. <see cref="TestNode"/> carries no overridden <c>Equals</c>, so its
/// default identity is reference equality, the same discipline the engine's <c>Node3D</c>
/// instantiation gets from an explicit instance-id comparer (⚠ trap: a generic resolver must not
/// inherit a node type's own equality). Fixture shapes (twin-instance templates sharing a child
/// name, a model-file suffix) mirror the cases named in the resolver's own doc comments; the
/// values are invented.
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

    // ---- ResolvePath walks a parent->child NAME path one scoped segment at a time ----

    [Fact]
    public void ResolvePathWalksEachSegmentInsideThePreviousMatches()
    {
        var root = Node("m_build01");
        var door = Node("door1");
        var hinge = Node("hinge1");
        var resolver = Build(
            (root, "m_build01", null),
            (door, "door1", root),
            (hinge, "hinge1", door));

        var found = resolver.ResolvePath(
            new List<string> { "m_build01", "door1", "hinge1" }, null, localOnly: true);

        Assert.Equal(new[] { hinge }, found);
    }

    private static TestNode Node(string label) => new() { Label = label };

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

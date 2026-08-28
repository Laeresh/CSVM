using System.Collections.Generic;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>The compiled <c>ACTIVATION_PREREQUISITE</c> node-state leaf: the state is bit 0 of
/// <c>active_raw</c> (bit 1 is the definition's LOCAL_NODES_ONLY scope), so a local INACTIVE entry,
/// the shape every zeppelin gasbag finisher ships, reads inactive even though mech3ax's
/// <c>active</c> field reports it true (docs/formats/anim-definitions/compiled-archives.md).</summary>
[Trait("Tier", "Quick")]
public class CompiledPrereqTests
{
    [Theory]
    [InlineData(0.0, false, false)]
    [InlineData(1.0, true, true)]
    [InlineData(2.0, true, false)]
    [InlineData(3.0, true, true)]
    public void TheLeafStateIsBitZeroOfActiveRaw(double raw, bool mech3axActive, bool expected)
    {
        var def = AnimDefinition.Parse(Def(Leaf("panelleftb1", raw, mech3axActive)), "test");
        var prereq = Assert.Single(def.PrereqNodes);
        Assert.Equal(new[] { "lkgasbag04", "panelleftb1" }, prereq.Path);
        Assert.Equal(expected, prereq.Active);
        Assert.True(prereq.Required);
    }

    [Fact]
    public void WithoutTheRawWordTheActiveFieldStands()
    {
        var leaf = Leaf("panelleftb1", 0.0, true);
        ((Dictionary<string, object?>)leaf["Object"]!).Remove("active_raw");
        var def = AnimDefinition.Parse(Def(leaf), "test");
        Assert.True(Assert.Single(def.PrereqNodes).Active);
    }

    private static Dictionary<string, object?> Leaf(string name, double raw, bool active) => new()
    {
        ["Object"] = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["required"] = true,
            ["active"] = active,
            ["ptr"] = 3874.0,
            ["active_raw"] = raw,
        },
    };

    private static AnimData Def(Dictionary<string, object?> leaf) => new(new Dictionary<string, object?>
    {
        ["name"] = "lkgasbag04",
        ["anim_name"] = "finished_lkgasbag04",
        ["activation"] = "OnCall",
        ["activ_prereq_min_to_satisfy"] = 0.0,
        ["activ_prereqs"] = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["Parent"] = new Dictionary<string, object?> { ["name"] = "lkgasbag04", ["required"] = true, ["active"] = true, ["ptr"] = 3819.0 },
            },
            leaf,
        },
    });
}

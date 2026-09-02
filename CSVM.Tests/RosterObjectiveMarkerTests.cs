using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The roster's own objective-marker fields (docs/formats/ai-rosters.md "Field table"): slot 37
/// <c>objectiveTarget</c>, a strict boolean, and slot 39 <c>helpLabel</c>, the MSG_OBJ_* key a
/// flagged block carries. Pins the boolean gate, the raw pass-through of a non-key string when the
/// flag is unset (C4/M05's <c>blakepeace_3_1</c>/<c>_2</c> author <c>"Blake Aviation"</c> here),
/// and the seam that carries both into <see cref="RosterSpawnPlan"/>.
/// </summary>
public class RosterObjectiveMarkerTests
{
    private static VehicleDefs Defs => VehicleDefs.FromRoot(new List<object?>
    {
        "basic_airplane", Props("mode", "jet", "nodename", "firebrand"),
        "player_airplane", Props("kind_of", "basic_airplane"),
        "fury", Props("kind_of", "basic_airplane"),
        "pfury", Props("kind_of", "player_airplane", "nodename", "player_fury"),
        "secfury", Props("kind_of", "fury"),
    });

    private static List<AiNet> Nets => new()
    {
        new AiNet
        {
            Id = 31,
            Name = "ThirtyOne",
            Nodes = new[] { new AiNetNode(Vector3.Zero, System.Array.Empty<float>()), new AiNetNode(Vector3.Right, System.Array.Empty<float>()) },
            Edges = new[] { (0, 1) },
        },
    };

    // AiSkills.RosterObjectiveTarget: the ">=1" gate, over all 414 shipped blocks a strict boolean.
    [Fact]
    public void ObjectiveTargetReadsOnlyWhenAuthoredOne()
    {
        Assert.True(AiSkills.RosterObjectiveTarget(Block(1f, "MSG_OBJ_FOLLOW")));
        Assert.False(AiSkills.RosterObjectiveTarget(Block(0f, "")));
        Assert.False(AiSkills.RosterObjectiveTarget(new List<object?>()));  // short block, slot absent
    }

    // AiSkills.RosterHelpLabel: reads slot 39 raw. The gate against slot 37 is the caller's job
    // (CampaignDirector.RegisterObjectiveMarker), not this accessor's — C4/M05's blakepeace_3_1/_2
    // author a non-key string here with the flag at 0, and this reader must not hide that.
    [Fact]
    public void HelpLabelReadsWhateverSlot39CarriesRaw()
    {
        Assert.Equal("MSG_OBJ_FOLLOW", AiSkills.RosterHelpLabel(Block(1f, "MSG_OBJ_FOLLOW")));
        Assert.Equal("Blake Aviation", AiSkills.RosterHelpLabel(Block(0f, "Blake Aviation")));
        Assert.Null(AiSkills.RosterHelpLabel(Block(1f, "")));
        Assert.Null(AiSkills.RosterHelpLabel(new List<object?>()));
    }

    // The seam: CampaignRosterPlan.Build reads both slots into RosterSpawnPlan, which
    // CampaignDirector.BuildRoster's registration gates on ObjectiveTarget before booking a label.
    [Fact]
    public void BothFieldsReachThePlannedBlock()
    {
        var plan = CampaignRosterPlan.Build(new List<(string, List<object?>)>
        {
            ("secfury_5", Block(1f, "MSG_OBJ_FOLLOW")),
            ("secfury_1", Block(0f, "")),
        }, Defs, Nets);

        var byName = plan.Spawns.ToDictionary(s => s.Name);
        Assert.True(byName["secfury_5"].ObjectiveTarget);
        Assert.Equal("MSG_OBJ_FOLLOW", byName["secfury_5"].HelpLabel);
        Assert.False(byName["secfury_1"].ObjectiveTarget);
        Assert.Null(byName["secfury_1"].HelpLabel);
    }

    private static List<object?> Props(params object?[] pairs)
    {
        var list = new List<object?>();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            list.Add(pairs[i]);
            list.Add(new List<object?> { pairs[i + 1] });
        }
        return list;
    }

    // An 81-slot block, the shape the shipped rosters author: netids, position, yaw, team, group,
    // enabled, primary_target, then objectiveTarget at 37 and helpLabel at 39, -1/0 elsewhere.
    private static List<object?> Block(float objectiveTarget, string helpLabel)
    {
        var fields = new List<object?>(81);
        for (int i = 0; i < 81; i++)
        {
            fields.Add(i is >= 8 and <= 19 ? 0f : -1f);
        }
        fields[0] = 31f;
        fields[1] = new List<object?> { 10f, 20f, 30f };
        fields[2] = 90f;
        fields[3] = 0f;
        fields[4] = 5f;
        fields[5] = 1f;
        fields[6] = "";
        fields[7] = 0f;
        fields[20] = "";
        fields[21] = 0f;
        fields[33] = null;
        fields[37] = objectiveTarget;
        fields[38] = "";
        fields[39] = helpLabel;
        fields[40] = 0f;
        fields[66] = -1f;
        return fields;
    }
}

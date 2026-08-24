using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The flight check screen: row composition for the pilot's plane with and without a
/// wingman, the plane-change gate on ordinals 13/17 and under three planes, the ammo route setting
/// the flow's slot, and the fly request.</summary>
public class CampaignFlightCheckPageTests
{
    private static readonly StockLoadouts Stock =
        StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));

    [Fact]
    public void ThePilotRowShowsTheSelectedPlaneAndItsChangeAmmoRow()
    {
        var page = NewPage(out _, out _, wingman: false);

        Assert.StartsWith("PILOT", page.RowText(0));
        Assert.Contains("Gypsy Magic", page.RowText(0));
        Assert.Contains("1)", page.Detail(0));
        Assert.Equal("CHANGE AMMO", page.RowText(1));
    }

    [Fact]
    public void WithNoWingmanFlagTheWingmanRowsAreAbsent()
    {
        var page = NewPage(out _, out _, wingman: false);

        for (int row = 0; row < page.RowCount; row++)
        {
            Assert.False(page.RowText(row).StartsWith("WINGMAN"));
        }

        Assert.Equal("RETURN TO BRIEFING", page.RowText(page.RowCount - 2));
        Assert.Equal("FLY MISSION", page.RowText(page.RowCount - 1));
    }

    [Fact]
    public void WithTheWingmanFlagSetTheWingmanBlockAndItsChangeAmmoRowAppear()
    {
        var page = NewPage(out _, out _, wingman: true);

        bool sawWingman = false;
        int changeAmmoCount = 0;
        for (int row = 0; row < page.RowCount; row++)
        {
            string text = page.RowText(row);
            if (text.StartsWith("WINGMAN"))
            {
                sawWingman = true;

                // A fresh profile seeds WingmanPlane at the same index as SelectedPlane (0), a
                // B11 gap the plan's C24 section records rather than papers over here.
                Assert.Contains("Gypsy Magic", text);
            }

            if (text == "CHANGE AMMO")
            {
                changeAmmoCount++;
            }
        }

        Assert.True(sawWingman);
        Assert.Equal(2, changeAmmoCount);
    }

    [Fact]
    public void EightRowGunAndRocketListsBlankRatherThanPad()
    {
        var page = NewPage(out _, out _, wingman: false);

        // The Devastator's stock fit (loadouts.md) fills all 6 barrel rows but only pylons
        // 1, 5, 2, 6 (Loadout.PylonFillOrder), so rocket rows 3, 4, 7, 8 stay bare "n)".
        string block = page.Detail(0);
        var lines = block.Split('\n');
        Assert.Equal(8, lines.Length);
        Assert.Equal("3)", lines[2].Split("    ")[1]);
        Assert.Equal("4)", lines[3].Split("    ")[1]);
        Assert.Equal("7)", lines[6].Split("    ")[1]);
        Assert.Equal("8)", lines[7].Split("    ")[1]);
        Assert.Contains(".50-cal", lines[0]);
    }

    [Theory]
    [InlineData(12, 3, true)]   // ordinal 13
    [InlineData(16, 3, true)]   // ordinal 17
    [InlineData(11, 3, false)]
    [InlineData(15, 3, false)]
    [InlineData(0, 2, true)]    // under three planes
    public void ChangePlaneIsBarredOnOrdinals13And17AndUnderThreePlanes(int seq, int planeCount, bool barred)
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        for (int i = profile.Planes.Count; i < planeCount; i++)
        {
            profile.Planes.Add(new OwnedPlane { Name = $"Extra {i}", Airframe = 5 });
        }

        var page = NewPage(out _, out _, wingman: false, profile: profile, missionSeq: seq);

        bool hasChangePlane = false;
        for (int row = 0; row < page.RowCount; row++)
        {
            if (page.RowText(row).StartsWith("CHANGE PLANE"))
            {
                hasChangePlane = true;
            }
        }

        Assert.Equal(!barred, hasChangePlane);
    }

    [Fact]
    public void ChangePlaneStepsThroughTheOwnedPlanesAndSaves()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Add(new OwnedPlane { Name = "Third Plane", Airframe = 5 });
        var page = NewPage(out var flow, out _, wingman: false, profile: profile, missionSeq: 0);

        int changePlaneRow = -1;
        for (int row = 0; row < page.RowCount; row++)
        {
            if (page.RowText(row).StartsWith("CHANGE PLANE"))
            {
                changePlaneRow = row;
                break;
            }
        }

        Assert.True(changePlaneRow >= 0);
        Assert.True(page.Step(changePlaneRow, 1));
        Assert.Equal(1, flow.Profile!.SelectedPlane);

        var reloaded = flow.Store.Load("Zachary");
        Assert.Equal(1, reloaded!.SelectedPlane);
    }

    [Fact]
    public void ChangeAmmoSetsTheFlowsSlotAndOpensAmmo()
    {
        var page = NewPage(out var flow, out _, wingman: true);

        int wingmanChangeAmmoRow = -1;
        bool first = true;
        for (int row = 0; row < page.RowCount; row++)
        {
            if (page.RowText(row) != "CHANGE AMMO")
            {
                continue;
            }

            if (first)
            {
                first = false;
                continue;
            }

            wingmanChangeAmmoRow = row;
            break;
        }

        Assert.True(wingmanChangeAmmoRow >= 0);
        Assert.True(page.Accept(wingmanChangeAmmoRow));
        Assert.Equal(CampaignScreen.Ammo, flow.Screen);
        Assert.Equal(1, flow.AmmoSlot);
    }

    [Fact]
    public void FlyMissionRequestsTheExit()
    {
        var page = NewPage(out var flow, out _, wingman: false);

        Assert.True(page.Accept(page.RowCount - 1));
        Assert.Equal(CampaignExit.FlyMission, flow.Exit);
    }

    [Fact]
    public void ReturnToBriefingNavigatesBack()
    {
        var page = NewPage(out var flow, out _, wingman: false);

        Assert.True(page.Accept(page.RowCount - 2));
        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
    }

    // A custom-built plane's own guns/hardpoints, not the airframe's stock fit, drive the lists
    // when the plane's name has a CustomPlaneStore entry.
    [Fact]
    public void ACustomBuildsOwnGunsOverrideTheAirframesStockFit()
    {
        string dir = Path.Combine(TestData.TempDir(), "Profiles");
        string planesDir = Path.Combine(TestData.TempDir(), "Planes");
        var store = new CampaignProfileStore(dir);
        var planes = new CustomPlaneStore(planesDir);

        var custom = new CustomPlaneDef { Name = "Gypsy Magic", Airframe = 5, LeftHardpoints = 1, RightHardpoints = 0 };
        custom.Guns[0] = new GunChoice(0, Twin: false); // .30-cal, single
        planes.Save(custom);

        var profile = CampaignProfileDef.NewProfile("Zachary");
        store.Save(profile);

        var flow = new CampaignFlow(store, UiStrings.Empty);
        flow.SelectProfile(store.Load("Zachary")!);
        flow.SetMission(0);
        var page = new CampaignFlightCheckPage(flow, planes, Stock, wingman: false);

        string block = page.Detail(0);
        var lines = block.Split('\n');

        // Row 1 (group 0, single .30-cal) has content; row 2 (the same group's second barrel) is
        // blank because the custom build is not twinned, unlike the Devastator's stock fit.
        Assert.Contains(".30-cal", lines[0]);
        Assert.Equal("2)", lines[1].Split("    ")[0]);
    }

    // Real-data checks: the wingman flag reads from cm_sequence.zrd (not a test override) and the
    // objectives note reads from the mission's own objectives.zrd, both through Flow.DataRoot.
    [ExtractedDataFact]
    public void TheWingmanFlagAgreesWithCampaignSequenceForARealMission()
    {
        var missions = CampaignSequence.Load(
            SessionPaths.PreferUnzipped(Path.Combine(TestData.DataRoot!, "extracted", "zrdr.zip")));
        int withWingman = -1;
        int withoutWingman = -1;
        foreach (var m in missions)
        {
            if (m.Wingman && withWingman < 0)
            {
                withWingman = m.Seq;
            }

            if (!m.Wingman && withoutWingman < 0)
            {
                withoutWingman = m.Seq;
            }
        }

        Assert.True(withWingman >= 0);
        Assert.True(withoutWingman >= 0);
        Assert.True(HasWingmanForReal(withWingman));
        Assert.False(HasWingmanForReal(withoutWingman));
    }

    [ExtractedDataFact]
    public void TheObjectivesNoteListsNumberedLinesForARealMission()
    {
        string dir = Path.Combine(TestData.TempDir(), "Profiles");
        var store = new CampaignProfileStore(dir);
        var def = CampaignProfileDef.NewProfile("Zachary");
        store.Save(def);

        var flow = new CampaignFlow(store, UiStrings.Empty, TestData.DataRoot);
        flow.SelectProfile(store.Load("Zachary")!);
        flow.SetMission(0);
        var planes = new CustomPlaneStore(Path.Combine(TestData.TempDir(), "Planes"));
        var page = new CampaignFlightCheckPage(flow, planes, Stock);

        string note = page.Detail(0);
        Assert.Contains("1)", note);
    }

    // A separate page/flow pair with a real DataRoot, isolated from the no-DataRoot fixtures
    // above, so the wingman flag test above reads Mission() rather than the test override.
    private static bool HasWingmanForReal(int seq)
    {
        string dir = Path.Combine(TestData.TempDir(), "Profiles");
        var store = new CampaignProfileStore(dir);
        var def = CampaignProfileDef.NewProfile("Zachary");
        store.Save(def);

        var flow = new CampaignFlow(store, UiStrings.Empty, TestData.DataRoot);
        flow.SelectProfile(store.Load("Zachary")!);
        flow.SetMission(seq);
        var planes = new CustomPlaneStore(Path.Combine(TestData.TempDir(), "Planes"));
        var page = new CampaignFlightCheckPage(flow, planes, Stock);

        bool sawWingman = false;
        for (int row = 0; row < page.RowCount; row++)
        {
            if (page.RowText(row).StartsWith("WINGMAN"))
            {
                sawWingman = true;
            }
        }

        return sawWingman;
    }

    private static CampaignFlightCheckPage NewPage(
        out CampaignFlow flow, out string dir, bool wingman,
        CampaignProfileDef? profile = null, int missionSeq = 0)
    {
        dir = Path.Combine(TestData.TempDir(), "Profiles");
        var store = new CampaignProfileStore(dir);
        var def = profile ?? CampaignProfileDef.NewProfile("Zachary");
        store.Save(def);

        // flow.GoTo(CampaignScreen.FlightCheck) is deliberately not called: the flow's own
        // registry-built page (no test seams) would touch Godot.ProjectSettings outside the
        // engine. The page under test is a second instance built with explicit stores below.
        flow = new CampaignFlow(store, UiStrings.Empty);
        flow.SelectProfile(store.Load(def.Name)!);
        flow.SetMission(missionSeq);

        var planes = new CustomPlaneStore(Path.Combine(TestData.TempDir(), "Planes"));
        return new CampaignFlightCheckPage(flow, planes, Stock, wingman);
    }
}

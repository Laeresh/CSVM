using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The ammo selection screen: four gun-group picks and eight pylon picks derived from a
/// plane's build (hangar-built or the airframe's stock fit), the mission-threshold ordnance
/// filter, and the working-copy commit ACCEPT/CANCEL implement. Tests drive the page directly
/// (not through <see cref="CampaignFlow"/>'s row dispatch) so the injected
/// <see cref="CustomPlaneStore"/>/<see cref="StockLoadouts"/> stay engine-free.</summary>
public class CampaignAmmoPageTests
{
    private const int AcceptRow = 12;
    private const int CancelRow = 13;

    private static readonly string StockLoadoutsPath =
        Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json");

    [Fact]
    public void GroupsAndPylonsDeriveFromTheBuiltPlane()
    {
        var (flow, page, planes) = NewFlow(out string profileDir);
        var built = new CustomPlaneDef { Name = "Built1", Airframe = 5, LeftHardpoints = 1, RightHardpoints = 3 };
        built.Guns[0] = new GunChoice(2, Twin: false);
        planes.Save(built);

        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Clear();
        profile.Planes.Add(new OwnedPlane { Name = "Built1", Airframe = 5 });
        profile.SelectedPlane = 0;
        flow.SelectProfile(profile);
        flow.SetAmmoSlot(0);

        Assert.Contains("Slug", page.RowText(0));
        Assert.Null(page.Combo(1));
        Assert.Null(page.Combo(2));
        Assert.Null(page.Combo(3));
        Assert.Equal(3, CaptionsSaying(page, "No Gun").Count);

        Assert.NotNull(page.Combo(4)); // left cell 0, LeftHardpoints=1
        Assert.Null(page.Combo(5));    // left cell 1, inactive
        Assert.NotNull(page.Combo(8));  // right cell 0, RightHardpoints=3
        Assert.NotNull(page.Combo(9));  // right cell 1
        Assert.NotNull(page.Combo(10)); // right cell 2
        Assert.Null(page.Combo(11));    // right cell 3, inactive

        Directory.Delete(profileDir, true);
    }

    [Fact]
    public void AStarterPlaneReadsTheAirframesStockFit()
    {
        // The two profile-seeded starters never touch CustomPlaneStore (B13): the Devastator
        // (airframe 5) stock fit is three guns and 2+2 hardpoints (loadouts.md's stock table).
        var (flow, page, _) = NewFlow(out string profileDir);
        var profile = CampaignProfileDef.NewProfile("Zachary"); // "Gypsy Magic" / "The Knave", airframe 5
        flow.SelectProfile(profile);
        flow.SetAmmoSlot(0);

        Assert.NotNull(page.Combo(0));
        Assert.NotNull(page.Combo(1));
        Assert.NotNull(page.Combo(2));
        Assert.Null(page.Combo(3));
        Assert.Single(CaptionsSaying(page, "No Gun"));

        Assert.NotNull(page.Combo(4));  // left cell 0
        Assert.NotNull(page.Combo(5));  // left cell 1
        Assert.Null(page.Combo(6));     // left cell 2, only 2 hardpoints per wing
        Assert.NotNull(page.Combo(8));  // right cell 0
        Assert.NotNull(page.Combo(9));  // right cell 1
        Assert.Null(page.Combo(10));    // right cell 2

        Directory.Delete(profileDir, true);
    }

    [Fact]
    public void SteppingAGreyedNoGunGroupIsANoOp()
    {
        var (flow, page, planes) = NewFlow(out string profileDir);
        var built = new CustomPlaneDef { Name = "Built2", Airframe = 5 }; // no guns at all
        planes.Save(built);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Clear();
        profile.Planes.Add(new OwnedPlane { Name = "Built2", Airframe = 5 });
        flow.SelectProfile(profile);
        flow.SetAmmoSlot(0);

        Assert.False(page.Step(0, 1));
        Assert.Null(page.Combo(0));
        Assert.Equal(4, CaptionsSaying(page, "No Gun").Count);

        Directory.Delete(profileDir, true);
    }

    [Fact]
    public void TheOrdnanceFilterHonoursTheMissionThresholdTable()
    {
        var (flow, page, planes) = NewFlow(out string profileDir);
        var built = new CustomPlaneDef { Name = "Built3", Airframe = 5, LeftHardpoints = 1 };
        planes.Save(built);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Clear();
        profile.Planes.Add(new OwnedPlane { Name = "Built3", Airframe = 5 });
        flow.SelectProfile(profile);
        flow.SetAmmoSlot(0);

        // Mission 1: only AP, HE and None (thresholds 1) may ever appear on cell 0.
        flow.SetMission(0); // seq 0 -> ordinal 1
        for (int i = 0; i < 12; i++)
        {
            page.Step(4, 1);
            string label = page.RowText(4);
            Assert.True(label is "Armor-piercing" or "High explosive" or "None",
                $"unlocked too early at ordinal 1: {label}");
        }

        // Mission 20: torpedoes (threshold 20) become reachable.
        flow.SetMission(19); // seq 19 -> ordinal 20
        bool sawTorpedo = false;
        for (int i = 0; i < 12; i++)
        {
            page.Step(4, 1);
            if (page.RowText(4) == "Torpedo")
            {
                sawTorpedo = true;
            }
        }

        Assert.True(sawTorpedo, "torpedoes should be reachable at ordinal 20");

        Directory.Delete(profileDir, true);
    }

    [Fact]
    public void AcceptPersistsAndCancelLeavesTheStoredFitUntouched()
    {
        var (flow, page, planes) = NewFlow(out string profileDir);
        var built = new CustomPlaneDef { Name = "Built4", Airframe = 5, LeftHardpoints = 1 };
        built.Guns[0] = new GunChoice(0, Twin: false);
        planes.Save(built);

        var store = new CampaignProfileStore(profileDir);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Clear();
        profile.Planes.Add(new OwnedPlane { Name = "Built4", Airframe = 5 });
        store.Save(profile);

        flow.SelectProfile(profile);
        flow.SetAmmoSlot(0);

        // Edit, then CANCEL: the stored file must not move.
        page.Step(0, 1); // ammo index 0 -> 1
        page.Step(4, 1); // pylon cell 0 to the next available ordnance
        Assert.True(page.Accept(CancelRow));

        var afterCancel = store.Load("Zachary")!;
        Assert.Equal(0, afterCancel.Planes[0].Ammo[0]);
        Assert.Equal(0, afterCancel.Planes[0].Ordnance[0]);

        // Re-entering the same target must read the still-unedited stored fit, not the discard.
        Assert.Contains("Slug", page.RowText(0));

        // Edit again, then ACCEPT: the stored file must carry the edit.
        page.Step(0, 1);
        Assert.True(page.Accept(AcceptRow));

        var afterAccept = store.Load("Zachary")!;
        Assert.Equal(1, afterAccept.Planes[0].Ammo[0]);

        Directory.Delete(profileDir, true);
    }

    [Fact]
    public void TheWingmanSlotEditsTheWingmansPlaneNotThePilots()
    {
        var (flow, page, planes) = NewFlow(out string profileDir);
        var pilotPlane = new CustomPlaneDef { Name = "Pilot", Airframe = 5 };
        pilotPlane.Guns[0] = new GunChoice(0, Twin: false); // gun mounted: reads an ammo pick
        var wingmanPlane = new CustomPlaneDef { Name = "Wingman", Airframe = 5 }; // default: no gun, reads "No Gun"
        planes.Save(pilotPlane);
        planes.Save(wingmanPlane);
        planes.Save(wingmanPlane);

        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Clear();
        profile.Planes.Add(new OwnedPlane { Name = "Pilot", Airframe = 5 });
        profile.Planes.Add(new OwnedPlane { Name = "Wingman", Airframe = 5 });
        profile.SelectedPlane = 0;
        profile.WingmanPlane = 1;
        flow.SelectProfile(profile);

        flow.SetAmmoSlot(0);
        string pilotAmmo = page.RowText(0);

        flow.SetAmmoSlot(1);
        string wingmanAmmo = page.RowText(0);

        Assert.NotEqual(pilotAmmo, wingmanAmmo);

        Directory.Delete(profileDir, true);
    }

    // ⚠ C22/decision 4: a guest's ammunition edits are free and discarded. ACCEPT writes their own
    // session-scoped record, and the seated profile's file is not rewritten at all.
    [Fact]
    public void AGuestsAcceptWritesTheirOwnRecordAndLeavesTheProfileFileUntouched()
    {
        var (flow, page, _) = NewFlow(out string profileDir);
        flow.Store.Save(CampaignProfileDef.NewProfile("Zachary"));
        flow.SelectProfile(flow.Store.Load("Zachary")!);
        flow.SetPlayers(2);
        flow.Field.Advance();
        string file = Path.Combine(profileDir, "Zachary", "profile.json");
        byte[] before = File.ReadAllBytes(file);

        Assert.True(page.Step(0, 2));
        Assert.True(page.Accept(AcceptRow));

        Assert.Equal(2, flow.Field.Plane(1)!.Ammo[0]);
        Assert.Equal(before, File.ReadAllBytes(file));

        Directory.Delete(profileDir, true);
    }

    // C9: the caption is the group's calibre and the field is a drop-down under it, both at
    // [@OrdinanceLayout@]'s own coordinates (OL_T_GunName0 at 142,105 and OL_D_AMMO0 at 136,120,
    // DROPWIDTH 148, STDITEMH 15). Nothing may sit at the other's position.
    [Fact]
    public void CaptionsAndFieldsTakeTheirAuthoredPositions()
    {
        var (flow, page, _) = NewFlow(out string profileDir);
        flow.SelectProfile(CampaignProfileDef.NewProfile("Zachary")); // Devastator: three guns
        flow.SetAmmoSlot(0);

        var captions = CaptionsSaying(page, "-cal.");
        Assert.Equal(3, captions.Count);
        Assert.Equal(new[] { 105f, 147f, 189f }, captions.Select(c => c.Y));
        Assert.All(captions, c => Assert.Equal(142f, c.X));

        var field = page.Combo(0)!;
        Assert.Equal((136f, 120f, 148f, 15f), (field.X, field.Y, field.Width, field.RowHeight));
        Assert.Equal(204f, page.Combo(2)!.Y);  // OL_D_AMMO2, its caption fifteen pixels above it
        Assert.Equal(136f, page.Combo(4)!.X);  // OL_D_ROCKETS0, the left rocket column
        Assert.Equal(410f, page.Combo(8)!.X);  // OL_D_ROCKETS4, the right one
        Assert.Equal(new[] { 320f, 348f }, new[] { page.Combo(4)!.Y, page.Combo(5)!.Y });

        Directory.Delete(profileDir, true);
    }

    // C9 adopting A1: the field opens over the screen, the axis moves inside it, and the confirm is
    // what writes the pick. The rocket list holds only what the mission ordinal has unlocked.
    [Fact]
    public void ARocketFieldOpensItsListAndTheConfirmTakesThePick()
    {
        var (flow, page, _) = NewFlow(out string profileDir);
        flow.SelectProfile(CampaignProfileDef.NewProfile("Zachary"));
        flow.SetAmmoSlot(0);
        flow.SetMission(0); // ordinal 1: only the three threshold-1 rows are offered

        var field = page.Combo(4)!;
        Assert.Equal(3, field.Entries.Count);
        Assert.False(field.Scrolls);

        Assert.True(page.Accept(4));
        Assert.True(field.Open);
        string before = field.Text;

        // The axis moves inside the open list, which is the seam the flow hands it through.
        Assert.True(field.Move(1));
        Assert.True(page.Accept(4));
        Assert.False(field.Open);
        Assert.NotEqual(before, field.Text);

        // Back on a closed field leaves the screen instead of collapsing anything.
        Assert.True(page.Accept(4));
        Assert.True(page.Back());
        Assert.False(field.Open);

        Directory.Delete(profileDir, true);
    }

    // The lines the page draws itself, matched on their words.
    private static List<BoardLine> CaptionsSaying(CampaignAmmoPage page, string words)
    {
        var found = new List<BoardLine>();
        foreach (var line in page.Captions)
        {
            if (line.Text.Contains(words))
            {
                found.Add(line);
            }
        }

        return found;
    }

    private static (CampaignFlow Flow, CampaignAmmoPage Page, CustomPlaneStore Planes) NewFlow(out string profileDir)
    {
        profileDir = Path.Combine(TestData.TempDir(), "Profiles");
        Directory.CreateDirectory(profileDir);
        var planesDir = Path.Combine(TestData.TempDir(), "Planes");
        var planes = new CustomPlaneStore(planesDir);
        var stock = StockLoadouts.Load(StockLoadoutsPath);
        // The flow carries both stores so the flight check the page returns to resolves fits
        // off-engine too, the way the shell's flow does on-engine.
        var flow = new CampaignFlow(new CampaignProfileStore(profileDir), UiStrings.Empty, null, planes, stock);
        var page = new CampaignAmmoPage(flow, planes, stock);
        return (flow, page, planes);
    }
}

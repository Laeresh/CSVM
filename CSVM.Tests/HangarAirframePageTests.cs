using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The AIRFRAME screen (PLAN-hangar C22): all 11 airframes offered (Decision 9), the stepper
/// writing the scratch plane's airframe and nothing else, the detail line carrying the stat
/// table's figures and the economy's own star ratings, and the focused airframe's blueprint
/// through the page-art seam. E41's airframe-defaults ask (string 206) meets a new plane's
/// arrival and every switch to a different airframe: OK loads the airframe's defaults, Cancel
/// keeps every current pick.
/// </summary>
public class HangarAirframePageTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarAirframePageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-airframe-" + Guid.NewGuid().ToString("N"));
        _store = new CustomPlaneStore(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Decision 9: every one of the 11 airframes is a row; the availability threshold
    /// gates nothing. Names come from langui 3000+id.</summary>
    [Fact]
    public void OffersAllElevenAirframes_NamedFromLangui()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":3000,\"text\":\"Ford Hoplite\",\"dll\":\"langui\"}," +
            "{\"id\":3002,\"text\":\"Blackflag Balmoral\",\"dll\":\"langui\"}]");
        var flow = OpenOnAirframe(strings);

        Assert.Equal(HangarEconomy.Airframes.Length, flow.Page.RowCount);
        Assert.Equal(11, flow.Page.RowCount);
        Assert.StartsWith("Ford Hoplite", flow.Page.RowText(0), StringComparison.Ordinal);
        Assert.StartsWith("Blackflag Balmoral", flow.Page.RowText(2), StringComparison.Ordinal);
    }

    /// <summary>The ←→ stepper makes the focused row the pick (raising the defaults ask, here
    /// declined) and ticks it; stepping a row that already is the pick changes nothing.</summary>
    [Fact]
    public void SteppingSelectsTheFocusedAirframe()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Move(1);
        flow.Move(1);

        Assert.True(flow.Step(1));
        Assert.Equal(2, flow.Scratch.Airframe);
        flow.AnswerDefaultsAsk(false);
        Assert.EndsWith("✓", flow.Page.RowText(2), StringComparison.Ordinal);
        Assert.False(flow.Page.RowText(0).EndsWith("✓", StringComparison.Ordinal));
        Assert.False(flow.Step(1));
    }

    /// <summary>Changing airframe through the ask's Cancel row preserves every other pick, the
    /// pre-E41 behaviour string 206 promises: guns and hardpoints are count-valid on every
    /// airframe (the wrong-claims disproof), so nothing re-clamps.</summary>
    [Fact]
    public void ChangingAirframe_PreservesTheOtherPicks()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Scratch.Engine = 3;
        flow.Scratch.Guns[3] = new GunChoice(4, true);
        flow.Scratch.LeftHardpoints = 4;
        flow.Scratch.ArmourTail = 7;

        flow.Move(1);
        Assert.True(flow.Step(1));

        Assert.Equal(1, flow.DefaultsAsk); // the switch itself already stands; the ask offers defaults
        Assert.Equal(1, flow.Scratch.Airframe);
        flow.Move(1); // onto Cancel
        flow.Accept(); // decline = the old behaviour

        Assert.Null(flow.DefaultsAsk);
        Assert.Equal(1, flow.Scratch.Airframe);
        Assert.Equal(3, flow.Scratch.Engine);
        Assert.Equal(new GunChoice(4, true), flow.Scratch.Guns[3]);
        Assert.Equal(4, flow.Scratch.LeftHardpoints);
        Assert.Equal(7, flow.Scratch.ArmourTail);
    }

    /// <summary>Confirm advances to the Engine screen without touching the pick, so walking the
    /// flow straight through keeps whatever airframe was chosen.</summary>
    [Fact]
    public void AcceptAdvancesWithoutEditing()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Scratch.Airframe = 5;
        flow.Accept();

        Assert.Equal(HangarScreen.Engine, flow.Screen);
        Assert.Equal(5, flow.Scratch.Airframe);
    }

    /// <summary>Every row's detail line shows its own decoded cost, weight and capacity
    /// (docs/org/hangar.md's stat table, pinned by HangarEconomyTests).</summary>
    [Fact]
    public void DetailShowsTheDecodedFigures()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);

        string hoplite = flow.Page.Detail(0);
        Assert.Contains("$6800", hoplite, StringComparison.Ordinal);
        Assert.Contains("1400 lbs.", hoplite, StringComparison.Ordinal);
        Assert.Contains("Capacity 4160 lbs.", hoplite, StringComparison.Ordinal);

        string balmoral = flow.Page.Detail(2);
        Assert.Contains("$1870", balmoral, StringComparison.Ordinal);
        Assert.Contains("5460 lbs.", balmoral, StringComparison.Ordinal);
        Assert.Contains("Capacity 15760 lbs.", balmoral, StringComparison.Ordinal);
    }

    /// <summary>The stars are HangarEconomy's own: Hoplite tops agility at 4, the Balmoral sits
    /// at 0 (the plan's hand-checked pair); armour stars track the scratch plane's armour units,
    /// exactly as the decoded formula reads the record.</summary>
    [Fact]
    public void StarRatingsMatchTheEconomy()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);

        Assert.Contains("Agility ★★★★", flow.Page.Detail(0), StringComparison.Ordinal);
        Assert.Contains("Armor ☆☆☆☆", flow.Page.Detail(0), StringComparison.Ordinal);
        Assert.Contains("Agility ☆☆☆☆", flow.Page.Detail(2), StringComparison.Ordinal);
        Assert.Contains("Armor ★☆☆☆", flow.Page.Detail(2), StringComparison.Ordinal);

        flow.Scratch.ArmourNose = 12;
        flow.Scratch.ArmourTail = 12;
        flow.Scratch.ArmourLeftWing = 12;
        flow.Scratch.ArmourRightWing = 12;
        Assert.Contains("Armor ★★★★", flow.Page.Detail(0), StringComparison.Ordinal);
    }

    /// <summary>Reading a detail line never edits the scratch plane, even though the ratings are
    /// priced for the focused row's airframe rather than the chosen one.</summary>
    [Fact]
    public void DetailLeavesTheScratchUntouched()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Scratch.Airframe = 7;
        flow.Page.Detail(2);

        Assert.Equal(7, flow.Scratch.Airframe);
    }

    /// <summary>A flow opened without a data root has no art to offer, and the screen still
    /// works: art is optional by design.</summary>
    [Fact]
    public void ArtIsNullWithoutADataRoot() =>
        Assert.Null(OpenOnAirframe(UiStrings.Empty).Page.Art);

    /// <summary>With the extraction present, the page's art is the focused airframe's blueprint
    /// TGA at its catalogued 358x335, captioned with that airframe's name, and it follows the
    /// cursor rather than the pick.</summary>
    [ExtractedDataFact]
    public void ArtShowsTheFocusedAirframesBlueprint()
    {
        var flow = new HangarFlow(_store, UiStrings.Empty, TestData.DataRoot);
        flow.Accept();
        flow.AnswerDefaultsAsk(false);

        var art = flow.Page.Art;
        Assert.NotNull(art);
        Assert.Equal(358, art!.Image.Width);
        Assert.Equal(335, art.Image.Height);
        Assert.Equal("Airframe 0", art.Caption);

        flow.Move(1);
        Assert.Equal("Airframe 1", flow.Page.Art!.Caption);
    }

    /// <summary>E41: a new plane's first arrival on the airframe screen raises the defaults ask
    /// as the inline two-row confirm, offering the opening airframe's own defaults.</summary>
    [Fact]
    public void ANewPlanesFirstArrivalRaisesTheAsk()
    {
        var flow = new HangarFlow(_store, UiStrings.Empty);
        flow.Accept(); // New Plane, on to Airframe

        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        Assert.Equal(0, flow.DefaultsAsk);
        Assert.Equal(2, flow.Page.RowCount);
        Assert.Equal("OK", flow.Page.RowText(0));
        Assert.Equal("Cancel", flow.Page.RowText(1));
        Assert.Contains("default armor, engine, and guns", flow.Page.Detail(0), StringComparison.Ordinal);
    }

    /// <summary>E41: editing a saved plane never asks; the plane already is what its builder
    /// chose, and the screen opens straight onto the airframe list.</summary>
    [Fact]
    public void EditingASavedPlaneDoesNotAsk()
    {
        _store.Save(new CustomPlaneDef { Name = "Kept", Airframe = 4 });
        var flow = new HangarFlow(_store, UiStrings.Empty);
        flow.Move(1);
        flow.Accept();

        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        Assert.Null(flow.DefaultsAsk);
        Assert.Equal(11, flow.Page.RowCount);
    }

    /// <summary>E41: stepping the airframe that already is the pick raises nothing, exactly as
    /// the original only asks when the airframe pick changes.</summary>
    [Fact]
    public void SteppingTheChosenAirframeDoesNotAsk()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);

        Assert.False(flow.Step(1)); // the cursor sits on the chosen airframe after the decline
        Assert.Null(flow.DefaultsAsk);
    }

    /// <summary>E41: the ask speaks string 206 with both names formatted in; %2 is the plane's
    /// own name once it has one.</summary>
    [Fact]
    public void TheAskSpeaksString206WithBothNames()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":206,\"text\":\"Defaults for %1!s!? You were building the %2!s!.\",\"dll\":\"langui\"}," +
            "{\"id\":3000,\"text\":\"HOPLITE\",\"dll\":\"langui\"}," +
            "{\"id\":3001,\"text\":\"HELLHOUND\",\"dll\":\"langui\"}]");
        var flow = OpenOnAirframe(strings);
        flow.Scratch.Name = "My Crate";

        flow.Move(1);
        Assert.True(flow.Step(1));
        Assert.Equal("Defaults for HELLHOUND? You were building the My Crate.", flow.Page.Detail(0));

        flow.AnswerDefaultsAsk(false);
        flow.Scratch.Name = string.Empty;
        flow.Move(-1); // back onto the Hoplite row
        Assert.True(flow.Step(1));
        Assert.Equal("Defaults for HOPLITE? You were building the HELLHOUND.", flow.Page.Detail(0));
    }

    /// <summary>E41's accept arm, the worked example: the Balmoral's defaults are its stock fit
    /// read back through the A3 mapping (four twin mounts: two fifty-cals, two thirty-cal
    /// turrets), its eight authored pylons as 4/4 wing counts, and the stock Lvl-2 engine
    /// (id 1). Armour stays 0 here: no zrdr scope was handed in.</summary>
    [Fact]
    public void AcceptingLoadsTheAirframeDefaults()
    {
        var stock = StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));
        var flow = OpenOnAirframe(UiStrings.Empty, stock);
        flow.Scratch.ArmourNose = 9; // a pick the defaults overwrite

        flow.Move(1);
        flow.Move(1); // onto the Balmoral
        Assert.True(flow.Step(1));
        flow.Accept(); // the cursor sits on OK when the ask is raised

        Assert.Null(flow.DefaultsAsk);
        Assert.Equal(2, flow.Scratch.Airframe);
        Assert.Equal(1, flow.Scratch.Engine);
        Assert.Equal(new GunChoice(2, true), flow.Scratch.Guns[0]);
        Assert.Equal(new GunChoice(2, true), flow.Scratch.Guns[1]);
        Assert.Equal(new GunChoice(0, true), flow.Scratch.Guns[2]);
        Assert.Equal(new GunChoice(0, true), flow.Scratch.Guns[3]);
        Assert.Equal(4, flow.Scratch.LeftHardpoints);
        Assert.Equal(4, flow.Scratch.RightHardpoints);
        Assert.Equal(0, flow.Scratch.ArmourNose);
    }

    /// <summary>E41: the Hoplite's stock fit authors one twin thirty-cal and two pylons, whose
    /// fill order (1, 5) puts one on each wing; the Kestrel's slot 1 is the one single-barrel
    /// stock mount, so its default is NOT a twin.</summary>
    [Fact]
    public void DefaultsReadTheStockFitPerAirframe()
    {
        var stock = StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));
        var flow = OpenOnAirframe(UiStrings.Empty, stock);

        flow.LoadAirframeDefaults(0); // Hoplite
        Assert.Equal(new GunChoice(0, true), flow.Scratch.Guns[0]);
        Assert.True(flow.Scratch.Guns[1].IsEmpty);
        Assert.Equal((1, 1), (flow.Scratch.LeftHardpoints, flow.Scratch.RightHardpoints));

        flow.LoadAirframeDefaults(8); // Kestrel
        Assert.Equal(new GunChoice(3, false), flow.Scratch.Guns[0]);
        Assert.Equal(new GunChoice(2, true), flow.Scratch.Guns[1]);
        Assert.True(flow.Scratch.Guns[2].IsEmpty);
        Assert.Equal(new GunChoice(1, true), flow.Scratch.Guns[3]);
        Assert.Equal((3, 2), (flow.Scratch.LeftHardpoints, flow.Scratch.RightHardpoints));
    }

    /// <summary>E41: declining on arrival keeps the brand-new plane's empty state; accepting
    /// without a stock table still sets the engine default and leaves the rest empty.</summary>
    [Fact]
    public void DecliningKeepsTheEmptyState_AndNoStockTableDegradesQuietly()
    {
        var flow = new HangarFlow(_store, UiStrings.Empty);
        flow.Accept();
        flow.AnswerDefaultsAsk(false);
        Assert.Equal(CustomPlaneDef.EngineNone, flow.Scratch.Engine);
        Assert.All(flow.Scratch.Guns, gun => Assert.True(gun.IsEmpty));
        Assert.Equal(0, flow.Scratch.LeftHardpoints);

        flow.LoadAirframeDefaults(0); // no StockFits handed in
        Assert.Equal(1, flow.Scratch.Engine);
        Assert.All(flow.Scratch.Guns, gun => Assert.True(gun.IsEmpty));
        Assert.Equal(0, flow.Scratch.LeftHardpoints);
        Assert.Equal(0, flow.Scratch.ArmourNose);
    }

    /// <summary>E41, with the real extraction: accepting the ask loads the airframe's stock
    /// zone allocations, the destroyable_parts armour pools at five per unit
    /// (docs/formats/vehicle.md), read back through PlaneStats' own vehicle defs.</summary>
    [ExtractedDataFact]
    public void TheDefaultsCarryTheStockArmourAllocations()
    {
        string zrdr = SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));
        var flow = new HangarFlow(_store, UiStrings.Empty, null, null, zrdr);
        flow.Accept(); // New Plane; the arrival ask offers the Hoplite's defaults
        flow.Accept(); // OK

        var stats = PlaneStats.Load(zrdr, "player_autogyro");
        foreach (var part in stats.DestroyableParts)
        {
            int? units = part.Name.ToLowerInvariant() switch
            {
                "nose" => flow.Scratch.ArmourNose,
                "tail" => flow.Scratch.ArmourTail,
                "leftwing" => flow.Scratch.ArmourLeftWing,
                "rightwing" => flow.Scratch.ArmourRightWing,
                _ => null,
            };
            if (units is { } bought)
            {
                Assert.True(bought > 0, $"{part.Name}: stock armour should buy units");
                Assert.Equal((int)Math.Round(part.MaxArmor / 5f), bought);
            }
        }
    }

    // A flow standing on the AIRFRAME screen with a fresh scratch plane, the arrival's
    // defaults ask declined so the plane keeps its empty state.
    private HangarFlow OpenOnAirframe(UiStrings strings, StockLoadouts? stockFits = null)
    {
        var flow = new HangarFlow(_store, strings, null, stockFits);
        flow.Accept(); // New Plane, on to Airframe
        flow.AnswerDefaultsAsk(false);
        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        return flow;
    }
}

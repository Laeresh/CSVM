using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The AIRFRAME screen (PLAN-hangar C22): all 11 airframes offered (Decision 9), confirm writing
/// the scratch plane's airframe and nothing else (E49: the pick is on Enter, the stepper is
/// inert), the detail line carrying the stat table's figures and the economy's own star ratings,
/// and the focused airframe's blueprint through the page-art seam. E41's airframe-defaults ask
/// (string 206) meets every pick of an airframe that is not already the pick: OK loads the
/// airframe's defaults, Cancel keeps every current pick.
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

    /// <summary>E49: confirm makes the focused row the pick (raising the defaults ask, here
    /// declined) and ticks it; the ←→ stepper does nothing at all on this screen, so a pick can
    /// only be made deliberately.</summary>
    [Fact]
    public void ConfirmSelectsTheFocusedAirframe_AndTheStepperIsInert()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Move(1);
        flow.Move(1);

        Assert.False(flow.Step(1));
        Assert.False(flow.Step(-1));
        Assert.False(flow.AirframeChosen);

        Assert.True(flow.Accept());
        Assert.Equal(2, flow.Scratch.Airframe);
        flow.AnswerDefaultsAsk(false);
        Assert.EndsWith("✓", flow.Page.RowText(2), StringComparison.Ordinal);
        Assert.False(flow.Page.RowText(0).EndsWith("✓", StringComparison.Ordinal));
    }

    /// <summary>E49, the user's finding: a new plane arrives with nothing committed. No row is
    /// ticked, no question is asked, and the model's own default airframe stays silent until the
    /// pilot picks one.</summary>
    [Fact]
    public void ANewPlaneArrivesWithNothingChosenAndNothingAsked()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);

        Assert.Null(flow.DefaultsAsk);
        Assert.False(flow.AirframeChosen);
        Assert.Equal(11, flow.Page.RowCount);
        Assert.All(
            new[] { 0, 1, 5, 10 },
            row => Assert.False(flow.Page.RowText(row).EndsWith("✓", StringComparison.Ordinal)));
    }

    /// <summary>E49: picking row 0 is still an explicit pick, so it ticks the row and raises the
    /// ask even though the model was already carrying airframe 0 underneath.</summary>
    [Fact]
    public void PickingTheOpeningRowStillCountsAsAPick()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);

        Assert.True(flow.Accept());
        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        Assert.Equal(0, flow.DefaultsAsk);
        flow.AnswerDefaultsAsk(false);
        Assert.True(flow.AirframeChosen);
        Assert.EndsWith("✓", flow.Page.RowText(0), StringComparison.Ordinal);
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
        Assert.True(flow.Accept());

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

    /// <summary>E49's double-enter idiom: the confirm that picks an airframe stays on the screen
    /// (through the ask), and the next confirm, now on the picked row, advances to the Engine
    /// screen without touching anything.</summary>
    [Fact]
    public void ConfirmingThePickAdvancesWithoutEditing()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.FocusRow(5);

        Assert.True(flow.Accept()); // picks the Fury; the ask opens
        flow.AnswerDefaultsAsk(false);
        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        Assert.Equal(5, flow.Row); // the cursor lands back on the pick

        Assert.True(flow.Accept());
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
        flow.Accept(); // New Plane, on to Airframe

        var art = flow.Page.Art;
        Assert.NotNull(art);
        Assert.Equal(358, art!.Image.Width);
        Assert.Equal(335, art.Image.Height);
        Assert.Equal("Airframe 0", art.Caption);

        flow.Move(1);
        Assert.Equal("Airframe 1", flow.Page.Art!.Caption);
    }

    /// <summary>E41 through E49: the first explicit pick raises the defaults ask as the inline
    /// two-row confirm, offering the picked airframe's own defaults.</summary>
    [Fact]
    public void TheFirstPickRaisesTheAsk()
    {
        var flow = new HangarFlow(_store, UiStrings.Empty);
        flow.Accept(); // New Plane, on to Airframe
        flow.Accept(); // pick the focused airframe

        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        Assert.Equal(0, flow.DefaultsAsk);
        Assert.Equal(2, flow.Page.RowCount);
        Assert.Equal("OK", flow.Page.RowText(0));
        Assert.Equal("Cancel", flow.Page.RowText(1));
        Assert.Contains("default armor, engine, and guns", flow.Page.Detail(0), StringComparison.Ordinal);
    }

    /// <summary>E41: editing a saved plane never asks; the plane already is what its builder
    /// chose, so the screen opens on that airframe's row, ticked (E49).</summary>
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
        Assert.True(flow.AirframeChosen);
        Assert.Equal(4, flow.Row);
        Assert.EndsWith("✓", flow.Page.RowText(4), StringComparison.Ordinal);
    }

    /// <summary>E49: a saved plane's own airframe is already the pick, so confirming it asks
    /// nothing and simply advances. Only a pick of a DIFFERENT airframe asks.</summary>
    [Fact]
    public void ConfirmingASavedPlanesAirframeAdvancesWithoutAsking()
    {
        _store.Save(new CustomPlaneDef { Name = "Kept", Airframe = 4, Engine = 2 });
        var flow = new HangarFlow(_store, UiStrings.Empty);
        flow.Move(1);
        flow.Accept(); // load the saved plane, on to Airframe

        Assert.True(flow.Accept());
        Assert.Null(flow.DefaultsAsk);
        Assert.Equal(HangarScreen.Engine, flow.Screen);
        Assert.Equal(4, flow.Scratch.Airframe);
        Assert.Equal(2, flow.Scratch.Engine);
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
        Assert.True(flow.Accept());
        Assert.Equal("Defaults for HELLHOUND? You were building the My Crate.", flow.Page.Detail(0));

        flow.AnswerDefaultsAsk(false);
        flow.Scratch.Name = string.Empty;
        flow.Move(-1); // back onto the Hoplite row
        Assert.True(flow.Accept());
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
        Assert.True(flow.Accept()); // picks it, raising the ask
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
        flow.Accept(); // New Plane
        flow.Accept(); // pick the focused airframe, raising the ask
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
        flow.Accept(); // New Plane, on to Airframe
        flow.Accept(); // pick the Hoplite, which raises its defaults ask
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

    // A flow standing on the AIRFRAME screen with a fresh scratch plane: nothing picked, nothing
    // asked, which is what a new build now arrives as (E49).
    private HangarFlow OpenOnAirframe(UiStrings strings, StockLoadouts? stockFits = null)
    {
        var flow = new HangarFlow(_store, strings, null, stockFits);
        flow.Accept(); // New Plane, on to Airframe
        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        Assert.Null(flow.DefaultsAsk);
        return flow;
    }
}

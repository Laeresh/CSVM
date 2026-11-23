using System;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Session.Career;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>The career's screens in the Original shell, driven on the layout fixture. The CAREER
/// door stands only over a career feature. A typed name starts a gang onto the bridge, and the
/// bridge reaches the board, the hangar and the ledger and comes back. Scuttling asks first.</summary>
public class OriginalCareerTests
{
    [Fact]
    public void TheCareerDoor_StandsOnlyOverACareerFeature()
    {
        Assert.DoesNotContain(Plain().Rows, r => r.Key == OriginalShell.CareerKey);
        Assert.Contains(Shell(out _).Rows, r => r.Key == OriginalShell.CareerKey);
    }

    [Fact]
    public void ATypedName_StartsAGangOntoTheBridge()
    {
        var shell = Shell(out var career);
        Press(shell, OriginalShell.CareerKey);
        Assert.Equal(OriginalScreen.CareerRoster, shell.Screen);
        Assert.False(Row(shell, OriginalCareerScreen.StartKey).Enabled);

        Press(shell, OriginalCareerScreen.NameKey);
        Assert.True(shell.CapturingText);
        shell.Step(new MenuCommands { Typed = "Rusty Gullz" });
        shell.Step(new MenuCommands { Erase = true });
        shell.Step(new MenuCommands { Typed = "s" });
        Assert.Equal("Rusty Gulls", shell.Career!.Draft);
        Assert.True(Row(shell, OriginalCareerScreen.StartKey).Enabled);

        Press(shell, OriginalCareerScreen.StartKey);

        Assert.Equal(OriginalScreen.CareerBridge, shell.Screen);
        Assert.Equal("Rusty Gulls", career.Current!.Name);
        Assert.Equal(new[] { "Rusty Gulls" }, career.Careers);
    }

    [Fact]
    public void TheBridge_ReachesEveryPageAndWaits()
    {
        var shell = Bridge(out var career);

        Press(shell, OriginalCareerScreen.WaitKey);
        Assert.Equal(1, career.Current!.Day);

        foreach (var (door, page) in new[]
        {
            (OriginalCareerScreen.ContractsKey, OriginalScreen.CareerContracts),
            (OriginalCareerScreen.HangarKey, OriginalScreen.CareerHangar),
            (OriginalCareerScreen.LedgerKey, OriginalScreen.CareerLedger),
        })
        {
            Press(shell, door);
            Assert.Equal(page, shell.Screen);
            Assert.NotEmpty(shell.Compose().Lines);
            Press(shell, OriginalCareerScreen.PageBackKey);
            Assert.Equal(OriginalScreen.CareerBridge, shell.Screen);
        }

        Press(shell, OriginalCareerScreen.BridgeBackKey);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.Null(career.Current);
    }

    [Fact]
    public void TheBoard_TakesOneContractAndFlyWaitsForTheLaunch()
    {
        var shell = Bridge(out var career);
        Press(shell, OriginalCareerScreen.ContractsKey);
        Assert.False(Row(shell, OriginalCareerScreen.FlyKey).Enabled);
        Assert.False(Row(shell, OriginalCareerScreen.AbandonKey).Enabled);
        int first = career.Current!.Board.Offers[0].Id;

        Press(shell, OriginalCareerScreen.OfferRowPrefix + "0");

        Assert.Equal(first, career.Current.ActiveContract!.Id);
        Assert.All(shell.Rows.Where(r => r.Key.StartsWith(OriginalCareerScreen.OfferRowPrefix, StringComparison.Ordinal)),
            r => Assert.False(r.Enabled));
        Press(shell, OriginalCareerScreen.AbandonKey);
        Assert.Null(career.Current.ActiveContract);
    }

    [Fact]
    public void TheHangar_RepairsAWornPlane()
    {
        var shell = Bridge(out var career);
        Press(shell, OriginalCareerScreen.HangarKey);
        Assert.True(career.Current!.Planes[0].NeedsRepair);

        Press(shell, OriginalCareerScreen.PlaneRowPrefix + "0");

        Assert.False(career.Current.Planes[0].NeedsRepair);
        Assert.False(Row(shell, OriginalCareerScreen.PlaneRowPrefix + "0").Enabled);
    }

    [Fact]
    public void Scuttling_AsksFirstAndDeletesTheGang()
    {
        var shell = Bridge(out var career);
        Press(shell, OriginalCareerScreen.ScuttleKey);
        Assert.Equal(OriginalScreen.CareerBridge, shell.Screen);
        Assert.NotNull(career.Current);

        Press(shell, OriginalShell.DialogYesKey);

        Assert.Equal(OriginalScreen.CareerRoster, shell.Screen);
        Assert.Empty(career.Careers);
    }

    [Fact]
    public void AGangOnTheRoster_ContinuesOntoTheBridge()
    {
        var shell = Bridge(out var career);
        Press(shell, OriginalCareerScreen.BridgeBackKey);
        Press(shell, OriginalShell.CareerKey);

        Press(shell, OriginalCareerScreen.GangRowPrefix + "0");

        Assert.Equal(OriginalScreen.CareerBridge, shell.Screen);
        Assert.Equal("Rusty Gulls", career.Current!.Name);
    }

    private static OriginalShell Bridge(out CareerFeature career)
    {
        var shell = Shell(out career);
        Press(shell, OriginalShell.CareerKey);
        Press(shell, OriginalCareerScreen.NameKey);
        shell.Step(new MenuCommands { Typed = "Rusty Gulls" });
        Press(shell, OriginalCareerScreen.StartKey);
        return shell;
    }

    private static OriginalShell Plain()
    {
        var setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, _ => null);
    }

    private static OriginalShell Shell(out CareerFeature career)
    {
        var store = new CareerStore(Path.Combine(TestData.TempDir(), "Careers"));
        career = new CareerFeature(() => store, newSeed: () => 7);
        var setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, _ => null, career: career);
    }

    private static OriginalRow Row(OriginalShell shell, string key) => shell.Rows.Single(r => r.Key == key);

    private static void Press(OriginalShell shell, string key)
    {
        var row = Row(shell, key);
        float x = row.X + 2f;
        float y = row.Y + (row.Height / 2f);
        shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, true, true) });
        shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, false, false) });
    }
}

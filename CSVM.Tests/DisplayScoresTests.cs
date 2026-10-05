using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using CSVM.Bindings;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.UI.Menu.Original;
using CSVM.UI.Overlays;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The held Display Scores: the appended action and its shipped controls, a saved keymap
/// that predates it, and its place on the Original controls page. Then the standings it shows: a
/// race's and a Dogfight's lines in both looks, and nothing where no mode keeps scores.</summary>
public class DisplayScoresTests
{
    private static readonly DeviceId Pad = DeviceId.Joypad("030000004c050000c405000000010000");

    // The install's three header strings, quoted with their padding as messages.json holds them.
    private static readonly OriginalScoresWords Shipped = new(
        "  Player                       ", "  Player/Team                  ", "score             ");

    [Fact]
    public void TheActionIsAppendedAfterEveryMemberThatAlreadyExisted()
    {
        var all = Enum.GetValues<InputAction>();
        Assert.Equal(91, (int)InputAction.LookUpRightRear);
        Assert.Equal(92, (int)InputAction.DisplayScores);
        Assert.Equal(InputAction.DisplayScores, all.Max());
        Assert.Equal(all.Length - 1, (int)InputAction.DisplayScores);
    }

    [Fact]
    public void ItShipsOnTabAndThePadsBackButtonAsAFlightActionNothingElseHolds()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var tab = new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Tab));
        var back = new Binding(Pad, BindingControl.Button((int)JoyButton.Back));

        Assert.Equal(InputContext.Flight, DefaultBindings.ContextOf(InputAction.DisplayScores));
        Assert.Equal(new[] { tab, back }, map.Bindings(InputAction.DisplayScores));
        Assert.Equal(new[] { InputAction.DisplayScores }, map.OwnersOf(tab));
        Assert.Equal(new[] { InputAction.DisplayScores }, map.OwnersOf(back));
        Assert.Equal("Display Scores (Multiplayer Only)", BindingLabels.Name(InputAction.DisplayScores));
    }

    /// <summary>A keymap written before the action existed names every other flight action. Each of
    /// those rows reads back as saved, a rebind included, and the new action takes its default.
    /// </summary>
    [Fact]
    public void AKeymapSavedBeforeTheActionKeepsEveryRowAndTheActionTakesItsDefault()
    {
        var saved = BindingProfile.Defaults(Pad, readsKeyboard: true);
        saved.Map(InputContext.Flight).Assign(InputAction.FireGuns, new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Z)));
        var json = JsonNode.Parse(BindingStore.Serialize(1, saved))!;
        Assert.True(json["contexts"]!["flight"]!.AsObject().Remove(nameof(InputAction.DisplayScores)));

        var loaded = BindingStore.Deserialize(json.ToJsonString(), Pad, readsKeyboard: true);
        foreach (var action in DefaultBindings.ActionsIn(InputContext.Flight).Where(a => a != InputAction.DisplayScores))
        {
            Assert.Equal(saved.Map(InputContext.Flight).Bindings(action).ToArray(),
                loaded.Map(InputContext.Flight).Bindings(action).ToArray());
        }

        Assert.Equal(DefaultBindings.MapFor(InputContext.Flight, Pad).Bindings(InputAction.DisplayScores).ToArray(),
            loaded.Map(InputContext.Flight).Bindings(InputAction.DisplayScores).ToArray());
    }

    /// <summary>A saved row that already put Tab on another action keeps it there; the new action
    /// gives the key up and keeps the pad's Back.</summary>
    [Fact]
    public void ASavedRowOnTabKeepsTheKeyAndTheActionGivesItUp()
    {
        const string json = "{\"version\": 1, \"player\": 1, \"contexts\": {\"flight\": {\"TargetNearest\": [\"keyboard/key:Tab\"]}}}";
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);
        var tab = new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Tab));

        Assert.Equal(new[] { InputAction.TargetNearest }, loaded.OwnersOf(tab));
        Assert.Equal(new[] { new Binding(Pad, BindingControl.Button((int)JoyButton.Back)) },
            loaded.Bindings(InputAction.DisplayScores));
    }

    /// <summary>The Other tab carries it where the original's own Other page does, between
    /// Pause/Quit/Objectives and Chat to Everyone (<c>OriginalScreenshots/Keybinds Other.png</c>).
    /// </summary>
    [Fact]
    public void TheOtherTabCarriesItBetweenPauseAndChatAsTheOriginalsPageDoes()
    {
        var other = OriginalKeysPage.ControlTabs[6].Rows.Select(r => r.Action).ToList();
        Assert.Equal(
            new[]
            {
                InputAction.AutoLand, InputAction.Nitro, InputAction.Respawn, InputAction.Pause,
                InputAction.DisplayScores, InputAction.ChatEveryone, InputAction.ChatTeam,
                InputAction.ToggleGraphicsMode,
            },
            other.Take(8).ToArray());
    }

    [Fact]
    public void AFreeForAllReadsAsTheOriginalsNameAndScoreColumnsThenKillsAndDeathsByScore()
    {
        var match = new VersusMatch(3, killTarget: 0, timeLimit: 0f);
        match.RegisterKill(1, 0);
        match.RegisterKill(1, 0);
        match.RegisterKill(2, 0);
        match.RegisterDeath(0);
        var names = new[] { "Laeresh", "A callsign far too long to fit", "P3" };

        var lines = OriginalScoresText.Dogfight(match, seat => names[seat], seat => 2, Shipped);

        Assert.Equal(
            new[]
            {
                $"{"  Player",-21} {"score",-7} {"kills",-6} deaths",
                $"{"A callsign far too lo",-21} {"2",-7} {"2",-6} 0",
                $"{"P3",-21} {"1",-7} {"1",-6} 0",
                $"{"Laeresh",-21} {"-1",-7} {"0",-6} 4",
            },
            lines.Select(l => l.Text).ToArray());
        Assert.All(lines, l => Assert.Equal(0, l.Flag));
    }

    /// <summary>A team match puts each team's line over its pilots, indented by one. The pilot carrying
    /// a flag is marked with that flag's team.</summary>
    [Fact]
    public void ATeamMatchHeadsEachTeamsPilotsWithItsTotalAndMarksAFlagCarrier()
    {
        var match = new VersusMatch(3, killTarget: 0, timeLimit: 0f);
        match.AssignTeams(new[] { 1, 2, 1 }, new Dictionary<int, string> { [1] = "Red Skulls", [2] = "Hell's Angels" });
        match.RegisterKill(0, 1);
        match.RegisterKill(2, 1);
        match.RegisterKill(2, 1);

        var lines = OriginalScoresText.Dogfight(match, seat => $"pilot{seat}", seat => seat == 2 ? 2 : 0, Shipped);

        Assert.Equal(
            new[]
            {
                $"{"  Player/Team",-21} {"score",-7} {"kills",-6} deaths",
                "Red Skulls (Team Score: 3)",
                $"{" pilot2",-21} {"2",-7} {"2",-6} 0",
                $"{" pilot0",-21} {"1",-7} {"1",-6} 0",
                "Hell's Angels (Team Score: 0)",
                $"{" pilot1",-21} {"0",-7} {"0",-6} 3",
            },
            lines.Select(l => l.Text).ToArray());
        Assert.Equal(new[] { 0, 0, 2, 0, 0, 0 }, lines.Select(l => l.Flag).ToArray());
    }

    [Fact]
    public void TheHudHoldsEighteenLinesHeaderIncluded()
    {
        var match = new VersusMatch(25, killTarget: 0, timeLimit: 0f);
        Assert.Equal(OriginalScoresText.MaxLines, OriginalScoresText.Dogfight(match, seat => $"P{seat + 1}", null, Shipped).Count);
        Assert.Equal(18, OriginalScoresText.MaxLines);
    }

    [Fact]
    public void ARaceReadsAsPlacePilotAircraftBestGapAndRunsInBothLooks()
    {
        var race = new StuntRace(60f, 2);
        race.Add(0, "Bloodhawk");
        race.Add(1, "Kestrel");
        race.BeginOpening(0f);
        FlyRun(race, 0, 6f, 12.5f);
        FlyRun(race, 1, 4f, 10f);
        Assert.True(race.RunStarted(0));

        var table = ScoresTable.Race(race);
        Assert.Equal(new[] { "", "PILOT", "AIRCRAFT", "BEST", "GAP", "RUNS" }, table.Headers);
        Assert.Equal(new[] { "1st", "P2", "Kestrel", "0:10.0", "", "1/1" }, table.Rows[0].Cells);
        Assert.Equal(new[] { "2nd", "P1", "Bloodhawk", "0:12.5", "+2.5", "1/2" }, table.Rows[1].Cells);
        Assert.Equal(new[] { 1, 0 }, table.Rows.Select(r => r.Seat).ToArray());

        var lines = OriginalScoresText.Race(race, Shipped).Select(l => l.Text).ToArray();
        Assert.Equal(
            new[]
            {
                $"{"  Player",-21} {"aircraft",-12} {"best",-10} {"gap",-9} runs",
                $"{"1st P2",-21} {"Kestrel",-12} {"0:10.0",-10} {"",-9} 1/1",
                $"{"2nd P1",-21} {"Bloodhawk",-12} {"0:12.5",-10} {"+2.5",-9} 1/2",
            },
            lines);
    }

    [Fact]
    public void ADogfightTableLeadsWithTheTeamsInTheBoardsColumns()
    {
        var match = new VersusMatch(2, killTarget: 0, timeLimit: 0f);
        match.AssignTeams(new[] { 1, 2 }, new Dictionary<int, string> { [1] = "Red", [2] = "Blue" });
        match.RegisterKill(1, 0);

        var table = ScoresTable.Dogfight(match, seat => $"P{seat + 1}");
        Assert.Equal(new[] { "", "PILOT", "SCORE", "KILLS", "DEATHS" }, table.Headers);
        Assert.Equal(
            new[] { "#1 Blue 1 1 0", "#2 Red 0 0 1", "#1 P2 1 1 0", "#2 P1 0 0 1" },
            table.Rows.Select(r => string.Join(" ", r.Cells)).ToArray());
        Assert.Equal(new[] { true, true, false, false }, table.Rows.Select(r => r.Team).ToArray());
        Assert.Equal(new[] { -1, -1, 1, 0 }, table.Rows.Select(r => r.Seat).ToArray());
    }

    /// <summary>Solo and campaign flights keep no scores, so the source shows nothing in either look.
    /// </summary>
    [Fact]
    public void ASourceWithNoRaceAndNoDogfightShowsNothing()
    {
        var source = new ScoresSource();
        Assert.False(source.HasScores);
        Assert.Null(source.Table());
        Assert.Empty(source.OriginalLines());
        Assert.True(new ScoresSource { Match = new VersusMatch(2) }.HasScores);
    }

    [Fact]
    public void TheHeaderWordsComeFromTheMessageTableAndFallBackWord()
    {
        var table = Messages.Parse("{\"entries\": [{\"key\": \"MSG_MPHUD_PLAYER\", \"id\": 7008, \"value\": \"  Spieler\"}]}");
        var words = OriginalScoresWords.From(table);
        Assert.Equal("  Spieler", words.Player);
        Assert.Equal(OriginalScoresWords.Fallback.PlayerTeam, words.PlayerTeam);
        Assert.Equal(OriginalScoresWords.Fallback, OriginalScoresWords.From(null));
    }

    // A whole run over two zones at the given run times, the second its finish.
    private static void FlyRun(StuntRace race, int index, float first, float finish)
    {
        Assert.True(race.RunStarted(index));
        race.ZoneCleared(index, 0, first);
        race.ZoneCleared(index, 1, finish);
        race.RunFinished(index, finish);
    }
}

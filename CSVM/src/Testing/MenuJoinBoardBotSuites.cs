using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>A local Dogfight's bot rows on the join board, over the install's decoded layout and
/// string tables. The board adds and edits them. The Dogfight screen counts them toward its
/// two-pilot minimum and carries them on its launch. The board is pressed in its own authored space
/// through the shell, as the pointer reaches it.</summary>
internal static class MenuJoinBoardBotSuites
{
    [Suite("menu-join-board-bots",
        "The join board's bot rows over the install's decoded layout: a lone seat's Dogfight waits for "
        + "a second pilot and its hint offers a bot; Add Bot lists a row named from the shipped pilot "
        + "names; the row's Edit Bot panel puts it on a stock Fury at ace; Fill to fills the field to "
        + "its count; the Dogfight screen's strip names the bots, FLY stands for the one seat, and the "
        + "launch carries the seat and every bot row in order")]
    internal static void JoinBoardBots(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.MessagesPath, $"message table");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var registry = new PresentationRegistry();
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "menu-join-board-bots");
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true })
        {
            Planes = planes,
        });
        var host = new MenuHost(registry, new SilentAudio(), _ => { });
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot);
        host.AddSeat(new MenuIdleSource());
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original");
            host.Show(MenuReturnDestination.TopLevel);
            if ((host.Active as OriginalPresentation)?.Shell is not { } shell)
            {
                ctx.Check(false, $"Original stands at the top level with its shell ({host.Active?.Id})");
                return;
            }

            var setup = host.Features.Get<PlayerSetupFeature>();
            ctx.Check(setup.Seats.Count == 1 && setup.Bots.Count == 0,
                $"the menu opens on one seat and no bot row ({setup.Seats.Count} seat(s), {setup.Bots.Count} bot(s))");
            LoneSeatWaits(ctx, shell);
            EditOnTheBoard(ctx, shell, setup);
            FlyWithTheBots(ctx, shell, setup);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "menu-join-board-bots");
        }
    }

    // ABLE-TO-FAIL CONTROL for the launch below: the same seat and pick with no bot row cannot fly.
    private static void LoneSeatWaits(TestContext ctx, OriginalShell shell)
    {
        Pick(shell, ctx.Chapter);
        ctx.Check(!Row(shell, OriginalShell.FlyKey).Enabled && HasLine(shell, "or a bot, from the JOIN BOARD"),
            $"ABLE-TO-FAIL CONTROL: one seat alone cannot fly a Dogfight, and the hint offers a bot (FLY {Row(shell, OriginalShell.FlyKey).Enabled})");
    }

    private static void EditOnTheBoard(TestContext ctx, OriginalShell shell, PlayerSetupFeature setup)
    {
        shell.JoinBoard.Open();
        Press(shell, OriginalJoinBoard.AddBotKey);
        var pool = BotSeats.CallsignPool(Messages.Load(ctx.MessagesPath));
        var first = setup.Bots.Rows.FirstOrDefault();
        ctx.Check(setup.Bots.Count == 1 && pool.Contains(first.Callsign) && first.RandomPlane && first.Skill == NetBotSkill.Veteran,
            $"Add Bot lists one row on a Random plane at veteran, named from the shipped pilot names ('{first.Callsign}', {pool.Count} in the pool)");
        ctx.Check(shell.Rows.Any(r => r.Key == OriginalBotPanel.RowKey(0) && r.Label == first.Callsign),
            $"and the Bots block draws its row");

        Press(shell, OriginalBotPanel.RowKey(0));
        ctx.Check(shell.JoinBoard.PickedBot == first.Id && shell.FocusedKey == OriginalJoinBoard.BotNameKey,
            $"its row's press opens Edit Bot on it, the callsign box focused ({shell.FocusedKey})");
        int fury = StockAirframes.IdOf("player_fury") ?? -1;
        Press(shell, OriginalJoinBoard.BotPlaneKey);
        Press(shell, OriginalJoinBoard.BotPlaneKey + ":" + (fury + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Press(shell, OriginalJoinBoard.BotSkillKey);
        Press(shell, OriginalJoinBoard.BotSkillKey + ":2");
        var edited = setup.Bots.ById(first.Id);
        ctx.Check(edited is { Airframe: var airframe, Skill: NetBotSkill.Ace } && airframe == fury,
            $"its plane and skill lists put it on a stock Fury at ace ({edited?.Airframe}, {edited?.Skill})");
        Press(shell, OriginalJoinBoard.BotDoneKey);
        ctx.Check(shell.JoinBoard.PickedBot < 0 && HasLine(shell, "ARTICLES OF THE CREW"),
            $"Accept lets it go, the articles back in the panel's place");

        while (shell.JoinBoard.FillCount > 4)
        {
            Press(shell, OriginalJoinBoard.FillArrowPrefix + "-");
        }

        Press(shell, OriginalJoinBoard.FillKey);
        ctx.Check(setup.FieldPilots == 4 && setup.Bots.Count == 3 && setup.Bots.Rows[0].Id == first.Id,
            $"Fill to 4 adds rows until the seat and the bots make four, keeping the edited row first ({setup.FieldPilots} pilots)");
        Press(shell, OriginalJoinBoard.ContinueKey);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"CONTINUE leaves the board ({shell.Screen})");
    }

    private static void FlyWithTheBots(TestContext ctx, OriginalShell shell, PlayerSetupFeature setup)
    {
        Pick(shell, ctx.Chapter);
        ctx.Check(HasLine(shell, OriginalShell.BotStripLine(setup.Bots.Count)) && HasLine(shell, "FLY when ready"),
            $"the Dogfight strip names the bots and the hint says FLY");
        var fly = Row(shell, OriginalShell.FlyKey);
        ctx.Check(fly.Enabled, $"FLY stands for the one seat beside its bots");
        var launch = Click(shell, fly).Exit as LaunchExit;
        ctx.Check(launch is { Mode: MenuMode.Versus, Seats.Count: 1, Net: null } && launch.Chapter == ctx.Chapter,
            $"FLY launches a local Dogfight on {ctx.Chapter} with one seat ({launch?.Mode}, {launch?.Seats.Count} seat(s), {launch?.Chapter})");
        var bots = launch?.Bots ?? Array.Empty<VsBotEntry>();
        ctx.Check(bots.Select(b => b.Callsign).SequenceEqual(setup.Bots.Rows.Select(b => b.Callsign))
                  && bots.Count == 3 && bots[0] is { Plane: "player_fury", Skill: NetBotSkill.Ace } && bots.Skip(1).All(b => b.Plane == null),
            $"and carries every bot row in order, the edited one on its Fury at ace and the rest Random ({string.Join(", ", bots.Select(b => $"{b.Callsign}:{b.Plane ?? "random"}:{b.Skill}"))})");
    }

    // The Dogfight screen with a map and seat 0's first aircraft picked.
    private static void Pick(OriginalShell shell, string chapter)
    {
        shell.Open(OriginalScreen.Dogfight);
        Press(shell, chapter);
        Press(shell, OriginalShell.AirframeKey(0));
    }

    private static OriginalRow Row(OriginalShell shell, string key) => shell.Rows.Single(r => r.Key == key);

    private static bool HasLine(OriginalShell shell, string text) =>
        shell.Compose().Lines.Any(l => l.Text.Contains(text, StringComparison.Ordinal));

    private static void Press(OriginalShell shell, string key) => Click(shell, Row(shell, key));

    // A click in the row's own authored space, the pointer pressed and released on it.
    private static OriginalStep Click(OriginalShell shell, OriginalRow row)
    {
        float x = row.X + Math.Min(4f, row.Width / 2f);
        float y = row.Y + (row.Height / 2f);
        shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, true, true) });
        return shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, false, false) });
    }

    private sealed class SilentAudio : IMenuAudio
    {
        public void Cue(MenuCue cue)
        {
        }

        public void BeginNarration(string wavName)
        {
        }

        public void EndNarration()
        {
        }

        public void PreviewMix(AudioLevels levels, MenuMixLevel moved)
        {
        }

        public void EndMixPreview()
        {
        }
    }
}

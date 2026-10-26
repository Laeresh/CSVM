using System;
using System.IO;
using System.Linq;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Overlays;
using Godot;

namespace CSVM.Testing;

/// <summary>The held Display Scores on two real seats built through the session's roster. Each seat
/// has a pane of its own with the chat panel and both looks of the scores overlay. A race's and a
/// Dogfight's standings stand in the holding seat's pane alone while the action is held. The chat
/// lines and the top status lines step aside for them, and all go back on release.</summary>
internal static class DisplayScoresSuites
{
    [Suite("stunt-race-display-scores",
        "two race seats over C1/IA1's zones, each in its own pane with a chat line up: nothing shows "
        + "before a hold; P1 holding Display Scores puts the race's standings in P1's pane alone, in "
        + "the original's HUD text, and hides P1's chat lines and its run status "
        + "and leaderboard lines while P2's stay; "
        + "the release takes both back; a seat in no race shows nothing however long it holds")]
    internal static void StuntRaceDisplayScores(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        var chapterGamez = ctx.ChapterGamez("C1");
        ctx.RequireData(chapterGamez, $"C1 gamez");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");
        ctx.RequireMessages();
        var messages = ctx.Data.Messages;
        var zones = StuntMission.Load(chapterGamez!, missionZrdr, messages);
        if (zones is not { TotalCount: >= 1 })
        {
            throw new SuiteSkippedException($"C1/IA1 loads no danger zone");
        }

        var race = new StuntRace(300f, zones.TotalCount);
        var source = new ScoresSource { Race = race, Words = OriginalScoresWords.From(messages) };
        using var seats = TwoSeats.Build(ctx, new HumanRosterBindings
        {
            RigCount = 2,
            StuntZones = zones,
            Race = race,
        });
        if (seats.Pilot(0) is not { } p1 || seats.Pilot(1) is not { } p2)
        {
            ctx.Check(false, $"both seats were built");
            return;
        }

        race.BeginOpening(0f);
        HeldScores(ctx, seats, source, p1, p2, "race",
            lines => lines.Count == 3 && lines[0].StartsWith("  Player", StringComparison.Ordinal)
                && lines[1].StartsWith("1st P1", StringComparison.Ordinal) && lines[2].StartsWith("2nd P2", StringComparison.Ordinal),
            seat => seat.PilotHud.StuntRun is { StatusShown: true });

        // A seat in no race has no scores to show: the overlay stays down, the chat and the status lines stay up.
        p1.Race = null;
        p1.HoldActionForTest(InputAction.DisplayScores, true);
        var solo = seats.Attach(0, new ScoresSource());
        solo.Refresh();
        ctx.Check(!p1.ScoresShown && !solo.Shown && seats.Chat(0).LinesShown && p1.PilotHud.StuntRun is { StatusShown: true },
            $"a seat in no race holding the action shows nothing: shown={p1.ScoresShown}/{solo.Shown}, chat up={seats.Chat(0).LinesShown}, status up={p1.PilotHud.StuntRun?.StatusShown}");
        ctx.Note($"held race standings in the holder's pane alone, gone on release");
    }

    [Suite("dogfight-display-scores",
        "two Dogfight seats, each in its own pane with a chat line up: nothing shows before a hold; "
        + "P2 holding Display Scores puts the match's scores in P2's pane alone, in the original's "
        + "name and score columns with the remake's kills and deaths after them, and hides P2's chat "
        + "lines and match line while "
        + "P1's stay; the release takes both back")]
    internal static void DogfightDisplayScores(TestContext ctx)
    {
        ctx.RequireMessages();
        var messages = ctx.Data.Messages;
        var match = new VersusMatch(2, killTarget: 0, timeLimit: 0f);
        match.RegisterKill(1, 0);
        var source = new ScoresSource { Match = match, Words = OriginalScoresWords.From(messages) };
        using var seats = TwoSeats.Build(ctx, new HumanRosterBindings { RigCount = 2, VersusMatch = match });
        if (seats.Pilot(0) is not { } p1 || seats.Pilot(1) is not { } p2)
        {
            ctx.Check(false, $"both seats were built");
            return;
        }

        // The rule VersusDirector writes on every seat of a match.
        p1.Match = match;
        p2.Match = match;
        HeldScores(ctx, seats, source, p2, p1, "Dogfight",
            lines => lines.Count == 3
                && lines[1] == OriginalScoresText.Cell("P2", OriginalScoresText.NameWidth) + " " + OriginalScoresText.Cell("1", OriginalScoresText.ScoreWidth) + " " + OriginalScoresText.Cell("1", 6) + " 0"
                && lines[2] == OriginalScoresText.Cell("P1", OriginalScoresText.NameWidth) + " " + OriginalScoresText.Cell("0", OriginalScoresText.ScoreWidth) + " " + OriginalScoresText.Cell("0", 6) + " 1",
            seat => seat.VersusStatusLine is { StatusShown: true });
        ctx.Note($"held Dogfight scores in the holder's pane alone, gone on release");
    }

    // The hold and the release on one seat, with the other seat's pane as the control. The scores
    // show only in the holder's pane, and that pane's chat lines and top status lines are away while
    // it lasts.
    private static void HeldScores(TestContext ctx, TwoSeats seats, ScoresSource source, FlightController holder,
        FlightController other, string mode, Func<System.Collections.Generic.IReadOnlyList<string>, bool> originalLines,
        Func<FlightController, bool> statusShown)
    {
        int held = holder.PlayerIndex, idle = other.PlayerIndex;
        var panes = new[] { seats.Attach(0, source), seats.Attach(1, source) };
        void Refresh()
        {
            foreach (var overlay in panes)
                overlay.Refresh();
        }

        Refresh();
        ctx.Check(panes.All(o => !o.Shown && o.Lines.Count == 0) && seats.Chat(0).LinesShown && seats.Chat(1).LinesShown
                && statusShown(holder) && statusShown(other),
            $"before any hold no pane shows the {mode} scores and both chat panels and status lines are up: status {statusShown(holder)}/{statusShown(other)}");

        holder.HoldActionForTest(InputAction.DisplayScores, true);
        other.HoldActionForTest(InputAction.DisplayScores, false);
        Refresh();
        ctx.Check(holder.ScoresShown && panes[held].Shown && !panes[idle].Shown,
            $"P{held + 1}'s hold shows the {mode} scores in its own pane alone: P{held + 1} {panes[held].Shown}, P{idle + 1} {panes[idle].Shown}");
        ctx.Check(originalLines(panes[held].Lines),
            $"in the original's HUD text: {string.Join(" | ", panes[held].Lines)}");
        ctx.Check(!seats.Chat(held).LinesShown && seats.Chat(idle).LinesShown,
            $"the holder's chat lines step aside and the other pane's stay: P{held + 1} up={seats.Chat(held).LinesShown}, P{idle + 1} up={seats.Chat(idle).LinesShown}");
        ctx.Check(!statusShown(holder) && statusShown(other),
            $"the holder's top status lines step aside and the other pane's stay: P{held + 1} up={statusShown(holder)}, P{idle + 1} up={statusShown(other)}");

        holder.HoldActionForTest(InputAction.DisplayScores, false);
        Refresh();
        ctx.Check(!holder.ScoresShown && panes.All(o => !o.Shown && o.Lines.Count == 0) && seats.Chat(held).LinesShown
                && statusShown(holder) && statusShown(other),
            $"the release takes the {mode} scores down and brings the chat and status lines back: status {statusShown(holder)}/{statusShown(other)}");
    }
    // Two seats built through the roster over C1, each in a pane of its own. Each pane carries the
    // chat panel the session gives it, with a line posted so the panel is up.
    internal sealed class TwoSeats : IDisposable
    {
        private readonly TextureArchive _textures;
        private readonly ProjectilePool _pool;
        private readonly SubViewport[] _panes;
        private readonly PlayerRig[] _rigs;
        private readonly FlightRoster _roster;
        private readonly ChatPanel[] _chats;

        private TwoSeats(TextureArchive textures, ProjectilePool pool, SubViewport[] panes, PlayerRig[] rigs,
            FlightRoster roster, ChatPanel[] chats)
        {
            _textures = textures;
            _pool = pool;
            _panes = panes;
            _rigs = rigs;
            _roster = roster;
            _chats = chats;
        }

        public static TwoSeats Build(TestContext ctx, HumanRosterBindings human)
        {
            ctx.RequireData(ctx.Data.Planes, $"planes gamez");
            ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
            TextureSet texturesPath = ctx.ChapterTextures("C1");
            ctx.RequireData(texturesPath, $"C1 textures");
            var planesGamez = ctx.Planes;
            var textures = new TextureArchive(texturesPath);
            var pool = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(pool);
            var panes = new[] { new SubViewport(), new SubViewport() };
            var rigs = new PlayerRig[2];
            for (int i = 0; i < 2; i++)
            {
                ctx.Host.AddChild(panes[i]);
                rigs[i] = new PlayerRig { Index = i, Camera = ctx.Camera, HudParent = panes[i], Viewport = panes[i] };
            }

            var spec = SessionSpec.Parse(new[] { "--no-pads" });
            var roster = new FlightRoster(FlightRosterPolicy.From(spec),
                new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
                new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host,
                SuiteConstants.AircraftResources(ctx, planesGamez, textures, ctx.Data.Messages, _ => new CamParams()),
                new FlightWorldBindings
                {
                    Projectiles = pool,
                    Gamez = planesGamez,
                    ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"),
                },
                new HumanRosterBindings
                {
                    RigCount = human.RigCount,
                    Rigs = rigs,
                    PauseState = new PauseState(),
                    StuntZones = human.StuntZones,
                    Race = human.Race,
                    VersusMatch = human.VersusMatch,
                }, new StuntRaceSuites.ApartStarts());
            roster.BuildPlayers(rigs);
            var chat = new FlightChat();
            chat.Post("P1: line up");
            var chats = new ChatPanel[2];
            for (int i = 0; i < 2; i++)
            {
                if (rigs[i].Controller is { } seat)
                {
                    seat.UseKeyboard = false;
                    seat.PadDevices = Array.Empty<int>();
                }

                chats[i] = ChatPanel.ForPane(chat, rigs[i]);
                panes[i].AddChild(chats[i]);
            }

            return new TwoSeats(textures, pool, panes, rigs, roster, chats);
        }

        public FlightController? Pilot(int seat) => _rigs[seat].Controller;

        public ChatPanel Chat(int seat) => _chats[seat];

        public ScoresOverlay Attach(int seat, ScoresSource source) =>
            ScoresOverlay.Attach(_rigs[seat], source);

        public void Dispose()
        {
            var built = _rigs.Select(rig => rig.Controller).Where(c => c != null).ToArray();
            _roster.ClearMembership();
            foreach (var controller in built)
                controller!.Free();
            foreach (var pane in _panes)
                pane.Free();
            _pool.Free();
            _textures.Dispose();
        }
    }
}

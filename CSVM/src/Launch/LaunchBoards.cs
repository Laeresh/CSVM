using System.Collections.Generic;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The whole-window boards the launcher stands with no flight in front of them, composed from the
/// launch's spec and the base paths. They are the load screen for the launch being built, the
/// blackboard over a live graphics switch, and the <c>--menu=pauseboard</c> doors. Those stand a
/// pause sheet alone for a capture. Stateless; the launcher parents what it is handed and owns when each goes
/// up and comes down. Module notes: docs/architecture/Launch.md.
/// </summary>
internal sealed class LaunchBoards(SessionSpec spec, BasePaths paths)
{
    /// <summary>What the load and pause screens call this flight. An Instant Action mission takes the
    /// wizard's own name for it ("Attacking a Zeppelin"), anything else its mode. ⚠ Not ModeName,
    /// that is the log file's internal tag ("fly", "stunt"), which is not a player's word.</summary>
    public static string Subject(SessionSpec launch)
    {
        if (launch.IaDef is { } def)
        {
            return Mech3.InstantAction.MissionTypeLabel(def.MissionType);
        }
        return launch.MissionType == DogfightMissionType.StuntRace ? DogfightLobby.StuntRaceName
            : launch.Versus ? "Dogfight" : "Free Flight";
    }

    /// <summary>The chapter and mode letter a Dogfight door's <c>:chapter:letter</c> suffix names.
    /// A suffix leaving either out names C1, or a Deathmatch with nobody on a team.</summary>
    public static (string Chapter, char Letter) MultiplayerDoor(string aid)
    {
        System.ArgumentNullException.ThrowIfNull(aid);
        var parts = aid.Split(':');
        return (
            parts.Length > 1 && parts[1].Length > 0 ? parts[1] : "C1",
            parts.Length > 2 && parts[2].Length > 0 ? parts[2][0] : 'd');
    }

    /// <summary>The load screen for the launch the spec describes. A campaign launch takes the
    /// original's chart sheet for the mission at <paramref name="missionSeq"/>, the spec's own
    /// position by default. Everything else takes its blackboard (docs/org/loading-screen.md).
    /// <paramref name="teamedLobby"/> picks a Dogfight's team dialog, and a door's
    /// <paramref name="briefingKey"/> names that dialog outright.</summary>
    public UI.Screens.LoadBoard LoadScreen(
        bool campaign, string? missionType, int? missionSeq, bool teamedLobby, string? briefingKey = null)
    {
        // The blackboard writes its dialog's own texts, and takes this heading only when no dialog
        // describes the flight. The chart sheet writes no words of ours at all.
        string subject = campaign ? string.Empty : LaunchSubject().ToUpperInvariant();
        var sheet = campaign
            ? CampaignLoadSheet(missionSeq ?? spec.CampaignMissionSeq ?? 0)
            : null;
        string? briefing = briefingKey
            ?? (campaign || missionType != null ? null : LaunchBriefing(teamedLobby));
        var board = UI.Screens.LoadBoard.Build(
            paths.DataRoot, paths.ZrdrPath, paths.Data.Messages, campaign, subject, missionType, sheet, briefing);
        if (briefing != null)
        {
            Log.Info("ui", $"load screen: {spec.Chapter} Dogfight reads {briefing}");
        }
        board.CaptureDir = spec.DebugLoad ?? string.Empty;
        return board;
    }

    /// <summary>The blackboard a live graphics switch stands over a flying world, headed
    /// <paramref name="subject"/>.</summary>
    public UI.Screens.LoadBoard SwitchScreen(string subject)
    {
        var board = UI.Screens.LoadBoard.Build(paths.DataRoot, paths.ZrdrPath, paths.Data.Messages, false, subject, null);
        board.CaptureDir = spec.DebugLoad ?? string.Empty;
        return board;
    }

    /// <summary>The <c>--menu=</c> pause doors, each standing its sheet under <paramref name="parent"/>:
    /// <c>pauseboard-ia</c>, <c>pauseboard-mp</c> and <c>pauseboard</c>. The pause sheet stands only
    /// while a mission is halted, so a shot of it needs a door of its own. Any other aid draws
    /// nothing.</summary>
    public void ShowPauseAid(string aid, Node parent)
    {
        // The campaign door's arguments name the mission and how many of its objectives are marked.
        // That is the whole of what the reference stills differ by.
        if (aid.StartsWith("pauseboard-ia", System.StringComparison.Ordinal))
        {
            var parts = aid.Split(':');
            ShowInstantActionPauseSheet(
                parent,
                parts.Length > 1 && parts[1].Length > 0 ? parts[1] : "C1",
                parts.Length > 2 && parts[2].Length > 0 ? parts[2] : "stunt_flying");
        }
        else if (aid.StartsWith("pauseboard-mp", System.StringComparison.Ordinal))
        {
            var (chapter, letter) = MultiplayerDoor(aid);
            ShowMultiplayerPauseSheet(parent, chapter, letter);
        }
        else if (aid.StartsWith("pauseboard", System.StringComparison.Ordinal))
        {
            var parts = aid.Split(':');
            ShowPauseSheet(
                parent,
                parts.Length > 1 && int.TryParse(parts[1], out int cm) ? cm : 1,
                parts.Length > 2 && int.TryParse(parts[2], out int done) ? done : 0);
        }
    }

    // A Dogfight's multiplayer dialog, from its chapter, its type and whether a lobby pilot joined a
    // team. The session's pause board resolves the same key off its seats. Null for anything else.
    private string? LaunchBriefing(bool teamedLobby) =>
        spec.Versus
            ? UI.Screens.LoadScreens.MultiplayerKey(spec.Chapter, spec.MissionType, teamedLobby)
            : null;

    // The chart sheet a story position resolves. It is the loading dialog the mission's storage
    // address names, that mission's objectives for the parchment, and the profile's memento.
    private UI.Screens.LoadSheet? CampaignLoadSheet(int seq)
    {
        if (CampaignMissionAt(seq) is not { } named)
        {
            Log.Warn("ui", $"load screen: cm_sequence names no mission at seq {seq}");
            return null;
        }

        string key = UI.Menu.EscapeDialog.CampaignKey(named.Campaign, named.Mission);
        var sheet = UI.Screens.LoadSheet.Load(
            paths.ZrdrPath, paths.Data.Messages,
            SessionPaths.MissionZrdr(paths.DataRoot, named.ChapterFolder, named.MissionFolder),
            key, SeatedMemento());
        if (sheet == null)
        {
            Log.Warn("ui",
                $"load screen: Loading.zrd has no sheet for {named.ChapterFolder}/{named.MissionFolder}");
            return null;
        }

        // The screen is torn down before the world appears, so this line is the only record of
        // which mission's chart a launch actually drew.
        string map = sheet.State.Map?.Bitmap ?? "-";
        Log.Info("ui",
            $"load screen: {named.ChapterFolder}/{named.MissionFolder} {key} map={map} objectives={sheet.Objectives.Count}");
        return sheet;
    }

    // The mission a sequence position names, or null where cm_sequence carries none.
    private CampaignMission? CampaignMissionAt(int seq)
    {
        CampaignMission? found = null;
        foreach (var mission in Mech3.CampaignSequence.Load(paths.ZrdrPath))
        {
            if (mission.Seq == seq)
            {
                found = mission;
            }
        }

        return found;
    }

    // The Original presentation's pause sheet over the whole window, standing on its own with no
    // mission behind it. The objectives are the named mission's own, and the ownship icon sits on
    // its authored PLAYER_INIT spawn, so nothing on the screen is invented. There is no world, so
    // the zeppelin icon is absent (docs/org/pause-screen.md).
    private void ShowPauseSheet(Node parent, int ordinal, int completed)
    {
        int last = System.Math.Max(0, Mech3.CampaignSequence.Load(paths.ZrdrPath).Count - 1);
        int seq = System.Math.Clamp(ordinal - 1, 0, last);
        if (CampaignMissionAt(seq) is not { } named)
        {
            Log.Warn("ui", $"pause aid: cm_sequence names no mission at seq {seq}");
            return;
        }

        string missionZrdr = SessionPaths.MissionZrdr(paths.DataRoot, named.ChapterFolder, named.MissionFolder);
        var sheet = UI.Screens.PauseSheet.Load(
            paths.ZrdrPath, paths.Data.Messages,
            UI.Menu.EscapeDialog.CampaignKey(named.Campaign, named.Mission), instantAction: false);
        if (sheet == null)
        {
            Log.Warn("ui", $"pause aid: no escape.zrd sheet for {named.ChapterFolder}/{named.MissionFolder}");
            return;
        }

        var readout = PauseAidReadout(sheet, missionZrdr, completed);
        StandPauseSheet(parent, sheet, () => readout);
        Log.Info("ui",
            $"pause aid: {named.ChapterFolder}/{named.MissionFolder} sheet with {completed} objective(s) marked");
    }

    // The Instant Action sortie's own pause sheet with no sortie behind it: the blackboard its
    // chapter and mission type resolve. It carries no map, memento or parchment, so it needs no
    // readout at all (docs/org/pause-screen.md).
    private void ShowInstantActionPauseSheet(Node parent, string chapter, string missionType)
    {
        if (UI.Screens.LoadScreens.LetterFor(missionType) is not { } letter)
        {
            Log.Warn("ui", $"pause aid: '{missionType}' is no Instant Action mission type");
            return;
        }

        string key = UI.Menu.EscapeDialog.InstantActionKey(
            Mech3.CampaignSequence.ChapterNumber(chapter), letter);
        var sheet = UI.Screens.PauseSheet.Load(paths.ZrdrPath, paths.Data.Messages, key, instantAction: true);
        if (sheet == null)
        {
            Log.Warn("ui", $"pause aid: no ia_escape.zrd sheet for {chapter} {missionType} ({key})");
            return;
        }

        StandPauseSheet(parent, sheet, () => UI.Screens.PauseReadout.Empty);
        Log.Info("ui", $"pause aid: {chapter} {missionType} draws {sheet.State.Key}");
    }

    // A Dogfight's pause sheet with no match behind it, for the mode the letter names on that
    // chapter's environment. docs/org/pause-screen.md.
    private void ShowMultiplayerPauseSheet(Node parent, string chapter, char letter)
    {
        if (UI.Screens.LoadScreens.MultiplayerKey(chapter, letter) is not { } key)
        {
            Log.Warn("ui", $"pause aid: no lobby environment flies {chapter}");
            return;
        }

        var sheet = UI.Screens.PauseSheet.LoadMultiplayer(paths.ZrdrPath, paths.Data.Messages, key);
        if (sheet == null)
        {
            Log.Warn("ui", $"pause aid: no escape.zrd or Loading.zrd sheet for {chapter} ({key})");
            return;
        }

        StandPauseSheet(parent, sheet, () => UI.Screens.PauseReadout.Empty);
        Log.Info("ui", $"pause aid: {chapter} Dogfight draws {sheet.State.Key}");
    }

    // The sheet on the board layer under the parent, then paused, so it stands as a halted
    // mission's does.
    private void StandPauseSheet(Node parent, UI.Screens.PauseSheet sheet, System.Func<UI.Screens.PauseReadout> readout)
    {
        var pause = new Flight.Modes.PauseState();
        var board = OriginalPauseBoard.Build(
            pause, _ => new UI.Boards.MenuInput { Keyboard = true }, paths.DataRoot, sheet, readout);
        var layer = new CanvasLayer { Name = "pause_board_aid", Layer = UI.Boards.HudLayers.Board };
        layer.AddChild(board);
        parent.AddChild(layer);
        pause.TryToggle(0);
    }

    private UI.Screens.PauseReadout PauseAidReadout(UI.Screens.PauseSheet sheet, string missionZrdr, int completed)
    {
        var objectives = UI.Menu.BriefingObjectives.Load(
            Mech3.Zrdr.LoadFile(missionZrdr, "objectives.json"), paths.Data.Messages);
        var rows = new List<UI.Screens.PauseObjective>(objectives.Count);
        for (int i = 0; i < objectives.Count; i++)
        {
            rows.Add(new UI.Screens.PauseObjective(objectives[i].Text, i < completed));
        }

        var icons = new List<UI.Screens.PauseWorldIcon>();
        if (sheet.Shared.OwnShip.Length > 0
            && Flight.Modes.SpawnPoints.LoadPlayerInit(missionZrdr) is { } init)
        {
            // Through the readout's own conversion off a nose vector, never off the spawn's heading
            // degrees. Those are the mission data's yaw, which runs opposite the compass.
            var nose = new Basis(Vector3.Up, Mathf.DegToRad(init.Spawn.HeadingDeg)) * Vector3.Forward;
            if (UI.Screens.PauseReadout.Icon(
                sheet.Shared.OwnShip, init.Spawn.Position.X, init.Spawn.Position.Z,
                nose.X, nose.Z) is { } ship)
            {
                icons.Add(ship);
            }

            if (sheet.State.Map is { } map
                && !map.TryProject(init.Spawn.Position.X, init.Spawn.Position.Z, out _))
            {
                Log.Info("ui",
                    $"pause aid: the mission's spawn sits off the chart's window, so no ownship icon draws");
            }
        }

        return new UI.Screens.PauseReadout(rows, SeatedMemento(), icons);
    }

    // The picture the seated profile hangs, read back off the store the cabin's chooser writes.
    // This screen, a real pause and the cabin wall all draw the one name. A launch or a door with
    // no profile behind it draws the seeded keepsake (docs/org/pause-screen.md).
    private string SeatedMemento() =>
        CampaignMementos.BitmapFor(
            spec.CampaignProfile is { } name
                ? CampaignProfileStore.ForSession(spec.ProfilesDir).Load(name)
                : null);

    private string LaunchSubject() => Subject(spec);
}

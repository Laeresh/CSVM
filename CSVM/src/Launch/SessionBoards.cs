using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.InstantAction;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>The whole-window boards over one flight and the doors between them. The pause board is
/// the Original presentation's sheet where the mode has one, the Built-in board otherwise, under
/// its options leaf. The results boards are the race, the dogfight, the Instant Action wrap-up and
/// each pane's solo stunt scoreboard. It holds one menu reader per local player, and photo mode,
/// which suspends whichever board is up. The session builds it once its rigs and pause state stand
/// and hands in each mode's Restart; nothing here reads a session field.
/// Module entry: docs/architecture/Launch.md on src/Launch/SessionBoards.cs.</summary>
internal sealed class SessionBoards
{
    // Nathan Zachary's own zeppelin, the world node escape.zrd's shared MYZEP icon stands for. The
    // original looks this name up before deciding whether to draw the icon at all.
    private const string PirateZepNode = "piratezep";

    private readonly Inputs _in;
    // One menu reader per local player, built with that player's own pad binding, so a board menu
    // can be driven by its owner alone.
    private readonly MenuInput[] _menuInputs;
    // The whole-window boards photo mode has to get out of the way, and what they were showing
    // before it did. A session that builds no board registers none.
    private readonly List<Control> _boards = new();
    private readonly List<bool> _boardWasVisible = new();

    // The Original presentation's pause sheet while it is the board in use. Its parchment carries
    // the objectives, so the corner readout is not built beside it.
    private OriginalPauseBoard? _originalPause;
    // The pause board in use, whichever presentation composed it, and the options leaf that stands
    // over it while PREFERENCES is open. Null leaves both boards without that door.
    private Control? _pauseBoard;
    private PausePreferences? _pauseOptions;
    // Photo mode's three pieces, all null unless it is engaged: the hint/exit reader, the camera
    // holding the pane, and whose pane it is.
    private UI.Overlays.PhotoModeHud? _photoHud;
    private SpectatorCamera? _photoCamera;
    private FlightController? _photoPilot;

    /// <summary>The boards of one flight. Builds one menu reader per pane from
    /// <see cref="Inputs.PadAssignment"/>.</summary>
    public SessionBoards(Inputs inputs)
    {
        _in = inputs;
        _menuInputs = new MenuInput[Math.Max(1, inputs.Rigs.Count)];
        for (int i = 0; i < _menuInputs.Length; i++)
            _menuInputs[i] = MenuInput.ForSessionSeat(i, inputs.PadAssignment?[i]);
    }

    /// <summary>The dogfight board, null outside a Dogfight. A suite reads its menu through this.
    /// </summary>
    public ResultsBoard? DogfightBoard => _boards.OfType<VersusBoard>().FirstOrDefault();

    /// <summary>The Restart the pause board carries, null where it offers none. A suite reads it,
    /// and fires it, through this.</summary>
    public Action? PauseRestart => _originalPause?.Restart ?? (_pauseBoard as PauseBoard)?.Restart;

    /// <summary>Whether the pause board is the Original sheet, whose parchment already carries the
    /// mission's objectives.</summary>
    public bool SheetCarriesObjectives => _originalPause != null;

    /// <summary>The pause board, whichever presentation composes it, under the launcher's options
    /// leaf when it supplies one. Each pane's solo stunt scoreboard then joins the board list. The
    /// restart is the sheet's Restart row, null where none is offered.</summary>
    public void BuildPause(PauseSheetInputs sheetInputs, Action? restart)
    {
        var pauseState = _in.PauseState;
        // Built before the boards so each knows whether it has a PREFERENCES door at all. The
        // Original sheet draws its strip either way and leaves the press a no-op. Built-in's menu
        // leaves the row off rather than offering one that does nothing.
        var pauseOptions = _in.PauseOptions?.Invoke();
        Action? preferences = pauseOptions == null
            ? null
            : () => OpenPauseOptions(pauseState.OwnerPlayerIndex);
        Control pauseBoard;
        if (BuildOriginalPauseBoard(sheetInputs) is { } sheet)
        {
            sheet.Restart = restart;
            sheet.Exit = _in.Exit;
            sheet.Preferences = preferences;
            // Read at press time, not captured: the owner is whoever paused THIS time, and only
            // that player drives the cursor that reached this row.
            sheet.PhotoMode = () => EnterPhotoMode(pauseState.OwnerPlayerIndex);
            _originalPause = sheet;
            pauseBoard = sheet;
        }
        else
        {
            var builtIn = PauseBoard.Build(pauseState, exitsToMenu: _in.MenuDriven, InputFor);
            builtIn.Restart = restart;
            builtIn.Exit = _in.Exit;
            builtIn.Preferences = preferences;
            // Read at press time, not captured: the owner is whoever paused THIS time, and only
            // that player drives the cursor that reached this row.
            builtIn.PhotoMode = () => EnterPhotoMode(pauseState.OwnerPlayerIndex);
            pauseBoard = builtIn;
        }

        _boards.Add(pauseBoard);
        _pauseBoard = pauseBoard;
        AddLayer(pauseBoard, "pause_board");
        if (pauseOptions != null)
        {
            _pauseOptions = pauseOptions;
            pauseOptions.Closed += ClosePauseOptions;
            _boards.Add(pauseOptions);
            AddLayer(pauseOptions, "pause_options");
        }

        // The per-pane stunt scoreboards are built with their rigs (HumanFlightAdapter), so they
        // are collected here rather than at a construction site of their own.
        foreach (var rig in _in.Rigs)
        {
            if (rig.Controller?.Scoreboard is not StuntScoreboard scoreboard)
                continue;
            int owner = rig.Index;
            scoreboard.PhotoMode = () => EnterPhotoMode(owner);
            _boards.Add(scoreboard);
        }
    }

    /// <summary>The time-attack race's shared results board, one ranked row per pilot over the
    /// whole window. It is the Original presentation's lobby-scores board where that art and the
    /// string table are installed, the Built-in chrome board otherwise. Instant Action builds it too,
    /// since the race ends a multi-seat stunt run. <paramref name="zoneNames"/> are in course order.</summary>
    public Control BuildRaceBoard(StuntRace race, IReadOnlyList<string> zoneNames, string context,
        Action restart)
    {
        Control built;
        string exitLabel = StuntRaceBoard.ExitLabel(_in.MenuDriven);
        // Player 1: a results board reads _inputFor(0), so its cursor is P1's whoever won.
        if (OriginalRaceStrings() is { } strings)
        {
            var board = OriginalRaceBoard.Build(race, zoneNames, context, exitLabel, _in.PauseState,
                InputFor, _in.DataRoot, strings);
            board.Restart = restart;
            board.Exit = _in.Exit;
            board.PhotoMode = () => EnterPhotoMode(0);
            built = board;
        }
        else
        {
            var board = StuntRaceBoard.Build(race, zoneNames, context, exitsToMenu: _in.MenuDriven,
                _in.PauseState, InputFor);
            board.Restart = restart;
            board.Exit = _in.Exit;
            board.PhotoMode = () => EnterPhotoMode(0);
            built = board;
        }

        _boards.Add(built);
        AddLayer(built, "race_board");
        return built;
    }

    /// <summary>The match's shared results board, one layer over the whole window, since the match
    /// ends for everybody at once. The withheld line is what a board shows in place of a Restart
    /// its machine may not call, null where it may.</summary>
    public VersusBoard BuildDogfightBoard(VersusMatch match, string context, Action restart,
        string? restartWithheld)
    {
        var board = VersusBoard.Build(match, context, exitsToMenu: _in.MenuDriven, _in.PauseState, InputFor);
        board.Restart = restart;
        if (restartWithheld != null)
            board.RestartWithheld = restartWithheld;
        board.Exit = _in.Exit;
        // Player 1, for the same reason the race board is: the cursor is _inputFor(0)'s.
        board.PhotoMode = () => EnterPhotoMode(0);
        _boards.Add(board);
        AddLayer(board, "dogfight_board");
        return board;
    }

    /// <summary>Instant Action's in-flight wrap-up board, hidden until the director presents it.
    /// It covers the whole window on its own layer, since the mission ends for every human at
    /// once.</summary>
    public IIaWrapupBoard BuildIaWrapupBoard(string context)
    {
        var board = IaWrapupBoard.Build(context, exitsToMenu: _in.MenuDriven, _in.PauseState, InputFor);
        board.Restart = _in.Restart;
        board.Exit = _in.Exit;
        // Player 1, for the same reason the race and dogfight boards are.
        board.PhotoMode = () => EnterPhotoMode(0);
        _boards.Add(board);
        AddLayer(board, "ia_wrapup_board");
        return board;
    }

    /// <summary>A solo stunt run's end-of-run board, drawn in that pilot's own pane. It records the
    /// best time itself, under the key the roster hands over.</summary>
    public Control BuildSoloStuntBoard(StuntMission run, string planeDisplay, string context,
        string scoreKey, Action rerun, StuntCapture shots)
    {
        var scores = ScoreStore.ForSession(_in.Spec.ScoresPath, _in.Spec.ScoresThrowaway);
        var board = StuntScoreboard.Build(run, planeDisplay, context, scores, scoreKey,
            _in.MenuDriven, _in.PauseState, InputFor);
        board.Restart = rerun;
        board.Exit = _in.Exit;
        board.Shots = shots;
        return board;
    }

    /// <summary>Escape (or pad B) out of photo mode: the board comes back and the pilot's HUD with
    /// it. The camera is left where it was flown to, so the board returns over the frame just
    /// composed and re-entering continues from the same eye.</summary>
    public void ExitPhotoMode()
    {
        if (_photoHud == null)
            return;
        _photoHud.QueueFree();
        _photoHud = null;
        _photoCamera?.QueueFree();
        _photoCamera = null;
        if (_photoPilot is { } pilot && GodotObject.IsInstanceValid(pilot))
        {
            pilot.SetPilotHudVisible(true);
            pilot.CameraOwned = false;
            // ⚠ The arm cannot cover this edge. Photo mode returns to the halted world the board
            // froze, and halted is its own no-write branch, so the rules would stay off.
            pilot.SetViewedFromOutside(false);
            pilot.EndPhotoMode();   // seeds the pause edge, or the held Escape unpauses too
        }
        _photoPilot = null;
        RestoreBoards();
        // The board's pointer too, for the reason the options leaf re-primes it. A mouse button
        // still down as the mode is left reads as a fresh click on the row it rests over.
        ReprimePauseBoard();
        // ⚠ Prime every board reader. MenuInput POLLS raw keys, so the Escape still held would read
        // as a fresh press and dismiss the pause the board just reopened (docs/architecture.md).
        foreach (var rig in _in.Rigs)
            InputFor(rig.Index).Prime();
    }

    // The reader a board menu drives its cursor from, for a roster seat. The readers are this
    // machine's players in order, so the seat goes through its local player first: a guest's own
    // seat is not 0. A seat with no local player here falls back to player 1, who always exists.
    private MenuInput InputFor(int seat)
    {
        int local = LocalPlayerOf(seat);
        return local >= 0 && local < _menuInputs.Length ? _menuInputs[local] : _menuInputs[0];
    }

    // Which of this machine's players sits in a roster seat, -1 for one flown elsewhere. Outside a
    // network match every seat is its own local player.
    private int LocalPlayerOf(int seat) => Net.NetSeats.LocalOrdinal(_in.NetSeats, seat);

    private void AddLayer(Control board, string name)
    {
        var layer = new CanvasLayer { Name = name, Layer = HudLayers.Board };
        layer.AddChild(board);
        _in.WorldRoot.AddChild(layer);
    }

    // The string table the Original race board writes in, or null where that board does not apply.
    // That is the Built-in presentation, or an install missing the table or the borrowed lobby art.
    private UiStrings? OriginalRaceStrings()
    {
        if (_in.Presentation != PresentationId.Original
            || !File.Exists(Extraction.RofTree.Under(_in.DataRoot, "ASSETS/GRAPHICS/" + OriginalRaceTable.PageArt)))
        {
            return null;
        }

        var strings = UiStrings.TryLoad(_in.DataRoot);
        if (strings == null)
        {
            Log.Warn("ui", $"race board: no string table under {_in.DataRoot}, the Built-in board stands in");
        }

        return strings;
    }

    // The Original presentation's pause sheet, or null where it does not apply. That is the
    // Built-in presentation, a mode with no authored dialog, or an extraction the sheet cannot be
    // read out of. Falling back to the Built-in board keeps a pause always available.
    private OriginalPauseBoard? BuildOriginalPauseBoard(PauseSheetInputs sheetInputs)
    {
        if (_in.Presentation != PresentationId.Original)
        {
            return null;
        }

        if (sheetInputs.Campaign is not { } campaign)
        {
            return BuildMultiplayerPauseBoard(sheetInputs) ?? BuildInstantActionPauseBoard(sheetInputs);
        }

        var (chapterNumber, missionNumber) = campaign.Address;
        var sheet = PauseSheet.Load(
            _in.ZrdrPath, _in.MessagesPath,
            EscapeDialog.CampaignKey(chapterNumber, missionNumber), instantAction: false);
        if (sheet == null)
        {
            Log.Warn("ui", $"pause: no escape.zrd sheet for C{chapterNumber}/M0{missionNumber}");
            return null;
        }

        var objectives = ReadPauseObjectives(campaign);
        var pauseState = _in.PauseState;
        var runtime = sheetInputs.World;
        return OriginalPauseBoard.Build(
            pauseState, InputFor, _in.DataRoot, sheet,
            () => PauseReadoutFor(sheet, campaign, objectives, pauseState, runtime));
    }

    // A Dogfight's briefing blackboard, under the key its load screen read: the chapter, the type and
    // whether any seat is on a lobby team. It carries no map, memento or parchment, so no readout.
    private OriginalPauseBoard? BuildMultiplayerPauseBoard(PauseSheetInputs sheetInputs)
    {
        var spec = _in.Spec;
        if (!spec.Versus
            || LoadScreens.MultiplayerKey(
                spec.Chapter, spec.MissionType, sheetInputs.Teamed) is not { } key)
        {
            return null;
        }

        var sheet = PauseSheet.LoadMultiplayer(_in.ZrdrPath, _in.MessagesPath, key);
        if (sheet == null)
        {
            Log.Warn("ui", $"pause: no escape.zrd or Loading.zrd sheet for {spec.Chapter} ({key})");
            return null;
        }

        Log.Info("ui", $"pause: {spec.Chapter} Dogfight draws {sheet.State.Key}");
        return OriginalPauseBoard.Build(
            _in.PauseState, InputFor, _in.DataRoot, sheet, () => PauseReadout.Empty);
    }

    // An Instant Action sortie's own sheet: ia_escape.zrd's blackboard for its chapter and mission
    // type. It carries no map, memento or parchment and so needs no readout. Free flight has no
    // shipped dialog and keeps the Built-in board, as on the load screen (docs/org/pause-screen.md).
    private OriginalPauseBoard? BuildInstantActionPauseBoard(PauseSheetInputs sheetInputs)
    {
        if (sheetInputs.InstantAction is not { } ia
            || LoadScreens.LetterFor(ia.Def.MissionType) is not { } letter)
        {
            return null;
        }

        var spec = _in.Spec;
        string key = EscapeDialog.InstantActionKey(
            CampaignSequence.ChapterNumber(spec.Chapter), letter);
        var sheet = PauseSheet.Load(_in.ZrdrPath, _in.MessagesPath, key, instantAction: true);
        if (sheet == null)
        {
            Log.Warn("ui", $"pause: no ia_escape.zrd sheet for {spec.Chapter} {ia.Def.MissionType}");
            return null;
        }

        Log.Info("ui", $"pause: {spec.Chapter} {ia.Def.MissionType} draws {sheet.State.Key}");
        return OriginalPauseBoard.Build(
            _in.PauseState, InputFor, _in.DataRoot, sheet, () => PauseReadout.Empty);
    }

    // The parchment's own row order, which is the briefing's: every keyed IDENTITY by priority.
    // The dialog's script indexes THIS list, so the graph's rows cannot stand in for it.
    private IReadOnlyList<BriefingObjective> ReadPauseObjectives(CampaignDirector campaign)
    {
        try
        {
            return BriefingObjectives.Load(
                Zrdr.LoadFile(campaign.MissionZrdrPath, "objectives.json"),
                Messages.Load(_in.MessagesPath));
        }
        catch (Exception e) when (
            e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            return Array.Empty<BriefingObjective>();
        }
    }

    private PauseReadout PauseReadoutFor(
        PauseSheet sheet,
        CampaignDirector campaign,
        IReadOnlyList<BriefingObjective> objectives,
        PauseState pauseState,
        AnimRuntime? runtime)
    {
        var graph = campaign.Graph;
        var rows = PauseReadout.Rows(objectives, n => graph?.CompletedOf(n) ?? false);

        var icons = new List<PauseWorldIcon>();
        if (RigOf(pauseState.OwnerPlayerIndex)?.Controller is { } own)
        {
            var forward = -own.GlobalTransform.Basis.Z;
            if (PauseReadout.Icon(
                sheet.Shared.OwnShip, own.GlobalPosition.X, own.GlobalPosition.Z,
                forward.X, forward.Z) is { } ship)
            {
                icons.Add(ship);
            }
        }

        // The original looks its own zeppelin up by name and draws nothing when the mission has
        // none, as an empty find does here. The icon turns with the hull, as the reference stills
        // show: the art is drawn along the course flown.
        foreach (var hull in runtime?.FindNodes(PirateZepNode) ?? Array.Empty<Node3D>())
        {
            var nose = -hull.GlobalTransform.Basis.Z;
            if (PauseReadout.Icon(
                sheet.Shared.MyZep, hull.GlobalPosition.X, hull.GlobalPosition.Z,
                nose.X, nose.Z) is { } zeppelin)
            {
                icons.Add(zeppelin);
            }

            break;
        }

        // Where each icon landed, one line per pause. A pose outside the dialog's world window draws
        // nothing, which looks like a lookup that found nothing. This line tells the two apart.
        foreach (var icon in icons)
        {
            string where = sheet.State.Map is { } chart
                && chart.TryProject(icon.WorldX, icon.WorldZ, out _) ? "on" : "off";
            Log.Info("ui", $"pause icon {icon.Bitmap} at ({icon.WorldX:0}, {icon.WorldZ:0}) {where} the chart");
        }

        return new PauseReadout(rows, campaign.Memento, icons);
    }

    private PlayerRig? RigOf(int playerIndex)
    {
        foreach (var rig in _in.Rigs)
        {
            if (rig.Index == playerIndex)
            {
                return rig;
            }
        }

        return _in.Rigs.Count > 0 ? _in.Rigs[0] : null;
    }

    // Take playerIndex's pane to photo mode, chosen from whichever board is up. The pause is NEVER
    // dropped: offline the world stays the still frame the board froze, so this only moves an eye.
    // Idempotent, so a second press of the row while already in it stacks no cameras.
    private void EnterPhotoMode(int playerIndex)
    {
        if (_photoHud != null)
            return;
        PlayerRig? found = null;
        foreach (var r in _in.Rigs)
            if (r.Index == playerIndex)
                found = r;
        if (found is not { Controller: { } pilot } rig)
            return;
        SuspendBoards();
        pilot.BeginPhotoMode();
        pilot.CameraOwned = true;   // The controller writes this pane's camera no more.
        pilot.SetViewedFromOutside(true);
        pilot.SetPilotHudVisible(false);
        var eye = rig.Camera.Position;
        _photoCamera = new SpectatorCamera(rig.Camera, eye, eye - rig.Camera.Basis.Z,
            pilot.PadDevices, pilot.UseKeyboard, pilot.LocalPlayer)
        {
            Name = "photo_mode_camera",
            ShowReadout = false,   // the hint line is this mode's only furniture
            LockCandidates = _in.LockCandidates,
        };
        _in.WorldRoot.AddChild(_photoCamera);
        // ⚠ Lock onto this player's OWN aircraft, wreck included, and nothing else. FollowNode
        // seeds the orbit from the current eye, so following the looked-at plane never jumps.
        // Locking any other plane would keep the offset and teleport the view.
        _photoCamera.FollowNode(pilot);
        _photoPilot = pilot;
        _photoHud = UI.Overlays.PhotoModeHud.Build(pilot.PadDevices, pilot.UseKeyboard);
        _photoHud.Exit += ExitPhotoMode;
        _in.WorldRoot.AddChild(_photoHud);
        Log.Info("flight", $"photo mode: P{playerIndex + 1}'s pane, over the frame the board froze");
    }

    // PREFERENCES on either pause board: the sheet steps aside and the options leaf stands in its
    // place. The pause is never dropped, so an offline mission stays the pause's still frame. Every
    // rig's pause key goes silent for the duration.
    private void OpenPauseOptions(int owner)
    {
        if (_pauseOptions is not { Visible: false } leaf || _pauseBoard == null)
        {
            return;
        }

        // Hide AND stop processing, SuspendBoards' own rule. A board left processing still polls
        // the owner's reader, so the keys driving the leaf would drive the menu under it too.
        _pauseBoard.Visible = false;
        _pauseBoard.ProcessMode = Node.ProcessModeEnum.Disabled;
        var pollers = new List<MenuInput>();
        var flying = new List<FlightController>();
        foreach (var rig in _in.Rigs)
        {
            if (rig.Controller is { } controller)
            {
                controller.BeginPauseLeaf();
                flying.Add(controller);
            }

            pollers.Add(InputFor(rig.Index));
        }

        if (pollers.Count == 0)
        {
            pollers.Add(InputFor(0));
        }

        // The seats go in so an accepted Controls page reaches the flight behind the leaf now, not
        // at the next restart. The pollers are local players in order, so the owner seat maps too.
        leaf.Open(pollers, Math.Max(0, LocalPlayerOf(owner)), flying);
    }

    // The leaf's own door out, by RETURN TO MAIN MENU, Back or an accepted page. The sheet comes
    // back over the world it never resumed, with its pointer and every board reader re-primed.
    private void ClosePauseOptions()
    {
        if (_pauseBoard == null)
        {
            return;
        }

        _pauseBoard.ProcessMode = Node.ProcessModeEnum.Inherit;
        _pauseBoard.Visible = _in.PauseState.Paused;
        ReprimePauseBoard();
        // ⚠ Prime every board reader and re-seed every pause edge, ExitPhotoMode's own hazard. The
        // Escape that left the leaf is still held, and would dismiss the returned sheet or resume
        // the mission behind it.
        foreach (var rig in _in.Rigs)
        {
            rig.Controller?.EndPauseLeaf();
            InputFor(rig.Index).Prime();
        }
    }

    // Both presentations' pause boards read the mouse, so whichever one is in use is re-primed.
    private void ReprimePauseBoard()
    {
        _originalPause?.Reprime();
        (_pauseBoard as PauseBoard)?.Reprime();
    }

    // ⚠ Suspending a board is hide AND stop processing, not hide alone. A board left processing
    // still polls its owner's menu reader. The cursor keys would then drive a hidden menu while
    // they fly the photo camera, the collision this mode exists to remove.
    private void SuspendBoards()
    {
        _boardWasVisible.Clear();
        foreach (var board in _boards)
        {
            bool alive = GodotObject.IsInstanceValid(board);
            _boardWasVisible.Add(alive && board.Visible);
            if (!alive)
                continue;
            board.Visible = false;
            board.ProcessMode = Node.ProcessModeEnum.Disabled;
        }
    }

    // Back to exactly what was on screen. A results board would recompute its own visibility on the
    // next _Process anyway, but the pause board's is event-driven and no event is coming.
    private void RestoreBoards()
    {
        for (int i = 0; i < _boards.Count; i++)
        {
            if (!GodotObject.IsInstanceValid(_boards[i]))
                continue;
            _boards[i].ProcessMode = Node.ProcessModeEnum.Inherit;
            _boards[i].Visible = i < _boardWasVisible.Count && _boardWasVisible[i];
        }
        _boardWasVisible.Clear();
    }

    /// <summary>What one flight's boards are built over, settled before the first board.</summary>
    internal sealed class Inputs
    {
        public SessionSpec Spec = null!;
        public PresentationId Presentation;
        // Launched from the menu, so a board's Exit item returns there rather than quitting.
        public bool MenuDriven;
        public Action Exit = null!;
        // The launcher's rebuild, which an Instant Action wrap-up's Restart takes.
        public Action Restart = null!;
        public Node3D WorldRoot = null!;
        public IReadOnlyList<PlayerRig> Rigs = null!;
        public IReadOnlyList<Net.NetSeat> NetSeats = null!;
        public PauseState PauseState = null!;
        public int[][]? PadAssignment;
        // The options leaf the launcher builds for the pause board, null for no PREFERENCES door.
        public Func<PausePreferences?>? PauseOptions;
        public Func<IReadOnlyList<Node3D>> LockCandidates = null!;
        public string ZrdrPath = "", MessagesPath = "", DataRoot = "";
    }

    /// <summary>What decides which Original sheet a pause draws. It is the campaign mission's, a
    /// Dogfight's by whether any seat is on a lobby team, or the Instant Action sortie's.</summary>
    internal sealed class PauseSheetInputs
    {
        public CampaignDirector? Campaign;
        public InstantActionRuntime? InstantAction;
        public bool Teamed;
        // The world a campaign sheet looks the pirate zeppelin up in.
        public AnimRuntime? World;
    }
}

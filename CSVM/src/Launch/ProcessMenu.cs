using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The process's one menu. Its host is built on the first show, with both presentations, the shared
/// features, seat 0 and the menu's audio service. It also holds the Original layout
/// the availability check loads, the cold start's <c>--menu=</c> aid, the wheel steps <c>_Input</c>
/// counts, and the in-flight Preferences leaf. The presentations' screens go up under the
/// launcher's node, and the paths are read live, as an extraction's hand-back re-resolves them.
/// Module notes: docs/architecture/Launch.md.
/// </summary>
internal sealed class ProcessMenu(Node parent, LaunchAudio audio, NetFlight net, System.Func<BasePaths> paths,
    System.Action<MenuExit> exit)
{
    // Wheel steps turned since the menu seat last read them, positive toward a list's foot.
    private int _wheel;
    private MenuAudioService? _audio;
    // The decoded menu layout the Original presentation composes from, loaded once by the
    // availability check and handed to every Original instance the registry creates.
    private MenuLayout? _originalLayout;
    // The --menu= aid, held for the cold start alone. The first presentation created reads it and the
    // first Show consumes it, so no return from flight and no switch re-enters its screen.
    private string? _aid;
    // The process's one chapter cinema, so a chapter's film plays once per program run. Entering
    // the campaign plays it; leaving to the main menu and coming back does not. A restart plays the
    // current chapter's film again until that chapter's first mission has been flown. This is a
    // chosen behaviour, not a decoded one: the original's rule sits behind GUI callback 2151, which
    // nothing here decodes. Do not replace it with a latch persisted in the profile.
    private ChapterCinema? _chapterCinema;
    // The process's one closing cinema, so the campaign's last film plays once per program run
    // however often the player reopens the book afterwards. Its gate is the seated profile's own
    // completion, not a stored flag. A second profile that finishes in the same run does not get it
    // again. Do not replace it with a latch persisted in the profile.
    private ClosingCinema? _closingCinema;

    /// <summary>The menu host, null until the menu's first show.</summary>
    public MenuHost? Host { get; private set; }

    /// <summary>Counts <paramref name="steps"/> wheel steps for the menu seat's next read.</summary>
    public void TurnWheel(int steps) => _wheel += steps;

    /// <summary>Shows the menu at <paramref name="destination"/>, building the host on the first
    /// show. It answers the cold start's aid, empty on every later show. The presentation
    /// created inside the first show reads the aid; it is consumed here.
    /// </summary>
    public string Show(SessionSpec cli, SessionSpec spec, UI.Screens.BuildStamp stamp, MenuReturnDestination destination)
    {
        Host ??= Build(cli, spec, stamp);
        string aid = _aid ?? string.Empty;
        Host.Show(destination);
        _aid = null;
        return aid;
    }

    /// <summary>The in-flight Preferences leaf both pause boards open, over the decoded layout the
    /// Original presentation composes from and the menu's audio service for its cues. The host's
    /// rebinding feature means a rebind over the pause edits the keymap the menu edits. Its apply
    /// is <paramref name="persist"/>. Null where no layout reads.</summary>
    public UI.Screens.PausePreferences? PauseOptions(System.Action<OptionsApplyExit> persist)
    {
        OriginalAvailable(PresentationId.Original);
        var controls = Host != null && Host.Features.TryGet<ControlsFeature>(out var feature)
            ? feature
            : null;
        return UI.Screens.PausePreferences.Build(paths().DataRoot, _originalLayout, controls, persist, _audio);
    }

    // The menu host: both presentations, the shared features, the audio service and the launcher's
    // exit sink. Its first seat is the keyboard plus every unclaimed pad, with the mouse as its
    // pointer. The active presentation is settled through PresentationResolution.
    private MenuHost Build(SessionSpec cli, SessionSpec spec, UI.Screens.BuildStamp stamp)
    {
        var at = paths();
        // ⚠ No live mix preview in a run that drives itself. A scripted walk across the AUDIO page
        // would make that run's mix a function of the walk. A --no-det run with a scripted flag is
        // still such a run, which is why both halves are read rather than Det alone.
        _audio = new MenuAudioService(audio.Music, audio.FindSound,
            Extraction.RofTree.Member(at.RofPath, "ASSETS/SOUNDS"), previews: !spec.Det && spec.ScriptedBy.Length == 0);
        parent.AddChild(_audio);
        var seatInput = new MenuInput { Keyboard = true };
        // Seat 0 is player 1, so it navigates on the menu keymap that player saved.
        seatInput.LoadSavedKeymap(1);
        var builtInSeat = new BuiltInSeat(seatInput);
        var seat = new PointerSeat(
            builtInSeat, MousePosition, () => PrimaryPressed(stamp), TakeWheel,
            () => Input.IsMouseButtonPressed(MouseButton.Right));
        var registry = new PresentationRegistry();
        // The factories read the aid when they run, which is inside a Show. The cold start's instance
        // gets it, and the fresh instance a switch creates gets none.
        _aid = cli.MenuStartScreen;
        // --profiles= names the cabin's store as well as the flight's. A cabin launch carries the
        // flag into the flight, and the profile the cabin seated must load there.
        var profiles = cli.ProfilesDir is { } profilesDir ? CampaignProfileStore.ForSession(profilesDir) : null;
        registry.Register(PresentationId.BuiltIn,
            () => new BuiltInPresentation(parent, at.ZrdrPath, at.DataRoot, _aid ?? string.Empty, builtInSeat.Input)
            {
                CampaignProfiles = profiles,
            });
        registry.Register(PresentationId.Original,
            () => new OriginalPresentation(parent, at.DataRoot, _originalLayout!, _aid ?? string.Empty, builtInSeat.Input, spec.DebugJoin)
            {
                DebugPointer = spec.DebugPointer,
                CampaignProfiles = profiles,
            });
        var host = new MenuHost(registry, _audio, exit);
        host.Availability = OriginalAvailable;
        host.Held = () => stamp.DesktopModeNotice?.Visible == true;
        host.Features.Add(new FreeFlightFeature());
        host.Features.Add(InstantActionFeature.ForDataRoot(at.DataRoot));
        // Before the seat: the host lends the setup feature's seat list once the feature is in,
        // so seat 0 has to be joined through it.
        host.Features.Add(new PlayerSetupFeature());
        var strings = UiStrings.TryLoad(at.DataRoot) ?? UiStrings.Empty;
        host.Features.Add(new HangarFeature(strings, StockAirframes.Node, () => StockLoadouts.Load(), at.ZrdrPath));
        // The campaign feature carries both cinemas because both presentations already read it.
        // Neither of them can reach this module to play a film through.
        _chapterCinema = new ChapterCinema(audio.PlayCinema, audio.StopCinema);
        _closingCinema = new ClosingCinema(audio.PlayCinema, audio.StopCinema);
        host.Features.Add(new CampaignFeature(
            strings, StockAirframes.Node, _chapterCinema, _closingCinema));
        // The keymap editor writes through the per-player binding store, with player 1's stick rows split
        // off to the profile files. A reset takes them from the stick defaults. Injected so the
        // feature stays engine-free for a suite.
        host.Features.Add(new ControlsFeature(
            (player, profile) => CSVM.Sticks.StickScreens.Save(player, profile, CSVM.Sticks.StickProfiles.Live,
                (who, keymap) => CSVM.Bindings.BindingStore.UserBindings().Save(who, keymap)),
            CSVM.Sticks.StickScreens.OpenUserFolder,
            () => CSVM.Sticks.StickProfiles.Live));
        // The multiplayer door, built over the carrier and the master server the spec settles.
        host.Features.Add(net.OpenDoor(spec));
        host.AddSeat(seat);
        string? reason = host.Select(spec.ForceBuiltInPresentation, spec.PresentationOverride);
        string why = reason == null ? "" : $" reason={reason}";
        Log.Info("ui", $"menu presentation active={host.Selected} requested={host.Requested}{why}");
        return host;
    }

    // The host's availability answer. Original needs the decoded layout and every file its asset
    // manifest classes required. The layout it loads is kept for the presentation itself. Asked
    // again on every switch, so a tree repaired between the two answers is seen. Every other id is
    // available once registered.
    private string? OriginalAvailable(PresentationId id)
    {
        if (id != PresentationId.Original)
        {
            return null;
        }

        var layout = OriginalAvailability.Load(paths().DataRoot, out var reason, out var degraded);
        if (layout != null)
        {
            _originalLayout = layout;
        }

        if (degraded != null)
        {
            Log.Info("ui", $"original presentation: {degraded}");
        }

        return reason;
    }

    // The mouse in window pixels, the pointer half of seat 0: the viewport's last known position,
    // which the Original presentation maps into its authored space.
    private (float X, float Y)? MousePosition()
    {
        var at = parent.GetViewport().GetMousePosition();
        return (at.X, at.Y);
    }

    // The primary button, the press half of seat 0's pointer. ⚠ Keep the stamp's check. Original
    // polls the button instead of taking GUI events, so a folder icon's click would also press the
    // plaque under it.
    private bool PrimaryPressed(UI.Screens.BuildStamp stamp) =>
        Input.IsMouseButtonPressed(MouseButton.Left) && !stamp.HoldsPointer(parent.GetViewport().GetMousePosition());

    // The wheel steps counted since the last read, the wheel half of seat 0's pointer.
    private int TakeWheel()
    {
        int steps = _wheel;
        _wheel = 0;
        return steps;
    }
}

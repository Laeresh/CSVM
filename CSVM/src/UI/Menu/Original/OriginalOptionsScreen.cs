using System;
using System.Collections.Generic;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>What the form offers a page behind it, so no page reads the form's own state. It is the
/// apply exit every settings page's ACCEPT CHANGES leaves through, and the way back to the hub with
/// the edits dropped.</summary>
internal interface IOriginalOptionsForm
{
    /// <summary>The apply exit carrying every setting the store holds, each page's own included.</summary>
    OptionsApplyExit Apply();

    /// <summary>Back to the hub with every page's unsaved edits dropped.</summary>
    void Leave();
}

/// <summary>One page behind the Options hub, the page-sized sibling of
/// <see cref="IOriginalScreenModule"/>: the form calls these on the page that owns the screen
/// showing. The form answers the page's ACCEPT CHANGES and CANCEL CHANGES rows itself, through
/// <see cref="Accept"/> and <see cref="Cancel"/>, and hands every other row to
/// <see cref="Activate"/>.</summary>
internal interface IOriginalOptionsPage
{
    /// <summary>The one screen the page stands on.</summary>
    OriginalScreen Screen { get; }

    /// <summary>The page's ACCEPT CHANGES row key.</summary>
    string AcceptKey { get; }

    /// <summary>The page's CANCEL CHANGES row key.</summary>
    string CancelKey { get; }

    /// <summary>The page's rows, in focus order, its plaques included.</summary>
    void BuildRows(List<OriginalRow> rows);

    /// <summary>The page's scrolling lists for the pointer.</summary>
    void Lists(List<OriginalList> lists);

    /// <summary>A sideways step on the focused row where it changes a value there.</summary>
    bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction);

    /// <summary>Closes the page's open list and puts the focus back on its box; false when none is
    /// open.</summary>
    bool CloseDropdown();

    /// <summary>The page's answer to any row but its two plaques.</summary>
    MenuExit? Activate(OriginalRow row);

    /// <summary>What the page's ACCEPT CHANGES does.</summary>
    MenuExit? Accept();

    /// <summary>What the page's CANCEL CHANGES does.</summary>
    void Cancel();

    /// <summary>Back on the page, which every page answers.</summary>
    void Back();

    /// <summary>The page as drawn over the hub's logo, which the form puts down first.</summary>
    void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers);
}

/// <summary>
/// The form behind the Options hub's four doors, one standalone module over five page modules. They
/// stand over the decoded <c>[@GameOptions@]</c>, <c>[@Audio@]</c>, <c>[@Video@]</c>,
/// <c>[@ControlsPrefs@]</c> and <c>[@Keys@]</c> sections. It keeps the frame every page stands in,
/// each page's ACCEPT CHANGES and CANCEL CHANGES, and the switch to the page showing. The settings
/// pages read the saved options once per entry and leave through one apply exit carrying them all.
/// <see cref="CSVM.Utils.OptionsStore"/>'s one writer so stays the only writer, while the rebinding
/// pages stage their edits in the shared <see cref="ControlsFeature"/>.
/// </summary>
public sealed class OriginalOptionsScreen : IOriginalScreenModule, IOriginalOptionsForm
{
    /// <summary>The Options hub's door onto the Game Options page.</summary>
    public const string GameOptionsDoorKey = "PF_B_GAMEOPTIONS";

    /// <summary>The Options hub's door onto the AUDIO page.</summary>
    public const string AudioDoorKey = "PF_B_AUDIO";

    /// <summary>The Options hub's door onto the VIDEO page.</summary>
    public const string VideoDoorKey = "PF_B_VIDEO";

    /// <summary>The Options hub's fourth door, onto the CONTROLS page.</summary>
    public const string ControlsDoorKey = "PF_B_CONTROLS";

    // The Options hub's own section, whose logo every page keeps standing behind it since none of
    // the five authors one. The hub is the shell's (OriginalShell.PreferencesSection), its name
    // restated here so this module holds no reference to OriginalShell.
    private const string HubSection = "Preferences";

    private readonly MenuLayout _layout;
    private readonly IOriginalScreenHost _host;
    private readonly Func<CSVM.Utils.OptionsDef>? _options;
    private readonly IOriginalOptionsPage[] _pages;

    /// <summary>An options form over <paramref name="layout"/>'s own five sections, calling back
    /// into <paramref name="host"/> for the state every screen family shares. The reader
    /// <paramref name="options"/> reads the saved settings the pages show back, null opening them on
    /// the shipped defaults, which an engine-free test wants. The VIDEO page's two enumerated rows
    /// come from <paramref name="screenSizes"/> and <paramref name="screens"/>, and the rebinding
    /// pages stage in <paramref name="controls"/>. It never writes.</summary>
    public OriginalOptionsScreen(
        MenuLayout layout,
        IOriginalScreenHost host,
        Func<CSVM.Utils.OptionsDef>? options = null,
        Func<CSVM.Utils.SizeList>? screenSizes = null,
        Func<CSVM.Utils.ScreenList>? screens = null,
        ControlsFeature? controls = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _options = options;
        var chrome = new OriginalOptionsChrome(layout, host);
        GameOptions = new OriginalGameOptionsPage(chrome, this);
        Audio = new OriginalAudioPage(chrome, this);
        Video = new OriginalVideoPage(chrome, this, screenSizes, screens);
        Keys = new OriginalKeysPage(chrome, controls);
        Controls = new OriginalControlsPage(chrome, this, controls, Keys);
        _pages = new IOriginalOptionsPage[] { GameOptions, Audio, Video, Controls, Keys };
    }

    /// <summary>The Game Options page and the five settings it would apply.</summary>
    public OriginalGameOptionsPage GameOptions { get; }

    /// <summary>The AUDIO page, its four levels and the preview the host plays.</summary>
    public OriginalAudioPage Audio { get; }

    /// <summary>The VIDEO page and the display settings it would apply.</summary>
    public OriginalVideoPage Video { get; }

    /// <summary>The CONTROLS page, the seat and scheme choosers over the shared rebinding feature.</summary>
    public OriginalControlsPage Controls { get; }

    /// <summary>The KEYS AND BUTTONS page, its standing tab and its cells.</summary>
    public OriginalKeysPage Keys { get; }

    // The page standing on the screen showing, or null on any screen the form does not own.
    private IOriginalOptionsPage? Showing
    {
        get
        {
            foreach (var page in _pages)
            {
                if (page.Screen == _host.Screen)
                {
                    return page;
                }
            }

            return null;
        }
    }

    /// <summary>Whether the screen showing is one of the five pages'.</summary>
    public bool Owns(OriginalScreen screen) =>
        screen is OriginalScreen.GameOptions or OriginalScreen.Audio or OriginalScreen.Video
            or OriginalScreen.ControlsPrefs or OriginalScreen.Keys;

    /// <summary>The showing page's rows, in focus order.</summary>
    public void BuildRows(List<OriginalRow> rows) => Showing?.BuildRows(rows);

    /// <summary>The showing page's scrolling lists for the pointer: an open option list where one
    /// stands, and the action list on the KEYS AND BUTTONS page.</summary>
    public void Lists(List<OriginalList> lists) => Showing?.Lists(lists);

    /// <summary>A sideways step on the focused row where it changes a value there. That is an
    /// option's next word with wrap, the next registered seat, or the other flying scheme. False on
    /// anything else, so the step crosses columns. The sliders are the shell's own step.</summary>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) =>
        Showing?.StepSideways(rows, focus, direction) ?? false;

    /// <summary>Closes an open option list on any page and puts the focus back on its box; false
    /// when none is open.</summary>
    public bool CloseDropdown()
    {
        foreach (var page in _pages)
        {
            if (page.CloseDropdown())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The showing page's answer to an activated row. The two plaques are the form's to
    /// route: ACCEPT CHANGES and CANCEL CHANGES reach the page's own answer to each.</summary>
    public MenuExit? Activate(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (Showing is not { } page)
        {
            return null;
        }

        if (row.Key == page.AcceptKey)
        {
            return page.Accept();
        }

        if (row.Key == page.CancelKey)
        {
            page.Cancel();
            return null;
        }

        return page.Activate(row);
    }

    /// <summary>Back on the showing page, which every page answers the way its own CANCEL CHANGES
    /// does, an open list or a pending steal going first.</summary>
    public bool Back()
    {
        if (Showing is not { } page)
        {
            return false;
        }

        page.Back();
        return true;
    }

    /// <summary>The showing page as drawn, over the frame: the hub's logo, in the backdrop. No page
    /// writes a note or a stroke, so those two layers of <paramref name="layers"/> stand untouched.</summary>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        if (Showing is not { } page)
        {
            return;
        }

        ComposeFrame(layers.Backdrop);
        page.Compose(rows, focus, layers);
    }

    /// <summary>Re-reads the saved settings where the screen just opened is one of the three pages
    /// that show them back. The shell calls it from every Open. A page opened by a door, by a
    /// return or by a screenshot aid owes the player the same words.</summary>
    public void ScreenOpened(OriginalScreen screen)
    {
        if (screen is OriginalScreen.GameOptions or OriginalScreen.Audio or OriginalScreen.Video)
        {
            ReadSavedOptions();
        }
    }

    /// <summary>Keeps the KEYS AND BUTTONS page's list window over the cursor at the end of a
    /// frame. Nothing on any other screen.</summary>
    public void SyncWindows()
    {
        if (_host.Screen == OriginalScreen.Keys)
        {
            Keys.SyncWindow();
        }
    }

    // Every settings page's ACCEPT CHANGES. A page writes the settings it shows and hands the rest
    // back as ReadSavedOptions read them, which keeps Launcher.ApplyOptions the options file's one
    // writer.
    OptionsApplyExit IOriginalOptionsForm.Apply() =>
        new(Video.GraphicsChoice, CSVM.Flight.Hangar.Difficulty.Word(GameOptions.DifficultyChoice),
            Video.MonitorChoice, Video.ResolutionChoice, Video.DisplayModeChoice, Video.VSyncChoice,
            Video.RenderScaleChoice, Video.AntiAliasingChoice, Video.ShadowQualityChoice,
            Audio.MasterChoice, Audio.MusicChoice, Audio.EffectsChoice, Audio.VoiceChoice,
            GameOptions.NearestAfterKillChoice, GameOptions.RumbleChoice,
            GameOptions.DefaultViewChoice, GameOptions.AutoHeadTurnChoice, Video.ViewDistanceChoice,
            Video.WaterQualityChoice);

    // Back from a page: the saved settings are read again, so an edit the player declined is gone.
    void IOriginalOptionsForm.Leave()
    {
        ReadSavedOptions();
        _host.Open(OriginalScreen.Options);
    }

    // The saved options every settings page shows back. They are what was asked for, not what this
    // process resolved. A flag or the config key can have decided either, and the page still owes
    // the player the words their own ACCEPT CHANGES saved. Every page reads its own, since the
    // apply carries all of them. A form with no reader opens on the shipped defaults.
    private void ReadSavedOptions()
    {
        var saved = _options?.Invoke();
        GameOptions.Read(saved);
        Audio.Read(saved);
        Video.Read(saved);
    }

    // Every page draws the hub's logo, its own section authoring none. Each puts its own plate in
    // the backdrop rather than among the pictures. A board draws its fills between the two layers.
    // A plate among the pictures would paint over each row's own focus mark. The page would then
    // show no focused row at all, whatever the mark is, the mark being under opaque art.
    private void ComposeFrame(List<BoardPicture> backdrop)
    {
        if (_layout.Screen(HubSection)?.Widget("PF_LOGO") is { Art.Count: > 0 } logo)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], Math.Max(1, logo.Frames)),
                logo.Int("X"), logo.Int("Y")));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The VIDEO page behind the Options hub, one page module over the decoded <c>[@Video@]</c>
/// section, in the Game Options table's shape. Per row it is a key, the authored title, control and
/// description widgets it stands on, and the store field it reads and writes. The monitor and size
/// rows are enumerated per machine and the rest are fixed words over
/// <see cref="CSVM.Utils.DisplayWords"/>. It holds the display settings and leaves through the
/// form's apply exit (<see cref="IOriginalOptionsForm"/>). Rows: <c>docs/org/menu-inventory.md</c>.
/// </summary>
public sealed class OriginalVideoPage : IOriginalOptionsPage
{
    /// <summary>The VIDEO page's layout section.</summary>
    public const string Section = "Video";

    /// <summary>ACCEPT CHANGES, which leaves as the options apply carrying every choice.</summary>
    public const string AcceptKey = "VP_B_ACCEPTCHANGES";

    /// <summary>CANCEL CHANGES, back to the hub with the choices dropped.</summary>
    public const string CancelKey = "VP_B_CANCELCHANGES";

    /// <summary>The page's monitor dropdown, the page's first row.</summary>
    public const string MonitorKey = "MONITOR";

    /// <summary>The page's resolution dropdown.</summary>
    public const string ResolutionKey = "RESOLUTION";

    /// <summary>The page's display-mode dropdown.</summary>
    public const string DisplayModeKey = "DISPLAYMODE";

    /// <summary>The page's V-Sync dropdown.</summary>
    public const string VSyncKey = "VSYNC";

    /// <summary>The page's render-scale dropdown.</summary>
    public const string RenderScaleKey = "RENDERSCALE";

    /// <summary>The page's anti-aliasing dropdown.</summary>
    public const string AntiAliasingKey = "ANTIALIASING";

    /// <summary>The page's shadow-quality dropdown, on the authored Texture Quality line.</summary>
    public const string ShadowQualityKey = "SHADOWQUALITY";

    /// <summary>The page's enhanced-graphics checkbox.</summary>
    public const string GraphicsKey = "GRAPHICS";

    // The page's authored row shape, used where a layout does not carry the section. It is the
    // title column and the Shadows row's line, the checkbox's offset from its title, and the
    // dropdown column and box. The description column takes the width the plaque column leaves it.
    private const float TitleX = 18f;
    private const float TitleWidth = 162f;
    private const float ShadowsY = 565f;
    private const float CheckDx = 158f;
    private const float CheckDy = -4f;
    private const float DropX = 186f;
    private const float DropWidth = 120f;
    private const float ItemHeight = 17f;
    private const float DescX = 325f;
    private const float PlaqueX = 569f;

    private static readonly string[] GraphicsWords = { "FAITHFUL", "ENHANCED" };

    // The settings, in the authored order of the rows they stand on. Each is a title, the authored
    // row it stands on, a description, a control and the words of the store field it reads and
    // writes.
    private static readonly VideoOption[] Options =
    {
        new(MonitorKey, "Monitor", "VP_T_VideoTitle", "VP_D_Device", "VP_T_DEVICEDESC",
            _ => "Select the monitor the game opens on.",
            OriginalRowKind.Dropdown, s => s.MonitorWords,
            s => CSVM.Utils.MonitorSetting.Resolve(s._monitorIndex, s.Screens).Screen,
            (s, i) => s._monitorIndex = CSVM.Utils.MonitorSetting.Word(i)),
        new(ResolutionKey, "Resolution", "VP_T_DisplayTitle", "VP_D_Display", "VP_T_DisplayDESC",
            s => s.ResolutionDescription(),
            OriginalRowKind.Dropdown, s => s.ResolutionWords,
            s => DisplaySettingRows.ResolutionIndex(s.Sizes, s._resolution, s._displayMode),
            (s, i) => s._resolution = s.ResolutionWords[i],
            s => !s.ResolutionPinned),
        new(DisplayModeKey, "Display Mode", "VP_T_ViewTitle", "VP_D_View", "VP_T_ViewDESC",
            _ => "Select how the window sits on the screen. Borderless leaves the desktop beneath it.",
            OriginalRowKind.Dropdown, _ => DisplaySettingRows.DisplayModeLabels,
            s => DisplaySettingRows.WordIndex(CSVM.Utils.DisplayWords.DisplayModes, s._displayMode, CSVM.Utils.DisplayModeSetting.Default),
            (s, i) => s._displayMode = CSVM.Utils.DisplayWords.DisplayModes[i]),
        new(VSyncKey, "V-Sync", "VP_T_EffectsTitle", "VP_D_Effects", "VP_T_EffectsDESC",
            _ => "Select the frame pacing. On follows the screen; off runs free, or to a frame cap.",
            OriginalRowKind.Dropdown, _ => DisplaySettingRows.VSyncLabels,
            s => DisplaySettingRows.WordIndex(CSVM.Utils.DisplayWords.VSyncChoices, s._vsync, CSVM.Utils.VSyncSetting.Default),
            (s, i) => s._vsync = CSVM.Utils.DisplayWords.VSyncChoices[i]),
        new(RenderScaleKey, "Render Scale", "VP_T_ObjectsTitle", "VP_D_Objects", "VP_T_ObjectsDESC",
            _ => "Render the world below native to spare the GPU, or above it for cleaner edges. Applies at once.",
            OriginalRowKind.Dropdown, s => DisplaySettingRows.RenderScaleLabels(s.RenderScaleWords),
            s => DisplaySettingRows.WordIndex(s.RenderScaleWords, s._renderScale, CSVM.Utils.RenderScaleSetting.Default),
            (s, i) => s._renderScale = s.RenderScaleWords[i]),
        new(AntiAliasingKey, "Anti-aliasing", "VP_T_LightTitle", "VP_D_DLight", "VP_T_LightDESC",
            _ => "Select how edges are smoothed. FSR 2.2 also upscales a Render Scale below 100%. Applies at once.",
            OriginalRowKind.Dropdown, _ => DisplaySettingRows.AntiAliasingLabels,
            s => DisplaySettingRows.WordIndex(CSVM.Utils.DisplayWords.AntiAliasingChoices, s.AntiAliasingWord, s.AntiAliasingWord),
            (s, i) => s.PickAntiAliasing(CSVM.Utils.DisplayWords.AntiAliasingChoices[i])),
        new(ShadowQualityKey, "Shadow Quality", "VP_T_TextureTitle", "VP_D_Texture", "VP_T_TextureDESC",
            s => DisplaySettingRows.ShadowQualityDetail(s._graphics),
            OriginalRowKind.Dropdown, _ => DisplaySettingRows.ShadowQualityLabels,
            s => DisplaySettingRows.WordIndex(CSVM.Utils.ShadowQualitySetting.Words, s._shadowQuality, CSVM.Utils.ShadowQualitySetting.Word),
            (s, i) => s._shadowQuality = CSVM.Utils.ShadowQualitySetting.Words[i],
            s => s.ShadowQualityLive),
        new(GraphicsKey, "Enhanced Graphics", "VP_T_ShadowsTitle", "VP_B_SHADOWS", "VP_T_ShadowsDESC",
            _ => GraphicsDescription(), OriginalRowKind.Radio, _ => GraphicsWords,
            s => s._graphics == CSVM.Utils.GraphicsMode.EnhancedWord ? 1 : 0,
            (s, i) => s._graphics = i == 1 ? CSVM.Utils.GraphicsMode.EnhancedWord : CSVM.Utils.GraphicsMode.Default,
            _ => !CSVM.Utils.GraphicsMode.SwitchLocked),
    };

    private readonly OriginalOptionsChrome _chrome;
    private readonly IOriginalScreenHost _host;
    private readonly IOriginalOptionsForm _form;
    private readonly Func<CSVM.Utils.SizeList>? _screenSizes;
    private readonly Func<CSVM.Utils.ScreenList>? _screens;

    private string? _open;
    private int _listTop;
    private string _graphics = CSVM.Utils.GraphicsMode.Default;
    // The view distance as saved, null while never set, which resolves to Normal. No row shows it;
    // the apply carries it unchanged.
    private string? _viewDistance;
    // The water quality as saved, null while never set. No row shows it either; the apply carries
    // it unchanged.
    private string? _waterQuality;
    private string? _monitorIndex;
    private string? _resolution;
    // The size the options file named when the page last read it, which the size row offers as an
    // entry of its own (Sizes). It is held apart from the stepped choice, so a hand-written size
    // stays in the list after a step lands elsewhere. A step back then reaches it again.
    private string? _savedResolution;
    private string? _displayMode;
    private string? _vsync;
    private string? _renderScale;
    private string? _antiAliasing;
    // The sun's shadow quality as saved. Null is "never set", which the next start reads as the
    // enhanced mode's own look.
    private string? _shadowQuality;

    internal OriginalVideoPage(
        OriginalOptionsChrome chrome,
        IOriginalOptionsForm form,
        Func<CSVM.Utils.SizeList>? screenSizes,
        Func<CSVM.Utils.ScreenList>? screens)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _host = chrome.Host;
        _screenSizes = screenSizes;
        _screens = screens;
    }

    /// <summary>The page's open option list's key, or null when none is open.</summary>
    public string? OpenOption => _open;

    /// <summary>The sizes the resolution row offers, which is what the window's own screen can
    /// hold. The size the options file names is added where that is not among them. Every other
    /// row's words are a fixed vocabulary. This one's are enumerated per screen, so a shell with no
    /// screen to ask offers every candidate size instead.</summary>
    public IReadOnlyList<string> ResolutionWords => Sizes.Words;

    /// <summary>The screens the monitor row offers, one label per screen in index order. Enumerated
    /// like the resolution row's sizes, so a shell with no engine to ask offers the one screen it
    /// can name.</summary>
    public IReadOnlyList<string> MonitorWords => Screens.Labels;

    /// <summary>Whether the display mode standing on the page owns the size, which borderless does.
    /// The row then reads the screen's own size, takes no press and draws dead. The saved size is
    /// left where it is, so picking Windowed or Fullscreen again gives the player it back.</summary>
    public bool ResolutionPinned => CSVM.Utils.ResolutionSetting.Pinned(_displayMode);

    /// <summary>The graphics mode word the page would apply.</summary>
    public string GraphicsChoice => _graphics;

    /// <summary>The view-distance word the page would apply, null while never set.</summary>
    public string? ViewDistanceChoice => _viewDistance;

    /// <summary>The water-quality word the page would apply, which is the saved one, null while never
    /// set.</summary>
    public string? WaterQualityChoice => _waterQuality;

    /// <summary>The screen index (<see cref="CSVM.Utils.MonitorSetting.Word"/>'s spelling) the
    /// page would apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? MonitorChoice => _monitorIndex;

    /// <summary>The window size (<see cref="CSVM.Utils.OptionsStore.FormatResolution"/>'s spelling)
    /// the page would apply, or null while nothing has been saved and no row has been
    /// touched.</summary>
    public string? ResolutionChoice => _resolution;

    /// <summary>The display-mode word (<see cref="CSVM.Utils.DisplayWords.DisplayModes"/>) the page
    /// would apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? DisplayModeChoice => _displayMode;

    /// <summary>The V-Sync word (<see cref="CSVM.Utils.DisplayWords.VSyncChoices"/>) the page would
    /// apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? VSyncChoice => _vsync;

    /// <summary>The render-scale word (<see cref="CSVM.Utils.DisplayWords.RenderScaleChoices"/>) the
    /// page would apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? RenderScaleChoice => _renderScale;

    /// <summary>The anti-aliasing word (<see cref="CSVM.Utils.DisplayWords.AntiAliasingChoices"/>) the
    /// page would apply, or null while nothing has been saved and no row has been touched. The next
    /// start reads a null as the graphics mode's own default.</summary>
    public string? AntiAliasingChoice => _antiAliasing;

    /// <summary>The shadow-quality word (<see cref="CSVM.Utils.ShadowQualitySetting.Words"/>) the
    /// page would apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? ShadowQualityChoice => _shadowQuality;

    /// <summary>Whether the Shadow Quality row takes a press, which it does while the page's own
    /// Enhanced Graphics box is ticked. The faithful world casts no sun shadow, so under Original the
    /// row draws dead and keeps the saved word for a later flip.</summary>
    public bool ShadowQualityLive => _graphics == CSVM.Utils.GraphicsMode.EnhancedWord;

    /// <summary>The scales the Render Scale row offers under the method the Anti-aliasing row stands
    /// on, which is fewer under FSR 2.2. The list follows that row live.</summary>
    public IReadOnlyList<string> RenderScaleWords => CSVM.Utils.RenderScaleSetting.ChoicesFor(AntiAliasingWord);

    OriginalScreen IOriginalOptionsPage.Screen => OriginalScreen.Video;

    string IOriginalOptionsPage.AcceptKey => AcceptKey;

    string IOriginalOptionsPage.CancelKey => CancelKey;

    // The screen's sizes and the one a saved size it lacks falls back to. They are read through the
    // reader on every access, like the screens below. The list is widened with the size the options
    // file named, so a hand-written one stands on the row where it sorts. The row's value falls
    // back through this list's own fallback, the same word ResolutionSetting.Resolve lands on. The
    // row therefore cannot name a size the window would not be standing at.
    private CSVM.Utils.SizeList Sizes =>
        (_screenSizes?.Invoke() ?? CSVM.Utils.ResolutionSetting.Unknown).Including(_savedResolution);

    // The machine's screens and the one a saved index that names none falls back to. It is read
    // through the reader on every access, since a monitor can be plugged in while the page stands
    // open. The row's value goes through MonitorSetting.Resolve over this, the same call the apply
    // makes. The row therefore cannot show a screen the window would not be moved to.
    private CSVM.Utils.ScreenList Screens => _screens?.Invoke() ?? CSVM.Utils.MonitorSetting.Unknown;

    // The method the Anti-aliasing row shows: the saved word, or the default of the graphics mode
    // this page would apply.
    private string AntiAliasingWord => DisplaySettingRows.AntiAliasingWord(_antiAliasing, _graphics);

    /// <summary>Opens the page on the saved options with its first row focused. The hub's VIDEO door
    /// and the screenshot aid both go through it.</summary>
    public void Open()
    {
        _open = null;
        _listTop = 0;
        _host.Open(OriginalScreen.Video);
        _host.FocusedRow = -1;
    }

    /// <summary>Opens the page with one named row focused rather than its first, the screenshot
    /// aid's way onto a setting further down the page.</summary>
    public void OpenOn(string key)
    {
        Open();
        _host.FocusKey(key);
    }

    // An open list's items stand alone while one is open. Otherwise each setting's control stands
    // on its authored row with the two plaques beside them, all one column. Without the section
    // the controls stand as text buttons so the page is still walkable.
    void IOriginalOptionsPage.BuildRows(List<OriginalRow> rows)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            for (int i = 0; i < Options.Length; i++)
            {
                var fallback = Options[i];
                rows.Add(_host.PlaqueRow(fallback.Key, fallback.Words(this)[fallback.Read(this)], i,
                    fallback.Editable(this), 0));
            }

            _chrome.AddFallbackPlaques(rows, AcceptKey, CancelKey, Options.Length);
            return;
        }

        if (OpenDrop() is { } drop)
        {
            _listTop = OriginalOptionsChrome.DropListTop(drop, _listTop, _host.FocusedRow);
            _chrome.AddDropListRows(drop, _listTop, rows);
            return;
        }

        BuildControls(screen, rows);
    }

    // The page's lists for the pointer: an open setting's list alone, and only once it outruns the
    // authored window.
    void IOriginalOptionsPage.Lists(List<OriginalList> lists)
    {
        if (OpenDrop() is { } drop && _chrome.DropListWindow(drop, _listTop) is { } window)
        {
            lists.Add(new OriginalList(drop.Key, window, top => _listTop = _chrome.ScrollDropList(drop, top)));
        }
    }

    // A sideways step on a focused setting picks the next value with wrap; false on anything else.
    // It reads the option behind the row, not the row's Enabled flag. A dead row must not take a
    // value from a key that never presses it.
    bool IOriginalOptionsPage.StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count || OptionFor(rows[focus].Key) is not { } option
            || !option.Editable(this))
        {
            return false;
        }

        option.Write(this, DisplaySettingRows.Step(option.Read(this), direction, option.Words(this).Count));
        _host.FocusKey(option.Key);
        return true;
    }

    bool IOriginalOptionsPage.CloseDropdown() => CloseDropdown();

    // A list item picks and closes, a dropdown opens its list, a checkbox flips.
    MenuExit? IOriginalOptionsPage.Activate(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0 && OptionFor(row.Key[..colon]) is { } picked)
        {
            string suffix = row.Key[(colon + 1)..];
            if (suffix is OriginalDropLists.UpSuffix or OriginalDropLists.DownSuffix)
            {
                if (OpenDrop() is { } open)
                {
                    _listTop = _chrome.ScrollDropList(open, _listTop + (suffix == OriginalDropLists.UpSuffix ? -1 : 1));
                }

                return null;
            }

            picked.Write(this, int.Parse(suffix, CultureInfo.InvariantCulture));
            _open = null;
            _host.FocusKey(picked.Key);
            return null;
        }

        if (OptionFor(row.Key) is not { } option)
        {
            return null;
        }

        if (option.Kind == OriginalRowKind.Dropdown && _chrome.Layout.Screen(Section) != null)
        {
            _open = option.Key;
            _listTop = 0;
            _host.FocusedRow = Math.Max(0, option.Read(this));
            return null;
        }

        option.Write(this, (option.Read(this) + 1) % option.Words(this).Count);
        return null;
    }

    // The settings this page does not show ride the apply unchanged, read back when it opened.
    MenuExit? IOriginalOptionsPage.Accept() => _form.Apply();

    void IOriginalOptionsPage.Cancel() => _form.Leave();

    // An open list closes first, then the page leaves the way CANCEL CHANGES does.
    // VP_B_CANCELCHANGES is the declining answer the layout gives the page.
    void IOriginalOptionsPage.Back()
    {
        if (!CloseDropdown())
        {
            _form.Leave();
        }
    }

    // The plate, the title, each setting's title and description, the controls and an open list as
    // the overlay. ⚠ Keep the plate in the backdrop, never among the pictures. Art there buries every
    // focus mark the rows compose.
    void IOriginalOptionsPage.Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            _host.ComposePlainPage("VIDEO", rows, focus, layers);
            return;
        }

        _chrome.ComposePlate(screen, "VP_BACKGROUND", layers.Backdrop);
        _chrome.ComposePageTitle(screen, "VP_T_TITLE", "VIDEO", layers.Lines);
        foreach (var option in Options)
        {
            var place = Place(screen, option);
            layers.Lines.Add(new BoardLine(option.Title, place.TitleX, place.TitleY, place.TitleWidth,
                OriginalOptionsChrome.TitleFont, BoardInk.Row));
            layers.Lines.Add(new BoardLine(option.Description(this), place.DescX, place.DescY, place.DescWidth,
                OriginalOptionsChrome.DescriptionFont, BoardInk.Row));
        }

        // With a list open the page under it is drawn from the closed controls with the open one
        // focused. The items become the overlay, as the Game Options page's own list does.
        IReadOnlyList<OriginalRow> controls = rows;
        int controlFocus = focus;
        int controlPressed = _host.PressedRow;
        if (_open != null)
        {
            var closed = new List<OriginalRow>();
            BuildControls(screen, closed);
            controls = closed;
            controlFocus = IndexOf(_open);
            controlPressed = -1;
        }

        _chrome.ComposeControls(controls, controlFocus, controlPressed, key => OptionFor(key)?.Read(this) == 1, layers);
        if (OpenDrop() is { } drop && rows.Count > 0)
        {
            layers.Overlays.Add(_chrome.ComposeOptionList(drop, _listTop, rows, focus));
        }
    }

    /// <summary>Takes the display settings off <paramref name="saved"/>, the shipped defaults where
    /// it is null.</summary>
    internal void Read(CSVM.Utils.OptionsDef? saved)
    {
        _graphics = saved?.GraphicsMode ?? CSVM.Utils.GraphicsMode.Default;
        _viewDistance = saved?.ViewDistance;
        _waterQuality = saved?.WaterQuality;
        _monitorIndex = saved?.MonitorIndex;
        _resolution = saved?.Resolution;
        _savedResolution = saved?.Resolution;
        _displayMode = saved?.DisplayMode;
        _vsync = saved?.VSync;
        _renderScale = saved?.RenderScale;
        _antiAliasing = saved?.AntiAliasing;
        _shadowQuality = saved?.ShadowQuality;
    }

    private static int IndexOf(string key)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            if (Options[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    private static VideoOption? OptionFor(string key)
    {
        int at = IndexOf(key);
        return at >= 0 ? Options[at] : null;
    }

    // The graphics row's description. The apply switches the running world
    // (Launcher.RequestGraphicsSwitch), over a paused flight as well, so no restart is owed. A network
    // session refuses it, and the row draws dead.
    private static string GraphicsDescription() => CSVM.Utils.GraphicsMode.SwitchLocked
        ? "Select the lit world. A network game keeps the one it started with."
        : "Select the lit world. Applies at once.";

    // The size row's description says what the size does under the mode standing beside it. It does
    // something different in each. It is the window's own size, nothing at all, or the size the
    // game draws at inside a fullscreen window Godot will not resize.
    private string ResolutionDescription()
    {
        if (ResolutionPinned)
        {
            return "Borderless runs at the desktop's size. Pick Windowed or Fullscreen to choose one.";
        }

        return _displayMode == CSVM.Utils.DisplayWords.Fullscreen
            ? "Select the size the game draws at, scaled up to fill the screen."
            : "Select the window size.";
    }

    // FSR 2.2 refuses a scale above native, so picking it moves a scale standing there to native.
    // The launch clamps a saved pair the same way, so the page never shows a scale the run ignores.
    private void PickAntiAliasing(string word)
    {
        _antiAliasing = word;
        _renderScale = CSVM.Utils.RenderScaleSetting.ClampFor(_renderScale, word);
    }

    private bool CloseDropdown()
    {
        if (_open == null)
        {
            return false;
        }

        string key = _open;
        _open = null;
        _host.FocusKey(key);
        return true;
    }

    // The open setting's list, or null while none is open. It is that row's words under its own
    // authored control box, windowed by the TotalDisplayed that row authors. The Resolution row's
    // words are enumerated per screen and can outrun that window. Every other row's vocabulary
    // fits it.
    private OpenDropList? OpenDrop()
    {
        if (_open is not { } key || _chrome.Layout.Screen(Section) is not { } screen
            || OptionFor(key) is not { } option)
        {
            return null;
        }

        var place = Place(screen, option);
        return OriginalOptionsChrome.DropList(key, screen.Widget(option.ControlKey), option.Words(this),
            (place.BoxX, place.BoxY, place.BoxWidth, place.BoxHeight));
    }

    private void BuildControls(MenuLayoutScreen screen, List<OriginalRow> rows)
    {
        foreach (var option in Options)
        {
            var place = Place(screen, option);
            rows.Add(new OriginalRow(option.Key,
                option.Kind == OriginalRowKind.Dropdown ? option.Words(this)[option.Read(this)] : string.Empty,
                option.Kind, place.BoxX, place.BoxY, place.BoxWidth, place.BoxHeight,
                option.Editable(this), 0, place.Box));
        }

        _chrome.AddPlaques(screen, rows, AcceptKey, CancelKey);
    }

    // One row's shape off the section's own widgets, each number falling back to the authored one
    // when the row is not there. Two of them are derived rather than read. The title box stops at
    // the control beside it. Every Video title but the Graphics one is authored the same 162 wide,
    // whatever stands to its right. A description the section gives no width wraps at the plaque
    // column, the two widthless rows being the two the plaques stand beside.
    private VideoPlacement Place(MenuLayoutScreen screen, VideoOption option)
    {
        var title = screen.Widget(option.TitleKey);
        var control = screen.Widget(option.ControlKey);
        var description = screen.Widget(option.DescriptionKey);
        bool drop = option.Kind == OriginalRowKind.Dropdown;
        float titleX = title?.Int("X", (int)TitleX) ?? TitleX;
        float titleY = title?.Int("Y", (int)ShadowsY) ?? ShadowsY;
        var box = drop
            ? OriginalOptionsChrome.StripArt(control?.Art ?? Array.Empty<string>(), 4)
            : OriginalOptionsChrome.StripArt(control?.Art ?? Array.Empty<string>(), 0, control?.Frames ?? 8);
        float fallbackX = drop ? DropX : titleX + CheckDx;
        float fallbackY = drop ? titleY : titleY + CheckDy;
        float boxX = control?.Int("X", (int)fallbackX) ?? fallbackX;
        float boxY = control?.Int("Y", (int)fallbackY) ?? fallbackY;
        float boxWidth;
        float boxHeight;
        if (drop)
        {
            boxWidth = control?.Int("Width", (int)DropWidth) ?? DropWidth;
            boxHeight = control?.Int("ItemHeight", (int)ItemHeight) ?? ItemHeight;
        }
        else
        {
            var size = _chrome.StripSize(box, OriginalOptionsChrome.FallbackCheckSize, OriginalOptionsChrome.FallbackCheckSize);
            boxWidth = size.Width;
            boxHeight = size.Height;
        }

        float descX = description?.Int("X", (int)DescX) ?? DescX;
        float descY = description?.Int("Y", (int)titleY) ?? titleY;
        float plaqueX = screen.Widget(AcceptKey)?.Int("X", (int)PlaqueX) ?? PlaqueX;
        int authoredDesc = description?.Int("Width") ?? 0;
        return new VideoPlacement(
            titleX,
            titleY,
            Math.Max(1f, Math.Min(title?.Int("Width", (int)TitleWidth) ?? TitleWidth, boxX - titleX)),
            boxX,
            boxY,
            boxWidth,
            boxHeight,
            descX,
            descY,
            authoredDesc > 0 ? authoredDesc : Math.Max(1f, plaqueX - descX),
            box);
    }

    // One setting, by its title and the authored widgets it composes over: the title, the control
    // and the description. Its description is read off the page, since a row can say something
    // about its saved state. The control it takes, the words of the store field it shows, how it is
    // read and written, and whether the row is live. The words come off the page too, the
    // resolution row's being enumerated per screen rather than held as an array.
    private sealed record VideoOption(
        string Key, string Title, string TitleKey, string ControlKey, string DescriptionKey,
        Func<OriginalVideoPage, string> Description, OriginalRowKind Kind,
        Func<OriginalVideoPage, IReadOnlyList<string>> Words,
        Func<OriginalVideoPage, int> Read, Action<OriginalVideoPage, int> Write,
        Func<OriginalVideoPage, bool>? Live = null)
    {
        // Whether the row takes a press at all. A row another setting owns the value of is dead.
        // The cursor walks past it, the pointer cannot arm it, and it draws in its disabled frame.
        // Every other unavailable row on these pages already does that.
        public bool Editable(OriginalVideoPage page) => Live?.Invoke(page) ?? true;
    }

    // One row's place in authored pixels: the title box, the control's own rectangle and strip,
    // and the description box.
    private sealed record VideoPlacement(
        float TitleX, float TitleY, float TitleWidth,
        float BoxX, float BoxY, float BoxWidth, float BoxHeight,
        float DescX, float DescY, float DescWidth, BoardArt? Box);
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The VIDEO page behind the Options hub, one page module over the decoded <c>[@Video@]</c>
/// section, in the Game Options table's shape. Per row it is a key, the authored title, control and
/// description widgets it stands on, and the store field it reads and writes. The monitor and size
/// rows are enumerated per machine. The word rows bind to their registered setting by key, which
/// gives the words, labels, description and gate; the layout stays the page's. It stages them in the form's shared
/// <see cref="OptionsChoices"/>, whose rules it keeps, and leaves through the form's apply exit (<see cref="IOriginalOptionsForm"/>). Rows: <c>docs/org/menu-inventory.md</c>.
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

    /// <summary>The page's view-distance dropdown, the last row of the scrolled window.</summary>
    public const string ViewDistanceKey = "VIEWDISTANCE";

    /// <summary>The page's vegetation-quality dropdown, under View Distance in the scrolled window.</summary>
    public const string VegetationQualityKey = "VEGETATIONQUALITY";

    /// <summary>The page's 3D vegetation dropdown, under Vegetation in the scrolled window.</summary>
    public const string VegetationModelsKey = "VEGETATIONMODELS";

    /// <summary>The page's water-quality checkbox, on the authored Clutter Detail line.</summary>
    public const string WaterQualityKey = "WATERQUALITY";

    /// <summary>The page's enhanced-graphics checkbox.</summary>
    public const string GraphicsKey = "GRAPHICS";

    /// <summary>The page's VR mode dropdown, the window's last row.</summary>
    public const string VrModeKey = "VRMODE";

    /// <summary>The headset's comfort vignette dropdown, in Display Mode's place while a headset is
    /// live.</summary>
    public const string VrVignetteKey = "VRVIGNETTE";

    /// <summary>The headset's render-scale dropdown, in V-Sync's place while a headset is live.</summary>
    public const string VrRenderScaleKey = "VRRENDERSCALE";
    /// <summary>The window the dropdown rows scroll through.</summary>
    public const string WindowKey = "VP_DROPDOWNS";

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

    // The dropdown window's scroll column: its gap from the widest box, and the arrow's size where
    // the art cannot be measured. The shipped boxes end at 306 and the descriptions start at 325.
    private const float BarGap = 2f;
    private const float BarWidth = 16f;
    private const float BarArrowHeight = 12f;

    private static readonly string[] GraphicsWords = { "FAITHFUL", "ENHANCED" };

    private static readonly string[] OffOnWords = { "Off", "On" };

    // The seven authored dropdown lines, top to bottom. The dropdown rows scroll through them as a
    // window, so a row takes the widgets of whichever line it stands on.
    private static readonly VideoSlot[] DropSlots =
    {
        new("VP_T_VideoTitle", "VP_D_Device", "VP_T_DEVICEDESC"),
        new("VP_T_DisplayTitle", "VP_D_Display", "VP_T_DisplayDESC"),
        new("VP_T_ViewTitle", "VP_D_View", "VP_T_ViewDESC"),
        new("VP_T_EffectsTitle", "VP_D_Effects", "VP_T_EffectsDESC"),
        new("VP_T_ObjectsTitle", "VP_D_Objects", "VP_T_ObjectsDESC"),
        new("VP_T_LightTitle", "VP_D_DLight", "VP_T_LightDESC"),
        new("VP_T_TextureTitle", "VP_D_Texture", "VP_T_TextureDESC"),
    };

    // The dropdown rows in walk order, one more than the window shows. Each is a title, a
    // description, a control and the words of the store field it reads and writes.
    private static readonly VideoOption[] Dropdowns =
    {
        new(MonitorKey, "Monitor", null,
            _ => "Select the monitor the game opens on.",
            OriginalRowKind.Dropdown, c => c.Screens.Labels, c => c.MonitorAt,
            (c, i) => c.MonitorIndex = CSVM.Utils.MonitorSetting.Word(i)),
        new(ResolutionKey, "Resolution", null,
            ResolutionDescription,
            OriginalRowKind.Dropdown, c => c.Sizes.Words, c => c.ResolutionAt,
            (c, i) => c.Resolution = c.Sizes.Words[i],
            c => !c.ResolutionPinned),
        new(DisplayModeKey, "Display Mode", null,
            _ => "Select how the window sits on the screen.",
            OriginalRowKind.Dropdown, _ => DisplaySettingRows.DisplayModeLabels, c => c.DisplayModeAt,
            (c, i) => c.DisplayMode = CSVM.Utils.DisplayWords.DisplayModes[i], HeadsetRow: false),
        new(VrVignetteKey, "Comfort Vignette", null,
            _ => DisplaySettingRows.VrVignetteDetail,
            OriginalRowKind.Dropdown, _ => CSVM.Utils.VrVignetteSetting.Labels(),
            c => DisplaySettingRows.WordIndex(CSVM.Utils.VrVignetteSetting.Words, c.VrVignette, CSVM.Utils.VrVignetteSetting.Default),
            (c, i) => c.VrVignette = CSVM.Utils.VrVignetteSetting.Words[i], HeadsetRow: true),
        new(VSyncKey, "V-Sync", null,
            _ => "Select the frame pacing.",
            OriginalRowKind.Dropdown, _ => DisplaySettingRows.VSyncLabels, c => c.VSyncAt,
            (c, i) => c.VSync = CSVM.Utils.DisplayWords.VSyncChoices[i], HeadsetRow: false),
        new(VrRenderScaleKey, "VR Render Scale", null,
            _ => DisplaySettingRows.VrRenderScaleDetail,
            OriginalRowKind.Dropdown, _ => CSVM.Utils.VrRenderScaleSetting.Labels(),
            c => DisplaySettingRows.WordIndex(CSVM.Utils.VrRenderScaleSetting.Words, c.VrRenderScale, CSVM.Utils.VrRenderScaleSetting.Default),
            (c, i) => c.VrRenderScale = CSVM.Utils.VrRenderScaleSetting.Words[i], HeadsetRow: true),
        Word(RenderScaleKey, CSVM.Utils.RenderScaleSetting.Lookup, "Render Scale"),
        Word(AntiAliasingKey, CSVM.Utils.AntiAliasingSetting.Lookup, "Anti-aliasing"),
        Word(ShadowQualityKey, CSVM.Utils.ShadowQualitySetting.Lookup, "Shadow Quality"),
        Word(ViewDistanceKey, CSVM.Utils.ViewDistance.Lookup, "View Distance"),
        Word(VegetationQualityKey, CSVM.Utils.VegetationQualitySetting.Lookup, "Vegetation"),
        Word(VegetationModelsKey, CSVM.Utils.VegetationModelsSetting.Lookup, "3D Vegetation"),
        new(VrModeKey, "VR Mode", null,
            _ => DisplaySettingRows.VrModeDetail(OperatingSystem.IsWindows()), OriginalRowKind.Dropdown, _ => OffOnWords,
            c => c.VrMode == true ? 1 : 0,
            (c, i) => c.VrMode = i == 1,
            _ => OperatingSystem.IsWindows()),
    };

    // The dropdowns each kind of launch walks, one more than the window shows. Under a headset the
    // comfort rows stand in Display Mode's and V-Sync's places, so both walks are as long.
    private static readonly VideoOption[] FlatDrops = Dropdowns.Where(static o => o.Shows(false)).ToArray();
    private static readonly VideoOption[] HeadsetDrops = Dropdowns.Where(static o => o.Shows(true)).ToArray();

    // The two checkboxes, fixed on the short authored lines under the window.
    private static readonly VideoOption[] Checks =
    {
        Check(WaterQualityKey, CSVM.Utils.WaterQualitySetting.Lookup, "Ocean Waves",
            new("VP_T_ClutterTitle", "VP_B_CLUTTER", "VP_T_ClutterDESC"),
            c => c.Enhanced ? "Draw a swell on the sea." : "Enhanced Graphics only."),
        new(GraphicsKey, "Enhanced Graphics", new("VP_T_ShadowsTitle", "VP_B_SHADOWS", "VP_T_ShadowsDESC"),
            _ => "Select the lit world.", OriginalRowKind.Radio, _ => GraphicsWords,
            c => c.Enhanced ? 1 : 0,
            (c, i) => c.Graphics = i == 1 ? CSVM.Utils.GraphicsMode.EnhancedWord : CSVM.Utils.GraphicsMode.Default,
            _ => !CSVM.Utils.GraphicsMode.SwitchLocked),
    };

    private static readonly VideoOption[] Options = Dropdowns.Concat(Checks).ToArray();

    // The furthest the window scrolls, which puts the last dropdown on its bottom line.
    private static readonly int LastTop = FlatDrops.Length - DropSlots.Length;

    private readonly OriginalOptionsChrome _chrome;
    private readonly IOriginalScreenHost _host;
    private readonly IOriginalOptionsForm _form;
    // The form's staged settings, shared with the other two settings pages. A word setting with no
    // row here rides in it unshown, the apply carrying it unchanged.
    private readonly OptionsChoices _choices;

    private string? _open;
    private int _listTop;
    // The first dropdown row the window shows.
    private int _top;

    internal OriginalVideoPage(OriginalOptionsChrome chrome, IOriginalOptionsForm form, OptionsChoices choices)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _choices = choices ?? throw new ArgumentNullException(nameof(choices));
        _host = chrome.Host;
    }

    /// <summary>The page's open option list's key, or null when none is open.</summary>
    public string? OpenOption => _open;

    /// <summary>The sizes the resolution row offers, which is what the window's own screen can
    /// hold. The size the options file names is added where that is not among them. Every other
    /// row's words are a fixed vocabulary. This one's are enumerated per screen, so a shell with no
    /// screen to ask offers every candidate size instead.</summary>
    public IReadOnlyList<string> ResolutionWords => _choices.Sizes.Words;

    /// <summary>The screens the monitor row offers, one label per screen in index order. Enumerated
    /// like the resolution row's sizes, so a shell with no engine to ask offers the one screen it
    /// can name.</summary>
    public IReadOnlyList<string> MonitorWords => _choices.Screens.Labels;

    /// <summary>Whether the display mode standing on the page owns the size, which borderless does.
    /// The row then reads the screen's own size, takes no press and draws dead. The saved size is
    /// left where it is, so picking Windowed or Fullscreen again gives the player it back.</summary>
    public bool ResolutionPinned => _choices.ResolutionPinned;

    /// <summary>The first dropdown row the window shows. It is 0 with Monitor on the top line. On the
    /// bottom line 1 puts View Distance, 2 Vegetation and 3 VR Mode.</summary>
    public int Top => _top;

    /// <summary>The graphics mode word the page would apply.</summary>
    public string GraphicsChoice => _choices.Graphics;

    /// <summary>The screen index (<see cref="CSVM.Utils.MonitorSetting.Word"/>'s spelling) the
    /// page would apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? MonitorChoice => _choices.MonitorIndex;

    /// <summary>The window size (<see cref="CSVM.Utils.OptionsStore.FormatResolution"/>'s spelling)
    /// the page would apply, or null while nothing has been saved and no row has been
    /// touched.</summary>
    public string? ResolutionChoice => _choices.Resolution;

    /// <summary>The display-mode word (<see cref="CSVM.Utils.DisplayWords.DisplayModes"/>) the page
    /// would apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? DisplayModeChoice => _choices.DisplayMode;

    /// <summary>The V-Sync word (<see cref="CSVM.Utils.DisplayWords.VSyncChoices"/>) the page would
    /// apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? VSyncChoice => _choices.VSync;

    /// <summary>The VR mode the page would apply, or null while nothing has been saved and the row
    /// has not been touched.</summary>
    public bool? VrModeChoice => _choices.VrMode;

    /// <summary>The headset render-scale word the page would apply, or null while never set.</summary>
    public string? VrRenderScaleChoice => _choices.VrRenderScale;

    /// <summary>The headset vignette word the page would apply, or null while never set.</summary>
    public string? VrVignetteChoice => _choices.VrVignette;

    /// <summary>Whether a headset is live. Its two comfort rows then take the Display Mode and V-Sync
    /// places. A headset has no use for those two: it paces its own frames, and the window only
    /// mirrors it. The hidden rows' words ride the apply unchanged. Settable for a test.</summary>
    public Func<bool> Headset { get; set; } = static () => CSVM.Bindings.XrControllers.SessionLive;

    OriginalScreen IOriginalOptionsPage.Screen => OriginalScreen.Video;

    string IOriginalOptionsPage.AcceptKey => AcceptKey;

    string IOriginalOptionsPage.CancelKey => CancelKey;

    // The dropdowns the page walks now. Under a headset its comfort rows stand in the places of two
    // settings it has no use for.
    private VideoOption[] Shown => Headset() ? HeadsetDrops : FlatDrops;

    // Every row the page shows now, in walk order: the dropdowns, then the two checkboxes.
    private List<VideoOption> Rows => Shown.Concat(Checks).ToList();

    /// <summary>The word the page would apply for <paramref name="setting"/>, or null while nothing has
    /// been saved and no row has been touched. A setting with no row here rides unchanged.</summary>
    public string? Choice(CSVM.Utils.WordSetting setting) => _choices.Words[setting];

    /// <summary>The words <paramref name="setting"/>'s row offers under the other rows, as the Render
    /// Scale row narrows under FSR 2.2. The list follows those rows live.</summary>
    public IReadOnlyList<string> Offered(CSVM.Utils.WordSetting setting) => _choices.Offered(setting);

    /// <summary>Opens the page on the saved options with its first row focused. The hub's VIDEO door
    /// and the screenshot aid both go through it.</summary>
    public void Open()
    {
        _open = null;
        _listTop = 0;
        _top = 0;
        _host.Open(OriginalScreen.Video);
        _host.FocusedRow = -1;
    }

    /// <summary>Opens the page with one named row focused rather than its first, the screenshot
    /// aid's way onto a setting further down the page. A dropdown row under the window scrolls it
    /// into view.</summary>
    public void OpenOn(string key)
    {
        Open();
        int at = Array.FindIndex(Shown, option => option.Key == key);
        _top = Math.Clamp(at - DropSlots.Length + 1, 0, LastTop);
        _host.FocusKey(key);
    }

    // An open list's items stand alone while one is open. Otherwise each setting's control stands
    // on its authored row with the two plaques beside them, all one column. Without the section
    // the controls stand as text buttons so the page is still walkable.
    void IOriginalOptionsPage.BuildRows(List<OriginalRow> rows)
    {
        var screen = _chrome.Layout.Screen(Section);
        var shown = Rows;
        if (screen == null)
        {
            for (int i = 0; i < shown.Count; i++)
            {
                var fallback = shown[i];
                rows.Add(_host.PlaqueRow(fallback.Key, fallback.Words(_choices)[fallback.Read(_choices)], i,
                    fallback.Editable(_choices), 0));
            }

            _chrome.AddFallbackPlaques(rows, AcceptKey, CancelKey, shown.Count);
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

    // The page's lists for the pointer: an open setting's list alone, once it outruns the authored
    // window, and otherwise the dropdown rows' window. That one draws no bar; the wheel and the
    // walk off its last line scroll it.
    void IOriginalOptionsPage.Lists(List<OriginalList> lists)
    {
        if (OpenDrop() is { } drop)
        {
            if (_chrome.DropListWindow(drop, _listTop) is { } window)
            {
                lists.Add(new OriginalList(drop.Key, window, top => _listTop = _chrome.ScrollDropList(drop, top)));
            }

            return;
        }

        if (_chrome.Layout.Screen(Section) is { } screen)
        {
            lists.Add(new OriginalList(WindowKey, Bar(screen).Window, line => _top = Math.Clamp(line, 0, LastTop)));
        }
    }

    // A sideways step on a focused setting picks the next value with wrap; false on anything else.
    // It reads the option behind the row, not the row's Enabled flag. A dead row must not take a
    // value from a key that never presses it.
    bool IOriginalOptionsPage.StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count || OptionFor(rows[focus].Key) is not { } option
            || !option.Editable(_choices))
        {
            return false;
        }

        option.Write(_choices, DisplaySettingRows.Step(option.Read(_choices), direction, option.Words(_choices).Count));
        _host.FocusKey(option.Key);
        return true;
    }

    bool IOriginalOptionsPage.CloseDropdown() => CloseDropdown();

    // A list item picks and closes, a dropdown opens its list, a checkbox flips, and the window's
    // own arrows scroll it a line.
    MenuExit? IOriginalOptionsPage.Activate(OriginalRow row)
    {
        if (row.Key.StartsWith(WindowKey + ":", StringComparison.Ordinal))
        {
            _top = Math.Clamp(_top + (row.Key.EndsWith(OriginalDropLists.UpSuffix, StringComparison.Ordinal) ? -1 : 1), 0, LastTop);
            return null;
        }

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

            picked.Write(_choices, int.Parse(suffix, CultureInfo.InvariantCulture));
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
            _host.FocusedRow = Math.Max(0, option.Read(_choices));
            return null;
        }

        option.Write(_choices, (option.Read(_choices) + 1) % option.Words(_choices).Count);
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
        var bar = Bar(screen);
        foreach (var option in Options)
        {
            if (SlotOf(option) is not { } slot)
            {
                continue;
            }

            var place = Place(screen, slot, option.Kind, HomeOf(option));
            float descWidth = option.Fixed == null
                ? Math.Min(place.DescWidth, bar.Window.ThumbX - BarGap - place.DescX)
                : place.DescWidth;
            layers.Lines.Add(new BoardLine(option.Title, place.TitleX, place.TitleY, place.TitleWidth,
                OriginalOptionsChrome.TitleFont, BoardInk.Row));
            layers.Lines.Add(new BoardLine(FirstSentence(option.Description(_choices)), place.DescX, place.DescY,
                descWidth, OriginalOptionsChrome.DescriptionFont, BoardInk.Row));
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
            controlFocus = ShownIndexOf(_open);
            controlPressed = -1;
        }

        _chrome.ComposeControls(controls, controlFocus, controlPressed, key => OptionFor(key)?.Read(_choices) == 1, layers);
        if (bar.Thumb != null)
        {
            layers.Pictures.Add(new BoardPicture(bar.Thumb, bar.Window.ThumbX, bar.Window.ThumbY, Height: bar.Window.ThumbHeight));
        }
        if (OpenDrop() is { } drop && rows.Count > 0)
        {
            layers.Overlays.Add(_chrome.ComposeOptionList(drop, _listTop, rows, focus));
        }
    }

    // A dropdown row bound to a registered word setting by its key. The setting gives the words,
    // labels, description and gate; the page gives the title.
    private static VideoOption Word(string key, CSVM.Utils.WordSetting setting, string title) =>
        new(key, title, null,
            c => setting.Detail(c.Enhanced), OriginalRowKind.Dropdown,
            c => c.Offered(setting).Select(setting.Label).ToArray(), c => c.At(setting),
            (c, i) => c.Pick(setting, c.Offered(setting)[i]),
            c => c.Live(setting));

    // A checkbox over a two-word setting on its own authored line, ticked on the second word. The
    // setting's own description names both words, so the page gives the box's.
    private static VideoOption Check(string key, CSVM.Utils.WordSetting setting, string title, VideoSlot slot,
        Func<OptionsChoices, string> description) =>
        new(key, title, slot,
            description, OriginalRowKind.Radio,
            c => c.Offered(setting).Select(setting.Label).ToArray(), c => c.At(setting),
            (c, i) => c.Pick(setting, c.Offered(setting)[i]),
            c => c.Live(setting));

    // The text up to and including its first full stop, the one sentence each line has room for.
    private static string FirstSentence(string text)
    {
        int end = text.IndexOf(". ", StringComparison.Ordinal);
        return end < 0 ? text : text[..(end + 1)];
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

    // The size row's description says what the size does under the mode standing beside it. It does
    // something different in each. It is the window's own size, nothing at all, or the size the
    // game draws at inside a fullscreen window Godot will not resize.
    private static string ResolutionDescription(OptionsChoices choices)
    {
        if (choices.ResolutionPinned)
        {
            return "Borderless runs at the desktop's size. Pick Windowed or Fullscreen to choose one.";
        }

        return choices.DisplayMode == CSVM.Utils.DisplayWords.Fullscreen
            ? "Select the size the game draws at, scaled up to fill the screen."
            : "Select the window size.";
    }

    // The line whose box a row wears: a dropdown's unscrolled line, the rows past the window taking
    // the last.
    private VideoSlot HomeOf(VideoOption option) =>
        option.Fixed ?? DropSlots[Math.Clamp(Array.IndexOf(Shown, option), 0, DropSlots.Length - 1)];

    // Where a key stands among the rows the page shows now, -1 when it is not shown.
    private int ShownIndexOf(string key)
    {
        var shown = Rows;
        for (int i = 0; i < shown.Count; i++)
        {
            if (shown[i].Key == key)
            {
                return i;
            }
        }

        return -1;
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
            || OptionFor(key) is not { } option || SlotOf(option) is not { } slot)
        {
            return null;
        }

        var place = Place(screen, slot, option.Kind, HomeOf(option));
        return OriginalOptionsChrome.DropList(key, screen.Widget(slot.ControlKey), option.Words(_choices),
            (place.BoxX, place.BoxY, place.BoxWidth, place.BoxHeight));
    }

    // The line a row stands on. A checkbox keeps its own, and a dropdown takes the window's line it
    // scrolls to, or null while out of view.
    private VideoSlot? SlotOf(VideoOption option)
    {
        if (option.Fixed != null)
        {
            return option.Fixed;
        }

        int line = Array.IndexOf(Shown, option) - _top;
        return line >= 0 && line < DropSlots.Length ? DropSlots[line] : null;
    }

    // A dropdown out of view keeps its index at the window's nearer end, unseen and never a stop.
    private void BuildControls(MenuLayoutScreen screen, List<OriginalRow> rows)
    {
        foreach (var option in Rows)
        {
            var slot = SlotOf(option);
            int line = Array.IndexOf(Shown, option) - _top;
            var place = Place(screen, slot ?? DropSlots[line < 0 ? 0 : ^1], option.Kind, HomeOf(option));
            rows.Add(new OriginalRow(option.Key,
                option.Kind == OriginalRowKind.Dropdown ? option.Words(_choices)[option.Read(_choices)] : string.Empty,
                option.Kind, place.BoxX, place.BoxY, place.BoxWidth, place.BoxHeight,
                option.Editable(_choices), 0, place.Box, slot != null));
        }

        _chrome.AddPlaques(screen, rows, AcceptKey, CancelKey);
        var bar = Bar(screen);
        var window = bar.Window;
        rows.Add(new OriginalRow(WindowKey + ":" + OriginalDropLists.UpSuffix, string.Empty, OriginalRowKind.Button,
            window.ThumbX, window.Y, bar.UpSize.Width, bar.UpSize.Height, _top > 0, 0, bar.Up));
        rows.Add(new OriginalRow(WindowKey + ":" + OriginalDropLists.DownSuffix, string.Empty, OriginalRowKind.Button,
            window.ThumbX, window.TrackTop + window.TrackHeight, bar.DownSize.Width, bar.DownSize.Height,
            _top < LastTop, 0, bar.Down));
    }

    // The dropdown window and its scroll column, right-aligned on the widest description's edge, since
    // the panel frame leaves no room past it. The window's descriptions wrap short of the column. It
    // hangs the D rows' own arrows at its two ends and their thumb on the track between.
    private VideoBar Bar(MenuLayoutScreen screen)
    {
        var art = screen.Widget(DropSlots[0].ControlKey)?.Art ?? Array.Empty<string>();
        var up = OriginalOptionsChrome.StripArt(art, 1);
        var down = OriginalOptionsChrome.StripArt(art, 2);
        var thumb = OriginalOptionsChrome.StripArt(art, 0, 1);
        var upSize = _chrome.StripSize(up, BarWidth, BarArrowHeight);
        var downSize = _chrome.StripSize(down, BarWidth, BarArrowHeight);
        var thumbSize = _chrome.StripSize(thumb, upSize.Width, BarArrowHeight);
        var first = Place(screen, DropSlots[0], OriginalRowKind.Dropdown);
        var last = Place(screen, DropSlots[^1], OriginalRowKind.Dropdown);
        float right = DropSlots.Max(slot => Place(screen, slot, OriginalRowKind.Dropdown) is var p ? p.DescX + p.DescWidth : 0f);
        float x = right - upSize.Width;
        float top = first.BoxY;
        float bottom = last.BoxY + last.BoxHeight;
        // The Accept plaque stands under the column's foot, so the column stops above it. The window
        // keeps the rows' full height, the walk scrolling off its foot.
        float foot = bottom;
        if (screen.Widget(AcceptKey) is { } accept && x + upSize.Width > accept.Int("X", (int)PlaqueX))
        {
            foot = Math.Min(bottom, accept.Int("Y", (int)bottom) - BarGap);
        }
        float trackTop = top + upSize.Height;
        float trackHeight = foot - top - upSize.Height - downSize.Height;
        float thumbHeight = ListWindow.ThumbHeightFor(trackHeight, DropSlots.Length, FlatDrops.Length, thumbSize.Height);
        var window = new ListWindow(first.TitleX, top, x + upSize.Width - first.TitleX, bottom - top,
            x, ListWindow.ThumbYFor(trackTop, trackHeight, thumbHeight, _top, LastTop), thumbSize.Width, thumbHeight,
            trackTop, trackHeight, FlatDrops.Length, DropSlots.Length, _top);
        return new VideoBar(window, up, upSize, down, downSize, thumb);
    }

    // One row's shape off the section's own widgets, falling back to the authored numbers. The title
    // box stops at the control beside it, every Video title being authored 162 wide. A widthless
    // description wraps at the plaque column. A dropdown takes its box column and width from its
    // home line, so no box resizes as the window scrolls. It stands on the line it scrolled to.
    private VideoPlacement Place(MenuLayoutScreen screen, VideoSlot slot, OriginalRowKind kind, VideoSlot? home = null)
    {
        var title = screen.Widget(slot.TitleKey);
        var control = screen.Widget(slot.ControlKey);
        var own = screen.Widget((home ?? slot).ControlKey);
        var description = screen.Widget(slot.DescriptionKey);
        bool drop = kind == OriginalRowKind.Dropdown;
        float titleX = title?.Int("X", (int)TitleX) ?? TitleX;
        float titleY = title?.Int("Y", (int)ShadowsY) ?? ShadowsY;
        var box = drop
            ? OriginalOptionsChrome.StripArt(own?.Art ?? Array.Empty<string>(), 4)
            : OriginalOptionsChrome.StripArt(control?.Art ?? Array.Empty<string>(), 0, control?.Frames ?? 8);
        float fallbackX = drop ? DropX : titleX + CheckDx;
        float fallbackY = drop ? titleY : titleY + CheckDy;
        float boxX = (drop ? own : control)?.Int("X", (int)fallbackX) ?? fallbackX;
        float boxY = control?.Int("Y", (int)fallbackY) ?? fallbackY;
        float boxWidth;
        float boxHeight;
        if (drop)
        {
            boxWidth = own?.Int("Width", (int)DropWidth) ?? DropWidth;
            boxHeight = own?.Int("ItemHeight", (int)ItemHeight) ?? ItemHeight;
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

    // One authored line by its three widgets: the title, the control and the description.
    private sealed record VideoSlot(string TitleKey, string ControlKey, string DescriptionKey);

    // The dropdown window as the pointer reads it, with its two arrows' art and sizes and the thumb.
    private sealed record VideoBar(
        ListWindow Window, BoardArt? Up, (float Width, float Height) UpSize,
        BoardArt? Down, (float Width, float Height) DownSize, BoardArt? Thumb);

    // One setting, by its title and, for a checkbox, the authored line it keeps; a dropdown takes the
    // window's line it scrolls to. Its description is read off the shared choices, since a row can
    // say something about the staged settings. The control it takes, the words of the store field it
    // shows, how it is read and written, and whether the row is live follow. The words come off the
    // choices too, the resolution row's being enumerated per screen rather than held as an array.
    private sealed record VideoOption(
        string Key, string Title, VideoSlot? Fixed,
        Func<OptionsChoices, string> Description, OriginalRowKind Kind,
        Func<OptionsChoices, IReadOnlyList<string>> Words,
        Func<OptionsChoices, int> Read, Action<OptionsChoices, int> Write,
        Func<OptionsChoices, bool>? Live = null, bool? HeadsetRow = null)
    {
        // Whether the row takes a press at all. A row another setting owns the value of is dead.
        // The cursor walks past it, the pointer cannot arm it, and it draws in its disabled frame.
        // Every other unavailable row on these pages already does that.
        public bool Editable(OptionsChoices choices) => Live?.Invoke(choices) ?? true;

        // Whether the row stands on the page at all: a headset row only under a live headset, a
        // flat row only without one. Two rows share each of two places in the walk.
        public bool Shows(bool headset) => HeadsetRow is not { } only || only == headset;
    }

    // One row's place in authored pixels: the title box, the control's own rectangle and strip,
    // and the description box.
    private sealed record VideoPlacement(
        float TitleX, float TitleY, float TitleWidth,
        float BoxX, float BoxY, float BoxWidth, float BoxHeight,
        float DescX, float DescY, float DescWidth, BoardArt? Box);
}

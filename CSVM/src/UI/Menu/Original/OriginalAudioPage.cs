using System;
using System.Collections.Generic;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The AUDIO page behind the Options hub, one page module over the decoded <c>[@Audio@]</c>
/// section. It is four slider rows over <see cref="CSVM.Utils.AudioMix"/>'s 0..100 on the
/// section's own lines. Master takes the In-Game Music row, since a slider reaching zero is that checkbox in one
/// fewer widget. A slider answers no Accept. <see cref="PreviewMix"/> is the mix the open page
/// stands at and <see cref="TakeMoved"/> the level a frame moved, the host applying and sounding
/// both. It leaves through the form's apply exit (<see cref="IOriginalOptionsForm"/>).
/// </summary>
public sealed class OriginalAudioPage : IOriginalOptionsPage
{
    /// <summary>The AUDIO page's layout section.</summary>
    public const string Section = "Audio";

    /// <summary>ACCEPT CHANGES, which leaves as the options apply carrying every choice.</summary>
    public const string AcceptKey = "AP_B_ACCEPTCHANGES";

    /// <summary>CANCEL CHANGES, back to the hub with the levels dropped.</summary>
    public const string CancelKey = "AP_B_CANCELCHANGES";

    /// <summary>The page's Master slider, the page's first row.</summary>
    public const string MasterKey = "AUDIOMASTER";

    /// <summary>The page's Music Volume slider.</summary>
    public const string MusicKey = "AUDIOMUSIC";

    /// <summary>The page's Effects Volume slider.</summary>
    public const string EffectsKey = "AUDIOEFFECTS";

    /// <summary>The page's Voice Volume slider.</summary>
    public const string VoiceKey = "AUDIOVOICE";

    // The page's authored row shape, used where a layout does not carry the section or one of its
    // rows. It is the title column and its box, the slider column, and the distance from a title's
    // line down to its own slider's. The description column and its width follow. Each row's own
    // line is on its table entry instead, the authored pitch being four different numbers rather
    // than one (docs/org/menu-inventory.md).
    private const float TitleX = 137f;
    private const float TitleWidth = 170f;
    private const float SliderX = 137f;
    private const float SliderOffsetFallback = 26f;
    private const float DescX = 348f;
    private const float DescWidth = 310f;

    // The aid's four levels, one per row and all distinct. A single shot then shows the thumb at
    // four places on the track rather than at the shipped default three times over.
    private const int PoseMaster = 100;
    private const int PoseMusic = 25;
    private const int PoseEffects = 60;
    private const int PoseVoice = 85;

    // The levels, in the authored order of the rows they stand on. The cursor walks the table, and
    // a form is read top to bottom. Each is a title, the authored title, control and description
    // widgets it stands on, and a description. Each also carries the authored lines those two texts
    // fall back to, and how the store field is read and written. A level is never a vocabulary
    // word, so a row reads a never-set field as the shipped default rather than as a first word.
    private static readonly AudioOption[] Options =
    {
        new(MasterKey, MenuMixLevel.Master, "Master", "AP_T_MusicTitle", null, "AP_T_MusicDesc",
            "Set the overall volume of all sounds.", 266f, 278f,
            s => s._master ?? CSVM.Utils.AudioMix.DefaultMaster,
            (s, v) => s._master = v),
        new(MusicKey, MenuMixLevel.Music, "Music Volume", "AP_T_MVolTitle", "AP_S_MVOLUME", "AP_T_MVolDesc",
            "Set the volume of the in-game music.", 324f, 332f,
            s => s._music ?? CSVM.Utils.AudioMix.DefaultMusic,
            (s, v) => s._music = v),
        new(EffectsKey, MenuMixLevel.Effects, "Effects Volume", "AP_T_EVolTitle", "AP_S_EVOLUME", "AP_T_EVolDesc",
            "Set the volume of the sound effects.", 381f, 387f,
            s => s._effects ?? CSVM.Utils.AudioMix.DefaultEffects,
            (s, v) => s._effects = v),
        new(VoiceKey, MenuMixLevel.Voice, "Voice Volume", "AP_T_VVolTitle", "AP_S_VVOLUME", "AP_T_VVolDesc",
            "Set the volume of the voices.", 434f, 442f,
            s => s._voice ?? CSVM.Utils.AudioMix.DefaultVoice,
            (s, v) => s._voice = v),
    };

    // The first authored slider row, which is where the page's slider geometry is read from. The
    // Master row has no authored slider of its own. Its authored row carries the In-Game Music
    // checkbox, which stands at another column and on the title's own line. A Master slider placed
    // at that widget's corner would sit 122 pixels right of the three below it.
    private static readonly AudioOption SliderRowFallback = Options[1];

    private readonly OriginalOptionsChrome _chrome;
    private readonly IOriginalScreenHost _host;
    private readonly IOriginalOptionsForm _form;

    // Which level a row's slider moved, cleared by the host's read of it. The control writes only
    // where the value actually changed. This stands at None through every frame of a drag that held
    // the thumb still, which keeps a preview off a pointer's frame rate.
    private MenuMixLevel _moved;
    // The four saved volume levels, null while never set. The other pages carry them too, since
    // every page's apply hands back the settings it does not show.
    private int? _master;
    private int? _music;
    private int? _effects;
    private int? _voice;

    internal OriginalAudioPage(OriginalOptionsChrome chrome, IOriginalOptionsForm form)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _host = chrome.Host;
    }

    /// <summary>The mix the page stands at while it is open. The host applies it, so a level can be
    /// judged by ear as it moves. It is null on every other screen, which is what makes leaving
    /// this page by any door drop the preview. Which bus each level reaches, and whether a preview
    /// sounds at all, are the audio service's.</summary>
    public CSVM.Utils.AudioLevels? PreviewMix =>
        _host.Screen == OriginalScreen.Audio
            ? new CSVM.Utils.AudioLevels(
                Level(MenuMixLevel.Master), Level(MenuMixLevel.Music),
                Level(MenuMixLevel.Effects), Level(MenuMixLevel.Voice))
            : null;

    /// <summary>The Master level (<see cref="CSVM.Utils.AudioMix"/>'s 0..100) the page would apply,
    /// or null while nothing has been saved and no row has been touched.</summary>
    public int? MasterChoice => _master;

    /// <summary>The Music level the page would apply, or null while never set.</summary>
    public int? MusicChoice => _music;

    /// <summary>The Effects level the page would apply, or null while never set.</summary>
    public int? EffectsChoice => _effects;

    /// <summary>The Voice level the page would apply, or null while never set.</summary>
    public int? VoiceChoice => _voice;

    OriginalScreen IOriginalOptionsPage.Screen => OriginalScreen.Audio;

    string IOriginalOptionsPage.AcceptKey => AcceptKey;

    string IOriginalOptionsPage.CancelKey => CancelKey;

    /// <summary>Opens the page on the saved mix with Master focused, which is what the hub's AUDIO
    /// door and the screenshot aid both go through.</summary>
    public void Open()
    {
        _host.Open(OriginalScreen.Audio);
        _host.FocusedRow = -1;
    }

    /// <summary>Stands the four rows at four distinct levels, the screenshot aid's pose for a page
    /// whose four thumbs otherwise sit at two places. Nothing happens off the AUDIO page, and
    /// nothing is saved: the pose is an edit the aid's shot is taken of.</summary>
    public void PoseMix()
    {
        if (_host.Screen != OriginalScreen.Audio)
        {
            return;
        }

        _master = PoseMaster;
        _music = PoseMusic;
        _effects = PoseEffects;
        _voice = PoseVoice;
    }

    /// <summary>Which level has moved since this was last asked, and <see cref="MenuMixLevel.None"/>
    /// when none has. ⚠ Taken rather than read: the host sounds the moved category, so a caller that
    /// saw one move twice would sound it twice.</summary>
    public MenuMixLevel TakeMoved()
    {
        var moved = _moved;
        _moved = MenuMixLevel.None;
        return moved;
    }

    // Each level's slider on its authored line and the two plaques beside them, all one column.
    // Without the section each level stands as a text button carrying its value. The pageless
    // composer draws no slider, and the page still says what the mix is.
    void IOriginalOptionsPage.BuildRows(List<OriginalRow> rows)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            for (int i = 0; i < Options.Length; i++)
            {
                var fallback = Options[i];
                rows.Add(_host.PlaqueRow(fallback.Key, $"{fallback.Title} {fallback.Read(this)}", i, true, 0));
            }

            _chrome.AddFallbackPlaques(rows, AcceptKey, CancelKey, Options.Length);
            return;
        }

        foreach (var option in Options)
        {
            var place = Place(screen, option);
            var control = option.ControlKey is { } key ? screen.Widget(key) : null;
            // The moved level is recorded here rather than in the table's own write. The control
            // calls this only where the value actually changed. A drag that held the thumb on the
            // same whole number records nothing, and the host sounds nothing for it.
            rows.Add(_chrome.SliderRow(control, option.Key, place.SliderX, place.SliderY,
                CSVM.Utils.AudioMix.MinLevel, CSVM.Utils.AudioMix.MaxLevel,
                option.Read(this),
                v =>
                {
                    option.Write(this, v);
                    _moved = option.Level;
                }));
        }

        _chrome.AddPlaques(screen, rows, AcceptKey, CancelKey);
    }

    // The page has no list, its rows all in view.
    void IOriginalOptionsPage.Lists(List<OriginalList> lists)
    {
    }

    // The sliders are the shell's own step, which runs ahead of the page's.
    bool IOriginalOptionsPage.StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) => false;

    bool IOriginalOptionsPage.CloseDropdown() => false;

    // A slider row answers nothing. Its value moves under the pointer or by a sideways step, and an
    // Accept that also moved it would have no opposite.
    MenuExit? IOriginalOptionsPage.Activate(OriginalRow row) => null;

    // The settings this page does not show ride the apply unchanged, read back when it opened.
    MenuExit? IOriginalOptionsPage.Accept() => _form.Apply();

    void IOriginalOptionsPage.Cancel() => _form.Leave();

    // The page carries no list, so the first Back leaves.
    void IOriginalOptionsPage.Back() => _form.Leave();

    // The page as drawn. It is the page's background and its title. Each level's title and
    // description follow at their authored columns, with the sliders and plaques over them.
    void IOriginalOptionsPage.Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            _host.ComposePlainPage("AUDIO", rows, focus, layers);
            return;
        }

        _chrome.ComposePlate(screen, "AP_BACKGROUND", layers.Backdrop);
        _chrome.ComposePageTitle(screen, "AP_T_TITLE", "AUDIO", layers.Lines);

        // The focused row's title in the focused ink, the mark a list row takes. The cursor then
        // shows where the eye already reads the row's name. The rows are built from this table in
        // this order, so a level's index is its row's.
        for (int i = 0; i < Options.Length; i++)
        {
            var option = Options[i];
            var place = Place(screen, option);
            layers.Lines.Add(new BoardLine(option.Title, place.TitleX, place.TitleY, place.TitleWidth,
                OriginalOptionsChrome.TitleFont, i == focus ? BoardInk.RowFocused : BoardInk.Row));
            layers.Lines.Add(new BoardLine(option.Description, place.DescX, place.DescY, place.DescWidth,
                OriginalOptionsChrome.DescriptionFont, BoardInk.Row));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            _host.ComposeGenericRow(rows[i], i == focus, i == _host.PressedRow, i, layers);
        }
    }

    /// <summary>Takes the four levels this page shows off <paramref name="saved"/>, never set where
    /// it is null.</summary>
    internal void Read(CSVM.Utils.OptionsDef? saved)
    {
        _master = saved?.AudioMaster;
        _music = saved?.AudioMusic;
        _effects = saved?.AudioEffects;
        _voice = saved?.AudioVoice;
    }

    // The column every slider stands in, read off the first authored slider row. It places the
    // Master row, whose own authored widget is the checkbox. A row with a slider of its own is
    // placed by that widget, and reaches this only when the layout has dropped it.
    private static float SliderColumnX(MenuLayoutScreen screen) =>
        screen.Widget(SliderRowFallback.ControlKey!)?.Int("X", (int)SliderX) ?? SliderX;

    // The drop from a title's line to its own slider's, read off the same row.
    private static float SliderOffset(MenuLayoutScreen screen)
    {
        var title = screen.Widget(SliderRowFallback.TitleKey);
        var slider = screen.Widget(SliderRowFallback.ControlKey!);
        return title != null && slider != null ? slider.Int("Y") - title.Int("Y") : SliderOffsetFallback;
    }

    // One row's shape off the section's own widgets, each number falling back to the authored one
    // when the row is not there. The lines are read per row rather than off a first row and a
    // pitch. This section's pitch changes from row to row (docs/org/menu-inventory.md). A single
    // pitch would misplace the rows under the second by up to five pixels each.
    private static AudioPlacement Place(MenuLayoutScreen screen, AudioOption option)
    {
        var title = screen.Widget(option.TitleKey);
        var description = screen.Widget(option.DescriptionKey);
        float titleY = title?.Int("Y", (int)option.TitleY) ?? option.TitleY;
        return new AudioPlacement(
            title?.Int("X", (int)TitleX) ?? TitleX,
            titleY,
            title?.Int("Width", (int)TitleWidth) ?? TitleWidth,
            SliderColumnX(screen),
            titleY + SliderOffset(screen),
            description?.Int("X", (int)DescX) ?? DescX,
            description?.Int("Y", (int)option.DescY) ?? option.DescY,
            description?.Int("Width", (int)DescWidth) ?? DescWidth);
    }

    // The level a row stands at, found through its own table entry so a level and its shipped
    // fallback keep one definition. Every member but None is in the table, so the last answer is
    // unreachable. It is the resting level rather than silence, a preview being no place to invent
    // a mute.
    private int Level(MenuMixLevel level)
    {
        foreach (var option in Options)
        {
            if (option.Level == level)
            {
                return option.Read(this);
            }
        }

        return CSVM.Utils.AudioMix.MaxLevel;
    }

    // One level, by its key and which of the mix's levels it is for the host that hears one move.
    // Its title and the authored widgets it composes over: the title, the slider and the
    // description, the slider null where the row has none. Its description, the authored lines the
    // title and description fall back to, and how the store field is read and written follow. The
    // description is a fixed string, since no level says anything the thumb does not already show.
    private sealed record AudioOption(
        string Key, MenuMixLevel Level, string Title, string TitleKey, string? ControlKey,
        string DescriptionKey, string Description, float TitleY, float DescY,
        Func<OriginalAudioPage, int> Read, Action<OriginalAudioPage, int> Write);

    // One row's place in authored pixels. It is the title box, the corner its slider stands at
    // where the row authors no slider of its own, and the description box.
    private sealed record AudioPlacement(
        float TitleX, float TitleY, float TitleWidth,
        float SliderX, float SliderY,
        float DescX, float DescY, float DescWidth);
}

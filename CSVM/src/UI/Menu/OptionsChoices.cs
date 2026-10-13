using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Flight.Camera;
using CSVM.Utils;
using DifficultyTiers = CSVM.Flight.Hangar.Difficulty;

namespace CSVM.UI.Menu;

/// <summary>
/// The settings an Options screen stages before the apply, and the rules tying them together.
/// Borderless pins the size, and a volume step clamps where other rows wrap. Each registered word
/// setting (<see cref="WordSettings.All"/>) stages in <see cref="Words"/> under its own rules.
/// Built-in walks it as numbered rows, each defined once in a table (<see cref="Step"/>,
/// <see cref="Label"/>, <see cref="Detail"/>). Original's three settings pages read and write the
/// same values from their own authored rows. Both read the saved options through <see cref="Load"/>
/// and leave through <see cref="ToExit"/>, so an Options apply writes through <c>LiveSettings.Persist</c>.
/// </summary>
public sealed class OptionsChoices
{
    // The GAME OPTIONS page's three in its order, then the targeting switch, the rumble and the
    // graphics mode.
    private static readonly Row[] GameRows =
    {
        new("Difficulty", c => DifficultyTiers.Label(c.Difficulty), (c, d) => c.Difficulty = Wrap(DifficultyTiers.Clamp(c.Difficulty) + d, 3),
            _ => "Select the difficulty level for a solo campaign. Enemy armour and health scale with it at spawn."),
        new("Default View", c => PilotView.Label(c.DefaultViewMode), (c, d) => c.DefaultView = PilotView.Name(PilotView.Step(c.DefaultViewMode, d)),
            _ => "Select your default view. A --view= on the command line still outranks it."),
        new("Auto Head Turn", c => OnOff(c.AutoHeadTurnOn), (c, _) => c.AutoHeadTurn = !c.AutoHeadTurnOn,
            _ => "Select to turn your head automatically as your aircraft turns. Cockpit views only."),
        new("Nearest target after a kill", c => OnOff(c.NearestAfterKillOn), (c, _) => c.NearestAfterKill = !c.NearestAfterKillOn,
            _ => "Take the nearest target after a kill instead of the first of the list."),
        new("Controller rumble", c => OnOff(c.RumbleOn), (c, _) => c.Rumble = !c.RumbleOn,
            _ => "Rumble the gamepad for guns, launches, hits, the nitro and a dive past the rated maximum."),
        new("Graphics", c => c.Enhanced ? "Enhanced" : "Original",
            (c, _) => c.Graphics = c.Enhanced ? GraphicsMode.Default : GraphicsMode.EnhancedWord,
            _ => "Original is the faithful world; Enhanced lights it. Applies at once."),
    };

    // The monitor, size, mode and V-Sync in the VIDEO page's order.
    private static readonly Row[] DisplayRows =
    {
        new("Monitor", c => c.Screens.Labels[c.MonitorAt], (c, d) => c.MonitorIndex = MonitorSetting.Word(DisplaySettingRows.Step(c.MonitorAt, d, c.Screens.Labels.Count)),
            _ => "Select the monitor the game opens on. Applied on the way out, before the size."),
        new("Resolution", c => c.Sizes.Words[c.ResolutionAt], (c, d) => c.StepResolution(d), c => c.ResolutionDetail()),
        new("Display mode", c => DisplaySettingRows.DisplayModeLabels[c.DisplayModeAt],
            (c, d) => c.DisplayMode = DisplayWords.DisplayModes[DisplaySettingRows.Step(c.DisplayModeAt, d, DisplayWords.DisplayModes.Count)],
            _ => "Select how the window sits on the screen. Borderless leaves the desktop beneath it."),
        new("V-Sync", c => DisplaySettingRows.VSyncLabels[c.VSyncAt],
            (c, d) => c.VSync = DisplayWords.VSyncChoices[DisplaySettingRows.Step(c.VSyncAt, d, DisplayWords.VSyncChoices.Count)],
            _ => "Select the frame pacing. On follows the screen; off runs free, or to a frame cap."),
    };

    // The four volume levels in the AUDIO page's order.
    private static readonly Row[] AudioRows =
    {
        new("Master volume", c => LevelLabel(c.AudioMaster, AudioMix.DefaultMaster), (c, d) => c.AudioMaster = StepLevel(c.AudioMaster, AudioMix.DefaultMaster, d),
            _ => "Set the overall volume of all sounds. Heard once the choices are applied."),
        new("Music volume", c => LevelLabel(c.AudioMusic, AudioMix.DefaultMusic), (c, d) => c.AudioMusic = StepLevel(c.AudioMusic, AudioMix.DefaultMusic, d),
            _ => "Set the volume of the in-game music. Heard once the choices are applied."),
        new("Effects volume", c => LevelLabel(c.AudioEffects, AudioMix.DefaultEffects), (c, d) => c.AudioEffects = StepLevel(c.AudioEffects, AudioMix.DefaultEffects, d),
            _ => "Set the volume of the sound effects. Heard once the choices are applied."),
        new("Voice volume", c => LevelLabel(c.AudioVoice, AudioMix.DefaultVoice), (c, d) => c.AudioVoice = StepLevel(c.AudioVoice, AudioMix.DefaultVoice, d),
            _ => "Set the volume of the voices. Heard once the choices are applied."),
    };

    // The headset's three in the VIDEO page's order. VR mode steps on Windows alone, PC VR being the
    // Windows build's.
    private static readonly Row[] VrRows =
    {
        new("VR mode (experimental)", c => OnOff(c.VrMode == true), (c, _) => c.VrMode = OperatingSystem.IsWindows() ? c.VrMode != true : c.VrMode,
            _ => DisplaySettingRows.VrModeDetail(OperatingSystem.IsWindows())),
        new("VR render scale", c => VrRenderScaleSetting.Label(c.VrRenderScale),
            (c, d) => c.VrRenderScale = StepVr(VrRenderScaleSetting.Words, c.VrRenderScale, VrRenderScaleSetting.Default, d),
            _ => DisplaySettingRows.VrRenderScaleDetail),
        new("VR comfort vignette", c => VrVignetteSetting.Label(c.VrVignette),
            (c, d) => c.VrVignette = StepVr(VrVignetteSetting.Words, c.VrVignette, VrVignetteSetting.Default, d),
            _ => DisplaySettingRows.VrVignetteDetail),
    };

    // Built-in's stepper rows, top to bottom. The game rows, the word settings under the graphics row,
    // the display rows, the other word settings, the VR rows, then the volume levels.
    private static readonly Row[] Rows = GameRows
        .Concat(WordSettings.All.Where(static s => s.UnderGraphicsRow).Select(WordRow))
        .Concat(DisplayRows)
        .Concat(WordSettings.All.Where(static s => !s.UnderGraphicsRow).Select(WordRow))
        .Concat(VrRows)
        .Concat(AudioRows)
        .ToArray();

    private readonly Func<SizeList> _screenSizes;
    private readonly Func<ScreenList> _screens;

    // The size the options file named at the last Load, which the size row offers as an entry of its
    // own (Sizes). Held apart from the stepped choice, a hand-written size stays in the list after a
    // step lands elsewhere. A step back then reaches it again.
    private string? _savedResolution;

    /// <summary>Choices over <paramref name="screenSizes"/> and <paramref name="screens"/>, read on
    /// every access since a monitor can be plugged in while a screen stands open. Null offers every
    /// candidate size and the one screen a shell with no engine can name.</summary>
    public OptionsChoices(Func<SizeList>? screenSizes = null, Func<ScreenList>? screens = null)
    {
        _screenSizes = screenSizes ?? (() => ResolutionSetting.Unknown);
        _screens = screens ?? (() => MonitorSetting.Unknown);
    }

    /// <summary>How many numbered rows <see cref="Step"/>, <see cref="Label"/> and <see cref="Detail"/>
    /// answer.</summary>
    public static int Count => Rows.Length;

    /// <summary>The difficulty tier (<see cref="CSVM.Flight.Hangar.Difficulty"/>).</summary>
    public int Difficulty { get; set; } = DifficultyTiers.Normal;

    /// <summary>The opening view as a <c>--view=</c> word, null while never set, which flies Chase.</summary>
    public string? DefaultView { get; set; }

    /// <summary>The automatic head turn, null while never set, which leaves the config key deciding.</summary>
    public bool? AutoHeadTurn { get; set; }

    /// <summary>The targeting setting, null while never set, which the consumer reads as off.</summary>
    public bool? NearestAfterKill { get; set; }

    /// <summary>The haptics setting, null while never set, which the consumer reads as on.</summary>
    public bool? Rumble { get; set; }

    /// <summary>The <see cref="GraphicsMode"/> word.</summary>
    public string Graphics { get; set; } = GraphicsMode.Default;

    /// <summary>The screen index in <see cref="MonitorSetting.Word"/>'s spelling, null while never set.</summary>
    public string? MonitorIndex { get; set; }

    /// <summary>The window size in <see cref="OptionsStore.FormatResolution"/>'s spelling, null while
    /// never set.</summary>
    public string? Resolution { get; set; }

    /// <summary>The <see cref="DisplayWords.DisplayModes"/> word, null while never set.</summary>
    public string? DisplayMode { get; set; }

    /// <summary>The <see cref="DisplayWords.VSyncChoices"/> word, null while never set.</summary>
    public string? VSync { get; set; }

    /// <summary>The word staged for each registered setting, null while never set. Set through
    /// <see cref="Pick"/>, which keeps a setting's partners in step.</summary>
    public WordValues Words { get; private set; } = new();

    /// <summary>VR mode, null while never set, which reads as off.</summary>
    public bool? VrMode { get; set; }

    /// <summary>The <see cref="VrRenderScaleSetting.Words"/> word, null while never set.</summary>
    public string? VrRenderScale { get; set; }

    /// <summary>The <see cref="VrVignetteSetting.Words"/> word, null while never set.</summary>
    public string? VrVignette { get; set; }

    /// <summary>The Master level (<see cref="AudioMix"/>'s 0..100), null while never set.</summary>
    public int? AudioMaster { get; set; }

    /// <summary>The Music level, null while never set.</summary>
    public int? AudioMusic { get; set; }

    /// <summary>The Effects level, null while never set.</summary>
    public int? AudioEffects { get; set; }

    /// <summary>The Voice level, null while never set.</summary>
    public int? AudioVoice { get; set; }

    /// <summary>Whether the graphics word is Enhanced, which an Enhanced-only word setting needs to
    /// step at all. Under Original each keeps its word for a later flip.</summary>
    public bool Enhanced => Graphics == GraphicsMode.EnhancedWord;

    /// <summary>Whether the display mode owns the size, which borderless does. The size then reads as
    /// the screen's own and takes no step, the saved size kept for Windowed or Fullscreen.</summary>
    public bool ResolutionPinned => ResolutionSetting.Pinned(DisplayMode);

    /// <summary>The opening view as a mode, Chase while never set.</summary>
    public PilotViewMode DefaultViewMode => PilotView.Parse(DefaultView ?? string.Empty) ?? PilotViewMode.Chase;

    /// <summary>The head turn as a switch reads it: on only once set on.</summary>
    public bool AutoHeadTurnOn => AutoHeadTurn == true;

    /// <summary>The targeting setting as a switch reads it: on only once set on.</summary>
    public bool NearestAfterKillOn => NearestAfterKill == true;

    /// <summary>The rumble as a switch reads it: on until set off, as the original ships it.</summary>
    public bool RumbleOn => Rumble != false;

    /// <summary>The machine's screens, read through the reader on every access.</summary>
    public ScreenList Screens => _screens();

    /// <summary>The sizes the size setting offers: the standing screen's own, widened with the size
    /// the options file named at the last <see cref="Load"/>. A hand-written size so stands where it
    /// sorts.</summary>
    public SizeList Sizes => _screenSizes().Including(_savedResolution);

    /// <summary>Where the monitor stands among <see cref="Screens"/>, through <see cref="MonitorSetting.Resolve"/>,
    /// so it cannot name a screen the apply would not move the window to.</summary>
    public int MonitorAt => MonitorSetting.Resolve(MonitorIndex, Screens).Screen;

    /// <summary>Where the size stands among <see cref="Sizes"/>.</summary>
    public int ResolutionAt => DisplaySettingRows.ResolutionIndex(Sizes, Resolution, DisplayMode);

    /// <summary>Where the display mode stands among <see cref="DisplayWords.DisplayModes"/>.</summary>
    public int DisplayModeAt => DisplaySettingRows.WordIndex(DisplayWords.DisplayModes, DisplayMode, DisplayModeSetting.Default);

    /// <summary>Where V-Sync stands among <see cref="DisplayWords.VSyncChoices"/>.</summary>
    public int VSyncAt => DisplaySettingRows.WordIndex(DisplayWords.VSyncChoices, VSync, VSyncSetting.Default);

    /// <summary>Takes every setting off <paramref name="saved"/>, the shipped defaults where it is
    /// null. What was asked for, not what this process resolved from a flag or a config key.</summary>
    public void Load(OptionsDef? saved)
    {
        Difficulty = DifficultyTiers.Parse(saved?.Difficulty) ?? DifficultyTiers.Normal;
        NearestAfterKill = saved?.NearestAfterKill;
        Rumble = saved?.Rumble;
        DefaultView = saved?.DefaultView;
        AutoHeadTurn = saved?.AutoHeadTurn;
        Graphics = saved?.GraphicsMode ?? GraphicsMode.Default;
        Words = saved?.Words.Copy() ?? new WordValues();
        MonitorIndex = saved?.MonitorIndex;
        Resolution = saved?.Resolution;
        _savedResolution = saved?.Resolution;
        DisplayMode = saved?.DisplayMode;
        VSync = saved?.VSync;
        VrMode = saved?.VrMode;
        VrRenderScale = saved?.VrRenderScale;
        VrVignette = saved?.VrVignette;
        AudioMaster = saved?.AudioMaster;
        AudioMusic = saved?.AudioMusic;
        AudioEffects = saved?.AudioEffects;
        AudioVoice = saved?.AudioVoice;
    }

    /// <summary>The apply exit carrying every setting, never-set ones as null.</summary>
    public OptionsApplyExit ToExit() =>
        new(Graphics, DifficultyTiers.Word(Difficulty), MonitorIndex, Resolution, DisplayMode, VSync,
            AudioMaster, AudioMusic, AudioEffects, AudioVoice,
            NearestAfterKill, Rumble, DefaultView, AutoHeadTurn, Words.Copy(), VrMode, VrRenderScale, VrVignette);

    /// <summary>The words <paramref name="setting"/> offers under the other staged choices.</summary>
    public IReadOnlyList<string> Offered(WordSetting setting) => setting.Offered?.Invoke(Words, Enhanced) ?? setting.Words;

    /// <summary>Where <paramref name="setting"/> stands among <see cref="Offered"/>: the staged word, or
    /// the word a never-set row shows.</summary>
    public int At(WordSetting setting) => DisplaySettingRows.WordIndex(Offered(setting), Words[setting], setting.Shown(Enhanced));

    /// <summary>Whether <paramref name="setting"/> takes a step under the standing graphics word.</summary>
    public bool Live(WordSetting setting) => !setting.EnhancedOnly || Enhanced;

    /// <summary>Stages <paramref name="word"/> for <paramref name="setting"/> and keeps its partners in
    /// step, as FSR 2.2 pulls a scale above native down to native.</summary>
    public void Pick(WordSetting setting, string word)
    {
        Words[setting] = word;
        setting.Picked?.Invoke(Words, word);
    }

    /// <summary>A sideways step of <paramref name="direction"/> on numbered row <paramref name="row"/>.
    /// A row that is dead under the standing choices takes nothing.</summary>
    public void Step(int row, int direction) => Rows[row].Step(this, direction);

    /// <summary>What numbered row <paramref name="row"/> draws: its title and its value.</summary>
    public string Label(int row) => $"{Rows[row].Title}: {Rows[row].Value(this)}";

    /// <summary>What numbered row <paramref name="row"/> says it does, under the standing choices.</summary>
    public string Detail(int row) => Rows[row].Detail(this);

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    private static string OnOff(bool on) => on ? "On" : "Off";

    // One volume step, the Original AUDIO slider's keyboard step, so both presentations write the
    // same levels. It clamps at both ends, since a step from silence must not land on full volume. A
    // step that moves nothing keeps a never-set level never set.
    private static int? StepLevel(int? level, int shipped, int dir)
    {
        int from = Math.Clamp(level ?? shipped, AudioMix.MinLevel, AudioMix.MaxLevel);
        int to = Math.Clamp(from + (dir * Original.SliderControl.KeyStep), AudioMix.MinLevel, AudioMix.MaxLevel);
        return to == from ? level : to;
    }

    // A VR word row's step over its vocabulary, a never-set word standing on the default.
    private static string StepVr(IReadOnlyList<string> words, string? word, string fallback, int dir) =>
        words[DisplaySettingRows.Step(DisplaySettingRows.WordIndex(words, word, fallback), dir, words.Count)];

    private static string LevelLabel(int? level, int shipped) =>
        Math.Clamp(level ?? shipped, AudioMix.MinLevel, AudioMix.MaxLevel).ToString(CultureInfo.InvariantCulture);

    // A word setting's row: its title, the label of the word it stands on, a step that clamps or
    // wraps, and its detail.
    private static Row WordRow(WordSetting setting) => new(
        setting.Title,
        c => setting.Label(c.Offered(setting)[c.At(setting)]),
        (c, d) => c.StepWord(setting, d),
        c => setting.Detail(c.Enhanced));

    private void StepResolution(int dir)
    {
        if (!ResolutionPinned)
        {
            var sizes = Sizes;
            Resolution = sizes.Words[DisplaySettingRows.Step(ResolutionAt, dir, sizes.Words.Count)];
        }
    }

    // A clamping row settles on its end under a held arrow; every other row wraps.
    private void StepWord(WordSetting setting, int dir)
    {
        if (!Live(setting))
        {
            return;
        }

        var offered = Offered(setting);
        int at = At(setting);
        Pick(setting, offered[setting.Clamps ? Math.Clamp(at + dir, 0, offered.Count - 1) : DisplaySettingRows.Step(at, dir, offered.Count)]);
    }

    // The size does something different in each display mode, and under borderless nothing at all.
    // A stepper that refuses without saying why reads as a broken row.
    private string ResolutionDetail()
    {
        if (ResolutionPinned)
        {
            return "Borderless runs at the desktop's own size. Pick Windowed or Fullscreen to choose one.";
        }

        return DisplayMode == DisplayWords.Fullscreen
            ? "Select the size the game draws at, scaled up to fill the fullscreen window."
            : "Select the window size. The list is what the screen the window stands on can hold.";
    }

    // One numbered row: its title, the value it draws, its step and its detail. All four sit in one
    // entry, so a row cannot lose one of them to its neighbour.
    private sealed record Row(
        string Title,
        Func<OptionsChoices, string> Value,
        Action<OptionsChoices, int> Step,
        Func<OptionsChoices, string> Detail);
}

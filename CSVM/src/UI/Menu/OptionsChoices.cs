using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Camera;
using CSVM.Utils;
using DifficultyTiers = CSVM.Flight.Hangar.Difficulty;
using ViewDistances = CSVM.Utils.ViewDistance;

namespace CSVM.UI.Menu;

/// <summary>
/// The nineteen settings an Options screen stages before the apply, and the rules tying them
/// together. Borderless pins the size, and FSR 2.2 clamps the render scale. The view distance,
/// shadow and water step only under Enhanced, and a volume step clamps where other rows wrap.
/// Built-in walks it as numbered rows, each defined once in a table (<see cref="Step"/>,
/// <see cref="Label"/>, <see cref="Detail"/>). Original's three settings pages read and write the
/// same values from their own authored rows. Both read the saved options through <see cref="Load"/>
/// and leave through <see cref="ToExit"/>, so <c>Launcher.ApplyOptions</c> stays the one writer.
/// </summary>
public sealed class OptionsChoices
{
    // Built-in's stepper rows, top to bottom. The GAME OPTIONS page's three come first in its order,
    // then the targeting switch, the rumble, the graphics mode and its view distance. The six display
    // settings and the shadow quality follow in the VIDEO page's order, then the water quality. The
    // four volume levels close in the AUDIO page's order.
    private static readonly Row[] Rows =
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
        new("View distance (Enhanced only)", c => ViewDistances.Label(c.ViewDistance), (c, d) => c.StepViewDistance(d),
            c => c.Enhanced
                ? "How far buildings and scenery draw before they fade; the haze stays. Applies at once."
                : "Enhanced Graphics only: choose Enhanced above to set how far buildings and scenery draw."),
        new("Monitor", c => c.Screens.Labels[c.MonitorAt], (c, d) => c.MonitorIndex = MonitorSetting.Word(DisplaySettingRows.Step(c.MonitorAt, d, c.Screens.Labels.Count)),
            _ => "Select the monitor the game opens on. Applied on the way out, before the size."),
        new("Resolution", c => c.Sizes.Words[c.ResolutionAt], (c, d) => c.StepResolution(d), c => c.ResolutionDetail()),
        new("Display mode", c => DisplaySettingRows.DisplayModeLabels[c.DisplayModeAt],
            (c, d) => c.DisplayMode = DisplayWords.DisplayModes[DisplaySettingRows.Step(c.DisplayModeAt, d, DisplayWords.DisplayModes.Count)],
            _ => "Select how the window sits on the screen. Borderless leaves the desktop beneath it."),
        new("V-Sync", c => DisplaySettingRows.VSyncLabels[c.VSyncAt],
            (c, d) => c.VSync = DisplayWords.VSyncChoices[DisplaySettingRows.Step(c.VSyncAt, d, DisplayWords.VSyncChoices.Count)],
            _ => "Select the frame pacing. On follows the screen; off runs free, or to a frame cap."),
        new("Render scale", c => DisplaySettingRows.RenderScaleLabels(c.RenderScaleWords)[c.RenderScaleAt],
            (c, d) => c.RenderScale = c.RenderScaleWords[DisplaySettingRows.Step(c.RenderScaleAt, d, c.RenderScaleWords.Count)],
            _ => "Render the world below native to spare the GPU, or above it for cleaner edges. Applies at once."),
        new("Anti-aliasing", c => DisplaySettingRows.AntiAliasingLabels[c.AntiAliasingAt],
            (c, d) => c.PickAntiAliasing(DisplayWords.AntiAliasingChoices[DisplaySettingRows.Step(c.AntiAliasingAt, d, DisplayWords.AntiAliasingChoices.Count)]),
            _ => "Select how edges are smoothed. FSR 2.2 also upscales a Render Scale below 100%. Applies at once."),
        new("Shadow quality", c => DisplaySettingRows.ShadowQualityLabels[c.ShadowQualityAt], (c, d) => c.StepShadowQuality(d),
            c => DisplaySettingRows.ShadowQualityDetail(c.Graphics)),
        new("Water quality", c => WaterQualitySetting.Label(c.WaterQuality), (c, d) => c.StepWaterQuality(d),
            c => c.Enhanced
                ? "Flat draws the original's sea; Waves draws a swell in its place. Flat runs faster. Applies at once."
                : "Enhanced Graphics only: the original world's sea is always flat."),
        new("Master volume", c => LevelLabel(c.AudioMaster, AudioMix.DefaultMaster), (c, d) => c.AudioMaster = StepLevel(c.AudioMaster, AudioMix.DefaultMaster, d),
            _ => "Set the overall volume of all sounds. Heard once the choices are applied."),
        new("Music volume", c => LevelLabel(c.AudioMusic, AudioMix.DefaultMusic), (c, d) => c.AudioMusic = StepLevel(c.AudioMusic, AudioMix.DefaultMusic, d),
            _ => "Set the volume of the in-game music. Heard once the choices are applied."),
        new("Effects volume", c => LevelLabel(c.AudioEffects, AudioMix.DefaultEffects), (c, d) => c.AudioEffects = StepLevel(c.AudioEffects, AudioMix.DefaultEffects, d),
            _ => "Set the volume of the sound effects. Heard once the choices are applied."),
        new("Voice volume", c => LevelLabel(c.AudioVoice, AudioMix.DefaultVoice), (c, d) => c.AudioVoice = StepLevel(c.AudioVoice, AudioMix.DefaultVoice, d),
            _ => "Set the volume of the voices. Heard once the choices are applied."),
    };

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

    /// <summary>The view-distance word, null while never set.</summary>
    public string? ViewDistance { get; set; }

    /// <summary>The water-quality word, null while never set.</summary>
    public string? WaterQuality { get; set; }

    /// <summary>The screen index in <see cref="MonitorSetting.Word"/>'s spelling, null while never set.</summary>
    public string? MonitorIndex { get; set; }

    /// <summary>The window size in <see cref="OptionsStore.FormatResolution"/>'s spelling, null while
    /// never set.</summary>
    public string? Resolution { get; set; }

    /// <summary>The <see cref="DisplayWords.DisplayModes"/> word, null while never set.</summary>
    public string? DisplayMode { get; set; }

    /// <summary>The <see cref="DisplayWords.VSyncChoices"/> word, null while never set.</summary>
    public string? VSync { get; set; }

    /// <summary>The render-scale word, null while never set.</summary>
    public string? RenderScale { get; set; }

    /// <summary>The anti-aliasing word, null while never set. Set through
    /// <see cref="PickAntiAliasing"/>, which keeps the render scale in step.</summary>
    public string? AntiAliasing { get; private set; }

    /// <summary>The <see cref="ShadowQualitySetting.Words"/> word, null while never set.</summary>
    public string? ShadowQuality { get; set; }

    /// <summary>The Master level (<see cref="AudioMix"/>'s 0..100), null while never set.</summary>
    public int? AudioMaster { get; set; }

    /// <summary>The Music level, null while never set.</summary>
    public int? AudioMusic { get; set; }

    /// <summary>The Effects level, null while never set.</summary>
    public int? AudioEffects { get; set; }

    /// <summary>The Voice level, null while never set.</summary>
    public int? AudioVoice { get; set; }

    /// <summary>Whether the graphics word is Enhanced, which the view distance, shadow and water
    /// settings need to step at all. Under Original each keeps its word for a later flip.</summary>
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

    /// <summary>The scales the render scale offers under the standing anti-aliasing method, fewer under
    /// FSR 2.2.</summary>
    public IReadOnlyList<string> RenderScaleWords => RenderScaleSetting.ChoicesFor(AntiAliasingWord);

    /// <summary>The anti-aliasing method standing: the saved word, else the graphics mode's own default.</summary>
    public string AntiAliasingWord => DisplaySettingRows.AntiAliasingWord(AntiAliasing, Graphics);

    /// <summary>Where the monitor stands among <see cref="Screens"/>, through <see cref="MonitorSetting.Resolve"/>,
    /// so it cannot name a screen the apply would not move the window to.</summary>
    public int MonitorAt => MonitorSetting.Resolve(MonitorIndex, Screens).Screen;

    /// <summary>Where the size stands among <see cref="Sizes"/>.</summary>
    public int ResolutionAt => DisplaySettingRows.ResolutionIndex(Sizes, Resolution, DisplayMode);

    /// <summary>Where the display mode stands among <see cref="DisplayWords.DisplayModes"/>.</summary>
    public int DisplayModeAt => DisplaySettingRows.WordIndex(DisplayWords.DisplayModes, DisplayMode, DisplayModeSetting.Default);

    /// <summary>Where V-Sync stands among <see cref="DisplayWords.VSyncChoices"/>.</summary>
    public int VSyncAt => DisplaySettingRows.WordIndex(DisplayWords.VSyncChoices, VSync, VSyncSetting.Default);

    /// <summary>Where the render scale stands among <see cref="RenderScaleWords"/>.</summary>
    public int RenderScaleAt => DisplaySettingRows.WordIndex(RenderScaleWords, RenderScale, RenderScaleSetting.Default);

    /// <summary>Where the anti-aliasing method stands among <see cref="DisplayWords.AntiAliasingChoices"/>.</summary>
    public int AntiAliasingAt => DisplaySettingRows.WordIndex(DisplayWords.AntiAliasingChoices, AntiAliasingWord, AntiAliasingWord);

    /// <summary>Where the shadow quality stands among <see cref="ShadowQualitySetting.Words"/>.</summary>
    public int ShadowQualityAt => DisplaySettingRows.WordIndex(ShadowQualitySetting.Words, ShadowQuality, ShadowQualitySetting.Word);

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
        ViewDistance = saved?.ViewDistance;
        WaterQuality = saved?.WaterQuality;
        MonitorIndex = saved?.MonitorIndex;
        Resolution = saved?.Resolution;
        _savedResolution = saved?.Resolution;
        DisplayMode = saved?.DisplayMode;
        VSync = saved?.VSync;
        RenderScale = saved?.RenderScale;
        AntiAliasing = saved?.AntiAliasing;
        ShadowQuality = saved?.ShadowQuality;
        AudioMaster = saved?.AudioMaster;
        AudioMusic = saved?.AudioMusic;
        AudioEffects = saved?.AudioEffects;
        AudioVoice = saved?.AudioVoice;
    }

    /// <summary>The apply exit carrying every setting, never-set ones as null.</summary>
    public OptionsApplyExit ToExit() =>
        new(Graphics, DifficultyTiers.Word(Difficulty), MonitorIndex, Resolution, DisplayMode, VSync,
            RenderScale, AntiAliasing, ShadowQuality, AudioMaster, AudioMusic, AudioEffects, AudioVoice,
            NearestAfterKill, Rumble, DefaultView, AutoHeadTurn, ViewDistance, WaterQuality);

    /// <summary>Picks an anti-aliasing method. FSR 2.2 refuses a scale above native, so a scale
    /// standing there moves to native; the launch clamps a saved pair the same way.</summary>
    public void PickAntiAliasing(string word)
    {
        AntiAliasing = word;
        RenderScale = RenderScaleSetting.ClampFor(RenderScale, word);
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

    private static string LevelLabel(int? level, int shipped) =>
        Math.Clamp(level ?? shipped, AudioMix.MinLevel, AudioMix.MaxLevel).ToString(CultureInfo.InvariantCulture);

    // Clamped at both ends, so a held arrow settles on Normal or Unlimited.
    private void StepViewDistance(int dir)
    {
        if (Enhanced)
        {
            ViewDistance = ViewDistances.Words[Math.Clamp(ViewDistances.Index(ViewDistance) + dir, 0, ViewDistances.Words.Length - 1)];
        }
    }

    private void StepResolution(int dir)
    {
        if (!ResolutionPinned)
        {
            var sizes = Sizes;
            Resolution = sizes.Words[DisplaySettingRows.Step(ResolutionAt, dir, sizes.Words.Count)];
        }
    }

    private void StepShadowQuality(int dir)
    {
        if (Enhanced)
        {
            ShadowQuality = ShadowQualitySetting.Words[DisplaySettingRows.Step(ShadowQualityAt, dir, ShadowQualitySetting.Words.Count)];
        }
    }

    private void StepWaterQuality(int dir)
    {
        if (Enhanced)
        {
            var words = WaterQualitySetting.Words;
            WaterQuality = words[DisplaySettingRows.Step(DisplaySettingRows.WordIndex(words, WaterQuality, WaterQualitySetting.Word), dir, words.Count)];
        }
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

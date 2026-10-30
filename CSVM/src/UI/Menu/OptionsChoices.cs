using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Camera;
using CSVM.Utils;
using DifficultyTiers = CSVM.Flight.Hangar.Difficulty;

namespace CSVM.UI.Menu;

/// <summary>
/// The settings an Options screen stages before the apply, and the rules tying them together.
/// Borderless pins the size. Each registered word setting (<see cref="WordSettings.All"/>) stages
/// in <see cref="Words"/> under its own rules. Original's three settings pages read and write the
/// values from their own authored rows. They read the saved options through <see cref="Load"/>
/// and leave through <see cref="ToExit"/>, so an Options apply writes through <c>LiveSettings.Persist</c>.
/// </summary>
public sealed class OptionsChoices
{
    private readonly Func<SizeList> _screenSizes;
    private readonly Func<ScreenList> _screens;
    private readonly Func<IReadOnlyList<string>> _modFolders;

    // The folders the row's picker chose, offered for the rest of the launch whatever is staged or saved.
    private readonly List<string> _pickedModFolders = new();

    // The size the options file named at the last Load, which the size row offers as an entry of its
    // own (Sizes). Held apart from the stepped choice, a hand-written size stays in the list after a
    // step lands elsewhere. A step back then reaches it again.
    private string? _savedResolution;

    // The modding folder the options file named at the last Load, offered whatever else is.
    private string? _savedModFolder;

    /// <summary>Choices over <paramref name="screenSizes"/> and <paramref name="screens"/>, read on
    /// every access since a monitor can be plugged in while a screen stands open. Null offers every
    /// candidate size and the one screen a shell with no engine can name. The Modding Folder row
    /// offers <paramref name="modFolders"/> beside the saved folder.</summary>
    public OptionsChoices(Func<SizeList>? screenSizes = null, Func<ScreenList>? screens = null, Func<IReadOnlyList<string>>? modFolders = null)
    {
        _screenSizes = screenSizes ?? (() => ResolutionSetting.Unknown);
        _screens = screens ?? (() => MonitorSetting.Unknown);
        _modFolders = modFolders ?? (() => Array.Empty<string>());
    }

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

    /// <summary>The leading aim assist, null while never set, which reads as off.</summary>
    public bool? LeadingAimAssist { get; set; }

    /// <summary>The lead marker, null while never set, which reads as off.</summary>
    public bool? LeadMarker { get; set; }

    /// <summary>The throttle plate, null while never set, which reads as off.</summary>
    public bool? ThrottlePlate { get; set; }

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

    /// <summary>The modding folder a launch reads (<c>Spec.ModdingFolder</c>), null for none.</summary>
    public string? ModFolder { get; set; }

    /// <summary>The folders the Modding Folder row offers after Off: the saved, offered, picked
    /// (<see cref="PickModFolder"/>) and staged ones, each once.</summary>
    public IReadOnlyList<string> ModFolders
    {
        get
        {
            var folders = new List<string>();
            foreach (string? folder in new[] { _savedModFolder }.Concat(_modFolders()).Concat(_pickedModFolders).Append(ModFolder))
            {
                if (folder != null && !folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    folders.Add(folder);
                }
            }

            return folders;
        }
    }

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

    /// <summary>The leading aim assist as a switch reads it: on only once set on.</summary>
    public bool LeadingAimAssistOn => LeadingAimAssist == true;

    /// <summary>The lead marker as a switch reads it: on only once set on.</summary>
    public bool LeadMarkerOn => LeadMarker == true;

    /// <summary>The throttle plate as a switch reads it: on only once set on.</summary>
    public bool ThrottlePlateOn => ThrottlePlate == true;

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
        LeadingAimAssist = saved?.LeadingAimAssist;
        LeadMarker = saved?.LeadMarker;
        ThrottlePlate = saved?.ThrottlePlate;
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
        ModFolder = _savedModFolder = saved?.ModFolder;
    }

    /// <summary>Stages <paramref name="folder"/>, a folder the row's picker chose, and offers it from
    /// then on: a <see cref="Load"/> drops the staged choice, never the offer.</summary>
    public void PickModFolder(string folder)
    {
        if (!_pickedModFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            _pickedModFolders.Add(folder);
        }

        ModFolder = folder;
    }

    /// <summary>The apply exit carrying every setting, never-set ones as null.</summary>
    public OptionsApplyExit ToExit() =>
        new(Graphics, DifficultyTiers.Word(Difficulty), MonitorIndex, Resolution, DisplayMode, VSync,
            AudioMaster, AudioMusic, AudioEffects, AudioVoice,
            NearestAfterKill, Rumble, DefaultView, AutoHeadTurn, Words.Copy(), VrMode, VrRenderScale, VrVignette,
            LeadingAimAssist, LeadMarker, ModFolder, ThrottlePlate);

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
}

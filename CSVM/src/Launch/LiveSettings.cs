using CSVM.Bindings;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The Options page's settings on the running process, and the live graphics-mode switch. The
/// frame pacing, the window and every word setting resolve at launch, and again on each Options
/// apply, which also writes the options file. A switch over a flying world runs under a
/// <see cref="SwitchCover"/>. The seat's Toggle Graphics Mode action, an apply and
/// <c>--debug-graphics-switch</c> all ask for one here. Module notes: docs/architecture/Launch.md.
/// </summary>
internal sealed class LiveSettings
{
    // The launcher node, which parents a switch's cover and owns the window.
    private readonly Node _host;

    // The process's persistent sun and Environment, which a switch dresses and a word setting reads.
    private readonly DirectionalLight3D _sun;
    private readonly Godot.Environment? _env;

    // The board a switch over a flying world stands, headed by the words it is given.
    private readonly System.Func<string, Control> _switchScreen;

    // A seat's graphics-mode action, acted on at the top of the next frame. The action arrives
    // inside a controller's own tick, which is no place to switch.
    private bool _pendingToggle;

    // The cover over a live graphics switch in flight, null when none is up.
    private SwitchCover? _cover;

    // How many of --debug-graphics-switch's frames have fired, process-scoped like the capture.
    private int _debugSwitchesDone;

    /// <summary>The settings over the launcher <paramref name="host"/>'s window, its persistent
    /// <paramref name="sun"/> and <paramref name="env"/>, and the board a switch stands.</summary>
    public LiveSettings(Node host, DirectionalLight3D sun, Godot.Environment? env, System.Func<string, Control> switchScreen)
    {
        _host = host;
        _sun = sun;
        _env = env;
        _switchScreen = switchScreen;
    }

    /// <summary>The frame pacing, then the window's saved screen, mode and size, at launch. A
    /// scripted run's window takes none of the three window settings.</summary>
    public static void ApplyWindowAtLaunch(SessionSpec spec, Window window)
    {
        // The pacing's three sources and both engine calls are VSyncSetting's; --no-vsync uncaps
        // the loop so frame/fps/script report work done rather than a refresh cap. The config read
        // stays unconditional so it self-registers, see Utils/Config.cs's entry.
        bool vsyncOnByConfig = Config.GetBool(VSyncSetting.Key, VSyncSetting.ConfigDefault);
        VSyncSetting.Apply(VSyncSetting.Resolve(spec.NoVsync, VSyncSetting.SavedWord(spec.Det), vsyncOnByConfig));

        // The saved screen, then the mode, then the size, for a session someone is at only. A
        // scripted run's window is hidden off screen, and its capture is compared against the
        // viewport project.godot pins. None asks for focus; docs/architecture/Utils.md.
        if (!spec.IsScripted)
        {
            MonitorSetting.Apply(MonitorSetting.Resolve(MonitorSetting.SavedWord(spec.Det), MonitorSetting.Screens()));
            var displayMode = DisplayModeSetting.Resolve(DisplayModeSetting.SavedWord(spec.Det));
            DisplayModeSetting.Apply(displayMode);
            ResolutionSetting.Apply(
                ResolutionSetting.Resolve(ResolutionSetting.SavedWord(spec.Det), ResolutionSetting.ScreenSizes(), displayMode.Word),
                window);
        }
    }

    /// <summary>The graphics mode and every word setting at launch, then the render flags on the
    /// window's own <paramref name="viewport"/>. A <c>--det</c> run reads no saved option.</summary>
    public static void ResolveGraphicsAtLaunch(SessionSpec spec, Viewport viewport)
    {
        // ⚠ --det reads no saved option: options.json is one machine's state (docs/cli.md's
        // --graphics bullet).
        var savedOptions = spec.Det ? null : OptionsStore.UserOptions().Load();
        bool graphicsEnhanced = GraphicsMode.Resolve(spec.GraphicsMode, savedOptions?.GraphicsMode);
        // A headset session's profile stands in for the flat words' flags. It resolves before any of
        // them, and a flat launch gets nothing from it.
        var vrQuality = VrQuality.Resolve(XrSession.WillBoot(spec.XrSim), spec.XrQuality,
            spec.WordFlags[AntiAliasingSetting.Lookup], spec.Msaa, spec.WordFlags[ShadowQualitySetting.Lookup], spec.XrFoveation);
        VrQuality.Current = vrQuality;
        // Every word setting under the same --det rule, before any viewport or session builds, since
        // both read the words. They share the mode's line, so a softer or slower run than expected
        // is read off one line.
        WordSettings.ResolveLaunch(Flags(spec), spec.Det);
        ShadowQualitySetting.HardShadows = (spec.SkippedPasses & EnhancedPasses.SoftShadows) != 0;
        string graphicsWord = graphicsEnhanced ? "enhanced" : "original";
        Log.Info("world", $"graphics mode: {GraphicsMode.Key}={graphicsWord} {WordSettings.Describe()}");
        // The window's own viewport takes the render flags here, before any scene builds. The
        // three SubViewports take them at construction.
        ViewportQuality.Apply(viewport);
        // The stereo root's own MSAA and foveation, written only when the plan names them.
        if (VrQuality.MsaaOf(vrQuality.Msaa) is { } msaa)
        {
            viewport.Msaa3D = msaa;
        }

        if (vrQuality.Foveation)
        {
            viewport.VrsMode = Viewport.VrsModeEnum.XR;
        }

        if (VrQuality.Describe(vrQuality) is { } vrLine)
        {
            Log.Info("world", $"{vrLine}");
        }
    }

    /// <summary>A seat's Toggle Graphics Mode action: the switch runs at the top of the next
    /// <see cref="Tick"/> and is saved.</summary>
    public void RequestToggle() => _pendingToggle = true;

    /// <summary>The frame's switch upkeep, run where a switch may start. It takes the owed toggle and
    /// the next <c>--debug-graphics-switch</c> frame. The cover ticks against the frame's wall cost
    /// <paramref name="frameMs"/>, and the Options row takes the network lock.</summary>
    public void Tick(double frameMs, SessionSpec spec, GameSession? session)
    {
        // The graphics-mode action, a frame after the seat that fired it, so no switch runs inside a
        // controller's own _Process. Saved like the Options row.
        if (_pendingToggle)
        {
            _pendingToggle = false;
            if (session is { InSession: true })
                Request(!GraphicsMode.Enhanced, "the graphics-mode action", save: true, spec, session);
        }

        // --debug-graphics-switch: the same flip at each named sim frame, unsaved.
        if (session is { InSession: true } && GameClock.Current is { } simClock && _cover == null
            && _debugSwitchesDone < spec.DebugGraphicsSwitch.Count
            && simClock.Frame >= spec.DebugGraphicsSwitch[_debugSwitchesDone])
        {
            _debugSwitchesDone++;
            Request(!GraphicsMode.Enhanced, $"--debug-graphics-switch at sim frame {simClock.Frame}", save: false, spec, session);
        }

        if (_cover?.Tick(frameMs) == true)
            _cover = null;
        GraphicsMode.SwitchLocked = session is { InSession: true, Wire.Link: not null };
    }

    /// <summary>The options file's writer for an Options apply, shared by the menu's apply and the
    /// pause leaf's. The display settings, the mix, the word settings and the graphics mode apply now.
    /// A headset pacing the frames (<paramref name="headsetPaces"/>) keeps its pacing over V-Sync.
    /// ⚠ The opening view, the difficulty, VR mode and the modding folder are saved and no more, so do not rebuild
    /// anything for them here. The pause leaf puts the head turn and targeting switch on the seats
    /// flying now (PausePreferences.FeedGameOptions).</summary>
    public void Persist(OptionsApplyExit applied, SessionSpec spec, GameSession? session, bool headsetPaces = false)
    {
        var store = OptionsStore.UserOptions();
        var options = store.Load();
        options.GraphicsMode = applied.Graphics;
        options.Difficulty = applied.Difficulty;
        options.NearestAfterKill = applied.NearestAfterKill;
        options.Rumble = applied.Rumble;
        options.DefaultView = applied.DefaultView;
        options.AutoHeadTurn = applied.AutoHeadTurn;
        options.LeadingAimAssist = applied.LeadingAimAssist;
        options.LeadMarker = applied.LeadMarker;
        options.MonitorIndex = applied.MonitorIndex;
        options.Resolution = applied.Resolution;
        options.DisplayMode = applied.DisplayMode;
        options.VSync = applied.VSync;
        foreach (var setting in WordSettings.All)
            options.Words[setting] = applied.Words[setting];
        options.AudioMaster = applied.AudioMaster;
        options.AudioMusic = applied.AudioMusic;
        options.AudioEffects = applied.AudioEffects;
        options.AudioVoice = applied.AudioVoice;
        options.VrMode = applied.VrMode;
        options.VrRenderScale = applied.VrRenderScale;
        options.VrVignette = applied.VrVignette;
        options.ModFolder = applied.ModFolder;
        store.Save(options);
        // The display settings take effect now, through the calls the startup path makes. The order
        // is the window's: the screen it sits on, the mode, the size, then the pacing, which
        // --no-vsync still beats (docs/menu-presentations.md).
        MonitorSetting.Apply(MonitorSetting.Resolve(applied.MonitorIndex, MonitorSetting.Screens()));
        DisplayModeSetting.Apply(DisplayModeSetting.Resolve(applied.DisplayMode));
        ResolutionSetting.Apply(
            ResolutionSetting.Resolve(applied.Resolution, ResolutionSetting.ScreenSizes(), applied.DisplayMode),
            _host.GetWindow());
        VSyncSetting.Apply(headsetPaces
            ? VSyncSetting.Headset
            : VSyncSetting.Resolve(spec.NoVsync, applied.VSync, Config.GetBool(VSyncSetting.Key, VSyncSetting.ConfigDefault)));
        // The mix takes effect now too, through the same call the startup path makes. Apply is
        // idempotent, so an accept from a page that shows no slider rewrites the same three gains.
        AudioMix.Apply(applied.AudioMaster, applied.AudioMusic, applied.AudioEffects, applied.AudioVoice);
        // The haptics toggle takes effect now for the same reason. A pilot turning it off over the
        // pause sheet flies the rest of the sortie with a quiet pad.
        PadRumble.Enabled = !spec.Det && applied.Rumble != false;
        // Each word setting resolved again with the applied word in the saved slot, so its flag still
        // beats it, and a headset's profile too.
        WordSettings.ResolveAll(Flags(spec), applied.Words, spec.Det);
        Log.Info("world", $"word settings applied: {WordSettings.Describe()}");
        // A mode switch puts every word setting on the world itself; otherwise each applies alone.
        bool switching = GraphicsMode.TryParse(applied.Graphics, out bool enhanced) && enhanced != GraphicsMode.Enhanced
            && Request(enhanced, "options", save: false, spec, session);
        if (!switching)
            WordSettings.ApplyAll(new LiveWorld(GodotObject.IsInstanceValid(_sun) ? _sun : null, session));

        // ⚠ The carve is NOT re-armed here. No screen offers it, so the saved key is untouched by an
        // apply and the gate keeps what boot gave it (see the launcher's arming).
        Log.Info("ui", $"options applied: {GraphicsMode.Key}={applied.Graphics} difficulty={applied.Difficulty}");
    }

    /// <summary>Takes the cover over a session being torn down off at once, its hold released.</summary>
    public void DropCover()
    {
        _cover?.Drop();
        _cover = null;
    }

    // The word settings' flags, with the VR profile's anti-aliasing and shadow words in place of an
    // absent flag under a headset (VrQuality.Current). A flat launch gets the spec's own.
    private static WordValues Flags(SessionSpec spec)
    {
        var flags = spec.WordFlags.Copy();
        flags[AntiAliasingSetting.Lookup] = VrQuality.Current.AntiAliasing ?? flags[AntiAliasingSetting.Lookup];
        flags[ShadowQualitySetting.Lookup] = VrQuality.Current.ShadowQuality ?? flags[ShadowQualitySetting.Lookup];
        return flags;
    }

    // G's save, the one field it changes. Never under --det, whose runs must not write the player's
    // options (the same rule the startup read keeps).
    private static void SaveGraphicsMode(SessionSpec spec)
    {
        if (spec.Det)
            return;
        var store = OptionsStore.UserOptions();
        var options = store.Load();
        options.GraphicsMode = GraphicsMode.Enhanced ? GraphicsMode.EnhancedWord : GraphicsMode.Default;
        store.Save(options);
    }

    // A switch over a flying world runs under a SwitchCover: the flight held, a load board over it.
    // With no world up it runs at once. Returns whether a switch runs. ⚠ Refuse it in a network
    // session. Its shared world has no pause to hold it in, and the stall would halt one seat in a
    // live match.
    private bool Request(bool enhanced, string why, bool save, SessionSpec spec, GameSession? session)
    {
        if (session is { InSession: true, Wire.Link: not null })
        {
            Log.Info("world", $"graphics mode: {why} refused, a network session switches no graphics mode");
            return false;
        }
        if (_cover != null)
        {
            Log.Info("world", $"graphics mode: {why} ignored, a switch is already under way");
            return false;
        }
        // The live switch on the process's sun, Environment and session (EnhancedLook.Switch).
        void Run()
        {
            EnhancedLook.Switch(enhanced, _sun, _env, spec.SkippedPasses, session, why);
            if (save)
                SaveGraphicsMode(spec);
        }
        if (session is not { InSession: true } flying)
        {
            Run();
            return true;
        }
        string subject = enhanced ? "SWITCHING TO ENHANCED GRAPHICS" : "SWITCHING TO ORIGINAL GRAPHICS";
        _cover = SwitchCover.Begin(_host, _switchScreen(subject), flying.Pause, GameClock.Current, Run, why);
        return true;
    }
}

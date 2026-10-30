using System;
using System.Collections.Generic;
using CSVM.Spec;

namespace CSVM.Launch;

/// <summary>
/// When the game restarts itself to switch VR mode, and with which arguments. Godot starts OpenXR
/// only with the engine, from <c>--xr-mode on</c> ahead of <c>--</c>, so the saved option takes
/// effect through a new process. A boot with VR mode saved on and no
/// <c>--xr-mode</c> given restarts once, and so does a menu apply that changed the option. The
/// child carries <c>--xr-mode</c> itself, which is what stops a loop. A dev run never restarts
/// (<see cref="FlatOnly"/>). Engine-free, so it unit-tests.
/// </summary>
public static class XrRelaunch
{
    /// <summary>What the menu tells the player when this launch asked for OpenXR and Godot did not
    /// start it. The game then plays flat; the runtime's own reason is in Godot's engine log.</summary>
    public const string FallbackNotice =
        "VR mode is on, but no headset started, so the game plays flat. Start the headset and its "
        + "OpenXR runtime (SteamVR or Virtual Desktop) and launch again, or turn VR mode off in Options.";

    private const string EngineFlag = "--xr-mode";
    private const string UserArgsSeparator = "--";
    private const string SkipIntroFlag = "--skip-intro";

    /// <summary>The <c>--xr-mode</c> given ahead of the user arguments: true for <c>on</c>, false
    /// for <c>off</c>, null when absent or <c>default</c>. The arguments are the process's own,
    /// with or without the program name.</summary>
    public static bool? Mode(IReadOnlyList<string> processArgs)
    {
        ArgumentNullException.ThrowIfNull(processArgs);
        if (XrLaunch.Requested(processArgs, projectEnabled: false))
        {
            return true;
        }

        return XrLaunch.Requested(processArgs, projectEnabled: true) ? null : false;
    }

    /// <summary>Why this launch stays flat whatever is saved, or null when it may restart. The
    /// dev modes, the labs, every scripted and pinned run, and the stand-in are flat-only. No test
    /// or golden therefore reads the player's saved VR mode. PC VR is the Windows build's alone.</summary>
    public static string? FlatOnly(SessionSpec spec, bool headless, bool windows)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return !windows ? "not Windows"
            : headless ? "--headless"
            : spec.RunTests ? "--run-tests"
            : spec.ScriptedBy.Length > 0 ? spec.ScriptedBy
            : spec.Det ? "--det"
            : spec.IsScripted ? "a scripted run"
            : spec.XrSim != null ? "--xr-sim"
            : spec.MovieName != null ? "--movie"
            : spec.NetHostPort != null || spec.NetJoin != null ? "--net-host or --net-join"
            : spec.Viewer ? "--viewer"
            : spec.Freecam ? "--freecam"
            : spec.AnimLab ? "--anim-lab"
            : spec.WeaponLab ? "--weapon-lab"
            : spec.DebugNodeLab != null ? "--debug-nodelab"
            : spec.DebugDamage != null ? "--debug-damage"
            : null;
    }

    /// <summary>Whether a launch restarts into the headset as it boots: VR mode saved on, no
    /// <c>--xr-mode</c> given, and not a flat-only run. An explicit <c>--xr-mode off</c> is a flat
    /// launch asked for by name, and an explicit on already is the headset launch.</summary>
    public static bool AtBoot(bool? saved, bool? mode, string? flatOnly) =>
        saved == true && mode == null && flatOnly == null;

    /// <summary>Whether the menu owes the player <see cref="FallbackNotice"/>: this launch asked
    /// Godot for OpenXR and no OpenXR session is live, whether or not the stand-in draws.</summary>
    public static bool FellBack(bool requested, bool openXrLive) => requested && !openXrLive;

    /// <summary>What the menu tells the player when the game could not start its new process to
    /// switch VR mode <paramref name="on"/> or off. This process carries on as it was.</summary>
    public static string StartFailedNotice(bool on) => on
        ? "VR mode is on, but the game could not start again in the headset, so it plays flat. "
          + "Launch it again, or turn VR mode off in Options."
        : "VR mode is off, but the game could not start again without the headset, so it stays "
          + "in VR until it is launched again.";

    /// <summary>The notice the menu owes the player at its next show, or null. A failed start
    /// toward <paramref name="failedStart"/> wins; otherwise <see cref="FallbackNotice"/> where
    /// <see cref="FellBack"/> says so.</summary>
    public static string? MenuNotice(bool requested, bool openXrLive, bool? failedStart) =>
        failedStart is { } on ? StartFailedNotice(on)
        : FellBack(requested, openXrLive) ? FallbackNotice
        : null;

    /// <summary>The VR mode a menu-side Options apply restarts into, or null for none. It does
    /// when the player changed the option and the new choice differs from what this launch asked
    /// Godot for (<paramref name="requested"/>). An unchanged row, an apply in a flat-only run, and
    /// a choice this process already runs under restart nothing.</summary>
    public static bool? OnApply(bool? before, bool? chosen, bool requested, string? flatOnly)
    {
        bool now = chosen == true;
        if (now == (before == true) || flatOnly != null || now == requested)
        {
            return null;
        }

        return now;
    }

    /// <summary>The child's arguments, <paramref name="processArgs"/> without the program name.
    /// Every argument is carried verbatim, engine and user alike, so a dev launch keeps its
    /// <c>--path</c> and its user flags. Each <c>--xr-mode</c> pair ahead of <c>--</c> is dropped.
    /// For <paramref name="on"/>, <c>--xr-mode on</c> goes first. <paramref name="skipIntro"/>
    /// adds <c>--skip-intro</c> after <c>--</c>, so a restart from the menu replays no intro.</summary>
    public static IReadOnlyList<string> Arguments(IReadOnlyList<string> processArgs, bool on, bool skipIntro)
    {
        ArgumentNullException.ThrowIfNull(processArgs);
        var args = new List<string>(processArgs.Count + 3);
        if (on)
        {
            args.Add(EngineFlag);
            args.Add("on");
        }

        bool user = false;
        bool hasSkip = false;
        for (int i = 0; i < processArgs.Count; i++)
        {
            string arg = processArgs[i];
            if (!user && arg == EngineFlag)
            {
                i++;
                continue;
            }

            if (!user && arg is UserArgsSeparator or "++")
            {
                user = true;
            }
            else if (user && arg == SkipIntroFlag)
            {
                hasSkip = true;
            }

            args.Add(arg);
        }

        if (skipIntro && !hasSkip)
        {
            if (!user)
            {
                args.Add(UserArgsSeparator);
            }

            args.Add(SkipIntroFlag);
        }

        return args;
    }

    /// <summary><paramref name="commandLine"/>, the process's own argument list as .NET reads it,
    /// without its first entry, the program name.</summary>
    public static IReadOnlyList<string> WithoutProgram(IReadOnlyList<string> commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        var args = new string[Math.Max(0, commandLine.Count - 1)];
        for (int i = 1; i < commandLine.Count; i++)
        {
            args[i - 1] = commandLine[i];
        }

        return args;
    }
}

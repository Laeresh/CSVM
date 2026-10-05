using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Launch;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;

namespace CSVM.Testing;

/// <summary>
/// VR mode's menu-side rules on the real screens of both presentations. One suite reads the
/// relaunch's argument source in this process. One is the single seat a headset or its stand-in
/// flies, where the join boards close the entries past the captain's. One is the fallback notice a
/// launch that asked for OpenXR and plays flat shows the player. None starts a process or a
/// runtime; the launcher's decisions are units (<c>CSVM.Tests/XrRelaunchTests.cs</c>).
/// </summary>
internal static class XrModeSuites
{
    // Pad indices no real device holds, so a machine with pads plugged in walks the board the same.
    private const int FirstPad = 96;
    private const int SecondPad = 97;

    [Suite("xr-one-seat",
        "VR mode's one local seat on both presentations' real screens, the limit the launcher sets "
        + "whenever a headset or the --xr-sim stand-in draws: Built-in's join board opens with the "
        + "captain's entry open and the other three closed and saying why, the first pad still takes "
        + "the captain's chair, a second pad is refused a seat, and the strip says VR flies one seat "
        + "where it would otherwise invite a pad; Original's board closes the same three entries "
        + "under its own subtitle and refuses the same pad, and its Dogfight screen names VR as what "
        + "holds a local match")]
    internal static void XrOneSeat(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        BuiltInOneSeat(ctx);
        OriginalOneSeat(ctx);
    }

    [Suite("xr-fallback-notice",
        "The notice a launch that asked for OpenXR and plays flat posts for the menu: Built-in "
        + "shows it on its error line at the first show and Original raises it as the one-button "
        + "warning box over its top level, each taking it once, so a later show is clean")]
    internal static void XrFallbackNotice(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        BuiltInNotice(ctx);
        OriginalNotice(ctx);
    }

    [Suite("xr-relaunch-args",
        "The relaunch's argument source read in this real engine process: the program name .NET puts "
        + "first is the executable Godot reports and is dropped, the user arguments behind the "
        + "separator are exactly what Godot hands the game, no --xr-mode stands, turning VR mode on "
        + "puts --xr-mode on ahead of the separator with every other argument carried verbatim, and "
        + "this --run-tests process is flat-only, so it neither relaunches at boot nor reads the "
        + "player's saved VR mode")]
    internal static void XrRelaunchArgs(TestContext ctx)
    {
        string[] commandLine = System.Environment.GetCommandLineArgs();
        string exe = Godot.OS.GetExecutablePath();
        ctx.Check(commandLine.Length > 0 && string.Equals(System.IO.Path.GetFileName(commandLine[0].Trim('"')),
                System.IO.Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase),
            $"the first entry is the executable Godot reports ({(commandLine.Length > 0 ? commandLine[0] : "none")}, {exe})");
        var args = XrRelaunch.WithoutProgram(commandLine);
        string[] user = Godot.OS.GetCmdlineUserArgs();
        int separator = args.ToList().IndexOf("--");
        ctx.Check(separator >= 0 && args.Skip(separator + 1).SequenceEqual(user),
            $"the user arguments behind the separator are the ones Godot hands the game ({string.Join(" ", args)})");
        ctx.Check(XrRelaunch.Mode(args) == null, $"no --xr-mode stands on a battery run");
        var child = XrRelaunch.Arguments(args, on: true, skipIntro: false);
        int childSeparator = child.ToList().IndexOf("--");
        ctx.Check(child.Count == args.Count + 2 && child[0] == "--xr-mode" && child[1] == "on"
            && childSeparator > 1 && child.Skip(2).SequenceEqual(args),
            $"turning VR mode on puts the engine flag ahead of the separator and carries the rest ({string.Join(" ", child)})");
        string? flatOnly = XrRelaunch.FlatOnly(CSVM.Spec.SessionSpec.Parse(user), Godot.DisplayServer.GetName() == "headless",
            OperatingSystem.IsWindows());
        ctx.Check(flatOnly != null && !XrRelaunch.AtBoot(saved: true, XrRelaunch.Mode(args), flatOnly),
            $"and this run is flat-only whatever is saved ({flatOnly ?? "may relaunch"})");
    }

    private static void BuiltInOneSeat(TestContext ctx)
    {
        var host = MenuSuiteHost.Bare(new List<MenuExit>(), ctx.DataRoot, out var seat);
        var setup = host.Features.Get<PlayerSetupFeature>();
        setup.SeatLimit = 1;
        var menu = MenuSuiteHost.Build(ctx, host, seat, "xr-one-seat");
        ctx.Host.AddChild(menu);
        try
        {
            menu.ShowMenu();
            ctx.Check(menu.ShownJoinHint == $"({PlayerSetupFeature.VrOneSeat})",
                $"Built-in's strip says VR flies one seat instead of inviting a pad ({menu.ShownJoinHint})");
            menu.ShowMenu(LaunchMenu.JoinBoardAid);
            ctx.Check(menu.ShownScreen == "Join", $"the join board stands ({menu.ShownScreen})");
            var manifest = menu.ShownManifest;
            ctx.Check(manifest.Count == 4 && manifest[0].Contains("open seat", StringComparison.Ordinal)
                && manifest.Skip(1).All(e => e.Contains("closed seat", StringComparison.Ordinal)
                    && e.Contains("VR mode flies one seat", StringComparison.Ordinal)),
                $"with the captain's entry open and the other three closed, saying why ({string.Join(" / ", manifest)})");
            ctx.Check(menu.Devices.SignOn(FirstPad) && menu.Devices.P1Pad == FirstPad && setup.Seats.Count == 1,
                $"the first pad still takes the captain's chair, seat 1 ({menu.Devices.P1Pad}, {setup.Seats.Count} seats)");
            ctx.Check(!menu.Devices.SignOn(SecondPad) && setup.Seats.Count == 1,
                $"and a second pad is refused a seat of its own ({setup.Seats.Count} seats)");
            menu.Devices.DropSignOns();
        }
        finally
        {
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
        }
    }

    private static void OriginalOneSeat(TestContext ctx)
    {
        var host = OriginalHost(ctx, "xr-one-seat", out var layoutReason);
        if (host == null)
        {
            ctx.Check(false, $"the install's layout passes the availability check ({layoutReason})");
            return;
        }

        var setup = host.Features.Get<PlayerSetupFeature>();
        setup.SeatLimit = 1;
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original");
            host.Show(MenuReturnDestination.TopLevel);
            if ((host.Active as OriginalPresentation) is not { Shell: { } shell, Devices: { } devices })
            {
                ctx.Check(false, $"Original stands with its shell and its pad roster ({host.Active?.Id})");
                return;
            }

            shell.JoinBoard.Open();
            ctx.Check(Lines(shell, "closed seat") == 3 && Lines(shell, "VR mode flies one seat") == 3
                && Lines(shell, "open seat") == 1 && Lines(shell, "one seat while VR mode is on") == 1,
                $"Original's board closes the three entries past the captain's under its own subtitle ({Lines(shell, "closed seat")} closed)");
            ctx.Check(devices.Closed(1) && devices.Closed(3) && !devices.Closed(0),
                $"which is the roster's own answer, the captain's entry never closed");
            ctx.Check(devices.SignOn(FirstPad) && !devices.SignOn(SecondPad) && setup.Seats.Count == 1,
                $"the captain's pad signs on and the second is refused ({setup.Seats.Count} seats)");
            devices.DropSignOns();

            shell.Open(OriginalScreen.Dogfight);
            setup.Select(setup.Seats[0]);
            ctx.Check(Lines(shell, PlayerSetupFeature.VrOneSeat) == 1,
                $"the Dogfight screen names VR as what holds a local match ({Lines(shell, PlayerSetupFeature.VrOneSeat)} lines)");
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "xr-one-seat");
        }
    }

    private static void BuiltInNotice(TestContext ctx)
    {
        var registry = new PresentationRegistry();
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "xr-fallback-notice");
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new UI.Boards.MenuInput { Keyboard = true })
        {
            Planes = planes,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), _ => { });
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot);
        var notice = new MenuNoticeFeature();
        notice.Post(XrRelaunch.FallbackNotice);
        host.Features.Add(notice);
        host.AddSeat(new MenuIdleSource());
        try
        {
            host.Select(forceBuiltIn: true, cliOverride: null);
            host.Show(MenuReturnDestination.TopLevel);
            var menu = (host.Active as BuiltInPresentation)?.Menu;
            ctx.Check(menu?.ShownDetail == XrRelaunch.FallbackNotice && notice.Pending == null,
                $"Built-in writes the notice on its error line and takes it ({menu?.ShownDetail})");
            host.Exit(new QuitExit());
            host.Show(MenuReturnDestination.TopLevel);
            ctx.Check(menu?.ShownDetail != XrRelaunch.FallbackNotice,
                $"and a later show is clean ({menu?.ShownDetail})");
        }
        finally
        {
            host.Deactivate();
            MenuSuiteHost.DropScratchPlanes(ctx, "xr-fallback-notice");
        }
    }

    private static void OriginalNotice(TestContext ctx)
    {
        var host = OriginalHost(ctx, "xr-fallback-notice", out var layoutReason);
        if (host == null)
        {
            ctx.Check(false, $"the install's layout passes the availability check ({layoutReason})");
            return;
        }

        var notice = new MenuNoticeFeature();
        notice.Post(XrRelaunch.FallbackNotice);
        host.Features.Add(notice);
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original");
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell is { Screen: OriginalScreen.TopLevel } && shell.Dialog?.Message == XrRelaunch.FallbackNotice
                && shell.Dialog.Icon == UI.Campaign.DialogIcon.Warning && shell.Dialog.Answers.Count == 1
                && notice.Pending == null,
                $"Original raises the notice as the one-button warning over its top level ({shell?.Screen}, {shell?.Dialog?.Message})");
            host.Exit(new QuitExit());
            host.Show(MenuReturnDestination.TopLevel);
            ctx.Check(shell?.Dialog?.Message != XrRelaunch.FallbackNotice,
                $"and a later show raises nothing ({shell?.Dialog?.Message ?? "no box"})");
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "xr-fallback-notice");
        }
    }

    // A host carrying Original alone over the install's decoded layout, with every shared feature
    // and an idle seat. Null where the install's layout does not pass its check.
    private static MenuHost? OriginalHost(TestContext ctx, string suite, out string reason)
    {
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        reason = why ?? "ok";
        if (layout == null)
        {
            return null;
        }

        var registry = new PresentationRegistry();
        var planes = MenuSuiteHost.ScratchPlanes(ctx, suite);
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new UI.Boards.MenuInput { Keyboard = true })
        {
            Planes = planes,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), _ => { });
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot);
        host.AddSeat(new MenuIdleSource());
        return host;
    }

    // How many composed lines carry a piece of text. The manifest states a seat in words rather
    // than in a row, so its entries are counted here.
    private static int Lines(OriginalShell shell, string text) =>
        shell.Compose().Lines.Count(line => line.Text.Contains(text, StringComparison.Ordinal));
}

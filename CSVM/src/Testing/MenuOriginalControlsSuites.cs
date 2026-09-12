using System;
using System.Collections.Generic;
using CSVM.Bindings;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// Original's rebinding pages through the presentation boundary over the install's decoded layout:
/// the Preferences page's CONTROLS door, the CONTROLS page's seat row and KEYS AND BUTTONS door,
/// the KEYS page's tabs and control cells, a capture armed on a cell and abandoned with Escape,
/// a second capture that binds, and ACCEPT CHANGES writing the seat's own keymap file.
/// </summary>
internal static class MenuOriginalControlsSuites
{
    private const float Dt = 1f / 60f;

    [Suite("menu-original-controls",
        "Original's rebinding pages through the presentation boundary over the install's decoded "
        + "layout: PREFERENCES opens the Options screen with its CONTROLS door live over the shared "
        + "feature, the door opens the decoded CONTROLS page whose seat row names player 1 and whose "
        + "KEYS AND BUTTONS button opens the decoded KEYS page on its Movement tab with the seven "
        + "category strips, the three column heads and CANCEL CHANGES authored left of ACCEPT "
        + "CHANGES, a click on a control cell arms a capture on that row's own action and slot, "
        + "Escape abandons it with the live keymap untouched, a second capture binds the key it is "
        + "given, ACCEPT CHANGES returns to the CONTROLS page and writes player 1's keymap file "
        + "carrying it, a tab press stands another category, and CANCEL CHANGES on the CONTROLS "
        + "page returns to the Options screen")]
    internal static void MenuOriginalControls(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string dir = System.IO.Path.Combine(ctx.ScratchDir, "menu-original-controls");
        System.IO.Directory.CreateDirectory(dir);
        // ⚠ Before the host: the save below writes through the store, and without the override it
        // would land on the keymap saved at this machine's controls.
        string? previous = BindingStore.DirectoryOverride;
        BindingStore.DirectoryOverride = dir;
        var written = new List<int>();
        var exits = new List<MenuExit>();
        var seat = new ScriptedSeat();
        var player1 = new MenuInput { Keyboard = true };
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, player1));
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, (player, profile) =>
        {
            written.Add(player);
            BindingStore.UserBindings().Save(player, profile);
        });
        host.AddSeat(seat);
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original", savedRequest: null);
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell != null, $"Show activates Original ({host.Active?.Id})");
            if (shell == null)
            {
                return;
            }

            var size = ctx.Host.GetViewport().GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            var controls = host.Features.Get<ControlsFeature>();
            Doors(ctx, host, seat, shell, fit, controls);
            Capture(ctx, host, seat, shell, fit, controls, written);
            Leave(ctx, host, seat, shell, fit, exits);
        }
        finally
        {
            host.Deactivate();
            BindingStore.DirectoryOverride = previous;
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }
    }

    // The three doors: the top level's PREFERENCES, the Options screen's CONTROLS and the CONTROLS
    // page's KEYS AND BUTTONS, each over the shared feature rather than a page-local copy.
    private static void Doors(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, ControlsFeature controls)
    {
        Click(host, seat, shell, fit, "MM_B_PREFERENCES");
        ctx.Check(shell.Screen == OriginalScreen.Options, $"PREFERENCES opens the Options screen ({shell.Screen})");
        var door = Row(shell, OriginalShell.ControlsDoorKey);
        ctx.Check(door is { Enabled: true }, $"its CONTROLS door is live over the shared feature ({door?.Enabled})");

        Click(host, seat, shell, fit, OriginalShell.ControlsDoorKey);
        ctx.Check(shell.Screen == OriginalScreen.ControlsPrefs, $"the door opens the CONTROLS page ({shell.Screen})");
        ctx.Check(controls.Players.Count == 1 && controls.Player == 1,
            $"with one seat registered on it ({controls.Players.Count} seats, player {controls.Player})");
        ctx.Check(Row(shell, OriginalShell.ControlsPlayerKey)?.Label == "Player 1",
            $"and the seat row naming player 1 ({Row(shell, OriginalShell.ControlsPlayerKey)?.Label})");
        ctx.Check(Row(shell, "CP_S_MOUSE") == null, $"the Mouse Sensitivity row is not drawn, this port having no cursor speed");

        Click(host, seat, shell, fit, OriginalShell.KeysDoorKey);
        ctx.Check(shell.Screen == OriginalScreen.Keys, $"KEYS AND BUTTONS opens the KEYS page ({shell.Screen})");
        ctx.Check(shell.KeysTab == 0, $"on its first category ({OriginalShell.ControlTabs[shell.KeysTab].Name})");
        int tabs = 0;
        for (int i = 0; i < OriginalShell.KeysTabCount; i++)
        {
            if (Row(shell, OriginalShell.KeysTabKey(i)) is { Enabled: true })
            {
                tabs++;
            }
        }

        ctx.Check(tabs == OriginalShell.KeysTabCount, $"the seven category strips all stand ({tabs})");
        var cancel = Row(shell, OriginalShell.KeysCancelKey);
        var accept = Row(shell, OriginalShell.KeysAcceptKey);
        ctx.Check(cancel != null && accept != null && cancel.X < accept.X && cancel.Y == accept.Y,
            $"CANCEL CHANGES is authored left of ACCEPT CHANGES on one line ({cancel?.X} vs {accept?.X})");
        var board = shell.Compose();
        int heads = 0;
        foreach (var line in board.Lines)
        {
            if (line.Text is "Action" or "Control A" or "Control B")
            {
                heads++;
            }
        }

        ctx.Check(heads == 3, $"the page draws its three authored column heads ({heads})");
    }

    // A capture armed on one cell: Escape abandons it, a second one binds, and ACCEPT CHANGES is
    // what reaches the keymap file.
    private static void Capture(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit,
        ControlsFeature controls, List<int> written)
    {
        // The seat again, with scripted hardware behind it: the presentation's own registration
        // reads the real keyboard, which a scripted run never presses. The bookkeeping's list is
        // untouched, so its per-frame sync leaves this registration standing.
        var devices = new ScriptedCaptureDevices();
        controls.AddSeat(1, ShippedProfile(), devices, readsKeyboard: true);
        var action = OriginalShell.ControlTabs[0].Rows[0].Action;
        string cell = OriginalShell.KeysCellKey(0, second: false);

        Click(host, seat, shell, fit, cell);
        ctx.Check(controls.Capturing && controls.Focused == action && controls.Slot == 0,
            $"a click on the first Control A cell arms a capture on that row's own action and slot ({controls.Capturing}, {controls.Focused}, slot {controls.Slot})");

        devices.Flight.Keys.Add((int)Godot.Key.Escape);
        Press(host, seat, MenuCommands.None);
        ctx.Check(!controls.Capturing && controls.Status == "Cancelled.",
            $"Escape abandons the capture ({controls.Capturing}, \"{controls.Status}\")");
        ctx.Check(!HoldsKey(controls.Bindings(InputContext.Flight, action), Godot.Key.M),
            $"and nothing was bound to {BindingLabels.Name(action)}");

        devices.Flight.Keys.Clear();
        Click(host, seat, shell, fit, cell);
        devices.Flight.Keys.Add((int)Godot.Key.M);
        Press(host, seat, MenuCommands.None);
        ctx.Check(!controls.Capturing && HoldsKey(controls.Bindings(InputContext.Flight, action), Godot.Key.M),
            $"a second capture binds the key it is given ({controls.Status})");
        ctx.Check(written.Count == 0, $"with nothing written yet ({written.Count} saves)");

        Click(host, seat, shell, fit, OriginalShell.KeysAcceptKey);
        ctx.Check(shell.Screen == OriginalScreen.ControlsPrefs,
            $"ACCEPT CHANGES returns to the CONTROLS page ({shell.Screen})");
        ctx.Check(written.Count == 1 && written[0] == 1,
            $"and writes player 1's keymap and nobody else's ([{string.Join(", ", written)}])");
        var saved = BindingStore.UserBindings().Load(1, MenuControlsSeats.PadOf(InputContext.Flight), readsKeyboard: true);
        ctx.Check(HoldsKey(saved.Map(InputContext.Flight).Bindings(action), Godot.Key.M),
            $"the saved keymap read back carries the rebind on {BindingLabels.Name(action)}");
    }

    // The way out: a tab press stands another category, and the CONTROLS page's CANCEL CHANGES
    // returns to the Options screen without an exit.
    private static void Leave(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, List<MenuExit> exits)
    {
        Click(host, seat, shell, fit, OriginalShell.KeysDoorKey);
        Click(host, seat, shell, fit, OriginalShell.KeysTabKey(2));
        ctx.Check(shell.KeysTab == 2, $"a tab press stands its category ({OriginalShell.ControlTabs[shell.KeysTab].Name})");
        Click(host, seat, shell, fit, OriginalShell.KeysCancelKey);
        ctx.Check(shell.Screen == OriginalScreen.ControlsPrefs, $"CANCEL CHANGES leaves the KEYS page ({shell.Screen})");
        Click(host, seat, shell, fit, OriginalShell.ControlsCancelKey);
        ctx.Check(shell.Screen == OriginalScreen.Options, $"and the CONTROLS page's own returns to Options ({shell.Screen})");
        ctx.Check(exits.Count == 0, $"neither page leaves through an exit ({exits.Count})");
    }

    private static bool HoldsKey(IReadOnlyList<Binding> bindings, Godot.Key key)
    {
        foreach (var binding in bindings)
        {
            if (binding.Control.Kind == ControlKind.Key && binding.Control.Index == (int)key)
            {
                return true;
            }
        }

        return false;
    }

    // One seat's three shipped keymaps, each on the identity its own polling site reads.
    private static BindingProfile ShippedProfile()
    {
        var maps = new Dictionary<InputContext, ActionMap>();
        foreach (var context in Enum.GetValues<InputContext>())
        {
            maps[context] = DefaultBindings.MapFor(context, MenuControlsSeats.PadOf(context));
        }

        return new BindingProfile(maps, readsKeyboard: true);
    }

    private static OriginalRow? Row(OriginalShell shell, string key)
    {
        foreach (var row in shell.Rows)
        {
            if (row.Key == key)
            {
                return row;
            }
        }

        return null;
    }

    // One click on a row by key: the press arms it and the release still on it fires, so a click is
    // two frames rather than one.
    private static void Click(MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, string key)
    {
        if (Row(shell, key) is not { } row)
        {
            return;
        }

        float x = fit.X(row.X + (row.Width / 2f));
        float y = fit.Y(row.Y + (row.Height / 2f));
        Press(host, seat, new MenuCommands { Pointer = new MenuPointer(x, y, true, true) });
        Press(host, seat, new MenuCommands { Pointer = new MenuPointer(x, y, false, false) });
    }

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        seat.Enqueue(frame);
        host.Tick(Dt);
    }

    // A seat whose frames a suite writes, and which reads nothing of its own.
    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }

    // The seat's capture readers with nothing behind them but what this suite puts there, one per
    // context on the identity that context's rows sit on.
    private sealed class ScriptedCaptureDevices : ICaptureDevices
    {
        private readonly Dictionary<InputContext, ScriptedDevices> _states = new();

        public ScriptedCaptureDevices()
        {
            foreach (var context in Enum.GetValues<InputContext>())
            {
                _states[context] = new ScriptedDevices(MenuControlsSeats.PadOf(context));
            }
        }

        public ScriptedDevices Flight => _states[InputContext.Flight];

        public DeviceId PadOf(InputContext context) => MenuControlsSeats.PadOf(context);

        public IDeviceState For(InputContext context) => _states[context];
    }

    // One frame of scripted hardware: the keys and buttons the suite says are held.
    private sealed class ScriptedDevices : IDeviceState
    {
        private readonly DeviceId _pad;

        public ScriptedDevices(DeviceId pad) => _pad = pad;

        public HashSet<int> Keys { get; } = new();

        public HashSet<int> Buttons { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => device == _pad && Buttons.Contains(button);

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) => 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}

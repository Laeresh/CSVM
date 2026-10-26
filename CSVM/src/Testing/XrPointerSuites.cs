using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Bindings;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.Testing;

/// <summary>The hand controllers of the <c>--xr-sim</c> stand-in as a device in the binding seam, and
/// their laser on the theater screen. Both run over the suite host's own root with no stereo viewport.
/// The stand-in's hands are aimed and pressed through its seams (<see cref="XrSimInterface.AimHand"/>,
/// <see cref="XrSimInterface.SetHandInput"/>). Module entries: docs/architecture/UI.md on
/// TheaterPointer and ScreenPointer, docs/architecture/Bindings.md on XrControllers.</summary>
internal static class XrPointerSuites
{
    private const float Dt = 1f / 60f;

    // The rows a pause board with every door wired carries, in its menu's order.
    private const int PhotoRow = 1;

    // Where the seated player holds each hand, in the XR origin's frame: low and forward of the eye.
    private static readonly Vector3 RightHandAt = new(0.18f, -0.32f, -0.12f);
    private static readonly Vector3 LeftHandAt = new(-0.2f, -0.3f, -0.1f);

    // A rotated seat far from the chapter origin, as a flight would stand it.
    private static readonly Transform3D Seat =
        new(Basis.FromEuler(new Vector3(0.12f, -2.3f, 0.05f)), new Vector3(-6350f, 300f, -4144f));

    /// <summary>The shipped XR flight rows resolve through a seat's reader, and read nothing once
    /// the session is gone. A chrome pause board on the theater screen is pointed at and fired
    /// with the right trigger through the polled reader. A Godot button takes the pushed click, and
    /// the left trigger takes the pointer over without one.</summary>
    [Suite("xr-pointer",
        "with the stand-in's hand controllers connected the hand buttons are a device in the binding seam: the left menu button holds Pause, Y holds Cycle Cockpit Views and the left stick click holds Recenter Headset through a seat's reader, and nothing reads once no session is live; the laser from the right hand's aim pose lands on a raised pause board's row with its beam and dot drawn, the polled reader moves the cursor there and a trigger pull fires the row, a Godot button on the screen takes the trigger as a pushed click, a pull on the left trigger hands the pointer to the left hand without clicking and its next pull clicks, with the screen down the other trigger moves nothing, and the desktop mouse is the pointer again once the laser leaves")]
    internal static void XrPointer(TestContext ctx)
    {
        if (XRServer.PrimaryInterface != null)
        {
            ctx.Check(false, $"no XR interface is live before the suite, found {XRServer.PrimaryInterface.GetName()}");
            return;
        }

        var report = new StringBuilder();
        var root = ctx.Host.GetViewport();
        bool wasLive = XrControllers.SessionLive;
        var sim = new XrSimInterface(XrSimSpec.Default);
        XRServer.AddInterface(sim);
        var owned = new List<Node>();
        var pause = new PauseState();
        try
        {
            ctx.Check(sim.Initialize(), $"the stand-in interface initialises");
            sim.ConnectHands();
            XrControllers.SessionLive = true;
            Bindings(ctx, sim);

            var theater = TheaterScreen.Build(root, camera: null, () => Seat, () => true, eyeFeed: false);
            var layer = new CanvasLayer { Name = "xr-pointer-board", Layer = HudLayers.Board };
            var board = PauseBoard.Build(pause, exitsToMenu: true, _ => new MenuInput { Keyboard = true });
            int photos = 0;
            board.PhotoMode = () => photos++;
            board.Preferences = () => { };
            board.Restart = () => { };
            board.Exit = () => { };
            layer.AddChild(board);
            var pointer = TheaterPointer.Build(root, theater, () => Seat, eyeFeed: false);
            owned.AddRange(new Node[] { theater, layer, pointer });
            foreach (var node in owned)
            {
                ctx.Host.AddChild(node);
            }

            pause.TryToggle(0);
            board._Process(0.0);
            PauseBoardSuites.Settle(board);
            Frame(theater, pointer, board);
            ctx.Check(theater.Shown, $"the pause raises the board and the screen");
            ctx.Check(ScreenPointer.Override != null && !pointer.BeamShown && pointer.At == null,
                $"connected hands take the pointer over, and no beam draws before a hand is aimed");

            PauseRow(ctx, sim, theater, pointer, board, report, ref photos);
            GuiClick(ctx, sim, theater, pointer, layer);
            HandOver(ctx, sim, theater, pointer, board, report, ref photos);

            pause.ForceResume();
            Frame(theater, pointer, board);
            ctx.Check(!theater.Shown && !pointer.BeamShown && pointer.At == null,
                $"resuming takes the screen away, and the beam with it");
            var pointing = pointer.Hand;
            Trigger(sim, XrControls.Other(pointing), true);
            Frame(theater, pointer, board);
            Trigger(sim, XrControls.Other(pointing), false);
            Frame(theater, pointer, board);
            ctx.Check(pointer.Hand == pointing,
                $"with no screen up the other trigger is the flight's and leaves the laser on the {pointing} hand ({pointer.Hand})");
        }
        finally
        {
            pause.ForceResume();
            foreach (var node in owned)
            {
                if (node.IsInsideTree())
                {
                    ctx.Host.RemoveChild(node);
                }

                node.QueueFree();
            }

            ctx.Check(ScreenPointer.Override == null, $"the pointer leaving gives the desktop mouse back");
            XrControllers.SessionLive = wasLive;
            XRServer.ClearReferenceFrame();
            sim.Uninitialize();
            XRServer.RemoveInterface(sim);
            ctx.WriteArtifact($"test-xr-pointer.txt", report.ToString());
        }
    }

    /// <summary>The Original top level on the theater screen, driven by the laser alone through the
    /// polled reader the launcher gives seat 0. Its pulls open PREFERENCES, CONTROLS and KEYS AND
    /// BUTTONS and stand the Other tab, where the XR controls list beside the keys and pads.</summary>
    [Suite("xr-pointer-original-menu",
        "on the Original presentation over the install's decoded layout, hosted on the theater screen, the right hand's laser and trigger, read by seat 0's polled pointer as the launcher wires it, open PREFERENCES, its CONTROLS door, KEYS AND BUTTONS and the Other tab, whose Pause and Recenter Headset rows list the left menu button and the left stick click while a stereo session is live and read as flat play's without one; a capture the laser opens takes neither the pull that opened it nor a click, binds the right trigger on a fresh pull and the left trigger without moving the laser, and the left trigger still hands the laser over once no capture listens")]
    internal static void XrPointerOriginalMenu(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        MenuSuiteHost.RequireLayout(ctx);
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        if (XRServer.PrimaryInterface != null)
        {
            ctx.Check(false, $"no XR interface is live before the suite, found {XRServer.PrimaryInterface.GetName()}");
            return;
        }

        string dir = System.IO.Path.Combine(ctx.ScratchDir, "xr-pointer-original-menu");
        System.IO.Directory.CreateDirectory(dir);
        string? previousStore = BindingStore.DirectoryOverride;
        BindingStore.DirectoryOverride = dir;
        bool wasLive = XrControllers.SessionLive;
        var root = ctx.Host.GetViewport();
        var sim = new XrSimInterface(XrSimSpec.Default);
        XRServer.AddInterface(sim);
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "xr-pointer-original-menu");
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }, data: ctx.Data)
        {
            Planes = planes,
        });
        var exits = new List<MenuExit>();
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        // Seat 0's pointer exactly as the launcher builds it: the position and the button off the
        // one pointer source, which the laser overrides.
        host.AddSeat(new PointerSeat(new IdleSeat(),
            () => ScreenPointer.Read(root) is { } p ? (p.At.X, p.At.Y) : null,
            () => ScreenPointer.Read(root) is { } p && p.Pressed));
        var theater = TheaterScreen.Build(root, camera: null, () => Seat, () => false, eyeFeed: false);
        var pointer = TheaterPointer.Build(root, theater, () => Seat, eyeFeed: false);
        try
        {
            ctx.Check(sim.Initialize(), $"the stand-in interface initialises");
            sim.ConnectHands();
            XrControllers.SessionLive = true;
            host.Select("original");
            host.Show(MenuReturnDestination.TopLevel);
            ctx.Host.AddChild(theater);
            ctx.Host.AddChild(pointer);
            MenuFrame(theater, pointer, host);
            ctx.Check(theater.Shown && theater.Hosted.Count > 0,
                $"with no flight live the screen shows the menu's layers ({theater.Hosted.Count})");
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell != null, $"Show activates Original ({host.Active?.Id})");
            if (shell == null)
            {
                return;
            }

            var size = root.GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            Pull(sim, theater, pointer, host, shell, fit, "MM_B_PREFERENCES");
            ctx.Check(shell.Screen == OriginalScreen.Options, $"the laser's pull on PREFERENCES opens the Options screen ({shell.Screen})");
            Pull(sim, theater, pointer, host, shell, fit, OriginalOptionsScreen.ControlsDoorKey);
            ctx.Check(shell.Screen == OriginalScreen.ControlsPrefs, $"and on its CONTROLS door the CONTROLS page ({shell.Screen})");
            Pull(sim, theater, pointer, host, shell, fit, OriginalControlsPage.KeysDoorKey);
            ctx.Check(shell.Screen == OriginalScreen.Keys, $"and on KEYS AND BUTTONS the KEYS page ({shell.Screen})");
            int other = OriginalKeysPage.TabCount - 1;
            Pull(sim, theater, pointer, host, shell, fit, OriginalKeysPage.TabKey(other));
            ctx.Check(shell.Options.Keys.Tab == other, $"and on the Other strip that tab ({shell.Options.Keys.Tab})");

            var pause = Cell(shell, InputAction.Pause);
            var recenter = Cell(shell, InputAction.RecenterHeadset);
            ctx.Check(pause.B.Contains("VR Menu") && recenter.B.Contains("VR L Stick Click"),
                $"under a stereo session Pause lists the left menu button ({pause.A} | {pause.B}) and Recenter Headset the left stick click ({recenter.A} | {recenter.B})");
            XrControllers.SessionLive = false;
            var flatPause = Cell(shell, InputAction.Pause);
            var flatRecenter = Cell(shell, InputAction.RecenterHeadset);
            ctx.Check(!flatPause.B.Contains("VR") && !flatRecenter.B.Contains("VR"),
                $"and without one both rows read as flat play's ({flatPause.A} | {flatPause.B}; {flatRecenter.A} | {flatRecenter.B})");
            XrControllers.SessionLive = true;
            TriggerCapture(ctx, sim, theater, pointer, host, shell, fit, other);
        }
        finally
        {
            host.Deactivate();
            foreach (var node in new Node[] { pointer, theater })
            {
                if (node.IsInsideTree())
                {
                    ctx.Host.RemoveChild(node);
                }

                node.QueueFree();
            }

            ctx.Check(ScreenPointer.Override == null, $"the pointer leaving gives the desktop mouse back");
            XrControllers.SessionLive = wasLive;
            BindingStore.DirectoryOverride = previousStore;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "xr-pointer-original-menu");
            XRServer.ClearReferenceFrame();
            sim.Uninitialize();
            XRServer.RemoveInterface(sim);
        }
    }

    // The shipped XR flight rows through a seat's reader, as FlightController's own reads them, and
    // the same buttons reading nothing once no session is live.
    private static void Bindings(TestContext ctx, XrSimInterface sim)
    {
        var seat = new SeatDeviceState(DefaultBindings.AnyPad, () => null);
        var profile = BindingProfile.Defaults(default, readsKeyboard: true);
        var flight = profile.Actions(InputContext.Flight);
        sim.SetHandInput(XrHand.Left, "menu_button", true);
        sim.SetHandInput(XrHand.Left, "by_button", true);
        sim.SetHandInput(XrHand.Left, "primary_click", true);
        seat.Refresh();
        profile.Poll(seat);
        ctx.Check(flight.Held(InputAction.Pause) && flight.Held(InputAction.CycleCockpitViews) && flight.Held(InputAction.RecenterHeadset),
            $"the left menu button, Y and the left stick click hold Pause, Cycle Cockpit Views and Recenter Headset");
        ctx.Check(!flight.Held(InputAction.FireGuns) && !flight.Held(InputAction.Respawn),
            $"and nothing the XR controls do not hold");

        XrControllers.SessionLive = false;
        profile.Poll(seat);
        ctx.Check(!flight.Held(InputAction.Pause) && !seat.IsButtonDown(DeviceId.Xr, (int)XrButton.LeftMenu),
            $"with no session live the same held buttons read nothing");
        XrControllers.SessionLive = true;
        sim.SetHandInput(XrHand.Left, "menu_button", false);
        sim.SetHandInput(XrHand.Left, "by_button", false);
        sim.SetHandInput(XrHand.Left, "primary_click", false);
        profile.Poll(seat);
        ctx.Check(!flight.Held(InputAction.Pause), $"and letting go releases them");
    }

    // The right hand aimed through Photo Mode's middle: the polled reader moves the cursor there, and
    // a pull and a release fire it.
    private static void PauseRow(
        TestContext ctx, XrSimInterface sim, TheaterScreen theater, TheaterPointer pointer, PauseBoard board,
        StringBuilder report, ref int photos)
    {
        if (board.Rows is not { } rows)
        {
            ctx.Check(false, $"the raised board carries its menu");
            return;
        }

        Vector2 middle = rows.RowRect(PhotoRow).GetCenter();
        Aim(sim, theater, XrHand.Right, RightHandAt, middle);
        Frame(theater, pointer, board);
        Frame(theater, pointer, board);
        report.AppendLine($"photo row middle {middle}, pointer at {pointer.At}, hand {pointer.Hand}");
        ctx.Check(pointer.At is { } at && at.DistanceTo(middle) < 1f,
            $"the right hand's ray lands on the row's middle ({pointer.At} against {middle})");
        ctx.Check(pointer.BeamShown && pointer.DotShown, $"with the beam and the dot drawn");
        ctx.Same(PhotoRow, board.FocusedRow, $"the polled reader moves the board's cursor onto the row");

        Trigger(sim, XrHand.Right, true);
        Frame(theater, pointer, board);
        Trigger(sim, XrHand.Right, false);
        Frame(theater, pointer, board);
        ctx.Same(1, photos, $"and a trigger pull and release fire it");
    }

    // A Godot button on the screen takes the trigger as the click the pointer pushes into the root.
    private static void GuiClick(TestContext ctx, XrSimInterface sim, TheaterScreen theater, TheaterPointer pointer, CanvasLayer layer)
    {
        int clicks = 0;
        var button = new Button { Name = "xr-pointer-button", Position = new Vector2(8f, 8f), Size = new Vector2(64f, 32f) };
        button.Pressed += () => clicks++;
        layer.AddChild(button);
        try
        {
            Aim(sim, theater, XrHand.Right, RightHandAt, new Vector2(40f, 24f));
            pointer.Step();
            Trigger(sim, XrHand.Right, true);
            pointer.Step();
            Trigger(sim, XrHand.Right, false);
            pointer.Step();
            ctx.Same(1, clicks, $"a button on the screen takes the trigger as a pushed click at {pointer.At}");
        }
        finally
        {
            layer.RemoveChild(button);
            button.QueueFree();
        }
    }

    // A pull on the left trigger moves the pointer to the left hand and clicks nothing. The left
    // hand's next pull clicks the row under its own ray.
    private static void HandOver(
        TestContext ctx, XrSimInterface sim, TheaterScreen theater, TheaterPointer pointer, PauseBoard board,
        StringBuilder report, ref int photos)
    {
        if (board.Rows is not { } rows)
        {
            return;
        }

        Vector2 middle = rows.RowRect(PhotoRow).GetCenter();
        Aim(sim, theater, XrHand.Right, RightHandAt, middle);
        Aim(sim, theater, XrHand.Left, LeftHandAt, middle);
        Frame(theater, pointer, board);
        int before = photos;
        Trigger(sim, XrHand.Left, true);
        Frame(theater, pointer, board);
        Trigger(sim, XrHand.Left, false);
        Frame(theater, pointer, board);
        ctx.Check(pointer.Hand == XrHand.Left && photos == before,
            $"a left trigger pull hands the pointer to the left hand ({pointer.Hand}) and clicks nothing ({photos - before})");
        Trigger(sim, XrHand.Left, true);
        Frame(theater, pointer, board);
        Trigger(sim, XrHand.Left, false);
        Frame(theater, pointer, board);
        ctx.Same(before + 1, photos, $"and the left hand's next pull clicks the row under its ray");
        report.AppendLine($"hand over: {pointer.Hand}, pointer at {pointer.At}, photos {photos}");
    }

    // A capture opened by the laser on the Pause row's cell, the seat reading the hands alone. The
    // pull that opened it binds nothing. The next right pull binds the right trigger and clicks
    // nothing. A left pull binds the left trigger and leaves the laser where it is. Outside a capture
    // the left trigger still hands the laser over.
    private static void TriggerCapture(
        TestContext ctx, XrSimInterface sim, TheaterScreen theater, TheaterPointer pointer, MenuHost host,
        OriginalShell shell, BoardFit fit, int tab)
    {
        var controls = host.Features.Get<ControlsFeature>();
        controls.AddSeat(1, BindingProfile.Defaults(DefaultBindings.AnyPad, readsKeyboard: true), new HandCaptureDevices(), readsKeyboard: true);
        int row = 0;
        var rows = OriginalKeysPage.ControlTabs[tab].Rows;
        while (row < rows.Count - 1 && rows[row].Action != InputAction.Pause)
        {
            row++;
        }

        string cell = OriginalKeysPage.CellKey(row, second: false);
        var right = new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.RightTrigger, 1, ControlCapture.CapturedDeadzone));
        var left = new Binding(DeviceId.Xr, BindingControl.Axis((int)XrAxis.LeftTrigger, 1, ControlCapture.CapturedDeadzone));
        Pull(sim, theater, pointer, host, shell, fit, cell);
        ctx.Check(controls.Capturing && controls.Focused == InputAction.Pause,
            $"the laser's pull on the Pause row's cell opens a capture ({controls.Capturing}, {controls.Focused})");
        ctx.Check(!controls.Bindings(InputContext.Flight, InputAction.Pause).Contains(right),
            $"and the pull that opened it binds nothing ({controls.Status})");

        Trigger(sim, XrHand.Right, true);
        MenuFrame(theater, pointer, host);
        MenuFrame(theater, pointer, host);
        Trigger(sim, XrHand.Right, false);
        MenuFrame(theater, pointer, host);
        MenuFrame(theater, pointer, host);
        ctx.Check(controls.Bindings(InputContext.Flight, InputAction.Pause).Contains(right) && controls.Status.Contains("VR R Trigger"),
            $"a fresh right pull binds the right trigger ({controls.Status})");
        ctx.Check(!controls.Capturing, $"and clicks nothing under the dot, which would open a capture again");

        Pull(sim, theater, pointer, host, shell, fit, cell);
        ctx.Check(controls.Capturing, $"the laser opens a second capture on the cell");
        Aim(sim, theater, XrHand.Left, LeftHandAt, pointer.At ?? Vector2.Zero);
        Trigger(sim, XrHand.Left, true);
        MenuFrame(theater, pointer, host);
        Trigger(sim, XrHand.Left, false);
        MenuFrame(theater, pointer, host);
        MenuFrame(theater, pointer, host);
        ctx.Check(controls.Bindings(InputContext.Flight, InputAction.Pause).Contains(left) && !controls.Capturing,
            $"a left pull during it binds the left trigger and clicks nothing ({controls.Status})");
        ctx.Check(pointer.Hand == XrHand.Right, $"and leaves the laser on the right hand ({pointer.Hand})");

        Trigger(sim, XrHand.Left, true);
        MenuFrame(theater, pointer, host);
        Trigger(sim, XrHand.Left, false);
        MenuFrame(theater, pointer, host);
        ctx.Check(pointer.Hand == XrHand.Left && !controls.Capturing,
            $"with no capture listening the bound left trigger still hands the laser over, clicking nothing ({pointer.Hand})");
    }

    // One click on an Original row through the laser: aimed at its middle, a frame to rest there,
    // then a pull and a release. Each is a frame of the theater, the pointer and the menu host.
    private static void Pull(
        XrSimInterface sim, TheaterScreen theater, TheaterPointer pointer, MenuHost host, OriginalShell shell, BoardFit fit, string key)
    {
        OriginalRow? found = null;
        foreach (var row in shell.Rows)
        {
            if (row.Key == key)
            {
                found = row;
            }
        }

        if (found is not { } target)
        {
            return;
        }

        var at = new Vector2(fit.X(target.X + (target.Width / 2f)), fit.Y(target.Y + (target.Height / 2f)));
        Aim(sim, theater, XrHand.Right, RightHandAt, at);
        MenuFrame(theater, pointer, host);
        Trigger(sim, XrHand.Right, true);
        MenuFrame(theater, pointer, host);
        Trigger(sim, XrHand.Right, false);
        MenuFrame(theater, pointer, host);
    }

    private static (string A, string B) Cell(OriginalShell shell, InputAction action)
    {
        var tab = OriginalKeysPage.ControlTabs[shell.Options.Keys.Tab];
        for (int i = 0; i < tab.Rows.Count; i++)
        {
            if (tab.Rows[i].Action == action)
            {
                var text = shell.Options.Keys.CellText(i);
                return (text.A, text.B);
            }
        }

        return (string.Empty, string.Empty);
    }

    // Points one hand from where it is held at a point of the root's layout. The aim pose goes to the
    // stand-in in tracking space, so the reference frame brings it back to the origin's.
    private static void Aim(XrSimInterface sim, TheaterScreen theater, XrHand hand, Vector3 from, Vector2 layoutPoint)
    {
        var target = theater.Layout.PointAt(layoutPoint / theater.LayoutSize, theater.LayoutSize);
        var aim = new Transform3D(Basis.LookingAt(target - from, Vector3.Up), from);
        sim.AimHand(hand, XRServer.GetReferenceFrame().AffineInverse() * aim);
    }

    private static void Trigger(XrSimInterface sim, XrHand hand, bool pulled)
    {
        sim.SetHandInput(hand, XrControls.TriggerClick, pulled);
        sim.SetHandInput(hand, XrControls.Trigger, pulled ? 1f : 0f);
    }

    // One frame in the order the tree runs it under a headset, the board reading what the pointer
    // left for it.
    private static void Frame(TheaterScreen theater, TheaterPointer pointer, PauseBoard board)
    {
        theater.Refresh();
        pointer.Step();
        board._Process(Dt);
    }

    private static void MenuFrame(TheaterScreen theater, TheaterPointer pointer, MenuHost host)
    {
        theater.Refresh();
        pointer.Step();
        host.Tick(Dt);
    }

    // A seat's capture readers that hear the stand-in's hands and nothing else.
    private sealed class HandCaptureDevices : ICaptureDevices
    {
        public DeviceId PadOf(InputContext context) => MenuControlsSeats.PadOf(context);

        public IDeviceState For(InputContext context) => XrControllers.Live;
    }

    // Seat 0's pad and keyboard half, reading nothing, so the laser is the only input.
    private sealed class IdleSeat : IMenuInputSource
    {
        public string DeviceLabel => "idle";

        public bool CapturingText { get; set; }

        public MenuCommands Poll(float dt) => MenuCommands.None;

        public void Prime()
        {
        }
    }
}

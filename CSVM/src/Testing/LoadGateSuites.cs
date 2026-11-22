using System;
using System.Linq;
using CSVM.Bindings;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.UI.Boards;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.Testing;

/// <summary>The load screen's gate (<see cref="Utils.LoadGate"/>). While the board stands no game
/// sound plays, and no input reaches the flight, the pause board or the menus. A control held
/// through the board's drop does not act on the first frame after it.</summary>
internal static class LoadGateSuites
{
    // A resting pad axis's drift: under PressTravel, so it is posture rather than a held control.
    private const float Drift = 0.01f;

    [Suite("load-gate",
        "while a load screen stands the master bus is muted, the root window takes no input event, "
        + "pad Start opens no pause board and a full right trigger reads no throttle; once it is "
        + "down the bus and the root's input are as they were, the Start and the trigger held "
        + "through its drop still act on nothing, a drifting trigger reads live at once, and the "
        + "next press pauses and the next pull reads full throttle; the leave question's keys read "
        + "off the keyboard answer it, and the Enter and Backspace held through a Yes raise nothing "
        + "on the menu, the Space held through a No fires no gun, until each is pressed again; a "
        + "second load screen the launcher stands replaces the first, so taking it down opens the gate")]
    internal static void LoadGate(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        var pause = new PauseState();
        var pilot = new FlightController
        {
            PlayerIndex = 0,
            IsHumanPiloted = true,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = true,
            PauseState = pause,
            Name = "LoadGatePilot",
        };
        var throttle = FlightReader();
        var drift = FlightReader();
        var root = ctx.Host.GetTree().Root;
        try
        {
            ctx.Check(Frame(pilot, start: true) && pause.Paused,
                $"ABLE-TO-FAIL CONTROL: with no load screen up, pad Start opens the pause board");
            pause.ForceResume();
            Frame(pilot, start: false);
            ctx.Check(Throttle(throttle, 1f) == 1f,
                $"ABLE-TO-FAIL CONTROL: …and a full right trigger reads full throttle up");
            Throttle(throttle, 0f);

            bool mutedBefore = AudioServer.IsBusMute(LaunchAudio.MasterBus);
            var board = LoadBoard.Build(
                ctx.DataRoot, ctx.Data.Rimage, ctx.Zrdr, ctx.Data.Messages, false, "FREE FLIGHT", null);
            ctx.Host.AddChild(board);
            try
            {
                bool muted = AudioServer.IsBusMute(LaunchAudio.MasterBus);
                ctx.Check(Utils.LoadGate.Closed && muted && root.GuiDisableInput,
                    $"the load screen closes the gate, mutes the master bus and stops the root window's events (closed {Utils.LoadGate.Closed}, muted {muted}, events off {root.GuiDisableInput})");
                Frame(pilot, start: true);
                Frame(pilot, start: true);
                ctx.Check(!pause.Halted, $"pad Start held under the load screen opens no pause board");
                ctx.Check(Throttle(throttle, 1f) == 0f, $"…and a full right trigger under it reads no throttle");
                ctx.Check(Throttle(drift, Drift) == 0f, $"…nor does a drifting one");
            }
            finally
            {
                ctx.Host.RemoveChild(board);
                board.QueueFree();
            }

            ctx.Check(!Utils.LoadGate.Closed && AudioServer.IsBusMute(LaunchAudio.MasterBus) == mutedBefore && !root.GuiDisableInput,
                $"the screen's drop opens the gate, gives the bus back as it was ({mutedBefore}) and lets events through");
            ctx.Check(!Frame(pilot, start: true) && !pause.Halted,
                $"the Start held through the drop opens nothing on the first frame after it");
            ctx.Check(Throttle(throttle, 1f) == 0f,
                $"…and the trigger held through it moves no throttle");
            float drifted = Throttle(drift, Drift);
            ctx.Check(drifted == Drift && Throttle(drift, 1f) == 1f,
                $"a trigger drifting at rest through the drop reads live at once ({drifted}), and its pull straight after reads full");
            Frame(pilot, start: false);
            ctx.Check(Frame(pilot, start: true) && pause.Paused,
                $"the next press of Start opens the pause board");
            Throttle(throttle, 0f);
            ctx.Check(Throttle(throttle, 1f) == 1f, $"…and the next pull reads full throttle up");
            if (mutedBefore)
            {
                ctx.Note($"the master bus was already muted by focus loss, so the mute checks could not fail on this run");
            }
        }
        finally
        {
            pause.ForceResume();
            pilot.Free();
        }

        HeldThroughLeaveQuestion(ctx);
        StackedLoadScreens(ctx);
    }

    // The keys the leave question reads off the keyboard, pressed through Godot's Input as a key
    // would be. Each answer is given with a key still held through the board's drop. A Yes lands on a
    // menu, whose seat reads Enter mapped and Backspace and Delete directly. A No goes on to fly.
    private static void HeldThroughLeaveQuestion(TestContext ctx)
    {
        var menu = new MenuInput(() => null, () => null) { Keyboard = true };
        menu.Prime();
        var flight = new PlayerActions(BindingProfile.Defaults(default, true).Map(InputContext.Flight), true);
        var keyboard = new GodotDeviceState(new DeviceRegistry());
        LoadBoard? board = null;
        try
        {
            board = LeaveBoard(ctx);
            menu.Poll(0f);
            Key(Godot.Key.Escape, true, board);
            ctx.Check(board.Asking != null, $"Esc pressed on a held board raises its leave question");
            Key(Godot.Key.Escape, false, board);
            Key(Godot.Key.Backspace, true, board);
            Key(Godot.Key.Enter, true, board);
            menu.Poll(0f);
            ctx.Check(board.LeaveConfirmed && board.Asking == null, $"…and Enter on its Yes answers it, the box down");
            Drop(ctx, ref board);

            menu.Poll(0f);
            ctx.Check(!menu.Accept && !menu.Erase && !menu.Unbind && Input.IsKeyPressed(Godot.Key.Backspace),
                $"the Enter and Backspace held through the drop raise no Accept, Erase or Unbind on the menu's first frame after it (accept {menu.Accept}, erase {menu.Erase}, unbind {menu.Unbind})");
            Key(Godot.Key.Enter, false);
            Key(Godot.Key.Backspace, false);
            menu.Poll(0f);
            Key(Godot.Key.Backspace, true);
            menu.Poll(0f);
            ctx.Check(menu.Erase && menu.Unbind, $"…and the next press of Backspace erases and unbinds");
            Key(Godot.Key.Backspace, false);
            Key(Godot.Key.Enter, true);
            menu.Poll(0f);
            ctx.Check(menu.Accept, $"…and the next press of Enter accepts");
            Key(Godot.Key.Enter, false);

            board = LeaveBoard(ctx);
            flight.Poll(keyboard);
            Key(Godot.Key.Escape, true, board);
            Key(Godot.Key.Escape, false, board);
            Key(Godot.Key.Right, true, board);
            Key(Godot.Key.Right, false, board);
            Key(Godot.Key.Space, true, board);
            flight.Poll(keyboard);
            ctx.Check(board.Asking == null && !board.LeaveConfirmed, $"Space on the question's No takes it down, the hold going on");
            Drop(ctx, ref board);

            flight.Poll(keyboard);
            ctx.Check(!flight.Held(InputAction.FireGuns) && Input.IsKeyPressed(Godot.Key.Space),
                $"the Space held through the drop fires no gun on the first flying frame after it");
            Key(Godot.Key.Space, false);
            flight.Poll(keyboard);
            Key(Godot.Key.Space, true);
            flight.Poll(keyboard);
            ctx.Check(flight.Held(InputAction.FireGuns), $"…and the next press of Space fires");
        }
        finally
        {
            Drop(ctx, ref board);
            foreach (var key in new[] { Godot.Key.Escape, Godot.Key.Enter, Godot.Key.Backspace, Godot.Key.Right, Godot.Key.Space })
            {
                Key(key, false);
            }
        }
    }

    // A board held for the other machines on the suite host, its leave question named. Its keys are
    // read once, so the next press is an edge.
    private static LoadBoard LeaveBoard(TestContext ctx)
    {
        var board = LoadBoard.Build(
            ctx.DataRoot, ctx.Data.Rimage, ctx.Zrdr, ctx.Data.Messages, false, "FREE FLIGHT", null);
        board.LeaveQuestion = "LEAVE?";
        ctx.Host.AddChild(board);
        board._Process(0d);
        return board;
    }

    private static void Drop(TestContext ctx, ref LoadBoard? board)
    {
        if (board == null)
        {
            return;
        }

        ctx.Host.RemoveChild(board);
        board.QueueFree();
        board = null;
    }

    // One key event through Godot's Input, then one frame of the board's own key read.
    private static void Key(Godot.Key key, bool pressed, LoadBoard? board = null)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
        Input.FlushBufferedEvents();
        board?._Process(0d);
    }

    // The launcher's own board, stood twice as a menu aid over a launch would. The second replaces
    // the first, so one drop opens the gate rather than leaving an unreferenced board holding it.
    private static void StackedLoadScreens(TestContext ctx)
    {
        if (ctx.Host.GetParent() is not Launcher launcher)
        {
            ctx.Check(false, $"the suite host hangs under the launcher, whose load screen this drives");
            return;
        }

        try
        {
            launcher.ShowLoadScreenForTest();
            launcher.ShowLoadScreenForTest();
            ctx.Check(Utils.LoadGate.Closed, $"the launcher's load screen, stood twice, closes the gate");
            launcher.HideLoadScreenForTest();
            ctx.Check(!Utils.LoadGate.Closed, $"…and one drop opens it, so the second board replaced the first");
        }
        finally
        {
            // A board the replace rule left standing would hold the gate closed and the root's input
            // off for every later suite in this shard.
            launcher.HideLoadScreenForTest();
            foreach (var layer in launcher.GetChildren())
            {
                if (layer is CanvasLayer && layer.GetChildren().Any(child => child is LoadBoard))
                {
                    launcher.RemoveChild(layer);
                    layer.QueueFree();
                }
            }
        }
    }

    // A flight reader over the shipped keymap's pad half, the shape a seat's pad reader takes.
    private static PlayerActions FlightReader() =>
        new(BindingProfile.Defaults(default, true).Map(InputContext.Flight), false);

    // One rendered frame of the pilot's pause key with pad Start up or down. Answers the halt.
    private static bool Frame(FlightController pilot, bool start)
    {
        var pad = new OnePad(start ? JoyButton.Start : null, 0f);
        pilot.ObserveDeviceForTest(new OnePad(null, 0f), pad);
        return pilot.PollPauseForTest(null);
    }

    // One poll of the reader with the right trigger at that pull. Answers its throttle-up value.
    private static float Throttle(PlayerActions actions, float pull)
    {
        actions.Poll(new OnePad(null, pull));
        return actions.Value(InputAction.ThrottleUp);
    }

    // One tick of the seat's pad placeholder: at most one button down and the right trigger's pull.
    private sealed class OnePad(JoyButton? down, float trigger) : IDeviceState
    {
        public bool IsKeyDown(DeviceId device, int keyCode) => false;

        public bool IsButtonDown(DeviceId device, int button) =>
            device == DefaultBindings.AnyPad && down is { } held && button == (int)held;

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) =>
            device == DefaultBindings.AnyPad && axis == (int)JoyAxis.TriggerRight ? trigger : 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}

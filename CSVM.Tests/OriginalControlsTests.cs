using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Original's two rebinding pages over the hand-authored layout fixture: which actions each of the
/// seven category tabs lists, how a row's two control cells map onto the shared feature's slots,
/// what the cells print when an action holds more controls than the two authored columns, the exit
/// pair's authored order, and the capture a cell arms with its Escape cancel and its accept.
/// </summary>
public class OriginalControlsTests
{
    private static readonly MenuCommands None = MenuCommands.None;

    [Fact]
    public void EveryActionBelongsToExactlyOneCategoryTab()
    {
        var seen = new Dictionary<InputAction, int>();
        foreach (var tab in OriginalShell.ControlTabs)
        {
            foreach (var row in tab.Rows)
            {
                seen[row.Action] = seen.TryGetValue(row.Action, out int count) ? count + 1 : 1;
            }
        }

        foreach (var action in Enum.GetValues<InputAction>())
        {
            Assert.True(seen.TryGetValue(action, out int count) && count == 1,
                $"{action} is listed {(seen.TryGetValue(action, out int n) ? n : 0)} times across the seven tabs");
        }
    }

    [Fact]
    public void TheSixNamedTabsAreFlightActionsAndOtherHoldsWhatTheOriginalNeverBound()
    {
        var tabs = OriginalShell.ControlTabs;
        Assert.Equal(7, tabs.Count);
        Assert.Equal(
            new[] { "Movement", "Throttle", "Weapons", "Targeting", "Views 1", "Views 2", "Other" },
            tabs.Select(t => t.Name).ToArray());
        for (int i = 0; i < 6; i++)
        {
            Assert.All(tabs[i].Rows, row => Assert.Equal(InputContext.Flight, row.Context));
        }

        var other = tabs[6].Rows;
        Assert.Contains(other, row => row.Context == InputContext.Menu);
        Assert.Contains(other, row => row.Context == InputContext.Camera);
        // Every menu and free-camera action, which the original binds on no page of its own.
        foreach (var context in new[] { InputContext.Menu, InputContext.Camera })
        {
            foreach (var action in DefaultBindings.ActionsIn(context))
            {
                Assert.Contains(other, row => row.Context == context && row.Action == action);
            }
        }
    }

    /// <summary>The Movement tab is the six attitude half-axes in the original page's own order,
    /// which begins with Point Nose Down rather than with the enum's first member
    /// (<c>OriginalScreenshots/Keybinds Movement.png</c>).</summary>
    [Fact]
    public void TheMovementTabIsTheSixAttitudeHalfAxesInTheOriginalsOrder()
    {
        Assert.Equal(
            new[]
            {
                InputAction.PitchDown, InputAction.PitchUp, InputAction.RollLeft, InputAction.RollRight,
                InputAction.YawLeft, InputAction.YawRight,
            },
            OriginalShell.ControlTabs[0].Rows.Select(r => r.Action).ToArray());
    }

    /// <summary>The Throttle tab is the two lever keys and then the nine absolute eighths, which are
    /// the digit row the original reserves for them.</summary>
    [Fact]
    public void TheThrottleTabCarriesTheNineEighthsBelowTheLeverPair()
    {
        var rows = OriginalShell.ControlTabs[1].Rows.Select(r => r.Action).ToArray();

        Assert.Equal(InputAction.ThrottleUp, rows[0]);
        Assert.Equal(InputAction.ThrottleDown, rows[1]);
        for (int eighths = 0; eighths <= 8; eighths++)
        {
            Assert.Equal(InputAction.ThrottleSet0 + eighths, rows[2 + eighths]);
        }

        Assert.Equal(11, rows.Length);
    }

    /// <summary>The Targeting tab lists all eleven of the original's targeting actions in the
    /// original page's own order, Next then Previous then Nearest per class and the two class-less
    /// ones last (<c>OriginalScreenshots/Keybinds Targeting.png</c>). Pinned because the order is
    /// read off that page rather than off the enum, which appends new members at its end.</summary>
    [Fact]
    public void TheTargetingTabIsTheOriginalsElevenInItsOwnOrder()
    {
        Assert.Equal(
            new[]
            {
                InputAction.TargetNextEnemy, InputAction.TargetPreviousEnemy, InputAction.TargetNearestEnemy,
                InputAction.TargetNextAlly, InputAction.TargetPreviousAlly, InputAction.TargetNearestAlly,
                InputAction.TargetNextNonAircraft, InputAction.TargetPreviousNonAircraft,
                InputAction.TargetNearestNonAircraft, InputAction.TargetNearest, InputAction.TargetClear,
            },
            OriginalShell.ControlTabs[3].Rows.Select(r => r.Action).ToArray());
    }

    [Fact]
    public void TheControlsDoorIsLiveOnlyWhereTheSharedFeatureStandsBehindIt()
    {
        var without = Shell(controls: null, out _, out _);
        without.Open(OriginalScreen.Options);
        Assert.True(Row(without, OriginalShell.ControlsDoorKey) is { Enabled: false });

        var with = Shell(out _, out _);
        with.Open(OriginalScreen.Options);
        Assert.True(Row(with, OriginalShell.ControlsDoorKey) is { Enabled: true });
        Click(with, Row(with, OriginalShell.ControlsDoorKey)!);
        Assert.Equal(OriginalScreen.ControlsPrefs, with.Screen);
    }

    [Fact]
    public void TheKeysPageAuthorsCancelLeftOfAccept()
    {
        var shell = Shell(out _, out _);
        shell.OpenKeys();
        var cancel = Row(shell, OriginalShell.KeysCancelKey);
        var accept = Row(shell, OriginalShell.KeysAcceptKey);
        Assert.NotNull(cancel);
        Assert.NotNull(accept);
        Assert.True(cancel!.X < accept!.X, $"CANCEL CHANGES stands left of ACCEPT CHANGES ({cancel.X} vs {accept.X})");
        Assert.Equal(cancel.Y, accept.Y);
    }

    [Fact]
    public void ACellNamesTheActionItsRowStandsOnAndArmsACaptureOnItsOwnSlot()
    {
        var shell = Shell(out var controls, out _);
        shell.OpenKeys();
        var tab = OriginalShell.ControlTabs[0];
        int row = tab.Rows.Count - 1;
        Click(shell, Row(shell, OriginalShell.KeysCellKey(row, second: false))!);
        Assert.Equal(tab.Rows[row].Context, controls.Context);
        Assert.Equal(tab.Rows[row].Action, controls.Focused);
        Assert.Equal(0, controls.Slot);
        Assert.True(controls.Capturing);

        controls.CancelCapture();
        Click(shell, Row(shell, OriginalShell.KeysCellKey(row, second: true))!);
        Assert.Equal(tab.Rows[row].Action, controls.Focused);
        Assert.Equal(Math.Min(1, controls.FocusedBindings.Count), controls.Slot);
    }

    [Fact]
    public void ARowWithMoreControlsThanColumnsSaysHowManyItIsNotShowing()
    {
        var shell = Shell(out var controls, out _);
        shell.OpenKeys();
        var action = OriginalShell.ControlTabs[0].Rows[0].Action;
        controls.Context = InputContext.Flight;
        controls.Focus(IndexOf(controls, action));
        while (controls.FocusedBindings.Count > 0)
        {
            controls.MoveSlot(0);
            controls.UnbindSlot();
        }

        foreach (var key in new[] { Godot.Key.M, Godot.Key.N, Godot.Key.B })
        {
            controls.MoveSlot(9);
            controls.Offer(new Binding(DeviceId.Keyboard, BindingControl.Key((int)key)));
            // A key the shipped set already holds asks first; this row wants all three either way.
            controls.ConfirmSteal();
        }

        var text = shell.KeysCellText(0);
        Assert.Equal("M", text.A);
        Assert.Equal("N, +1 more", text.B);
    }

    [Fact]
    public void AnArmedCellTakesEscapeAsTheAbandonAndABoundKeyAsTheAnswer()
    {
        var shell = Shell(out var controls, out var devices, out var written);
        shell.OpenKeys();
        var action = OriginalShell.ControlTabs[0].Rows[0].Action;
        Click(shell, Row(shell, OriginalShell.KeysCellKey(0, second: false))!);
        Assert.True(controls.Capturing);

        devices.Flight.Keys.Add((int)Godot.Key.Escape);
        shell.Step(None);
        Assert.False(controls.Capturing);
        Assert.Equal("Cancelled.", controls.Status);
        Assert.DoesNotContain(controls.Bindings(InputContext.Flight, action),
            b => b.Control.Kind == ControlKind.Key && b.Control.Index == (int)Godot.Key.M);

        devices.Flight.Keys.Clear();
        Click(shell, Row(shell, OriginalShell.KeysCellKey(0, second: false))!);
        devices.Flight.Keys.Add((int)Godot.Key.M);
        shell.Step(None);
        Assert.False(controls.Capturing);
        Assert.Contains(controls.Bindings(InputContext.Flight, action),
            b => b.Control.Kind == ControlKind.Key && b.Control.Index == (int)Godot.Key.M);
        Assert.Empty(written);

        Click(shell, Row(shell, OriginalShell.KeysAcceptKey)!);
        Assert.Equal(OriginalScreen.ControlsPrefs, shell.Screen);
        Assert.Equal(new[] { 1 }, written.ToArray());
    }

    [Fact]
    public void CancelChangesDropsTheWholeVisitAndLeavesTheLiveKeymapAlone()
    {
        var shell = Shell(out var controls, out var devices, out var written);
        shell.OpenKeys();
        var action = OriginalShell.ControlTabs[0].Rows[0].Action;
        Click(shell, Row(shell, OriginalShell.KeysCellKey(0, second: false))!);
        devices.Flight.Keys.Add((int)Godot.Key.M);
        shell.Step(None);
        Assert.True(controls.Dirty);

        Click(shell, Row(shell, OriginalShell.KeysCancelKey)!);
        Assert.Equal(OriginalScreen.ControlsPrefs, shell.Screen);
        Assert.False(controls.Dirty);
        Assert.DoesNotContain(controls.Bindings(InputContext.Flight, action),
            b => b.Control.Kind == ControlKind.Key && b.Control.Index == (int)Godot.Key.M);
        Assert.Empty(written);
    }

    [Fact]
    public void ATabPressStandsItsCategoryAndTheListWindowHoldsTheCursor()
    {
        var shell = Shell(out _, out _);
        shell.OpenKeys();
        Assert.Equal(0, shell.KeysTab);
        int last = OriginalShell.ControlTabs.Count - 1;
        Click(shell, Row(shell, OriginalShell.KeysTabKey(last))!);
        Assert.Equal(last, shell.KeysTab);
        Assert.Equal(0, shell.KeysTop);

        // The Other tab outruns the list's window, so a cell past its foot is off screen until the
        // cursor walks onto it and the window follows.
        int rows = OriginalShell.ControlTabs[last].Rows.Count;
        Assert.True(rows > 11, $"the Other tab outruns the authored window ({rows} rows)");
        Assert.False(Row(shell, OriginalShell.KeysCellKey(rows - 1, second: false))!.Visible);
        shell.Step(new MenuCommands { MoveX = 1 });
        for (int i = 0; i < rows + 4 && shell.FocusedKey != OriginalShell.KeysCellKey(rows - 1, second: false); i++)
        {
            shell.Step(new MenuCommands { MoveY = 1 });
        }

        Assert.Equal(OriginalShell.KeysCellKey(rows - 1, second: false), shell.FocusedKey);
        Assert.True(Row(shell, OriginalShell.KeysCellKey(rows - 1, second: false))!.Visible);
        Assert.True(shell.KeysTop > 0, $"the window followed the cursor down ({shell.KeysTop})");
    }

    [Fact]
    public void TheKeysPageDrawsItsTabsWithTheStandingOneDepressed()
    {
        var shell = Shell(out _, out _);
        shell.OpenKeys();
        Click(shell, Row(shell, OriginalShell.KeysTabKey(2))!);
        var board = shell.Compose();
        var tabs = board.Plaques.Where(p => p.Label.Length > 0).ToList();
        Assert.Equal(OriginalShell.ControlTabs.Select(t => t.Name).ToArray(), tabs.Select(p => p.Label).ToArray());
        Assert.Equal(3, tabs[2].Frame);
        Assert.All(tabs.Where((_, i) => i != 2), p => Assert.NotEqual(3, p.Frame));
        Assert.Contains(board.Lines, l => l.Text == "Weapons");
        Assert.Contains(board.Lines, l => l.Text == "Action");
        Assert.Contains(board.Lines, l => l.Text == "Control A");
        Assert.Contains(board.Lines, l => l.Text == "Control B");
    }

    [Fact]
    public void TheControlsPageDrawsItsSeatRowAndNotTheMouseSensitivitySlider()
    {
        var shell = Shell(out var controls, out _);
        shell.OpenControlsPrefs();
        Assert.Equal("Player 1", Row(shell, OriginalShell.ControlsPlayerKey)!.Label);
        Assert.Null(Row(shell, "CP_S_MOUSE"));
        var board = shell.Compose();
        Assert.Contains(board.Lines, l => l.Text == "CONTROLS");
        Assert.Contains(board.Lines, l => l.Text == "Player");
        Assert.DoesNotContain(board.Lines, l => l.Text == "Mouse Sensitivity");
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Configure the keyboard", StringComparison.Ordinal));
        Assert.Equal(1, controls.Players.Count);
    }

    private static int IndexOf(ControlsFeature controls, InputAction action)
    {
        var actions = controls.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] == action)
            {
                return i;
            }
        }

        return 0;
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

    // One click as the shell reads it: the press arms the row and the release on it fires.
    private static void Click(OriginalShell shell, OriginalRow row)
    {
        float x = row.X + (row.Width / 2f);
        float y = row.Y + (row.Height / 2f);
        shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, true, true) });
        shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, false, false) });
    }

    private static OriginalShell Shell(out ControlsFeature controls, out FakeCaptureDevices devices) =>
        Shell(out controls, out devices, out _);

    private static OriginalShell Shell(
        out ControlsFeature controls, out FakeCaptureDevices devices, out List<int> written)
    {
        var saved = new List<int>();
        written = saved;
        devices = new FakeCaptureDevices();
        controls = new ControlsFeature((player, _) => saved.Add(player));
        controls.AddSeat(1, Profile(), devices, readsKeyboard: true);
        return Shell(controls, out _, out _);
    }

    private static OriginalShell Shell(
        ControlsFeature? controls, out FreeFlightFeature free, out PlayerSetupFeature setup)
    {
        free = new FreeFlightFeature();
        setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(Array.Empty<CSVM.Flight.CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), free, setup, Measure, controls: controls);
    }

    private static BindingProfile Profile()
    {
        var maps = new Dictionary<InputContext, ActionMap>();
        foreach (var context in Enum.GetValues<InputContext>())
        {
            maps[context] = DefaultBindings.MapFor(context, MenuControlsSeats.PadOf(context));
        }

        return new BindingProfile(maps, readsKeyboard: true);
    }

    // The fixture's strips: the button strips 240x200 in four frames, the tab strip 120x148 in
    // four, the list's bar and arrows at the shipped sizes, the plates unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PP_B_Large.png" => (120, 148),
        "PP_B_KbBar.png" => (16, 11),
        "PP_B_KbUp.png" or "PP_B_KbDown.png" => (16, 44),
        "PP_B_SliderSlot.png" or "PF_B_SliderSlot.png" => (171, 3),
        "PP_B_Slider.png" or "PF_B_Slider.png" => (43, 21),
        "PM_B_Paper.png" => (160, 112),
        _ when art.StartsWith("PM_B_", StringComparison.Ordinal) || art.StartsWith("PP_B_", StringComparison.Ordinal)
            => (240, 200),
        _ => null,
    };

    /// <summary>A seat's capture readers with nothing behind them but what a test puts there, one
    /// per context on the identity that context's rows sit on.</summary>
    internal sealed class FakeCaptureDevices : ICaptureDevices
    {
        private readonly Dictionary<InputContext, FakeDevices> _states = new();

        public FakeCaptureDevices()
        {
            foreach (var context in Enum.GetValues<InputContext>())
            {
                _states[context] = new FakeDevices(MenuControlsSeats.PadOf(context));
            }
        }

        /// <summary>The flight keymap's reader, which the six named tabs capture through.</summary>
        public FakeDevices Flight => _states[InputContext.Flight];

        public DeviceId PadOf(InputContext context) => MenuControlsSeats.PadOf(context);

        public IDeviceState For(InputContext context) => _states[context];
    }

    /// <summary>One frame of scripted hardware: the keys and buttons a test says are held.</summary>
    internal sealed class FakeDevices : IDeviceState
    {
        private readonly DeviceId _pad;

        public FakeDevices(DeviceId pad) => _pad = pad;

        public HashSet<int> Keys { get; } = new();

        public HashSet<int> Buttons { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) =>
            device == _pad && Buttons.Contains(button);

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) => 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}

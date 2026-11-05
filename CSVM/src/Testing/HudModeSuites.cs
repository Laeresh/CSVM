using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Bindings;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The HUD mode against real <see cref="FlightHud"/> panes, with no game data. Each pane
/// carries every part. Bare dial, tape and pipper controls stand in for the textured ones, since only
/// their visibility is read. The canvases stay out of the tree,
/// so no part draws with empty textures. The options file is a scratch store the suite points
/// <see cref="OptionsStore.DirectoryOverride"/> at. Each suite puts the process-wide mode and the
/// text block option back in a finally.</summary>
internal static class HudModeSuites
{
    private static readonly HudMode[] Modes = { HudMode.Full, HudMode.Minimal, HudMode.None };

    [Suite("hud-mode-parts",
        "the HUD mode on a pane with every part: in an outside view Full shows the dial cluster, the "
        + "compass tape, the throttle plate (its box on), the pipper, the marker HUD, the message "
        + "stack and both prompts exactly as a pane that was never given a mode, with the text block "
        + "only where its option is on; Minimal takes the dials, tape and plate off and shows the text "
        + "block, built in its Attach slot under the dials when the option is off, keeping the "
        + "pipper, markers, messages and prompts; None keeps the pipper, the message stack and the "
        + "prompts, the targeting HUD drawing the lead marker alone, and no text block; the cockpit "
        + "view is the same in all three modes and asks for no text block lines; a cutscene's hide takes everything in every mode; and "
        + "back in Full a late-built text block is hidden again where the option is off")]
    internal static void HudModeParts(TestContext ctx)
    {
        bool textBlock = FlightHud.TextBlockEnabled;
        try
        {
            foreach (bool option in new[] { false, true })
            {
                FlightHud.TextBlockEnabled = option;
                Parts(ctx, option);
            }
        }
        finally
        {
            FlightHud.TextBlockEnabled = textBlock;
        }
    }

    [Suite("hud-mode-cycle-splitscreen",
        "the Cycle HUD Mode action across two splitscreen seats: a press on the second seat steps "
        + "both panes to Minimal and posts 'HUD: Minimal' in both message stacks, a held key steps "
        + "nothing more, the next presses on the first seat walk None and back to Full with every "
        + "pane alike, and each step writes hudMode to the options file; a launch that applies the "
        + "saved word (a mission change or a restart) starts in the last mode chosen while a --det "
        + "launch starts in Full, and the cycle under --det steps the panes and writes no file; a pane "
        + "follows the process-wide mode on its next frame, which is how an Options apply reaches it, "
        + "the applied word and not the file, so the row moves the panes under --det as the key does; "
        + "and the step is silent while photo mode or the options leaf owns the keyboard")]
    internal static void HudModeCycleSplitscreen(TestContext ctx)
    {
        var before = HudModeSetting.Current;
        string? previousDir = OptionsStore.DirectoryOverride;
        string dir = Path.GetFullPath(Path.Combine(ctx.ScratchDir, "hud-mode-cycle-splitscreen"));
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        OptionsStore.DirectoryOverride = dir;
        var made = new List<GodotObject>();
        try
        {
            HudModeSetting.Current = HudMode.Full;
            var rigs = new[] { Seat(0, made), Seat(1, made) };
            var panes = rigs.Select(r => r.PilotHud).ToArray();
            var store = OptionsStore.UserOptions();
            foreach (var rig in rigs)
            {
                rig.CycleHudMode = () => CSVM.Launch.GameSession.StepHudMode(panes, store);
            }

            Press(ctx, rigs[1], panes, HudMode.Minimal, store);
            Press(ctx, rigs[0], panes, HudMode.None, store);
            Persists(ctx, HudMode.None);
            Press(ctx, rigs[0], panes, HudMode.Full, store);
            Press(ctx, rigs[0], panes, HudMode.Minimal, store);
            Persists(ctx, HudMode.Minimal);

            Silenced(ctx, rigs[0], panes);
            Follows(ctx, rigs, panes);
            Deterministic(ctx, panes);
        }
        finally
        {
            HudModeSetting.Current = before;
            OptionsStore.DirectoryOverride = previousDir;
            foreach (var node in made)
            {
                if (GodotObject.IsInstanceValid(node))
                {
                    node.Free();
                }
            }
        }
    }

    // One pane per text block option, its parts read in every mode in both views.
    private static void Parts(TestContext ctx, bool option)
    {
        var canvas = new CanvasLayer();
        var hud = Pane(canvas);
        var baseline = new CanvasLayer();
        var untouched = Pane(baseline);
        try
        {
            string when = option ? "text block option on" : "text block option off";
            var full = Shown(untouched);
            hud.SetHudMode(HudMode.Full);
            ctx.Check(Shown(hud).SequenceEqual(full) && !hud.TargetHud!.LeadOnly,
                $"{when}: Full shows exactly what a pane never given a mode shows ({Describe(hud)})");
            ctx.Check(full.Take(7).All(v => v) && full[7] == option,
                $"…every part, the text block only with its option ({Describe(hud)})");
            hud.SetHudMode(HudMode.Minimal);
            var minimal = Shown(hud);
            ctx.Check(!minimal[0] && !minimal[1] && !minimal[2] && minimal[7],
                $"{when}: Minimal takes the dials, tape and plate off and shows the text block ({Describe(hud)})");
            ctx.Check(minimal[3] && minimal[4] && minimal[5] && minimal[6] && !hud.TargetHud!.LeadOnly,
                $"…keeping the pipper, the marker HUD, the messages and the prompts ({Describe(hud)})");
            var text = hud.TextBlock;
            ctx.Check(text != null && text.GetParent() == canvas && text.GetIndex() == 0 && text.GetIndex() < hud.Compass!.GetIndex(),
                $"…its text block first on the canvas, the slot Attach gives it, under the tape and dials (index {text?.GetIndex()}, tape {hud.Compass!.GetIndex()})");
            ctx.Check(hud.DrawsTextBlock, $"…and the pane asks for the text block's lines");

            hud.SetHudMode(HudMode.None);
            var none = Shown(hud);
            ctx.Check(!none[0] && !none[1] && !none[2] && !none[7] && !hud.DrawsTextBlock,
                $"{when}: None takes the dials, tape, plate and text block off ({Describe(hud)})");
            ctx.Check(none[3] && none[4] && hud.TargetHud!.LeadOnly && none[5] && none[6],
                $"…keeping the pipper, the targeting HUD at its lead marker alone, the messages and the prompts ({Describe(hud)})");

            hud.SetHudMode(HudMode.Full);
            ctx.Check(Shown(hud).SequenceEqual(full) && hud.DrawsTextBlock == option,
                $"{when}: back in Full the pane reads as it did, a late-built text block hidden without its option ({Describe(hud)})");

            hud.SetCockpitView(true);
            untouched.SetCockpitView(true);
            var cockpit = Shown(untouched);
            foreach (var mode in Modes)
            {
                hud.SetHudMode(mode);
                ctx.Check(Shown(hud).SequenceEqual(cockpit) && !hud.TargetHud!.LeadOnly && !hud.DrawsTextBlock,
                    $"{when}: the cockpit view in {mode} is the cockpit view as it was ({Describe(hud)})");
            }

            hud.SetCockpitView(false);
            foreach (var mode in Modes)
            {
                hud.SetHudMode(mode);
                hud.SetVisible(false);
                ctx.Check(Shown(hud).All(v => !v), $"{when}: a cutscene's hide takes every part in {mode} ({Describe(hud)})");
                hud.SetVisible(true);
            }
        }
        finally
        {
            canvas.Free();
            baseline.Free();
        }
    }

    // A pane with every part its canvas can hold, attached the way a flight attaches it.
    private static FlightHud Pane(CanvasLayer canvas)
    {
        var hud = new FlightHud
        {
            Compass = new CompassTape(),
            Gauges = new GaugeCluster(),
            ThrottlePlate = ThrottlePlate.Build(),
            Reticle = new ImpactReticle(),
            TargetHud = new TargetHud(),
        };
        hud.Attach(canvas, canvas, null, null);
        hud.SetThrottlePlateShown(true);
        return hud;
    }

    // Dials, tape, plate, pipper, marker HUD, message stack, both prompts, text block.
    private static bool[] Shown(FlightHud hud) => new[]
    {
        hud.Gauges!.Visible,
        hud.Compass!.Visible,
        hud.ThrottlePlate?.Visible ?? false,
        hud.Reticle!.Visible,
        hud.TargetHud!.Visible,
        hud.MessageStack!.Visible,
        hud.AutoDock!.Visible && hud.CrashPrompt!.Visible,
        TextShown(hud),
    };

    private static bool TextShown(FlightHud hud) => hud.TextBlock?.Visible == true;

    private static string Describe(FlightHud hud)
    {
        var v = Shown(hud);
        return $"dials={v[0]} tape={v[1]} plate={v[2]} pipper={v[3]} markers={v[4]} leadOnly={hud.TargetHud!.LeadOnly} "
            + $"messages={v[5]} prompts={v[6]} text={v[7]}";
    }

    private static string Describe(IEnumerable<FlightHud> panes) =>
        string.Join(" | ", panes.Select(p => p.HudMode.ToString()));

    // A seat the way a splitscreen roster builds one, out of the tree: its HUD on a canvas of its own.
    private static FlightController Seat(int player, List<GodotObject> made)
    {
        var rig = new FlightController
        {
            PlayerIndex = player,
            UseKeyboard = player == 0,
            PadDevices = Array.Empty<int>(),
        };
        var canvas = new CanvasLayer();
        made.Add(rig);
        made.Add(canvas);
        rig.PilotHud.Attach(canvas, canvas, null, null);
        return rig;
    }

    // One press and release on one seat, read on every pane and in the file.
    private static void Press(TestContext ctx, FlightController seat, FlightHud[] panes, HudMode expected, OptionsStore store)
    {
        seat.HoldActionForTest(InputAction.CycleHudMode, true);
        seat.PollHudModeCycleForTest();
        string line = HudModeSetting.Line(expected);
        ctx.Check(panes.All(p => p.HudMode == expected) && HudModeSetting.Current == expected,
            $"a press on seat {seat.PlayerIndex + 1} steps every pane to {expected} ({Describe(panes)})");
        ctx.Check(panes.All(p => p.MessageStack!.LineAt(0) == line),
            $"…and every pane's message stack reads '{line}' ({string.Join(" | ", panes.Select(p => p.MessageStack!.LineAt(0)))})");
        ctx.Check(store.Load().HudMode == HudModeSetting.WordOf(expected),
            $"…and the options file holds hudMode={store.Load().HudMode}");
        seat.HoldActionForTest(InputAction.CycleHudMode, true);
        seat.PollHudModeCycleForTest();
        ctx.Check(panes.All(p => p.HudMode == expected) && HudModeSetting.Current == expected,
            $"…and the key still held on the next frame steps nothing more ({Describe(panes)})");
        seat.HoldActionForTest(InputAction.CycleHudMode, false);
        seat.PollHudModeCycleForTest();
    }

    // What the next launch reads: Launcher.LaunchSession applies the saved word for a mission change
    // and for a restart alike.
    private static void Persists(TestContext ctx, HudMode expected)
    {
        var running = HudModeSetting.Current;
        HudModeSetting.Current = HudMode.Full;
        HudModeSetting.ApplySaved(det: false);
        ctx.Check(HudModeSetting.Current == expected,
            $"a launch applying the saved word starts in {expected} ({HudModeSetting.Current})");
        HudModeSetting.ApplySaved(det: true);
        ctx.Check(HudModeSetting.Current == HudMode.Full,
            $"…while a --det launch over the same file starts in Full ({HudModeSetting.Current})");
        HudModeSetting.Current = running;
    }

    private static void Silenced(TestContext ctx, FlightController seat, FlightHud[] panes)
    {
        var mode = HudModeSetting.Current;
        seat.BeginPhotoMode();
        seat.HoldActionForTest(InputAction.CycleHudMode, true);
        seat.PollHudModeCycleForTest();
        ctx.Check(HudModeSetting.Current == mode && panes.All(p => p.HudMode == mode),
            $"a press under photo mode steps nothing ({Describe(panes)})");
        seat.HoldActionForTest(InputAction.CycleHudMode, false);
        seat.PollHudModeCycleForTest();
        seat.EndPhotoMode();
    }

    // An Options apply sets the process-wide mode alone, through the call ApplySaved makes once
    // LiveSettings.Persist has written the file. Each pane takes it on its own next frame.
    private static void Follows(TestContext ctx, FlightController[] rigs, FlightHud[] panes)
    {
        HudModeSetting.Apply("none");
        ctx.Check(panes.All(p => p.HudMode != HudMode.None), $"before their next frame the panes hold their mode ({Describe(panes)})");
        foreach (var rig in rigs)
        {
            rig.PollHudModeCycleForTest();
        }

        ctx.Check(panes.All(p => p.HudMode == HudMode.None), $"…and on it every pane follows the applied mode ({Describe(panes)})");
    }

    private static void Deterministic(TestContext ctx, FlightHud[] panes)
    {
        string? saved = OptionsStore.UserOptions().Load().HudMode;
        var mode = CSVM.Launch.GameSession.StepHudMode(panes, null);
        ctx.Check(mode == HudMode.Full && panes.All(p => p.HudMode == HudMode.Full),
            $"under --det the step still walks the panes, None to Full ({Describe(panes)})");
        ctx.Check(OptionsStore.UserOptions().Load().HudMode == saved,
            $"…and writes no file (hudMode={OptionsStore.UserOptions().Load().HudMode}, was {saved})");
    }
}

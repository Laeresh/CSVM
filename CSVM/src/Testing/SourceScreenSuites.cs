using System;
using System.IO;
using CSVM.Extraction;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The game-file screen as a built layer, the half a unit cannot reach. It pins each
/// view's words, buttons and focus, and the copy's progress through the frame tick. It pins the
/// questions asked once and the picker's mode and pad buttons. Every install is a few files in
/// scratch, every answer lands in a scratch store. Nothing here reads a real install or the
/// player's options.
/// ⚠ This pins the screen's wiring, not its look or the Deck's controls; both are judged by eye.</summary>
internal static class SourceScreenSuites
{
    [Suite("source-screen",
        "with no game files the picker view opens with Use these files focused, a non-install pick shows the "
        + "check's message, a temp install is taken and remembered, Copy shows the progress view with Cancel "
        + "focused and the bar fed through the frame tick, the copy is the source handed back, a source "
        + "without crimptch.rof notes it once with OK focused, and a second start with the same options is not asked again")]
    internal static void SourceScreen(TestContext ctx)
    {
        string root = Scratch(ctx);
        try
        {
            string install = Install(root);
            string data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
            var store = new OptionsStore(Directory.CreateDirectory(Path.Combine(root, "user")).FullName);
            Action? pending = null;
            var flow = new SourceFlow(data, null, string.Empty, store, ask: true, work => pending = work);
            var screen = Screen(ctx, flow, out var handbacks);
            try
            {
                ctx.Check(screen.Shown == SourceView.Asking && screen.FieldShown, $"a screen with no game files asks for them, with the field shown ({screen.Shown})");
                ctx.Check(screen.TitleText == flow.Title && screen.BodyText == flow.Body && screen.TitleText.Length > 0,
                    $"under the flow's title and body ({screen.TitleText})");
                ctx.Check(Focused(screen) == screen.DefaultButton && screen.DefaultButton.Text == "Use these files",
                    $"Use these files holds the focus ({Focused(screen)?.Name})");

                string notInstall = Directory.CreateDirectory(Path.Combine(root, "Documents")).FullName;
                screen.TakePick(notInstall);
                ctx.Check(screen.NoticeText == InstallLocator.Check(notInstall).Message,
                    $"a folder that is not an install shows the check's message ({screen.NoticeText})");

                screen.TakePick(install);
                ctx.Check(screen.NoticeText.Length == 0, $"a pick that is an install clears the notice ({screen.NoticeText})");
                screen.Press("Use these files");
                ctx.Check(screen.Shown == SourceView.Found && !screen.FieldShown, $"Use moves to the found view ({screen.Shown})");
                ctx.Check(store.Load().InstallPath == Path.GetFullPath(install), $"and remembers the pick ({store.Load().InstallPath})");
                ctx.Check(screen.Button("Copy, then play") != null && Focused(screen) == screen.Button("Play"),
                    $"with Play focused and the copy offered ({string.Join(", ", screen.ButtonLabels)})");

                screen.Press("Copy, then play");
                ctx.Check(screen.Shown == SourceView.Copying && Focused(screen) == screen.Button("Cancel") && screen.Button("Quit") == null,
                    $"Copy shows the progress view with Cancel focused and no Quit ({string.Join(", ", screen.ButtonLabels)})");
                pending?.Invoke();
                ctx.Check(screen.BarValue == 0, $"the bar has not seen the copy before the frame tick ({screen.BarValue})");
                screen._Process(0);
                ctx.Check(Math.Abs(screen.BarValue - 100) < 0.01, $"the tick moves the bar to the finished copy ({screen.BarValue})");
                ctx.Check(flow.Source?.Location == GameFileCopy.Target(data) && flow.Picked, $"and the copy is the source ({flow.Source?.Location})");

                ctx.Check(screen.Shown == SourceView.Patch && screen.BodyText.Contains("crimptch.rof", StringComparison.Ordinal),
                    $"the copy holds no patch archive, which is noted ({screen.Shown})");
                ctx.Check(Focused(screen) == screen.Button("OK"), $"with OK focused");
                ctx.Check(handbacks[0] == 0, $"and no hand-back before it is read ({handbacks[0]})");
                screen.Press("OK");
                screen._Process(0);
                ctx.Check(handbacks[0] == 1, $"OK hands back to the launcher once ({handbacks[0]})");
                ctx.Check(store.Load().PatchNoteShown == true, $"and the note is remembered");
                ctx.Check(new SourceFlow(data, flow.Source, string.Empty, store, ask: true).View == SourceView.Done,
                    $"so the next start with those game files asks nothing");
            }
            finally
            {
                Drop(ctx, screen);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Suite("source-upgrade",
        "game files beside an old extracted folder ask keep or delete with Keep it focused and the modding "
        + "folder named, Keep it leaves the folder and hands back once, Delete asks once more with Back "
        + "focused, and the confirmed delete removes the folder on a worker and hands back")]
    internal static void SourceUpgrade(TestContext ctx)
    {
        string root = Scratch(ctx);
        try
        {
            var source = new InstallFolderSource(Install(root, patched: true));
            string data = Path.Combine(root, "data");
            string extracted = Path.Combine(data, SourceFlow.ExtractedFolder);
            Directory.CreateDirectory(Path.Combine(extracted, "C1"));
            File.WriteAllText(Path.Combine(extracted, "C1", "gamez.zip"), "x");

            var keep = Screen(ctx, new SourceFlow(data, source, string.Empty, null, ask: true), out var kept);
            try
            {
                ctx.Check(keep.Shown == SourceView.Upgrade && keep.BodyText.Contains("modding folder", StringComparison.Ordinal),
                    $"an old extraction is asked about, naming the modding folder ({keep.Shown})");
                ctx.Check(Focused(keep) == keep.Button("Keep it"), $"with Keep it focused");
                keep.Press("Keep it");
                keep._Process(0);
                ctx.Check(kept[0] == 1 && Directory.Exists(extracted), $"Keep it hands back once and leaves the folder ({kept[0]})");
            }
            finally
            {
                Drop(ctx, keep);
            }

            Action? pending = null;
            var delete = Screen(ctx, new SourceFlow(data, source, string.Empty, null, ask: true, work => pending = work), out var deleted);
            try
            {
                delete.Press("Delete it...");
                ctx.Check(delete.Shown == SourceView.ConfirmDelete && Focused(delete) == delete.Button("Back"),
                    $"Delete asks once more, with Back focused ({delete.Shown})");
                ctx.Check(Directory.Exists(extracted), $"and nothing is deleted yet");
                delete.Press("Delete");
                ctx.Check(delete.Shown == SourceView.Deleting && delete.Button("Quit") == null, $"the confirmed delete runs ({delete.Shown})");
                pending?.Invoke();
                delete._Process(0);
                delete._Process(0);
                ctx.Check(!Directory.Exists(extracted), $"and removes the folder");
                ctx.Check(deleted[0] == 1, $"then hands back once ({deleted[0]})");
            }
            finally
            {
                Drop(ctx, delete);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Suite("source-picker",
        "the picker is Godot's own folder-or-disc-image dialog over the whole file system, opens on the folder "
        + "it is given, the left shoulder goes up a folder, Y takes the folder shown into the field with the "
        + "check's message when it is not an install, and Esc during a copy cancels it instead of quitting")]
    internal static void SourcePicker(TestContext ctx)
    {
        string root = Scratch(ctx);
        try
        {
            string install = Install(root);
            string inside = Directory.CreateDirectory(Path.Combine(install, "GOSDATA", "ASSETS")).FullName;
            string data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
            var screen = Screen(ctx, new SourceFlow(data, null, install, null, ask: false, _ => { }), out _);
            try
            {
                var picker = screen.Picker;
                ctx.Check(picker.FileMode == FileDialog.FileModeEnum.OpenAny && !picker.UseNativeDialog
                    && picker.Access == FileDialog.AccessEnum.Filesystem && string.Join(",", picker.Filters).Contains("*.cue", StringComparison.Ordinal),
                    $"the picker is Godot's own dialog choosing a folder or a disc image anywhere on disk (mode {picker.FileMode}, native {picker.UseNativeDialog}, access {picker.Access}, filters {string.Join(",", picker.Filters)})");

                screen.OpenPicker(inside);
                ctx.Check(picker.Visible, $"Choose shows the picker");
                ctx.Check(Same(picker.CurrentDir, inside), $"it opens on the folder it was given ({picker.CurrentDir})");

                picker.HandlePad(new InputEventJoypadButton { ButtonIndex = InstallPicker.UpButton, Pressed = true });
                ctx.Check(Same(picker.CurrentDir, Path.Combine(install, "GOSDATA")),
                    $"the left shoulder goes up one folder ({picker.CurrentDir})");

                picker.HandlePad(new InputEventJoypadButton { ButtonIndex = InstallPicker.TakeButton, Pressed = true });
                ctx.Check(!picker.Visible, $"Y closes the picker");
                ctx.Check(Same(screen.PathText, Path.Combine(install, "GOSDATA")), $"and puts the folder shown in the field ({screen.PathText})");
                ctx.Check(screen.NoticeText.Contains(install, StringComparison.Ordinal),
                    $"a pick inside the install names the install to choose instead ({screen.NoticeText})");

                screen.TakePick(install);
                ctx.Check(screen.NoticeText.Length == 0, $"a pick that is the install clears the notice ({screen.NoticeText})");

                screen.Press("Use these files");
                screen.Press("Copy, then play");
                screen._UnhandledInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
                ctx.Check(screen.Flow.Cancelling, $"Esc during a copy asks it to stop ({screen.Flow.View})");
            }
            finally
            {
                Drop(ctx, screen);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    // The screen in the tree, with a counter of its hand-backs to the launcher in handbacks[0].
    private static UI.Screens.SourceScreen Screen(TestContext ctx, SourceFlow flow, out int[] handbacks)
    {
        var count = new int[1];
        handbacks = count;
        var screen = UI.Screens.SourceScreen.Build(flow, () => count[0]++, () => { });
        ctx.Host.AddChild(screen);
        return screen;
    }

    private static Control? Focused(UI.Screens.SourceScreen screen) => screen.GetViewport().GuiGetFocusOwner();

    private static void Drop(TestContext ctx, UI.Screens.SourceScreen screen)
    {
        ctx.Host.RemoveChild(screen);
        screen.QueueFree();
    }

    private static string Scratch(TestContext ctx) =>
        Directory.CreateDirectory(Path.Combine(ctx.ScratchDir, "source-screen-" + Guid.NewGuid().ToString("N"))).FullName;

    // An install layout with one chapter archive, and the patch archive only when asked for.
    private static string Install(string root, bool patched = false)
    {
        string install = Path.Combine(root, "Crimson Skies");
        Directory.CreateDirectory(Path.Combine(install, "ZBD", "C1"));
        Directory.CreateDirectory(Path.Combine(install, "GOSDATA", "ASSETS"));
        File.WriteAllText(Path.Combine(install, "ZBD", "planes.zbd"), "x");
        File.WriteAllText(Path.Combine(install, "ZBD", "C1", "gamez.zbd"), "x");
        File.WriteAllText(Path.Combine(install, "GOSDATA", "ASSETS", "crimson.rof"), "x");
        if (patched)
        {
            File.WriteAllText(Path.Combine(install, "GOSDATA", "ASSETS", "crimptch.rof"), "x");
        }

        return install;
    }

    private static bool Same(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void TryDelete(string root)
    {
        try
        {
            Directory.Delete(root, true);
        }
        catch (IOException)
        {
        }
    }
}

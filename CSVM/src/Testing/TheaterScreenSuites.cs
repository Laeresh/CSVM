using System.Text;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.Testing;

/// <summary>The theater screen a stereo session shows its boards on, over the suite host's own root
/// with no stereo viewport. It reads which layers move onto the screen and when the screen and its
/// surround show. A real pause board on it is driven by the keyboard and by the pointer seam.
/// Module entries: docs/architecture/UI.md on TheaterScreen, CanvasRehost and TheaterLayout.</summary>
internal static class TheaterScreenSuites
{
    // The rows a pause board with every door wired carries, in its menu's order.
    private const int ResumeRow = 0;
    private const int PhotoRow = 1;
    private const int RowCount = 5;

    /// <summary>Builds the screen over the root and raises a Built-in pause board on a board layer.
    /// The board is then driven by key and by the pointer seam while it draws on the screen.</summary>
    [Suite("theater-screen",
        "under the stereo path the theater screen takes every board-layer canvas layer and leaves the flight chrome on the root, shows with a full surround while no flight is live, hides in flight until a board shows and then dims the world behind it, a real pause board on it takes Down and Enter from the keyboard as in flat play, a pointer ray through a row lands on that row through the seam, a Godot button on it takes a click pushed into the root, and every layer goes back to the root as it leaves")]
    internal static void TheaterScreenHostsBoards(TestContext ctx)
    {
        var report = new StringBuilder();
        var root = ctx.Host.GetViewport();
        float oversampling = root.OversamplingOverride;
        bool live = false;
        // A rotated seat far from the chapter origin, as a flight would stand it.
        var origin = new Transform3D(Basis.FromEuler(new Vector3(0.12f, -2.3f, 0.05f)), new Vector3(-6350f, 300f, -4144f));

        // A board layer standing before the screen is built, which the screen must sweep up.
        var early = new CanvasLayer { Name = "theater-early", Layer = HudLayers.Board };
        ctx.Host.AddChild(early);
        var theater = TheaterScreen.Build(root, camera: null, () => origin, () => live, eyeFeed: false);
        ctx.Host.AddChild(theater);
        var hud = new CanvasLayer { Name = "theater-hud", Layer = HudLayers.Hud };
        var layer = new CanvasLayer { Name = "theater-board", Layer = HudLayers.Board };
        var pause = new PauseState();
        var board = PauseBoard.Build(pause, exitsToMenu: true, _ => new MenuInput { Keyboard = true });
        int photos = 0;
        board.PhotoMode = () => photos++;
        board.Preferences = () => { };
        board.Restart = () => { };
        board.Exit = () => { };
        layer.AddChild(board);
        ctx.Host.AddChild(hud);
        ctx.Host.AddChild(layer);
        try
        {
            ctx.Check(early.CustomViewport == theater.Surface && layer.CustomViewport == theater.Surface,
                $"a board layer standing before the screen and one added after both draw on its surface");
            ctx.Check(hud.CustomViewport == null, $"a flight chrome layer stays on the root");
            ctx.Check(board.GetViewport() == root,
                $"the hosted board stays in the root's tree, so it lays out and takes input there");

            theater.Refresh();
            Vector2 layout = root.GetVisibleRect().Size;
            ctx.Check(theater.LayoutSize == layout && theater.Surface.Size2DOverride == (Vector2I)layout,
                $"the surface lays out the root's {layout} ({theater.Surface.Size2DOverride})");
            ctx.Check(theater.Surface.Size == TheaterLayout.SurfaceSize(layout),
                $"and draws it at {theater.Surface.Size}, two texels per authored board pixel");
            ctx.Check(theater.Shown && theater.SurroundOpacity == 1f,
                $"with no flight live the screen shows in a full surround ({theater.SurroundOpacity})");

            live = true;
            theater.Refresh();
            ctx.Check(!theater.Shown && theater.SurroundOpacity == 0f,
                $"in flight, with the board hidden, neither the screen nor the surround shows");

            pause.TryToggle(0);
            board._Process(0.0);
            PauseBoardSuites.Settle(board);
            theater.Refresh();
            ctx.Check(theater.Shown && theater.SurroundOpacity == TheaterScreen.DimOpacity,
                $"the pause raises the board and the screen over the world dimmed to {theater.SurroundOpacity}");
            ctx.Same(ResumeRow, board.FocusedRow, $"with the cursor on Resume");

            Keys(ctx, board, report, ref photos);
            Pointer(ctx, theater, board, report);
            GuiClick(ctx, theater, layer);
            Priorities(ctx, theater);

            pause.ForceResume();
            theater.Refresh();
            ctx.Check(!theater.Shown, $"resuming takes the screen away again");

            ctx.Host.RemoveChild(layer);
            ctx.Check(layer.CustomViewport == root, $"a hosted layer leaving the tree is handed back to the root");
            ctx.Host.AddChild(layer);
            ctx.Check(layer.CustomViewport == theater.Surface, $"and taken again when it comes back");
        }
        finally
        {
            ReleaseKeys();
            pause.ForceResume();
            theater.QueueFree();
            ctx.Host.RemoveChild(theater);
            ctx.Check(early.CustomViewport == root && layer.CustomViewport == root,
                $"the screen leaving hands every layer back to the root");
            ctx.Check(root.OversamplingOverride == oversampling,
                $"and restores the root's font oversampling ({root.OversamplingOverride})");
            foreach (var node in new Node[] { early, hud, layer })
            {
                if (node.IsInsideTree())
                {
                    ctx.Host.RemoveChild(node);
                }

                node.QueueFree();
            }

            ctx.WriteArtifact($"test-theater-screen.txt", report.ToString());
        }
    }

    // Down then Enter, through Godot's own input as a keyboard delivers them, a frame each way.
    private static void Keys(TestContext ctx, PauseBoard board, StringBuilder report, ref int photos)
    {
        board._Process(0.0);
        Press(Godot.Key.Down, true);
        board._Process(0.016);
        Press(Godot.Key.Down, false);
        board._Process(0.016);
        ctx.Same(PhotoRow, board.FocusedRow, $"Down on the keyboard moves the hosted board's cursor to Photo Mode");
        Press(Godot.Key.Enter, true);
        board._Process(0.016);
        Press(Godot.Key.Enter, false);
        board._Process(0.016);
        ctx.Same(1, photos, $"and Enter fires that row");
        report.AppendLine($"keys: focused {board.FocusedRow}, photo mode opened {photos} time(s)");
    }

    // A hand-held ray, off the eye, through the middle of a row lands on that row by the seam.
    private static void Pointer(TestContext ctx, TheaterScreen theater, PauseBoard board, StringBuilder report)
    {
        if (board.Rows is not { } rows)
        {
            ctx.Check(false, $"the raised board carries its menu");
            return;
        }

        var hand = new Vector3(0.18f, -0.32f, -0.12f);
        var size = theater.Layout.SizeM(theater.LayoutSize);
        int landed = 0;
        for (int row = 0; row < RowCount; row++)
        {
            Vector2 middle = rows.RowRect(row).GetCenter();
            Vector2 uv = middle / theater.LayoutSize;
            var onScreen = theater.Layout.Pose.Origin + new Vector3((uv.X - 0.5f) * size.X, (0.5f - uv.Y) * size.Y, 0f);
            if (theater.Hit(hand, (onScreen - hand).Normalized()) is { } hit)
            {
                Vector2 at = theater.LayoutPoint(hit);
                landed += rows.RowAt(at.X, at.Y) == row ? 1 : 0;
                report.AppendLine($"row {row}: middle {middle} hit uv {hit} layout {at}");
            }
        }

        ctx.Same(RowCount, landed, $"a pointer ray through each row's middle lands on that row");
        ctx.Check(theater.Hit(hand, new Vector3(0f, 0f, 1f)) == null && theater.Hit(hand, new Vector3(1f, 0f, -0.01f)) == null,
            $"a ray pointing away from the screen, or past its edge, lands nowhere");
    }

    // Godot's own controls on a hosted layer still take the root's mouse events.
    private static void GuiClick(TestContext ctx, TheaterScreen theater, CanvasLayer layer)
    {
        int clicks = 0;
        var button = new Button { Name = "theater-button", Position = new Vector2(8f, 8f), Size = new Vector2(64f, 32f) };
        button.Pressed += () => clicks++;
        layer.AddChild(button);
        try
        {
            Vector2 at = theater.LayoutPoint(new Vector2(40f, 24f) / theater.LayoutSize);
            var root = layer.GetViewport();
            root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at }, true);
            root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at }, true);
            ctx.Same(1, clicks, $"a button on the screen takes a click the pointer seam pushes into the root at {at}");
        }
        finally
        {
            layer.RemoveChild(button);
            button.QueueFree();
        }
    }

    private static void Priorities(TestContext ctx, TheaterScreen theater)
    {
        var material = (ShaderMaterial)theater.ScreenQuad.MaterialOverride;
        int fade = HeadLockedQuads.PriorityOf(HeadLockedQuads.Tier.MissionEndFade);
        ctx.Check(fade < TheaterScreen.SurroundPriority && TheaterScreen.SurroundPriority < TheaterScreen.ScreenPriority
            && TheaterScreen.ScreenPriority <= RenderingServer.MaterialRenderPriorityMax && material.RenderPriority == TheaterScreen.ScreenPriority,
            $"the surround ({TheaterScreen.SurroundPriority}) and the screen ({TheaterScreen.ScreenPriority}) draw over the mission-end fade ({fade})");
        ctx.Check(material.Shader.Code.Contains("depth_test_disabled"),
            $"and the screen ignores depth, so the cockpit never cuts a board");
    }

    private static void Press(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
        Input.FlushBufferedEvents();
    }

    private static void ReleaseKeys()
    {
        Press(Godot.Key.Down, false);
        Press(Godot.Key.Enter, false);
    }
}

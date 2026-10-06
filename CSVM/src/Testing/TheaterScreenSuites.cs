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

    /// <summary>A presenting cutscene on the screen. It covers when the cutscene comes up and goes,
    /// and how its pair stands on the authored camera. It checks each eye's frustum against a flat
    /// camera's.</summary>
    [Suite("theater-cutscene",
        "under the stereo path a presenting cutscene in flight brings the theater screen up in a full surround with the camera's plain environment, its two cameras stand one interocular apart on the authored camera with its orientation, layers less the headset's and pre-swap environment, a point at the convergence lands on the same pixel in both eyes and on a flat 4:3 camera's, a far point carries the parallax law's disparity, a board raised over it keeps it playing, and its hand-back hides the screen, stops both renders and gives the camera its environment back")]
    internal static void TheaterPlaysACutscene(TestContext ctx)
    {
        var report = new StringBuilder();
        var root = ctx.Host.GetViewport();
        // Not the stand-in's default separation, so the pair is seen to take the runtime's.
        float ipd = 0.064f;
        float fov = 55.76f;
        float convergence = StereoPair.ConvergenceM(7.5f);
        bool live = false;
        bool playing = false;
        var origin = new Transform3D(Basis.FromEuler(new Vector3(0f, -2.3f, 0f)), new Vector3(-6350f, 300f, -4144f));
        var look = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Sky };
        var authored = new Camera3D
        {
            Name = "theater-cutscene-camera",
            Fov = fov,
            Near = 0.05f,
            Far = 40000f,
            CullMask = 0xFFFFFu & ~(1u << 13),
            Environment = look,
            Current = false,
        };
        ctx.Host.AddChild(authored);
        authored.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(-0.31f, 1.9f, 0.12f)), new Vector3(-6120f, 412f, -3980f));
        var theater = TheaterScreen.Build(root, authored, () => origin, () => live, eyeFeed: false,
            () => playing ? new TheaterCutscene(authored, convergence) : null, () => ipd);
        ctx.Host.AddChild(theater);
        var flat = new SubViewport { Name = "theater-cutscene-flat", Size = TheaterLayout.CutsceneSize, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
        var control = new Camera3D { Fov = fov, Near = authored.Near, Far = authored.Far, Current = true };
        flat.AddChild(control);
        ctx.Host.AddChild(flat);
        var layer = new CanvasLayer { Name = "theater-cutscene-board", Layer = HudLayers.Board, Visible = false };
        layer.AddChild(new ColorRect { Size = new Vector2(64f, 64f) });
        ctx.Host.AddChild(layer);
        try
        {
            playing = true;
            theater.Refresh();
            ctx.Check(theater.Shown && theater.SurroundOpacity == 1f && !theater.PlaysCutscene,
                $"with no flight live a cutscene is not the screen's to play: the menus' full surround stands ({theater.SurroundOpacity})");

            live = true;
            playing = false;
            theater.Refresh();
            ctx.Check(!theater.Shown && !theater.PlaysCutscene && theater.CutsceneEyes[0].RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled,
                $"in flight with no cutscene the screen is down and neither eye renders");

            playing = true;
            theater.Refresh();
            ctx.Check(theater.Shown && theater.PlaysCutscene && theater.SurroundOpacity == 1f,
                $"a presenting cutscene raises the screen in a full surround ({theater.SurroundOpacity})");
            ctx.Check(theater.CutsceneEyes[0].RenderTargetUpdateMode == SubViewport.UpdateMode.Always
                && theater.CutsceneEyes[1].RenderTargetUpdateMode == SubViewport.UpdateMode.Always,
                $"and both eyes render");
            ctx.Check(authored.Environment != look && theater.CutsceneCameras[0].Environment == look && theater.CutsceneCameras[1].Environment == look,
                $"the camera takes the plain environment while each eye keeps the look it wore before");
            Pair(ctx, theater, authored, control, ipd, convergence, report);
            Layers(ctx, theater, authored);

            layer.Visible = true;
            theater.Refresh();
            ctx.Check(theater.Shown && theater.PlaysCutscene && theater.SurroundOpacity == 1f,
                $"a board raised over the cutscene leaves it playing under the board in the full surround");
            layer.Visible = false;

            playing = false;
            theater.Refresh();
            ctx.Check(!theater.Shown && !theater.PlaysCutscene && authored.Environment == look
                && theater.CutsceneEyes[1].RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled,
                $"the hand-back takes the screen down, stops both renders and gives the camera its environment back");
        }
        finally
        {
            theater.QueueFree();
            ctx.Host.RemoveChild(theater);
            foreach (var node in new Node[] { authored, flat, layer })
            {
                ctx.Host.RemoveChild(node);
                node.QueueFree();
            }

            ctx.WriteArtifact($"test-theater-cutscene.txt", report.ToString());
        }
    }

    // The pair against the parallax law and a flat camera on the authored pose, at a 4:3 render.
    private static void Pair(TestContext ctx, TheaterScreen theater, Camera3D authored, Camera3D control, float ipd,
        float convergence, StringBuilder report)
    {
        var left = theater.CutsceneCameras[0];
        var right = theater.CutsceneCameras[1];
        var pose = authored.GlobalTransform;
        control.Transform = pose;
        var mid = (left.GlobalPosition + right.GlobalPosition) * 0.5f;
        var across = right.GlobalPosition - left.GlobalPosition;
        ctx.Check(Mathf.Abs(across.Length() - ipd) < 0.001f && across.Normalized().Dot(pose.Basis.X.Normalized()) > 0.999f
            && mid.DistanceTo(pose.Origin) < 0.001f,
            $"the eyes stand {across.Length() * 1000f:0.#} mm apart along the camera's right, centred on it");
        ctx.Check(left.GlobalBasis.IsEqualApprox(pose.Basis.Orthonormalized()) && right.GlobalBasis.IsEqualApprox(pose.Basis.Orthonormalized()),
            $"with the authored camera's orientation");
        ctx.Check(left.Projection == Camera3D.ProjectionType.Frustum && left.Near == authored.Near && left.Far == authored.Far,
            $"each eye a shifted frustum on the authored planes");

        // Points ahead of the camera in its own frame: on the convergence plane, and far away.
        var at = pose.Origin + (pose.Basis * new Vector3(1.3f, -0.8f, -convergence));
        Vector2 l = left.UnprojectPosition(at), r = right.UnprojectPosition(at), c = control.UnprojectPosition(at);
        report.AppendLine($"convergence {convergence} m: left {l} right {r} flat {c}");
        ctx.Check(l.DistanceTo(r) < 0.5f && l.DistanceTo(c) < 0.5f,
            $"a point at the convergence lands on the same pixel in both eyes and on the flat camera ({l}, {r}, {c})");

        float depth = 3000f;
        var far = pose.Origin + (pose.Basis * new Vector3(-40f, 25f, -depth));
        float px = right.UnprojectPosition(far).X - left.UnprojectPosition(far).X;
        float height = TheaterLayout.Default.HeightM;
        float law = StereoPair.ParallaxM(depth, ipd, convergence, authored.Fov, height) / height * TheaterLayout.CutsceneSize.Y;
        report.AppendLine($"far {depth} m: disparity {px:0.###} px, law {law:0.###} px");
        ctx.Check(Mathf.Abs(px - law) < 0.3f && px > 0f,
            $"a point {depth} m out lies {px:0.##} px behind the screen, the parallax law's {law:0.##} px");
        float vertical = right.UnprojectPosition(far).Y - left.UnprojectPosition(far).Y;
        ctx.Check(Mathf.Abs(vertical) < 0.05f, $"with no vertical disparity ({vertical:0.###} px)");
    }

    private static void Layers(TestContext ctx, TheaterScreen theater, Camera3D authored)
    {
        uint mask = theater.CutsceneCameras[0].CullMask;
        ctx.Check(mask == (authored.CullMask & ~SplitScreen.HeadsetLayer) && theater.CutsceneCameras[1].CullMask == mask,
            $"each eye draws the camera's layers less the headset's (0x{mask:X5})");
        ctx.Check(theater.ScreenQuad.Layers == SplitScreen.HeadsetLayer,
            $"and the screen stands on the headset's layer, out of its own picture");
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

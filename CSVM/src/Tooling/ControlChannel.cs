using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSVM.Session.World;
using CSVM.Utils;
using Godot;

namespace CSVM.Tooling;

/// <summary><c>--control=&lt;port&gt;</c>: a loopback line protocol an agent drives a live run
/// through, one command per line and one reply line per command. A worker thread owns the socket;
/// every command runs on the main thread in <see cref="Tick"/>, so nothing here touches the scene
/// tree off it. The verbs are in <c>scripts/Control.ps1</c>'s help. The sim starts held on the
/// first frame a clock exists, so the agent's think time never advances the mission.</summary>
public sealed class ControlChannel
{
    // ponytail: a fixed idle quit; make it a flag if an agent ever needs a longer think.
    private const double IdleQuitSeconds = 600.0;

    private readonly ConcurrentQueue<Command> _inbox = new();
    private readonly TcpListener _listener;
    private bool _held = true;
    private double _idle;
    private Camera3D? _camera;
    private Camera3D? _stoodDown;

    // The one command spanning frames: a step waits for its frame, a shot for a rendered frame.
    private Command? _waiting;
    private long _stepTarget;
    private string? _shotPath;

    public ControlChannel(int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        new Thread(Serve) { IsBackground = true, Name = "control-channel" }.Start();
        Log.Info("core", $"control: listening on 127.0.0.1:{port}");
    }

    /// <summary>The session's cutscene, read per command; null at the launchscreen.</summary>
    public Func<CutsceneController?> Cutscene { get; init; } = () => null;

    /// <summary>The first seat's aircraft, for <c>state</c>; null when nothing flies.</summary>
    public Func<Node3D?> Plane { get; init; } = () => null;

    /// <summary>Runs the queued commands and finishes a waiting one. Once per <c>_Process</c>, after
    /// the session's own frame, so a step lands before its reply reads the clock.</summary>
    public void Tick(Viewport viewport, SceneTree tree, double delta)
    {
        var clock = GameClock.Current;
        if (clock != null)
        {
            clock.ScriptHeld = _held;
        }

        if (_waiting != null)
        {
            Finish(viewport, clock);
            return;
        }

        if (!_inbox.TryDequeue(out var cmd))
        {
            _idle += delta;
            if (_idle > IdleQuitSeconds)
            {
                Log.Info("core", $"control: idle {IdleQuitSeconds:0} s, quitting");
                tree.Quit();
            }

            return;
        }

        _idle = 0;
        Log.Info("core", $"control: {cmd.Line}");
        try
        {
            Run(cmd, viewport, tree, clock);
        }
        catch (Exception e) when (e is FormatException or IndexOutOfRangeException or ArgumentException)
        {
            cmd.Reply.TrySetResult($"err {e.Message}");
        }
    }

    private static Vector3 Vec(string arg)
    {
        var p = arg.Split(',');
        if (p.Length != 3)
        {
            throw new FormatException($"'{arg}' is not x,y,z (a decimal comma?)");
        }

        return new Vector3(
            float.Parse(p[0], CultureInfo.InvariantCulture),
            float.Parse(p[1], CultureInfo.InvariantCulture),
            float.Parse(p[2], CultureInfo.InvariantCulture));
    }

    private void Run(Command cmd, Viewport viewport, SceneTree tree, GameClock? clock)
    {
        var words = cmd.Line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string verb = words.Length > 0 ? words[0] : string.Empty;
        switch (verb)
        {
            case "state":
                cmd.Reply.TrySetResult(State(viewport, clock));
                return;
            case "pause":
                _held = true;
                cmd.Reply.TrySetResult("ok");
                return;
            case "run":
                _held = false;
                cmd.Reply.TrySetResult("ok");
                return;
            case "step":
                if (clock == null)
                {
                    cmd.Reply.TrySetResult("err no session clock yet");
                    return;
                }

                _held = true;
                _stepTarget = clock.Frame + (words.Length > 1 ? long.Parse(words[1], CultureInfo.InvariantCulture) : 1);
                _waiting = cmd;
                return;
            case "skip":
                cmd.Reply.TrySetResult(Cutscene() is { Playing: true } scene
                    ? scene.Skip() ? "ok skipped" : "ok declined (hold ff to fast-forward)"
                    : "err no cutscene playing");
                return;
            case "ff":
                if (Cutscene() is not { } ff)
                {
                    cmd.Reply.TrySetResult("err no cutscene");
                    return;
                }

                ff.HoldFastForward(words.Length > 1 && words[1] == "on");
                cmd.Reply.TrySetResult("ok");
                return;
            case "camera":
                cmd.Reply.TrySetResult(Camera(viewport, words));
                return;
            case "shot":
                _shotPath = Path.GetFullPath(string.Join(' ', words[1..]));
                _waiting = cmd;  // the next _Process reads the frame drawn after this one
                return;
            case "quit":
                cmd.Reply.TrySetResult("ok");
                tree.Quit();
                return;
            default:
                cmd.Reply.TrySetResult($"err unknown verb '{verb}'");
                return;
        }
    }

    private void Finish(Viewport viewport, GameClock? clock)
    {
        var cmd = _waiting!;
        if (_shotPath != null)
        {
            var img = viewport.GetTexture().GetImage();
            Directory.CreateDirectory(Path.GetDirectoryName(_shotPath)!);
            var err = img?.SavePng(_shotPath) ?? Error.Unavailable;
            cmd.Reply.TrySetResult(err == Error.Ok ? $"ok {_shotPath} frame={clock?.Frame ?? 0}" : $"err save {err}");
            _shotPath = null;
            _waiting = null;
            return;
        }

        if (clock == null || clock.Frame >= _stepTarget)
        {
            cmd.Reply.TrySetResult($"ok frame={clock?.Frame ?? 0}");
            _waiting = null;
            return;
        }

        clock.StepOnce();
    }

    // camera x,y,z x,y,z [fov] places a channel-owned camera looking at the second point;
    // camera off hands the view back to whichever camera had it.
    private string Camera(Viewport viewport, string[] words)
    {
        if (words.Length > 1 && words[1] == "off")
        {
            if (_camera == null)
            {
                return "ok";
            }

            _camera.QueueFree();
            _camera = null;
            if (_stoodDown != null && GodotObject.IsInstanceValid(_stoodDown))
            {
                _stoodDown.Current = true;
            }

            return "ok";
        }

        var eye = Vec(words[1]);
        var at = Vec(words[2]);
        if (_camera == null)
        {
            _stoodDown = viewport.GetCamera3D();
            _camera = new Camera3D { Name = "ControlCamera", Far = 40000f };
            viewport.AddChild(_camera);
        }

        if (words.Length > 3)
        {
            _camera.Fov = float.Parse(words[3], CultureInfo.InvariantCulture);
        }

        _camera.LookAtFromPosition(eye, at, Vector3.Up);
        _camera.Current = true;
        return "ok";
    }

    private string State(Viewport viewport, GameClock? clock)
    {
        var sb = new StringBuilder("ok");
        sb.Append(CultureInfo.InvariantCulture, $" session={(clock != null ? "yes" : "no")} held={_held}");
        if (clock != null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" frame={clock.Frame} time={clock.Time:0.###}");
        }

        var scene = Cutscene();
        sb.Append(CultureInfo.InvariantCulture, $" cutscene={(scene is { Playing: true } ? "playing" : "none")}");
        if (viewport.GetCamera3D() is { } cam)
        {
            var x = cam.GlobalTransform;
            sb.Append(CultureInfo.InvariantCulture, $" cam={CaptureDirector.Vec3Arg(x.Origin)} dir={CaptureDirector.DirArg(-x.Basis.Z)} fov={cam.Fov:0.#}");
        }

        if (Plane() is { } plane && GodotObject.IsInstanceValid(plane))
        {
            sb.Append(CultureInfo.InvariantCulture, $" plane={CaptureDirector.Vec3Arg(plane.GlobalPosition)}");
        }

        return sb.ToString();
    }

    // One client at a time, one line in and one line out; a client may hold the connection or
    // reconnect per command.
    private void Serve()
    {
        while (true)
        {
            using var client = _listener.AcceptTcpClient();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };
            try
            {
                while (reader.ReadLine() is { } line)
                {
                    var cmd = new Command(line.Trim(), new TaskCompletionSource<string>());
                    _inbox.Enqueue(cmd);
                    writer.WriteLine(cmd.Reply.Task.Result);
                }
            }
            catch (IOException)
            {
                // The client went away mid-reply; wait for the next one.
            }
        }
    }

    private sealed record Command(string Line, TaskCompletionSource<string> Reply);
}

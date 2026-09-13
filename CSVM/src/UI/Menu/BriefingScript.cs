using System;
using System.Collections.Generic;
using CSVM.Mech3;

namespace CSVM.UI.Menu;

/// <summary>The reveal script's opcode vocabulary, censused across the briefing's 24 mission
/// states, the pause screen's <c>ESC_SCRIPT</c>s and the load screen's <c>LOADING_SCRIPT</c>s
/// (docs/formats/briefing.md). No other opcode occurs in any of the three, so an unknown one is a
/// reader bug.</summary>
public enum BriefingOp
{
    /// <summary>Starts the mission's narration wav, which is the reveal's clock.</summary>
    PlaySound,

    /// <summary>Blocks until the narration reaches a numbered cue point.</summary>
    WaitForMarker,

    /// <summary>Places a named picture element at a fixed dialog position.</summary>
    Pict,

    /// <summary>Opacity tween from one value to another over a duration.</summary>
    Fade,

    /// <summary>Rotation tween, in revolutions, over a duration.</summary>
    Spin,

    /// <summary>Position tween along a path over a duration.</summary>
    Move,

    /// <summary>A straight connector line between two points.</summary>
    Line,

    /// <summary>Shows an element.</summary>
    On,

    /// <summary>Hides an element.</summary>
    Off,

    /// <summary>Binds a text element to an entry of the mission's objectives list.</summary>
    Objective,

    /// <summary>A fixed-time pause, independent of the narration's cue points.</summary>
    Wait,

    /// <summary>Sends an element behind everything else in draw order.</summary>
    ToBack,

    /// <summary>A bitmap cycle at a fixed position and rate. Only the load screen's script authors
    /// one, for the propeller beside its bar, and it carries all six frames and the rate: a still
    /// composition draws the first, a build reporting its steps steps through them.</summary>
    Cycle,
}

/// <summary>One dialog-space point, as the script's <c>at</c>, <c>path</c> and <c>points</c>
/// arguments carry it. The dialog is the original's 800x600 screen.</summary>
public readonly record struct BriefingPoint(float X, float Y);

/// <summary>A connector line's authored colour, the script's own 0-255 RGB triple.</summary>
public readonly record struct BriefingColor(byte R, byte G, byte B);

/// <summary>
/// One opcode with its arguments. The fields are shared across opcodes the way the data shares
/// them: <see cref="From"/>/<see cref="To"/> are a fade's start and end and a spin's start and
/// end revolutions, <see cref="Duration"/> is also a <see cref="BriefingOp.Wait"/>'s seconds, and
/// <see cref="Index"/> is a marker number, an objective entry or a start marker by opcode.
/// </summary>
public sealed record BriefingStep
{
    /// <summary>Which opcode this is.</summary>
    public BriefingOp Op { get; init; }

    /// <summary>The element the opcode acts on, or the sound name for
    /// <see cref="BriefingOp.PlaySound"/>.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The picture a <see cref="BriefingOp.Pict"/> loads.</summary>
    public string Bitmap { get; init; } = string.Empty;

    /// <summary>Where a <see cref="BriefingOp.Pict"/> places its element.</summary>
    public BriefingPoint At { get; init; }

    /// <summary>Whether <see cref="At"/> is the picture's middle rather than its top left, which
    /// is the script's own <c>center</c> flag and how most flags and photographs are placed.</summary>
    public bool Center { get; init; }

    /// <summary>A <see cref="BriefingOp.Line"/>'s authored colour.</summary>
    public BriefingColor Color { get; init; }

    /// <summary>A tween's starting value.</summary>
    public float From { get; init; }

    /// <summary>A tween's ending value.</summary>
    public float To { get; init; }

    /// <summary>A tween's or a wait's length in seconds. Authored constants, never invented.</summary>
    public float Duration { get; init; }

    /// <summary>A marker number, an objectives-list entry, or a start marker.</summary>
    public int Index { get; init; }

    /// <summary>A move's path or a line's two endpoints.</summary>
    public IReadOnlyList<BriefingPoint> Path { get; init; } = Array.Empty<BriefingPoint>();

    /// <summary>A <see cref="BriefingOp.Cycle"/>'s whole frame list, in authored order; empty for
    /// every other opcode.</summary>
    public IReadOnlyList<string> Frames { get; init; } = Array.Empty<string>();

    /// <summary>A <see cref="BriefingOp.Cycle"/>'s authored rate in frames a second.</summary>
    public float Fps { get; init; }
}

/// <summary>
/// One mission's briefing state: the parchment map bitmap it draws, the narration it plays, and
/// the reveal script that brings the flags and the objectives note in step with the voice. The
/// map art is reused across missions (13 bitmaps for 24 states), so a reader takes the name from
/// the state and never builds one from the mission's position.
/// </summary>
public sealed record BriefingState(
    string Key, string Background, string Sound, int StartMarker, IReadOnlyList<BriefingStep> Steps);

/// <summary>
/// The shared <c>Briefing.zrd</c> dialog: 24 mission states keyed <c>brief_c&lt;campaign&gt;
/// &lt;mission&gt;</c> from the mission's own <c>cm_sequence</c> entry, plus a <c>default</c>
/// loading state that carries no script. The states sit at the reader's top level, beside
/// <c>BRIEFINGDIALOG</c> rather than inside it. Decode: docs/formats/briefing.md.
/// </summary>
public sealed class BriefingDialog
{
    private readonly Dictionary<string, BriefingState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every mission state, by key.</summary>
    public IReadOnlyDictionary<string, BriefingState> States => _states;

    /// <summary>The state key for a mission's storage address. ⚠ The digits are the ZBD world
    /// folder and its <c>M0n</c> number, not the story chapter and act position.</summary>
    public static string StateKey(int campaign, int mission) => $"brief_c{campaign}{mission}";

    /// <summary>Loads the dialog from a shared zrdr scope (<c>extracted/zrdr.zip</c> or its
    /// unpacked sibling). A state with no script is skipped, which drops <c>default</c>.</summary>
    public static BriefingDialog Load(string zrdrPath)
    {
        var dialog = new BriefingDialog();
        var root = Zrdr.LoadFile(zrdrPath, "Briefing.json");
        if (root.Count == 0 || root[0] is not List<object?> entries)
        {
            return dialog;
        }

        // The reader's top level pairs a key with a list, but tolerates a bare flag between two
        // of them, so step by what is there rather than by two.
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] is not string key || i + 1 >= entries.Count
                || entries[i + 1] is not List<object?> body)
            {
                continue;
            }

            i++;
            if (key.StartsWith("brief_c", StringComparison.OrdinalIgnoreCase)
                && ParseState(key, body) is { } state)
            {
                dialog._states[key] = state;
            }
        }

        return dialog;
    }

    /// <summary>The state a mission's <c>cm_sequence</c> entry names, or null when the dialog
    /// carries none.</summary>
    public BriefingState? Find(int campaign, int mission) =>
        _states.TryGetValue(StateKey(campaign, mission), out var state) ? state : null;

    /// <summary>Reads one reveal script into its beats. Shared with <see cref="EscapeDialog"/>,
    /// whose <c>ESC_SCRIPT</c> and <c>LOADING_SCRIPT</c> are the same opcode vocabulary over the
    /// same argument spellings; an opcode outside <see cref="BriefingOp"/> is skipped rather than
    /// throwing.</summary>
    internal static List<BriefingStep> ParseScript(List<object?> script)
    {
        var steps = new List<BriefingStep>();
        for (int i = 0; i < script.Count; i++)
        {
            if (script[i] is not string name || i + 1 >= script.Count
                || script[i + 1] is not List<object?> args)
            {
                continue;
            }

            i++;
            if (Enum.TryParse(name, out BriefingOp op))
            {
                steps.Add(ParseStep(op, args));
            }
        }

        return steps;
    }

    // A state's own art, narration and script. BACKGROUND_IMAGES is a list of [bitmap, x, y]
    // entries and every mission state ships exactly one.
    private static BriefingState? ParseState(string key, List<object?> body)
    {
        var d = ZrdrDict.FromAlternating(body);
        if (d.List("SCRIPT") is not { } script)
        {
            return null;
        }

        string background = d.List("BACKGROUND_IMAGES") is { Count: > 0 } images
            && images[0] is List<object?> { Count: > 0 } first && first[0] is string bitmap
            ? bitmap
            : string.Empty;

        var steps = ParseScript(script);
        string sound = string.Empty;
        int startMarker = 0;
        foreach (var step in steps)
        {
            if (step.Op == BriefingOp.PlaySound)
            {
                sound = step.Id;
                startMarker = step.Index;
                break;
            }
        }

        return new BriefingState(key, background, sound, startMarker, steps);
    }

    // Most opcodes carry a bare element id then alternating key/value pairs, which ZrdrDict reads
    // straight through (the leading id lands as a bare flag). Wait and WaitForMarker carry a bare
    // number instead.
    private static BriefingStep ParseStep(BriefingOp op, List<object?> args)
    {
        var d = ZrdrDict.FromAlternating(args);
        string id = args.Count > 0 && args[0] is string first ? first : string.Empty;
        float bare = args.Count > 0 && args[0] is float value ? value : 0f;
        return op switch
        {
            BriefingOp.PlaySound => new BriefingStep
            {
                Op = op,
                Id = d.Str("sound") ?? string.Empty,
                To = d.Float("vol", 1f),
                Index = (int)d.Float("startmarker"),
            },
            BriefingOp.WaitForMarker => new BriefingStep { Op = op, Index = (int)bare },
            BriefingOp.Wait => new BriefingStep { Op = op, Duration = bare },
            BriefingOp.Pict => new BriefingStep
            {
                Op = op,
                Id = id,
                Bitmap = d.Str("bitmap") ?? string.Empty,
                At = new BriefingPoint(d.Float("at"), d.Float("at", 0f, 1)),
                Center = string.Equals(d.Str("center"), "true", StringComparison.OrdinalIgnoreCase),
            },
            BriefingOp.Fade => new BriefingStep
            {
                Op = op,
                Id = id,
                From = d.Float("start"),
                To = d.Float("end"),
                Duration = d.Float("duration"),
            },
            BriefingOp.Spin => new BriefingStep
            {
                Op = op,
                Id = id,
                From = d.Float("startrevs"),
                To = d.Float("revs"),
                Duration = d.Float("duration"),
            },
            BriefingOp.Move => new BriefingStep
            {
                Op = op,
                Id = id,
                Path = Points(d.List("path")),
                Duration = d.Float("duration"),
            },
            BriefingOp.Line => new BriefingStep
            {
                Op = op,
                Id = id,
                Path = Points(d.List("points")),
                Color = new BriefingColor(
                    (byte)d.Float("color"), (byte)d.Float("color", 0f, 1), (byte)d.Float("color", 0f, 2)),
            },
            BriefingOp.Cycle => new BriefingStep
            {
                Op = op,
                Id = id,
                Bitmap = Names(d.List("bitmaps")) is { Count: > 0 } frames ? frames[0] : string.Empty,
                Frames = Names(d.List("bitmaps")),
                Fps = d.Float("fps"),
                At = new BriefingPoint(d.Float("at"), d.Float("at", 0f, 1)),
            },
            BriefingOp.Objective => new BriefingStep { Op = op, Id = id, Index = (int)d.Float("index") },
            _ => new BriefingStep { Op = op, Id = id },
        };
    }

    // A bitmap list as its names, skipping anything that is not one.
    private static IReadOnlyList<string> Names(List<object?>? list)
    {
        if (list == null)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>(list.Count);
        foreach (var entry in list)
        {
            if (entry is string name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static IReadOnlyList<BriefingPoint> Points(List<object?>? list)
    {
        if (list == null)
        {
            return Array.Empty<BriefingPoint>();
        }

        var points = new List<BriefingPoint>();
        foreach (var entry in list)
        {
            if (entry is List<object?> { Count: >= 2 } pair && pair[0] is float x && pair[1] is float y)
            {
                points.Add(new BriefingPoint(x, y));
            }
        }

        return points;
    }
}

/// <summary>One element the reveal has placed on the map: a flag pin, a photograph, the objectives
/// note. The interpreter owns the mutation; a renderer reads the current values.</summary>
public sealed class BriefingElement
{
    /// <summary>Binds a fresh element to its script id.</summary>
    public BriefingElement(string id) => Id = id;

    /// <summary>The script's own name for it (<c>OBJPIN1</c>, <c>ZEPTEXT1</c>, …).</summary>
    public string Id { get; }

    /// <summary>The picture it draws, or "" for a text or chrome element.</summary>
    public string Bitmap { get; internal set; } = string.Empty;

    /// <summary>Where it sits now, after any move.</summary>
    public BriefingPoint At { get; internal set; }

    /// <summary>Whether <see cref="At"/> is its middle rather than its top left.</summary>
    public bool Center { get; internal set; }

    /// <summary>A connector line's two endpoints, empty for a picture.</summary>
    public IReadOnlyList<BriefingPoint> Points { get; internal set; } = Array.Empty<BriefingPoint>();

    /// <summary>A connector line's colour.</summary>
    public BriefingColor Color { get; internal set; }

    /// <summary>Whether an <see cref="BriefingOp.On"/> has shown it and no
    /// <see cref="BriefingOp.Off"/> has taken it away.</summary>
    public bool Visible { get; internal set; }

    /// <summary>Current opacity, 0 to 1.</summary>
    public float Opacity { get; internal set; } = 1f;

    /// <summary>Current rotation in revolutions.</summary>
    public float Revs { get; internal set; }

    /// <summary>Whether <see cref="BriefingOp.ToBack"/> pushed it behind the rest.</summary>
    public bool Back { get; internal set; }

    /// <summary>The objectives-list entry it shows, or -1 when it is not an objective line.</summary>
    public int ObjectiveIndex { get; internal set; } = -1;

    /// <summary>A cycling element's whole frame list, empty for everything else. The load screen's
    /// propeller is the only one any shipped script authors.</summary>
    public IReadOnlyList<string> Frames { get; internal set; } = Array.Empty<string>();

    /// <summary>A cycling element's authored rate in frames a second.</summary>
    public float Fps { get; internal set; }
}

/// <summary>
/// One mission state's reveal, run against a clock the caller advances. The script is a linear
/// beat sheet: it blocks on the narration's cue points and on authored waits, and everything
/// between them (fades, spins, moves, the flag pins and their objectives-note lines) is authored
/// data. Engine-free, so the beat order, the marker gating and the objective binding test off
/// engine and run identically whether or not the narration is actually sounding.
///
/// <para>With no cue points the reveal degrades rather than inventing timings: every
/// <see cref="BriefingOp.WaitForMarker"/> releases at once, so the map finishes immediately and
/// the narration plays over the completed map.</para>
/// </summary>
public sealed class BriefingReveal
{
    private readonly IReadOnlyList<BriefingStep> _steps;
    private readonly IReadOnlyList<double> _markers;
    private readonly Dictionary<string, BriefingElement> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BriefingElement> _elements = new();
    private readonly List<Tween> _tweens = new();
    private readonly List<int> _revealed = new();
    private int _pc;
    private double _clock;
    private double _resumeAt;

    /// <summary>Binds a script to the narration cue times it waits on, both in ascending seconds.
    /// An empty <paramref name="markers"/> is the degraded mode described above.</summary>
    public BriefingReveal(IReadOnlyList<BriefingStep> steps, IReadOnlyList<double> markers)
    {
        _steps = steps;
        _markers = markers;
        Run();
    }

    private enum TweenKind
    {
        Fade,
        Spin,
        Move,
    }

    /// <summary>Seconds since the narration started.</summary>
    public double Clock => _clock;

    /// <summary>Whether every beat has run and every tween has landed.</summary>
    public bool Complete => _pc >= _steps.Count && _tweens.Count == 0;

    /// <summary>The narration the script asked for, or "" before <see cref="BriefingOp.PlaySound"/>
    /// has run. This is the state's own sound name, never one computed from the mission.</summary>
    public string Narration { get; private set; } = string.Empty;

    /// <summary>How many times the narration has been asked for, which a shell watches to know it
    /// must (re)start playback: REPLAY BRIEFING raises it.</summary>
    public int NarrationStarts { get; private set; }

    /// <summary>The marker the script is waiting on, or -1 when it is not blocked on one.</summary>
    public int BlockedOnMarker { get; private set; } = -1;

    /// <summary>Every element the script has placed, in the order it placed them, which is the
    /// draw order: the photographs a briefing stacks over its map are never turned off, and each
    /// new one sits over the last. An element with <see cref="BriefingElement.Back"/> set draws
    /// behind the rest.</summary>
    public IReadOnlyList<BriefingElement> Elements => _elements;

    /// <summary>The objectives-list entries revealed so far, in reveal order.</summary>
    public IReadOnlyList<int> RevealedObjectives => _revealed;

    /// <summary>Moves the clock on and runs whatever beats that releases.</summary>
    public void Advance(double seconds)
    {
        if (seconds > 0)
        {
            _clock += seconds;
        }

        Run();
    }

    /// <summary>REPLAY BRIEFING: back to a blank map with the narration starting over.</summary>
    public void Restart()
    {
        _byId.Clear();
        _elements.Clear();
        _tweens.Clear();
        _revealed.Clear();
        _pc = 0;
        _clock = 0;
        _resumeAt = 0;
        BlockedOnMarker = -1;
        Narration = string.Empty;
        Run();
    }

    /// <summary>One element by script id, or null when the script has not placed it.</summary>
    public BriefingElement? Element(string id) => _byId.TryGetValue(id, out var e) ? e : null;

    // A move walks its path with the duration split evenly between segments, which is the only
    // reading the data supports: a path carries points and one duration, never per-leg times.
    private static void Apply(Tween tween, float t)
    {
        var step = tween.Step;
        switch (tween.Kind)
        {
            case TweenKind.Fade:
                tween.Target.Opacity = step.From + ((step.To - step.From) * t);
                break;
            case TweenKind.Spin:
                tween.Target.Revs = step.From + ((step.To - step.From) * t);
                break;
            case TweenKind.Move:
                tween.Target.At = Along(step.Path, t);
                break;
        }
    }

    private static BriefingPoint Along(IReadOnlyList<BriefingPoint> path, float t)
    {
        if (path.Count == 1)
        {
            return path[0];
        }

        float scaled = t * (path.Count - 1);
        int leg = Math.Min((int)scaled, path.Count - 2);
        float within = scaled - leg;
        var from = path[leg];
        var to = path[leg + 1];
        return new BriefingPoint(
            from.X + ((to.X - from.X) * within), from.Y + ((to.Y - from.Y) * within));
    }

    // Executes beats until one blocks, then settles the tweens at the current clock.
    private void Run()
    {
        while (_pc < _steps.Count && _clock >= _resumeAt)
        {
            BlockedOnMarker = -1;
            var step = _steps[_pc];
            if (step.Op == BriefingOp.WaitForMarker)
            {
                double? at = Marker(step.Index);
                _pc++;
                if (at is { } time && time > _clock)
                {
                    _resumeAt = time;
                    BlockedOnMarker = step.Index;
                }

                continue;
            }

            if (step.Op == BriefingOp.Wait)
            {
                _pc++;
                _resumeAt = _clock + step.Duration;
                continue;
            }

            Execute(step);
            _pc++;
        }

        Settle();
    }

    // A marker's time, or null when the wav carries no such cue point (the degraded mode: the
    // beat releases at once rather than waiting on a timing nobody measured).
    private double? Marker(int index) =>
        index >= 0 && index < _markers.Count ? _markers[index] : null;

    private void Execute(BriefingStep step)
    {
        switch (step.Op)
        {
            case BriefingOp.PlaySound:
                Narration = step.Id;
                NarrationStarts++;
                break;
            // A cycle's first frame is placed exactly as a Pict is, its own point being the
            // bitmap's top left: the load screen authors no center flag on one.
            case BriefingOp.Pict:
            case BriefingOp.Cycle:
                var placed = Ensure(step.Id);
                placed.Bitmap = step.Bitmap;
                placed.At = step.At;
                placed.Center = step.Center;
                placed.Frames = step.Frames;
                placed.Fps = step.Fps;
                break;
            case BriefingOp.Line:
                var line = Ensure(step.Id);
                line.Points = step.Path;
                line.Color = step.Color;
                if (step.Path.Count > 0)
                {
                    line.At = step.Path[0];
                }

                break;
            case BriefingOp.Fade:
                var faded = Ensure(step.Id);
                faded.Opacity = step.From;
                Tweens(faded, TweenKind.Fade, step);
                break;
            case BriefingOp.Spin:
                var spun = Ensure(step.Id);
                spun.Revs = step.From;
                Tweens(spun, TweenKind.Spin, step);
                break;
            case BriefingOp.Move:
                var moved = Ensure(step.Id);
                if (step.Path.Count > 0)
                {
                    // The path's first point wins over the Pict position when they disagree, which
                    // is what the screen shows where a state authors both.
                    moved.At = step.Path[0];
                    Tweens(moved, TweenKind.Move, step);
                }

                break;
            case BriefingOp.On:
                var shown = Ensure(step.Id);
                shown.Visible = true;
                if (shown.ObjectiveIndex >= 0 && !_revealed.Contains(shown.ObjectiveIndex))
                {
                    _revealed.Add(shown.ObjectiveIndex);
                }

                break;
            case BriefingOp.Off:
                var hidden = Ensure(step.Id);
                hidden.Visible = false;
                _revealed.Remove(hidden.ObjectiveIndex);
                break;
            case BriefingOp.Objective:
                Ensure(step.Id).ObjectiveIndex = step.Index;
                break;
            case BriefingOp.ToBack:
                Ensure(step.Id).Back = true;
                break;
        }
    }

    private void Tweens(BriefingElement target, TweenKind kind, BriefingStep step)
    {
        _tweens.RemoveAll(t => t.Kind == kind && ReferenceEquals(t.Target, target));
        if (step.Duration > 0)
        {
            _tweens.Add(new Tween(target, kind, step, _clock));
        }
        else
        {
            Apply(new Tween(target, kind, step, _clock), 1f);
        }
    }

    private void Settle()
    {
        for (int i = _tweens.Count - 1; i >= 0; i--)
        {
            var tween = _tweens[i];
            float t = (float)Math.Clamp((_clock - tween.Start) / tween.Step.Duration, 0d, 1d);
            Apply(tween, t);
            if (t >= 1f)
            {
                _tweens.RemoveAt(i);
            }
        }
    }

    private BriefingElement Ensure(string id)
    {
        if (!_byId.TryGetValue(id, out var element))
        {
            element = new BriefingElement(id);
            _byId[id] = element;
            _elements.Add(element);
        }

        return element;
    }

    private sealed record Tween(BriefingElement Target, TweenKind Kind, BriefingStep Step, double Start);
}

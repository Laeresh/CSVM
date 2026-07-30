using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// The project's diagnostic log: one call shape, a fixed category vocabulary, four levels, and
/// two sinks with different jobs.
///
/// <para><b>Console</b> is the human's view and stays quiet by default: errors and warnings
/// always, info as well (that is what an unconverted <c>GD.Print</c> did, so converting a site
/// changes nothing you see), and debug only for the categories <c>--log=</c> names.</para>
///
/// <para><b>The file sink</b> is the machine's view and always takes <i>everything</i>, at every
/// level, in every category, to <c>.scratch/logs/&lt;mode&gt;-&lt;timestamp&gt;.log</c>. That is
/// the whole point: a post-hoc grep can never miss a category nobody thought to enable before the
/// run. It is line-flushed, so a run that crashes still leaves every line it had written.</para>
///
/// <para><b>Grammar.</b> Every line ends in <c>[cat] message key=value …</c> — the file prefixes
/// a fixed-width level token, the console prefixes one only for warnings and errors. There is no
/// timestamp column on purpose: a deterministic run must produce a byte-identical log, so a line
/// that needs time carries it as an explicit <c>key=value</c>. Messages are interpolated strings
/// rendered with <see cref="CultureInfo.InvariantCulture"/>, so a float reads <c>16.667</c> on
/// every machine — a German locale would otherwise turn an XYZ triple into six ambiguous numbers.</para>
/// </summary>
public static class Log
{
    /// <summary>The category vocabulary. A category is a subsystem an investigator would want to
    /// turn up on its own; <c>ui</c> covers the launchscreen and the labs, whose state dumps a
    /// user reads on purpose and should be able to silence without silencing the session spine
    /// (<c>core</c>).</summary>
    public static readonly string[] Categories =
    {
        "anim", "world", "flight", "weapons", "sound", "perf", "test", "ui", "core",
    };

    // Console default: info and above. Warnings and errors ignore this entirely (below).
    private const Level DefaultThreshold = Level.Info;

    // Lines logged before the sink opens are held here and written the moment it does. Bounded,
    // because "never buffer in memory" is the rule the sink exists to honour — this covers only
    // the few milliseconds of startup before the repo root and the session mode are known.
    private const int PreludeCap = 512;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Level> Thresholds = new();
    private static readonly List<string> Prelude = new();
    private static readonly List<string> UnknownCategories = new();

    private static Level _threshold = DefaultThreshold;
    private static StreamWriter? _sink;
    private static bool _preludeOverflowed;

    public enum Level
    {
        Error = 0,
        Warn = 1,
        Info = 2,
        Debug = 3,
    }

    /// <summary>The open log file's absolute path, or null before <see cref="Open"/>.</summary>
    public static string? SinkPath { get; private set; }

    /// <summary>Applies a <c>--log=</c> filter spec: comma-separated <c>cat</c>,
    /// <c>cat:level</c>, <c>*</c>, <c>*:level</c> or a bare <c>level</c>. A bare category means
    /// "turn it up to debug"; a bare level sets every category. Pure — it touches no Godot API,
    /// so it is callable from a test host; an unknown category is kept (never dropped) and
    /// reported when the sink opens.</summary>
    public static void Configure(string spec)
    {
        foreach (string raw in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string token = raw.Trim();
            if (token.Length == 0)
            {
                continue;
            }
            int colon = token.IndexOf(':');
            string cat = colon < 0 ? token : token[..colon];
            string levelText = colon < 0 ? "" : token[(colon + 1)..];

            // A bare token that names a level is the whole-session form: --log=debug == --log=*:debug.
            if (colon < 0 && ParseLevel(token) is { } bare)
            {
                _threshold = bare;
                continue;
            }
            Level level = (levelText.Length == 0 ? Level.Debug : ParseLevel(levelText)) ?? Level.Debug;
            if (cat == "*")
            {
                _threshold = level;
                Thresholds.Clear();
                continue;
            }
            if (Array.IndexOf(Categories, cat) < 0 && !UnknownCategories.Contains(cat))
            {
                UnknownCategories.Add(cat);
            }
            Thresholds[cat] = level;
        }
    }

    /// <summary>Opens the always-on file sink and flushes anything logged before it existed.
    /// One sink per process; a second call is a no-op.</summary>
    /// <param name="repoRoot">Repo root — the log lands in its <c>.scratch/logs/</c>.</param>
    /// <param name="mode">The session shape (<c>fly</c>, <c>freecam</c>, …), used in the filename.</param>
    public static void Open(string repoRoot, string mode)
    {
        if (_sink != null)
        {
            return;
        }
        string dir = Path.Combine(repoRoot, ".scratch", "logs");
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string path = Path.Combine(dir, $"{mode}-{stamp}.log");
        try
        {
            Directory.CreateDirectory(dir);
            // Two sessions started in the same second collide, and on Windows the first one still
            // holds the file — the PID makes the second run's name unique either way.
            if (File.Exists(path))
            {
                path = Path.Combine(dir, $"{mode}-{stamp}-{System.Environment.ProcessId}.log");
            }
            StreamWriter writer;
            try
            {
                writer = OpenWriter(path);
            }
            catch (IOException)
            {
                path = Path.Combine(dir, $"{mode}-{stamp}-{System.Environment.ProcessId}.log");
                writer = OpenWriter(path);
            }
            lock (Gate)
            {
                _sink = writer;
                SinkPath = path;
                foreach (string line in Prelude)
                {
                    writer.WriteLine(line);
                }
                Prelude.Clear();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"ERROR [core] log sink unavailable dir={dir} error={Describe(e)}");
            return;
        }

        Info("core", $"log file={path} mode={mode} console={DescribeFilter()}");
        if (_preludeOverflowed)
        {
            Warn("core", $"log prelude overflowed cap={PreludeCap} — earlier lines reached the console only");
        }
        foreach (string cat in UnknownCategories)
        {
            Warn("core", $"log filter names an unknown category cat={cat} known={string.Join(",", Categories)}");
        }
    }

    public static void Error(string cat, FormattableString message) => Emit(Level.Error, cat, Format(message), null);

    /// <summary>An error with its exception: the console gets type and message, the file also
    /// gets the full stack trace.</summary>
    public static void Error(string cat, FormattableString message, Exception e) =>
        Emit(Level.Error, cat, $"{Format(message)} error={Describe(e)}", e.ToString());

    public static void Warn(string cat, FormattableString message) => Emit(Level.Warn, cat, Format(message), null);

    public static void Info(string cat, FormattableString message) => Emit(Level.Info, cat, Format(message), null);

    public static void Debug(string cat, FormattableString message) => Emit(Level.Debug, cat, Format(message), null);

    /// <summary>Writes an already-formatted block verbatim to both sinks — the multi-line
    /// <c>StringBuilder</c> reports (<c>--dump-*</c>) that do not fit a one-line grammar.</summary>
    public static void Raw(string text)
    {
        GD.Print(text);
        WriteFile(text);
    }

    /// <summary>Would this line reach the console? The file sink takes everything regardless, so
    /// a per-frame diagnostic must still gate itself at the call site (its own <c>--debug-*</c>
    /// flag) rather than relying on this.</summary>
    public static bool ConsoleShows(string cat, Level level)
    {
        if (level <= Level.Warn)
        {
            return true; // errors and warnings are not suppressible
        }
        return level <= (Thresholds.TryGetValue(cat, out var t) ? t : _threshold);
    }

    /// <summary>The canonical file line for a message — the console line with its level token.</summary>
    public static string FileLine(Level level, string cat, string message) =>
        $"{Tag(level)} [{cat}] {message}";

    /// <summary>Renders an interpolated message exactly as every logging call does: invariant
    /// culture, so a float reads <c>16.667</c> and never <c>16,667</c>. Pure — no Godot API.
    /// <b>A message must be ONE interpolated string.</b> Concatenating two (<c>$"a{x}" + $"b{y}"</c>)
    /// produces a <c>string</c>, which will not compile against these overloads — deliberately, since
    /// the concatenation would have already formatted its floats in the current culture.</summary>
    public static string Format(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    private static void Emit(Level level, string cat, string message, string? detail)
    {
        if (ConsoleShows(cat, level))
        {
            switch (level)
            {
                case Level.Error:
                    GD.PrintErr($"ERROR [{cat}] {message}");
                    break;
                case Level.Warn:
                    // Plain, not GD.PushWarning: Godot .NET appends a managed stack trace to
                    // every pushed warning, which buries the message it is meant to surface.
                    GD.Print($"WARN [{cat}] {message}");
                    break;
                default:
                    GD.Print($"[{cat}] {message}");
                    break;
            }
        }
        WriteFile(FileLine(level, cat, message));
        if (detail != null)
        {
            WriteFile(detail);
        }
    }

    private static void WriteFile(string line)
    {
        lock (Gate)
        {
            if (_sink == null)
            {
                if (Prelude.Count < PreludeCap)
                {
                    Prelude.Add(line);
                }
                else
                {
                    _preludeOverflowed = true;
                }
                return;
            }
            try
            {
                _sink.WriteLine(line);
            }
            catch (IOException e)
            {
                // A dead sink must never take the run with it — drop it and say so once.
                _sink = null;
                SinkPath = null;
                GD.PrintErr($"ERROR [core] log sink write failed — file logging off error={Describe(e)}");
            }
        }
    }

    // AutoFlush is the crash-safety contract: every line reaches the OS before the next one is
    // built, so a killed or throwing run still leaves everything it had logged. The UTF-8 BOM is
    // deliberate: Windows PowerShell 5.1 — the shell every tool here runs in — decodes a
    // BOM-less file as the ANSI codepage, which mangles the em dashes and box glyphs the
    // messages carry into mojibake before a grep ever sees them.
    private static StreamWriter OpenWriter(string path) =>
        new(new FileStream(path, FileMode.Create, System.IO.FileAccess.Write, FileShare.Read), new UTF8Encoding(true))
        {
            AutoFlush = true,
        };

    private static string Describe(Exception e) => $"{e.GetType().Name}: {e.Message}";

    private static string Tag(Level level) => level switch
    {
        Level.Error => "ERROR",
        Level.Warn => "WARN ",
        Level.Info => "INFO ",
        _ => "DEBUG",
    };

    private static Level? ParseLevel(string text) => text.ToLowerInvariant() switch
    {
        "error" => Level.Error,
        "warn" => Level.Warn,
        "info" => Level.Info,
        "debug" => Level.Debug,
        _ => null,
    };

    private static string DescribeFilter()
    {
        var parts = new List<string> { $"*:{Tag(_threshold).Trim().ToLowerInvariant()}" };
        foreach (var kv in Thresholds)
        {
            parts.Add($"{kv.Key}:{Tag(kv.Value).Trim().ToLowerInvariant()}");
        }
        return string.Join(",", parts);
    }
}

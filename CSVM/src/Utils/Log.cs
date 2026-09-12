using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// The project's diagnostic log: one call shape, a fixed category vocabulary, four levels, and
/// two sinks with different jobs. Console is the human's view and stays quiet by default; the
/// file sink is the machine's view and always takes everything, at every level, in every
/// category, line-flushed so a crash still leaves what was written. Grammar, the category list
/// and the --log= filter: docs/org/logging.md.
/// ⚠ Messages are interpolated strings rendered with <see cref="CultureInfo.InvariantCulture"/>,
/// so a float reads <c>16.667</c> on every machine, never the current-culture form.
/// ⚠ Migration off the remaining <c>GD.Print</c> call sites is incremental by decision, a
/// family converts when an item touches it, never a bulk sweep.
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
    // because "never buffer in memory" is the rule the sink exists to honour, this covers only
    // the few milliseconds of startup before the repo root and the session mode are known.
    private const int PreludeCap = 512;

    // The SCOPED console sink, one value per execution flow (see PushConsoleSink). Deliberately
    // not the same storage as ConsoleSink below: that one is the process-wide default, and it has
    // to be, because CSVM.Tests installs it from a [ModuleInitializer], an AsyncLocal written
    // there is invisible on the threads xunit later runs tests on, so folding the two tiers into
    // one would drop every test back onto the host-killing GD.Print fallthrough.
    private static readonly AsyncLocal<Action<string>?> ScopedSink = new();

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

    /// <summary>The PROCESS-WIDE default for console lines; null (the default) means
    /// <c>GD.Print</c> / <c>GD.PrintErr</c>. A test host installs one once, so that a plain class
    /// that logs is callable without an engine. To capture lines and assert on them, use
    /// <see cref="PushConsoleSink"/> instead, this one is shared by every thread in the process.
    /// The file sink is untouched by this, it always takes everything regardless.</summary>
    public static Action<string>? ConsoleSink { get; set; }

    /// <summary>Routes this execution flow's console lines to <paramref name="sink"/> until the
    /// returned handle is disposed, then restores whatever this flow had before. Scopes nest, and
    /// disposing twice does nothing. Per-flow, not global, so a concurrent flow cannot steal or
    /// add to these lines.
    /// ⚠ A thread this flow spawns does not inherit the scope unless it captures the execution
    /// context; its lines go to <see cref="ConsoleSink"/> instead.</summary>
    public static IDisposable PushConsoleSink(Action<string> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        return new ConsoleSinkScope(sink);
    }

    /// <summary>Applies a <c>--log=</c> filter spec: comma-separated <c>cat</c>,
    /// <c>cat:level</c>, <c>*</c>, <c>*:level</c> or a bare <c>level</c>. A bare category means
    /// "turn it up to debug"; a bare level sets every category. Pure, it touches no Godot API,
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

    /// <summary>The directory the file sink writes into, and the one place that decides it. A repo
    /// run uses the git-ignored <c>.scratch/logs/</c> the verification toolchain reads. An exported
    /// build uses a plain <c>logs/</c> beside the exe, since a recipient must find their own log.
    /// Pure, so both branches are assertable without an engine.
    /// ⚠ Do not point a development tree at <c>logs/</c>: <c>.gitignore</c> covers <c>.scratch/</c>,
    /// so a new top-level directory would be committed.</summary>
    public static string DirectoryFor(string root, bool exported) =>
        exported ? Path.Combine(root, "logs") : Path.Combine(root, ".scratch", "logs");

    /// <summary>Opens the always-on file sink and flushes anything logged before it existed.
    /// One sink per process; a second call is a no-op.</summary>
    /// <param name="root">The run's root: the repo checkout, or the exe's own folder.</param>
    /// <param name="mode">The session shape (<c>fly</c>, <c>freecam</c>, …), used in the filename.</param>
    /// <param name="version">The build's version (<see cref="BuildVersion.Current"/>), written as the file's first line.</param>
    /// <param name="exported">An exported build, which logs where <see cref="DirectoryFor"/> says.</param>
    public static void Open(string root, string mode, string version, bool exported)
    {
        if (_sink != null)
        {
            return;
        }
        string dir = DirectoryFor(root, exported);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string path = Path.Combine(dir, $"{mode}-{stamp}.log");
        try
        {
            Directory.CreateDirectory(dir);
            // Two sessions started in the same second collide, and on Windows the first one still
            // holds the file, the PID makes the second run's name unique either way.
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
                // The FIRST line in the file, ahead of the prelude: a report arrives as an
                // attached log, and which build wrote it has to be readable from the top. Written
                // straight to the writer, since Emit would queue it after the prelude.
                writer.WriteLine(FileLine(Level.Info, "core", $"csvm version={version}"));
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

    /// <summary>Writes an already-formatted block verbatim to both sinks, the multi-line
    /// <c>StringBuilder</c> reports (<c>--dump-*</c>) that do not fit a one-line grammar.</summary>
    public static void Raw(string text)
    {
        WriteConsole(text, isError: false);
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

    /// <summary>The canonical file line for a message, the console line with its level token.
    /// No timestamp column, deliberately: a <c>--det</c> run must produce a byte-identical log,
    /// so a line that needs the time carries it as an explicit <c>key=value</c>.</summary>
    public static string FileLine(Level level, string cat, string message) =>
        $"{Tag(level)} [{cat}] {message}";

    /// <summary>Renders an interpolated message exactly as every logging call does: invariant
    /// culture, so a float reads <c>16.667</c> and never <c>16,667</c>. Pure, no Godot API.
    /// ⚠ Pass one interpolated string, never a concatenation of two (<c>$"a{x}" + $"b{y}"</c>):
    /// that produces a plain <c>string</c>, which will not compile against these overloads.</summary>
    public static string Format(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    private static void Emit(Level level, string cat, string message, string? detail)
    {
        if (ConsoleShows(cat, level))
        {
            switch (level)
            {
                case Level.Error:
                    WriteConsole($"ERROR [{cat}] {message}", isError: true);
                    break;
                case Level.Warn:
                    // Plain, not GD.PushWarning: Godot .NET appends a managed stack trace to
                    // every pushed warning, which buries the message it is meant to surface.
                    WriteConsole($"WARN [{cat}] {message}", isError: false);
                    break;
                default:
                    WriteConsole($"[{cat}] {message}", isError: false);
                    break;
            }
        }
        WriteFile(FileLine(level, cat, message));
        if (detail != null)
        {
            WriteFile(detail);
        }
    }

    // The console half of a line, resolved in three tiers: this flow's scoped sink, else the
    // process-wide default, else the engine. A sink, once found, replaces GD.Print/GD.PrintErr
    // entirely, the caller who set it decides what "console" means, including dropping the
    // Error/non-Error distinction if it wants one sink for everything.
    private static void WriteConsole(string line, bool isError)
    {
        Action<string>? sink = ScopedSink.Value ?? ConsoleSink;
        if (sink != null)
        {
            sink(line);
            return;
        }
        if (isError)
        {
            GD.PrintErr(line);
        }
        else
        {
            GD.Print(line);
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
                // A dead sink must never take the run with it, drop it and say so once.
                _sink = null;
                SinkPath = null;
                GD.PrintErr($"ERROR [core] log sink write failed — file logging off error={Describe(e)}");
            }
        }
    }

    // AutoFlush is the crash-safety contract: every line reaches the OS before the next one is
    // built, so a killed or throwing run still leaves everything it had logged. The UTF-8 BOM is
    // deliberate: Windows PowerShell 5.1, the shell every tool here runs in, decodes a
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

    private sealed class ConsoleSinkScope : IDisposable
    {
        private readonly Action<string>? _previous;
        private bool _popped;

        internal ConsoleSinkScope(Action<string> sink)
        {
            _previous = ScopedSink.Value;
            ScopedSink.Value = sink;
        }

        // Restores the enclosing SCOPE, not null, nested scopes have to compose, and the
        // process-wide ConsoleSink is a different tier that a scope must never touch.
        public void Dispose()
        {
            if (_popped)
            {
                return;
            }
            _popped = true;
            ScopedSink.Value = _previous;
        }
    }
}

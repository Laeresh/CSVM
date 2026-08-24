using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>How one suite ended. <see cref="Skip"/> is reported separately from
/// <see cref="Pass"/> on purpose: "the data was not there" must never read as "the check
/// held".</summary>
public enum SuiteStatus
{
    Pass,
    Fail,
    Skip,
}

/// <summary>
/// The in-engine test harness behind <c>--run-tests[=filter]</c>: a registry of assertion suites,
/// a PASS/FAIL/SKIP table, a <c>.scratch/test-report.json</c>, and a nonzero exit code on failure.
/// Scope and error policy: this module's entry in docs/architecture.md. Windowed-run rule:
/// docs/verification.md LOG-8.
/// ⚠ In-engine is the SMALLER half. Only a check that needs a live Godot belongs here; anything
/// that runs without the engine belongs in <c>CSVM.Tests</c> (<c>dotnet test</c>) instead.
/// </summary>
public static class TestHarness
{
    /// <summary>The engine errors this project currently emits that are not the harness's to fix.
    /// Every entry names the open item that owns it; when that item lands, the entry is deleted and
    /// the cap does the rest.</summary>
    public static readonly IReadOnlyList<ErrorAllowance> ErrorAllowlist = new[]
    {
        // Godot's own Basis::invert guard in the destructible death path: a print-and-return C++
        // macro, not a managed throw, so the run completes and every later row still reports.
        // Cap set just above the worst measured chapter (C2 4) so a new source still trips it.
        new ErrorAllowance(@"Condition ""det == 0"" is true\.", 8,
            "pre-existing singular-basis guard in the destructible death path (backlog: det == 0 invert error)"),
        // A one-shot SOUND event whose anchor is read for its world position during the animation
        // bootstrap, before the subtree is in the tree. Measured: C3 1, every other chapter 0.
        new ErrorAllowance(@"Condition ""!is_inside_tree\(\)"" is true", 4,
            "pre-existing bootstrap sound-position read on an out-of-tree anchor (backlog: C3 sound bind)"),
    };

    // The engine's own error format (print_error): "ERROR: …", "SCRIPT ERROR: …", "USER ERROR: …".
    // Log's own error lines read "ERROR [cat] …" — no colon — and are deliberately out of scope
    // here, because the suite that emitted one has already failed on it.
    private static readonly Regex EngineError = new(@"^(ERROR|SCRIPT ERROR|USER ERROR): ", RegexOptions.Compiled);

    private static readonly List<Suite> Registry = new();

    public static IReadOnlyList<Suite> All
    {
        get
        {
            if (Registry.Count == 0)
            {
                SuiteCatalog.RegisterAll(Registry);
            }
            return Registry;
        }
    }

    /// <summary>Runs every registered suite whose name contains <paramref name="filter"/> (empty =
    /// all), prints the table, writes <c>.scratch/test-report.json</c>, and returns the process exit
    /// code: 0 when nothing failed, 1 otherwise. A skipped suite is not a failure.</summary>
    public static int Run(TestContext ctx, string filter)
    {
        // Numbers in a committed report must read the same on every machine.
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        var selected = All.Where(s => filter.Length == 0
            || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        var results = new List<SuiteResult>();
        Log.Info("test", $"run-tests suites={selected.Count}/{All.Count} filter='{filter}' chapter={ctx.Chapter} mission={ctx.Mission}");
        if (selected.Count == 0)
        {
            Log.Error("test", $"run-tests filter '{filter}' matched no suite of {string.Join(", ", All.Select(s => s.Name))}");
            return 1;
        }

        foreach (var suite in selected)
        {
            ctx.Failures.Clear();
            ctx.Notes.Clear();
            ctx.Counts.Clear();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SuiteStatus status;
            string detail = suite.What;
            try
            {
                suite.Body(ctx);
                status = ctx.Failures.Count == 0 ? SuiteStatus.Pass : SuiteStatus.Fail;
            }
            catch (SuiteSkippedException e)
            {
                status = SuiteStatus.Skip;
                detail = e.Message;
            }
            catch (Exception e)
            {
                status = SuiteStatus.Fail;
                detail = $"{e.GetType().Name}: {e.Message}";
                ctx.Failures.Add(detail);
                Log.Error("test", $"suite threw name={suite.Name}", e);
            }
            watch.Stop();
            results.Add(new SuiteResult
            {
                Name = suite.Name,
                Status = status,
                Seconds = watch.Elapsed.TotalSeconds,
                Detail = detail,
                Failures = ctx.Failures.ToList(),
                Notes = ctx.Notes.ToList(),
                Counts = new Dictionary<string, long>(ctx.Counts),
            });
            Log.Info("test", $"suite {suite.Name} {status.ToString().ToUpperInvariant()} in {watch.Elapsed.TotalSeconds:0.00}s");
        }
        ctx.ReleaseWorlds();

        var screen = ScreenEngineLog(out string? logPath);
        int pass = results.Count(r => r.Status == SuiteStatus.Pass);
        int fail = results.Count(r => r.Status == SuiteStatus.Fail);
        int skip = results.Count(r => r.Status == SuiteStatus.Skip);
        // An unknown error fails the run; an over-cap allowance fails it; no log SKIPs the engine
        // errors row — it never counts as a pass, but it never fails the run either.
        bool screenFailed = screen is { Ok: false };

        Log.Raw(FormatTable(results, screen, logPath));
        WriteReport(ctx, results, screen, logPath);
        string errors = screen == null ? "unscreened" : screen.Ok ? "clean" : "UNEXPECTED";
        Log.Info("test", $"run-tests pass={pass} fail={fail} skip={skip} errors={errors}");
        return fail > 0 || screenFailed ? 1 : 0;
    }

    /// <summary>Classifies a run's log lines: how many engine error lines there were, how many the
    /// allowlist covers, which are unknown, and which allowed pattern went over its cap. Pure — no
    /// Godot API, no file access — so it is unit-testable outside the engine.</summary>
    public static StderrScreen Screen(IEnumerable<string> lines)
    {
        var counts = new Dictionary<string, int>();
        var unexpected = new List<string>();
        int total = 0, allowed = 0;
        string? pending = null;   // the previous error line, so its "   at: …" location joins it
        foreach (string line in lines)
        {
            if (pending != null && line.StartsWith("   at: ", StringComparison.Ordinal))
            {
                unexpected[^1] = $"{pending} {line.Trim()}";
                pending = null;
                continue;
            }
            pending = null;
            if (!EngineError.IsMatch(line))
            {
                continue;
            }
            total++;
            var match = ErrorAllowlist.FirstOrDefault(a => Regex.IsMatch(line, a.Pattern));
            if (match != null)
            {
                counts.TryGetValue(match.Pattern, out int n);
                counts[match.Pattern] = n + 1;
                allowed++;
                continue;
            }
            unexpected.Add(line);
            pending = line;
        }
        var overCap = new List<string>();
        foreach (var allowance in ErrorAllowlist)
        {
            counts.TryGetValue(allowance.Pattern, out int n);
            if (n > allowance.Max)
            {
                overCap.Add($"{allowance.Pattern} seen {n}x, allowed {allowance.Max}x — {allowance.Why}");
            }
        }
        return new StderrScreen
        {
            Total = total,
            Allowed = allowed,
            Unexpected = unexpected,
            OverCap = overCap,
            AllowedCounts = counts,
        };
    }

    // The engine log Godot's `--log-file` is writing, or null when launched without it. Error
    // lines are flushed as printed, so reading it while the process holds it open is sound.
    // ⚠ Read the command line directly, not `OS.GetCmdlineArgs()`: Godot hands that back only
    // args its own parser did not recognise, and `--log-file` is one it consumes. Falls back to
    // the project's own rotating log (desktop default) if the flag is absent.
    private static string? EngineLogPath()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--log-file")
            {
                return args[i + 1];
            }
        }
        string setting = ProjectSettings.GetSetting("debug/file_logging/log_path").AsString();
        if (setting.Length == 0)
        {
            return null;
        }
        string fallback = ProjectSettings.GlobalizePath(setting);
        // Only if this run is the one writing it: file logging can be off, and screening a
        // previous run's leftover log would report someone else's errors as this run's.
        if (File.Exists(fallback)
            && File.GetLastWriteTime(fallback) >= System.Diagnostics.Process.GetCurrentProcess().StartTime)
        {
            return fallback;
        }
        return null;
    }

    private static StderrScreen? ScreenEngineLog(out string? logPath)
    {
        logPath = EngineLogPath();
        if (logPath == null)
        {
            return null;
        }
        try
        {
            // Godot still owns the handle; share it rather than fighting for exclusive access.
            using var stream = new FileStream(logPath, FileMode.Open, System.IO.FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }
            return Screen(lines);
        }
        catch (Exception e)
        {
            Log.Warn("test", $"engine log unreadable path={logPath} error={e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    private static string FormatTable(List<SuiteResult> results, StderrScreen? screen, string? logPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- run-tests -----------------------------------------------------------");
        int width = results.Count == 0 ? 4 : results.Max(r => r.Name.Length);
        foreach (var r in results)
        {
            sb.AppendLine($"  {Tag(r.Status)}  {r.Name.PadRight(width)}  {r.Seconds,6:0.00}s  {r.Detail}");
            foreach (string f in r.Failures)
            {
                sb.AppendLine($"          !! {f}");
            }
            foreach (string n in r.Notes)
            {
                sb.AppendLine($"           . {n}");
            }
        }
        if (screen == null)
        {
            sb.AppendLine($"  SKIP  engine errors    — no --log-file, native ERROR lines not screened"
                          + (logPath != null ? $" ({logPath})" : ""));
        }
        else
        {
            sb.AppendLine($"  {Tag(screen.Ok ? SuiteStatus.Pass : SuiteStatus.Fail)}  engine errors  "
                          + $"{screen.Total} line(s), {screen.Allowed} allowlisted, {screen.Unexpected.Count} unexpected");
            foreach (var allowance in ErrorAllowlist)
            {
                screen.AllowedCounts.TryGetValue(allowance.Pattern, out int n);
                sb.AppendLine($"           . allowed {n}/{allowance.Max}x  {allowance.Why}");
            }
            foreach (string u in screen.Unexpected)
            {
                sb.AppendLine($"          !! {u}");
            }
            foreach (string o in screen.OverCap)
            {
                sb.AppendLine($"          !! over cap: {o}");
            }
        }
        int pass = results.Count(r => r.Status == SuiteStatus.Pass);
        int fail = results.Count(r => r.Status == SuiteStatus.Fail);
        int skip = results.Count(r => r.Status == SuiteStatus.Skip);
        sb.AppendLine($"  {pass} passed, {fail} failed, {skip} skipped");
        sb.AppendLine("-------------------------------------------------------------------------");
        return sb.ToString();
    }

    private static string Tag(SuiteStatus s) => s switch
    {
        SuiteStatus.Pass => "PASS",
        SuiteStatus.Fail => "FAIL",
        _ => "SKIP",
    };

    private static void WriteReport(TestContext ctx, List<SuiteResult> results,
        StderrScreen? screen, string? logPath)
    {
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"chapter\": {Quote(ctx.Chapter)},");
        json.AppendLine($"  \"mission\": {Quote(ctx.Mission)},");
        json.AppendLine($"  \"dataRoot\": {Quote(ctx.DataRoot)},");
        json.AppendLine($"  \"passed\": {results.Count(r => r.Status == SuiteStatus.Pass)},");
        json.AppendLine($"  \"failed\": {results.Count(r => r.Status == SuiteStatus.Fail)},");
        json.AppendLine($"  \"skipped\": {results.Count(r => r.Status == SuiteStatus.Skip)},");
        json.AppendLine("  \"suites\": [");
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            json.AppendLine("    {");
            json.AppendLine($"      \"name\": {Quote(r.Name)},");
            json.AppendLine($"      \"status\": {Quote(r.Status.ToString().ToLowerInvariant())},");
            json.AppendLine($"      \"seconds\": {r.Seconds.ToString("0.000", CultureInfo.InvariantCulture)},");
            json.AppendLine($"      \"detail\": {Quote(r.Detail)},");
            json.AppendLine($"      \"counts\": {{{string.Join(", ", r.Counts.Select(kv => $"{Quote(kv.Key)}: {kv.Value}"))}}},");
            json.AppendLine($"      \"failures\": [{string.Join(", ", r.Failures.Select(Quote))}],");
            json.AppendLine($"      \"notes\": [{string.Join(", ", r.Notes.Select(Quote))}]");
            json.AppendLine(i == results.Count - 1 ? "    }" : "    },");
        }
        json.AppendLine("  ],");
        json.AppendLine("  \"engineErrors\": {");
        json.AppendLine($"    \"logFile\": {(logPath == null ? "null" : Quote(logPath))},");
        json.AppendLine($"    \"screened\": {(screen != null).ToString().ToLowerInvariant()},");
        json.AppendLine($"    \"total\": {screen?.Total ?? 0},");
        json.AppendLine($"    \"allowlisted\": {screen?.Allowed ?? 0},");
        json.AppendLine($"    \"unexpected\": [{string.Join(", ", (screen?.Unexpected ?? Array.Empty<string>()).Select(Quote))}],");
        json.AppendLine($"    \"overCap\": [{string.Join(", ", (screen?.OverCap ?? Array.Empty<string>()).Select(Quote))}],");
        json.AppendLine("    \"allowlist\": [");
        for (int i = 0; i < ErrorAllowlist.Count; i++)
        {
            var a = ErrorAllowlist[i];
            int seen = 0;
            screen?.AllowedCounts.TryGetValue(a.Pattern, out seen);
            json.AppendLine($"      {{\"pattern\": {Quote(a.Pattern)}, \"max\": {a.Max}, \"seen\": {seen}, \"why\": {Quote(a.Why)}}}"
                            + (i == ErrorAllowlist.Count - 1 ? "" : ","));
        }
        json.AppendLine("    ]");
        json.AppendLine("  }");
        json.AppendLine("}");

        // Absolute, and inside .scratch/: the one place a suite is allowed to write.
        Directory.CreateDirectory(ctx.ScratchDir);
        string path = Path.Combine(ctx.ScratchDir, "test-report.json");
        File.WriteAllText(path, json.ToString());
        Log.Info("test", $"report file={path}");
    }

    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:x4}");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    public sealed record Suite(string Name, string What, Action<TestContext> Body);
}

/// <summary>Thrown by <see cref="TestContext.RequireData"/> to end a suite as SKIP.</summary>
public sealed class SuiteSkippedException : Exception
{
    public SuiteSkippedException(string why) : base(why) { }
}

/// <summary>One built chapter world, held for the suites that need one. The archives are closed
/// as soon as the build is done (<see cref="WorldSession"/>'s disposal-lifetime contract), so what
/// survives here is the scene subtree and its bound runtime.</summary>
public sealed class TestWorld
{
    public required string Chapter { get; init; }
    public required bool Collision { get; init; }
    public required WorldSession Session { get; init; }
    public required Node3D Stage { get; init; }

    /// <summary>The chapter's parsed gamez, kept past the build so a suite can build real geometry
    /// of its own from it — the effect-template stage the world-effects runtime stages,
    /// which is meshes and cannot be faked with named empty nodes. Not disposable, and
    /// not the texture archive, which IS and is closed with the build.</summary>
    public required GameZ Gamez { get; init; }

    public AnimRuntime Runtime => Session.Runtime;

    internal void Destroy()
    {
        Session.Lights?.Dispose();
        // Free, not QueueFree: the harness runs to completion inside one _Ready call, so a queued
        // free would only happen after every suite had already built its own world.
        Stage.Free();
    }
}

/// <summary>
/// What a suite is handed: the resolved data paths, the assertion verbs, a scene-tree host, and
/// the chapter-world builder. Every assertion and note goes through the <c>test</c> category of
/// <see cref="Log"/>, so the full-detail file sink records the whole run whatever the console
/// filter is.
/// </summary>
public sealed class TestContext
{
    internal readonly List<string> Failures = new();
    internal readonly List<string> Notes = new();
    internal readonly Dictionary<string, long> Counts = new();

    private readonly Dictionary<string, TestWorld> _worlds = new();

    public required string RepoRoot { get; init; }
    public required string DataRoot { get; init; }
    public required string Chapter { get; init; }
    public required string Mission { get; init; }
    public required string ZrdrPath { get; init; }
    public required string MessagesPath { get; init; }
    public required string PlanesGamezPath { get; init; }
    public required string InterpPath { get; init; }
    public required string SoundsPath { get; init; }
    public required string PlaneName { get; init; }
    public required bool Mute { get; init; }

    /// <summary><c>--loadout=&lt;def&gt;</c>: bind every plane to that def instead of its own. A
    /// cross-binding control — passing a def whose markers the airframe does not carry is how the
    /// <c>loadout-bind</c> suite is shown able to fail on real bad input rather than a planted
    /// assertion.</summary>
    public string? LoadoutOverride { get; init; }

    /// <summary>Installs a fake in place of the real <c>PufferEmitterFactory</c> for the next world
    /// this builds — null (the default) leaves <see cref="WorldSession.Options.EmitterFactory"/> null
    /// too, so a suite that never touches this gets the real adapter exactly as before. Mutable, not
    /// <c>init</c>: a suite sets it right before its own <see cref="WithWorld"/> call, on a chapter
    /// other than <see cref="Chapter"/> so the cached default-chapter world — built with whatever this
    /// property held first — is never silently reused in its place.</summary>
    public IEmitterFactory? EmitterFactory { get; set; }

    /// <summary>Extra sound-group names to prewarm for the next world this builds, a mission's own
    /// vocabulary the anim program never sees (<c>ObjectiveScript.SoundGroupNames()</c>). Mutable,
    /// the same reason <see cref="EmitterFactory"/> is: a suite sets it right before its own
    /// <see cref="WithWorld(string, bool, Action{TestWorld})"/> call.</summary>
    public IReadOnlyCollection<string>? ExtraPrewarmSoundNames { get; set; }

    /// <summary>Where a suite parents anything that must be in the scene tree — a built plane whose
    /// markers are read by global transform, a chapter world whose death sequences are ticked.</summary>
    public required Node3D Host { get; init; }

    /// <summary>The session camera. PLAYER_RANGE conditions and the sound listener measure from it,
    /// exactly as an interactive session's do.</summary>
    public required Camera3D Camera { get; init; }

    /// <summary>The scratch directory every artifact this run writes must stay inside.</summary>
    public string ScratchDir => Path.Combine(RepoRoot, ".scratch");

    /// <summary>Records a check. A false verdict fails the suite but does not stop it — the rest of
    /// the checks still run, so one report names every broken thing rather than the first.</summary>
    public void Check(bool ok, FormattableString what)
    {
        string text = Log.Format(what);
        if (ok)
        {
            Log.Debug("test", $"ok {text}");
            return;
        }
        Failures.Add(text);
        Log.Error("test", $"FAIL {text}");
    }

    /// <summary>A check on a count, phrased so the failure line carries both numbers.</summary>
    public void Same(long expected, long actual, FormattableString what)
    {
        Counts[Log.Format(what)] = actual;
        Check(expected == actual, $"{Log.Format(what)} expected={expected} actual={actual}");
    }

    /// <summary>Something worth having in the report that is not a verdict — a measured count, a
    /// caveat about what this run could not see.</summary>
    public void Note(FormattableString what)
    {
        string text = Log.Format(what);
        Notes.Add(text);
        Log.Info("test", $"note {text}");
    }

    /// <summary>Ends the suite as SKIP when an input the suite needs is not on disk. Skipped is
    /// reported distinctly from passed, so an empty data root cannot read as a green run.</summary>
    public void RequireData(string path, FormattableString what)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new SuiteSkippedException($"{Log.Format(what)} not found: {path}");
        }
    }

    /// <summary>Leaves a suite's full report in the scratch folder, so a failure is diagnosable
    /// without re-running the equivalent <c>--dump-*</c> tool by hand. Absolute path, inside
    /// <c>.scratch/</c> — the only place a suite may write.</summary>
    public void WriteArtifact(string fileName, string text)
    {
        Directory.CreateDirectory(ScratchDir);
        string path = Path.Combine(ScratchDir, fileName);
        File.WriteAllText(path, text);
        Log.Info("test", $"artifact file={path}");
    }

    /// <summary>Builds (or reuses) a chapter world and runs <paramref name="body"/> against it. The
    /// run's own chapter is cached; any other chapter is freed once the body returns, so a
    /// per-chapter census does not hold eight worlds at once. The subtree is in the scene tree with
    /// <see cref="AnimRuntime.ManualAdvance"/> set, so an out-of-tree transform read returns identity
    /// and <c>_Process</c> does not also drive the runtime.</summary>
    public void WithWorld(string chapter, bool collision, Action<TestWorld> body) =>
        WithWorld(chapter, collision, mission: null, body);

    /// <summary>The mission-override form: builds the chapter at a mission other than the
    /// run's own (the zeppelin damage suite wants C1 at M04, where <c>piratezep</c> is live).
    /// An overridden-mission world is never cached — the cache is keyed by chapter alone, so
    /// storing it would hand the wrong mission to every later same-chapter suite.</summary>
    public void WithWorld(string chapter, bool collision, string? mission, Action<TestWorld> body)
    {
        bool defaultMission = mission == null || mission == Mission;
        if (defaultMission && _worlds.TryGetValue(chapter, out var cached))
        {
            if (!collision || cached.Collision)
            {
                body(cached);
                return;
            }
            _worlds.Remove(chapter);
            cached.Destroy();
        }
        var world = BuildWorld(chapter, collision, mission ?? Mission);
        if (defaultMission && chapter == Chapter)
        {
            _worlds[chapter] = world;
            body(world);
            return;
        }
        try
        {
            body(world);
        }
        finally
        {
            world.Destroy();
        }
    }

    internal void ReleaseWorlds()
    {
        foreach (var w in _worlds.Values)
        {
            w.Destroy();
        }
        _worlds.Clear();
    }

    private TestWorld BuildWorld(string chapter, bool collision, string mission)
    {
        string gamezPath = SessionPaths.ChapterGamez(DataRoot, chapter);
        string texturesPath = SessionPaths.ChapterTextures(DataRoot, chapter);
        RequireData(gamezPath, $"chapter {chapter} gamez");
        RequireData(texturesPath, $"chapter {chapter} textures");

        var stage = new Node3D { Name = $"TestWorld_{chapter}" };
        Host.AddChild(stage);
        // The archives are this scope's: WorldSession clears the puffer factory and the sound
        // loader after its bootstrap precisely so they can close here.
        var archives = SessionArchives.OpenFor(ArchiveIntent.Suite, gamezPath, texturesPath,
            SoundsPath, ZrdrPath, Mute);
        using var textures = archives.Textures;
        using var sounds = archives.Sounds;

        var session = WorldSession.Build(
            new WorldSession.Options
            {
                DataRoot = DataRoot,
                Chapter = chapter,
                Mission = mission,
                ZrdrPath = ZrdrPath,
                InterpPath = InterpPath,
                MissionZrdrPath = SessionPaths.MissionZrdr(DataRoot, chapter, mission),
                EffectsParent = stage,
                PlayerPosition = () => Camera.GlobalPosition,
                Collision = collision,
                RuntimeSeed = Rng.IntSeedFor(Rng.Anim),
                EmitterFactory = EmitterFactory,
                ExtraPrewarmNames = ExtraPrewarmSoundNames,
                TexturesOutliveBuild = archives.TexturesOutliveBuild,
                SoundsOutliveBuild = archives.SoundsOutliveBuild,
            },
            archives.Gamez, textures, sounds, archives.SoundDefs, archives.SoundGroups);
        stage.AddChild(session.Root);
        session.Runtime.ManualAdvance = true;
        return new TestWorld
        {
            Chapter = chapter,
            Collision = collision,
            Session = session,
            Stage = stage,
            Gamez = archives.Gamez,
        };
    }
}

/// <summary>One suite's verdict, as it reaches the table and the JSON report.</summary>
public sealed class SuiteResult
{
    public required string Name { get; init; }
    public required SuiteStatus Status { get; init; }
    public required double Seconds { get; init; }
    public string Detail { get; init; } = "";
    public IReadOnlyList<string> Failures { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, long> Counts { get; init; } = new Dictionary<string, long>();
}

/// <summary>A native engine error the run is known to emit and that no suite here caused. Each
/// carries a cap, so the same message appearing MORE often than measured still fails — an
/// allowlist that swallowed an unbounded count would hide the next regression in the shape of an
/// old one.</summary>
public sealed record ErrorAllowance(string Pattern, int Max, string Why);

/// <summary>The result of screening a run's engine log for error lines.</summary>
public sealed class StderrScreen
{
    public int Total { get; init; }
    public int Allowed { get; init; }
    public IReadOnlyList<string> Unexpected { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OverCap { get; init; } = Array.Empty<string>();

    /// <summary>How often each allowlisted pattern actually fired. Reported even when the screen
    /// passes: an allowlist entry whose count is invisible is exactly how a new error hides inside
    /// an old one's shape.</summary>
    public IReadOnlyDictionary<string, int> AllowedCounts { get; init; } = new Dictionary<string, int>();

    public bool Ok => Unexpected.Count == 0 && OverCap.Count == 0;
}


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM;

/// <summary>
/// One immutable value for everything the command line settles about a session: the raw args,
/// parsed once, with no resolution applied.
///
/// <para><b>Raw means raw.</b> Every property here answers "what did the command line say", never
/// "what will the session do". A flag records only itself: <c>--markers</c> sets
/// <see cref="MarkersOverlay"/> and NOT <see cref="Viewer"/>, <c>--damage-test</c> sets
/// <see cref="DamageTest"/> and NOT <see cref="Freecam"/>, <c>--play-anim=</c> does not set
/// <see cref="AnimLab"/>, and <c>--stunt</c> does not set <see cref="Fly"/> or move
/// <see cref="Scenario"/>. Every one of those implications — and the mode arbitration, the
/// <c>--det</c> bundle, the placement routing, <c>WorldMode</c> and <c>BuildsCollision</c> — is
/// resolution, and resolution is a separate step. Reading a raw property as if it were the
/// resolved answer is the one way to misuse this type.</para>
///
/// <para><b>Pure.</b> <see cref="Parse"/> touches no engine state and no globals: it does not set
/// <c>Pads.Disabled</c>, does not call <c>TextureDropIn</c>, does not <c>Log.Configure</c> and does
/// not log. The three arg branches that reach out and touch something today are recorded instead —
/// <see cref="NoPads"/>, <see cref="TexOverrides"/>/<see cref="TexCensus"/>,
/// <see cref="LogSpecs"/> — and complaints land in <see cref="Warnings"/> for the caller to emit.
/// That is what makes the whole surface reachable from <c>CSVM.Tests</c>, which has no Godot
/// runtime to print into.</para>
///
/// <para>Godot's <c>Vector3</c>/<c>Color</c> are plain managed structs, so they cost nothing here;
/// <see cref="SessionPaths"/> is the precedent for lifting pure logic out of
/// <see cref="PlaneViewer"/> this way.</para>
/// </summary>
public sealed record SessionSpec
{
    /// <summary>A parse-time complaint, held rather than logged so parsing stays engine-free.
    /// The category is the <c>Log</c> one the caller should emit it under.</summary>
    public readonly record struct Note(string Category, string Message);

    private SessionSpec()
    {
    }

    /// <summary>The user args this spec was parsed from, verbatim and in order.</summary>
    public IReadOnlyList<string> Args { get; private set; } = Array.Empty<string>();

    /// <summary>Parse-time complaints, in the order they arose. Nothing is logged during parse.</summary>
    public IReadOnlyList<Note> Warnings { get; private set; } = Array.Empty<Note>();

    // ---- Mode-selecting flags, each recording only itself -------------------------------------

    /// <summary>A content-selecting arg was given, so the launchscreen is bypassed.</summary>
    public bool HasContentArg { get; private set; }
    public bool Fly { get; private set; }
    public bool Stunt { get; private set; }
    public bool Viewer { get; private set; }
    public bool Freecam { get; private set; }
    public bool AnimLab { get; private set; }
    public bool DamageLab { get; private set; }
    public bool ForceMenu { get; private set; }
    public string MenuStartScreen { get; private set; } = "";

    // ---- The world ----------------------------------------------------------------------------

    public string Chapter { get; private set; } = "C1";
    /// <summary><c>--chapter</c>/<c>--chapter=</c> was given. <c>--node=</c> also implies a chapter,
    /// but that is resolution, so it does not show here.</summary>
    public bool ChapterGiven { get; private set; }
    public string Mission { get; private set; } = "IA1";
    public string Scenario { get; private set; } = "zeppelin_run";
    public bool ScenarioExplicit { get; private set; }
    /// <summary>The raw <c>--stage=</c> value, unvalidated: only "empty" names a stage, and
    /// rejecting anything else (and rejecting it in the inspection modes) is resolution.</summary>
    public string? Stage { get; private set; }
    public string? NodeName { get; private set; }
    public string SkyZone { get; private set; } = "zone2";
    public bool SkyZoneExplicit { get; private set; }
    public bool NoFog { get; private set; }
    public int AnimLod { get; private set; } = AnimRuntime.HighLod;
    public string? DestroyName { get; private set; }

    // ---- The aircraft -------------------------------------------------------------------------

    public string PlaneName { get; private set; } = "player_bhawk";
    /// <summary>The <c>--plane=</c> list; empty when a single plane (or none) was named. Whether a
    /// list of several implies <see cref="Players"/> is resolution.</summary>
    public IReadOnlyList<string> PlaneNames { get; private set; } = Array.Empty<string>();
    public int Players { get; private set; } = 1;
    public bool PlayersExplicit { get; private set; }
    public string? LoadoutOverride { get; private set; }
    public string? RocketOverride { get; private set; }
    public int GunSelect { get; private set; }
    public bool InfiniteAmmo { get; private set; }
    public bool AutoFire { get; private set; }
    public bool AutoFireRockets { get; private set; }
    public (FlightInput, float)[][]? HoldSets { get; private set; }
    /// <summary>The <c>--damage=</c> preset pairs (part, fraction 0–1); null when <c>--damage</c>
    /// carried no value.</summary>
    public IReadOnlyList<(string Part, float Fraction)>? DamagePreset { get; private set; }

    // ---- Liveries -----------------------------------------------------------------------------

    public IReadOnlyList<string>? PaintNames { get; private set; }
    public IReadOnlyList<Color>? PaintColorOverride { get; private set; }
    public IReadOnlyList<int>? PaintDecalOverride { get; private set; }
    public ulong PaintSeed { get; private set; }
    public bool PaintSeedExplicit { get; private set; }

    // ---- Determinism, as asked for — not as resolved ------------------------------------------

    /// <summary><c>--det</c> was passed. Which other flags imply the bundle is resolution.</summary>
    public bool Det { get; private set; }
    public bool NoDet { get; private set; }
    /// <summary><c>--seed=N</c>, null when unset. The master seed (pinned value or clock draw) is
    /// resolution.</summary>
    public ulong? Seed { get; private set; }
    /// <summary><c>--spawn=N</c>; &lt; 0 means "random pick", the unset default.</summary>
    public int SpawnIndex { get; private set; } = -1;
    /// <summary><c>--no-pads</c> was passed. Parsing does not touch <c>Pads.Disabled</c>.</summary>
    public bool NoPads { get; private set; }
    /// <summary><c>--jitter=</c> degrees; &lt; 0 means "auto", resolved from <c>--shots</c>/det.</summary>
    public float JitterDeg { get; private set; } = -1f;

    // ---- Placement, as written on the command line --------------------------------------------

    public Vector3? Pos { get; private set; }
    public Vector3? Direction { get; private set; }
    public Vector3? LookAt { get; private set; }
    /// <summary>The deprecated <c>--spawn-at=</c>. <c>--pos</c> reaching the spawn is resolution.</summary>
    public Vector3? SpawnAt { get; private set; }
    /// <summary>The deprecated <c>--spawn-dir=</c>.</summary>
    public Vector3? SpawnDir { get; private set; }
    /// <summary>The deprecated <c>--campos=</c>. There is no raw <c>CamDir</c>: the camera aim only
    /// ever comes from routing <c>--direction</c>, which is resolution.</summary>
    public Vector3? CamPos { get; private set; }
    public float? Yaw { get; private set; }
    public float? Pitch { get; private set; }
    /// <summary>The <c>--view=</c> numpad digit, already filtered to 1–4/6–9 (0 = chase). Whether a
    /// view survives outside flight is resolution.</summary>
    public int View { get; private set; }
    /// <summary>Deprecated spellings seen, first-seen order, deduplicated, with their replacement.</summary>
    public IReadOnlyList<(string Old, string New)> Deprecated { get; private set; }
        = Array.Empty<(string, string)>();

    // ---- Capture ------------------------------------------------------------------------------

    public string? ScreenshotPath { get; private set; }
    public int ScreenshotFrames { get; private set; } = 15;
    public int ScreenshotShots { get; private set; } = 1;

    // ---- The probes that drive and end a session themselves ------------------------------------

    public bool DumpMarkers { get; private set; }
    public string DumpMarkersPlane { get; private set; } = "";
    public bool DumpWeapons { get; private set; }
    public string DumpWeaponsFilter { get; private set; } = "";
    public bool DumpLoadout { get; private set; }
    public string DumpLoadoutFilter { get; private set; } = "";
    public bool DumpFlight { get; private set; }
    public string DumpFlightPlane { get; private set; } = "";
    public bool DumpConfig { get; private set; }
    public bool DumpSession { get; private set; }
    public bool DamageTest { get; private set; }
    public string DamageTestFilter { get; private set; } = "";
    public float DamageHd { get; private set; }
    public bool EffectsTest { get; private set; }
    public bool WeaponTest { get; private set; }
    public bool RunTests { get; private set; }
    public string RunTestsFilter { get; private set; } = "";
    public bool HudFontTest { get; private set; }
    public string HudFontTestText { get; private set; } = "GUNS 30: 2000  ROCKETS 06: 9";

    // ---- Inspection overlays and labs ----------------------------------------------------------

    public bool DebugAnim { get; private set; }
    public bool DebugAnimUi { get; private set; }
    public string? PlayAnim { get; private set; }
    public bool DebugDzPaths { get; private set; }
    public bool DebugScoreboard { get; private set; }
    public int? DebugLivery { get; private set; }
    public string? DebugMesh { get; private set; }
    public string? DebugNames { get; private set; }
    public string? DebugSelect { get; private set; }
    /// <summary>The raw <c>--debug-nodelab=</c> spec. Its token grammar belongs to the lab
    /// (<c>UI.NodeLab.ParseDebugSpec</c>), which logs as it filters, so normalising it stays a
    /// consumer's job — parsing here would either duplicate the grammar or import the logging.</summary>
    public string? DebugNodeLab { get; private set; }
    /// <summary>The raw <c>--debug-damage=</c> spec; same split as <see cref="DebugNodeLab"/>
    /// (<c>UI.WorldDamageLab.ParseDebugSpec</c>).</summary>
    public string? DebugDamage { get; private set; }
    public int DebugJoin { get; private set; }
    public bool MarkersOverlay { get; private set; }
    public bool WeaponLab { get; private set; }
    public string? WeaponSelect { get; private set; }
    public string? WeaponMount { get; private set; }
    public bool WeaponFire { get; private set; }

    // ---- Collision ----------------------------------------------------------------------------

    public bool ForceCollision { get; private set; }
    public bool ShowColliders { get; private set; }
    public bool DebugCollision { get; private set; }

    // ---- Where the data comes from: override VALUES only, null = not given ---------------------

    /// <summary><c>--data-root=</c> verbatim. The precedence against <c>CSVM_DATA_ROOT</c> and the
    /// repo root, and the paths derived from the winner, are resolution.</summary>
    public string? DataRoot { get; private set; }
    public string? Gamez { get; private set; }
    public string? Textures { get; private set; }
    public string? Zrdr { get; private set; }
    public string? Sounds { get; private set; }
    public string? Interp { get; private set; }
    public string? Messages { get; private set; }
    public string? Rof { get; private set; }

    // ---- Texture drop-in (applied to statics by the caller, not here) --------------------------

    public IReadOnlyList<string> TexOverrides { get; private set; } = Array.Empty<string>();
    public bool TexCensus { get; private set; }
    public string TexCensusFilter { get; private set; } = "";

    // ---- Everything else ------------------------------------------------------------------------

    public bool Mute { get; private set; }
    public bool NoVsync { get; private set; }
    public bool Perf { get; private set; }
    public bool NoFocus { get; private set; }
    /// <summary>The <c>--log=</c> specs in command-line order. <c>--debug-anim</c>'s implied
    /// "anim:debug,sound:debug" is not one of them — that is resolution.</summary>
    public IReadOnlyList<string> LogSpecs { get; private set; } = Array.Empty<string>();

    /// <summary>Parses the user args into a spec. Last occurrence of a flag wins, as it does in the
    /// loop this mirrors; an unrecognised arg is ignored silently, also as today.</summary>
    public static SessionSpec Parse(IEnumerable<string> args)
    {
        var argv = args.ToArray();
        var s = new SessionSpec { Args = argv };
        var notes = new List<Note>();
        var deprecated = new List<(string Old, string New)>();
        var logSpecs = new List<string>();
        var texOverrides = new List<string>();

        void Deprecate(string old, string replacement)
        {
            if (!deprecated.Exists(d => d.Old == old))
            {
                deprecated.Add((old, replacement));
            }
        }

        foreach (var arg in argv)
        {
            if (arg.StartsWith("--plane="))
            {
                s.PlaneNames = ParsePlanes(arg["--plane=".Length..]);
                if (s.PlaneNames.Count > 0)
                {
                    s.PlaneName = s.PlaneNames[0];
                }
                s.HasContentArg = true;
            }
            else if (arg == "--viewer") { s.Viewer = true; s.HasContentArg = true; }
            else if (arg == "--damage") { s.DamageLab = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--damage="))
            {
                s.DamageLab = true;
                var rejected = new List<string>();
                s.DamagePreset = ParseDamagePreset(arg["--damage=".Length..], rejected);
                foreach (var bad in rejected)
                {
                    notes.Add(new Note("core", $"--damage: cannot parse '{bad}' (want part:fraction)"));
                }
                s.HasContentArg = true;
            }
            else if (arg == "--chapter") { s.ChapterGiven = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--chapter=")) { s.Chapter = arg["--chapter=".Length..]; s.ChapterGiven = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--stage=")) { s.Stage = arg["--stage=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--node=")) { s.NodeName = arg["--node=".Length..]; s.HasContentArg = true; }
            else if (arg == "--fly") { s.Fly = true; s.HasContentArg = true; }
            else if (arg == "--stunt") { s.Stunt = true; s.HasContentArg = true; }
            else if (arg == "--freecam") { s.Freecam = true; s.HasContentArg = true; }
            else if (arg == "--anim-lab") { s.AnimLab = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--play-anim=")) { s.PlayAnim = arg["--play-anim=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--seed=")) { s.Seed = ulong.Parse(arg["--seed=".Length..]); }
            else if (arg == "--debug-anim-ui") { s.DebugAnimUi = true; s.HasContentArg = true; }
            else if (arg == "--debug-anim") { s.DebugAnim = true; }
            else if (arg == "--no-pads") { s.NoPads = true; }
            else if (arg == "--det") { s.Det = true; }
            else if (arg == "--no-det") { s.NoDet = true; }
            else if (arg == "--perf") { s.Perf = true; }
            else if (arg.StartsWith("--log=")) { logSpecs.Add(arg["--log=".Length..]); }
            else if (arg.StartsWith("--anim-lod=")) { s.AnimLod = int.Parse(arg["--anim-lod=".Length..]); }
            else if (arg == "--menu") { s.ForceMenu = true; }
            else if (arg.StartsWith("--menu=")) { s.ForceMenu = true; s.MenuStartScreen = arg["--menu=".Length..]; }
            else if (arg == "--debug-dzpaths") { s.DebugDzPaths = true; }
            else if (arg.StartsWith("--debug-join=")) { s.DebugJoin = int.Parse(arg["--debug-join=".Length..]); }
            else if (arg.StartsWith("--paint=")) { s.PaintNames = arg["--paint=".Length..].Split(',', StringSplitOptions.TrimEntries); }
            else if (arg.StartsWith("--paint-color=")) { s.PaintColorOverride = ParsePaintColors(arg["--paint-color=".Length..]); }
            else if (arg.StartsWith("--paint-decal=")) { s.PaintDecalOverride = ParsePaintDecals(arg["--paint-decal=".Length..]); }
            else if (arg.StartsWith("--paint-seed=")) { s.PaintSeed = ulong.Parse(arg["--paint-seed=".Length..]); s.PaintSeedExplicit = true; }
            else if (arg.StartsWith("--rof=")) { s.Rof = arg["--rof=".Length..]; }
            else if (arg == "--debug-scoreboard") { s.DebugScoreboard = true; }
            else if (arg == "--debug-livery") { s.DebugLivery ??= 0; }
            else if (arg.StartsWith("--debug-livery=")) { s.DebugLivery = int.Parse(arg["--debug-livery=".Length..]); }
            else if (arg == "--debug-mesh") { s.DebugMesh ??= ""; }
            else if (arg.StartsWith("--debug-mesh=")) { s.DebugMesh = arg["--debug-mesh=".Length..]; }
            else if (arg == "--debug-names") { s.DebugNames ??= "meshes"; }
            else if (arg.StartsWith("--debug-names=")) { s.DebugNames = arg["--debug-names=".Length..]; }
            else if (arg == "--debug-select") { s.DebugSelect ??= ""; }
            else if (arg.StartsWith("--debug-select=")) { s.DebugSelect = arg["--debug-select=".Length..]; }
            else if (arg == "--debug-nodelab") { s.DebugNodeLab ??= ""; }
            else if (arg.StartsWith("--debug-nodelab=")) { s.DebugNodeLab = arg["--debug-nodelab=".Length..]; }
            else if (arg == "--collision") { s.ForceCollision = true; }
            else if (arg.StartsWith("--collision="))
            {
                s.ForceCollision = true;
                string want = arg["--collision=".Length..];
                s.ShowColliders = want == "show";
                if (!s.ShowColliders && want.Length > 0)
                {
                    notes.Add(new Note("world", $"--collision='{want}' is not a value it takes (only '=show', which opens the C overlay) — building collision anyway"));
                }
            }
            else if (arg == "--debug-colliders") { s.ShowColliders = true; }
            else if (arg == "--debug-damage") { s.DebugDamage ??= ""; }
            else if (arg.StartsWith("--debug-damage=")) { s.DebugDamage = arg["--debug-damage=".Length..]; }
            else if (arg == "--markers") { s.MarkersOverlay = true; s.HasContentArg = true; }
            else if (arg == "--dump-markers") { s.DumpMarkers = true; }
            else if (arg.StartsWith("--dump-markers=")) { s.DumpMarkers = true; s.DumpMarkersPlane = arg["--dump-markers=".Length..]; }
            else if (arg == "--dump-weapons") { s.DumpWeapons = true; }
            else if (arg.StartsWith("--dump-weapons=")) { s.DumpWeapons = true; s.DumpWeaponsFilter = arg["--dump-weapons=".Length..]; }
            else if (arg == "--dump-loadout") { s.DumpLoadout = true; }
            else if (arg.StartsWith("--dump-loadout=")) { s.DumpLoadout = true; s.DumpLoadoutFilter = arg["--dump-loadout=".Length..]; }
            else if (arg == "--dump-flight") { s.DumpFlight = true; }
            else if (arg.StartsWith("--dump-flight=")) { s.DumpFlight = true; s.DumpFlightPlane = arg["--dump-flight=".Length..]; }
            else if (arg == "--dump-config") { s.DumpConfig = true; }
            else if (arg == "--dump-session") { s.DumpSession = true; }
            else if (arg == "--damage-test") { s.DamageTest = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--damage-test=")) { s.DamageTest = true; s.DamageTestFilter = arg["--damage-test=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--damage-hd=")) { s.DamageHd = Flt(arg["--damage-hd=".Length..]); }
            else if (arg == "--effects-test") { s.EffectsTest = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--destroy=")) { s.DestroyName = arg["--destroy=".Length..]; }
            else if (arg.StartsWith("--loadout=")) { s.LoadoutOverride = arg["--loadout=".Length..]; }
            else if (arg == "--infinite-ammo") { s.InfiniteAmmo = true; }
            else if (arg == "--fire") { s.AutoFire = true; }
            else if (arg == "--fire-rockets") { s.AutoFireRockets = true; }
            else if (arg.StartsWith("--gun-select=")) { s.GunSelect = int.Parse(arg["--gun-select=".Length..]); }
            else if (arg.StartsWith("--rocket=")) { s.RocketOverride = arg["--rocket=".Length..]; }
            else if (arg == "--hud-font-test") { s.HudFontTest = true; }
            else if (arg.StartsWith("--hud-font-test=")) { s.HudFontTest = true; s.HudFontTestText = arg["--hud-font-test=".Length..]; }
            else if (arg == "--weapon-lab") { s.WeaponLab = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--weapon-lab=")) { s.WeaponLab = true; s.WeaponSelect = arg["--weapon-lab=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--weapon-mount=")) { s.WeaponMount = arg["--weapon-mount=".Length..]; s.HasContentArg = true; }
            else if (arg == "--weapon-fire") { s.WeaponFire = true; s.HasContentArg = true; }
            else if (arg == "--weapon-test") { s.WeaponTest = true; s.HasContentArg = true; }
            else if (arg == "--run-tests") { s.RunTests = true; }
            else if (arg.StartsWith("--run-tests=")) { s.RunTests = true; s.RunTestsFilter = arg["--run-tests=".Length..]; }
            else if (arg.StartsWith("--mission=")) { s.Mission = arg["--mission=".Length..]; }
            else if (arg.StartsWith("--scenario=")) { s.Scenario = arg["--scenario=".Length..]; s.ScenarioExplicit = true; }
            else if (arg.StartsWith("--spawn=")) { s.SpawnIndex = int.Parse(arg["--spawn=".Length..]); }
            else if (arg.StartsWith("--spawn-at=")) { s.SpawnAt = ParseVec3(arg["--spawn-at=".Length..]); Deprecate("--spawn-at", "--pos"); }
            else if (arg.StartsWith("--spawn-dir=")) { s.SpawnDir = ParseVec3(arg["--spawn-dir=".Length..]); Deprecate("--spawn-dir", "--direction"); }
            else if (arg.StartsWith("--sky-zone=")) { s.SkyZone = arg["--sky-zone=".Length..]; s.SkyZoneExplicit = true; }
            else if (arg.StartsWith("--data-root=")) { s.DataRoot = arg["--data-root=".Length..]; }
            else if (arg.StartsWith("--gamez=")) { s.Gamez = arg["--gamez=".Length..]; }
            else if (arg.StartsWith("--textures=")) { s.Textures = arg["--textures=".Length..]; }
            else if (arg.StartsWith("--zrdr=")) { s.Zrdr = arg["--zrdr=".Length..]; }
            else if (arg.StartsWith("--interp=")) { s.Interp = arg["--interp=".Length..]; }
            else if (arg.StartsWith("--sounds=")) { s.Sounds = arg["--sounds=".Length..]; }
            else if (arg.StartsWith("--messages=")) { s.Messages = arg["--messages=".Length..]; }
            else if (arg == "--no-fog") { s.NoFog = true; }
            else if (arg.StartsWith("--tex-override=")) { texOverrides.Add(arg["--tex-override=".Length..]); }
            else if (arg == "--tex-census") { s.TexCensus = true; }
            else if (arg.StartsWith("--tex-census=")) { s.TexCensus = true; s.TexCensusFilter = arg["--tex-census=".Length..]; }
            else if (arg == "--no-focus") { s.NoFocus = true; }
            else if (arg == "--no-vsync") { s.NoVsync = true; }
            else if (arg == "--mute") { s.Mute = true; }
            else if (arg == "--debug-collision") { s.DebugCollision = true; }
            else if (arg.StartsWith("--players=")) { s.Players = int.Parse(arg["--players=".Length..]); s.PlayersExplicit = true; }
            else if (arg.StartsWith("--hold=")) { s.HoldSets = ParseHold(arg["--hold=".Length..]); }
            else if (arg.StartsWith("--frames=")) { s.ScreenshotFrames = int.Parse(arg["--frames=".Length..]); }
            else if (arg.StartsWith("--shots=")) { s.ScreenshotShots = Math.Max(1, int.Parse(arg["--shots=".Length..])); }
            else if (arg.StartsWith("--jitter=")) { s.JitterDeg = Flt(arg["--jitter=".Length..]); }
            else if (arg.StartsWith("--screenshot=")) { s.ScreenshotPath = arg["--screenshot=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--yaw=")) { s.Yaw = Flt(arg["--yaw=".Length..]); }
            else if (arg.StartsWith("--pitch=")) { s.Pitch = Flt(arg["--pitch=".Length..]); }
            else if (arg.StartsWith("--pos=")) { s.Pos = ParseVec3(arg["--pos=".Length..]); }
            else if (arg.StartsWith("--direction=")) { s.Direction = ParseVec3(arg["--direction=".Length..]); }
            else if (arg.StartsWith("--campos=")) { s.CamPos = ParseVec3(arg["--campos=".Length..]); Deprecate("--campos", "--pos"); }
            else if (arg.StartsWith("--lookat=")) { s.LookAt = ParseVec3(arg["--lookat=".Length..]); }
            else if (arg.StartsWith("--view="))
            {
                string want = arg["--view=".Length..];
                s.View = ParseView(want);
                if (s.View == 0)
                {
                    notes.Add(new Note("core", $"--view={want} is not a numpad view (1-4, 6-9) — using the chase camera"));
                }
            }
        }

        s.Warnings = notes;
        s.Deprecated = deprecated;
        s.LogSpecs = logSpecs;
        s.TexOverrides = texOverrides;
        return s;
    }

    /// <summary>Parse <c>--plane=</c>: one node name, or a comma-separated list — one plane per
    /// player for splitscreen (the launchscreen's simultaneous pick produces the same list).</summary>
    public static IReadOnlyList<string> ParsePlanes(string value)
    {
        var names = new List<string>();
        foreach (var name in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            names.Add(name.Trim());
        }
        return names;
    }

    /// <summary>Parse an "x,y,z" triple, invariant culture.</summary>
    public static Vector3 ParseVec3(string s)
    {
        var parts = s.Split(',');
        return new Vector3(Flt(parts[0]), Flt(parts[1]), Flt(parts[2]));
    }

    /// <summary>The numpad view digit for <c>--view=</c>. 5 has no perspective of its own (the
    /// middle of the pad is the chase camera), and anything outside 1–9 is a typo — both give 0,
    /// the chase camera, which the caller reports.</summary>
    public static int ParseView(string s)
    {
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
            && n >= 1 && n <= 9 && n != 5)
        {
            return n;
        }
        return 0;
    }

    /// <summary>Parse the scripted hold argument: '|' separates one sequence per player (the last
    /// one covers any remaining players, so the old single-sequence form still drives everyone),
    /// ';' separates that sequence's segments, each "pitch,roll,yaw,throttle" with an optional
    /// "@seconds" duration. The last segment (or one without a duration) holds forever.</summary>
    public static (FlightInput, float)[][] ParseHold(string s)
    {
        var players = new List<(FlightInput, float)[]>();
        foreach (var perPlayer in s.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = new List<(FlightInput, float)>();
            foreach (var seg in perPlayer.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var at = seg.Split('@');
                var p = at[0].Split(',');
                segments.Add((new FlightInput { Pitch = Flt(p[0]), Roll = Flt(p[1]), Yaw = Flt(p[2]), Throttle = Flt(p[3]) },
                              at.Length > 1 ? Flt(at[1]) : 0f));
            }
            players.Add(segments.ToArray());
        }
        return players.ToArray();
    }

    /// <summary>Parse <c>--paint-color=</c>: up to three '/'-separated byte triples (body / dark
    /// trim / light trim); a missing slot repeats the last one given.</summary>
    public static Color[] ParsePaintColors(string spec)
    {
        var parts = spec.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var outc = new Color[3];
        for (int i = 0; i < 3; i++)
        {
            var v = ParseVec3(parts[Math.Min(i, parts.Length - 1)]);
            outc[i] = PaintScheme.FromBytes((int)v.X, (int)v.Y, (int)v.Z);
        }
        return outc;
    }

    /// <summary>Parse <c>--paint-decal=</c>: up to three comma-separated indices (nose / tail /
    /// wing); a missing slot repeats the last one given.</summary>
    public static int[] ParsePaintDecals(string spec)
    {
        var parts = spec.Split(',', StringSplitOptions.TrimEntries);
        var outd = new int[3];
        for (int i = 0; i < 3; i++)
        {
            outd[i] = int.Parse(parts[Math.Min(i, parts.Length - 1)]);
        }
        return outd;
    }

    /// <summary>Parse <c>--damage=</c> presets: "nose:0.25,leftwing:40" — part:fraction pairs,
    /// values &gt; 1 read as percent. Malformed pairs are skipped, and appended to
    /// <paramref name="rejected"/> when one is supplied, so the caller can report them.</summary>
    public static List<(string Part, float Fraction)> ParseDamagePreset(string s, List<string>? rejected = null)
    {
        var list = new List<(string, float)>();
        foreach (var item in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = item.Split(':');
            if (kv.Length == 2 && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                list.Add((kv[0].Trim(), Mathf.Clamp(v > 1f ? v / 100f : v, 0f, 1f)));
            }
            else
            {
                rejected?.Add(item);
            }
        }
        return list;
    }

    private static float Flt(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}

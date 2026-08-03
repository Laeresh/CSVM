using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM;

/// <summary>The session shapes, closed. Every launch is exactly one of these; <c>Stunt</c>,
/// <c>Players</c>, <c>EmptyStage</c>, <c>NodeName</c> and <c>DamageLab</c> are modifiers on top of
/// one, not shapes of their own.</summary>
public enum SessionMode
{
    /// <summary>No content arg (or <c>--menu</c>): the launchscreen picks the session.</summary>
    Menu,
    Fly,
    Viewer,
    Freecam,
    AnimLab,
}

/// <summary>The probes that wear a mode as a disguise: each drives and ends a session by itself,
/// and each asks for the mode that gives it the world it needs. Naming them keeps the coercion
/// visible instead of letting <see cref="SessionMode"/> inherit it as if the user had asked.</summary>
public enum SessionProbe
{
    None,
    /// <summary><c>--damage-test</c>: needs a world with no aircraft in it → freecam.</summary>
    DamageTest,
    /// <summary><c>--effects-test</c>: same world, same reason → freecam.</summary>
    EffectsTest,
    /// <summary><c>--weapon-test</c>: needs a parked plane to mount weapons on → viewer.</summary>
    WeaponTest,
}

/// <summary>
/// One immutable value for everything the command line settles about a session: parsed once, then
/// resolved once, so a consumer reads an answer instead of re-deriving one.
///
/// <para><b><see cref="Parse"/> returns a RESOLVED spec.</b> The raw stage exists only inside it:
/// the mode flags arrive as private fields, arbitration turns them into <see cref="Mode"/>, and the
/// public <see cref="Fly"/>/<see cref="Viewer"/>/<see cref="Freecam"/>/<see cref="AnimLab"/> are
/// computed from it, so there is exactly one definition of each. The properties resolution
/// overwrites say so on themselves; everything else is what the command line said.</para>
///
/// <para><b>Pure.</b> Nothing here touches engine state or globals: it does not set
/// <c>Pads.Disabled</c> (see <see cref="PadsDisabled"/>), does not call <c>TextureDropIn</c>, does
/// not <c>Log.Configure</c>, does not draw a clock seed (see <see cref="PinnedSeed"/>) and does not
/// log — complaints land in <see cref="Warnings"/> for the caller to emit. That is what makes the
/// whole surface reachable from <c>CSVM.Tests</c>, which has no Godot runtime to print into.</para>
///
/// <para><b>Resolution reproduces today's behaviour, warts included</b>, because the equivalence
/// gate compares against it: <see cref="ModeName"/> still omits <c>--dump-flight</c> from its
/// "dump" chain and <see cref="ShowsMenu"/> is still true under <c>--run-tests</c>. Both are marked
/// where they live. Fixing either is a behaviour change and belongs in its own item.</para>
///
/// <para>Godot's <c>Vector3</c>/<c>Color</c> are plain managed structs, so they cost nothing here;
/// <see cref="SessionPaths"/> is the precedent for lifting pure logic out of
/// <see cref="CSVM.Session.GameSession"/> this way.</para>
/// </summary>
public sealed record SessionSpec
{
    /// <summary>The sim frame a bare <c>--crash</c> (no <c>=frame</c>) fires at — early enough that
    /// the default <c>--frames=</c> screenshot lands mid-break-up rather than pre-impact.</summary>
    private const int DefaultCrashFrame = 5;

    private List<Note> _notes = new();

    // What the command line asked for. Private, because a vote is not an outcome: several flags
    // vote for the same mode (--markers/--damage/--weapon-* for the viewer, --damage-test/
    // --effects-test for the freecam, --play-anim=/--debug-anim-ui for the anim lab), and the
    // arbitration below is the only thing entitled to turn them into one.
    private bool _flyArg, _viewerArg, _freecamArg, _animLabArg, _stuntArg, _damageLabArg, _detArg;

    private SessionSpec()
    {
    }

    /// <summary>The user args this spec was parsed from, verbatim and in order.</summary>
    public IReadOnlyList<string> Args { get; private set; } = Array.Empty<string>();

    /// <summary>Every complaint parse and resolution raised, in order. Nothing is logged here — the
    /// caller emits these, which is what keeps the type engine-free.</summary>
    public IReadOnlyList<Note> Warnings => _notes;

    // ---- The mode: raw votes in, one arbitrated answer out -------------------------------------

    /// <summary>A content-selecting arg was given, so the launchscreen is bypassed.</summary>
    public bool HasContentArg { get; private set; }

    /// <summary><b>Resolved.</b> The one session shape, arbitrated from every flag that votes for
    /// one: anim lab &gt; freecam &gt; viewer &gt; fly, with <c>--node=</c> forcing the viewer
    /// unless the anim lab was asked for, and any content arg defaulting to flight.</summary>
    public SessionMode Mode { get; private set; } = SessionMode.Menu;
    public bool Fly => Mode == SessionMode.Fly;
    public bool Viewer => Mode == SessionMode.Viewer;
    public bool Freecam => Mode == SessionMode.Freecam;
    public bool AnimLab => Mode == SessionMode.AnimLab;
    /// <summary><b>Resolved.</b> Flight plus the mission's danger zones — a modifier on
    /// <see cref="SessionMode.Fly"/>, cleared by any mode that beats flight.</summary>
    public bool Stunt { get; private set; }
    /// <summary><b>Resolved.</b> Open the aircraft's per-part HP sliders at launch — a modifier on
    /// <see cref="SessionMode.Viewer"/> (the parked plane) or <see cref="SessionMode.Fly"/> (the
    /// flown one), dropped by the modes that build no aircraft at all. The lab itself is always
    /// built; this only decides whether it starts open or waits behind F5.</summary>
    public bool DamageLab { get; private set; }
    public bool ForceMenu { get; private set; }
    public string MenuStartScreen { get; private set; } = "";

    /// <summary>Which mode-coercing probe (if any) drives this session. The three set a mode at
    /// parse time today; here they vote like anything else and this names the vote.</summary>
    public SessionProbe Probe =>
        DamageTest ? SessionProbe.DamageTest
        : EffectsTest ? SessionProbe.EffectsTest
        : WeaponTest ? SessionProbe.WeaponTest
        : SessionProbe.None;

    /// <summary>Whether <c>_Ready</c> shows the launchscreen before building anything. Independent
    /// of <see cref="Mode"/>: <c>--menu --chapter=C4</c> resolves to flight AND shows the menu.
    /// ⚠ True under <c>--run-tests</c> too — the harness is saved only by returning before the menu
    /// branch. Reproduced deliberately; the equivalence gate compares against it.</summary>
    public bool ShowsMenu => ForceMenu || !HasContentArg;

    /// <summary>The session shape's name: the log file's, and the startup timing line's. ⚠ The
    /// "dump" arm omits <c>--dump-flight</c>, so a <c>--dump-flight</c> run logs as
    /// <c>menu-*.log</c>. That is today's behaviour, reproduced on purpose.</summary>
    public string ModeName =>
        Mode == SessionMode.AnimLab ? "anim-lab"
        : DamageTest || EffectsTest || WeaponTest || RunTests ? "test"
        : DumpMarkers || DumpWeapons || DumpLoadout || DumpConfig || DumpMips ? "dump"
        : Mode == SessionMode.Freecam ? "freecam"
        : Mode == SessionMode.Viewer ? "viewer"
        : Stunt ? "stunt"
        : Mode == SessionMode.Fly ? "fly"
        : "menu";

    /// <summary>Whether a flag will drive and end this session by itself, so nobody is at the
    /// controls: the window hides instead of asking for focus. ⚠ <c>--dump-flight</c>'s absence is
    /// a drift, not a decision — the same omission as <see cref="ModeName"/>'s, and it means a
    /// <c>--dump-flight</c> run turns the bundle on yet still asks for focus.</summary>
    public bool IsScripted =>
        NoFocus || ScreenshotPath != null || ExportGltfPath != null || RunTests
        || DumpMarkers || DumpWeapons || DumpLoadout || DumpConfig || DumpMips
        || DamageTest || EffectsTest || WeaponTest;

    /// <summary><b>Resolved.</b> The chapter world is built instead of a single parked plane.</summary>
    public bool WorldMode { get; private set; }
    /// <summary><b>Resolved.</b> <c>--stage=empty</c> survived: no gamez at all, a generated grid
    /// over a collidable ground plane.</summary>
    public bool EmptyStage { get; private set; }

    /// <summary>Whether this session builds the world's colliders — the one definition every
    /// consumer reads. Flight needs them to fly into things; the headless damage sweep and the
    /// world damage lab need them because a kill's collider count would otherwise read zero and
    /// lie; <c>--collision</c> is the interactive request for them in a mode that builds none.</summary>
    public bool BuildsCollision => Fly || DamageTest || ForceCollision || DebugDamage != null;

    // ---- The world ----------------------------------------------------------------------------

    public string Chapter { get; private set; } = "C1";
    /// <summary><b>Resolved.</b> A chapter world was asked for — by <c>--chapter</c>/<c>--chapter=</c>,
    /// or by <c>--node=</c>, whose subtree comes out of the chapter's gamez.</summary>
    public bool ChapterGiven { get; private set; }
    public string Mission { get; private set; } = "IA1";
    /// <summary><b>Resolved.</b> Which instant-action spawn list to use. <c>--stunt</c> forces
    /// "stunt_flying" unless the tester pinned another one for a specific spawn.</summary>
    public string Scenario { get; private set; } = "zeppelin_run";
    public bool ScenarioExplicit { get; private set; }
    /// <summary>The <c>--stage=</c> value as given, unvalidated — only "empty" names a stage.
    /// Whether it survived is <see cref="EmptyStage"/>.</summary>
    public string? Stage { get; private set; }
    public string? NodeName { get; private set; }
    public string SkyZone { get; private set; } = "zone2";
    public bool SkyZoneExplicit { get; private set; }
    public bool NoFog { get; private set; }
    /// <summary><c>--mips=authored|generated</c>: whether a texture's levels 1 and 2 come from the
    /// archive's hand-authored <c>_1</c>/<c>_2</c> siblings (the default) or are box-filtered from
    /// the base like every level below them. See <see cref="Mech3.TextureArchive.MipSource"/>.</summary>
    public TextureArchive.MipSource Mips { get; private set; } = TextureArchive.MipSource.Authored;
    public int AnimLod { get; private set; } = AnimRuntime.HighLod;
    public string? DestroyName { get; private set; }
    /// <summary><c>--crash[=frame]</c>: the fixed sim frame (<see cref="Utils.GameClock.Frame"/>)
    /// at which every player's <see cref="Flight.FlightController.DebugForceCrash"/> fires — the
    /// only headless trigger for the per-player crash rig (no live collision analog exists).
    /// Null when the flag was absent.</summary>
    public int? CrashFrame { get; private set; }

    // ---- The aircraft -------------------------------------------------------------------------

    public string PlaneName { get; private set; } = "player_bhawk";
    /// <summary>The <c>--plane=</c> list; empty when a single plane (or none) was named.</summary>
    public IReadOnlyList<string> PlaneNames { get; private set; } = Array.Empty<string>();
    /// <summary><b>Resolved.</b> Splitscreen panes. A <c>--plane=</c> list of several states the
    /// count on its own; an explicit <c>--players=</c> still wins. Clamped to the rig's capacity,
    /// and back to 1 for any static view — splitscreen is a flight mode, it needs planes to fly.</summary>
    public int Players { get; private set; } = 1;
    public bool PlayersExplicit { get; private set; }
    public string? LoadoutOverride { get; private set; }
    public string? RocketOverride { get; private set; }
    public int GunSelect { get; private set; }
    public bool InfiniteAmmo { get; private set; }
    /// <summary>The <c>--ammo=</c> low-ammo start knob: caps both gun groups and pylons to this many
    /// rounds at rig build, bypassing config.json (DET-8 drops it) so the cap survives <c>--det</c>.
    /// Null when the flag was absent. Mutually exclusive with <see cref="InfiniteAmmo"/> — whichever
    /// flag comes last in the arg list wins; the loser is logged in <see cref="Warnings"/>.</summary>
    public int? AmmoCap { get; private set; }
    public bool AutoFire { get; private set; }
    public bool AutoFireRockets { get; private set; }
    /// <summary><c>--incoming[=metres[,wep_id]]</c>: the near-miss test rig — a phantom shooter on
    /// each player's six walking bursts past the canopy, so the incoming-fire cue is reachable
    /// before there is anything in the world that shoots back. Null when the flag was absent.</summary>
    public float? IncomingPass { get; private set; }
    /// <summary>Which weapon <c>--incoming</c> fires; null takes the target's own first gun.</summary>
    public string? IncomingWeapon { get; private set; }
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

    // ---- The --det bundle, resolved in one place -----------------------------------------------

    /// <summary>Which flag turns the bundle on by driving and ending the session itself, in
    /// precedence order; empty when none does.</summary>
    public string ScriptedBy =>
        ScreenshotPath != null ? "--screenshot"
        : ExportGltfPath != null ? "--export-gltf"
        : DumpMarkers ? "--dump-markers"
        : DumpWeapons ? "--dump-weapons"
        : DumpLoadout ? "--dump-loadout"
        : DumpFlight ? "--dump-flight"
        : DumpConfig ? "--dump-config"
        : DumpMips ? "--dump-mips"
        : DamageTest ? "--damage-test"
        : EffectsTest ? "--effects-test"
        : WeaponTest ? "--weapon-test"
        : RunTests ? "--run-tests"
        : "";

    /// <summary><c>--det</c> was passed outright, rather than implied.</summary>
    public bool DetExplicit => _detArg;
    /// <summary><b>Resolved.</b> The deterministic bundle is on: fixed-dt clock, pinned master
    /// seed, spawn 0, pinned liveries, no pads, no jitter. <c>--no-det</c> beats both the
    /// implication and an explicit <c>--det</c> — there is one way to ask for wall-clock
    /// behaviour, whatever else is on the command line.</summary>
    public bool Det => !NoDet && (DetExplicit || ScriptedBy.Length > 0);
    public bool NoDet { get; private set; }
    /// <summary>What turned the bundle on, for the announcement line.</summary>
    public string DetVia => DetExplicit ? "--det" : ScriptedBy;
    /// <summary><c>--seed=N</c>, null when unset.</summary>
    public ulong? Seed { get; private set; }
    /// <summary>Whether the master seed is pinned rather than drawn from the clock. The anim lab
    /// pins it by nature — its whole point is an identical replay.</summary>
    public bool SeedPinned => Seed != null || Det || Mode == SessionMode.AnimLab;
    /// <summary>The master seed when it is pinned, else null — the clock draw stays outside, or
    /// this type would not be a function of its args (and a baseline would differ from itself).</summary>
    public ulong? PinnedSeed => SeedPinned ? Seed ?? Rng.DefaultSeed : null;
    /// <summary><b>Resolved.</b> <c>--spawn=N</c>; &lt; 0 means "random pick". <c>--det</c> pins it
    /// to 0 — a pinned CHOICE beats a pinned dice roll, since a seeded pick still moves when the
    /// mission's spawn list grows. An explicit <c>--spawn=N</c> still wins.</summary>
    public int SpawnIndex { get; private set; } = -1;
    /// <summary><c>--no-pads</c> was passed. Nothing here touches <c>Pads.Disabled</c>.</summary>
    public bool NoPads { get; private set; }
    /// <summary>Whether every gamepad is ignored: asked for, or implied by the bundle — a stick
    /// with drift steers the free camera and nudges the flight model.</summary>
    public bool PadsDisabled => NoPads || Det;
    /// <summary><b>Resolved.</b> Burst camera dither in degrees. Bursts default it on so
    /// z-fighting flickers across frames; <c>--det</c> defaults it off instead, since
    /// bit-identical frames are the one property a deterministic run is for.</summary>
    public float JitterDeg { get; private set; } = -1f;

    // ---- Placement, as written on the command line --------------------------------------------

    public Vector3? Pos { get; private set; }
    /// <summary><b>Resolved.</b> Normalised, and derived from <see cref="LookAt"/> in flight — a
    /// point converted against the eye. Null once a degenerate vector is dropped.</summary>
    public Vector3? Direction { get; private set; }
    /// <summary>A POINT, not a vector: the orbit view's pivot, and the freecam's aim when no
    /// <c>--pos</c> was given. The conversion to a direction is one-way and flight-only.</summary>
    public Vector3? LookAt { get; private set; }
    /// <summary><b>Resolved.</b> Where the plane spawns: <c>--pos</c> routed here in flight, or the
    /// deprecated <c>--spawn-at=</c>, or the empty stage's default.</summary>
    public Vector3? SpawnAt { get; private set; }
    /// <summary><b>Resolved.</b> The nose direction there — <c>--direction</c> routed in flight, or
    /// the deprecated <c>--spawn-dir=</c>.</summary>
    public Vector3? SpawnDir { get; private set; }
    /// <summary><b>Resolved.</b> The camera eye: <c>--pos</c> routed here outside flight, or the
    /// deprecated <c>--campos=</c> (which never places the plane, in any mode).</summary>
    public Vector3? CamPos { get; private set; }
    /// <summary><b>Resolved.</b> The camera's aim, held apart from <see cref="LookAt"/> because a
    /// direction names no pivot and the orbit view needs one.</summary>
    public Vector3? CamDir { get; private set; }
    public float? Yaw { get; private set; }
    public float? Pitch { get; private set; }
    /// <summary><b>Resolved.</b> The <c>--view=</c> numpad digit (0 = chase). The numpad views orbit
    /// a FLYING plane, so one asked for outside flight is dropped.</summary>
    public int View { get; private set; }
    /// <summary>Deprecated spellings seen, first-seen order, deduplicated, with their replacement.</summary>
    public IReadOnlyList<(string Old, string New)> Deprecated { get; private set; }
        = Array.Empty<(string, string)>();

    // ---- Capture ------------------------------------------------------------------------------

    public string? ScreenshotPath { get; private set; }
    public int ScreenshotFrames { get; private set; } = 15;
    public int ScreenshotShots { get; private set; } = 1;
    /// <summary>The <c>--export-gltf=</c> target path; null when the flag was absent. The plane
    /// subtree is written there as glTF once the session builds it.</summary>
    public string? ExportGltfPath { get; private set; }

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
    public bool DumpMips { get; private set; }
    public string DumpMipsFilter { get; private set; } = "";
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
    /// <summary><b>Resolved.</b> Null outside <c>--freecam</c>/<c>--anim-lab</c>: the shared
    /// selection lives in the two world-observation modes, the viewer's LMB is already the orbit
    /// drag, and flight has no cursor.</summary>
    public string? DebugSelect { get; private set; }
    /// <summary><b>Resolved.</b> Filtered to the lab's token grammar
    /// (<c>UI.NodeLab.ParseDebugSpec</c>, called with a reject list so it hands them back as data
    /// instead of logging), and null outside <c>--freecam</c>/<c>--anim-lab</c>.</summary>
    public string? DebugNodeLab { get; private set; }
    /// <summary><b>Resolved.</b> Same treatment as <see cref="DebugNodeLab"/>, through
    /// <c>UI.WorldDamageLab.ParseDebugSpec</c>.</summary>
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
    public bool ShowClassOverlay { get; private set; }

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

    /// <summary>Parses the user args and resolves them: the returned spec is the finished answer.
    /// Last occurrence of a flag wins, as it does in the loop this mirrors; an unrecognised arg is
    /// ignored silently, also as today.</summary>
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
            else if (arg == "--viewer") { s._viewerArg = true; s.HasContentArg = true; }
            else if (arg == "--damage") { s._damageLabArg = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--damage="))
            {
                s._damageLabArg = true;
                var rejected = new List<string>();
                s.DamagePreset = ParseDamagePreset(arg["--damage=".Length..], rejected);
                foreach (var bad in rejected)
                {
                    notes.Add(new Note("", $"--damage: cannot parse '{bad}' (want part:fraction)"));
                }
                s.HasContentArg = true;
            }
            else if (arg == "--chapter") { s.ChapterGiven = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--chapter=")) { s.Chapter = arg["--chapter=".Length..]; s.ChapterGiven = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--stage=")) { s.Stage = arg["--stage=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--node=")) { s.NodeName = arg["--node=".Length..]; s.HasContentArg = true; }
            else if (arg == "--fly") { s._flyArg = true; s.HasContentArg = true; }
            else if (arg == "--stunt") { s._stuntArg = true; s.HasContentArg = true; }
            else if (arg == "--freecam") { s._freecamArg = true; s.HasContentArg = true; }
            else if (arg == "--anim-lab") { s._animLabArg = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--play-anim=")) { s.PlayAnim = arg["--play-anim=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--seed=")) { s.Seed = ulong.Parse(arg["--seed=".Length..]); }
            else if (arg == "--debug-anim-ui") { s.DebugAnimUi = true; s.HasContentArg = true; }
            else if (arg == "--debug-anim") { s.DebugAnim = true; }
            else if (arg == "--no-pads") { s.NoPads = true; }
            else if (arg == "--det") { s._detArg = true; }
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
            else if (arg == "--debug-classoverlay") { s.ShowClassOverlay = true; }
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
            else if (arg == "--damage-test") { s.DamageTest = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--damage-test=")) { s.DamageTest = true; s.DamageTestFilter = arg["--damage-test=".Length..]; s.HasContentArg = true; }
            else if (arg.StartsWith("--damage-hd=")) { s.DamageHd = Flt(arg["--damage-hd=".Length..]); }
            else if (arg == "--effects-test") { s.EffectsTest = true; s.HasContentArg = true; }
            else if (arg.StartsWith("--destroy=")) { s.DestroyName = arg["--destroy=".Length..]; }
            else if (arg == "--crash") { s.CrashFrame = DefaultCrashFrame; }
            else if (arg.StartsWith("--crash=")) { s.CrashFrame = int.Parse(arg["--crash=".Length..]); }
            else if (arg.StartsWith("--loadout=")) { s.LoadoutOverride = arg["--loadout=".Length..]; }
            else if (arg == "--infinite-ammo")
            {
                s.InfiniteAmmo = true;
                if (s.AmmoCap != null)
                {
                    notes.Add(new Note("weapons", "--infinite-ammo overrides the earlier --ammo=; using infinite ammo"));
                    s.AmmoCap = null;
                }
            }
            else if (arg.StartsWith("--ammo="))
            {
                s.AmmoCap = int.Parse(arg["--ammo=".Length..]);
                if (s.InfiniteAmmo)
                {
                    notes.Add(new Note("weapons", "--ammo= overrides the earlier --infinite-ammo; using the capped load"));
                    s.InfiniteAmmo = false;
                }
            }
            else if (arg == "--incoming") { s.IncomingPass = IncomingFire.DefaultPass; }
            else if (arg.StartsWith("--incoming="))
            {
                var parts = arg["--incoming=".Length..].Split(',');
                s.IncomingPass = parts[0].Length > 0 ? Flt(parts[0]) : IncomingFire.DefaultPass;
                if (parts.Length > 1 && parts[1].Length > 0)
                    s.IncomingWeapon = parts[1];
            }
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
            else if (arg.StartsWith("--mips=")) { s.SetMips(arg["--mips=".Length..]); }
            else if (arg == "--dump-mips") { s.DumpMips = true; }
            else if (arg.StartsWith("--dump-mips=")) { s.DumpMips = true; s.DumpMipsFilter = arg["--dump-mips=".Length..]; }
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
            else if (arg.StartsWith("--export-gltf=")) { s.ExportGltfPath = arg["--export-gltf=".Length..]; s.HasContentArg = true; }
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

        s._notes = notes;
        s.Deprecated = deprecated;
        s.LogSpecs = logSpecs;
        s.TexOverrides = texOverrides;
        s.Resolve();
        return s;
    }

    /// <summary>The spec for a launchscreen launch: the picked chapter, one plane per player, and
    /// whether it is a stunt run. The menu always launches flight over a chapter world, whatever
    /// mode the command line asked for — one plane each, the player count stated by the list.
    ///
    /// <para><b>Derived from <paramref name="cli"/>, the PRISTINE command line, never from the spec
    /// the last session ran with.</b> Nothing a previous launch settled can reach this one, so
    /// "clear the state the last run left" stops being a patch somebody has to remember and becomes
    /// the shape of the thing. The base is a parameter rather than <c>this</c> precisely so it
    /// cannot quietly become the live spec again.</para>
    ///
    /// <para>⚠ <b>It does not re-resolve.</b> No mode arbitration re-runs (a <c>--viewer</c> vote
    /// would win a second time and the menu would stop launching flight) and placement is not
    /// re-routed (<c>--pos</c> was routed to the camera at parse time under a non-flight mode, and
    /// re-routing it here would start moving the menu's plane). Both match what the launchscreen has
    /// always done: it overwrites an answer, it does not ask the question again.</para></summary>
    public static SessionSpec FromMenu(SessionSpec cli, string chapter, IReadOnlyList<string> planeNodes,
        bool stunt)
    {
        var names = planeNodes.ToArray();
        return cli with
        {
            Chapter = chapter,
            PlaneNames = names,
            // An empty pick cannot come from the launchscreen (it launches only when every joined
            // slot is locked, and it needs at least one), so this falls back to the command line's
            // plane rather than to whatever the last session flew.
            PlaneName = names.Length > 0 ? names[0] : cli.PlaneName,
            Players = Mathf.Clamp(names.Length, 1, UI.SplitScreen.MaxPlayers),
            Stunt = stunt,
            Mode = SessionMode.Fly,
            WorldMode = true,
            Scenario = cli.ScenarioExplicit ? cli.Scenario : stunt ? "stunt_flying" : "zeppelin_run",
        };
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

    /// <summary>Turns the parsed votes into the one answer each: the mode, its modifiers, the world
    /// selection, the player count, the <c>--det</c> bundle's pinned values and the placement
    /// routing. Runs once, from <see cref="Parse"/>, on a spec that has not escaped yet.
    ///
    /// <para>The step ORDER is the behaviour, and it is the order <c>_Ready</c> ran these in. Two
    /// places depend on it in a way that is easy to undo by tidying: <c>--stunt</c> moves
    /// <see cref="Scenario"/> BEFORE arbitration can clear <see cref="Stunt"/>, so
    /// <c>--anim-lab --stunt</c> still ends up on the stunt spawn list; and the <c>--freecam</c>/
    /// <c>--anim-lab</c>-only debug tools are dropped AFTER <c>--node=</c> has forced the viewer,
    /// so <c>--node= --debug-select=</c> loses the tool.</para></summary>
    private void Resolve()
    {
        // Every flag that votes for a mode, gathered before anything is arbitrated. The probes vote
        // like the rest: they need a world without an aircraft (or a parked plane to shoot at), and
        // asking for it through the mode is how they get one.
        bool fly = _flyArg || _stuntArg;
        bool stunt = _stuntArg;
        bool damageLab = _damageLabArg;
        bool viewer = _viewerArg || MarkersOverlay || WeaponLab
            || WeaponMount != null || WeaponFire || WeaponTest;
        bool freecam = _freecamArg || DamageTest || EffectsTest;
        bool animLab = _animLabArg || PlayAnim != null || DebugAnimUi;

        // --stunt is free flight over the mission's danger zones: the flight path plus the
        // stunt_flying spawn list, unless the tester pinned another scenario for a specific spawn.
        if (stunt && !ScenarioExplicit)
        {
            Scenario = "stunt_flying";
        }
        // --anim-lab is the animation debugger's stage: the chapter world under the lab's own
        // clock, with no flight controller. The most specific mode of all, so it wins outright —
        // combining it with a flight/viewer/spectator mode is a contradiction.
        if (animLab && (fly || viewer || freecam))
        {
            Print("--anim-lab is the animation lab; ignoring --fly/--stunt/--viewer/--damage/--freecam");
            fly = stunt = viewer = damageLab = freecam = false;
        }
        // --freecam is the spectator world view: not flight (no aircraft) and not the parked-plane
        // viewer. Asking for a plane-less world AND a plane is a contradiction either way.
        if (freecam && (fly || viewer))
        {
            Print("--freecam is a world view with no aircraft; ignoring --fly/--stunt/--viewer/--damage");
            fly = stunt = viewer = damageLab = false;
        }
        // The damage lab has two hosts now — the parked plane and the flown one — so --damage
        // asks for a lab, not for a mode. It only picks the parked viewer when nothing else
        // claimed the session, which keeps a bare --damage= meaning what it always did.
        if (damageLab && !fly && !freecam && !animLab)
        {
            viewer = true;
        }
        // Flight is the default for any content arg; --viewer opts out into the static inspection
        // view. The explicit --viewer wins, since a bare --fly is now just the default spelled out.
        if (viewer && fly)
        {
            Print("--viewer and --fly/--stunt are opposites (flight is the default); using --viewer");
            fly = stunt = false;
        }
        // --node= is a single-subtree INSPECTION stage: the static viewer unless the anim lab was
        // asked for. Neither flight nor the spectator view has anything to do with one object.
        if (NodeName != null && !animLab)
        {
            if (fly || stunt || freecam)
            {
                Print("--node= is a single-subtree inspection stage; ignoring --fly/--stunt/--freecam");
                fly = stunt = freecam = false;
            }
            viewer = true;
            ChapterGiven = true; // the subtree comes out of the chapter's gamez
        }
        if (HasContentArg && !viewer && !freecam && !animLab)
        {
            fly = true;
        }
        Mode = animLab ? SessionMode.AnimLab
            : freecam ? SessionMode.Freecam
            : viewer ? SessionMode.Viewer
            : fly ? SessionMode.Fly
            : SessionMode.Menu;
        Stunt = stunt;
        DamageLab = damageLab;

        // The numpad views orbit a FLYING plane; the other modes have their own cameras (the
        // viewer's orbit, the spectator freecam) placed with --pos/--direction instead.
        if (View != 0 && !Fly)
        {
            Warn("core", $"--view={View} is a flight camera; ignoring it outside --fly/--stunt");
            View = 0;
        }
        bool observing = Mode == SessionMode.Freecam || Mode == SessionMode.AnimLab;
        if (DebugSelect != null && !observing)
        {
            Warn("ui", "--debug-select is a --freecam/--anim-lab tool; ignoring it here");
            DebugSelect = null;
        }
        if (DebugNodeLab != null && !observing)
        {
            Warn("ui", "--debug-nodelab is a --freecam/--anim-lab tool; ignoring it here");
            DebugNodeLab = null;
        }
        if (DebugDamage != null && !observing)
        {
            Warn("ui", "--debug-damage is a --freecam/--anim-lab tool; ignoring it here (--damage-test is the headless twin)");
            DebugDamage = null;
        }
        // The two lab spec grammars live in their labs; the reject list keeps them from logging,
        // which is what lets this run with no engine under it.
        DebugNodeLab = FilterSpec(DebugNodeLab, UI.NodeLab.ParseDebugSpec, "--debug-nodelab token",
            "is not deps/dest/open/all/node=<cs_name>");
        DebugDamage = FilterSpec(DebugDamage, UI.WorldDamageLab.ParseDebugSpec, "--debug-damage step",
            "is not node=/pool=/hp=/kill/reset/tick=/open");

        // --stage= replaces the chapter world outright, so it is a flight/spectator affair: there
        // is no gamez to inspect, which is what the static viewer and the anim lab exist for.
        if (Stage != null)
        {
            if (!string.Equals(Stage, "empty", StringComparison.OrdinalIgnoreCase))
            {
                Print($"--stage='{Stage}' is not a known stage (only 'empty'); ignoring");
            }
            else if (Mode == SessionMode.Viewer || Mode == SessionMode.AnimLab || NodeName != null)
            {
                Print("--stage=empty has no gamez to inspect; ignoring it in --viewer/--anim-lab/--node=");
            }
            else
            {
                EmptyStage = true;
            }
        }
        // The static viewer shows a chapter world when asked for one, else the parked plane.
        // Flight, the spectator view and the anim lab always need the world built.
        WorldMode = !EmptyStage && (Fly || Freecam || AnimLab || (Viewer && ChapterGiven));
        // A --plane= list of several aircraft states the player count on its own; an explicit
        // --players= still wins.
        if (!PlayersExplicit && PlaneNames.Count > 1)
        {
            Players = PlaneNames.Count;
        }
        Players = Mathf.Clamp(Players, 1, UI.SplitScreen.MaxPlayers);
        if (Players > 1 && !Fly)
        {
            Print($"--players={Players} needs flight (nothing to fly in --viewer); using 1");
            Players = 1;
        }
        if (DamageLab && WorldMode)
        {
            Print("--damage is the plane lab (use --viewer --plane without --chapter); ignoring");
            DamageLab = false;
        }

        // The --det bundle. Det/PadsDisabled/SeedPinned are computed from what is settled by now;
        // the two values it PINS are the only ones that have to be written down.
        if (Det && SpawnIndex < 0)
        {
            SpawnIndex = 0;
        }
        if (JitterDeg < 0f)
        {
            JitterDeg = ScreenshotShots > 1 && !Det ? 0.15f : 0f;
        }

        ResolvePlacement();
    }

    /// <summary>Routes <c>--pos</c>/<c>--direction</c> — the one placement pair — onto the per-mode
    /// plumbing that already carries placement: the plane's spawn override in flight, the camera's
    /// placement everywhere else. Routing happens HERE, in one place, so no consumer downstream has
    /// to ask what mode it is in or whether it holds a point or a vector.
    ///
    /// <para>The superseded spellings keep their old per-mode reach, so <c>--campos</c> still places
    /// only a camera (never the plane) and <c>--spawn-at</c> still moves the anim lab's parked stage
    /// prop. <c>--lookat</c> names a POINT and <c>--direction</c> a VECTOR; the conversion is
    /// one-way and flight-only, because the orbit view PIVOTS on the point and no direction can
    /// express that.</para></summary>
    private void ResolvePlacement()
    {
        if (Fly && Direction == null && LookAt is { } aimPoint && (Pos ?? SpawnAt) is { } eye)
        {
            Direction = aimPoint - eye;
        }
        if (Direction is { } aim)
        {
            Direction = aim.LengthSquared() > 1e-6f ? aim.Normalized() : null;
        }
        if (Pos is { } place)
        {
            if (Fly)
            {
                SpawnAt = place;
            }
            else
            {
                CamPos = place;
            }
        }
        if (Direction is { } dir)
        {
            if (Fly)
            {
                SpawnDir = dir;
            }
            else
            {
                CamDir = dir;
            }
        }
        // A nose direction with nothing to place it on is a silently ignored argument: the spawn
        // override only engages when a position was given.
        if (Fly && SpawnDir != null && SpawnAt == null)
        {
            Warn("core", "--direction ignored: flight steers the nose from the spawn override, which needs --pos");
        }
        // The empty stage has no mission spawn list to draw from, so the subject starts over the
        // grid origin — through the same fields --pos resolves into, so an explicit placement wins.
        if (EmptyStage)
        {
            if (Fly)
            {
                SpawnAt ??= new Vector3(0f, Mech3.EmptyStage.SpawnAltitude, 0f);
            }
            else
            {
                CamPos ??= Mech3.EmptyStage.CameraPos;
            }
        }
    }

    /// <summary>Runs a lab's own token filter over a spec value without letting it log.</summary>
    private string? FilterSpec(string? spec, Func<string, List<string>?, string> filter,
        string what, string wanted)
    {
        if (spec == null)
        {
            return null;
        }
        var rejected = new List<string>();
        string kept = filter(spec, rejected);
        foreach (var token in rejected)
        {
            Warn("ui", $"{what} '{token}' {wanted} — ignoring it");
        }
        return kept;
    }

    /// <summary>Reads <c>--mips=</c>. An unreadable value keeps the current policy rather than
    /// quietly falling back to one of them — a run whose mip source is not what was asked for is a
    /// run whose pixel evidence means nothing.</summary>
    private void SetMips(string value)
    {
        if (Enum.TryParse<TextureArchive.MipSource>(value, ignoreCase: true, out var source))
        {
            Mips = source;
        }
        else
        {
            Warn("world", $"--mips='{value}' is neither 'authored' nor 'generated' — "
                          + $"keeping {Mips.ToString().ToLowerInvariant()}");
        }
    }

    private void Warn(string category, string message) => _notes.Add(new Note(category, message));

    /// <summary>A complaint that is a bare console line today, with no log category.</summary>
    private void Print(string message) => _notes.Add(new Note("", message));

    /// <summary>A parse- or resolve-time complaint, held rather than logged so the spec stays
    /// engine-free. <paramref name="Category"/> is the <c>Log</c> category to emit it under, or
    /// empty for the ones that are bare console lines today (<c>GD.Print</c>).</summary>
    public readonly record struct Note(string Category, string Message);
}

using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Godot;

namespace CSVM.Session;

internal sealed class FlightRosterPolicy
{
    public (FlightInput Input, float Duration)[][]? HoldSets { get; init; }
    public IReadOnlyList<LoadoutChoice?> MenuLoadouts { get; init; } = Array.Empty<LoadoutChoice?>();
    public IReadOnlyList<CustomPlaneDef?> MenuCustomPlanes { get; init; } = Array.Empty<CustomPlaneDef?>();
    public IReadOnlyList<string> PlaneNames { get; init; } = Array.Empty<string>();
    public string PlaneName { get; init; } = "player_bhawk";
    public string Chapter { get; init; } = "";
    public string Mission { get; init; } = "";
    public string Scenario { get; init; } = "";
    public string? LoadoutOverride { get; init; }
    public string? RocketOverride { get; init; }
    public string? TargetSelect { get; init; }
    public string HudFontTestText { get; init; } = "";
    public int AnimLod { get; init; }
    public int GunSelect { get; init; }
    public int View { get; init; }
    public PilotViewMode ViewMode { get; init; }
    public int? AmmoCap { get; init; }
    public int? AiAttackSkill { get; init; }
    public bool AiAttackSkillExplicit { get; init; }
    public bool AutoFire { get; init; }
    public bool AutoFireRockets { get; init; }
    public bool DebugMarkers { get; init; }
    public bool DebugScoreboard { get; init; }
    public bool EmptyStage { get; init; }
    public bool HudFontTest { get; init; }
    public bool InfiniteAmmo { get; init; }
    public bool NoAssist { get; init; }
    public bool WeaponLab { get; init; }

    public static FlightRosterPolicy From(SessionSpec spec) => new()
    {
        HoldSets = spec.HoldSets,
        MenuLoadouts = spec.MenuLoadouts,
        MenuCustomPlanes = spec.MenuCustomPlanes,
        PlaneNames = spec.PlaneNames,
        PlaneName = spec.PlaneName,
        Chapter = spec.Chapter,
        Mission = spec.Mission,
        Scenario = spec.Scenario,
        LoadoutOverride = spec.LoadoutOverride,
        RocketOverride = spec.RocketOverride,
        TargetSelect = spec.TargetSelect,
        HudFontTestText = spec.HudFontTestText,
        AnimLod = spec.AnimLod,
        GunSelect = spec.GunSelect,
        View = spec.View,
        ViewMode = spec.ViewMode,
        AmmoCap = spec.AmmoCap,
        AiAttackSkill = spec.AiAttackSkill,
        AiAttackSkillExplicit = spec.AiAttackSkillExplicit,
        AutoFire = spec.AutoFire,
        AutoFireRockets = spec.AutoFireRockets,
        DebugMarkers = spec.DebugMarkers,
        DebugScoreboard = spec.DebugScoreboard,
        EmptyStage = spec.EmptyStage,
        HudFontTest = spec.HudFontTest,
        InfiniteAmmo = spec.InfiniteAmmo,
        NoAssist = spec.NoAssist,
        WeaponLab = spec.WeaponLab,
    };
}

internal sealed class AircraftAssemblyResources
{
    public GameZ PlanesGamez { get; init; } = null!;
    public Func<string, PlaneStats> StatsFor { get; init; } = null!;
    public Func<string, string?, PlaneStats> AiStatsFor { get; init; } = null!;
    public Func<string, CamParams> CamParamsFor { get; init; } = null!;
    public WeaponDefs WeaponDefs { get; init; } = null!;
    public Messages WeaponMessages { get; init; } = null!;
    public StockLoadouts StockLoadouts { get; init; } = null!;
    public TurretDefs? TurretDefs { get; init; }
    public ShakeDefs Shakes { get; init; } = null!;
    public TextureArchive Textures { get; init; } = null!;
    public HudFont? HudFont { get; init; }
    public Texture2D? ReticleTex { get; init; }
    public RandomNumberGenerator PaintRng { get; init; } = null!;
    public string ZrdrPath { get; init; } = "";
}

internal sealed class FlightWorldBindings
{
    public ProjectilePool Projectiles { get; init; } = null!;
    public EffectAmbience Ambience { get; init; } = EffectAmbience.Still;
    public GameZ Gamez { get; init; } = null!;
    public SceneBuilder? WorldScene { get; init; }
    public AnimRuntime? WorldRuntime { get; init; }
    public AnimRuntime? WorldEffects { get; init; }
    public SurfaceDefTable? TouchdownDefs { get; init; }
    public AnimProgram? CrashProgram { get; init; }
    public SoundArchive? Sounds { get; init; }
    public Dictionary<string, SoundDef>? SoundDefs { get; init; }
    public Dictionary<string, SoundGroup>? SoundGroups { get; init; }
    public string ChapterZrdrPath { get; init; } = "";
    public string MissionZrdrPath { get; init; } = "";
    public bool DebugCollision { get; init; }
}

internal sealed class HumanRosterBindings
{
    public int RigCount { get; init; }
    public float MixGain { get; init; } = 1f;
    public int[][]? PadAssignment { get; init; }
    public PauseState PauseState { get; init; } = null!;
    public Func<int, MenuInput> MenuInputFor { get; init; } = null!;
    public bool ExitsToMenu { get; init; }
    public Action ExitSession { get; init; } = null!;
    public List<SpawnPoint>? SpawnList { get; init; }
    public int SpawnBase { get; init; }
    public StuntMission? StuntZones { get; init; }
    public StuntRace? Race { get; init; }
    public VersusMatch? VersusMatch { get; init; }
    public IReadOnlyList<PlayerRig> Rigs { get; init; } = Array.Empty<PlayerRig>();
    public string? InstantActionPlayerPlaneNode { get; init; }
    public bool InstantActionActive { get; init; }
    public bool Coop { get; init; }
    public SmokeScreens? SmokeScreens { get; init; }
}

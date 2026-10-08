using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Launch;

/// <summary>Per-build state threaded through <see cref="GameSession.StartSession"/>'s ordered
/// steps: the archives, the world build's outputs and the running counts. It is the one input the
/// build steps share, so a step reads what an earlier one wrote here rather than a session field.
/// ⚠ Nothing here may be cached across a rebuild.</summary>
internal sealed class BuildState
{
    public string DataRoot = "", ZrdrPath = "", SoundsPath = "", InterpPath = "",
        MessagesPath = "", PlanesGamezPath = "";
    public bool Mute, DebugCollision;
    public string TexturesPath = "", GamezPath = "", MissionZrdrPath = "";

    public GameZ Gamez = null!;
    public TextureArchive Textures = null!;
    public SoundArchive? Sounds;
    public Dictionary<string, SoundDef>? SoundDefs;
    public Dictionary<string, SoundGroup>? SoundGroups;
    // Set by LoadArchives from the ArchiveIntent (Session/Lab) it opened the archives for,
    // BuildWorldStage's WorldSession.Options carries them through unchanged.
    public bool TexturesOutliveBuild;
    public bool SoundsOutliveBuild;
    // The archives that outlive this build scope in the anim lab, whose node owns their disposal.
    // A failed build closes them from StartSession's catch instead.
    public TextureArchive? LabTextures;
    public SoundArchive? LabSounds;
    public UI.Labs.AnimLab? AnimLabNode;

    public GameZNode? NodeSubtree;
    // The --node= subtree's world-frame box, measured at build time for the closing framing step.
    // FrameCamera says why the live-tree merge is the wrong instrument.
    public Aabb? NodeAabb;

    public int MeshInstances;
    // The intro's staged prop aircraft, counted apart because the world builder never saw it:
    // it comes off the aircraft archive on its own SceneBuilder (Mech3/AircraftStage.cs).
    public int StagedAircraftMeshes;
    // The same stage, kept for the roster build. A staged prop takes the livery of the aeroplane
    // it stands in for once the rigs that resolved it exist.
    public AircraftStage? Aircraft;
    public int Colliders;
    public string What = "";

    public Node3D? CloudDeck;
    public IReadOnlyDictionary<Rid, ArrayMesh>? DeckUndimmedMeshes;
    public AnimProgram? CrashProgram;
    public SceneBuilder? WorldScene;
    public AnimRuntime? WorldRuntime;
    public CraterField? Craters;
    public Effects.ScorchField? Scorches;

    /// <summary>The chapter's resolved approach rows, kept so the actor build can re-bind the
    /// trigger once the roster's own approach nodes exist (<see cref="Mech3.RosterMarkers"/>).
    /// </summary>
    public IReadOnlyList<LandingApproach>? Landings;

    public IReadOnlyList<PickupSpec>? Pickups;
}

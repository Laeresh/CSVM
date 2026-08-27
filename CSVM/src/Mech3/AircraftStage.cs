using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The aircraft-archive subtrees a story-mission intro or a chuteman-carrying cutscene animates,
/// staged into a chapter world so the animation runtime's node table can reach them:
/// <c>piratefighter</c> as a prop with no pilot, a bodiless <c>player</c> the flown aircraft
/// follows, and <c>chuteman</c>'s parachutist subtree. All three come from the shared aircraft
/// archive rather than the chapter gamez, so their compiled pointers are rebased onto the
/// chapter's own pointer space (<see cref="PointerBaseOf"/>).
/// Decode: docs/formats/anim-definitions/cutscenes.md.
/// </summary>
public sealed class AircraftStage
{
    /// <summary>The aircraft-archive node an intro poses as the player's own aeroplane. A
    /// parentless wrapper in the archive, so CSVM stands a bodiless marker in for it and the flown
    /// airframe takes its pose; what the wrapper holds there is an artefact of the archive build,
    /// not a statement about which aeroplane the intro is about.</summary>
    public const string PlayerNode = "player";

    /// <summary>The aircraft-archive node an intro stages under <c>piratezep</c> and flies with
    /// <c>gi_pfighter1</c>/<c>gi_pfighter2</c> — the Devastator's remote model, carrying no
    /// pilot and no flight model.</summary>
    public const string PropNode = "piratefighter";

    /// <summary>The aircraft-archive node a mid-mission drop/hookup cutscene reparents under its
    /// own aiming node and activates: a parentless wrapper whose <c>chutemanparent</c> child carries
    /// the visible <c>pilot</c> mesh and the parachute's <c>stamp</c>. Ships <c>INACTIVE</c>
    /// (the shared <c>chuteman</c> def's own <c>RESET_STATE</c>), the same base state
    /// <see cref="PropNode"/> ships in.</summary>
    public const string ChuteNode = "chuteman";

    /// <summary>The block a chapter's pointer base is rounded up to. Measured over all eight
    /// chapters; no site in the executable computing it has been traced, so a ninth chapter's base
    /// is a prediction rather than a reading.</summary>
    public const int PointerBlock = 2500;

    private AircraftStage() { }

    /// <summary>The bodiless <c>player</c> marker, or null when the archive carries no such node.
    /// The flown aircraft is posed onto it while the cutscene holds the player out of flight.
    /// </summary>
    public Node3D? PlayerMarker { get; private set; }

    /// <summary>The staged <c>piratefighter</c> subtree, built switched off: the intro's own
    /// <c>gi_pfighter1</c> is what activates it. Null when the archive carries no such node.
    /// </summary>
    public Node3D? Prop { get; private set; }

    /// <summary>The staged <c>chuteman</c> subtree, built switched off: a mid-mission drop's own
    /// called animation (e.g. C3/M01's <c>tdchute</c>) is what reparents and activates it. Null
    /// when the archive carries no such node.</summary>
    public Node3D? Chuteman { get; private set; }

    /// <summary>Mesh instances the prop build added, for the session's build summary.</summary>
    public int MeshInstances { get; private set; }

    /// <summary>Where a chapter's own node table ends and the shared aircraft archive's begins: the
    /// chapter's node count rounded up to the next multiple of <see cref="PointerBlock"/>. Holds for
    /// all eight extracted chapters.</summary>
    public static int PointerBaseOf(int chapterNodeCount) =>
        ((chapterNodeCount / PointerBlock) + 1) * PointerBlock;

    /// <summary>Builds both aircraft under <paramref name="worldRoot"/> and rebases their compiled
    /// pointers onto <paramref name="chapterNodeCount"/>'s archive block, so the runtime's symbol
    /// table binds the names an intro definition addresses. Runs before the animation bind, beside
    /// the cutscene roots.</summary>
    public static AircraftStage Build(Node3D worldRoot, int chapterNodeCount, GameZ planesGamez,
        TextureArchive textures)
    {
        var stage = new AircraftStage();
        int pointerBase = PointerBaseOf(chapterNodeCount);
        if (planesGamez.FindByName(PlayerNode) is { } player)
        {
            var marker = new Node3D { Name = PlayerNode };
            marker.SetMeta(AnimRuntime.NameMeta, player.Name);
            marker.SetMeta(AnimRuntime.IndexMeta, player.Index + pointerBase);
            worldRoot.AddChild(marker);
            stage.PlayerMarker = marker;
        }

        // One builder over the aircraft archive, shared by every archive subtree this stage builds:
        // the world's own SceneBuilder reads the chapter gamez's meshes and materials, which none of
        // these subtrees' model indices address.
        var scene = new SceneBuilder(planesGamez, textures, cullBackfaces: true);
        if (planesGamez.FindByName(PropNode) is { } prop
            && scene.BuildSubtree(prop, collisionSkip: _ => true) is { } builtProp)
        {
            builtProp.Transform = Transform3D.Identity;
            Rebase(builtProp, pointerBase);
            AnimRuntime.SetSubtreeActive(builtProp, false);
            worldRoot.AddChild(builtProp);
            stage.Prop = builtProp;
        }

        if (planesGamez.FindByName(ChuteNode) is { } chute
            && scene.BuildSubtree(chute, collisionSkip: _ => true) is { } builtChute)
        {
            builtChute.Transform = Transform3D.Identity;
            Rebase(builtChute, pointerBase);
            AnimRuntime.SetSubtreeActive(builtChute, false);
            worldRoot.AddChild(builtChute);
            stage.Chuteman = builtChute;
        }

        stage.MeshInstances = scene.MeshInstanceCount;

        string marked = stage.PlayerMarker != null ? PlayerNode : $"no {PlayerNode}";
        string staged = stage.Prop != null ? PropNode : $"no {PropNode}";
        string chuted = stage.Chuteman != null ? ChuteNode : $"no {ChuteNode}";
        Log.Info("world", $"aircraft stage: base {pointerBase} over {chapterNodeCount} chapter node(s), {marked}, {staged}, {chuted}, {stage.MeshInstances} mesh instances");
        return stage;
    }

    // Every built node's stamped archive index shifted into the chapter's cross-archive block. The
    // two spaces cannot overlap, since the base always clears the chapter's own table.
    private static void Rebase(Node3D node, int pointerBase)
    {
        if (node.HasMeta(AnimRuntime.IndexMeta))
        {
            node.SetMeta(AnimRuntime.IndexMeta, (int)node.GetMeta(AnimRuntime.IndexMeta) + pointerBase);
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3d)
            {
                Rebase(n3d, pointerBase);
            }
        }
    }
}

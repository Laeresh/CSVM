using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>A motion that owns one of a node's channels over time (an SI script playback, a
/// from→to tween, a ballistic body, or an opacity fade). The runtime ticks them centrally so
/// a node driven by two motions on the SAME channel resolves to the later one
/// deterministically.</summary>
internal interface IAnimMotion
{
    Node3D Target { get; }

    // The (def, anchor) instance that registered this motion, so Stop can tear down exactly
    // the motions a stopped instance drives. Set by MotionSet.Add; ownership transfers when a
    // later instance's motion replaces an earlier one on the same target.
    (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    bool Finished { get; }

    // Almost every motion drives the transform; only OpacityFade overrides this. A default
    // interface member, so a transform motion need not declare it.
    MotionChannel Channel => MotionChannel.Transform;

    void Tick(float dt);

    void Seek(float t);
}

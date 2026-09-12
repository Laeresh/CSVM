namespace CSVM.Mech3.Anim;

/// <summary>Which of a node's channels a motion drives. A node carries at most ONE motion
/// per channel (see <c>MotionSet.Add</c>): the transform channel (translate/rotate/scale,
/// all held in <c>Target.Transform</c>) and the opacity channel (the <c>csky_opacity</c>
/// shader parameter). They are independent, the crash dust ramps its scale via a
/// <see cref="MotionRuntime"/> while an <see cref="OpacityFade"/> fades it out, so a fade
/// must not evict a live transform motion, nor a transform motion a live fade.</summary>
internal enum MotionChannel { Transform, Opacity }

using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>One aircraft's propeller presentation slot, the original's <c>+0x6cc</c>. Either the
/// spin definition turns the blur discs, or the stop definition has wound them down to the still
/// blade and its <c>snd_propstop</c>. The engine-out edges, the death routine and the spawn are its
/// only writers. Each passes the rig and the model it acts on. The slot asks for the rig only when
/// it changes, so a slot already in the wanted state never forces an armed rig's build.
/// Decode: docs/org/ordnanceTypes.md.</summary>
public sealed class PropellerSlot
{
    private PlaneStats? _stats;

    /// <summary>Whether the stopped presentation holds the slot, the still blade shown and the blur
    /// discs faded out.</summary>
    public bool Stopped { get; private set; }

    /// <summary>The definition the airframe's def names as <c>spin_props_anim</c>, which the spawn
    /// and the choke's restart start. <c>spinprops</c> when the def names none.</summary>
    public string SpinAnim => _stats?.SpinPropsAnim ?? EffectCatalogue.DefaultSpinPropsAnim;

    /// <summary>The definition the airframe's def names as <c>stop_props_anim</c>, which the choke
    /// and the death start. <c>stopprops</c> when the def names none.</summary>
    public string StopAnim => _stats?.StopPropsAnim ?? EffectCatalogue.DefaultStopPropsAnim;

    /// <summary>Names the airframe whose two definitions the slot starts. Unbound, both names are
    /// the defaults.</summary>
    public void Bind(PlaneStats? stats) => _stats = stats;

    /// <summary>Both bit-2 edges of the original's disabled-systems mask, read off the engine-out
    /// state. A dead engine winds the slot down, and a running one spins a stopped slot back up.
    /// <paramref name="rig"/> is asked only on a change.</summary>
    public void Sync(bool engineDead, Node3D? model, Func<AnimRuntime?> rig)
    {
        if (engineDead)
        {
            Stop(model, rig);
            return;
        }

        if (!Stopped || model == null || rig() is not { } spinRig)
            return;
        Spin(spinRig, model);
    }

    /// <summary>The wind-down half. ⚠ It refuses while the stopped presentation already holds the
    /// slot. The original starts nothing when its stop handle is occupied. That keeps a choked
    /// aircraft's crash from replaying the fade and its <c>snd_propstop</c>.</summary>
    public void Stop(Node3D? model, Func<AnimRuntime?> rig)
    {
        if (Stopped || model == null || rig() is not { } stopRig)
            return;
        stopRig.Stop(SpinAnim);
        stopRig.Play(StopAnim, model);
        Stopped = true;
    }

    /// <summary>The spin start, silent and instant, shared by the spawn and the choke's restart.
    /// The original's falling edge runs the spin definition and never <c>startprops</c>. Its
    /// <c>snd_propstart</c> plays nowhere in the original.</summary>
    public void Spin(AnimRuntime rig, Node3D model)
    {
        string spin = SpinAnim;
        // ⚠ Take the stop definition off first. A hull that went down stopped would otherwise fly
        // again with the wind-down still fading staticpropN in.
        rig.Stop(StopAnim);
        // ⚠ Suppressed, or the def's endless XYZ_ROTATION becomes a second writer on the same
        // disc transforms PropAnimator turns at those very rates.
        rig.SuppressedMotionAnims.Add(spin);
        // ⚠ Keep the reset: agyro_rotors activates prop1/rotor1 only in its RESET_STATE, which the
        // original's start runs. Without it a choked autogyro restarts with no propeller.
        rig.Play(spin, model);
        RestoreDiscOpacity(rig, model);
        Stopped = false;
    }

    /// <summary>A fresh airframe's slot: spun on <paramref name="builtRig"/> when one is already
    /// built, and running whatever the last hull ended on. ⚠ Pass the built rig, never a forcing
    /// read. A spawn must not build an armed rig on its placement frame.</summary>
    public void Respawned(AnimRuntime? builtRig, Node3D? model)
    {
        if (builtRig != null && model != null)
            Spin(builtRig, model);
        Stopped = false;
    }

    // The spin definition re-activates the blur discs and writes no opacity. The wind-down it
    // reverses faded those same discs to zero. The restart puts the alpha back itself, or the
    // aeroplane comes out of a choke with its propellers turning invisibly.
    private static void RestoreDiscOpacity(AnimRuntime rig, Node node)
    {
        if (node is Node3D n3d && PropParts.Spin(PropParts.Classify(AnimRuntime.NameOf(n3d)), out _, out _))
            rig.SetSubtreeOpacity(n3d, 1f);
        for (int i = 0, count = node.GetChildCount(); i < count; i++)
            RestoreDiscOpacity(rig, node.GetChild(i));
    }
}

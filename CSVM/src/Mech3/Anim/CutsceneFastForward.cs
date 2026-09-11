using System;
using System.Collections.Generic;

namespace CSVM.Mech3.Anim;

/// <summary>
/// The rate one cutscene episode's own definitions run at while the player holds a key through a
/// scene that offers no skip: a multiplier that spools up to <see cref="Target"/> over
/// <see cref="RampSeconds"/> and back down on release. Scoped to the episode's call closure, so
/// the world outside it keeps real time and every channel inside it is carried by the same
/// number. A remake-only rule: the original ends such a scene in no way at all, and neither the
/// rate nor the ramp is read off it. What it stands beside:
/// docs/formats/anim-definitions/cutscenes.md, "Handoff and skip".
/// </summary>
public sealed class CutsceneFastForward
{
    /// <summary>The held rate. TUNE: nothing in the original sets it, so this is fast enough to be
    /// worth holding the key for and slow enough to still read the scene.</summary>
    public const float Target = 4f;

    /// <summary>Seconds the rate takes to cross the whole span, either way. TUNE on the same
    /// standing as <see cref="Target"/>: short enough to answer the key, long enough that the
    /// picture and its sound step up rather than jump.</summary>
    public const float RampSeconds = 0.25f;

    /// <summary>Is the key down? The caller decides what an input means and whether this episode
    /// may take one at all; this object only ramps toward the answer.</summary>
    public bool Held;

    private readonly HashSet<AnimDefinition> _scope = new();

    /// <summary>The multiplier as the ramp has it now, 1 at real speed.</summary>
    public float Rate { get; private set; } = 1f;

    /// <summary>Does an episode own this fast-forward? False leaves every definition at 1.</summary>
    public bool Scoped => _scope.Count > 0;

    /// <summary>The definitions the rate applies to: one episode's whole call closure, since a
    /// callee drives as much of the picture as its caller.</summary>
    public void Scope(IEnumerable<AnimDefinition> defs)
    {
        _scope.Clear();
        foreach (var def in defs)
        {
            _scope.Add(def);
        }
    }

    /// <summary>Hands every definition back to real speed at once, with no ramp: the episode is
    /// over, or it turned out to be one the player may really skip.</summary>
    public void Clear()
    {
        _scope.Clear();
        Held = false;
        Rate = 1f;
    }

    /// <summary>Moves the rate one step of <paramref name="dt"/> UNSCALED seconds toward whatever
    /// <see cref="Held"/> asks for. ⚠ Feed it the runtime's own incoming dt, never a dt this
    /// object already multiplied; a rate that ramps on its own output is exponential.</summary>
    public void Ramp(float dt)
    {
        float target = Held && Scoped ? Target : 1f;
        float step = Math.Abs(dt) * ((Target - 1f) / RampSeconds);
        Rate = Rate < target
            ? Math.Min(target, Rate + step)
            : Math.Max(target, Rate - step);
    }

    /// <summary>The multiplier for one definition's dt: the ramped rate for a definition of the
    /// scoped episode, and exactly 1 for every other, so an unscoped runtime multiplies by a
    /// literal 1 and its arithmetic is unchanged.</summary>
    public float RateFor(AnimDefinition? def) =>
        def != null && _scope.Contains(def) ? Rate : 1f;
}

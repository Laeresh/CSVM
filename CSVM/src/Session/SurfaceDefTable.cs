using System;
using System.Collections.Generic;
using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>
/// One of the original's per-surface anim-def vectors — <c>"player_crash_" + name</c> or
/// <c>"touchdown_" + name</c> over every <see cref="SurfaceRegistry.Names"/> slot — plus the
/// cascade that indexes it with a struck material's numeric surface id
/// (<see cref="SceneBuilder.SurfaceIdMeta"/>, ultimately <see cref="GameZMaterial.SoilId"/>).
///
/// <para>The original builds each vector by string concatenation over the registry
/// (<c>FUN_00476250</c> for the crash family, <c>FUN_004735b0</c> for touchdown) and selects with
/// <c>FUN_0048b920</c> <c>0x0048bac5</c>–<c>0x0048bb00</c>: a null struck material, a negative id,
/// an id at or beyond the vector's length, or a slot that names nothing playable all resolve
/// <b>slot 0</b>; a vector that is empty or whose slot 0 is itself empty resolves the bare anim
/// name instead; anything else resolves <c>vector[id]</c>.</para>
///
/// <para><b>The empty-slot arm is the whole mechanism, not a special case.</b> This install ships
/// three defs per family (<c>default</c>/<c>dirt</c>/<c>water</c>), so the other eleven slots name
/// a def that does not exist and fall back to slot 0 — which is why the ordinary ground crash is
/// <c>player_crash_default</c> and <c>_dirt</c> plays only on <c>dirt</c>(13)-tagged material.
/// Which ids fall back is therefore never listed here: it is whatever the bound program happens to
/// define, asked once at construction.</para>
///
/// <para>Engine-free and pure, so the cascade is testable without a scene: the caller supplies
/// both the "does this def exist" test and the id.</para>
/// </summary>
public sealed class SurfaceDefTable
{
    private readonly string?[] _slots;
    private readonly string _lastResort;

    /// <summary>Builds the vector for <paramref name="prefix"/> against the program
    /// <paramref name="defExists"/> answers for. <paramref name="lastResort"/> is the bare anim
    /// name the original falls to when the vector cannot answer at all.</summary>
    public SurfaceDefTable(string prefix, string lastResort, Func<string, bool> defExists)
    {
        _lastResort = lastResort;
        _slots = new string?[SurfaceRegistry.Names.Count];
        var playable = new List<string>();
        for (int id = 0; id < _slots.Length; id++)
        {
            string def = prefix + SurfaceRegistry.Names[id];
            if (!defExists(def))
                continue; // an empty slot: this id falls back to slot 0
            _slots[id] = def;
            playable.Add(def);
        }
        PlayableDefs = playable;
    }

    /// <summary>Every def this vector can reach, in slot order — what a runtime binding this
    /// family has to bind, since the struck surface is only known at the moment of impact.
    /// The registry names are pairwise distinct, so no def appears twice.</summary>
    public IReadOnlyList<string> PlayableDefs { get; }

    /// <summary>The def for a struck surface id, or the bare last-resort anim name.
    /// <paramref name="surfaceId"/> is null for "no struck material" — the original's null-material
    /// arm, which the headless <c>--crash</c> force takes.</summary>
    public string DefForSurfaceId(int? surfaceId)
    {
        // vector[id], when the id is in range (signed lower bound, unsigned upper) and its slot
        // names a def that exists.
        if (surfaceId is { } id && id >= 0 && (uint)id < (uint)_slots.Length && _slots[id] is { } named)
            return named;
        // Otherwise slot 0 — unless the vector itself cannot answer, which is the bare anim name.
        return _slots.Length > 0 && _slots[0] is { } fallback ? fallback : _lastResort;
    }
}

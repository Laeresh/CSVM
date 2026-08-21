using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>One runtime's `SOUND_NODE`/`SOUND` events: the pooled ambient emitters, the one-shot
/// player, the late-failure census, and the sound half of `OBJECT_ADD_CHILD`/
/// `OBJECT_ACTIVE_STATE` that the router still owns the case bodies for.
/// ⚠ `SoundHandledElsewhere` exists because a second runtime (effects) shares the world: read it
/// live on every call, never cache it, since the two runtimes can disagree from one dispatch to
/// the next. `Sounds` itself is read the same way — null before the world build, non-null after,
/// on the very same runtime instance.</summary>
internal sealed class SoundChannel
{
    // Keyed by (sound name, anchor), like the lights and for the same reason: the anchor
    // identifies the *instance* of the definition, so C1's four firetrucks each get their own
    // siren rather than sharing one. It cannot be keyed by host node the way puffers are — in the
    // reader's triple the emitter is declared BEFORE anything says where it goes.
    private readonly Dictionary<(string Name, Node3D? Anchor), object> _soundEmitters = new();

    private readonly HashSet<string> _soundFailuresReported = new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<WorldSounds?> _sounds;

    private readonly Func<bool> _handledElsewhere;

    private readonly Func<string, AnimDefinition, Node3D?, Node3D?> _resolve;

    private readonly Func<Random> _rng;

    private readonly Action _recordApplied;

    private int _soundsUnknown, _soundsAfterBuild;

    // Set once Bootstrap has printed its emitter census. After it, a failed SOUND_NODE is invisible
    // unless reported at the point of use, because the census is a bootstrap snapshot and cannot
    // tell "never requested" from "requested later and failed". Report once per name, not per
    // event; a single sound name has hundreds of sites.
    private bool _soundCensusPrinted;

    public SoundChannel(Func<WorldSounds?> sounds, Func<bool> handledElsewhere,
        Func<string, AnimDefinition, Node3D?, Node3D?> resolve, Func<Random> rng, Action recordApplied)
    {
        _sounds = sounds;
        _handledElsewhere = handledElsewhere;
        _resolve = resolve;
        _rng = rng;
        _recordApplied = recordApplied;
    }

    /// <summary>One-shot SOUND events that resolved to a stream and fired this session (the
    /// destruction/damage/impact audio). Exposed for the damage-test harness, which cannot
    /// screenshot audio: a nonzero delta across a kill is how "the death's explosion sounded" is
    /// verified headless.</summary>
    public int OneShotSoundsPlayed { get; private set; }

    /// <summary>How many ambient emitters this channel has declared, for the bootstrap census.
    /// </summary>
    internal int EmitterCount => _soundEmitters.Count;

    internal int Unknown => _soundsUnknown;

    internal int AfterBuild => _soundsAfterBuild;

    internal IEnumerable<string> Names => _sounds()?.Names ?? Enumerable.Empty<string>();

    /// <summary>Ends the bootstrap-snapshot window: a SOUND_NODE that fails after this point reports
    /// itself instead of waiting for a census that has already printed.</summary>
    internal void MarkCensusPrinted() => _soundCensusPrinted = true;

    /// <summary>The OBJECT_ACTIVE_STATE reach-in: switches a named sound emitter on/off instead of a
    /// gamez node. Returns false when the event's NAME names no declared emitter, so the router can
    /// fall through to its ordinary node-target handling.</summary>
    internal bool TrySetActive(AnimEvent ev, Node3D? anchor)
    {
        if (Emitter(ev, anchor) is not { } handle)
            return false;
        _sounds()!.SetActive(handle, ev.Data.Bool("state"));
        _recordApplied();
        return true;
    }

    /// <summary>The sound-emitter case of OBJECT_ADD_CHILD: whether <paramref name="child"/> names a
    /// declared emitter, and if so its handle. The router resolves the parent node itself; this
    /// channel only knows sound names.</summary>
    internal bool TryGetChild(string child, Node3D? anchor, out object handle) =>
        _soundEmitters.TryGetValue((child, anchor), out handle!);

    internal void Attach(object handle, Node3D host)
    {
        _sounds()!.Attach(handle, host);
        _recordApplied();
    }

    // Declares (and for the compiled form, places and starts) one ambient emitter. The two
    // front-ends spell it differently and both land here: a reader def writes a three-event triple
    // where this event only declares, while a compiled event carries active_state and translate
    // inline, but only on the events that do not leave them to OBJECT_ADD_CHILD
    // (docs/formats/anim-definitions.md).
    internal void HandleSoundNode(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (_handledElsewhere())
            return;
        if (ev.Data.Str("name") is not { } name)
            return;
        var sounds = _sounds();
        if (sounds == null)
        {
            _soundsAfterBuild++;
            ReportLateSoundFailure(name, "there is no audio session");
            return;
        }

        var key = (name, anchor);
        if (!_soundEmitters.TryGetValue(key, out var handle))
        {
            // Re-assertion must be a no-op, not a second emitter: the data keeps its definitions
            // alive with `[SOUND_NODE, …, Loop{-1}]` exactly as it does for puffers.
            if (sounds.Create(name) is not { } created)
            {
                _soundsUnknown++;
                ReportLateSoundFailure(name, "no stream could be resolved for it "
                                             + "(unknown to sounds.json, or never prewarmed)");
                return;
            }
            handle = created;
            _soundEmitters[key] = handle;
            _recordApplied();
        }

        // AT_NODE — the compiled form's own placement. Absent in the reader form and in the 865
        // compiled events that leave it to OBJECT_ADD_CHILD.
        if (ev.Data.Obj("translate")?.Union() is { Tag: "AtNode", Value: Dictionary<string, object?> at })
        {
            var atData = new AnimData(at);
            if (atData.Str("name") is { } hostName && _resolve(hostName, def, anchor) is { } host)
                sounds.Attach(handle, host, atData.Vec3("pos"));
        }
        // ⚠ An absent active_state means "leave it alone", never OFF; the reader form's ACTIVE
        // arrives as the next event. ⚠ The compiled field is a JSON boolean here, where
        // PUFFER_STATE's same-named field is numeric, so Num() alone switches every emitter off.
        if (ev.Data.Has("active_state"))
            sounds.SetActive(handle, ev.Data.Bool("active_state") || ev.Data.Num("active_state") >= 1f);
    }

    // A one-shot SOUND event: the destruction, impact and damage audio a sequence emits. Unlike
    // SOUND_NODE's pooled looping emitters it plays once at a world point and disposes itself.
    // ⚠ The event's NAME is a sounds.json definition or a SOUND_GROUPS name, never a gamez node.
    // The AT_NODE, when present, positions it; absent, it plays at the anchor.
    internal void HandleSound(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (_handledElsewhere())
        {
            return;
        }
        if (ev.Data.Str("name") is not { } name)
        {
            return;
        }
        var sounds = _sounds();
        if (sounds == null)
        {
            _soundsAfterBuild++;
            ReportLateSoundFailure(name, "there is no audio session", "SOUND");
            return;
        }
        if (sounds.PlayOneShot(name, OneShotSoundPosition(ev, def, anchor), _rng()) != null)
        {
            OneShotSoundsPlayed++;
            _recordApplied();
        }
        else
        {
            _soundsUnknown++;
            ReportLateSoundFailure(name, "no stream could be resolved for it (unknown to "
                                         + "sounds.json / SOUND_GROUPS, or never prewarmed)", "SOUND");
        }
    }

    /// <summary>The crash rig's respawn: pauses every declared emitter and forgets it, matching
    /// <see cref="EmitterDirector.Reset"/>'s disposition for puffers.</summary>
    internal void Reset()
    {
        if (_sounds() is { } sounds)
        {
            foreach (var handle in _soundEmitters.Values)
                sounds.SetActive(handle, false);
        }
        _soundEmitters.Clear();
    }

    /// <summary>The sound half of `TearDownResourcesOf`: every emitter this anchor's instance
    /// declared stops and is forgotten, keyed by anchor the same way lights are.</summary>
    internal void DiscardFor(Node3D? anchor)
    {
        if (_sounds() is not { } sounds)
            return;
        foreach (var key in _soundEmitters.Keys.Where(k => k.Anchor == anchor).ToList())
        {
            sounds.SetActive(_soundEmitters[key], false);
            _soundEmitters.Remove(key);
        }
    }

    private void ReportLateSoundFailure(string name, string why, string kind = "SOUND_NODE")
    {
        if (!_soundCensusPrinted || !_soundFailuresReported.Add(name))
        {
            return;
        }
        GD.PushWarning($"anim: {kind} '{name}' requested after the world build and {why} — "
                       + "it will be silent for the rest of the session");
    }

    // The emitter an event's NAME refers to, or null when the name isn't one this definition
    // declared. This is what lets OBJECT_ACTIVE_STATE and OBJECT_ADD_CHILD — both perfectly
    // ordinary node events elsewhere — address a sound emitter without either handler having to
    // guess from the name whether `snd_waterfall` is a node or a sound.
    private object? Emitter(AnimEvent ev, Node3D? anchor)
    {
        if (_sounds() == null || ev.Data.Str("name") is not { } name)
            return null;
        return _soundEmitters.TryGetValue((name, anchor), out var handle) ? handle : null;
    }

    // Where a one-shot SOUND plays: its AT_NODE's world pose plus the trailing offset, or the
    // anchor's when it names no node. The compiled form nests AT_NODE as {name, pos}; the reader
    // form (normalized in AnimDefs) carries a flat at_node name plus a translate offset.
    private Vector3 OneShotSoundPosition(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        Node3D? host = null;
        Vector3 offset = Vector3.Zero;
        if (ev.Data.Obj("at_node") is { } atObj)
        {
            if (atObj.Str("name") is { } hostName)
            {
                host = _resolve(hostName, def, anchor);
            }
            offset = atObj.Vec3("pos");
        }
        else if (ev.Data.Str("at_node") is { } atName)
        {
            host = _resolve(atName, def, anchor);
            offset = ev.Data.Vec3("translate");
        }
        host ??= anchor;
        if (host is not { } h || !GodotObject.IsInstanceValid(h))
            return Vector3.Zero;
        var pos = AnimRuntime.WorldTransform(h, out bool composed) * offset;
        if (composed)
            Log.Info("sound", $"one-shot SOUND '{ev.Data.Str("name")}' positioned by out-of-tree ancestor composition at {pos} (world root not parented at bootstrap)");
        return pos;
    }
}

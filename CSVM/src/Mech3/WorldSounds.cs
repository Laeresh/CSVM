using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The animated world's ambient 3D sound emitters (<c>SOUND_NODE</c>) — the waterfall roar, the
/// train, the firetruck and police sirens, the zeppelin nacelle engines, the fire crackle and the
/// warning beeper. One pooled <see cref="AudioStreamPlayer3D"/> per live emitter, positioned each
/// frame from the world node the animation attached it to.
///
/// Why <c>SOUND_NODE</c> is a separate kind from one-shot <c>SOUND</c>: measured across the whole
/// install the two event kinds are cleanly different animals, and only this one is ambient world
/// audio. <c>SOUND_NODE</c> uses exactly **10 distinct names**, every one of them present in
/// sounds.json, every one <c>3D</c>, and 9 of 10 <c>LOOPED</c> with a RANGE — a looping positional
/// emitter bound to a node, handled by this pool. <c>SOUND</c> is 87 names of one-shot combat/
/// destruction audio, 21 of which are <c>DYNAMIC_WEIGHTS</c> groups; it is handled by the
/// fire-and-forget <see cref="PlayOneShot"/>, which the <c>Sound</c> anim event drives.
///
/// Emitters are pooled here rather than parented into the world subtree they follow, for the same
/// reason puffers are: <see cref="AnimRuntime"/>'s <c>FindAll</c> memoization is sound only while
/// nothing adds or reparents world nodes at runtime. Following the host's pose per frame is exact
/// (the train's whistle travels the whole 327 s track loop) and costs nothing at this count.
/// </summary>
public sealed partial class WorldSounds : Node3D
{
    /// <summary>
    /// Resolves a sound definition's WAV into a stream. Supplied by the caller and valid only
    /// DURING the world build, exactly like <see cref="AnimRuntime.PufferFactory"/>: the session
    /// <see cref="SoundArchive"/> is disposed when the build scope ends. Decoded streams are
    /// cached below, so an emitter created later reusing a name already heard still works; only a
    /// genuinely new name after the build is reported rather than faulting on a closed zip.
    ///
    /// <para><b>The cache is not enough on its own — <see cref="Prewarm"/> is what makes this
    /// safe.</b> "A name already heard" only covers names some emitter happened to
    /// create during bootstrap. Measured install-wide, <b>947 of 1,244</b> SOUND_NODE events sit
    /// in Initial sequences of <c>OnCall</c>-activation defs, so they are first reached at
    /// runtime — always after this field is nulled — and 386 of them name a sound that is never
    /// in a cacheable position at all (<c>snd_fire1</c> ×363, <c>snd_beeper</c> ×16,
    /// <c>snd_firetruck</c>, <c>snd_police</c>, …). Those failed silently. Prewarm decodes every
    /// name the loaded program can ever ask for, while the archive is still open.</para>
    ///
    /// <para>The bool is <c>warn</c>: false on the speculative prewarm decode (a per-chapter archive
    /// legitimately lacks WAVs the program can reference), true at the point of use.</para>
    /// </summary>
    public Func<SoundDef, bool, AudioStreamWav?>? Loader;

    private readonly Dictionary<string, SoundDef> _defs;
    private readonly IReadOnlyDictionary<string, SoundGroup> _groups;
    private readonly Dictionary<string, AudioStreamWav?> _streams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One live emitter: the player plus the world node whose pose it rides.</summary>
    private sealed class Emitter
    {
        public string Name = "";
        public AudioStreamPlayer3D Player = null!;
        public Node3D? Host;      // the node this was attached to (OBJECT_ADD_CHILD / AT_NODE)
        public Vector3 Offset;    // in the host's own frame
        public bool Active;       // OBJECT_ACTIVE_STATE / the compiled event's active_state
        public bool Looped;
    }

    private readonly List<Emitter> _emitters = new();

    // Live one-shot players (PlayOneShot). Fire-and-forget, so nothing holds them but this list —
    // swept in Tick once they stop. The grace lets Play() take effect before a not-yet-playing one
    // is mistaken for finished. FlushOneShots frees them for a harness that pumps no frames.
    private readonly List<(AudioStreamPlayer3D Player, float Age)> _oneShots = new();
    private const float OneShotGrace = 0.5f; // s before a non-playing one-shot is swept

    public WorldSounds(Dictionary<string, SoundDef> defs,
        IReadOnlyDictionary<string, SoundGroup>? groups = null)
    {
        _defs = defs;
        _groups = groups ?? new Dictionary<string, SoundGroup>();
    }

    public int Count => _emitters.Count;

    /// <summary>Clears every <see cref="SoundGroup"/>'s last-picked memory. That memory is mutable
    /// state outside the RNG, so a re-seeded replay diverges on the first weighted pick without
    /// this — the caller re-seeding its generator calls it in the same breath.</summary>
    public void ResetGroupRecency()
    {
        foreach (var group in _groups.Values)
        {
            group.ResetRecency();
        }
    }

    public IEnumerable<string> Names
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in _emitters)
                if (seen.Add(e.Name))
                    yield return e.Name;
        }
    }

    /// <summary>
    /// Decodes every named sound into the stream cache while <see cref="Loader"/> is still valid,
    /// so an emitter created after the build scope closes finds its stream instead of failing.
    /// Call once, immediately before nulling <see cref="Loader"/>.
    ///
    /// <para>Names unknown to sounds.json are skipped silently — the caller passes whatever the
    /// animation program references, and the "unknown to sounds.json" census is
    /// <see cref="AnimRuntime"/>'s job at the point of use, not this one's. Returns the number of
    /// streams newly decoded, for the build log.</para>
    /// </summary>
    public int Prewarm(IEnumerable<string> names)
    {
        if (Loader == null)
        {
            return 0;
        }
        int decoded = 0;
        foreach (var name in names)
        {
            // A one-shot SOUND may name a SOUND_GROUPS group rather than a definition; prewarm each
            // of the group's members, since any of them may be the one Pick returns at runtime.
            if (_groups.TryGetValue(name, out var group))
            {
                foreach (var (member, _) in group.Members)
                {
                    decoded += PrewarmOne(member);
                }
                continue;
            }
            decoded += PrewarmOne(name);
        }
        return decoded;
    }

    private int PrewarmOne(string name)
    {
        if (_streams.ContainsKey(name) || !_defs.TryGetValue(name, out var def))
        {
            return 0;
        }
        _streams[name] = Loader!(def, false);   // quiet: a missing WAV here is reported at the point of use
        return 1;
    }

    /// <summary>
    /// Creates the emitter for a sound-definition name, or null when the name is unknown to
    /// sounds.json or its WAV cannot be loaded. Idempotent per caller-held handle: the caller keys
    /// emitters by (name, anchor) and only calls this once per key, because the data re-asserts its
    /// SOUND_NODE on every loop iteration the way PUFFER_STATE does.
    /// </summary>
    public object? Create(string name)
    {
        if (!_defs.TryGetValue(name, out var def))
            return null;
        if (!_streams.TryGetValue(name, out var stream))
        {
            if (Loader == null)
                return null;
            stream = Loader(def, true);
            _streams[name] = stream;
        }
        if (stream == null)
            return null;

        // RANGE is [full-volume distance, audible distance]. Godot's inverse-distance curve is
        // ~unattenuated inside UnitSize and clipped silent past MaxDistance, which is the same
        // shape; the curve between them is an approximation of the original's, hence TUNE.
        var player = new AudioStreamPlayer3D
        {
            Stream = stream,
            UnitSize = def.RangeMin,
            MaxDistance = def.RangeMax,
            VolumeDb = Mathf.LinearToDb(Mathf.Max(def.Volume, 0.0001f)),
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
        };
        AddChild(player);
        var emitter = new Emitter { Name = name, Player = player, Looped = def.Looped };
        _emitters.Add(emitter);
        return emitter;
    }

    /// <summary>
    /// Fires a one-shot <c>SOUND</c> at a world point — the fire-and-forget destruction/impact/damage
    /// audio a sequence emits (<c>air_mixed_exp_sg</c> when a building is struck, <c>snd_gasbagexp1</c>
    /// on a zeppelin kill). Unlike the pooled ambient emitters this is throwaway: a player that frees
    /// itself when the clip ends, so nothing has to track or re-assert it.
    ///
    /// <para>When <paramref name="name"/> is a <see cref="SoundGroup"/> it resolves to one member by
    /// weight through <paramref name="rng"/> first (the runtime's seedable RNG, so a lab replay is
    /// deterministic). Returns the resolved definition name on success, or null when the name is
    /// unknown to sounds.json / SOUND_GROUPS or its stream was never prewarmed (the archive is shut by
    /// the time most one-shots fire — see <see cref="Prewarm"/>).</para>
    /// </summary>
    public string? PlayOneShot(string name, Vector3 worldPos, Random rng)
    {
        string resolved = _groups.TryGetValue(name, out var group)
            ? group.Pick(rng) ?? name
            : name;
        if (!_defs.TryGetValue(resolved, out var def))
        {
            return null;
        }
        if (!_streams.TryGetValue(resolved, out var stream))
        {
            if (Loader == null)
            {
                return null;
            }
            stream = Loader(def, true);
            _streams[resolved] = stream;
        }
        if (stream == null)
        {
            return null;
        }

        var player = new AudioStreamPlayer3D
        {
            Stream = stream,
            UnitSize = def.RangeMin,
            MaxDistance = def.RangeMax,
            VolumeDb = Mathf.LinearToDb(Mathf.Max(def.Volume, 0.0001f)),
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
        };
        AddChild(player);
        player.GlobalPosition = worldPos;
        _oneShots.Add((player, 0f));   // swept when it stops (Tick) — no reliance on the Finished signal
        player.Play();
        if (Debug)
        {
            GD.Print($"sound one-shot: {name}"
                     + (resolved != name ? $" → {resolved}" : "")
                     + $" @ {worldPos.Snapped(Vector3.One)}");
        }
        return resolved;
    }

    /// <summary>Attaches an emitter to the world node that gives it its position — the reader's
    /// <c>OBJECT_ADD_CHILD</c> or the compiled event's <c>AT_NODE</c>.</summary>
    public void Attach(object handle, Node3D host, Vector3 offset = default)
    {
        if (handle is not Emitter e)
            return;
        e.Host = host;
        e.Offset = offset;
    }

    /// <summary>Switches an emitter on or off — the reader's <c>OBJECT_ACTIVE_STATE</c> or the
    /// compiled event's <c>active_state</c>.</summary>
    public void SetActive(object handle, bool active)
    {
        if (handle is not Emitter e)
            return;
        e.Active = active;
        if (!active && e.Player.Playing)
            e.Player.Stop();
    }

    /// <summary>Immediately frees every live one-shot player. For the synchronous damage-test
    /// harness, which pumps no frames — so neither <see cref="Tick"/>'s sweep nor a deferred
    /// <c>QueueFree</c> ever runs, and the players would otherwise leak at process exit.</summary>
    public void FlushOneShots()
    {
        foreach (var (player, _) in _oneShots)
        {
            if (IsInstanceValid(player))
            {
                if (player.Playing)
                {
                    player.Stop();
                }
                player.Free();
            }
        }
        _oneShots.Clear();
    }

    /// <summary>
    /// Positions every live emitter from its host's current world pose and gates it on the host
    /// being visible in tree — the same rule the point lights use, and for the same reason: an
    /// emitter inside a subtree the mission deactivated must be silent, or a hidden destroyed
    /// variant keeps making noise through its healthy twin.
    /// </summary>
    /// <summary>--debug-anim: log each emitter's host, distance and playing state once a second.
    /// Audio cannot be screenshot-verified, so this is the headless equivalent — and it is what
    /// distinguishes "silent because the mission deactivated its host" from "silent because the
    /// host never resolved", which look identical from the outside.</summary>
    public bool Debug;

    /// <summary>Emitters silenced because their host node's world pose is degenerate — a
    /// pre-existing animation-runtime defect this path merely observes (see Tick).</summary>
    public readonly HashSet<string> DegenerateHosts = new(StringComparer.OrdinalIgnoreCase);

    private float _logClock;
    private Func<Vector3>? _listener;

    /// <summary>Where the player is, for the debug log's distance column only.</summary>
    public void SetListener(Func<Vector3> listener) => _listener = listener;

    public void Tick()
    {
        // Sweep finished one-shots. A stopped player past the start grace is done; free it.
        for (int i = _oneShots.Count - 1; i >= 0; i--)
        {
            var (player, age) = _oneShots[i];
            if (!IsInstanceValid(player))
            {
                _oneShots.RemoveAt(i);
                continue;
            }
            age += 1f / 60f;
            if (!player.Playing && age > OneShotGrace)
            {
                player.QueueFree();
                _oneShots.RemoveAt(i);
                continue;
            }
            _oneShots[i] = (player, age);
        }

        for (int i = _emitters.Count - 1; i >= 0; i--)
        {
            var e = _emitters[i];
            if (!IsInstanceValid(e.Player))
            {
                _emitters.RemoveAt(i);
                continue;
            }
            bool audible = e.Active
                && e.Host is { } host && IsInstanceValid(host) && host.IsVisibleInTree();
            if (!audible)
            {
                if (e.Player.Playing)
                    e.Player.Stop();
                continue;
            }
            var pos = e.Host!.GlobalTransform * e.Offset;
            // A host whose world pose has blown up is silenced rather than positioned at 1e29,
            // where it would be inaudible anyway but would keep a stream running and make the
            // debug log unreadable. This is NOT a defect of the sound path: some zeppelin
            // animation nodes (`gasbag3`, one `rock_zeppelin` instance) already carry a basis of
            // ~1e27 on the pre-sound build — verified by probing the baseline directly. Counted
            // so it stays visible instead of being quietly swallowed.
            if (!pos.IsFinite() || pos.Length() > 1e6f)
            {
                if (e.Player.Playing)
                    e.Player.Stop();
                if (DegenerateHosts.Add(e.Name))
                    GD.Print($"sound: '{e.Name}' silenced — its host node's world pose is "
                             + $"degenerate ({pos}); pre-existing, not caused by the sound path");
                continue;
            }
            e.Player.GlobalPosition = pos;
            // A LOOPED stream is marked as a forward loop by SoundArchive, so one Play() runs
            // forever; the one non-looping SOUND_NODE name in the data (snd_freighter) fires once
            // and is deliberately not restarted here.
            if (!e.Player.Playing && (e.Looped || !e.Player.HasMeta("csky_played")))
            {
                e.Player.SetMeta("csky_played", true);
                e.Player.Play();
            }
        }
        LogOnce();
    }

    private void LogOnce()
    {
        if (!Debug)
            return;
        _logClock += 1f / 60f;
        if (_logClock < 1f)
            return;
        _logClock = 0f;
        var ear = _listener?.Invoke() ?? Vector3.Zero;
        foreach (var e in _emitters)
        {
            string host = e.Host is { } h && IsInstanceValid(h)
                ? (h.HasMeta("cs_name") ? h.GetMeta("cs_name").AsString() : h.Name.ToString())
                : "UNATTACHED";
            bool hidden = e.Host is { } hv && IsInstanceValid(hv) && !hv.IsVisibleInTree();
            GD.Print($"sound: {e.Name} @ {host}{(hidden ? " (host hidden)" : "")} "
                     + $"pos {e.Player.GlobalPosition.Snapped(Vector3.One)} "
                     + $"dist {ear.DistanceTo(e.Player.GlobalPosition):0} m "
                     + $"max {e.Player.MaxDistance:0} m "
                     + (e.Player.Playing ? "PLAYING" : e.Active ? "silent" : "off"));
        }
    }
}

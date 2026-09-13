using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The animated world's ambient 3D sound emitters (<c>SOUND_NODE</c>), the waterfall roar, the
/// train, the firetruck and police sirens, the zeppelin nacelle engines, the fire crackle and the
/// warning beeper. One pooled <see cref="AudioStreamPlayer3D"/> per live emitter, positioned each
/// frame from the world node the animation attached it to. <c>SOUND_NODE</c> vs one-shot
/// <c>SOUND</c>: docs/formats/anim-definitions.md.
/// ⚠ Emitters are pooled here, never parented into the world subtree they follow:
/// <see cref="AnimRuntime"/>'s <c>FindAll</c> memoization is sound only while nothing reparents
/// world nodes at runtime.
/// </summary>
public sealed partial class WorldSounds : Node3D
{
    /// <summary>Emitters silenced because their host node's world pose is degenerate, a
    /// pre-existing animation-runtime defect this path merely observes (see Tick).</summary>
    public readonly HashSet<string> DegenerateHosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolves a sound definition's WAV into a stream. Supplied by the caller and valid
    /// only during the world build: the session <see cref="SoundArchive"/> is disposed when the
    /// build scope ends. The bool is <c>warn</c>: false on the speculative prewarm decode, true at
    /// the point of use.
    /// ⚠ The stream cache alone is not enough; <see cref="Prewarm"/> is what makes a later decode
    /// safe after this field is nulled.</summary>
    public Func<SoundDef, bool, AudioStreamWav?>? Loader;

    /// <summary>--debug-anim: log each emitter's host, distance and playing state once a second.
    /// Audio cannot be screenshot-verified, so this is the headless equivalent, and it is what
    /// distinguishes "silent because the mission deactivated its host" from "silent because the
    /// host never resolved", which look identical from the outside.</summary>
    public bool Debug;

    /// <summary>The session's mission radio queue, or null when the session built none. Cues whose
    /// definition is a radio line belong there and not on a positional emitter; the mission layer
    /// reads this to find the channel rather than being handed a second reference.</summary>
    public MissionRadio? Radio;

    private const float OneShotGrace = 0.5f; // s before a non-playing one-shot is swept
    // A StringName, read every frame per emitter: a string literal here would convert per call.
    private static readonly StringName PlayedMeta = "csky_played";

    private readonly Dictionary<string, SoundDef> _defs;
    private readonly IReadOnlyDictionary<string, SoundGroup> _groups;
    private readonly Dictionary<string, AudioStreamWav?> _streams = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Emitter> _emitters = new();

    // Live one-shot players (PlayOneShot). Fire-and-forget, so nothing holds them but this list,
    // swept in Tick once they stop. The grace lets Play() take effect before a not-yet-playing one
    // is mistaken for finished. FlushOneShots frees them for a harness that pumps no frames.
    private readonly List<OneShot> _oneShots = new();

    private float _logClock;
    private Func<IReadOnlyList<Vector3>>? _listeners;

    public WorldSounds(Dictionary<string, SoundDef> defs,
        IReadOnlyDictionary<string, SoundGroup>? groups = null)
    {
        _defs = defs;
        _groups = groups ?? new Dictionary<string, SoundGroup>();
    }

    public int Count => _emitters.Count;

    /// <summary>How many one-shots actually started an <see cref="AudioStreamPlayer3D"/> playing,
    /// the <see cref="Mech3.Anim.SoundChannel.OneShotSoundsPlayed"/> precedent for this class: an
    /// in-engine suite counts this rather than grepping a Debug-gated log line, and it moves only
    /// when <see cref="Spawn"/> actually resolved a stream (--mute or an unknown group leaves it
    /// unchanged, which is why a cue-firing assertion must run at --volume=0, never --mute).</summary>
    public int OneShotsStarted { get; private set; }

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

    /// <summary>Clears every <see cref="SoundGroup"/>'s last-picked memory. That memory is mutable
    /// state outside the RNG, so a re-seeded replay diverges on the first weighted pick without
    /// this, the caller re-seeding its generator calls it in the same breath.</summary>
    public void ResetGroupRecency()
    {
        foreach (var group in _groups.Values)
        {
            group.ResetRecency();
        }
    }

    /// <summary>Decodes every named sound into the stream cache while <see cref="Loader"/> is
    /// still valid, so an emitter created after the build scope closes finds its stream instead of
    /// failing. Call once, immediately before nulling <see cref="Loader"/>. Names unknown to
    /// sounds.json are skipped silently; that census is <see cref="AnimRuntime"/>'s job. Returns
    /// the number of streams newly decoded, for the build log.</summary>
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
            // of the group's members, since any of them may be the one Pick returns at runtime,
            // and each dialogue-chain line, since a chain plays its members by name later too.
            if (_groups.TryGetValue(name, out var group))
            {
                foreach (var (member, _) in group.Members)
                {
                    decoded += PrewarmOne(member);
                }
                foreach (var chain in group.Chains)
                {
                    foreach (var line in chain)
                    {
                        decoded += PrewarmOne(line);
                    }
                }
                continue;
            }
            decoded += PrewarmOne(name);
        }
        return decoded;
    }

    /// <summary>Whether a definition name has a decoded, playable stream: the availability check
    /// the voice dispatch needs (a def is not proof of a WAV: see <see cref="CombatVoice"/>).
    /// Reads the prewarm cache; while the <see cref="Loader"/> is still open it decodes on demand,
    /// so the answer is the same before and after the build scope closes.</summary>
    public bool HasStream(string name) => StreamFor(name) != null;

    /// <summary>The decoded stream behind a definition name, or null when the name is unknown or
    /// its WAV is missing. The same cache <see cref="Create"/> and <see cref="Spawn"/> read, so a
    /// channel that sits beside this one (<see cref="MissionRadio"/>) plays the prewarmed stream
    /// rather than decoding the archive a second time.</summary>
    public AudioStreamWav? StreamFor(string name)
    {
        if (_streams.TryGetValue(name, out var cached))
        {
            return cached;
        }
        if (Loader != null && _defs.TryGetValue(name, out var def))
        {
            var stream = Loader(def, false);
            _streams[name] = stream;
            return stream;
        }
        return null;
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
            // Decode-on-miss for a SOUND_NODE emitter, most names are
            // prewarmed, but not all (see this file's own Prewarm doc), so this is a live path.
            using var _ = PerfSample.Scope(PerfSite.AudioLoad);
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
            Bus = AudioBuses.Effects,
        };
        AddChild(player);
        var emitter = new Emitter { Name = name, Player = player, Looped = def.Looped };
        _emitters.Add(emitter);
        return emitter;
    }

    /// <summary>Fires a one-shot <c>SOUND</c> at a world point: fire-and-forget destruction/impact
    /// audio, a player that frees itself when the clip ends. A <see cref="SoundGroup"/> name
    /// resolves to one member by weight through <paramref name="rng"/> first. <paramref name="bus"/>
    /// is this call's mix category, defaulted so every destruction caller stays on Effects and
    /// combat voice alone asks for Voice; <paramref name="pitch"/> is the cutscene fast-forward's
    /// rate, 1 everywhere else. Null when unknown or never prewarmed (<see cref="Prewarm"/>).</summary>
    public string? PlayOneShot(string name, Vector3 worldPos, Random rng,
        string bus = AudioBuses.Effects, float pitch = 1f) =>
        Spawn(name, worldPos, null, rng, bus, pitch);

    /// <summary>The source-following variant of <see cref="PlayOneShot(string, Vector3, Random, string)"/>:
    /// the one-shot rides <paramref name="source"/>'s world pose each <see cref="Tick"/>. For a
    /// voice line from a moving aircraft, where a once-written position would fall behind within a
    /// second. When the source is freed mid-clip the sound holds its last position and finishes
    /// there. Who hears it is the pinned per-pane listener model (<c>UI.SplitScreen</c>): the
    /// nearest pane's volume wins, and following the source only keeps the range honest.</summary>
    public string? PlayOneShot(string name, Node3D source, Random rng,
        string bus = AudioBuses.Effects)
    {
        var pos = IsInstanceValid(source) && source.IsInsideTree()
            ? source.GlobalPosition
            : Vector3.Zero;
        return Spawn(name, pos, source, rng, bus, 1f);
    }

    /// <summary>Attaches an emitter to the world node that gives it its position, the reader's
    /// <c>OBJECT_ADD_CHILD</c> or the compiled event's <c>AT_NODE</c>.</summary>
    public void Attach(object handle, Node3D host, Vector3 offset = default)
    {
        if (handle is not Emitter e)
            return;
        e.Host = host;
        e.Offset = offset;
    }

    /// <summary>Switches an emitter on or off, the reader's <c>OBJECT_ACTIVE_STATE</c> or the
    /// compiled event's <c>active_state</c>.</summary>
    public void SetActive(object handle, bool active)
    {
        if (handle is not Emitter e)
            return;
        e.Active = active;
        if (!active && e.Player.Playing)
            e.Player.Stop();
    }

    /// <summary>Plays an emitter faster or slower, and higher or lower with it: the cutscene
    /// fast-forward's pitch half, so a sped-up shot's ambient sound rises with its picture.
    /// ⚠ Floored, never zero: a <c>PitchScale</c> of 0 stalls the stream rather than silencing
    /// it, the same floor the flight engine note keeps.</summary>
    public void SetPitch(object handle, float pitch)
    {
        if (handle is Emitter e && IsInstanceValid(e.Player))
            e.Player.PitchScale = Mathf.Max(0.01f, pitch);
    }

    /// <summary>Immediately frees every live one-shot player. For the synchronous damage-test
    /// harness, which pumps no frames, so neither <see cref="Tick"/>'s sweep nor a deferred
    /// <c>QueueFree</c> ever runs, and the players would otherwise leak at process exit.</summary>
    public void FlushOneShots()
    {
        foreach (var shot in _oneShots)
        {
            if (IsInstanceValid(shot.Player))
            {
                if (shot.Player.Playing)
                {
                    shot.Player.Stop();
                }
                shot.Player.Free();
            }
        }
        _oneShots.Clear();
    }

    /// <summary>Where the session's audio listeners are, one camera per pane, since every pane is
    /// listener-enabled (UI.SplitScreen). For the debug log's distance column only: the engine reads
    /// the listeners itself, and reports the NEAREST one here because that is the pane whose volume
    /// wins the mix.</summary>
    public void SetListeners(Func<IReadOnlyList<Vector3>> listeners) => _listeners = listeners;

    /// <summary>
    /// Positions every live emitter from its host's current world pose and gates it on the host
    /// being visible in tree, the same rule the point lights use, and for the same reason: an
    /// emitter inside a subtree the mission deactivated must be silent, or a hidden destroyed
    /// variant keeps making noise through its healthy twin.
    /// </summary>
    public void Tick()
    {
        // Sweep finished one-shots. A stopped player past the start grace is done; free it. A
        // source-following one rides its source's pose; a freed source leaves it at its last spot.
        for (int i = _oneShots.Count - 1; i >= 0; i--)
        {
            var shot = _oneShots[i];
            if (!IsInstanceValid(shot.Player))
            {
                _oneShots.RemoveAt(i);
                continue;
            }
            shot.Age += 1f / 60f;
            if (!shot.Player.Playing && shot.Age > OneShotGrace)
            {
                shot.Player.QueueFree();
                _oneShots.RemoveAt(i);
                continue;
            }
            if (shot.Source is { } src)
            {
                if (IsInstanceValid(src) && src.IsInsideTree())
                {
                    shot.Player.GlobalPosition = src.GlobalPosition;
                }
                else
                {
                    shot.Source = null;   // finish where the source last was
                }
            }
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
            // Silenced rather than positioned at a huge coordinate: a pre-existing degenerate host
            // pose from the animation runtime, not a defect here. Counted so it stays visible.
            if (!pos.IsFinite() || pos.Length() > 1e6f)
            {
                if (e.Player.Playing)
                    e.Player.Stop();
                if (DegenerateHosts.Add(e.Name))
                    Log.Info("sound", $"sound: '{e.Name}' silenced — its host node's world pose is degenerate ({pos}); pre-existing, not caused by the sound path");
                continue;
            }
            e.Player.GlobalPosition = pos;
            // A LOOPED stream is marked as a forward loop by SoundArchive, so one Play() runs
            // forever; the one non-looping SOUND_NODE name in the data (snd_freighter) fires once
            // and is deliberately not restarted here.
            if (!e.Player.Playing && (e.Looped || !e.Player.HasMeta(PlayedMeta)))
            {
                e.Player.SetMeta(PlayedMeta, true);
                e.Player.Play();
            }
        }
        LogOnce();
    }

    // Range from the closest listener to `at`, or -1 with no listeners,
    // which is not a formatting quirk but the state that silences every 3D emitter, so the log says
    // it rather than printing a distance from the world origin.
    private static (float Range, int Index) NearestEar(IReadOnlyList<Vector3> ears, Vector3 at)
    {
        float best = -1f;
        int which = -1;
        for (int i = 0; i < ears.Count; i++)
        {
            float d = ears[i].DistanceTo(at);
            if (best < 0f || d < best)
            {
                best = d;
                which = i;
            }
        }
        return (best, which);
    }

    private string? Spawn(string name, Vector3 worldPos, Node3D? source, Random rng, string bus,
        float pitch)
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
            // Decode-on-miss for a one-shot SOUND, reachable from
            // RunDeathSequence's own Sound events, same as SOUND_NODE's Create above.
            using var _ = PerfSample.Scope(PerfSite.AudioLoad);
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

        // The bus comes from this call, since one call site serves two mix categories. A fresh
        // player per one-shot makes that per-play by construction; keep the assignment here, or a
        // later pooling of these players would speak an explosion through the Voice bus.
        var player = new AudioStreamPlayer3D
        {
            Stream = stream,
            UnitSize = def.RangeMin,
            MaxDistance = def.RangeMax,
            VolumeDb = Mathf.LinearToDb(Mathf.Max(def.Volume, 0.0001f)),
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            Bus = bus,
            PitchScale = Mathf.Max(0.01f, pitch),
        };
        AddChild(player);
        player.GlobalPosition = worldPos;
        // Swept when it stops (Tick), no reliance on the Finished signal.
        _oneShots.Add(new OneShot { Player = player, Source = source });
        player.Play();
        OneShotsStarted++;
        if (Debug)
        {
            Log.Info("sound", $"sound one-shot: {name}{(resolved != name ? $" → {resolved}" : "")} @ {worldPos.Snapped(Vector3.One)}{(source != null ? " (following)" : "")}");
        }
        return resolved;
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

    private void LogOnce()
    {
        if (!Debug)
            return;
        _logClock += 1f / 60f;
        if (_logClock < 1f)
            return;
        _logClock = 0f;
        var ears = _listeners?.Invoke() ?? Array.Empty<Vector3>();
        // Names the reference of the dist column: it is the nearest LISTENER (pane camera), which is
        // the one whose volume the engine's per-channel max keeps, not player one's.
        var where = new System.Text.StringBuilder();
        for (int i = 0; i < ears.Count; i++)
            where.Append(i == 0 ? " at " : ", ").Append(Log.Format($"{ears[i].Snapped(Vector3.One)}"));
        Log.Info("sound", $"sound: {ears.Count} listener{(ears.Count == 1 ? "" : "s")}{where} (dist below = range to the NEAREST pane camera)");
        foreach (var e in _emitters)
        {
            string host = e.Host is { } h && IsInstanceValid(h)
                ? (h.HasMeta("cs_name") ? h.GetMeta("cs_name").AsString() : h.Name.ToString())
                : "UNATTACHED";
            bool hidden = e.Host is { } hv && IsInstanceValid(hv) && !hv.IsVisibleInTree();
            var near = NearestEar(ears, e.Player.GlobalPosition);
            Log.Info("sound", $"sound: {e.Name} @ {host}{(hidden ? " (host hidden)" : "")} pos {e.Player.GlobalPosition.Snapped(Vector3.One)} dist {near.Range:0} m (P{near.Index + 1}) max {e.Player.MaxDistance:0} m {(e.Player.Playing ? "PLAYING" : e.Active ? "silent" : "off")}");
        }
    }

    // One live fire-and-forget one-shot; Source non-null makes it follow
    // that node's pose until the clip ends or the node dies.
    private sealed class OneShot
    {
        public AudioStreamPlayer3D Player = null!;
        public float Age;
        public Node3D? Source;
    }

    // One live emitter: the player plus the world node whose pose it rides.
    private sealed class Emitter
    {
        public string Name = "";
        public AudioStreamPlayer3D Player = null!;
        public Node3D? Host;      // the node this was attached to (OBJECT_ADD_CHILD / AT_NODE)
        public Vector3 Offset;    // in the host's own frame
        public bool Active;       // OBJECT_ACTIVE_STATE / the compiled event's active_state
        public bool Looped;
    }
}

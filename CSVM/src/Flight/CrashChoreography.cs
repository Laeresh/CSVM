using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The crash choreography (polish-4 item 8): the timed burst of authored effects the
/// original plays on a crash, over and above the single fireball + wreck fire the earlier
/// passes shipped. The data is three compiled anim defs rooted at the <c>player</c> node —
/// <c>player_crash_default</c> (air), <c>_dirt</c> (ground), <c>_water</c> — each a sequence
/// of <c>CallAnimation</c> events that fire the shared effect defs (<c>small_yellow_sparks</c>,
/// <c>large_fireball</c>, <c>large_black_smokeball</c>, …) at offsets from the crash pose.
///
/// <para><b>This covers the ground/dirt variant.</b> It reproduces the dirt sequence's spark
    /// bursts, its delayed fireball cluster, its black smokeball, and its five burning debris
    /// arcs (<c>call_crash_trails</c>); the earth-impact boom (<c>snd_exp_ground_a</c>) is played
    /// by <c>FlightController.Crash</c> via <c>FlightAudio.OnGroundExplosion</c>. The existing
    /// primary fireball (<c>FlightController.CrashEffect</c>) and the 10-second wreck fire
    /// (<c>CrashBreakup</c>) already cover <c>large_fireball</c> and <c>large_10sec_fire</c>.
    /// Deliberately left for later slices, each independently verifiable: the <c>flydirt_plane</c>
    /// dust burst and the water splash+steam (mesh/scale/opacity path, not puffers), and the
    /// per-piece <c>large_firetrail</c> + bounce sub-sequences. The air and water variants wait
    /// on their triggers: water needs a sea-surface signal the collision system does not yet
    /// expose, and the air (no-impact) variant has no trigger until weapons can down a plane
    /// mid-flight (M3). Every reachable crash today is a terrain or building collision = dirt.</para>
///
/// <para>Params are read from the compiled defs at build time (via
/// <see cref="PufferState.FromAnimEvent"/>), never transcribed — the same puffer name
/// <c>trailpuffer2</c> is reused with different numbers/sizes by sparks, smokeball and steam,
/// so only the per-call compiled payload is authoritative.</para>
/// </summary>
public sealed class CrashChoreography
{
    /// <summary>Which crash variant the original would pick. The engine chooses natively from
    /// the impact surface (the three defs are never <c>CallAnimation</c>-referenced by name),
    /// so the choice is ours to reconstruct — see <see cref="FlightController"/>.</summary>
    public enum Surface { Air, Ground, Water }

    // 0 = spark burst, 1 = fireball, 2 = smokeball — the kinds this slice emits.
    private struct Cue { public float Time; public int Kind; public Vector3 Offset; }

    // One launched debris arc: the emitter, its world-space ballistic launch, and a clock. The
    // anchor is virtual — integrated here, no scene node — and its position drives the
    // fire-trail puffer once per frame (DISTANCE_INTERVAL: one puff per 2.5 m of travel).
    private struct DebrisRun
    {
        public Vector3 Origin, V0;
        public float Gravity, RunTime, Clock;
        public bool Live;
    }

    private readonly Puffer[] _sparks;      // small_yellow_sparks bursts (additive yellow)
    private readonly Puffer[] _fireballs;   // the delayed large_fireball cluster (additive fire)
    private readonly Puffer? _smokeball;    // large_black_smokeball (forced MIX — see gap 1)
    private readonly (Puffer Puffer, DebrisTrail Def)[] _debris; // call_crash_trails fire arcs
    private readonly DebrisRun[] _debrisRuns;

    private readonly List<Cue> _cues = new();
    private readonly System.Random _rng = new();
    private int _next, _sparkRing, _fireballRing;
    private float _clock;
    private bool _active;
    private Vector3 _anchor;   // impact point — the effects cluster around the visible fireball
    private Basis _basis;      // crash-pose orientation, so local offsets follow the attitude

    private CrashChoreography(Puffer[] sparks, Puffer[] fireballs, Puffer? smokeball,
        (Puffer, DebrisTrail)[] debris)
    {
        _sparks = sparks;
        _fireballs = fireballs;
        _smokeball = smokeball;
        _debris = debris;
        _debrisRuns = new DebrisRun[debris.Length];
    }

    /// <summary>The parsed effect parameters, loaded ONCE per session and shared across every
    /// player's own emitter instances (the Puffers themselves are per-plane scene nodes).</summary>
    /// <summary>One of <c>call_crash_trails</c>'s five burning-debris arcs: a DISTANCE_INTERVAL
    /// fire-trail puffer (<c>spurtpufferN</c>) riding a ballistic anchor (<c>fly_trailN</c>). The
    /// anchor node itself is never built — it is an invisible carrier, and only the fire it
    /// leaves is seen — so its trajectory is reconstructed from the anim's own <c>gravity</c> and
    /// <c>run_time</c> plus the <c>translation_range</c> extents. ⚠ <c>translation_range</c> is
    /// undocumented and <c>AnimRuntime</c> does not implement it (ballistic OBJECT_MOTION is
    /// counted, never simulated — there are no weapons to trigger it), so the reading here is a
    /// reasoned interpretation, not a settled decode: <c>xz</c>/<c>y</c> are taken as the
    /// horizontal/vertical distance the debris travels over <c>run_time</c>, launched in a random
    /// azimuth. The two other range fields (<c>initial</c> ≈ 7–13, <c>delta</c> = 0) are left
    /// unmapped, as <c>AnimRuntime</c> leaves the rotation <c>delta</c> it cannot place. The arc
    /// shape is TUNE — only its rough scale reads, the anchor being invisible.</summary>
    public sealed class DebrisTrail
    {
        public PufferState Puffer = null!;
        public float Gravity;      // m/s² (negative), OBJECT_MOTION gravity.value
        public float XzMin, XzMax; // horizontal travel over RunTime (translation_range.xz)
        public float YMin, YMax;   // vertical travel over RunTime (translation_range.y)
        public float RunTime;
    }

    public sealed class EffectSet
    {
        public PufferState? Sparks;
        public PufferState? Smokeball;
        public PufferState? Fireball;
        public List<DebrisTrail> Debris = new();

        public bool Any => Sparks != null || Smokeball != null || Fireball != null || Debris.Count > 0;

        /// <summary>Reads the crash effect puffers. Sparks and the smokeball come from the
        /// compiled cam_anim defs (their <c>trailpuffer2</c> params are per-call, so a reader
        /// lookup by puffer name would return the wrong numbers); the fireball reuses the same
        /// reader source as the primary <c>CrashEffect</c> so the cluster matches it. The debris
        /// arcs pair each <c>call_crash_trails</c> puffer with its <c>fly_trailN</c> ballistic.</summary>
        public static EffectSet Load(AnimProgram program, string zrdrPath)
        {
            return new EffectSet
            {
                Sparks = PufferFromDef(program, "small_yellow_sparks"),
                Smokeball = PufferFromDef(program, "large_black_smokeball"),
                Fireball = PufferState.Load(zrdrPath, "flame_ball.json", "fierypuffer"),
                Debris = LoadDebris(program),
            };
        }

        /// <summary>Reads <c>call_crash_trails</c>: five <c>spurtpufferN</c> DISTANCE trails, each
        /// paired with the <c>ObjectMotion</c> that flings its <c>fly_trailN</c> anchor. The two
        /// are joined by the shared node name (the puffer's <c>at_node</c> = the motion's
        /// <c>node</c>). A trail with a puffer but no ballistic (or textures absent from this
        /// chapter) is dropped rather than launched from a standstill.</summary>
        private static List<DebrisTrail> LoadDebris(AnimProgram program)
        {
            var puffers = new Dictionary<string, PufferState>();
            var arcs = new Dictionary<string, DebrisTrail>();
            foreach (var def in program.ByAnimName("call_crash_trails"))
            {
                foreach (var seq in def.Sequences)
                {
                    foreach (var ev in seq.Events)
                    {
                        if (ev.Kind == "PufferState"
                            && (ev.Data.Num("active_state") ?? 0f) >= 1f
                            && ev.Data.Str("at_node") is { } atNode
                            && ev.Data.Has("textures"))
                        {
                            puffers[atNode] = PufferState.FromAnimEvent(ev.Data);
                        }
                        else if (ev.Kind == "ObjectMotion"
                                 && ev.Data.Obj("translation_range") is { } tr
                                 && ev.Data.Str("node") is { } node)
                        {
                            arcs[node] = new DebrisTrail
                            {
                                Gravity = ev.Data.Obj("gravity")?.Num("value") ?? -3f,
                                XzMin = tr.Obj("xz")?.Num("min") ?? 0f,
                                XzMax = tr.Obj("xz")?.Num("max") ?? 0f,
                                YMin = tr.Obj("y")?.Num("min") ?? 0f,
                                YMax = tr.Obj("y")?.Num("max") ?? 0f,
                                RunTime = ev.Data.Num("run_time") ?? 2.5f,
                            };
                        }
                    }
                }
            }
            var trails = new List<DebrisTrail>();
            foreach (var (node, arc) in arcs)
            {
                if (puffers.TryGetValue(node, out var p))
                {
                    arc.Puffer = p;
                    trails.Add(arc);
                }
            }
            return trails;
        }

        /// <summary>The first fully-defined (activating, NUMBER-bearing) PUFFER_STATE in any
        /// sequence of the def an ANIMATION_NAME resolves to. The deactivate stubs share the
        /// name but carry <c>active_state 0</c> and a null NUMBER, so both are required.</summary>
        private static PufferState? PufferFromDef(AnimProgram program, string animName)
        {
            foreach (var def in program.ByAnimName(animName))
            {
                foreach (var seq in def.Sequences)
                {
                    foreach (var ev in seq.Events)
                    {
                        if (ev.Kind == "PufferState"
                            && (ev.Data.Num("active_state") ?? 0f) >= 1f
                            && ev.Data.Has("number"))
                        {
                            return PufferState.FromAnimEvent(ev.Data);
                        }
                    }
                }
            }
            return null;
        }
    }

    /// <summary>Builds one plane's emitter pools from the shared <paramref name="fx"/>. Null
    /// when nothing loaded (no compiled defs and no reader fireball) — the crash then falls
    /// back to the primary fireball + wreck fire alone, as before this item.</summary>
    public static CrashChoreography? Create(EffectSet fx, TextureArchive textures)
    {
        // Pool sizes cover the most simultaneous same-kind effects any variant needs: the dirt
        // cluster overlaps 3 fireballs (0.25 s apart, ~1 s life) and fires 2 sparks at once.
        var sparks = MakePool(fx.Sparks, textures, 3, 0.2f);
        var fireballs = MakePool(fx.Fireball, textures, 3, 0.3f);
        var smokeball = fx.Smokeball != null
            ? Puffer.Create(fx.Smokeball, textures, activeDuration: 2.1f,
                blend: PufferBlend.Mix, softParticles: false)
            : null;
        // The debris arcs: one DISTANCE_INTERVAL fire-trail emitter per loaded trail (additive
        // fire, like the fireball — the puffers carry no COLORS ramp). activeDuration is unused
        // by a trail emitter; it emits while driven via TrailAdvance.
        var debris = new List<(Puffer, DebrisTrail)>();
        foreach (var trail in fx.Debris)
        {
            if (Puffer.Create(trail.Puffer, textures) is { } p)
                debris.Add((p, trail));
        }
        var debrisArr = debris.ToArray();
        if (sparks.Length == 0 && fireballs.Length == 0 && smokeball == null && debrisArr.Length == 0)
            return null;
        return new CrashChoreography(sparks, fireballs, smokeball, debrisArr);
    }

    private static Puffer[] MakePool(PufferState? state, TextureArchive textures, int count, float duration)
    {
        if (state == null)
            return Array.Empty<Puffer>();
        var list = new List<Puffer>(count);
        for (int i = 0; i < count; i++)
        {
            if (Puffer.Create(state, textures, activeDuration: duration) is { } p)
                list.Add(p);
        }
        return list.ToArray();
    }

    /// <summary>The emitter nodes the caller must parent (once) so they render.</summary>
    public IEnumerable<Node3D> Emitters
    {
        get
        {
            foreach (var p in _sparks)
                yield return p;
            foreach (var p in _fireballs)
                yield return p;
            if (_smokeball != null)
                yield return _smokeball;
            foreach (var (p, _) in _debris)
                yield return p;
        }
    }

    public int SparkCount => _sparks.Length;
    public int FireballCount => _fireballs.Length;
    public bool HasSmoke => _smokeball != null;
    public int DebrisCount => _debris.Length;

    /// <summary>Starts the choreography at a crash. <paramref name="pose"/> is the plane's
    /// crash-time transform whose <b>origin is the anchor</b> and whose attitude orients the
    /// local offsets. The data attaches every crash effect to the <c>healthy</c> node — the
    /// plane's own centre, a few metres above the ground — NOT the collision contact point;
    /// anchoring at the centre both matches the data and lifts the rising smoke clear of the
    /// terrain, where the shader's soft-particle fade would otherwise gut it. <paramref
    /// name="impact"/> is the collision point (kept for a future water-surface test).</summary>
    public void Begin(Transform3D pose, Vector3 impact, Surface surface)
    {
        _active = true;
        _clock = 0f;
        _next = 0;
        _sparkRing = 0;
        _fireballRing = 0;
        _anchor = pose.Origin;
        _basis = pose.Basis.Orthonormalized();
        BuildCues(surface);
        StartDebris(surface);
        FireDue(); // the t = 0 cues, so a screenshot on the impact frame already shows them
    }

    // The five burning-debris arcs (call_crash_trails): each flings from the crash point in a
    // world-space azimuth and rises on a gravity arc, trailing fire. The anchor node is
    // invisible, so only the trail reads. Ground and air both fire it (their crash defs call it
    // at destroyed +(0,−3,3) / +(0,0,3)); water's is a later slice. Azimuth/extent are randomized
    // per crash, but the trajectory reading is a documented interpretation — see DebrisTrail.
    private void StartDebris(Surface surface)
    {
        if (_debris.Length == 0 || surface == Surface.Water)
            return;
        var offset = surface == Surface.Ground ? new Vector3(0f, -3f, 3f) : new Vector3(0f, 0f, 3f);
        var origin = _anchor + _basis * offset;
        for (int i = 0; i < _debris.Length; i++)
        {
            var def = _debris[i].Def;
            // Fan the arcs evenly around the compass, jittered — scattered debris, not a line.
            float azimuth = Mathf.Tau * (i + Rand(-0.15f, 0.15f)) / _debris.Length;
            float horiz = Rand(def.XzMin, def.XzMax) / Mathf.Max(def.RunTime, 0.1f);
            // Reach ~Y up over RunTime under the data's (weak) gravity: solve
            // vertDist = v0·t + ½·g·t² for v0 (g < 0, so the second term ADDS launch speed).
            float vVert = Rand(def.YMin, def.YMax) / Mathf.Max(def.RunTime, 0.1f)
                          - 0.5f * def.Gravity * def.RunTime;
            _debrisRuns[i] = new DebrisRun
            {
                Origin = origin,
                V0 = new Vector3(Mathf.Cos(azimuth) * horiz, vVert, Mathf.Sin(azimuth) * horiz),
                Gravity = def.Gravity,
                RunTime = def.RunTime,
                Clock = 0f,
                Live = true,
            };
            _debris[i].Puffer.TrailAdvance(origin); // seed the trail at the launch point
        }
    }

    /// <summary>Fires any cues now due; call each physics frame while crashed (the puffers
    /// self-animate in <c>_Process</c> even through the crash freeze).</summary>
    public void Advance(float dt)
    {
        if (!_active)
            return;
        _clock += dt;
        FireDue();
        for (int i = 0; i < _debrisRuns.Length; i++)
        {
            ref var run = ref _debrisRuns[i];
            if (!run.Live)
                continue;
            run.Clock += dt;
            float t = Mathf.Min(run.Clock, run.RunTime);
            var pos = run.Origin + run.V0 * t + new Vector3(0f, 0.5f * run.Gravity * t * t, 0f);
            _debris[i].Puffer.TrailAdvance(pos);
            if (run.Clock >= run.RunTime)
            {
                _debris[i].Puffer.TrailEnd(); // stop emitting; the live fire decays on its own
                run.Live = false;
            }
        }
    }

    /// <summary>Clears every emitter and disarms the schedule (respawn).</summary>
    public void Reset()
    {
        _active = false;
        _cues.Clear();
        foreach (var p in _sparks)
            p.Clear();
        foreach (var p in _fireballs)
            p.Clear();
        _smokeball?.Clear();
        for (int i = 0; i < _debris.Length; i++)
        {
            _debris[i].Puffer.Clear();
            _debrisRuns[i].Live = false;
        }
    }

    // The event schedule for one variant, transcribed from the crash defs' CallAnimation lists
    // (times relative to the crash instant; the dirt cluster's Event+0.25 chain is absolute
    // 0.25/0.50/0.75). Offsets are the CallAnimation AtNode.position, in the plane frame.
    private void BuildCues(Surface surface)
    {
        _cues.Clear();
        switch (surface)
        {
            case Surface.Ground:
                // player_crash_dirt: 2 sparks + smokeball at the pose, then a 3-fireball cluster.
                AddSpark(0f, new Vector3(0f, 0f, 0f));
                AddSpark(0f, new Vector3(1f, 0f, 2f));
                AddSmoke(0f, Vector3.Zero);
                AddFireball(0.25f, new Vector3(-3f, 0f, 5f));
                AddFireball(0.50f, new Vector3(3f, 2f, 5f));
                AddFireball(0.75f, new Vector3(-3f, 2f, -5f));
                break;
            case Surface.Air:
                // player_crash_default: 5 sparks around the pose, one delayed belly fireball.
                // (Not reachable until a mid-air destruct trigger exists — kept so the seam is
                // real. The spark ring wraps a 3-pool, which for additive near-coincident bursts
                // reads the same as five distinct emitters.)
                AddSpark(0f, Vector3.Zero);
                AddSpark(0f, Vector3.Zero);
                AddSpark(0f, Vector3.Zero);
                AddSpark(0f, new Vector3(0f, 0f, 1f));
                AddSpark(0f, new Vector3(1f, 0f, 0f));
                AddFireball(0.35f, new Vector3(0f, -4f, 0f));
                break;
            case Surface.Water:
                // player_crash_water is splash + steam (the mesh/opacity path) — a later slice.
                // Until then a water hit falls back to nothing here (the wreck fire still burns).
                break;
        }
    }

    private void AddSpark(float t, Vector3 offset)
    {
        if (_sparks.Length > 0)
            _cues.Add(new Cue { Time = t, Kind = 0, Offset = offset });
    }

    private void AddFireball(float t, Vector3 offset)
    {
        if (_fireballs.Length > 0)
            _cues.Add(new Cue { Time = t, Kind = 1, Offset = offset });
    }

    private void AddSmoke(float t, Vector3 offset)
    {
        if (_smokeball != null)
            _cues.Add(new Cue { Time = t, Kind = 2, Offset = offset });
    }

    private void FireDue()
    {
        while (_next < _cues.Count && _cues[_next].Time <= _clock)
        {
            var c = _cues[_next++];
            var world = _anchor + _basis * c.Offset;
            switch (c.Kind)
            {
                case 0:
                    _sparks[_sparkRing++ % _sparks.Length].Burst(world);
                    break;
                case 1:
                    _fireballs[_fireballRing++ % _fireballs.Length].Burst(world);
                    break;
                case 2:
                    _smokeball?.Burst(world);
                    break;
            }
        }
    }

    private float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
}

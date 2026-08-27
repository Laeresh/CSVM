using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>One runtime's object-pose and visual events: the `OBJECT_*` state poses, the
/// motion-BUILDER role (parsing `OBJECT_MOTION`/`OBJECT_MOTION_FROM_TO`/`OBJECT_MOTION_SI_SCRIPT`
/// into the motion value types handed to <see cref="MotionSet"/>, which stays a pure container
/// and never constructs one), and the subtree opacity/fade machinery. Every handler returns how
/// many ops it applied; the router adds that to its census counter, keeping the counter itself
/// where every other case writes it. The `_rest` pose table stays on `AnimRuntime` (the death flow
/// reads it too); this channel and the motion builders reach it only through
/// <see cref="AnimRuntime.RestOf"/>, and the runtime reference held here exists for that seam and
/// as the builders' own host argument, nothing wider.</summary>
internal sealed class PoseChannel
{
    // Below this alpha a faded subtree drops its colliders, mirroring the deactivation path's
    // "invisible implies non-collidable" rule, and regains them when it fades back above.
    // ⚠ Keep the write edge-triggered on the last collidable state per subtree root; a fade
    // re-writes opacity every tick and re-walking the subtree each frame would thrash.
    // Independent of SetSubtreeActive's collider toggle: separate channels, most recent event wins.
    private const float OpacityCollisionEpsilon = 0.01f;

    private readonly AnimRuntime _rt;

    private readonly Func<AnimEvent, AnimDefinition, Node3D?, List<Node3D>> _targets;

    private readonly MotionSet _motions;

    private readonly Func<EmitterDirector> _emitters;

    private readonly Func<AnimDefinition, int, SiScript?> _scriptFor;

    private readonly Action<string> _count;

    // Last opacity pushed to each subtree root. These events sit in `Loop{-1}` sequences —
    // C1's `cloudparent#` re-asserts its 0.6 every frame — so without this the whole subtree
    // would be re-walked and re-written ~31 times a frame to set values it already holds. Same
    // lesson as LightState's per-light host cache, which cost ~7 ms/frame before it existed.
    private readonly Dictionary<Node3D, float> _opacity = new();

    // The fade twins a genuine partial opacity installs per instance (see EnsureOpacityPath):
    // source material -> its translucent twin (null = cannot be made translucent), the twin
    // set for recognising an override this runtime installed, and the shader-level cache so
    // materials sharing one generated shader share one twin shader.
    private readonly Dictionary<ShaderMaterial, ShaderMaterial?> _fadeTwinCache = new();

    private readonly HashSet<Material> _fadeTwins = new();

    private readonly Dictionary<Shader, Shader?> _fadeShaderCache = new();

    // Nodes that have just landed by contact, whose NEXT ballistic launch must start from where
    // they came to rest rather than the authored rest pose (MotionRuntime.Create's re-home rule).
    // A bounce is a continuation, and re-basing it teleports the piece back to the crash point.
    // ⚠ Keep this one-shot per node and consumed by the launch that follows. The re-home rule
    // itself is what stops pooled effect templates drifting across repeat explosions.
    private readonly HashSet<Node3D> _resumeFromLanding = new();

    public PoseChannel(AnimRuntime rt, Func<AnimEvent, AnimDefinition, Node3D?, List<Node3D>> targets,
        MotionSet motions, Func<EmitterDirector> emitters,
        Func<AnimDefinition, int, SiScript?> scriptFor, Action<string> count)
    {
        _rt = rt;
        _targets = targets;
        _motions = motions;
        _emitters = emitters;
        _scriptFor = scriptFor;
        _count = count;
    }

    /// <summary>The non-sound remainder of OBJECT_ACTIVE_STATE (the router tests
    /// <c>Sound.TrySetActive</c> first): activates/deactivates each target subtree.</summary>
    internal int HandleActiveState(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant)
    {
        int applied = 0;
        foreach (var t in _targets(ev, def, anchor))
        {
            bool active = ev.Data.Bool("state");
            _rt.SetTargetActive(t, active);
            if (!active)
            {
                // A played deactivation spares an emitter started in this same
                // instant (the splash idiom writes both halves and means the second); the
                // RESET_STATE path does not, being base state where the last write wins.
                _emitters().EndOn(t, sparingSameInstant: !instant);
            }
            applied++;
        }
        return applied;
    }

    internal int HandleTranslateState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        int applied = 0;
        var host = AtNode(ev.Data.Str("at_node"));
        foreach (var t in _targets(ev, def, anchor))
            applied += host != null
                ? PoseAtNode(t, host, ev.Data.Vec3("state"), rotate: false)
                : PoseTranslate(t, ev.Data.Vec3("state"), ev.Data.Bool("relative"));
        return applied;
    }

    internal int HandleRotateState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        int applied = 0;
        // The two AT_NODE spellings are different rules, not two names for one: MATRIX takes the
        // host's composed orientation, XYZ takes the rotation the host was last SCRIPTED to. State
        // is radians on both paths already. Decode: docs/formats/anim-definitions/cutscenes.md.
        var basis = ev.Data.Obj("basis");
        var matrixHost = AtNode(basis?.Str("AtNodeMatrix"));
        var xyzHost = matrixHost == null ? AtNode(basis?.Str("AtNodeXYZ")) : null;
        var host = matrixHost ?? xyzHost;
        foreach (var t in _targets(ev, def, anchor))
            applied += host != null
                ? PoseAtNode(t, host, ev.Data.Vec3("state"), rotate: true, scripted: xyzHost != null)
                : PoseRotate(t, ev.Data.Vec3("state"));
        return applied;
    }

    // The AT_NODE host, by name over the whole index: it is a world root of its own, not something
    // the event's anchor contains. The resolver memoizes the lookup, which matters because the
    // letterbox definition re-asserts this every tick.
    internal Node3D? AtNode(string? name)
    {
        if (name == null)
            return null;
        var found = _rt.FindNodes(name);
        return found.Count > 0 ? found[0] : null;
    }

    internal int HandleScaleState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        int applied = 0;
        foreach (var t in _targets(ev, def, anchor))
            applied += PoseScale(t, ev.Data.Vec3("state"));
        return applied;
    }

    internal int HandleMotionFromTo(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant,
        out float duration)
    {
        float runTime = ev.Data.Num("run_time") ?? 0f;
        int applied = 0;
        foreach (var t in _targets(ev, def, anchor))
        {
            var tween = FromToMotion.Create(_rt, t, ev.Data, runTime);
            if (tween == null)
                continue;
            if (instant || runTime <= 0f)
                tween.Seek(runTime); // RESET_STATE / zero-length: land on the end pose
            else
                _motions.Add(tween, def, anchor);
            applied++;
        }
        duration = instant ? 0f : runTime;
        return applied;
    }

    internal int HandleOpacityState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        // ⚠ Read `state` as "translucency enabled", never as visibility: false means
        // render normally, not disappear (docs/formats/anim-definitions.md). Hiding is
        // OBJECT_ACTIVE_STATE's job, and the data uses it right alongside this.
        if (ev.Data.Get("state") is not bool on)
        {
            _count("ObjectOpacityState(no state)");
            return 0;
        }
        float alpha = on ? ev.Data.Num("opacity") ?? 1f : 1f;
        int applied = 0;
        foreach (var t in _targets(ev, def, anchor))
        {
            SetSubtreeOpacity(t, alpha);
            applied++;
        }
        return applied;
    }

    internal int HandleOpacityFromTo(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant,
        out float duration)
    {
        // ⚠ Do not let the endpoint `state` flag invert the value here, as it does on
        // OBJECT_OPACITY_STATE; this is a literal lerp of the two opacity numbers.
        // `opacity_delta` never ships a value, so report one rather than ignoring it.
        float runTime = ev.Data.Num("run_time") ?? 0f;
        duration = 0f;
        var from = ev.Data.Obj("opacity_from");
        var to = ev.Data.Obj("opacity_to");
        if (from == null || to == null)
        {
            _count("ObjectOpacityFromTo(no endpoints)");
            return 0;
        }
        if (ev.Data.Has("opacity_delta"))
            _count("ObjectOpacityFromTo(delta)");
        float o0 = from.Num("opacity") ?? 1f;
        float o1 = to.Num("opacity") ?? 1f;
        int applied = 0;
        foreach (var t in _targets(ev, def, anchor))
        {
            var fade = new OpacityFade(_rt, t, o0, o1, runTime);
            if (instant || runTime <= 0f)
                fade.Seek(runTime); // RESET_STATE / zero-length: land on the end opacity
            else
                _motions.Add(fade, def, anchor);
            applied++;
        }
        duration = instant ? 0f : runTime;
        return applied;
    }

    internal int HandleMotion(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant,
        out float duration)
    {
        duration = 0f;
        // OBJECT_MOTION spans two jobs (docs/org/objectMotion.md): a rotation-only event
        // is a steady spin, the shape every ON_STARTUP event takes; the rest pair
        // motion with GRAVITY/TRANSLATION/SCALE/FORWARD_ROTATION for ballistic debris.
        bool hasBallistic = ev.Data.Has("translation") || ev.Data.Has("translation_range")
                            || ev.Data.Has("scale") || ev.Data.Has("forward_rotation");
        if (hasBallistic)
        {
            // The full rigid-body simulation: a ballistic translate/launch, a scale ramp
            // and a tumble (plus any steady XYZ_ROTATION), all on one node over run_time.
            // See MotionRuntime for the semantics and the TUNE caveats.
            float authored = ev.Data.Num("run_time") ?? 0f;
            // ⚠ Read the flight back off each body rather than assuming it here; a
            // launch with no authored time solves its own from the parabola it drew.
            // The longest of them is what the sequence waits on.
            float ballTime = authored;
            bool bounceArmed = false;
            int applied = 0;
            foreach (var t in _targets(ev, def, anchor))
            {
                var motion = MotionRuntime.Create(_rt, t, ev.Data, authored);
                if (motion == null)
                    continue;
                float flight = motion.RunTime;
                // ⚠ A body runs if it has a duration OR a contact tier to end it.
                // Dropping the second test poses at rest every fall with no apex to
                // solve, a shot-down zeppelin among them. Either reports 0 anyway.
                if (instant || (flight <= 0f && !motion.TestsContact))
                {
                    motion.Seek(0f); // RESET_STATE / zero-length: pose the launch start (rest)
                }
                else
                {
                    _motions.Add(motion, def, anchor); // MotionSet.Add counts the launch
                    ballTime = Mathf.Max(ballTime, flight);
                    // A contact-tested body arms its bounce at contact, since the struck
                    // surface picks the branch; the test being on is enough here.
                    bounceArmed |= motion.PendingBounce != null || motion.TestsContact;
                }
                applied++;
            }
            // ⚠ Never file an ARMED bounce as unhandled; TickMotions dispatches it when
            // the body lands, and counting it reports a working feature as a missing
            // one. Only a fall that never arms is deferred (docs/org/objectMotion.md).
            if (ev.Data.Has("bounce_sequence") && !bounceArmed)
                _count("ObjectMotion(bounce_sequence deferred)");
            duration = instant ? 0f : ballTime;
            return applied;
        }
        // No motion channel: either a steady spin (below) or a bare GRAVITY/BOUNCE stub
        // with nothing to drive (meaningless without translation — reported, not acted on).
        if (ev.Data.Obj("xyz_rotation") is not { } spin)
        {
            bool bareBallistic = ev.Data.Has("gravity") || ev.Data.Has("bounce_sequence");
            _count(bareBallistic ? "ObjectMotion(ballistic)" : ev.Kind);
            return 0;
        }

        var rate = spin.Vec3("initial");
        // ⚠ Report `delta` rather than guessing at it. The data does not settle whether
        // it is acceleration, a decelerating ramp or a random spread, and all but one
        // reachable event leaves it zero.
        if (!spin.Vec3("delta").IsZeroApprox())
            _count("ObjectMotion(rotation delta)");
        if (rate.IsZeroApprox())
            return 0;

        float spinFor = ev.Data.Num("run_time") ?? 0f;
        int spinApplied = 0;
        foreach (var t in _targets(ev, def, anchor))
        {
            // ⚠ Keep re-assertion idempotent. These sit in `Loop{-1}` sequences, and a
            // rebuilt spin re-reads rest from the current pose and restarts its clock,
            // so the prop sits almost still while the logs show it driven.
            if (_motions.HasSpinOn(t, rate, spinFor))
                continue;
            var motion = new SpinMotion(t, rate, spinFor);
            if (instant)
                motion.Seek(0f); // RESET_STATE poses the start; a spin starts unturned
            else
                _motions.Add(motion, def, anchor);
            spinApplied++;
        }
        duration = instant ? 0f : spinFor;
        return spinApplied;
    }

    internal int HandleMotionSiScript(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant,
        out float duration)
    {
        duration = 0f;
        int slot = (int)(ev.Data.Num("index") ?? 0f);
        var script = _scriptFor(def, slot);
        if (script == null)
        {
            _count("ObjectMotionSiScript(no script)");
            return 0;
        }
        // A mission cutscene's SI script owns its clock even when one staged actor is absent.
        // Keep ordinary ambient misses inert; widening this changes unrelated world timelines.
        duration = !instant && _rt.MissionTriggerActive ? script.Duration : 0f;
        int applied = 0;
        foreach (var t in _targets(ev, def, anchor))
        {
            var playback = new ScriptPlayback(_rt, t, script);
            if (instant)
                playback.Seek(0f); // pose at the script's first frame
            else
                _motions.Add(playback, def, anchor);
            applied++;
            duration = Mathf.Max(duration, script.Duration);
        }
        return applied;
    }

    /// <summary>Whether this node's next ballistic launch continues from where it landed, clearing
    /// the mark as it answers. See <see cref="_resumeFromLanding"/>.</summary>
    internal bool ConsumeLandingResume(Node3D target) => _resumeFromLanding.Remove(target);

    /// <summary>Marks a node as having just landed by contact — see
    /// <see cref="_resumeFromLanding"/>. Called on the dispatch path, and by the
    /// <c>ground-contact</c> suite, which drives a motion set directly.</summary>
    internal void MarkLandingResume(Node3D target) => _resumeFromLanding.Add(target);

    // OBJECT_OPACITY_STATE applies to the whole subtree, as a per-instance shader parameter
    // rather than a material edit: SceneBuilder's materials are cached and shared, so writing
    // alpha into one would fade every other node that happens to use it. A partial opacity
    // landing on an opaque-variant mesh (no alpha path in the shader) swaps that instance's
    // surfaces to a fade-capable twin material for the duration — see EnsureOpacityPath;
    // anything still without a path after that is counted, not swallowed.
    internal void SetSubtreeOpacity(Node3D node, float alpha)
    {
        if (_opacity.TryGetValue(node, out float prev) && Mathf.IsEqualApprox(prev, alpha))
            return;
        _opacity[node] = alpha;

        // The fade is a shader parameter, which visibility knows nothing about — so a subtree
        // faded to nothing is marked faded and its colliders derive from that too. Reports only
        // the crossing, not every tick of the fade.
        bool collidable = alpha > OpacityCollisionEpsilon;
        if (WorldCollision.SetFaded(node, !collidable))
        {
            Utils.Log.Info("anim", $"anim: fade {(collidable ? "restored" : "dropped")} colliders under '{node.Name}'");
        }

        int applied = ApplyOpacity(node, alpha);
        if (applied == 0 && !Mathf.IsEqualApprox(alpha, 1f))
            _count("ObjectOpacityState(no alpha path)");
    }

    // The *_STATE poses use the same absolute-in-parent-frame convention as
    // OBJECT_MOTION_FROM_TO — see FromToMotion's remarks for the evidence. OBJECT_TRANSLATE_STATE
    // carries an explicit RELATIVE flag (false in all 1143 uses in this install) and
    // OBJECT_ROTATE_STATE a BASIS of "Absolute" (6430 of ~6600), which is the data saying so
    // outright. Each returns the ops it applied (always 1), the shape every handler above shares.
    internal int PoseTranslate(Node3D target, Vector3 position, bool relative)
    {
        _rt.RestOf(target); // record the authored pose before we disturb it
        target.Position = relative ? target.Position + position : position;
        return 1;
    }

    internal int PoseRotate(Node3D target, Vector3 radians)
    {
        var rest = _rt.RestOf(target);
        target.Basis = Basis.FromEuler(radians, EulerOrder.Yxz)
                            .Scaled(rest.Basis.Scale);
        return 1;
    }

    // The AT_NODE form of the two pose events: the target takes another node's world frame, plus an
    // authored offset in that frame. Written globally because the host is a root of its own and the
    // target need not share its parent. `scripted` is the AT_NODE_XYZ rule: the host contributes the
    // rotation it was placed at rather than the one it is flying at (AnimRuntime.PlacedRotationOf).
    // ⚠ Read the host's frame through AnimRuntime.WorldTransform, not GlobalTransform: during the
    // bootstrap the world root is not parented and a bare global read answers identity.
    internal int PoseAtNode(Node3D target, Node3D host, Vector3 offset, bool rotate,
        bool scripted = false)
    {
        var rest = _rt.RestOf(target);
        var frame = AnimRuntime.WorldTransform(host, out _).Orthonormalized();
        if (scripted && _rt.PlacedRotationOf(host) is { } placed)
        {
            frame = new Transform3D(frame.Basis * host.Basis.Orthonormalized().Inverse() * placed,
                frame.Origin);
        }

        var current = AnimRuntime.WorldTransform(target, out bool detached);
        var desired = rotate
            ? new Transform3D((frame.Basis * Basis.FromEuler(offset, EulerOrder.Yxz)).Scaled(rest.Basis.Scale), current.Origin)
            : new Transform3D(current.Basis, frame.Origin + (frame.Basis * offset));

        if (detached)
        {
            var parentWorld = target.GetParent() is Node3D parent
                ? AnimRuntime.WorldTransform(parent, out _)
                : Transform3D.Identity;
            target.Transform = parentWorld.AffineInverse() * desired;
        }
        else if (rotate)
        {
            target.GlobalBasis = desired.Basis;
        }
        else
        {
            target.GlobalPosition = desired.Origin;
        }

        return 1;
    }

    internal int PoseScale(Node3D target, Vector3 scale)
    {
        if (scale.LengthSquared() < 1e-9f)
            return 0;
        var rest = _rt.RestOf(target);
        target.Basis = rest.Basis.Orthonormalized().Scaled(AnimRuntime.NonSingularScale(scale));
        return 1;
    }

    // Whether this mesh's shader reads the opacity parameter, and if not, whether a fade twin can
    // give it one. A partial opacity installs a per-surface override on THIS instance only.
    // ⚠ Never edit the shared material or mesh; both are cached across nodes. Opacity 1 removes
    // the override again. ⚠ Test for the USE (SceneBuilder.OpacityTerm), never the uniform name or
    // the include line: the uniform is declared in the shared preamble, so a name test is true even
    // with no alpha path, and the declaration is not textually in sh.Code.
    private bool EnsureOpacityPath(GeometryInstance3D g, float alpha)
    {
        if (g is not MeshInstance3D mi || mi.Mesh is not { } mesh)
            return false;
        bool fading = !Mathf.IsEqualApprox(alpha, 1f);
        bool any = false;
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            if (mi.GetSurfaceOverrideMaterial(i) is { } installed && _fadeTwins.Contains(installed))
            {
                if (fading)
                    any = true;
                else
                    mi.SetSurfaceOverrideMaterial(i, null);
                continue;
            }
            if (mesh.SurfaceGetMaterial(i) is not ShaderMaterial { Shader: { } sh } sm)
                continue;
            if (sh.Code.Contains(SceneBuilder.OpacityTerm, StringComparison.Ordinal))
            {
                any = true;
                continue;
            }
            if (fading && FadeTwinOf(sm, sh) is { } twin)
            {
                mi.SetSurfaceOverrideMaterial(i, twin);
                any = true;
            }
        }
        return any;
    }

    private ShaderMaterial? FadeTwinOf(ShaderMaterial source, Shader shader)
    {
        if (_fadeTwinCache.TryGetValue(source, out var twin))
            return twin;
        if (!_fadeShaderCache.TryGetValue(shader, out var fadeShader))
            _fadeShaderCache[shader] = fadeShader = SceneBuilder.FadeShaderFor(shader);
        if (fadeShader != null)
        {
            twin = (ShaderMaterial)source.Duplicate();
            twin.Shader = fadeShader;
            _fadeTwins.Add(twin);
        }
        _fadeTwinCache[source] = twin;
        return twin;
    }

    private int ApplyOpacity(Node node, float alpha)
    {
        int n = 0;
        if (node is GeometryInstance3D g)
        {
            g.SetInstanceShaderParameter(SceneBuilder.OpacityParam, alpha);
            if (EnsureOpacityPath(g, alpha))
                n++;
        }
        foreach (var child in node.GetChildren())
            n += ApplyOpacity(child, alpha);
        return n;
    }
}

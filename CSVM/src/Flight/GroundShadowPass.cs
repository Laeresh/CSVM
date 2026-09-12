using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The per-frame ground-shadow pass: one modulating quad under every live aircraft, placed,
/// sized, coloured and dropped by <see cref="GroundShadowLaw"/>, with the aircraft's own
/// silhouette rasterised into its texture by <see cref="GroundShadowSilhouette"/>. Original
/// graphics mode only, enhanced mode casting real shadow maps instead (docs/architecture/Root.md).
/// ⚠ The quad is flat where the original modulates the ground's own polygons, so the shadow
/// rides over a steep enough slope. Decode: docs/org/shadows.md.
/// </summary>
public sealed partial class GroundShadowPass : Node3D
{
    // The quad is a separate surface lying over ground the original never separates from: it
    // modulates the terrain's own polygons. Lifting it clear is this stand-in's own constant,
    // decoded from nothing, and is why the shadow rides visibly over a steep enough slope.
    private const float GroundLift = 0.5f;

    // The pass reads poses the flight rigs write in their own _Process, so it runs after every
    // default-priority node rather than a frame behind them.
    private const int AfterFlightRigs = 1;

    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_mul, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

        uniform sampler2D coverage : filter_linear, repeat_disable;
        uniform vec3 shadow_color;

        void fragment() {
            // blend_mul multiplies the frame buffer by ALBEDO, so white darkens nothing at all
            // and the ramp runs from there to the shadow colour.
            ALBEDO = mix(vec3(1.0), shadow_color, texture(coverage, UV).r);
            ALPHA = 1.0;
        }
        """;

    private readonly Dictionary<ulong, Caster> _casters = new();
    private readonly List<ulong> _retired = new();
    private readonly Func<IReadOnlyList<FlightController>> _aircraft;
    private readonly Func<IReadOnlyList<Vector3>> _players;
    private readonly Func<(Vector3 Diffuse, Vector3 Ambient)> _sunlight;
    private readonly Mesh _quad;

    private GroundShadowPass(Func<IReadOnlyList<FlightController>> aircraft,
        Func<IReadOnlyList<Vector3>> players, Func<(Vector3 Diffuse, Vector3 Ambient)> sunlight)
    {
        _aircraft = aircraft;
        _players = players;
        _sunlight = sunlight;
        _quad = new PlaneMesh { Size = Vector2.One };
        Name = "ground_shadows";
        ProcessPriority = AfterFlightRigs;
    }

    /// <summary>How many shadows are drawn right now, for the suites and for a scripted
    /// run.</summary>
    public int Drawn
    {
        get
        {
            int drawn = 0;
            foreach (var caster in _casters.Values)
            {
                if (caster.Quad.Visible)
                    drawn++;
            }

            return drawn;
        }
    }

    /// <summary>Build the pass under a session's world root, or nothing at all in enhanced
    /// graphics mode, which lights and shadows the world itself.</summary>
    public static GroundShadowPass? Build(Node3D worldRoot,
        Func<IReadOnlyList<FlightController>> aircraft, Func<IReadOnlyList<Vector3>> players,
        Func<(Vector3 Diffuse, Vector3 Ambient)> sunlight)
    {
        if (GraphicsMode.Enhanced)
        {
            Log.Info("world", $"ground shadows: off (enhanced graphics casts its own)");
            return null;
        }

        var pass = new GroundShadowPass(aircraft, players, sunlight);
        worldRoot.AddChild(pass);
        Log.Info("world", $"ground shadows: on, to {GroundShadowLaw.CutoffAltitude:0} m of altitude and {GroundShadowLaw.FarDistance:0} m of range");
        return pass;
    }

    /// <summary>The shadow quad under one aircraft, or null when that aircraft casts none. The
    /// suites read its transform; nothing else has a reason to.</summary>
    public MeshInstance3D? QuadFor(FlightController rig) =>
        _casters.TryGetValue(rig.GetInstanceId(), out var caster) && caster.Quad.Visible
            ? caster.Quad : null;

    /// <summary>The silhouette behind one aircraft's shadow, as the last pass rasterised it. The
    /// suites read its coverage; nothing else has a reason to.</summary>
    public GroundShadowSilhouette? ShapeFor(FlightController rig) =>
        _casters.TryGetValue(rig.GetInstanceId(), out var caster) ? caster.Shape : null;

    public override void _Process(double delta) => Tick();

    /// <summary>One pass over the roster: place, colour or drop every aircraft's shadow. Driven
    /// per rendered frame, and called directly by the suites, which pump no frames.</summary>
    public void Tick()
    {
        var aircraft = _aircraft();
        var players = _players();
        var (diffuse, ambient) = _sunlight();
        foreach (var rig in aircraft)
        {
            if (!GodotObject.IsInstanceValid(rig) || rig.PlaneModel is not { } model)
                continue;
            Place(rig, model, CasterFor(rig, model), players, diffuse, ambient);
        }

        Retire(aircraft);
    }

    // The surface under one aircraft: the highest collider at or below it, which is what the
    // original's own terrain-column wrapper answers. ⚠ Read downward from the aircraft, never a
    // whole column: the projected path takes the column's plain maximum and so snaps its shadow
    // to a surface ABOVE the aircraft under a bridge, which is a bug in the original.
    private bool Ground(Vector3 at, out float groundY)
    {
        groundY = 0f;
        if (GetWorld3D()?.DirectSpaceState is not { } space)
            return false;
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
            at, at - (Vector3.Up * GroundShadowLaw.CutoffAltitude), CollisionLayers.World));
        if (hit.Count == 0)
            return false;
        groundY = ((Vector3)hit["position"]).Y;
        return true;
    }

    // One aircraft's shadow, built on first sight of it. Its shape is read once here: an airframe
    // swap rebuilds the rig and with it this entry.
    private Caster CasterFor(FlightController rig, Node3D model)
    {
        ulong id = rig.GetInstanceId();
        if (_casters.TryGetValue(id, out var known))
            return known;
        var shape = GroundShadowSilhouette.For(model, rig.IsHumanPiloted);
        var material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        material.SetShaderParameter("coverage", shape.Texture);
        var quad = new MeshInstance3D
        {
            Mesh = _quad,
            MaterialOverride = material,
            TopLevel = true, // placed in world coordinates, not under the pass's own frame
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Name = $"shadow_{id}",
            Visible = false,
        };
        AddChild(quad);
        var caster = new Caster(quad, material, shape);
        _casters[id] = caster;
        return caster;
    }

    // One aircraft's shadow for this frame. The law decides everything except whether there is
    // ground under it at all.
    private void Place(FlightController rig, Node3D model, Caster caster,
        IReadOnlyList<Vector3> players, Vector3 diffuse, Vector3 ambient)
    {
        var quad = caster.Quad;
        if (!rig.InPlay || rig.Crashed || !model.Visible)
        {
            quad.Visible = false;
            return;
        }

        var pose = model.GlobalTransform;
        bool isPlayer = rig.IsHumanPiloted;
        float distance = isPlayer ? 1f : NearestPlayerFactor(pose.Origin, players);
        if (distance <= 0f || !Ground(pose.Origin, out float groundY))
        {
            quad.Visible = false;
            return;
        }

        float altitudeFactor = GroundShadowLaw.AltitudeFactor(pose.Origin.Y - groundY);
        float strength = altitudeFactor * distance;
        if (strength <= 0f)
        {
            quad.Visible = false;
            return;
        }

        var direction = GroundShadowLaw.Direction(isPlayer, -pose.Basis.Z.Normalized());
        var origin = GroundShadowLaw.Project(pose.Origin, groundY, direction);
        var footprint = Footprint(caster.Shape.Box, pose, groundY, direction);
        caster.Shape.Raster(pose, groundY, direction, footprint);
        float scale = GroundShadowLaw.FootprintScale(isPlayer, altitudeFactor);
        var min = origin + ((footprint.Position - origin) * scale);
        var max = origin + ((footprint.End - origin) * scale);
        quad.GlobalTransform = new Transform3D(
            Basis.Identity.Scaled(new Vector3(Mathf.Abs(max.X - min.X), 1f, Mathf.Abs(max.Z - min.Z))),
            new Vector3((min.X + max.X) * 0.5f, groundY + GroundLift, (min.Z + max.Z) * 0.5f));
        // The law answers in the original's gamma space, as its 8-bit modulate texture is; the
        // shader multiplies a linear frame buffer, where the same darkening is the linear value.
        var colour = GroundShadowLaw.Colour(diffuse, ambient, direction, strength).SrgbToLinear();
        caster.Material.SetShaderParameter(
            "shadow_color", new Vector3(colour.R, colour.G, colour.B));
        quad.Visible = true;
    }

    // The footprint: the model's eight bounding-box corners, carried into the world by the pose
    // it is drawn at and flattened onto the ground along the same direction as its origin.
    private Aabb Footprint(Aabb box, Transform3D pose, float groundY, Vector3 direction)
    {
        var projected = new Aabb(
            GroundShadowLaw.Project(pose * box.GetEndpoint(0), groundY, direction), Vector3.Zero);
        for (int i = 1; i < 8; i++)
            projected = projected.Expand(
                GroundShadowLaw.Project(pose * box.GetEndpoint(i), groundY, direction));
        return projected;
    }

    // The distance fade runs off the nearest player, the same "who is nearest" reading the
    // effects and the projectile pool take. No players at all leaves the shadow at full strength.
    private float NearestPlayerFactor(Vector3 at, IReadOnlyList<Vector3> players)
    {
        if (players.Count == 0)
            return 1f;
        float factor = 0f;
        foreach (var player in players)
            factor = Mathf.Max(factor, GroundShadowLaw.DistanceFactor(at, player));
        return factor;
    }

    // A shadow outlives its aircraft by one frame at most: the original frees the object the
    // moment its strength reaches zero, and a retired rig never reports one again.
    private void Retire(IReadOnlyList<FlightController> aircraft)
    {
        _retired.Clear();
        foreach (var id in _casters.Keys)
        {
            bool live = false;
            foreach (var rig in aircraft)
            {
                if (GodotObject.IsInstanceValid(rig) && rig.GetInstanceId() == id)
                {
                    live = true;
                    break;
                }
            }

            if (!live)
                _retired.Add(id);
        }

        foreach (var id in _retired)
        {
            _casters[id].Quad.QueueFree();
            _casters.Remove(id);
        }
    }

    private sealed record Caster(
        MeshInstance3D Quad, ShaderMaterial Material, GroundShadowSilhouette Shape);
}

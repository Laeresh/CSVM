using System;
using System.Collections.Generic;
using CrimsonSkies.Effects;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The crash breakup (Run-2 item 10d): on impact the healthy model is hidden
/// (FlightController) and the plane's 'destroyed' subtree — a handful of pieceN
/// wreck meshes the data ships per plane — appears at the crash pose, each piece a
/// free body with an impact-derived velocity. The pieces are hand-simulated
/// (ballistic + tumble, like the puffers: deterministic and cheap), rest on the
/// terrain via a short downward ray, and persist until respawn. On top of the
/// existing fireball + explosion sound, the wreck burns: the
/// player_plane_destruct.json 'fire_n_smoke' puffer (sustained fire) and the
/// pufftrails 'black_smoke' (as a rising trail emitted over the burn time) run at
/// the impact point for ~10 s. The full player_crash_default/_dirt/_water
/// choreography (sparks, surface variants, plane_destroy_sg) stays on the backlog.
/// </summary>
public sealed class CrashBreakup
{
    private struct Piece
    {
        public Node3D Node;
        public Transform3D Local;    // rest pose inside the destroyed group
        public Vector3 Velocity;     // world m/s
        public Vector3 SpinAxis;     // world, unit
        public float SpinRate;       // rad/s
        public bool Resting;
    }

    private readonly Node3D _root;
    private readonly Transform3D _chain; // baked plane-root → destroyed-group transform
    private readonly Puffer? _fire, _smoke;
    private Piece[] _pieces = Array.Empty<Piece>();
    private readonly Random _rng = new();
    private bool _active;
    private float _burnLeft;
    private Vector3 _smokeAnchor;

    private const float ScatterSpeed = 7f;    // TUNE: m/s of random per-piece scatter
    private const float KeepVelocity = 0.45f; // TUNE: fraction of impact velocity the pieces keep
    private const float UpKick = 5f;          // TUNE: m/s upward bias (thrown clear of the ground)
    private const float MaxSpin = 4f;         // TUNE: rad/s tumble cap
    private const float Gravity = 9.81f;
    private const float RestOffset = 0.4f;    // m above the ground a piece settles
    private const float BurnTime = 10f;       // s of wreck fire/smoke (plan's ~10 s)
    private const float SmokeRise = 2.2f;     // m/s the black-smoke emit point climbs (a rising column)

    private CrashBreakup(Node3D root, Puffer? fire, Puffer? smoke)
    {
        _root = root;
        _chain = root.Transform; // captured before Begin re-bases the root to world space
        _fire = fire;
        _smoke = smoke;
    }

    /// <summary>Wraps a built 'destroyed' subtree (see PlaneBuilder.BuildDestroyed) —
    /// hidden until a crash. Null if the subtree is missing or empty.</summary>
    public static CrashBreakup? Create(Node3D? destroyedRoot, Puffer? fire, Puffer? smoke)
    {
        if (destroyedRoot == null)
            return null;
        var pieces = new List<Piece>();
        foreach (var child in destroyedRoot.GetChildren())
            if (child is Node3D p)
                pieces.Add(new Piece { Node = p, Local = p.Transform });
        if (pieces.Count == 0)
        {
            destroyedRoot.Free(); // built but useless (never parented)
            return null;
        }
        destroyedRoot.Visible = false;
        var breakup = new CrashBreakup(destroyedRoot, fire, smoke) { _pieces = pieces.ToArray() };
        return breakup;
    }

    public int PieceCount => _pieces.Length;

    /// <summary>The (hidden) wreck-piece holder; the caller parents it once. The fire
    /// and smoke puffers are parented by their builder.</summary>
    public Node3D WreckRoot => _root;

    /// <summary>Starts the breakup: pieces appear at the crashed plane's pose and
    /// scatter with the impact velocity; the wreck fire/smoke starts at the impact
    /// point.</summary>
    public void Begin(Transform3D planePose, Vector3 impactPoint, Vector3 velocity)
    {
        _active = true;
        _burnLeft = BurnTime;
        // pieces live in world space from here (their holder ignores the plane)
        _root.TopLevel = true;
        _root.GlobalTransform = Transform3D.Identity;
        for (int i = 0; i < _pieces.Length; i++)
        {
            ref var p = ref _pieces[i];
            p.Node.Transform = planePose * _chain * p.Local;
            p.Velocity = velocity * KeepVelocity
                + new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)) * ScatterSpeed
                + Vector3.Up * (UpKick * Rand(0.4f, 1f));
            p.SpinAxis = new Vector3(Rand(-1f, 1f), Rand(-1f, 1f), Rand(-1f, 1f)).Normalized();
            p.SpinRate = Rand(0.5f, MaxSpin);
            p.Resting = false;
        }
        _root.Visible = true;
        _fire?.Burst(impactPoint);
        _smokeAnchor = impactPoint;
        _smoke?.TrailAdvance(_smokeAnchor); // starts the trail; risen anchor feeds it in Advance
    }

    /// <summary>Advances the piece ballistics + the burn; call each physics frame
    /// while crashed.</summary>
    public void Advance(float dt, PhysicsDirectSpaceState3D? space)
    {
        if (!_active)
            return;
        for (int i = 0; i < _pieces.Length; i++)
        {
            ref var p = ref _pieces[i];
            if (p.Resting)
                continue;
            p.Velocity += Vector3.Down * (Gravity * dt);
            var from = p.Node.GlobalPosition;
            var to = from + p.Velocity * dt;
            // ground-rest: a short ray along this frame's fall
            if (space != null && p.Velocity.Y < 0f)
            {
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to + Vector3.Down * RestOffset));
                if (hit.Count > 0)
                {
                    p.Node.GlobalPosition = (Vector3)hit["position"] + Vector3.Up * RestOffset;
                    p.Resting = true;
                    continue;
                }
            }
            p.Node.GlobalPosition = to;
            p.Node.RotateObjectLocal(p.Node.GlobalBasis.Inverse() * p.SpinAxis, p.SpinRate * dt);
        }

        if (_burnLeft > 0f)
        {
            _burnLeft -= dt;
            // the black smoke column rises out of the fire
            _smokeAnchor += Vector3.Up * (SmokeRise * dt);
            _smoke?.TrailAdvance(_smokeAnchor);
            if (_burnLeft <= 0f)
                _smoke?.TrailEnd();
        }
    }

    /// <summary>Hides the wreck and stops the burn (respawn).</summary>
    public void Reset()
    {
        _active = false;
        _root.Visible = false;
        for (int i = 0; i < _pieces.Length; i++)
        {
            ref var p = ref _pieces[i];
            p.Node.Transform = p.Local;
            p.Resting = false;
        }
        _fire?.Clear();
        _smoke?.Clear();
    }

    private float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
}

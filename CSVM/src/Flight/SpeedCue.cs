using System;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The chapter-authored speed cue: large, pale smoke sprites emitted from a point 60 m ahead of
/// the player's aircraft and left in world space for the aircraft to pass. The three
/// <c>cuepufferN</c> states come directly from <c>speed_cue.zrd</c>; this adapter owns only the
/// animation's altitude selection and lifecycle.
/// </summary>
public sealed class SpeedCue
{
    public const float NearGroundMeters = 50f;
    public const float Cue1CeilingMeters = 800f;
    public const float Cue2LowCeilingMeters = 900f;
    public const float Cue3CeilingMeters = 1200f;
    public const float Cue2HighCeilingMeters = 1500f;

    private const float SelectionIntervalSeconds = 0.1f;

    private readonly Puffer[] _puffers;
    private Selection _active = Selection.None;
    private float _selectionTimer;

    private SpeedCue(Puffer[] puffers) => _puffers = puffers;

    private enum Selection
    {
        None,
        Cue1,
        Cue2,
        Cue3,
        KeepCurrent,
    }

    /// <summary>Loads all three authored states and parents their world-space renderers beneath
    /// <paramref name="parent"/>. The optional decorator stamps a splitscreen visual layer after
    /// each puffer has built its renderer subtree.</summary>
    public static SpeedCue? Build(string chapterZrdrPath, TextureArchive textures, Node parent,
        EffectAmbience? ambience = null, Action<Node>? decorate = null)
    {
        var puffers = new Puffer[3];
        for (int i = 0; i < puffers.Length; i++)
        {
            if (Puffer.MakePuffer(chapterZrdrPath, textures, parent, "speed_cue.json",
                    $"cuepuffer{i + 1}", ambience: ambience) is not { } puffer)
            {
                for (int built = 0; built < i; built++)
                    puffers[built].QueueFree();
                return null;
            }
            decorate?.Invoke(puffer);
            puffers[i] = puffer;
        }
        Log.Info("flight", $"speed cue: {puffers.Length} authored smoke puffers ready (30/15/8 m intervals, player local offset 0,0,-60)");
        return new SpeedCue(puffers);
    }

    /// <summary>Test entry point over real <see cref="Puffer"/> instances with fake renderers.</summary>
    public static SpeedCue CreateWith(Puffer cue1, Puffer cue2, Puffer cue3) =>
        new(new[] { cue1, cue2, cue3 });

    /// <summary>Advances the authored altitude menu and feeds the selected distance puffer the
    /// aircraft's current world pose. Above 1500 m the script has no ELSE events, so it preserves
    /// whichever puffer was already active; starting there leaves the effect off.</summary>
    public void Update(float dt, Vector3 playerPosition, Basis playerBasis,
        float cameraAltitude, float cameraAgl)
    {
        _selectionTimer -= dt;
        if (_selectionTimer <= 0f)
        {
            _selectionTimer += SelectionIntervalSeconds;
            Selection selected = Select(cameraAltitude, cameraAgl);
            if (selected != Selection.KeepCurrent && selected != _active)
            {
                if (IsCue(_active))
                    PufferFor(_active).Stop();
                _active = selected;
            }
        }
        if (IsCue(_active))
            PufferFor(_active).Emit(playerPosition, playerBasis, dt);
    }

    /// <summary>Crash/respawn lifecycle: remove every live cue and forget the previous altitude
    /// band, so a teleported aircraft cannot draw a trail from its old position.</summary>
    public void Reset()
    {
        foreach (var puffer in _puffers)
            puffer.Clear();
        _active = Selection.None;
        _selectionTimer = 0f;
    }

    private static Selection Select(float altitude, float agl)
    {
        if (agl < NearGroundMeters)
            return Selection.None;
        if (altitude < Cue1CeilingMeters)
            return Selection.Cue1;
        if (altitude < Cue2LowCeilingMeters)
            return Selection.Cue2;
        if (altitude < Cue3CeilingMeters)
            return Selection.Cue3;
        if (altitude < Cue2HighCeilingMeters)
            return Selection.Cue2;
        return Selection.KeepCurrent;
    }

    private static bool IsCue(Selection selection) =>
        selection is >= Selection.Cue1 and <= Selection.Cue3;

    private Puffer PufferFor(Selection selection) =>
        _puffers[(int)selection - (int)Selection.Cue1];
}

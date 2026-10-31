using System;
using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>A seat's respawn rule (CONTEXT.md), fixed for the launch. The mode picks one of the
/// named rules below and a network session may only tighten it (<see cref="OnTheWire"/>). Lives,
/// a lost aircraft and a race's final run end a return without changing the rule.</summary>
/// <param name="AutoRespawnAfter">Seconds on the crash camera before the seat returns on its own,
/// or null for the respawn control only.</param>
/// <param name="WaitsOnFire">Whether a run-down timer then waits for Fire Guns</param>
/// <param name="Placement">Who places the return</param>
/// <param name="LiveRespawn">Whether the respawn control works on a live aircraft</param>
public sealed record RespawnRule(float? AutoRespawnAfter, bool WaitsOnFire, ReturnPlacement Placement, bool LiveRespawn)
{
    /// <summary>Free flight and the stunt runs: the respawn control only, back at the spawn. On a
    /// live aircraft it means "put me back at the spawn".</summary>
    public static readonly RespawnRule FreeFlight = new(null, false, ReturnPlacement.Spawn, true);

    /// <summary>A story mission is lost with the aeroplane, so no live respawn. A crash still
    /// returns on the respawn control under <c>--no-crash-loss</c>.</summary>
    public static readonly RespawnRule Campaign = FreeFlight with { LiveRespawn = false };

    /// <summary>A bot a race machine flies: nobody presses the respawn control for it, so a crash
    /// returns it unattended.</summary>
    public static readonly RespawnRule RaceBot = FreeFlight with { AutoRespawnAfter = AircraftLifecycle.AutoRespawnDelay };

    /// <summary>Crash cam, then back in, the respawn control skips. Lives make it the crash cam's
    /// skip alone, except in a race, whose pilot restarts at will (the time attack's rule).</summary>
    public static RespawnRule InstantAction(float delay, bool race) => new(delay, false, ReturnPlacement.Spawn, race);

    /// <summary>Dogfight, split screen or wired: crash cam, then back in. With the lobby's Auto
    /// Respawn off it then waits for Fire Guns, as the original's does. ⚠ Never on a bot seat,
    /// which has no Fire Guns to press and would stay down for the match. No live respawn, since the
    /// match counts every return.</summary>
    public static RespawnRule Versus(float delay, bool isBot, bool autoRespawn) =>
        new(delay, !autoRespawn && !isBot, ReturnPlacement.Spawn, false);

    /// <summary>This rule on a network session: no live respawn but in a stunt race, a free repair
    /// the host never grants. It only refuses, so a mode's own refusal stands.</summary>
    public RespawnRule OnTheWire(bool race) => race ? this : this with { LiveRespawn = false };
}

/// <summary>Who places a seat's return from a crash.</summary>
public abstract record ReturnPlacement
{
    /// <summary>Back on the spawn pose <c>Setup</c> fixed, which every mode but the dogfight wants.</summary>
    public static readonly ReturnPlacement Spawn = new AtSpawn();

    private ReturnPlacement()
    {
    }

    /// <summary>The spawn pose.</summary>
    public sealed record AtSpawn : ReturnPlacement;

    /// <summary>This machine picks the pose, asked once per return: the dogfight's rotation. A null
    /// answer keeps the pose the seat has.</summary>
    public sealed record Rotation(Func<(Vector3 Pos, Vector3 LookAt)?> Place) : ReturnPlacement;

    /// <summary>Somebody else grants the return. A due timer and the respawn control ask on every
    /// step the timer is due, so asking the wire once is the asker's. The seat flies again when the
    /// session calls <c>RespawnAt</c> with the answer.</summary>
    public sealed record AskHost(Action Ask) : ReturnPlacement;
}

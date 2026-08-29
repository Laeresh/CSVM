using System;

namespace CSVM.Session;

/// <summary>The named runtime roles advanced by <see cref="SessionSimulation"/>.</summary>
public interface ISessionSimulationRuntime
{
    bool SimHeld { get; }
    bool EndingHold { get; }

    void StepEndingHold(float dt);
    void CaptureAiAircraft();
    void StepIncomingFire(float dt);
    void StepProjectiles(float dt);
    void StepHumanAircraft(float dt);
    void StepZeppelins(float dt);
    void StepTurretEmplacements(float dt);
    void StepGenerators(float dt);
    void StepSurfaceVehicles(float dt);
    void StepCapturedAiAircraft(float dt);
    void StepLandingApproaches();
    void StepInstantAction(float dt);
    void StepCampaign(float dt);
    void StepRadio(float dt);
    void StepSmokeScreens(float dt);
    void StepBeeperTags(float dt);
    void StepAiVoice(float dt);
    void StepVersus(float dt);
}

/// <summary>
/// Advances one haltable session-simulation step in the canonical dependency order. The runtime
/// retains every subsystem and its state; this module owns only admission, ordering and the hold
/// gate. Authored animation and presentation do not enter this boundary.
/// </summary>
public sealed class SessionSimulation
{
    private readonly ISessionSimulationRuntime _runtime;

    public SessionSimulation(ISessionSimulationRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    /// <summary>Advances exactly one requested step, or no-ops while the session is held.</summary>
    public void Step(float dt)
    {
        if (_runtime.EndingHold)
        {
            _runtime.StepEndingHold(dt);
            return;
        }
        if (_runtime.SimHeld)
            return;

        // Membership is fixed at step entry. A generator may append aircraft below, but those
        // aircraft are not eligible until the next step (including the next catch-up substep).
        _runtime.CaptureAiAircraft();

        _runtime.StepIncomingFire(dt);
        _runtime.StepProjectiles(dt);
        _runtime.StepHumanAircraft(dt);
        _runtime.StepZeppelins(dt);
        _runtime.StepTurretEmplacements(dt);
        _runtime.StepGenerators(dt);
        // A hull a generator launched this step is not eligible until the next one, the same
        // admission the captured-aircraft membership above takes.
        _runtime.StepSurfaceVehicles(dt);
        _runtime.StepCapturedAiAircraft(dt);
        _runtime.StepLandingApproaches();
        if (_runtime.SimHeld)
            return;

        _runtime.StepInstantAction(dt);
        _runtime.StepCampaign(dt);
        if (_runtime.EndingHold || _runtime.SimHeld)
            return;

        _runtime.StepRadio(dt);
        _runtime.StepSmokeScreens(dt);
        _runtime.StepBeeperTags(dt);
        _runtime.StepAiVoice(dt);
        _runtime.StepVersus(dt);
    }
}

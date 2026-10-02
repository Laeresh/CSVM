using System;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>What one pilot's own aircraft wears in a first-person view, applied frame by frame to the
/// pose the camera actually took. It is the body hide (<see cref="Visibility"/>), the interior's
/// render pass (<see cref="Pass"/>) and the authored panel's needles (<see cref="Panel"/>). The
/// screen-space cluster comes off while that panel is on the screen. Every external pose takes it
/// all off again, a held look-behind included. Every part is null on a rig built no interior, an AI
/// aircraft or a lab plane, which then dresses nothing. Decode: docs/org/cameraViews.md.</summary>
public sealed class FirstPersonDressing
{
    private readonly FlightHud _hud;

    // The interior is on the screen this frame, which is what the panel's needles are driven on.
    private bool _panelShown;

    /// <summary>Dresses the pilot whose screen-space instruments <paramref name="hud"/> draws.</summary>
    public FirstPersonDressing(FlightHud hud)
    {
        ArgumentNullException.ThrowIfNull(hud);
        _hud = hud;
    }

    /// <summary>The per-mode hiding of the pilot's own aircraft (interior in, body out; Nose also
    /// drops markers and dontmove).</summary>
    public CockpitVisibility? Visibility { get; set; }

    /// <summary>The authored instrument panel inside the interior: needles and the two warning
    /// lamps, driven off the same readings the screen-space cluster draws.</summary>
    public CockpitGauges? Panel { get; set; }

    /// <summary>The <c>cockpit1</c> subtree itself, as the plane builder returned it.</summary>
    public Node3D? Interior { get; set; }

    /// <summary>The interior's own render pass, which takes <see cref="Interior"/> out of the plane
    /// model and draws it at the origin. Null under <c>--no-cockpit-pass</c>, when the interior
    /// renders in the main world instead.</summary>
    public CockpitOverlay? Pass { get; set; }

    /// <summary>One frame's dressing for <paramref name="selected"/> under the pose the frame took.
    /// <paramref name="firstPersonPose"/> is that pose, not the selection, so a held look-behind
    /// brings the body back. The pass follows the drawn attitude and the airframe's wobble, and
    /// mirrors <paramref name="pool"/>'s live muzzle flashes.</summary>
    public void Show(PilotViewMode selected, bool firstPersonPose, Basis drawn, CameraController camera,
        float shakeRoll, ProjectilePool? pool)
    {
        Visibility?.Apply(selected, firstPersonPose);
        // Same rule, so the panel is driven exactly on the frames it is on the screen.
        SetPanel(CockpitVisibility.Rules(selected, firstPersonPose).Interior);
        // After the hide, so the pass shows exactly the frames the interior itself does.
        Pass?.Sync(drawn, camera, shakeRoll, pool?.ActiveMuzzleLights());
    }

    /// <summary>Takes every first-person part off for an outside vantage. That is a static camera
    /// cut, or an episode that owns the camera. <paramref name="selected"/> is the view the pilot
    /// keeps selected underneath.</summary>
    public void Leave(PilotViewMode selected)
    {
        Visibility?.Apply(selected, firstPerson: false);
        Pass?.Deactivate();
        SetPanel(false);
    }

    /// <summary>Drives the panel's needles off this frame's cluster readings, on the frames the
    /// interior is on the screen. Called after the HUD's own feed, so the needles show this frame's
    /// readings rather than trailing the screen-space dials by one.</summary>
    public void DriveNeedles()
    {
        if (_panelShown && Panel != null && _hud.Gauges is { } readings)
            Panel.Apply(readings);
    }

    // The interior's on-screen frames drive two things together. The authored panel's needles
    // move, and the screen-space cluster comes off so the panel is not doubled by it.
    private void SetPanel(bool shown)
    {
        _panelShown = shown;
        _hud.SetCockpitView(shown);
    }
}

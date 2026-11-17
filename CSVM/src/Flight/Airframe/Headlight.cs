using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>The headlight: a forward cone of light ahead of the aircraft's nose that a human pilot
/// switches, which the original does not have. The switch starts off with the aircraft and nothing
/// resets it, so it survives a respawn. The caller allows the beam under Enhanced graphics alone,
/// and it casts shadows unless Shadow Quality is Off. The light is built on its first shown frame,
/// so an aircraft that never lights it pays nothing. The switch shares a control with the view
/// cycle (<see cref="Read"/>).</summary>
public sealed class Headlight
{
    /// <summary>The beam's reach in metres. TUNE: judged at the controls.</summary>
    public const float Range = 250f;

    /// <summary>The cone's half-angle in degrees. TUNE.</summary>
    public const float ConeDegrees = 20f;

    /// <summary>The beam's energy. TUNE.</summary>
    public const float Energy = 8f;

    /// <summary>How far ahead of the hull's front the light sits, in metres. ⚠ Keep it outside the
    /// hull: a light inside the nose has its whole beam shadowed by the airframe.</summary>
    public const float NoseClearance = 0.5f;

    /// <summary>Where the light sits on an aircraft with no model to measure, metres ahead of its origin.</summary>
    public const float BareNoseDistance = 5f;

    /// <summary>The beam's warm white. TUNE.</summary>
    public static readonly Color WarmWhite = new(1f, 0.9f, 0.75f);

    private readonly TapHoldButton _keySplit = new(TapHoldButton.PadHoldSeconds);
    private readonly TapHoldButton _padSplit = new(TapHoldButton.PadHoldSeconds);

    /// <summary>Gets or sets the pilot's switch. A remote aircraft's is its owner's, from the samples.</summary>
    public bool On { get; set; }

    /// <summary>Gets the built light, null until the beam first shows.</summary>
    public SpotLight3D? Light { get; private set; }

    /// <summary>Gets a value indicating whether the beam draws this frame.</summary>
    public bool Lit => Light is { Visible: true };

    /// <summary>One frame of the view cycle and the switch over a seat's two device halves, as
    /// levels. A control held by one of the two acts for it alone. A control held by both is split
    /// by hold length: a tap cycles the view on release, a hold past
    /// <see cref="TapHoldButton.PadHoldSeconds"/> switches the headlight. Answers each half's cycle
    /// level, for the view keys' own edges, and the switch's level.</summary>
    public (bool CycleKey, bool CyclePad, bool Switch) Read(
        bool keyCycle, bool keyLight, bool padCycle, bool padLight, float dt)
    {
        var (cycleKey, switchKey) = Split(_keySplit, keyCycle, keyLight, dt);
        var (cyclePad, switchPad) = Split(_padSplit, padCycle, padLight, dt);
        return (cycleKey, cyclePad, switchKey || switchPad);
    }

    /// <summary>Draws the beam on <paramref name="host"/> this frame, or hides it. The first shown
    /// frame builds the light ahead of the model's front. The shadow follows Shadow Quality every
    /// frame, so an Options apply reaches a lit beam.</summary>
    public void Show(Node3D host, Node3D? model, bool show)
    {
        if (!show)
        {
            if (Light != null)
                Light.Visible = false;
            return;
        }

        Light ??= Build(host, model);
        Light.Visible = true;
        Light.ShadowEnabled = ShadowQualitySetting.Sun.Cast;
    }

    private static (bool Cycle, bool Switch) Split(TapHoldButton split, bool cycle, bool light, float dt)
    {
        var press = split.Step(cycle && light, dt);
        return ((cycle && !light) || press == TapHold.Tap, (light && !cycle) || press == TapHold.Hold);
    }

    private static SpotLight3D Build(Node3D host, Node3D? model)
    {
        var light = new SpotLight3D
        {
            Name = "headlight",
            Position = new Vector3(0f, 0f, NoseZ(host, model)),
            SpotRange = Range,
            SpotAngle = ConeDegrees,
            LightEnergy = Energy,
            LightColor = WarmWhite,
        };
        host.AddChild(light);
        Log.Debug("flight", $"headlight built on {host.Name} at z={light.Position.Z:0.00}");
        return light;
    }

    // The front of the drawn hull in the host's frame, forward being -Z, plus the clearance.
    private static float NoseZ(Node3D host, Node3D? model)
    {
        if (model == null || !model.IsInsideTree())
            return -BareNoseDistance;
        var toHost = host.GlobalTransform.AffineInverse();
        float front = float.MaxValue;
        void Walk(Node node)
        {
            if (node is MeshInstance3D { Mesh: not null } mesh && mesh.IsVisibleInTree())
                front = Mathf.Min(front, (toHost * mesh.GlobalTransform * SceneBuilder.GameplayAabb(mesh)).Position.Z);
            int count = node.GetChildCount();
            for (int i = 0; i < count; i++)
                Walk(node.GetChild(i));
        }

        Walk(model);
        return front < float.MaxValue ? front - NoseClearance : -BareNoseDistance;
    }
}

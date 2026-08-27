using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Drives the authored instrument panel inside the pilot's own <c>cockpit1</c> interior: the five
/// needle nodes take an absolute angle each frame, and the two warning lamps take the visibility
/// their condition says. Both read <see cref="GaugeCluster"/>'s already-computed state rather than
/// re-deriving it, so the 3D panel and the screen-space dials never disagree or blink out of step.
/// <see cref="Bind"/> finds the nodes in one built interior; <see cref="Apply"/> is the per-frame
/// write, the same shape <see cref="CockpitVisibility"/> uses.
/// </summary>
public sealed class CockpitGauges
{
    private readonly Needle _altHundreds, _altThousands, _speed, _nitroBoost, _nitroCharge;
    private readonly Needle _gunArrow, _missileArrow;
    private readonly Node3D? _lowAltLamp, _stallLamp, _nitroDial;

    private CockpitGauges(Node3D gauges)
    {
        _altHundreds = Needle.Find(gauges, "hundreds");
        _altThousands = Needle.Find(gauges, "thousands");
        _speed = Needle.Find(gauges, "speed");
        _nitroBoost = Needle.Find(gauges, "nitro_boost");
        _nitroCharge = Needle.Find(gauges, "nitro_charge");
        _gunArrow = Needle.Find(gauges, "ggarrow");
        _missileArrow = Needle.Find(gauges, "mgarrow");
        _lowAltLamp = FindNamed(gauges, "lowalt_on");
        _stallLamp = FindNamed(gauges, "stallwarning_on");
        _nitroDial = FindNamed(gauges, "nitrogauge");
    }

    /// <summary>Finds the panel inside one built interior, or null when there is no interior (an AI
    /// plane, or any build that did not ask <see cref="PlaneBuilder"/> for one) or no
    /// <c>gauges</c> subtree in it.</summary>
    public static CockpitGauges? Bind(Node3D? interior)
    {
        if (interior == null || FindNamed(interior, "gauges") is not { } gauges)
        {
            return null;
        }
        return new CockpitGauges(gauges);
    }

    /// <summary>Write this frame's readings onto the panel. Called only while the interior is on
    /// the screen, so an external view costs nothing; the needles hold their last pose behind the
    /// hidden interior, which is what the pilot sees on returning to the cockpit anyway.</summary>
    public void Apply(GaugeCluster gauges)
    {
        // ⚠ Negated: the exposed altimeter and speedometer angles are clockwise-positive (what the
        // screen-space draw wants), while a node rotation about +Z is counter-clockwise-positive.
        _altHundreds.SetAngleDeg(-GaugeCluster.AltHundredsAngleDeg(gauges.AltitudeFt));
        _altThousands.SetAngleDeg(-GaugeCluster.AltThousandsAngleDeg(gauges.AltitudeFt));
        _speed.SetAngleDeg(-GaugeCluster.SpeedAngleDeg(gauges.SpeedMph));
        // The nitro pair is already the decoded Euler-z, so these take the angle unnegated.
        _nitroBoost.SetAngleDeg(gauges.NitroBoostAngleDeg);
        _nitroCharge.SetAngleDeg(gauges.NitroChargeAngleDeg);
        // The belt arrows sweep clockwise like the dial needles. NaN until a loadout binds one,
        // and an unswept arrow keeps the pose it was authored at.
        SetIfSwept(_gunArrow, gauges.GunArrowAngleDeg);
        SetIfSwept(_missileArrow, gauges.MissileArrowAngleDeg);
        Show(_lowAltLamp, gauges.LowAltLampLit);
        Show(_stallLamp, gauges.StallLampLit);
        // ⚠ Only the Devastator ships nitrogauge active:false, so on every other airframe the dial
        // is authored present and the injector is what decides. The screen-space cluster draws it
        // on the same flag; without this the 3D panel shows a nitro dial on a plane with no nitrous.
        Show(_nitroDial, gauges.NitroInstalled);
    }

    private static void SetIfSwept(Needle needle, float angleDeg)
    {
        if (!float.IsNaN(angleDeg))
        {
            needle.SetAngleDeg(-angleDeg);
        }
    }

    private static void Show(Node3D? node, bool visible)
    {
        if (node != null && node.Visible != visible)
        {
            node.Visible = visible;
        }
    }

    private static Node3D? FindNamed(Node3D root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is not Node3D n3d)
            {
                continue;
            }
            if (AnimRuntime.NameOf(n3d).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return n3d;
            }
            if (FindNamed(n3d, name) is { } hit)
            {
                return hit;
            }
        }
        return null;
    }

    /// <summary>One authored needle and the rest pose it hangs at. The modeled rest ROTATION is
    /// arbitrary and the engine overwrites it outright (docs/formats/hud.md), so only the
    /// translation and scale are kept; an unbound needle is a no-op, which is what the Devastator's
    /// absent nitro dial needs.</summary>
    private readonly struct Needle
    {
        private readonly Node3D? _node;
        private readonly Vector3 _origin;
        private readonly Vector3 _scale;

        private Needle(Node3D node)
        {
            _node = node;
            _origin = node.Transform.Origin;
            _scale = node.Transform.Basis.Scale;
        }

        public static Needle Find(Node3D gauges, string name) =>
            FindNamed(gauges, name) is { } node ? new Needle(node) : default;

        public void SetAngleDeg(float degrees)
        {
            if (_node == null)
            {
                return;
            }
            var basis = new Basis(Vector3.Back, Mathf.DegToRad(degrees)).Scaled(_scale);
            _node.Transform = new Transform3D(basis, _origin);
        }
    }
}

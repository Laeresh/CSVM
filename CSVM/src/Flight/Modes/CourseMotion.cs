using System;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// The idle motion of a course gate, a pure function of the gate and the sim time in seconds. A balloon
/// ring bobs, drifts sideways in its own plane and rolls about its axis, the ring and its balloons as one
/// body. A pylon gate stands still, and its ripple is drawn only. The drawn ring and its scoring polygon
/// both stand at <c>Rest(gate) * Offset(gate, seconds)</c>, so they cannot part. Each gate takes its
/// phases from a hash of its id, so rings do not move in lockstep. The amplitudes are remake-only
/// looks, tuned at the controls.
/// </summary>
public static class CourseMotion
{
    /// <summary>A ring's vertical bob, metres either side of rest.</summary>
    public const float BobM = 1.0f;

    public const float BobPeriodS = 6.0f;

    /// <summary>A ring's sideways drift along its own plane, metres either side of rest.</summary>
    public const float DriftM = 0.6f;

    /// <summary>The drift's period; the roll shares it.</summary>
    public const float SwayPeriodS = 8.5f;

    /// <summary>A ring's roll about its axis, degrees either side, in step with the drift. It turns the
    /// ring in its own plane, so the scoring plane's normal never moves.</summary>
    public const float RollDeg = 2.0f;

    /// <summary>The gate's rest pose: its position turned by its heading. Local -Z is
    /// <see cref="CourseGate.Forward"/> and +Y is up. The plane a gate is flown through is local z = 0.</summary>
    public static Transform3D Rest(CourseGate gate) =>
        new(new Basis(Vector3.Up, Mathf.DegToRad(gate.Heading)), gate.Position);

    /// <summary>The motion at <paramref name="seconds"/> of sim time, in the gate's own frame: a
    /// translation in local x and y and a roll about local z. Identity for a pylon gate.</summary>
    public static Transform3D Offset(CourseGate gate, double seconds)
    {
        if (gate.Kind == CourseGateKind.PylonGate)
        {
            return Transform3D.Identity;
        }

        uint hash = Hash(gate.Id);
        double bob = Math.Sin((Math.Tau * seconds / BobPeriodS) + Turn(hash & 0xFFFF));
        double sway = Math.Sin((Math.Tau * seconds / SwayPeriodS) + Turn(hash >> 16));
        return new Transform3D(
            new Basis(Vector3.Back, Mathf.DegToRad(RollDeg) * (float)sway),
            new Vector3(DriftM * (float)sway, BobM * (float)bob, 0f));
    }

    /// <summary>The gate's world pose at <paramref name="seconds"/>, <c>Rest * Offset</c>.</summary>
    public static Transform3D Pose(CourseGate gate, double seconds) => Rest(gate) * Offset(gate, seconds);

    // FNV-1a over the id's characters, then murmur3's finaliser. string.GetHashCode is seeded per
    // process, which would move every ring between runs. ⚠ Keep the finaliser: without it ids that
    // differ in their last character ("g1", "g2") take phases a few degrees apart.
    private static uint Hash(string id)
    {
        uint h = 2166136261u;
        foreach (char c in id)
        {
            h = (h ^ c) * 16777619u;
        }

        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        return h ^ (h >> 16);
    }

    private static double Turn(uint sixteenBits) => sixteenBits / 65536.0 * Math.Tau;
}

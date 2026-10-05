using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace CSVM.Spec;

/// <summary>What a <c>--xr-sim</c> screenshot saves: both eyes side by side (left eye on the left),
/// or one eye alone.</summary>
public enum XrSimShot
{
    Both,
    Left,
    Right,
}

/// <summary>
/// The stand-in headset a <c>--xr-sim[=spec]</c> launch flies with, parsed from the flag's
/// comma-separated <c>key=value</c> tokens. A yaw and pitch in degrees (+ right, + up) pin the head
/// for the whole run. Its offset from the seated eye is in metres (+x right, +y up, +z back). Each
/// eye is a symmetric frustum of a horizontal field of view, <see cref="IpdMm"/> apart. The default
/// head sits at the mount looking ahead, and two default eyes side by side make a 1280x720 shot, the
/// golden size. Engine-free, so it unit-tests.
/// </summary>
public sealed record XrSimSpec(float YawDeg, float PitchDeg, Vector3 Head, float IpdMm, Vector2I EyePx,
    float FovDeg, XrSimShot Shot)
{
    /// <summary>A bare <c>--xr-sim</c>.</summary>
    public static readonly XrSimSpec Default = new(0f, 0f, Vector3.Zero, 63f, new Vector2I(640, 720), 90f, XrSimShot.Both);

    /// <summary>The pixel size a screenshot of this headset saves.</summary>
    public Vector2I ShotPx => Shot == XrSimShot.Both ? new Vector2I(EyePx.X * 2, EyePx.Y) : EyePx;

    /// <summary>Parses the text after <c>--xr-sim=</c> over <see cref="Default"/>. A token that does
    /// not parse keeps the default and adds a sentence to <paramref name="problems"/>. A typo in a
    /// capture script should not kill the run.</summary>
    public static XrSimSpec Parse(string text, List<string> problems)
    {
        var spec = Default;
        foreach (string raw in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = raw.IndexOf('=');
            string key = eq < 0 ? raw : raw[..eq];
            string value = eq < 0 ? string.Empty : raw[(eq + 1)..];
            XrSimSpec? next = key.ToLowerInvariant() switch
            {
                "yaw" => Float(value, -180f, 180f) is { } yaw ? spec with { YawDeg = yaw } : null,
                "pitch" => Float(value, -90f, 90f) is { } pitch ? spec with { PitchDeg = pitch } : null,
                "head" => Slashed(value) is { } head ? spec with { Head = head } : null,
                "ipd" => Float(value, 0f, 100f) is { } ipd ? spec with { IpdMm = ipd } : null,
                "eye" => Size(value) is { } eye ? spec with { EyePx = eye } : null,
                "fov" => Float(value, 1f, 179f) is { } fov ? spec with { FovDeg = fov } : null,
                "shot" => ShotOf(value) is { } shot ? spec with { Shot = shot } : null,
                _ => null,
            };
            if (next == null)
            {
                problems.Add($"--xr-sim token '{raw}' is not yaw=, pitch=, head=x/y/z, ipd=, eye=WxH, fov= or shot=both|left|right in range, keeping the default");
                continue;
            }

            spec = next;
        }

        return spec;
    }

    private static float? Float(string value, float min, float max) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v >= min && v <= max
            ? v
            : null;

    private static Vector3? Slashed(string value)
    {
        var parts = value.Split('/');
        if (parts.Length != 3)
        {
            return null;
        }

        var xyz = new float[3];
        for (int i = 0; i < 3; i++)
        {
            if (Float(parts[i], -2f, 2f) is not { } v)
            {
                return null;
            }

            xyz[i] = v;
        }

        return new Vector3(xyz[0], xyz[1], xyz[2]);
    }

    private static Vector2I? Size(string value)
    {
        var parts = value.ToLowerInvariant().Split('x');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h)
            && w is >= 16 and <= 8192 && h is >= 16 and <= 8192
            ? new Vector2I(w, h)
            : null;
    }

    private static XrSimShot? ShotOf(string value) => value.ToLowerInvariant() switch
    {
        "both" => XrSimShot.Both,
        "left" => XrSimShot.Left,
        "right" => XrSimShot.Right,
        _ => null,
    };
}

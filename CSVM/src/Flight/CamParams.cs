using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;

namespace CSVM.Flight;

/// <summary>
/// The original's camera tuning for one aircraft, from the zrdr extraction's
/// <c>camparam.json</c>: a <c>default</c> block every plane starts from, with seven of the eleven
/// airframes overriding their own chase distance on top. See
/// <see href="../../../docs/formats/camparam.md">camparam.md</see>.
///
/// <para><b>Only <see cref="Dist"/> currently drives anything</b> (<see cref="CameraController"/>
/// takes it as the chase radius). Everything else is decoded and carried here so the next reader
/// does not have to re-derive it, but is deliberately dormant — the mechanisms behind those keys
/// are not settled:</para>
///
/// <para>⚠ <b>The dynamic-distance law is not decoded.</b> In the <c>default</c> block
/// <see cref="Dist"/> is 13.0 while <see cref="DistMin"/> is 15.7 — the minimum is LARGER than the
/// base — so the rule cannot be "clamp Dist into [DistMin, DistMax]". For all seven per-plane
/// overrides the two are equal instead. <see cref="DistFactor"/> and <see cref="DistVary"/> have no
/// identified input (speed? throttle? load factor?). Do not invent one.</para>
///
/// <para>⚠ <b>The catch-up triplet's units are undecoded.</b>
/// <see cref="PosCatchUp"/>/<see cref="LookCatchUp"/>/<see cref="DistCatchUp"/> read plausibly as
/// the 1/s exponential rates <see cref="CameraController"/> already uses, but could as easily be
/// frame counts or seconds-to-settle. Wiring them on the plausible reading would slow the camera
/// roughly fourfold if the reading is wrong, and nothing would look obviously broken — confirm the
/// shape against a capture first.</para>
/// </summary>
public sealed class CamParams
{
    /// <summary>Chase distance in metres — the one applied field. 13.0 is the shipped default,
    /// which the four airframes with no override of their own take.</summary>
    public float Dist = 13f;

    // The dynamic-distance block. Undecoded — see the type's second ⚠.
    public float DistFactor = 0.01f;
    public float DistVary = 0.1f;
    public float DistMin = 15.7f;
    public float DistMax = 25f;

    // Catch-up rates. Units undecoded — see the type's third ⚠.
    public float DistCatchUp = 1f;
    public float PosCatchUp = 2f;
    public float LookCatchUp = 3f;

    /// <summary>Third-person eye height and pitch. The Balmoral is the only airframe overriding
    /// them (0.2/0.2 against 0.138/0.29). ThirdpPitch in radians is 16.6°, close enough to our
    /// hand-picked chase elevation (15.7°) to be suggestive, but the height's units are unknown —
    /// so the offset DIRECTION stays hand-picked and only the radius comes from the data.</summary>
    public float ThirdpHeight = 0.138f;
    public float ThirdpPitch = 0.29f;

    // The look-behind view's distance range.
    public float BackDistMin = 15.5f;
    public float BackDistMax = 55f;

    // The death camera: a periodic re-frame of the dying aircraft.
    public float DeathInterval = 2f;
    public float DeathZ;
    public float DeathX = 80f;
    public float DeathAlt = 5f;
    public float DeathMinAlt = 15.1f;

    // The crash camera's geometry.
    public float CrashHoriz = 30f;
    public float CrashY = 45f;
    public float CrashChordY = 1000f;
    public float CrashElev = 40f;

    // The flyby camera: a roadside pass that watches the plane go by, then re-sites itself.
    public float FlybyMinWatchTime = 3.8f;
    public float FlybyMaxWatchTime = 4.3f;
    public float FlybyMinRadius = 5.5f;
    public float FlybyMaxRadius = 7f;
    public float FlybyZ;
    public float FlybyY = 0.1f;
    public float FlybyMinAlt = 0.1f;
    public float FlybyMinInterval = 1.9f;
    public float FlybyMaxInterval = 2.3f;
    public float FlybyMinSwitchDist = 70f;
    public float FlybyMaxSwitchDist = 85f;

    /// <summary>The block name this resolved against ("Bloodhawk"), or null when the airframe has
    /// no block of its own and takes <c>default</c> whole.</summary>
    public string? DisplayName;

    /// <summary>False when <c>camparam.json</c> was not present, so every value above is the
    /// hard-coded fallback. A partial extraction still flies; the session says so once.</summary>
    public bool FromData;

    /// <summary>Resolves one airframe's camera block: <c>default</c> first, then the plane's own
    /// keys layered over it.
    ///
    /// <para>⚠ <b>The file is keyed by DISPLAY name</b> ("Bloodhawk", "Firebrand"), not by the
    /// model node or the vehicle def, so the lookup goes through
    /// <see cref="MarkerRig.PlayerAirframes"/>. Do not substitute
    /// <c>PlaneRoster.PlaneDisplayName</c>: it strips a leading <c>p</c> and title-cases, which
    /// yields "Fbrand" for <c>player_fbrand</c> and would silently drop the Firebrand's
    /// override.</para></summary>
    public static CamParams Load(string zrdrPath, string planeNodeName)
    {
        var result = new CamParams();
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(zrdrPath, "camparam.json");
        }
        catch (FileNotFoundException)
        {
            return result; // FromData stays false — the caller reports it once
        }
        catch (DirectoryNotFoundException)
        {
            return result;
        }
        result.FromData = true;

        // The root alternates name, props, name, props. Tolerate the extra wrapping list
        // vehicle.json carries, so one shape check covers both reader layouts.
        if (root.Count > 0 && root[0] is List<object?> wrapped)
        {
            root = wrapped;
        }

        var blocks = new Dictionary<string, ZrdrDict>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < root.Count; i += 2)
        {
            if (root[i] is string name && root[i + 1] is List<object?> props)
            {
                blocks[name] = ZrdrDict.FromAlternating(props);
            }
        }

        foreach (var (model, display) in MarkerRig.PlayerAirframes)
        {
            if (string.Equals(model, planeNodeName, StringComparison.OrdinalIgnoreCase))
            {
                result.DisplayName = blocks.ContainsKey(display) ? display : null;
                break;
            }
        }

        if (blocks.TryGetValue("default", out var fallback))
        {
            result.Apply(fallback);
        }
        if (result.DisplayName != null)
        {
            result.Apply(blocks[result.DisplayName]);
        }
        return result;
    }

    // Overlays whichever keys the block carries, leaving the rest as resolved so far — which is
    // what makes the per-plane blocks (three or five keys each) layer onto default.
    private void Apply(ZrdrDict d)
    {
        Dist = d.Float("dist", Dist);
        DistFactor = d.Float("dist_factor", DistFactor);
        DistVary = d.Float("dist_vary", DistVary);
        DistMin = d.Float("dist_min", DistMin);
        DistMax = d.Float("dist_max", DistMax);
        DistCatchUp = d.Float("dist_catch_up", DistCatchUp);
        PosCatchUp = d.Float("pos_catch_up", PosCatchUp);
        LookCatchUp = d.Float("look_catch_up", LookCatchUp);
        ThirdpHeight = d.Float("thirdp_height", ThirdpHeight);
        ThirdpPitch = d.Float("thirdp_pitch", ThirdpPitch);
        BackDistMin = d.Float("back_dist_min", BackDistMin);
        BackDistMax = d.Float("back_dist_max", BackDistMax);
        DeathInterval = d.Float("death_interval", DeathInterval);
        DeathZ = d.Float("death_z", DeathZ);
        DeathX = d.Float("death_x", DeathX);
        DeathAlt = d.Float("death_alt", DeathAlt);
        DeathMinAlt = d.Float("death_min_alt", DeathMinAlt);
        CrashHoriz = d.Float("crash_horiz", CrashHoriz);
        CrashY = d.Float("crash_y", CrashY);
        CrashChordY = d.Float("crash_chord_y", CrashChordY);
        CrashElev = d.Float("crash_elev", CrashElev);
        FlybyMinWatchTime = d.Float("flyby_min_watch_time", FlybyMinWatchTime);
        FlybyMaxWatchTime = d.Float("flyby_max_watch_time", FlybyMaxWatchTime);
        FlybyMinRadius = d.Float("flyby_min_radius", FlybyMinRadius);
        FlybyMaxRadius = d.Float("flyby_max_radius", FlybyMaxRadius);
        FlybyZ = d.Float("flyby_z", FlybyZ);
        FlybyY = d.Float("flyby_y", FlybyY);
        FlybyMinAlt = d.Float("flyby_min_alt", FlybyMinAlt);
        FlybyMinInterval = d.Float("flyby_min_interval", FlybyMinInterval);
        FlybyMaxInterval = d.Float("flyby_max_interval", FlybyMaxInterval);
        FlybyMinSwitchDist = d.Float("flyby_min_switch_dist", FlybyMinSwitchDist);
        FlybyMaxSwitchDist = d.Float("flyby_max_switch_dist", FlybyMaxSwitchDist);
    }
}

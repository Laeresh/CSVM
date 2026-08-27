using System;
using Godot;

namespace CSVM.Session;

/// <summary>One airframe a mission can put the player into mid-flight: the authored
/// <c>CALLBACK</c> value, the vehicle def the stats and the stock fit come from (<c>pbalmoral</c>),
/// and the planes.zbd node the model is built from (<c>player_balmoral</c>). That pair is the one
/// every other player-airframe path already uses (<c>PlaneStats.DefName</c> /
/// <c>UI.PlanePickerRoster.AirframeNode</c>), so a swap needs no table of its own.</summary>
public readonly record struct AirframeSwapCode(int Code, string Def, string PlaneNode);

/// <summary>One swap as the mission-script host raises it: the airframe the code names, and the
/// node the raising definition is rooted on, which is the aircraft the capture animation belongs
/// to. <c>CaptureRoot</c> is null for a definition rooted on nothing the runtime could name.
/// </summary>
public readonly record struct AirframeSwapOrder(AirframeSwapCode Airframe, string? CaptureRoot);

/// <summary>What one raised swap did: whether an aircraft was replaced at all, and the aircraft the
/// swap took out of the world (967's capture, null for the other two codes and for a root that
/// resolved to nothing). ⚠ The caller must hand <c>Hidden</c> back to whatever else is holding that
/// aircraft out of play, or a cutscene's own reveal puts it back in front of the player.</summary>
public readonly record struct AirframeSwapResult(bool Swapped, Flight.FlightController? Hidden);

/// <summary>What one mid-mission airframe swap replaces on a player's rig: the planes.zbd node to
/// build, and the flight state the replacement starts in, lifted off the aircraft being left.
/// ⚠ Three things a swap deliberately does NOT rebuild. A custom plane, whose bought armour and
/// pylon counts belong to the airframe the pilot bought rather than to the one a mission handed
/// them. A stunt run, which belongs to the pilot. And the spawn list, since the aircraft being left
/// is where the replacement begins. <c>Scheme</c> is null outside a 967 whose capture root resolved,
/// and also null INSIDE one when the captured rig's own <c>ShippedSkins</c> resolution painted
/// nothing: <c>ShippedSkins</c> carries that reading so the rebuild draws the captured rig's own
/// result rather than falling back to this pilot's default livery.</summary>
internal sealed record AirframeSwapRequest(
    string PlaneNode, FlightStart Start, Mech3.PaintScheme? Scheme = null, bool ShippedSkins = false);

/// <summary>
/// The three <c>CALLBACK</c> codes that hand the player a different airframe in mid mission, and
/// what each names. Decode: docs/formats/anim-definitions/cutscenes.md's callback table, from the
/// mission-script host's own switch. They are the data-side counterpart of the intro definitions'
/// <c>check_balmoral</c>/<c>check_warhawk</c> branches, and the only mechanism in the shipped data
/// that changes what the player is flying without ending the mission.
///
/// <para>⚠ A swap is to the player's OWN airframe rig, never to one of the mission's aircraft. The
/// captured plane is a roster-spawned AI to the last frame; what the code does is rebuild the
/// player's aircraft from the named record, which is why the Balmoral being a heavy plane rather
/// than an airship is the whole of what the runtime has to know about it.</para>
/// </summary>
public static class AirframeSwapCodes
{
    private static readonly AirframeSwapCode[] All =
    {
        new(965, "pbloodhawk", "player_bhawk"),
        new(966, "pwarhawk", "player_warhawk"),
        new(967, "pbalmoral", "player_balmoral"),
    };

    /// <summary>Every swap code, in ascending code order.</summary>
    public static ReadOnlySpan<AirframeSwapCode> Table => All;

    /// <summary>The airframe an authored code names, or null for a code that names none.</summary>
    public static AirframeSwapCode? For(int code)
    {
        foreach (var entry in All)
        {
            if (entry.Code == code)
            {
                return entry;
            }
        }

        return null;
    }
}

/// <summary>
/// The two things codes 966 and 967 do past rebuilding the player's rig: the capture animation's
/// own aircraft hands its damage to the new hull (967 alone), and the aeroplane the player just
/// left goes to <c>wingman_4</c>.
/// ⚠ Neither behaviour is asked for by anything in the shipped data. Both are keyed on chapter
/// and mission strings inside the executable, so a search of the data for a trigger comes back
/// empty and that emptiness is NOT evidence they do not exist. Decode:
/// docs/formats/anim-definitions/cutscenes.md, "The airframe swap codes 965, 966 and 967".
/// </summary>
public static class AirframeHandover
{
    /// <summary>The roster block the outgoing aeroplane goes to. The original resolves this name
    /// in the live vehicle list once at mission start and holds nothing everywhere else.</summary>
    public const string WingmanName = "wingman_4";

    /// <summary>How far off the player the outgoing aeroplane is placed, metres.</summary>
    public const float RangeM = 100f;

    /// <summary>What the placement bearing takes off the player's own heading, degrees. Negative
    /// puts the aeroplane forward and to starboard; its nose still takes the player's heading, so
    /// the two are flying alongside rather than converging.</summary>
    public const float BearingDeg = -45f;

    // The capture case. 966 measures the outgoing sums and hands them over exactly as 967 does,
    // but scales nothing and hides nothing: it has no capture animation to read.
    private const int CaptureCode = 967;

    // The chapter/mission pairs the original resolves the name in, lower case as the exe holds
    // them. Nothing in the data says these two are special.
    private static readonly (string Chapter, string Mission)[] Missions =
    {
        ("c3", "m05"),
        ("c4", "m04"),
    };

    /// <summary>Does <see cref="WingmanName"/> resolve in this mission? Everywhere else the
    /// original holds no record of that name and the hand-over does nothing.</summary>
    public static bool Resolves(string? chapter, string? mission)
    {
        foreach (var (ch, ms) in Missions)
        {
            if (ch.Equals(chapter, StringComparison.OrdinalIgnoreCase)
                && ms.Equals(mission, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether this code carries the captured aircraft's damage into the new hull and
    /// hides that aircraft.</summary>
    public static bool CarriesCapturedDamage(AirframeSwapCode airframe) =>
        airframe.Code == CaptureCode;

    /// <summary>Where the outgoing aeroplane is put, and the point its nose is put on: 100 m along
    /// a bearing <see cref="BearingDeg"/> off the player's heading, facing the player's heading.
    /// A vertical or zero nose falls back to the world's own forward axis rather than throwing.
    /// </summary>
    public static (Vector3 Position, Vector3 LookAt) Placement(Vector3 playerPos, Vector3 playerNose)
    {
        var flat = new Vector3(playerNose.X, 0f, playerNose.Z);
        flat = flat.LengthSquared() > 1e-6f ? flat.Normalized() : Vector3.Forward;
        var bearing = new Basis(Vector3.Up, Mathf.DegToRad(BearingDeg)) * flat;
        var position = playerPos + (bearing * RangeM);
        return (position, position + flat);
    }
}

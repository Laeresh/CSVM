using System;

namespace CSVM.Session;

/// <summary>One airframe a mission can put the player into mid-flight: the authored
/// <c>CALLBACK</c> value, the vehicle def the stats and the stock fit come from (<c>pbalmoral</c>),
/// and the planes.zbd node the model is built from (<c>player_balmoral</c>). That pair is the one
/// every other player-airframe path already uses (<c>PlaneStats.DefName</c> /
/// <c>UI.PlanePickerRoster.AirframeNode</c>), so a swap needs no table of its own.</summary>
public readonly record struct AirframeSwapCode(int Code, string Def, string PlaneNode);

/// <summary>What one mid-mission airframe swap replaces on a player's rig: the planes.zbd node to
/// build, and the flight state the replacement starts in, lifted off the aircraft being left.
/// ⚠ Three things a swap deliberately does NOT rebuild. A custom plane, whose bought armour and
/// pylon counts belong to the airframe the pilot bought rather than to the one a mission handed
/// them. A stunt run, which belongs to the pilot. And the spawn list, since the aircraft being left
/// is where the replacement begins.</summary>
internal sealed record AirframeSwapRequest(string PlaneNode, FlightStart Start);

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

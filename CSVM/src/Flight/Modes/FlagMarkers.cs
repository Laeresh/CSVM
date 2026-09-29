using System;
using System.Globalization;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>The three target markers <c>FUN_00495d40</c> relabels per flag, each an entry the
/// mission's own <c>targets.zrd</c> already lists.</summary>
public enum FlagMarker
{
    /// <summary><c>ctf_n</c>, the team's base: "Your Base" or "Enemy Base", always on.</summary>
    Base,

    /// <summary><c>cs_flag_n</c>, the flag standing at its base, on while the flag is home.</summary>
    AtBase,

    /// <summary><c>cs_flg_lightn</c>, the flag carried or floating, on while it is away.</summary>
    Away,
}

/// <summary>
/// Capture the Flag's target labels per flag and per reading side. They cover the base, the flag
/// at its base, the flag away, and the carrier's name tag. Every line reads "Your" to the flag's own
/// team and "Enemy" to the other, by <see cref="AimAssist.Friendly"/>. The name line of all three
/// markers is the flag's team name. Decode: docs/org/multiplayer-ctf.md "Markers". ⚠ Keep it free
/// of any engine dependency beyond the vector struct, as <see cref="FlagMatch"/>.
/// </summary>
public static class FlagMarkers
{
    /// <summary>"%1  Holds %2 flag", row 198, the carrier's tag (<c>0x49a930</c>).</summary>
    public const string HoldsFlagKey = "MSG_HOLDS_FLAG";

    /// <summary>"Your", row 196, the flag's own side (<c>0x49a8dd</c>).</summary>
    public const string YourKey = "MSG_X_YOUR";

    /// <summary>"Enemy", row 197, the other side (<c>0x49a8e4</c>).</summary>
    public const string EnemyKey = "MSG_X_ENEMY";

    /// <summary>"%1 Flag Captured by %2", row 199, the away marker while held (<c>0x49a9e6</c>).
    /// </summary>
    public const string CapturedByKey = "MSG_FLAG_CAP_BY";

    /// <summary>The marker a <c>targets.zrd</c> key stands for, with its flag's team number: the
    /// <c>ctf_n</c>, <c>cs_flag_n</c> and <c>cs_flg_lightn</c> names <c>FUN_00495d40</c> formats.
    /// Null for any other key.</summary>
    public static (FlagMarker Marker, int Team)? MarkerOf(string key)
    {
        return Numbered(key, "cs_flg_light") is int away ? (FlagMarker.Away, away)
            : Numbered(key, "cs_flag_") is int atBase ? (FlagMarker.AtBase, atBase)
            : Numbered(key, "ctf_") is int team ? (FlagMarker.Base, team)
            : null;
    }

    /// <summary>One marker's labels for a flag in <paramref name="row"/>'s state. The flag's side is
    /// <paramref name="sideTeam"/>, the hostility id of its lobby team. The at-base marker is on
    /// while the flag is home (<c>0x49a5ee</c>..<c>0x49a60a</c>). The away marker is on while it is
    /// not (<c>0x49aaad</c>, <c>0x49ad65</c>). A held flag's away marker names its holder,
    /// <paramref name="holder"/>.</summary>
    public static SiteSide Side(FlagMarker marker, FlagRow row, int sideTeam, string teamName,
        string holder, Messages? strings, Vector3? at = null)
    {
        return marker switch
        {
            FlagMarker.Base => new SiteSide(sideTeam, teamName,
                Text(strings, "MSG_MP_YOUR_BASE"), Text(strings, "MSG_MP_ENEMY_BASE")),
            FlagMarker.AtBase => new SiteSide(sideTeam, teamName,
                Text(strings, "MSG_MP_YOUR_FLAG_BASE"), Text(strings, "MSG_MP_ENEMY_FLAG_BASE"),
                Shown: row.State == FlagState.Home),
            _ when row.State == FlagState.Held => new SiteSide(sideTeam, teamName,
                Messages.Fill(Text(strings, CapturedByKey), Text(strings, YourKey), holder),
                Messages.Fill(Text(strings, CapturedByKey), Text(strings, EnemyKey), holder), At: at),
            _ when row.State == FlagState.Floating => new SiteSide(sideTeam, teamName,
                Text(strings, "MSG_YOUR_FLAG_FLOAT"), Text(strings, "MSG_ENEMY_FLAG_FLOAT"), At: at),
            _ => new SiteSide(sideTeam, teamName,
                Text(strings, "MSG_MP_YOUR_FLAG_CAP"), Text(strings, "MSG_MP_ENEMY_FLAG_CAP"),
                Shown: false, At: at),
        };
    }

    /// <summary>The carrier's name line as a pane on <paramref name="ownTeam"/> reads it. It is the
    /// holder's name, then "Holds Your flag" on the flag's side and "Holds Enemy flag" otherwise.
    /// </summary>
    public static string HolderTag(Messages? strings, string holder, int ownTeam, int sideTeam) =>
        Messages.Fill(Text(strings, HoldsFlagKey), holder,
            Text(strings, AimAssist.Friendly(ownTeam, sideTeam) ? YourKey : EnemyKey));

    private static string Text(Messages? strings, string key) => strings?.Get(key) ?? key;

    // The number after a prefix, or null when the rest is not a whole positive number.
    private static int? Numbered(string key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
            out int n) && n > 0 ? n : null;
    }
}

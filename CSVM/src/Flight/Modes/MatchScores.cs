using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;

namespace CSVM.Flight.Modes;

/// <summary>
/// What every scoring event of a network match is worth, engine-free: the nine <c>score_*</c> keys
/// <c>FUN_004735b0</c> reads from <c>player.zrd</c>. A key the file does not author keeps the
/// executable's fallback. Every mode scores with one set, read at session build. The shipped file
/// authors kill, suicide, lost hull and both flag events, so those differ from
/// <see cref="Fallback"/>. Each member is named for its event, and <see cref="Read"/> pairs it
/// with its key. Decode: docs/org/multiplayer-scoring.md.
/// </summary>
public sealed record MatchScores(
    int Kill, int Suicide, int TurretKill, int HullLoss, int ZeppelinKill,
    int GasbagKill, int OwnGasbagKill, int FlagReturn, int FlagCapture)
{
    /// <summary>The executable's own values, stored when a key is missing (<c>0x473fd0</c> to
    /// <c>0x47411b</c>). A match built without the file scores these.</summary>
    public static MatchScores Fallback { get; } = new(
        Kill: 1, Suicide: -1, TurretKill: 1, HullLoss: 100, ZeppelinKill: 1,
        GasbagKill: 10, OwnGasbagKill: -10, FlagReturn: 1, FlagCapture: 5);

    /// <summary>The values <paramref name="player"/>, a parsed <c>player.zrd</c>, authors, each
    /// missing key keeping its <see cref="Fallback"/>.</summary>
    public static MatchScores Read(ZrdrDict player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var fb = Fallback;
        int Key(string key, int fallback) =>
            player.TryFloat(key, out float value) ? (int)MathF.Round(value) : fallback;
        return new MatchScores(
            Key("score_kill", fb.Kill), Key("score_suicide", fb.Suicide),
            Key("score_turret_kill", fb.TurretKill), Key("score_zep", fb.HullLoss),
            Key("score_zep_kill", fb.ZeppelinKill), Key("score_gas_kill", fb.GasbagKill),
            Key("score_my_gas_kill", fb.OwnGasbagKill), Key("score_return_flag", fb.FlagReturn),
            Key("score_enemy_flag", fb.FlagCapture));
    }

    /// <summary>The values of the <c>player.zrd</c> in <paramref name="zrdrPath"/>, else
    /// <see cref="Fallback"/> with the reason handed to <paramref name="warn"/>.</summary>
    public static MatchScores Load(string zrdrPath, Action<string>? warn = null)
    {
        try
        {
            if (Zrdr.LoadFile(zrdrPath, "player.json") is [List<object?> list, ..])
            {
                return Read(ZrdrDict.FromAlternating(list));
            }

            warn?.Invoke("player.zrd holds no root list");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            warn?.Invoke(e.Message);
        }

        return Fallback;
    }
}

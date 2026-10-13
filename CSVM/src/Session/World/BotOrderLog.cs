using System.Collections.Generic;

namespace CSVM.Session.World;

/// <summary>Which bot order changes a match logs: one line each time a bot seat's reason changes,
/// at most <see cref="LinesPerSeat"/> per seat. A long match then does not log every run or torpedo
/// cycle. The Zeppelin vs Zeppelin, Capture the Flag and stunt race bot steps share it. Only a host
/// steps a bot. Each step loops over the bot seats it flies and keeps its own <c>BotObjective</c>
/// switch. Each logs one "bot seat {seat} {why}" line through <see cref="Admit"/>.</summary>
internal sealed class BotOrderLog
{
    /// <summary>Order changes logged per bot seat.</summary>
    public const int LinesPerSeat = 8;

    /// <summary>The reason a bot seat steps on while its runtime's <c>BotObjective</c> is off.</summary>
    public const string ObjectiveOff = "has its objective off";

    private readonly Dictionary<int, (string Why, int Logged)> _last = new();

    /// <summary>Notes <paramref name="seat"/>'s reason this step. True when it changed and the seat
    /// still has a line left, which the caller then logs.</summary>
    public bool Admit(int seat, string why)
    {
        var (was, logged) = _last.TryGetValue(seat, out var last) ? last : ("", 0);
        bool admit = why != was && logged < LinesPerSeat;
        _last[seat] = (why, admit ? logged + 1 : logged);
        return admit;
    }
}

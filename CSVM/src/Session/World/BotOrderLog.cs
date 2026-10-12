using System.Collections.Generic;

namespace CSVM.Session.World;

/// <summary>Which bot order changes a match logs: one line each time a bot seat's reason changes,
/// at most <see cref="LinesPerSeat"/> per seat. A long match then does not log every run or torpedo
/// cycle. The Zeppelin vs Zeppelin and Capture the Flag host steps share it.</summary>
internal sealed class BotOrderLog
{
    /// <summary>Order changes logged per bot seat.</summary>
    public const int LinesPerSeat = 8;

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

using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>
/// The soak's three shaped link cells, each the same in both directions. Latency, jitter and loss
/// step up together from a good broadband link to a poor wireless one. The loopback suites fly
/// their matrices through these. The shaping flag takes them by <see cref="Named"/>, so a real
/// link flown under a cell meets the bar its soak twin set.
/// </summary>
public static class SoakCells
{
    /// <summary>50 ms with 10 ms of jitter and 5 per cent loss.</summary>
    public static readonly LoopbackConditions Broadband = new(0.05, 0.01, 0.05);

    /// <summary>100 ms with 20 ms of jitter and 10 per cent loss.</summary>
    public static readonly LoopbackConditions Congested = new(0.10, 0.02, 0.10);

    /// <summary>200 ms with 40 ms of jitter and 20 per cent loss.</summary>
    public static readonly LoopbackConditions PoorWireless = new(0.20, 0.04, 0.20);

    /// <summary>Each cell under the name a command line gives it.</summary>
    public static readonly IReadOnlyList<(string Name, LoopbackConditions Conditions)> Named = new[]
    {
        ("soak50", Broadband),
        ("soak100", Congested),
        ("soak200", PoorWireless),
    };
}

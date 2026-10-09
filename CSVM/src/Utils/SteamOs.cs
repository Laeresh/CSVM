using System;

namespace CSVM.Utils;

/// <summary>
/// Whether this run is on a SteamOS device in Game Mode, the gamescope session Steam starts. Game
/// Mode draws Steam's on-screen keyboard over the game, and shows no window but the game's, so no
/// file browser either. Desktop Mode is an ordinary Linux desktop and is not Game Mode.
/// </summary>
public static class SteamOs
{
    /// <summary>Whether this run is in Game Mode, read once from the environment Steam launched it
    /// with (<see cref="Detect"/>). A suite or a test sets it to take either branch.</summary>
    public static bool InGameMode { get; set; } = Detect(Environment.GetEnvironmentVariable);

    /// <summary>Whether <paramref name="env"/> names a SteamOS device in Game Mode: <c>SteamDeck</c>
    /// or <c>SteamOS</c> set to 1, under <c>XDG_CURRENT_DESKTOP=gamescope</c>.</summary>
    public static bool Detect(Func<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);
        bool steamOs = env("SteamDeck") == "1" || env("SteamOS") == "1";
        return steamOs && string.Equals(env("XDG_CURRENT_DESKTOP"), "gamescope", StringComparison.OrdinalIgnoreCase);
    }
}

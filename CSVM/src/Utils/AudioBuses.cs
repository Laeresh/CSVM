namespace CSVM.Utils;

/// <summary>
/// The names of the four buses <c>CSVM/default_bus_layout.tres</c> ships, so no player-construction
/// site spells a bus string. Godot resolves an unknown bus name to Master without an error, so a
/// typo at a site would be silent; one constant per bus is what makes that unrepresentable.
/// </summary>
public static class AudioBuses
{
    /// <summary>Bus 0, the output every other bus sends into. The developer <c>--volume=</c> gain
    /// and the focus mute both write here, and neither is part of a player's mix.</summary>
    public const string Master = "Master";

    /// <summary>The score channel: <c>MusicPlayer</c>'s one player and nothing else.</summary>
    public const string Music = "Music";

    /// <summary>Everything the world and the aircraft make: engines, guns, explosions, ambient
    /// emitters, and the menu's own cues.</summary>
    public const string Effects = "Effects";

    /// <summary>Spoken lines: the mission radio, the briefing narration, combat callouts, and the
    /// cinemas' own soundtracks, which are narrated films rather than score or world sound.</summary>
    public const string Voice = "Voice";
}

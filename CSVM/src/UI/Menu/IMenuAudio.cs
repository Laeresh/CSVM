namespace CSVM.UI.Menu;

/// <summary>
/// The shared menu audio service. Presentations request cues and narration at moments they own;
/// the service owns resolution, playback, volume and the handoff into a launching session, so no
/// presentation touches a stream, a bus or a player. Contract only: the host supplies the
/// implementation over the existing playback code, which stays where it is until B12.
/// </summary>
public interface IMenuAudio
{
    /// <summary>Plays one cue. Unresolvable names are the service's to swallow or log; a
    /// presentation never learns whether a sound actually exists.</summary>
    void Cue(MenuCue cue);

    /// <summary>Starts spoken narration from the named WAV, replacing any already playing. The
    /// service ducks and restores whatever music it runs; the caller only names the file.</summary>
    void BeginNarration(string wavName);

    /// <summary>Stops narration if any is playing.</summary>
    void EndNarration();
}

/// <summary>A semantic audio cue a presentation asks for by name. Which sound the name resolves
/// to, and whether it resolves at all, is the audio service's lookup; which cue to ask for and
/// when are the requesting presentation's choices.</summary>
public readonly record struct MenuCue(string Name);

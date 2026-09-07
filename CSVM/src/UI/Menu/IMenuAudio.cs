namespace CSVM.UI.Menu;

/// <summary>Which of a mix page's levels a frame moved. The members are that page's rows, not
/// buses: which bus a level reaches, and whether moving one is audible at all, is the audio
/// service's business and never a presentation's.</summary>
public enum MenuMixLevel
{
    /// <summary>No level moved this frame.</summary>
    None,

    /// <summary>The multiplier over the other three, so moving it moves every category at once.
    /// </summary>
    Master,

    /// <summary>The score's level.</summary>
    Music,

    /// <summary>The level everything the world and the aircraft make stands at.</summary>
    Effects,

    /// <summary>The level spoken lines stand at.</summary>
    Voice,
}

/// <summary>
/// The shared menu audio service. Presentations request cues and narration at moments they own,
/// and state the mix a page that sets one stands at; the service owns resolution, playback, volume,
/// the buses and the handoff into a launching session, so no presentation touches a stream, a bus
/// or a player. Contract only: the host supplies the implementation over the existing playback
/// code, which stays where it is until B12.
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

    /// <summary>The mix a page that sets one wants heard while it is open, so a player judges a
    /// level by ear as it moves rather than by where the thumb sits. <paramref name="moved"/> names
    /// the level this frame moved, <see cref="MenuMixLevel.None"/> when none did, which is what lets
    /// the service make that category audible without the page knowing how. Called every frame such
    /// a page is open; whether a preview sounds or applies at all is the service's, and a run that
    /// drives itself has none.</summary>
    void PreviewMix(Utils.AudioLevels levels, MenuMixLevel moved);

    /// <summary>Puts back the mix that stood before the first <see cref="PreviewMix"/> and stops
    /// whatever the preview was sounding. Idempotent, so every door out of such a page and every
    /// hide can call it; a preview is a menu affordance and must not survive the page that ran
    /// it.</summary>
    void EndMixPreview();
}

/// <summary>A semantic audio cue a presentation asks for by name. Which sound the name resolves
/// to, and whether it resolves at all, is the audio service's lookup; which cue to ask for and
/// when are the requesting presentation's choices.</summary>
public readonly record struct MenuCue(string Name);

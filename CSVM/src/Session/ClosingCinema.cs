using System;
using CSVM.UI;

namespace CSVM.Session;

/// <summary>
/// Whether the closing cinema plays before the scrapbook a finished mission opens, and the one
/// handoff to that scrapbook. The gate is the profile's own campaign position, through
/// <see cref="CampaignProgression.Complete"/>, rather than the original's <c>callback 3104</c>,
/// which nothing here decodes; an unfinished campaign reaches the book with no film, which is what
/// <c>FINALCINEMA.SCRIPT</c>'s own false branch does (docs/formats/cinemas.md). Playing is a
/// delegate the caller supplies, which keeps the whole decision testable with no engine present and
/// leaves the film itself to <c>Launcher.PlayCinema</c>.
/// </summary>
public sealed class ClosingCinema
{
    /// <summary>The film, spelled as the <c>FinalCinema</c> screen's own <c>CF_MOVIE</c> layout row
    /// spells it. That row is the only place the name is written down, because
    /// <c>FINALCINEMA.SCRIPT</c> sets no art path (docs/formats/cinemas.md). It is transcribed here
    /// rather than read back, since the row lives in Original's decoded layout and this film plays
    /// under both presentations; the lookup is case-blind either way.</summary>
    public const string Name = "Final.MPG";

    private readonly CinemaPlay _play;

    private bool _played;

    /// <summary>Builds a closing cinema over the call that puts one on screen.</summary>
    public ClosingCinema(CinemaPlay play) => _play = play;

    /// <summary>Whether the film has been handed to <see cref="CinemaPlay"/>. It is what keeps a
    /// second finished mission, or a second visit to the book, from replaying it.</summary>
    public bool Played => _played;

    /// <summary>Opens the scrapbook for a profile, playing the closing film first when that profile
    /// has finished the campaign and this instance has not played it yet. Answers whether a cinema
    /// was played, so the caller can tell a handoff that already happened from one still to
    /// come.</summary>
    public bool OpenScrapbook(CampaignProfileDef profile, Action showScrapbook) =>
        OpenScrapbook(CampaignProgression.Complete(profile), showScrapbook);

    /// <summary>The same, over the completion answer directly.</summary>
    public bool OpenScrapbook(bool campaignComplete, Action showScrapbook)
    {
        if (!campaignComplete || _played)
        {
            showScrapbook();
            return false;
        }

        _played = true;

        // ⚠ Do not unify these presses with the chapter cinema's. Space and Return skip a chapter
        // cinema and do nothing here, which the two scripts author separately.
        _play(Name, CinemaHandoff.Once(showScrapbook), CinemaScreen.ClosingKeys);
        return true;
    }
}

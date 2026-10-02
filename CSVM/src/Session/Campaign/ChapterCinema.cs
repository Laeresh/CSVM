using System;
using CSVM.Mech3;
using CSVM.Video;

namespace CSVM.Session.Campaign;

/// <summary>
/// Which cinema plays before a campaign chapter, and the one handoff to the passenger cabin that
/// follows it. The chapter is the profile's own story position and nothing else, so a chapter
/// number is never passed in from a screen; the mapping from chapter to film is the identity
/// <c>CAMPAIGNINTRO.SCRIPT</c> builds, chapter N playing <c>chapN.mpg</c>
/// (docs/formats/cinemas.md). Playing is a delegate the caller supplies, which keeps the whole
/// decision testable with no engine present and leaves the film itself to
/// <c>Launcher.PlayCinema</c>.
/// </summary>
public sealed class ChapterCinema
{
    /// <summary>How many story positions one chapter spans. The five chapters open at <c>seq</c> 0,
    /// 5, 10, 15 and 20, which <c>SCRAPBOOK.CSV</c>'s own chapter keys confirm independently of the
    /// sequence reader; the last chapter is four missions long and the division still lands on it
    /// (docs/formats/campaign-sequence.md).</summary>
    public const int MissionsPerChapter = 5;

    /// <summary>Escape, Space, Return or a left mouse press, the chapter cinema's set, and a pad
    /// button with them. ⚠ Do not unify it with <see cref="ClosingCinema.Keys"/>; the two scripts
    /// author their presses separately.</summary>
    public const CinemaSkip Keys = CinemaSkip.Escape | CinemaSkip.Space | CinemaSkip.Return
        | CinemaSkip.LeftMouse | CinemaSkip.PadButton;

    private readonly CinemaPlay _play;
    private readonly Action? _stop;

    private int _played;

    /// <summary>Builds a chapter cinema over the call that puts one on screen and, where given,
    /// <paramref name="stop"/>, the call that ends the one showing.</summary>
    public ChapterCinema(CinemaPlay play, Action? stop = null)
    {
        _play = play;
        _stop = stop;
    }

    /// <summary>The chapter last handed to <see cref="CinemaPlay"/>, or 0 when none has been. It
    /// is what keeps a second visit to the same cabin from replaying the film.</summary>
    public int ChapterPlayed => _played;

    /// <summary>The story chapter, 1 to 5, a campaign position belongs to, or 0 for a position the
    /// sequence does not hold. ⚠ Not <see cref="CampaignSequence.Chapter"/>, which answers the
    /// world folder 1 to 8: chapter 2 spans three of those folders.</summary>
    public static int ChapterOf(int seq) =>
        seq < 0 || seq >= CampaignSequence.MissionCount ? 0 : (seq / MissionsPerChapter) + 1;

    /// <summary>Whether a position is the first mission of its chapter, which is where a chapter
    /// cinema belongs and the only place one plays.</summary>
    public static bool OpensChapter(int seq) => ChapterOf(seq) > 0 && seq % MissionsPerChapter == 0;

    /// <summary>The film a chapter plays, without its extension.</summary>
    public static string NameOf(int chapter) => $"chap{chapter}";

    /// <summary>Opens the passenger cabin for a profile, playing its chapter cinema first when the
    /// profile's next mission opens a chapter this instance has not played yet. Answers whether a
    /// cinema was played, so the caller can tell a handoff that already happened from one that is
    /// still to come.</summary>
    public bool OpenCabin(CampaignProfileDef profile, Action showCabin) =>
        OpenCabin(CampaignProgression.NextMissionSeq(profile), showCabin);

    /// <summary>The same, over a story position directly.</summary>
    public bool OpenCabin(int seq, Action showCabin)
    {
        int chapter = ChapterOf(seq);
        if (!OpensChapter(seq) || chapter == _played)
        {
            showCabin();
            return false;
        }

        _played = chapter;
        _play(NameOf(chapter), CinemaHandoff.Once(showCabin), Keys);
        return true;
    }

    /// <summary>Plays <paramref name="chapter"/>'s film whatever the latch holds, as a co-op guest
    /// does when its host plays one, and runs <paramref name="then"/> when it stops. The latch is
    /// left alone, since the film was the host's. False, with <paramref name="then"/> run at once,
    /// for a chapter the story does not hold.</summary>
    public bool Play(int chapter, Action then)
    {
        ArgumentNullException.ThrowIfNull(then);
        if (chapter < 1 || chapter > ChapterOf(CampaignSequence.MissionCount - 1))
        {
            then();
            return false;
        }

        _play(NameOf(chapter), CinemaHandoff.Once(then), Keys);
        return true;
    }

    /// <summary>Ends the film showing now, as a skip does. Nothing happens without a stop call.
    /// </summary>
    public void Stop() => _stop?.Invoke();
}

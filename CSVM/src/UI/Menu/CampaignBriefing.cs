using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;

namespace CSVM.UI.Menu;

/// <summary>
/// One mission's briefing as the campaign feature holds it: the <c>cm_sequence</c> entry, the
/// dialog state the <c>brief_c&lt;campaign&gt;&lt;mission&gt;</c> formula names, the narration wav
/// its sound resolves to, the objectives note, and the running <see cref="BriefingReveal"/> whose
/// progress is feature state. The clock is the presentation's: it calls <see cref="Advance"/> once
/// a frame while the briefing shows and <see cref="Restart"/> on REPLAY BRIEFING, and it decides
/// when to draw. Every string a presentation labels the screen with comes from the same data root
/// through <see cref="Messages"/>. A broken or absent extraction loads as a briefing with no
/// state, which a presentation shows as its buttons alone rather than refusing to open.
/// </summary>
public sealed class CampaignBriefing
{
    private CampaignBriefing(CampaignMission mission)
    {
        Mission = mission;
    }

    /// <summary>The story entry this briefing is about.</summary>
    public CampaignMission Mission { get; }

    /// <summary>The dialog state driving the screen, or null when the mission has none.</summary>
    public BriefingState? State { get; private set; }

    /// <summary>The reveal, or null when there is no state to run.</summary>
    public BriefingReveal? Reveal { get; private set; }

    /// <summary>The narration wav for the mission, or "" when there is none.</summary>
    public string NarrationWav { get; private set; } = string.Empty;

    /// <summary>The mission's whole objectives note, revealed or not.</summary>
    public IReadOnlyList<BriefingObjective> Objectives { get; private set; } = Array.Empty<BriefingObjective>();

    /// <summary>The <c>MSG_*</c> table the buttons and the note heading are labelled from.</summary>
    public Messages Messages { get; private set; } = new();

    /// <summary>How many times the script has asked for its narration; REPLAY raises it.</summary>
    public int NarrationStarts => Reveal?.NarrationStarts ?? 0;

    /// <summary>Whether every beat has run and every tween has landed; true with no reveal.</summary>
    public bool Complete => Reveal?.Complete ?? true;

    /// <summary>Loads the briefing for story position <paramref name="seq"/> under
    /// <paramref name="dataRoot"/>, or null when the sequence carries no such entry or cannot be
    /// read at all. Everything is read once here, never per repaint.</summary>
    public static CampaignBriefing? Load(string dataRoot, int seq)
    {
        try
        {
            string shared = SessionPaths.PreferUnzipped(Path.Combine(dataRoot, "extracted", "zrdr.zip"));
            CampaignMission? found = null;
            foreach (var mission in CampaignSequence.Load(shared))
            {
                if (mission.Seq == seq)
                {
                    found = mission;
                }
            }

            if (found is not { } entry)
            {
                return null;
            }

            var briefing = new CampaignBriefing(entry);
            briefing.Read(dataRoot, shared);
            return briefing;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Moves the reveal on by a frame's worth of seconds.</summary>
    public void Advance(double seconds) => Reveal?.Advance(seconds);

    /// <summary>REPLAY BRIEFING: the reveal back to a blank map with the narration starting over.</summary>
    public void Restart() => Reveal?.Restart();

    // The state, the note and the narration's cue points, each degrading on its own: a mission with
    // a state but no cue chunk still reveals, just with every marker released at once.
    private void Read(string root, string shared)
    {
        try
        {
            Messages = Messages.Load(Path.Combine(root, "extracted", "messages.json"));
            State = BriefingDialog.Load(shared).Find(Mission.Campaign, Mission.Mission);
            Objectives = BriefingObjectives.Load(
                Zrdr.LoadFile(
                    SessionPaths.MissionZrdr(root, Mission.ChapterFolder, Mission.MissionFolder),
                    "objectives.json"),
                Messages);
            if (State is { } state)
            {
                NarrationWav = SoundDefs.Load(shared).TryGetValue(state.Sound, out var def)
                    ? def.WavName
                    : string.Empty;
                Reveal = new BriefingReveal(state.Steps, Markers(root));
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            // An extraction that is absent or half-written: the briefing degrades, it does not throw.
        }
    }

    // The narration's own cue points, which is where every WaitForMarker time comes from. No file
    // and no cue chunk means no markers, which BriefingReveal degrades on rather than inventing.
    private IReadOnlyList<double> Markers(string root) =>
        NarrationWav.Length == 0
            ? Array.Empty<double>()
            : WavCues.ReadFrom(
                SessionPaths.PreferUnzipped(Path.Combine(root, "extracted", "soundsh.zip")),
                NarrationWav);
}

using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;

namespace CSVM.UI;

/// <summary>
/// The mission briefing (<c>Campaign Briefing.png</c>): the parchment map, the flag pins, the
/// objectives note that fills in beside them, the narration, and REPLAY BRIEFING / RETURN TO CABIN
/// / GO TO FLIGHT CHECK. Everything on it is the mission's own data, resolved from
/// <see cref="CampaignFlow.MissionSeq"/> through the <c>brief_c&lt;campaign&gt;&lt;mission&gt;</c>
/// formula: the map bitmap and the narration name come from the chosen state, never from the
/// mission's story position (the map art is reused, and Hawaii's wav numbering is not play order).
///
/// <para>The reveal itself is <see cref="BriefingReveal"/>, run off a clock the shell advances
/// (<see cref="Advance"/>). The buttons hold rows 0 to 2 so their indices never move under the
/// cursor while the note fills in below them.</para>
/// </summary>
public sealed class CampaignBriefingPage : CampaignPage
{
    /// <summary>REPLAY BRIEFING's row.</summary>
    public const int ReplayRow = 0;

    /// <summary>RETURN TO CABIN's row.</summary>
    public const int CabinRow = 1;

    /// <summary>GO TO FLIGHT CHECK's row.</summary>
    public const int FlightCheckRow = 2;

    private const int ButtonRows = 3;

    // The objectives note's own authored placement, the dialog's OBJECTIVESLIST BACKGROUND
    // (docs/formats/briefing.md). It draws under every reveal element, which is where ToBack, the
    // one opcode ever aimed at it, leaves it.
    private const int ParchmentY = 295;

    private static readonly BoardArt Parchment = new(BoardArtLibrary.Rimage, "parchment");

    private readonly Dictionary<string, HangarArt?> _art = new(StringComparer.OrdinalIgnoreCase);

    // The MissionSeq everything below describes. -2 is "nothing loaded yet", distinct from the
    // flow's own -1 for "the cabin has not named a mission".
    private int _loaded = -2;

    private CampaignMission? _mission;
    private BriefingState? _state;
    private BriefingReveal? _reveal;
    private Messages _messages = new();
    private IReadOnlyList<BriefingObjective> _objectives = Array.Empty<BriefingObjective>();

    /// <summary>Binds the page to its flow.</summary>
    public CampaignBriefingPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Briefing;

    /// <summary>The original's screen carries no title text at all; this names it for the board
    /// chrome, which heads every screen, with the mission's long name (langui <c>3450 + seq</c>).</summary>
    public override string Title
    {
        get
        {
            Sync();
            return _mission is { } mission
                ? Flow.Strings.Text(3450 + mission.Seq, "MISSION BRIEFING")
                : "MISSION BRIEFING";
        }
    }

    /// <summary>The three buttons, then one row per objective the reveal has uncovered.</summary>
    public override int RowCount
    {
        get
        {
            Sync();
            return ButtonRows + (_reveal?.RevealedObjectives.Count ?? 0);
        }
    }

    /// <inheritdoc/>
    public override string Footer =>
        "↑↓  Choose       Enter / A  Select       Esc / B  Back to the cabin";

    /// <summary>The mission's parchment map, the bitmap the state's own
    /// <c>BACKGROUND_IMAGES</c> names.</summary>
    public override HangarArt? Art
    {
        get
        {
            Sync();
            return _state is { } state && state.Background.Length > 0
                ? Picture(state.Background, MapCaption())
                : null;
        }
    }

    /// <summary>The narration wav for the mission showing, or "" when there is none. The shell
    /// plays it; see the wiring contract in the plan's C23 section.</summary>
    public string NarrationWav { get; private set; } = string.Empty;

    /// <summary>How many times the script has asked for its narration. A shell restarts playback
    /// whenever this changes, which is how REPLAY BRIEFING restarts the voice.</summary>
    public int NarrationStarts => _reveal?.NarrationStarts ?? 0;

    /// <summary>The briefing state driving the screen, or null when the mission has none.</summary>
    public BriefingState? State
    {
        get
        {
            Sync();
            return _state;
        }
    }

    /// <summary>The running reveal, or null before a mission is named.</summary>
    public BriefingReveal? Reveal
    {
        get
        {
            Sync();
            return _reveal;
        }
    }

    /// <summary>The mission's whole objectives note, revealed or not.</summary>
    public IReadOnlyList<BriefingObjective> Objectives
    {
        get
        {
            Sync();
            return _objectives;
        }
    }

    /// <summary>The mission's map, the objectives parchment over it, then every element the reveal
    /// has placed: the photographs, the flag pins, the flourishes. Each carries the script's own
    /// position, opacity and rotation, so what is drawn is the authored composition and when it
    /// arrives stays <see cref="BriefingReveal"/>'s business.</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            Sync();
            var pictures = new List<BoardPicture>();
            if (_state is { Background.Length: > 0 } state)
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, state.Background), 0, 0));
            }

            pictures.Add(new BoardPicture(Parchment, 0, ParchmentY));
            if (_reveal is { } reveal)
            {
                AddElements(pictures, reveal, back: true);
                AddElements(pictures, reveal, back: false);
            }

            return pictures;
        }
    }

    /// <summary>The route line a state may draw between two of its pins, at most one per mission.</summary>
    public override IReadOnlyList<BoardStroke> Strokes
    {
        get
        {
            Sync();
            var strokes = new List<BoardStroke>();
            foreach (var element in _reveal?.Elements ?? Array.Empty<BriefingElement>())
            {
                if (element.Visible && element.Opacity > 0f && element.Points.Count >= 2)
                {
                    strokes.Add(new BoardStroke(
                        element.Points[0].X, element.Points[0].Y,
                        element.Points[1].X, element.Points[1].Y,
                        element.Color.R, element.Color.G, element.Color.B, element.Opacity));
                }
            }

            return strokes;
        }
    }

    /// <summary>The parchment's own title widget, at the dialog's authored position.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            Sync();
            return new[] { new BoardLine(NoteHeading(), 35, 315, 185, 17, BoardInk.Heading) };
        }
    }

    /// <summary>Moves the reveal on by a frame's worth of seconds. The shell calls this while the
    /// briefing is the screen showing; nothing else on the page needs a clock.</summary>
    public void Advance(double seconds)
    {
        Sync();
        _reveal?.Advance(seconds);
    }

    /// <summary>The three plaques the dialog's own <c>BUTTONS</c> section carries, in its order.
    /// Every later row is an uncovered note line, which the parchment lists rather than draws as a
    /// control.</summary>
    public override BoardButtonRef Button(int row) => row switch
    {
        ReplayRow => new BoardButtonRef(BoardButton.ReplayBriefing),
        CabinRow => new BoardButtonRef(BoardButton.ReturnToCabin),
        FlightCheckRow => new BoardButtonRef(BoardButton.GoToFlightCheck),
        _ => BoardButtonRef.None,
    };

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        Sync();
        return row switch
        {
            ReplayRow => Label("MSG_BTN_REPLAY_BRIEFING", "REPLAY BRIEFING"),
            CabinRow => Label("MSG_BTN_RETURN_TO_CABIN", "RETURN TO CABIN"),
            FlightCheckRow => Label("MSG_BTN_GO_TO_FLIGHT_CHECK", "GO TO FLIGHT CHECK"),
            _ => NoteLine(row)?.Text ?? string.Empty,
        };
    }

    /// <summary>The flag pin a note line stands for: the <c>OBJPIN&lt;n&gt;</c> picture matching
    /// its <c>ZEPTEXT&lt;n&gt;</c> text element, where the state ships one.</summary>
    public override HangarArt? RowArt(int row)
    {
        Sync();
        if (row < ButtonRows || _reveal == null)
        {
            return null;
        }

        int index = _reveal.RevealedObjectives[row - ButtonRows];
        foreach (var element in _reveal.Elements)
        {
            if (element.ObjectiveIndex == index && PinFor(element.Id) is { } pin)
            {
                return Picture(pin, string.Empty);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        Sync();
        return row switch
        {
            ReplayRow => "Plays the briefing again from the start",
            CabinRow => "Back to the cabin",
            FlightCheckRow => "On to the flight check",
            _ => NoteHeading(),
        };
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        Sync();
        switch (row)
        {
            case ReplayRow:
                _reveal?.Restart();
                return true;
            case CabinRow:
                Flow.GoTo(CampaignScreen.Cabin);
                return true;
            case FlightCheckRow:
                Flow.GoTo(CampaignScreen.FlightCheck);
                return true;
            default:
                // A note line is the briefing's text, not an action; the press stays on the screen.
                return true;
        }
    }

    // One pass of the reveal's elements: the ToBack ones first, then the rest, both in the order
    // the script placed them, which is the order they stack in.
    private static void AddElements(List<BoardPicture> into, BriefingReveal reveal, bool back)
    {
        foreach (var element in reveal.Elements)
        {
            if (element.Back == back && element.Visible && element.Opacity > 0f
                && element.Bitmap.Length > 0)
            {
                into.Add(new BoardPicture(
                    new BoardArt(BoardArtLibrary.Rimage, element.Bitmap),
                    element.At.X, element.At.Y, 0, element.Center, element.Opacity, element.Revs));
            }
        }
    }

    // The OBJPIN whose trailing number matches a ZEPTEXT's, which is how every state pairs the two
    // (censused across all 24). A state that ships no matching pin simply has none.
    private string? PinFor(string textId)
    {
        int digits = textId.Length;
        while (digits > 0 && char.IsDigit(textId[digits - 1]))
        {
            digits--;
        }

        if (digits == textId.Length || _reveal == null)
        {
            return null;
        }

        var pin = _reveal.Element("OBJPIN" + textId[digits..]);
        return pin is { Bitmap.Length: > 0 } ? pin.Bitmap : null;
    }

    private BriefingObjective? NoteLine(int row)
    {
        if (_reveal == null || row < ButtonRows)
        {
            return null;
        }

        int index = _reveal.RevealedObjectives[row - ButtonRows];
        return index >= 0 && index < _objectives.Count ? _objectives[index] : null;
    }

    private string NoteHeading() => Label("MSG_BRF_DLG_OBJECTIVES", "Objectives");

    private string MapCaption()
    {
        if (_mission is not { } mission)
        {
            return string.Empty;
        }

        string act = Flow.Strings.Text(1220 + (mission.Seq / 5), string.Empty);
        return act.Length > 0 ? act : Flow.Strings.Text(3480 + mission.Seq, string.Empty);
    }

    // A MSG_* label, with the original's own words as the fallback: Messages.Get hands back the
    // raw key when the table is missing, and a menu shows a label rather than a key.
    private string Label(string key, string fallback)
    {
        string text = _messages.Get(key);
        return text.StartsWith("MSG_", StringComparison.Ordinal) ? fallback : text;
    }

    // One rimage picture, decoded once and kept including a miss, so an absent extraction is
    // probed once per bitmap rather than once per frame.
    private HangarArt? Picture(string bitmap, string caption)
    {
        if (_art.TryGetValue(bitmap, out var art))
        {
            return art;
        }

        art = null;
        if (Flow.DataRoot is { } root)
        {
            var path = Path.Combine(root, "extracted", "rimage", bitmap.ToLowerInvariant() + ".png");
            if (PngImage.TryLoad(path) is { } image)
            {
                art = new HangarArt(image, caption);
            }
        }

        _art[bitmap] = art;
        return art;
    }

    private void Sync()
    {
        if (_loaded == Flow.MissionSeq)
        {
            return;
        }

        _loaded = Flow.MissionSeq;
        _mission = null;
        _state = null;
        _reveal = null;
        _objectives = Array.Empty<BriefingObjective>();
        NarrationWav = string.Empty;
        _art.Clear();
        if (Flow.DataRoot is { } root && Flow.MissionSeq >= 0)
        {
            Load(root);
        }
    }

    // Everything the screen shows, from the one integer the cabin set. A broken or absent
    // extraction leaves the page on its buttons rather than taking the menu down with it.
    private void Load(string root)
    {
        try
        {
            var shared = SessionPaths.PreferUnzipped(Path.Combine(root, "extracted", "zrdr.zip"));
            foreach (var mission in CampaignSequence.Load(shared))
            {
                if (mission.Seq == Flow.MissionSeq)
                {
                    _mission = mission;
                }
            }

            if (_mission is not { } found)
            {
                return;
            }

            _messages = Messages.Load(Path.Combine(root, "extracted", "messages.json"));
            _state = BriefingDialog.Load(shared).Find(found.Campaign, found.Mission);
            _objectives = BriefingObjectives.Load(
                Zrdr.LoadFile(
                    SessionPaths.MissionZrdr(root, found.ChapterFolder, found.MissionFolder),
                    "objectives.json"),
                _messages);
            if (_state is { } state)
            {
                NarrationWav = SoundDefs.Load(shared).TryGetValue(state.Sound, out var def)
                    ? def.WavName
                    : string.Empty;
                _reveal = new BriefingReveal(state.Steps, Markers(root));
            }
        }
        catch (IOException)
        {
            // An extraction that is absent or half-written: the screen degrades, it does not throw.
        }
        catch (InvalidDataException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.Text.Json.JsonException)
        {
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

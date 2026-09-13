using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;
using CSVM.UI.Menu;

namespace CSVM.UI;

/// <summary>
/// The campaign load screen's authored half, with the two things the original's own constructor
/// binds into it at runtime: the mission's objectives, which the parchment lists, and the
/// profile's memento image. Read once per launch, since a load screen is a still.
/// Decode: docs/org/loading-screen.md.
/// </summary>
public sealed record LoadSheet(
    EscapeState State,
    EscapeShared Shared,
    BriefingReveal Reveal,
    string ObjectivesTitle,
    IReadOnlyList<string> Objectives,
    string Memento)
{
    /// <summary>Reads one campaign loading dialog, the block every dialog in the file shares and
    /// the mission's own objectives, runs the beat sheet out through
    /// <see cref="EscapeDialog.Settled"/>, and resolves the parchment's title. Null for an
    /// unreadable extraction or a key the file does not carry, which leaves the screen its frame
    /// and its bar.</summary>
    public static LoadSheet? Load(
        string zrdrPath,
        string messagesPath,
        string missionZrdrPath,
        string dialogKey,
        string memento)
    {
        EscapeDialog dialog;
        Messages messages;
        IReadOnlyList<BriefingObjective> objectives;
        try
        {
            dialog = EscapeDialog.Load(zrdrPath, EscapeDialog.LoadingFile);
            messages = Messages.Load(messagesPath);
            objectives = BriefingObjectives.Load(
                Zrdr.LoadFile(missionZrdrPath, "objectives.json"), messages);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;
        }

        if (dialog.Find(dialogKey) is not { } state || dialog.Shared is not { } shared)
        {
            return null;
        }

        var rows = new List<string>(objectives.Count);
        foreach (var objective in objectives)
        {
            rows.Add(objective.Text);
        }

        return new LoadSheet(
            state,
            shared,
            EscapeDialog.Settled(state.Steps),
            EscapeDialog.Label(messages, shared.Objectives?.TitleKey ?? string.Empty),
            rows,
            memento);
    }
}

/// <summary>
/// What the mission load screen is made of, in the original's 800x600 dialog space: the campaign
/// chart sheet, and the Instant Action blackboard with the four texts its own <c>loading_i</c>
/// dialog places. Engine-free, so what the screen says and where it says it settles without a
/// window; <see cref="LoadBoard"/> is only the node that hangs it over the build.
/// The dialogs, the mission-type letter and the face mapping: <c>docs/org/loading-screen.md</c>.
/// </summary>
public static class LoadScreens
{
    // The reader file all three load-screen families live in. This install ships no
    // ia_loading.zrd, so it is the file the original opens too.
    private const string DialogFile = "Loading.json";

    // The dialog name the mission-type letter completes. The environment digit selects nothing in
    // the Instant Action family bar loading_i6a's HEAD2, so the lowest is the one read.
    private const string DialogPrefix = "loading_i1";

    // loadListTitle, as the size its words measure in the reference screenshots. The face itself is
    // not shipped, so a board writes the original's placement in its own.
    private const float TitleFace = 17f;

    // loadListbody, the blurb and the win condition, measured the same way.
    private const float BodyFace = 13f;

    // The frame behind the campaign sheet, which every campaign dialog names as its one background
    // image. A sheet that did not read stands on it alone.
    private const string Frame = "loadframe";

    // The unlit bar, the PROGRESS entry every campaign dialog authors at one position. What fills
    // it is the repaint's own pixel clip over prog_redload (docs/org/loading-screen.md).
    private const string BarArt = "prog_blkload";

    // The bar's authored top left.
    private const float BarX = 90f;
    private const float BarY = 548f;

    // The pitch the blurb's wrapped lines take, measured between the same two lines. Our own face
    // leads wider than this, which runs a three-line blurb through the chalk rule below it.
    private const float BodyLeading = 13f;

    /// <summary>The board a node holds before it has one of its own, drawing nothing.</summary>
    public static ComposedBoard Empty { get; } = new(
        Array.Empty<BoardPicture>(),
        Array.Empty<BoardStroke>(),
        Array.Empty<BoardLine>(),
        Array.Empty<BoardPlaque>());

    /// <summary>The board for one launch. <paramref name="campaign"/> picks the paper sheet over the
    /// blackboard, the split the original makes, and takes <paramref name="sheet"/> as its whole
    /// content. <paramref name="missionType"/> names the Instant Action dialog to read, or is null
    /// for a mode of ours, which takes <paramref name="subject"/> as its heading instead; neither
    /// reaches the campaign sheet, which writes no words of ours.</summary>
    public static ComposedBoard For(
        bool campaign,
        string subject,
        string? missionType,
        string zrdrPath,
        string messagesPath,
        LoadSheet? sheet = null) =>
        campaign
            ? CampaignSheet(sheet)
            : Blackboard(Texts(missionType, subject, zrdrPath, messagesPath));

    /// <summary>The mission-type letter that completes the dialog name, from the jump table at
    /// <c>0x004a14f0</c>: the ace duel, the squadron, the stunt run and the zeppelin. Null for a
    /// mode of ours, which no shipped dialog describes.</summary>
    public static char? LetterFor(string? missionType) => missionType?.ToLowerInvariant() switch
    {
        "dogfight_ace" => 'a',
        "dogfight_squadron" => 'd',
        "stunt_flying" => 's',
        "zeppelin_run" => 'z',
        _ => null,
    };

    /// <summary>The words one load screen writes. An Instant Action mission takes its dialog's four
    /// texts; free flight and dogfight are ours rather than the original's, so they take
    /// <paramref name="heading"/> alone at the mode heading's authored place, no dialog's blurb
    /// being true of them.</summary>
    public static IReadOnlyList<BoardLine> Texts(
        string? missionType, string heading, string zrdrPath, string messagesPath)
    {
        if (LetterFor(missionType) is not { } letter)
        {
            return new[] { new BoardLine(heading, 70f, 35f, 0f, TitleFace, BoardInk.Heading) };
        }

        return DialogTexts(zrdrPath, DialogFile, DialogPrefix + letter, Messages.Load(messagesPath));
    }

    /// <summary>The words one Instant Action dialog writes, at its own authored positions, wrap
    /// widths and faces. Taken by file and key so the pause screen's <c>ia_escape.zrd</c> dialog
    /// draws through the same composition as the load screen's <c>Loading.zrd</c> one: the two
    /// author the same four texts (docs/org/pause-screen.md).</summary>
    public static IReadOnlyList<BoardLine> DialogTexts(
        string zrdrPath, string file, string dialog, Messages messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var lines = new List<BoardLine>(4);
        foreach (var text in Widgets(zrdrPath, file, dialog))
        {
            var at = text.List("at");
            if (at is not { Count: >= 2 } || at[0] is not float x || at[1] is not float y)
            {
                continue;
            }

            // The empty font is the dialog's own default face, which the stunt screen writes its
            // mission type in and no other widget on the board uses.
            string face = text.Str("font") ?? string.Empty;
            bool body = face.Equals("loadListbody", StringComparison.OrdinalIgnoreCase);
            lines.Add(new BoardLine(
                messages.Get(text.Str("string")), x, y, text.Float("wrap"),
                body ? BodyFace : TitleFace,
                body ? BoardInk.Row : BoardInk.Heading,
                Bold: face.Length == 0,
                Leading: body ? BodyLeading : 0f));
        }

        return lines;
    }

    // The campaign screen, through the same drawer PauseScreens uses: the mission's chart at its
    // authored crop, the pins, device icons and propeller its script places, the parchment, the
    // memento over its shadow, and the unlit bar.
    // ⚠ Place no ownship or zeppelin icon here: the load dialog's constructor binds neither, and
    // the pause screen's is the only one that does (docs/org/pause-screen.md).
    // An unreadable sheet leaves the frame and the bar standing, which is all this screen promises.
    private static ComposedBoard CampaignSheet(LoadSheet? sheet)
    {
        var backdrop = new List<BoardPicture>
        {
            new(
                new BoardArt(BoardArtLibrary.Rimage, Background(sheet)), 0, 0, 0, false, 1f, 0f,
                BoardFit.AuthoredWidth, BoardFit.AuthoredHeight),
        };
        var pictures = new List<BoardPicture>();
        var lines = new List<BoardLine>();
        if (sheet != null)
        {
            if (sheet.State.Map is { Bitmap.Length: > 0 } map)
            {
                pictures.Add(MissionMap.Sheet(map));
            }

            if (sheet.Shared.Objectives is { Background.Length: > 0 } list)
            {
                pictures.Add(new BoardPicture(
                    new BoardArt(BoardArtLibrary.Rimage, list.Background),
                    list.BackgroundAt.X, list.BackgroundAt.Y));
            }

            MissionMap.Elements(pictures, sheet.Reveal, back: true);
            MissionMap.Elements(pictures, sheet.Reveal, back: false);
            AddMemento(pictures, sheet);
            if (sheet.Shared.Objectives != null && sheet.ObjectivesTitle.Length > 0)
            {
                lines.Add(new BoardLine(
                    sheet.ObjectivesTitle, sheet.Shared.Objectives.TitleAt.X,
                    sheet.Shared.Objectives.TitleAt.Y, 0f, EscapeObjectivesList.TitleFont,
                    BoardInk.Heading));
            }
        }

        pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, BarArt), BarX, BarY));
        return new ComposedBoard(
            pictures,
            MissionMap.Strokes(sheet?.Reveal),
            lines,
            Array.Empty<BoardPlaque>(),
            Notes(sheet),
            backdrop);
    }

    // The frame the dialog names, which every campaign dialog authors as its one background image.
    private static string Background(LoadSheet? sheet) =>
        sheet?.State.Background is { Length: > 0 } named ? named : Frame;

    // The parchment's rows: the mission's own objectives in the order the script indexed them.
    // None is marked, because the screen stands before the mission it lists has run.
    private static IReadOnlyList<BoardNote> Notes(LoadSheet? sheet)
    {
        if (sheet?.Shared.Objectives is not { } list)
        {
            return Array.Empty<BoardNote>();
        }

        var entries = new List<string>();
        foreach (int index in sheet.Reveal.RevealedObjectives)
        {
            if (index >= 0 && index < sheet.Objectives.Count)
            {
                entries.Add(sheet.Objectives[index]);
            }
        }

        if (entries.Count == 0)
        {
            return Array.Empty<BoardNote>();
        }

        return new[]
        {
            new BoardNote(
                entries, list.ListAt.X, list.ListAt.Y, list.RowWrap, list.WrapHeight,
                list.Spacing, EscapeObjectivesList.RowFont, BoardInk.Row, Italic: true),
        };
    }

    // The memento's position is authored and its picture is not: the runtime replaces the
    // placeholder with the profile's own memento image. Drawn after the script's elements so it
    // sits over the shadow the script centres under it.
    private static void AddMemento(List<BoardPicture> into, LoadSheet sheet)
    {
        string bitmap = sheet.Memento.Length > 0 ? sheet.Memento : sheet.State.MementoBitmap;
        if (bitmap.Length > 0)
        {
            into.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Rimage, bitmap),
                sheet.State.MementoAt.X, sheet.State.MementoAt.Y));
        }
    }

    // The Instant Action screen: the blackboard, its three authored photographs each centred on
    // its own coordinate, the unlit lamp bar and one still propeller frame beside it.
    private static ComposedBoard Blackboard(IReadOnlyList<BoardLine> lines) =>
        new(
            new[]
            {
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "loadframempt2"), 0, 0),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "MP-shotdown"), 197, 157, 0, true),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "MP-crash"), 197, 307, 0, true),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "mp-dangerzone2"), 197, 457, 0, true),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "prp0"), 506, 549),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "prog_blk"), 564, 546),
            },
            Array.Empty<BoardStroke>(),
            lines,
            Array.Empty<BoardPlaque>());

    // Every Text the named dialog's script places, in script order, as its own property dict. An
    // unreadable or absent extraction yields none, since a board must still come up without one.
    private static IEnumerable<ZrdrDict> Widgets(string zrdrPath, string file, string dialog)
    {
        List<object?>? script;
        try
        {
            script = Script(Zrdr.LoadFile(zrdrPath, file), dialog);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            yield break;
        }

        if (script == null)
        {
            yield break;
        }

        for (int i = 0; i + 1 < script.Count; i++)
        {
            if (script[i] is "Text" && script[i + 1] is List<object?> { Count: > 1 } args)
            {
                yield return ZrdrDict.FromAlternating(args.GetRange(1, args.Count - 1));
            }
        }
    }

    // A dialog's beat sheet, under whichever of the three names its own file gives the block. The
    // file is one alternating list: the shared image path, the shared primitives, then every dialog
    // by name, so the name is looked up in it directly.
    private static List<object?>? Script(List<object?> root, string dialog)
    {
        if (root.Count == 0 || root[0] is not List<object?> file)
        {
            return null;
        }

        for (int i = 0; i + 1 < file.Count; i++)
        {
            if (file[i] is string name
                && name.Equals(dialog, StringComparison.OrdinalIgnoreCase)
                && file[i + 1] is List<object?> body)
            {
                var d = ZrdrDict.FromAlternating(body);
                return d.List("SCRIPT") ?? d.List("ESC_SCRIPT") ?? d.List("LOADING_SCRIPT");
            }
        }

        return null;
    }
}

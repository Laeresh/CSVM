using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;

namespace CSVM.UI;

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
    /// blackboard, the split the original makes. <paramref name="missionType"/> names the Instant
    /// Action dialog to read, or is null for a mode of ours, which takes
    /// <paramref name="subject"/> as its heading instead.</summary>
    public static ComposedBoard For(
        bool campaign, string subject, string? missionType, string zrdrPath, string messagesPath) =>
        campaign
            ? CampaignSheet(subject)
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

        var messages = Messages.Load(messagesPath);
        var lines = new List<BoardLine>(4);
        foreach (var text in Widgets(zrdrPath, DialogPrefix + letter))
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

    // The campaign screen: a chart sheet in its frame, with the unlit bar under it at the position
    // the repaint measures its fill from. No pictures, which is what the campaign dialogs carry.
    private static ComposedBoard CampaignSheet(string subject)
    {
        var lines = new List<BoardLine>(2)
        {
            new("LOADING", 60f, 60f, 460f, 26f, BoardInk.Heading),
        };
        if (subject.Length > 0)
        {
            lines.Add(new BoardLine(subject, 60f, 100f, 460f, 15f, BoardInk.Row));
        }

        return new ComposedBoard(
            new[]
            {
                new BoardPicture(
                    new BoardArt(BoardArtLibrary.Rimage, "loadframe"), 0, 0, 0, false, 1f, 0f,
                    BoardFit.AuthoredWidth, BoardFit.AuthoredHeight),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "prog_blkload"), 90, 548),
            },
            Array.Empty<BoardStroke>(),
            lines,
            Array.Empty<BoardPlaque>());
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
    private static IEnumerable<ZrdrDict> Widgets(string zrdrPath, string dialog)
    {
        List<object?>? script;
        try
        {
            script = Script(Zrdr.LoadFile(zrdrPath, DialogFile), dialog);
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

    // A dialog's SCRIPT block. The file is one alternating list: the shared image path, the shared
    // primitives, then all 81 dialogs by name, so the name is looked up in it directly.
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
                return ZrdrDict.FromAlternating(body).List("SCRIPT");
            }
        }

        return null;
    }
}

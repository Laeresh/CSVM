using CSVM.Mech3;
using CSVM.Net;
using CSVM.UI.Boards;
using CSVM.UI.Screens;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The words, faces, label colours and plaques the Multiplayer Connection and Lobby pages share.
/// Both read the original's string table under one data root and draw every multiplayer face in
/// regular weight. Both tint a plaque's label with the scripts' four label colours.
/// </summary>
internal sealed class MultiplayerBoardText
{
    /// <summary>The text height a line takes when its string names no face.</summary>
    internal const float TextFallback = 12f;

    /// <summary>The word a bot row shows for a Random plane. No langui row words one, so it is the
    /// remake's own.</summary>
    internal const string RandomPlaneWord = "Random";

    // The scripts' label colours on the plaques and radios: greyed, normal, rollover and pressed.
    internal static readonly BoardTint LabelDisabled = new(142, 142, 142);
    internal static readonly BoardTint LabelNormal = new(226, 224, 206);
    internal static readonly BoardTint LabelRollover = new(198, 188, 140);
    internal static readonly BoardTint LabelPressed = new(255, 204, 102);

    private readonly IOriginalScreenHost _host;
    private readonly string? _dataRoot;
    private UiStrings? _strings;

    /// <summary>The shared text of a page drawn for <paramref name="host"/>, reading the string
    /// table under <paramref name="dataRoot"/>.</summary>
    internal MultiplayerBoardText(IOriginalScreenHost host, string? dataRoot)
    {
        _host = host;
        _dataRoot = dataRoot;
    }

    /// <summary>The string table, loaded on first read. Empty when the data root has none.</summary>
    internal UiStrings Strings => _strings ??= (_dataRoot is { } root ? UiStrings.TryLoad(root) : null) ?? UiStrings.Empty;

    /// <summary>A plaque label's colour for its state.</summary>
    internal static BoardTint LabelTint(bool enabled, bool focused, bool pressed) =>
        !enabled ? LabelDisabled : pressed ? LabelPressed : focused ? LabelRollover : LabelNormal;

    /// <summary>String <paramref name="id"/>'s text in <paramref name="strings"/>, or
    /// <paramref name="fallback"/> when the table lacks it. A leading <c>]</c>, which several lobby
    /// strings carry, is not drawn. A board drawn in flight reads the lobby's words this way.</summary>
    internal static string Word(UiStrings strings, int id, string fallback)
    {
        string text = strings.Text(id, fallback).Trim().TrimStart(']');
        return text.Length > 0 ? text : fallback;
    }

    /// <summary>A string's face in regular weight: the original's capture draws every multiplayer
    /// face that way, its B tags included.</summary>
    internal static LanguiFace? Regular(UiStrings strings, int id) =>
        LanguiFace.Parse(strings.Face(id)) is { } face ? face with { Bold = false } : null;

    /// <summary><see cref="Word(UiStrings, int, string)"/> over this page's table.</summary>
    internal string Word(int id, string fallback) => Word(Strings, id, fallback);

    /// <summary><see cref="Regular(UiStrings, int)"/> over this page's table.</summary>
    internal LanguiFace? Regular(int id) => Regular(Strings, id);

    /// <summary>IDS_IA_DIFFICULTY's word for a bot tier, its first letter raised as the lobby's are.</summary>
    internal string SkillWord(NetBotSkill skill)
    {
        string word = skill switch
        {
            NetBotSkill.Novice => Word(3695, "novice"),
            NetBotSkill.Ace => Word(3697, "ace"),
            _ => Word(3696, "veteran"),
        };

        return word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    /// <summary>One string of the table in the face its row names, or the face of
    /// <paramref name="faceId"/> when that is set, in an authored colour.</summary>
    internal BoardLine Line(
        int id, string fallback, float x, float y, float width, BoardTint colour, BoardJustify justify = BoardJustify.Left,
        string? text = null, int faceId = 0)
    {
        var face = Regular(faceId != 0 ? faceId : id);
        return new BoardLine(
            text ?? Word(id, fallback), x, y, width, face?.Pixels ?? TextFallback, BoardInk.Row, -1,
            Justify: justify, Face: face, Colour: colour);
    }

    /// <summary>A plaque button drawn from a strip of <paramref name="frames"/> frames, sized by
    /// its art.</summary>
    internal OriginalRow Strip(
        string key, string art, float x, float y, bool enabled, int column, float fallbackWidth, float fallbackHeight, int frames = 4)
    {
        var strip = new BoardArt(BoardArtLibrary.Ui, art, frames);
        var size = OriginalWidgets.StripSize(strip, _host.Measure, fallbackWidth, fallbackHeight);
        return new OriginalRow(key, string.Empty, OriginalRowKind.Button, x, y, size.Width, size.Height, enabled, column, strip);
    }
}

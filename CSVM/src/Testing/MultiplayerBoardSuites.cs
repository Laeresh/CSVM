using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.Testing;

/// <summary>Every multiplayer blackboard the lobby offers, on both screens that draw it: a
/// Dogfight's load screen and its pause sheet. Each text is measured in the face the board draws it
/// in. It must stay on the blackboard the frame art paints, clear of every picture, strip and other
/// text. Decode: docs/org/loading-screen.md and docs/org/pause-screen.md.</summary>
internal static class MultiplayerBoardSuites
{
    // How much redder than blue a frame pixel is before it counts as the copper frame rather than
    // the board. The board and its chalk are grey, at most 10 apart over the whole text area.
    private const float Warm = 15f / 255f;

    // The alpha a picture's pixel covers a text at.
    private const float Opaque = 0.5f;

    // The lobby's four mode rows as the load screen keys them: a Deathmatch alone and teamed,
    // Capture the Flag and Zeppelin vs Zeppelin.
    private static readonly (DogfightMissionType Type, bool Teamed)[] Modes =
    {
        (DogfightMissionType.Deathmatch, false), (DogfightMissionType.Deathmatch, true),
        (DogfightMissionType.CaptureTheFlag, false), (DogfightMissionType.ZeppelinVsZeppelin, false),
    };

    [Suite("multiplayer-board",
        "every multiplayer dialog the lobby offers (each environment and mode) on the Dogfight load "
        + "screen and on its pause sheet: every text, measured wrapped in the board's own face, "
        + "stays on the blackboard the frame art paints (no copper pixel under it), touches no "
        + "opaque pixel of a photograph, the propeller, the bar or a pause strip, and runs into no "
        + "other text; the same load texts with no wrap run off the board and the rows at their "
        + "authored points reach the bar, propeller or strips, so both measures can fail")]
    internal static void MultiplayerBoard(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, EscapeDialog.LoadingFile);
        var probe = new Control();
        ctx.Host.AddChild(probe);
        try
        {
            if (probe.GetThemeDefaultFont() is not { } font)
            {
                throw new SuiteSkippedException("the default theme carries no font to measure with");
            }

            Sweep(ctx, font);
        }
        finally
        {
            ctx.Host.RemoveChild(probe);
            probe.QueueFree();
        }
    }

    private static void Sweep(TestContext ctx, Font font)
    {
        var judge = new Judge(ctx, font);
        var messages = ctx.Data.Messages;
        int offered = 0, screens = 0, unwrapped = 0, unlifted = 0;
        for (int environment = 0; environment < DogfightLobby.EnvironmentCount; environment++)
        {
            string chapter = DogfightLobby.ChapterOf(environment);
            foreach (var mode in Modes)
            {
                if (!DogfightLobby.Offers(mode.Type, environment))
                {
                    continue;
                }

                offered++;
                if (LoadScreens.MultiplayerKey(chapter, mode.Type, mode.Teamed) is not { } key
                    || PauseSheet.LoadMultiplayer(ctx.ZrdrPath, ctx.Data.Messages, key) is not { } sheet)
                {
                    ctx.Check(false, $"{chapter}'s Dogfight ({mode}) resolves a multiplayer dialog");
                    continue;
                }

                var load = LoadScreens.For(
                    false, string.Empty, null, ctx.ZrdrPath, ctx.Data.Messages, briefing: key);
                var pause = PauseScreens.For(sheet, PauseReadout.Empty, 0, false);
                if (load.Pictures.Count == 0 || pause.Backdrop.Count == 0)
                {
                    ctx.Check(false, $"{key} composes its frame on both screens");
                    continue;
                }

                screens += 2;
                var loadOver = Over(load.Pictures, 1);
                var pauseOver = Over(pause.Pictures, 0);
                foreach (var plaque in pause.Plaques)
                {
                    pauseOver.Add((plaque.Art.Name, plaque.X, plaque.Y, false));
                }

                judge.Screen($"{key} load", load.Pictures[0].Art.Name, load.Lines, loadOver, true);
                judge.Screen($"{key} pause", pause.Backdrop[0].Art.Name, pause.Lines, pauseOver, true);

                // The controls: the texts unwrapped, and wrapped but at their authored points and
                // sizes, must fail the same measures.
                var raw = LoadScreens.DialogTexts(ctx.ZrdrPath, EscapeDialog.LoadingFile, key, messages);
                unwrapped += judge.Screen($"{key} load unwrapped", load.Pictures[0].Art.Name, raw, loadOver, false).Outside;
                var loadAuthored = judge.Screen(
                    $"{key} load at authored points", load.Pictures[0].Art.Name, Measured(raw), loadOver, false);
                unlifted += loadAuthored.Outside + loadAuthored.Covered + judge.Screen(
                    $"{key} pause at authored points", pause.Backdrop[0].Art.Name,
                    Measured(Authored(ctx, key, messages)), pauseOver, false).Covered;
            }
        }

        ctx.Check(offered > 0, $"the lobby offers multiplayer dialogs to sweep ({offered})");
        ctx.Same(offered * 2, screens, $"every offered dialog composes a load screen and a pause sheet");
        ctx.Same(0, judge.Outside, $"no text on either screen leaves the blackboard");
        ctx.Same(0, judge.Covered, $"no text sits under a photograph, the propeller, the bar or a pause strip");
        ctx.Same(0, judge.Crossed, $"no text runs into another");
        ctx.Check(unwrapped > 0, $"the board measure can fail: unwrapped, {unwrapped} load text(s) leave the board");
        ctx.Check(
            unlifted > 0,
            $"the furniture measure can fail: at their authored points, {unlifted} row(s) reach the bar, propeller or strips");
        foreach (var (kind, size) in judge.Smallest)
        {
            ctx.Note($"the smallest {kind} row is set at {size:0.##} px ({size * 72f / 96f:0.#} pt) against the body's 13 px");
            judge.Report.AppendLine($"smallest {kind} row: {size:0.##} px");
        }

        judge.Report.AppendLine(
            $"{screens} screens over {offered} dialogs; controls: {unwrapped} unwrapped off the board, "
            + $"{unlifted} authored rows on the furniture");
        ctx.WriteArtifact("test-multiplayer-board.txt", judge.Report.ToString());
    }

    // Every unwrapped body text given the widest wrap its dialog's body authors. No other board
    // rule applies: no box and no moved row.
    private static List<BoardLine> Measured(IReadOnlyList<BoardLine> raw)
    {
        float measure = 0f;
        foreach (var line in raw)
        {
            measure = line.Leading > 0f ? Mathf.Max(measure, line.Width) : measure;
        }

        var set = new List<BoardLine>(raw.Count);
        foreach (var line in raw)
        {
            set.Add(line.Leading > 0f && line.Width <= 0f ? line with { Width = measure } : line);
        }

        return set;
    }

    // The pause sheet's texts as its file authors them, from the file the sheet itself read.
    private static IReadOnlyList<BoardLine> Authored(TestContext ctx, string key, Messages messages)
    {
        var escape = LoadScreens.DialogTexts(ctx.ZrdrPath, EscapeDialog.CampaignFile, key, messages);
        return escape.Count > 0
            ? escape
            : LoadScreens.DialogTexts(ctx.ZrdrPath, EscapeDialog.LoadingFile, key, messages);
    }

    private static List<(string Art, float X, float Y, bool Centered)> Over(
        IReadOnlyList<BoardPicture> pictures, int from)
    {
        var over = new List<(string, float, float, bool)>();
        for (int i = from; i < pictures.Count; i++)
        {
            over.Add((pictures[i].Art.Name, pictures[i].X, pictures[i].Y, pictures[i].Centered));
        }

        return over;
    }

    // Measures one screen's texts and keeps the tally. A control screen is reported but tallied
    // apart, since it is meant to fail.
    private sealed class Judge(TestContext ctx, Font font)
    {
        private readonly BoardFit _fit = BoardFit.For(BoardFit.AuthoredWidth, BoardFit.AuthoredHeight);
        private readonly Dictionary<string, Image?> _art = new();

        public StringBuilder Report { get; } = new();

        public int Outside { get; private set; }

        public int Covered { get; private set; }

        public int Crossed { get; private set; }

        // The smallest face the renderer fits a body row to, per screen, where any row was fitted.
        public Dictionary<string, float> Smallest { get; } = new();

        public (int Outside, int Covered) Screen(
            string who,
            string frameArt,
            IReadOnlyList<BoardLine> lines,
            List<(string Art, float X, float Y, bool Centered)> over,
            bool tally)
        {
            if (Art(frameArt) is not { } frame)
            {
                ctx.Check(false, $"{who}: the frame {frameArt} is extracted");
                return (0, 0);
            }

            // The frame and the pictures are tested against each drawn line. Neighbours are tested
            // against the slot a block takes at its pitch, since its own wrapped lines share descents.
            var texts = new List<(BoardLine Line, IReadOnlyList<Rect2> Drawn, Rect2 Slot)>();
            foreach (var authored in lines)
            {
                // Measured as drawn: a boxed row at the face the renderer fits it to.
                var line = ComposedBoardView.Fitted(_fit, font, authored);
                if (tally && line.Size < authored.Size)
                {
                    string kind = who.EndsWith("pause", System.StringComparison.Ordinal) ? "pause" : "load";
                    Smallest[kind] = Mathf.Min(Smallest.TryGetValue(kind, out float was) ? was : line.Size, line.Size);
                }

                if (line.Text.Length > 0)
                {
                    var drawn = ComposedBoardView.LineBoxes(_fit, font, line);
                    float wide = 0f;
                    foreach (var box in drawn)
                    {
                        wide = Mathf.Max(wide, box.Size.X);
                    }

                    texts.Add((line, drawn, new Rect2(
                        line.X, line.Y, wide, ComposedBoardView.Block(_fit, font, line))));
                }
            }

            int outside = 0, covered = 0, crossed = 0;
            for (int i = 0; i < texts.Count; i++)
            {
                var (line, drawn, slot) = texts[i];
                if (Any(drawn, box => Painted(frame, box, 0f, 0f, c => c.R - c.B > Warm, outOfArt: true)))
                {
                    outside++;
                    Report.AppendLine($"  {who}: '{Clip(line.Text)}' leaves the board");
                }

                foreach (var (art, x, y, centered) in over)
                {
                    if (Art(art) is not { } picture)
                    {
                        continue;
                    }

                    var at = centered
                        ? new Vector2(x - (picture.GetWidth() / 2f), y - (picture.GetHeight() / 2f))
                        : new Vector2(x, y);
                    if (Any(drawn, box => Painted(picture, box, at.X, at.Y, c => c.A > Opaque, outOfArt: false)))
                    {
                        covered++;
                        Report.AppendLine($"  {who}: '{Clip(line.Text)}' is under {art}");
                    }
                }

                for (int j = i + 1; j < texts.Count; j++)
                {
                    if (slot.Intersects(texts[j].Slot))
                    {
                        crossed++;
                        Report.AppendLine($"  {who}: '{Clip(line.Text)}' runs into '{Clip(texts[j].Line.Text)}'");
                    }
                }
            }

            Report.AppendLine($"{who}: {texts.Count} texts, {outside} off the board, {covered} covered, {crossed} crossed");
            if (tally)
            {
                Outside += outside;
                Covered += covered;
                Crossed += crossed;
            }

            return (outside, covered);
        }

        private static string Clip(string text) => text.Length > 32 ? text[..32] : text;

        private static bool Any(IReadOnlyList<Rect2> boxes, System.Func<Rect2, bool> hit)
        {
            foreach (var box in boxes)
            {
                if (hit(box))
                {
                    return true;
                }
            }

            return false;
        }

        // Whether any pixel of the art, hung with its corner at (x, y), inside the box passes the
        // test. A box reaching past the art counts as a hit only where the art is the whole screen.
        private static bool Painted(
            Image art, Rect2 box, float x, float y, System.Func<Color, bool> test, bool outOfArt)
        {
            int x0 = Mathf.FloorToInt(box.Position.X - x), x1 = Mathf.CeilToInt(box.End.X - x);
            int y0 = Mathf.FloorToInt(box.Position.Y - y), y1 = Mathf.CeilToInt(box.End.Y - y);
            for (int py = y0; py < y1; py++)
            {
                for (int px = x0; px < x1; px++)
                {
                    bool inside = px >= 0 && py >= 0 && px < art.GetWidth() && py < art.GetHeight();
                    if (inside ? test(art.GetPixel(px, py)) : outOfArt)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Image? Art(string name)
        {
            if (!_art.TryGetValue(name, out var image))
            {
                string path = Path.Combine(ctx.DataRoot, "extracted", "rimage", name.ToLowerInvariant() + ".png");
                image = File.Exists(path) && Image.LoadFromFile(path) is { } loaded && !loaded.IsEmpty()
                    ? loaded
                    : null;
                _art[name] = image;
            }

            return image;
        }
    }
}

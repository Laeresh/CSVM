using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.Career;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>What a suite's menu host is built from: the shared features a presentation needs and
/// the multiplayer door bound to the loopback address. Beside them stand scratch stores for options
/// and saved planes, and a silent audio service.</summary>
internal static class MenuSuiteHost
{
    /// <summary>Registers the shared features a presentation needs, as the launcher does. Before
    /// the seat: the host lends the setup feature's seat list once the feature is in, so seat 0
    /// has to be joined through it. The Course list reads a suite's own scratch library where one
    /// is passed as <paramref name="courses"/>, and the shipped courses alone otherwise.</summary>
    internal static void AddFeatures(
        MenuHost host, string dataRoot, Spec.GameData data, Action<int, BindingProfile>? saveBindings = null,
        ChapterCinema? chapterCinema = null, ClosingCinema? closingCinema = null,
        NetPlayFeature? netDoor = null, Flight.Modes.CourseLibrary? courses = null)
    {
        host.Features.Add(new FreeFlightFeature());
        // Never the game folder: no suite may depend on a course one machine dropped in.
        var library = courses ?? new Flight.Modes.CourseLibrary(Flight.Modes.CourseLibrary.ShippedRoot, null);
        host.Features.Add(InstantActionFeature.ForGameData(data, chapter =>
            library.List(chapter).Select(f => (f.Name, library.DisplayName(chapter, f.Name))).ToList(),
            (chapter, name) => library.Load(chapter, name, out _)));
        host.Features.Add(new PlayerSetupFeature());
        // The hangar reads its labels and stock defaults as the launcher wires them: the langui
        // table, the stock fits and the root reader scope.
        var strings = UiStrings.TryLoad(dataRoot) ?? UiStrings.Empty;
        host.Features.Add(new HangarFeature(strings, StockAirframes.Node, () => StockLoadouts.Load(), data.Zrdr));
        host.Features.Add(new CampaignFeature(
            strings, StockAirframes.Node, chapterCinema, closingCinema)
        { Data = data });
        // A store of the process's own under the temp folder: no suite may touch user://Careers.
        var careers = new CareerStore(Path.Combine(Path.GetTempPath(), "csvm-suite-careers",
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));
        host.Features.Add(new CareerFeature(() => careers, () => StockLoadouts.Load()));
        // No save by default: a suite must never write over the keymap saved at this machine's
        // controls, and only a suite holding the store's directory override may pass one.
        host.Features.Add(new ControlsFeature(saveBindings));
        host.Features.Add(netDoor ?? NetDoor());
    }

    /// <summary>Ends the suite as SKIP when the run's game files carry no decoded menu layout. That
    /// is no install, or a synthetic tree without the Original shell.</summary>
    internal static void RequireLayout(TestContext ctx) => RequireRof(ctx, RofFiles.MenuLayoutFile, $"decoded menu layout");

    /// <summary>Ends the suite as SKIP when the run's game files lack the UI archive file
    /// <paramref name="relative"/>, the way <c>RequireData</c> does for a missing file.</summary>
    internal static void RequireRof(TestContext ctx, string relative, FormattableString what)
    {
        if (!ctx.Data.Rof.Exists(relative))
        {
            throw new SuiteSkippedException($"{CSVM.Utils.Log.Format(what)} not found: no {relative} in the game files for {ctx.DataRoot}");
        }
    }

    /// <summary>The multiplayer door as a suite gets it: the real ENet carrier, bound to the
    /// loopback address, and no port mapping at all. ⚠ Neither the wildcard bind nor the UPnP
    /// search belongs in a run: one raises a firewall dialog, the other reaches the router.
    /// </summary>
    internal static NetPlayFeature NetDoor() =>
        new((port, guests, bind) => CSVM.Net.EnetTransport.Host(port, guests, bind),
            (address, port) => CSVM.Net.EnetTransport.Join(address, port))
        {
            BindAddress = "127.0.0.1",
        };

    /// <summary>Points the options store at an emptied directory of <paramref name="suite"/>'s own
    /// and answers the one it replaced, which the caller puts back in a finally. The network boxes'
    /// OK remembers a callsign and a game name there, and a later suite must not open on them.
    /// </summary>
    internal static string? ScratchOptions(TestContext ctx, string suite)
    {
        string dir = Path.Combine(ctx.ScratchDir, suite, "Options");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        string? previous = CSVM.Utils.OptionsStore.DirectoryOverride;
        CSVM.Utils.OptionsStore.DirectoryOverride = dir;
        return previous;
    }

    /// <summary>Answers the network boxes <paramref name="shell"/> stands, through each OK:
    /// Game Information with <paramref name="game"/> when it stands, then Player Information with
    /// <paramref name="callsign"/>. <paramref name="click"/> presses a row by key.</summary>
    internal static void AnswerNetInfo(OriginalShell shell, Action<string> click, string callsign, string? game = null)
    {
        if (shell.NetInfo.Page == NetInfoPage.Game)
        {
            shell.NetInfo.Draft.GameName = game ?? callsign;
            click(OriginalNetInfoBox.OkKey);
        }

        if (shell.NetInfo.Page == NetInfoPage.Player)
        {
            shell.NetInfo.Draft.Callsign = callsign;
            click(OriginalNetInfoBox.OkKey);
        }
    }

    /// <summary>A saved-plane store over a fresh <c>Planes</c> folder in <paramref name="suite"/>'s
    /// scratch directory, holding <paramref name="customs"/> builds written through the store's own
    /// writer. A presentation handed it reads this roster, not the player's
    /// <c>user://Planes</c>, whose contents would otherwise decide what the suite sees.
    /// <see cref="DropScratchPlanes"/> removes it.</summary>
    internal static CustomPlaneStore ScratchPlanes(TestContext ctx, string suite, int customs = 0)
    {
        DropScratchPlanes(ctx, suite);
        var store = new CustomPlaneStore(Path.Combine(ctx.ScratchDir, suite, "Planes"));
        for (int i = 0; i < customs; i++)
        {
            store.Save(new CustomPlaneDef
            {
                Name = $"Scratch Build {i + 1:D2}",
                Airframe = i % (CustomPlaneDef.MaxAirframe + 1),
            });
        }

        return store;
    }

    /// <summary>Deletes the folder <see cref="ScratchPlanes"/> made for <paramref name="suite"/>.</summary>
    internal static void DropScratchPlanes(TestContext ctx, string suite)
    {
        string dir = Path.Combine(ctx.ScratchDir, suite, "Planes");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Checks that a field's word sits with its ink inside the box's one-pixel outline and
    /// centred in it, to a pixel. The ink is the issue's own "Slug", measured glyph by glyph in the
    /// board face, standing on the baseline the renderer gives the line. The same word at <paramref name="oldDrop"/> below the box top
    /// is the able-to-fail control, which must miss.</summary>
    internal static void InkInBox(TestContext ctx, string what, BoardLine? line, OriginalRow? box, float oldDrop)
    {
        ctx.Check(line != null && box != null, $"{what} draws its word");
        if (line == null || box == null)
        {
            return;
        }

        var view = new ComposedBoardView();
        try
        {
            // Measured at twice the authored size and halved. At 1x the hinting rounds a 12-pixel
            // face's ascender up a whole pixel, which no window a pilot runs draws.
            var (top, bottom) = Ink(view.GetThemeDefaultFont(), "Slug", (int)MathF.Round(line.Size * 2f));
            (top, bottom) = (top / 2f, bottom / 2f);
            string Margins(float lineY) =>
                $"{lineY + line.Size + top - box.Y:0.0} px of top, {box.Y + box.Height - (lineY + line.Size + bottom):0.0} px of foot";
            bool Fits(float lineY)
            {
                float above = lineY + line.Size + top - box.Y;
                float below = box.Y + box.Height - (lineY + line.Size + bottom);
                return above >= 1f && below >= 1f && MathF.Abs(above - below) <= 1f;
            }

            ctx.Check(bottom > 0f, $"the measured word hangs a descender below its baseline ({top:0.0} to {bottom:0.0})");
            ctx.Check(!Fits(box.Y + oldDrop),
                $"ABLE-TO-FAIL CONTROL: {what}'s word {oldDrop:0} px under the box top misses ({Margins(box.Y + oldDrop)} in {box.Height:0})");
            ctx.Check(Fits(line.Y), $"{what}'s word keeps its ink inside the box and centred ({Margins(line.Y)} in {box.Height:0})");
        }
        finally
        {
            view.Free();
        }
    }

    // A word's ink against the baseline: its glyphs' rows at least half covered, read off the font
    // cache's own atlas. The glyph rectangle alone is padded past the ink.
    private static (float Top, float Bottom) Ink(Godot.Font font, string text, int points)
    {
        var server = Godot.TextServerManager.GetPrimaryInterface();
        var shaped = server.CreateShapedText();
        try
        {
            server.ShapedTextAddString(shaped, text, font.GetRids(), points, font.GetOpentypeFeatures());
            float top = float.MaxValue, bottom = float.MinValue;
            var size = new Godot.Vector2I(points, 0);
            foreach (var glyph in server.ShapedTextGetGlyphs(shaped))
            {
                var rid = glyph["font_rid"].AsRid();
                long index = glyph["index"].AsInt64();
                var offset = server.FontGetGlyphOffset(rid, size, index);
                var uv = server.FontGetGlyphUVRect(rid, size, index);
                var atlas = server.FontGetTextureImage(rid, size, server.FontGetGlyphTextureIdx(rid, size, index));
                for (int y = (int)uv.Position.Y; atlas != null && y < (int)uv.End.Y; y++)
                {
                    for (int x = (int)uv.Position.X; x < (int)uv.End.X; x++)
                    {
                        if (atlas.GetPixel(x, y).A >= 0.5f)
                        {
                            float row = offset.Y + y - uv.Position.Y;
                            top = MathF.Min(top, row);
                            bottom = MathF.Max(bottom, row + 1f);
                            break;
                        }
                    }
                }
            }

            return (top, bottom);
        }
        finally
        {
            server.FreeRid(shaped);
        }
    }

    /// <summary>An audio service that plays nothing and records nothing: a scripted run has no
    /// sound to hear, and the menu tolerates a silent install already.</summary>
    internal sealed class SilentMenuAudio : IMenuAudio
    {
        public void Cue(MenuCue cue)
        {
        }

        public void BeginNarration(string wavName)
        {
        }

        public void EndNarration()
        {
        }

        public void PreviewMix(CSVM.Utils.AudioLevels levels, MenuMixLevel moved)
        {
        }

        public void EndMixPreview()
        {
        }
    }
}

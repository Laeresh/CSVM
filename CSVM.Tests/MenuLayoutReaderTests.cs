using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CSVM.Extraction;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The runtime reader of <c>menu_layout.json</c>, read against the artifact the decoder itself
/// emits from the hand-authored fixtures, so the reader is pinned to the emitter's shape rather
/// than to a copy of it: screens by section, widgets by key, typed field access through the
/// artifact's own kind table, the navigation and asset lists, and the two failure modes a caller
/// falls back on.
/// </summary>
public class MenuLayoutReaderTests
{
    [Fact]
    public void ScreensAreFoundBySectionAndWidgetsByKeyCaseInsensitively()
    {
        var layout = ProbeLayout();

        Assert.Equal(MenuLayoutDecoder.SchemaVersion, layout.Schema);
        Assert.Equal(new[] { "ProbeMain", "ProbeOptions" }, layout.Screens.Select(s => s.Section));
        var main = layout.Screen("probemain");
        Assert.NotNull(main);
        Assert.Equal("ASSETS/SCRIPTS/ProbeMain.SCRIPT", main!.Script);
        Assert.Equal(8, main.Widgets.Count);
        Assert.NotNull(main.Widget("pr_b_start"));
        Assert.Null(main.Widget("PR_B_NOSUCH"));
        Assert.Null(layout.Screen("NoSuchScreen"));
        Assert.Equal(new[] { "pr_t_runtime" }, main.KeysWithoutLayoutRow);
    }

    [Fact]
    public void TypedAccessorsFollowTheArtifactsKindTable()
    {
        var start = ProbeLayout().Screen("ProbeMain")!.Widget("PR_B_START")!;

        Assert.Equal("B", start.TypeCode);
        Assert.Equal("button", start.Widget);
        Assert.Equal("int", start.Type!.KindOf("X"));
        Assert.Equal(40, start.Int("X"));
        Assert.Equal(300, start.Int("Y"));
        Assert.True(start.Bool("EndScript"));
        Assert.Equal("ProbeOptions", start.Field("ScriptToExe"));
        Assert.Equal("ProbeOptions", start.NavigateTo);
        Assert.Equal(4, start.Frames);
        Assert.Equal(new[] { "PR_B_Start.png" }, start.Art);

        // An int accessor refuses a field of another kind, and a missing field reads as absent.
        Assert.False(start.TryInt("ArtPath", out _));
        Assert.False(start.TryInt("NoSuchField", out _));
        Assert.Equal(7, start.Int("NoSuchField", 7));
        Assert.False(start.TryColor("X", out _));
    }

    [Fact]
    public void ColoursReadAsArgbAndTheAuthoredMacroTokenIsKept()
    {
        var layout = ProbeLayout();
        var quit = layout.Screen("ProbeMain")!.Widget("PR_B_QUIT")!;

        Assert.True(quit.TryColor("ColorRollover", out var rollover));
        Assert.Equal(new MenuLayoutColor(0xFF, 0xEE, 0xEE, 0xEE), rollover);
        Assert.Equal("<ROLLOVER>", quit.Authored("ColorRollover"));
        Assert.Null(quit.Authored("X"));
        Assert.Equal("Quit", quit.Text);
        Assert.Equal("IDS_PR_QUIT", quit.ResIdSymbol);
        Assert.Equal(101, quit.ResId);
        Assert.Equal("resource", quit.TextSource);

        Assert.Equal("0xFFBCBCBC", layout.Global("DISABLED"));
        Assert.Equal(new MenuLayoutColor(0xFF, 0xBC, 0xBC, 0xBC), layout.GlobalColor("DISABLED"));
        Assert.Null(layout.GlobalColor("NOSUCH"));
        Assert.Null(layout.Screen("ProbeMain")!.Widget("PR_T_HINT")!.Text);
    }

    [Fact]
    public void NavigationAssetsAndMissingArtAreCarriedThrough()
    {
        var layout = ProbeLayout();

        var edge = Assert.Single(layout.Navigation, e => e.From == "ProbeMain");
        Assert.Equal("PR_B_START", edge.Widget);
        Assert.Equal("ProbeOptions", edge.To);
        Assert.Equal("0x1000", edge.Priority);
        Assert.True(edge.EndScript);
        Assert.Contains(layout.ExternalAssets, a => a.Path == "assets/sounds/probeclick.wav" && a.Kind == "file" && a.Present);
        Assert.Equal(new[] { "ProbeFlag.MPG" }, layout.MissingArt);
        Assert.Equal(11, layout.WidgetTypes.Count);
    }

    [Fact]
    public void AMissingFileIsNullWithAReasonNeverAnEmptyLayout()
    {
        string empty = Path.Combine(Path.GetTempPath(), "csvm-menu-layout-empty-" + Path.GetRandomFileName());
        Directory.CreateDirectory(empty);
        try
        {
            var layout = MenuLayout.TryLoad(MenuLayout.PathUnder(empty), out var reason);

            Assert.Null(layout);
            Assert.NotNull(reason);
            Assert.Contains("menu_layout.json", reason);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public void MalformedOrForeignJsonIsRefusedWithAReason()
    {
        string dir = Path.Combine(Path.GetTempPath(), "csvm-menu-layout-bad-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "menu_layout.json");
            File.WriteAllText(path, "{ not json");
            Assert.Null(MenuLayout.TryLoad(path, out var reason));
            Assert.Contains("unreadable", reason);

            File.WriteAllText(path, "{\"schema\": 1}");
            Assert.Null(MenuLayout.TryLoad(path, out reason));
            Assert.Contains("unreadable", reason);
            Assert.Throws<InvalidDataException>(() => MenuLayout.Parse("[]"));
            Assert.ThrowsAny<JsonException>(() => MenuLayout.Parse("nope"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [ExtractedDataFact]
    public void TheInstallsOwnArtifactReadsToItsCensus()
    {
        string path = MenuLayout.PathUnder(TestData.DataRoot!);
        if (!File.Exists(path))
        {
            return;
        }

        var layout = MenuLayout.TryLoad(path, out var reason);

        Assert.Null(reason);
        Assert.NotNull(layout);
        Assert.Equal(34, layout!.Screens.Count);
        Assert.Equal(46, layout.Navigation.Count);
        var main = layout.Screen("MainMenu");
        Assert.NotNull(main);
        Assert.Equal(6, main!.Widgets.Count(w => w.TypeCode == "B"));
        Assert.Equal(4, main.Widget("MM_B_QUIT")!.Frames);
    }

    /// <summary>The probe fixture decoded and emitted by the extractor's own code, then read back.</summary>
    internal static MenuLayout ProbeLayout() => MenuLayout.Parse(MenuLayoutDecoder.ToJson(MenuLayoutDecoder.Decode(ProbeInput())));

    /// <summary>The Original shell's fixture: a main menu and a flight-check plaque row with
    /// invented geometry under the keys the shell reads.</summary>
    internal static MenuLayout OriginalLayout()
    {
        var input = new MenuLayoutInput
        {
            LayoutCsv = File.ReadAllText(TestData.Fixture("menu-layout-original", "LAYOUT.CSV"), Encoding.ASCII),
            ResourceHeader = "#define IDS_PM_CHANGEPLANE 200\n",
            Strings = new Dictionary<int, string> { [200] = "Change Plane" },
            ArchiveFiles = new List<string>
            {
                "GRAPHICS/PM_Logo.png", "GRAPHICS/PM_Frame.png", "GRAPHICS/PM_B_Campaign.png",
                "GRAPHICS/PM_B_InstantAction.png", "GRAPHICS/PM_B_MultiPlayer.png",
                "GRAPHICS/PM_B_Preferences.png", "GRAPHICS/PM_B_Credits.png", "GRAPHICS/PM_B_Quit.png",
                "GRAPHICS/PM_B_Paper.png",
            },
        };
        return MenuLayout.Parse(MenuLayoutDecoder.ToJson(MenuLayoutDecoder.Decode(input)));
    }

    private static MenuLayoutInput ProbeInput()
    {
        var input = new MenuLayoutInput
        {
            LayoutCsv = File.ReadAllText(TestData.Fixture("menu-layout", "LAYOUT.CSV"), Encoding.ASCII),
            ScrapbookCsv = File.ReadAllText(TestData.Fixture("menu-layout", "SCRAPBOOK.CSV"), Encoding.ASCII),
            ResourceHeader = File.ReadAllText(TestData.Fixture("menu-layout", "RESOURCE.H"), Encoding.ASCII),
            Strings = new Dictionary<int, string> { [100] = "Probe Title", [101] = "Quit", [102] = "Sound" },
            ArchiveFiles = new List<string>
            {
                "GRAPHICS/PR_BackGround.png", "GRAPHICS/PR_B_Start.png", "GRAPHICS/PR_B_Plaque.png",
                "GRAPHICS/PR_B_Check8States.png", "GRAPHICS/PR_B_ScrollUp.png",
                "GRAPHICS/PR_B_ScrollDown.png", "GRAPHICS/PR_B_ScrollBar.png",
                "GRAPHICS/PO_BackGround.png", "GRAPHICS/probepointer.png", "SOUNDS/probeclick.wav",
            },
            PatchMembers = new List<string> { "ASSETS/SCRIPTS/PROBEMAIN.SCRIPT" },
        };
        input.Scripts.Add(new MenuScriptSource
        {
            Name = "ProbeMain",
            Text = File.ReadAllText(TestData.Fixture("menu-layout", "PROBEMAIN.SCRIPT"), Encoding.ASCII),
        });
        return input;
    }
}

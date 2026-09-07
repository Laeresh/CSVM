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

    /// <summary>The Original shell's fixture: a main menu, a flight-check plaque row and an
    /// Instant Action section with invented geometry under the keys the shell reads.</summary>
    internal static MenuLayout OriginalLayout()
    {
        var input = new MenuLayoutInput
        {
            LayoutCsv = File.ReadAllText(TestData.Fixture("menu-layout-original", "LAYOUT.CSV"), Encoding.ASCII),
            ResourceHeader = "#define IDS_PM_CHANGEPLANE 200\n#define IDS_PI_WEAPONS 201\n#define IDS_PI_FLY 202\n"
                + "#define IDS_PI_VIEW 203\n#define IDS_PI_TABLE 204\n#define IDS_PI_TABLEINSTR 205\n"
                + "#define IDS_PI_STORYINSTR 206\n#define IDS_PI_PILOT 207\n#define IDS_PI_WINGMEN 208\n"
                + "#define IDS_PI_PLANE 209\n#define IDS_PI_MISSION 210\n#define IDS_PI_ENV 211\n"
                + "#define IDS_PI_ENEMY 212\n#define IDS_PI_CONTINUED 213\n#define IDS_PI_GOBACK 214\n"
                + "#define IDS_OK 220\n#define IDS_CANCEL 221\n#define IDS_PN_TITLE 222\n#define IDS_PN_DEFAULTLOADOUT 223\n"
                + "#define IDS_PX_B_AIRFRAME 224\n#define IDS_PX_B_ENGINE 225\n#define IDS_PX_B_ARMOR 226\n#define IDS_PX_B_GUNS 227\n"
                + "#define IDS_PX_B_HARDPOINTS 228\n#define IDS_PX_B_PAINT 229\n#define IDS_PUR_B_PURCHASE 230\n#define IDS_PS_B_SELL 231\n"
                + "#define IDS_PS_B_EXPORT 232\n#define IDS_AF_TITLE 233\n#define IDS_PX_PLANECOST_TITLE 234\n#define IDS_PX_AIRFRAME_TITLE 235\n"
                + "#define IDS_HA_TITLE 236\n#define IDS_PUR_TITLE 237\n#define IDS_PUR_TOTALS_TITLE 238\n#define IDS_PX_PLANENAME_TITLE 239\n"
                + "#define IDS_PX_WEIGHTCAPACITY_TITLE 240\n#define IDS_PX_CURRENTWEIGHT_TITLE 241\n#define IDS_PX_CASH_TITLE 242\n"
                + "#define IDS_PF_TITLE 250\n#define IDS_PF_GO_DESC 251\n#define IDS_PF_AUDIO_DESC 252\n"
                + "#define IDS_PF_VIDEO_DESC 253\n#define IDS_PF_CONTROLS_DESC 254\n"
                + "#define IDS_GO_TITLE 260\n#define IDS_GO_DIFF_TITLE 261\n#define IDS_GO_VIEW_TITLE 262\n"
                + "#define IDS_GO_HEAD_TITLE 263\n#define IDS_GO_DIFF_DESC 264\n#define IDS_GO_VIEW_DESC 265\n"
                + "#define IDS_GO_HEAD_DESC 266\n"
                + "#define IDS_VP_TITLE 270\n#define IDS_VP_SHADOWS_TITLE 271\n#define IDS_VP_SHADOWS_DESC 272\n"
                + "#define IDS_VP_EFFECTS_TITLE 273\n#define IDS_VP_EFFECTS_DESC 274\n"
                + "#define IDS_VP_VIEW_TITLE 275\n#define IDS_VP_VIEW_DESC 276\n"
                + "#define IDS_VP_DISPLAY_TITLE 277\n#define IDS_VP_DISPLAY_DESC 278\n",
            Strings = new Dictionary<int, string>
            {
                [100] = "OK",
                [102] = "Yes",
                [103] = "No",
                [220] = "OK",
                [221] = "Cancel",
                [250] = "PREFERENCES",
                [251] = "Change the difficulty level and default views.",
                [252] = "Change the audio settings.",
                [253] = "Change the video settings.",
                [254] = "Configure the joystick, keyboard, and mouse.",
                [260] = "GAME OPTIONS",
                [261] = "Difficulty",
                [262] = "Default View",
                [263] = "Auto Head Turn",
                [264] = "Select the difficulty level for a solo campaign.",
                [265] = "Select your default view.",
                [266] = "Select to turn your head automatically as your aircraft turns.",
                [270] = "VIDEO",
                [271] = "Shadows",
                [272] = "Select to see shadows.",
                [273] = "Effects Level",
                [274] = "Select the detail level of visual effects, such as explosions and smoke.",
                [275] = "Viewing Range",
                [276] = "Select how far you can see in the game.",
                [277] = "Resolution",
                [278] = "Select the screen resolution.",
                [222] = "PLANE NAME",
                [223] = "Load Default Configuration",
                [224] = "Airframe",
                [225] = "Engine",
                [226] = "Armor",
                [227] = "Guns",
                [228] = "Hardpoints",
                [229] = "Paint",
                [230] = "Purchase Now",
                [231] = "Sell",
                [232] = "Export",
                [233] = "1) SELECT AN AIRFRAME",
                [234] = "PLANE COST:  $%1!d!",
                [235] = "AIRFRAME: %1!s!",
                [236] = "INVENTORY",
                [237] = "7) CONSTRUCTION TOTALS",
                [238] = "Totals",
                [239] = "PLANE NAME:",
                [240] = "[CSB11I]WEIGHT CAPACITY:\n %1!d! lbs.",
                [241] = "[CSB11I]CURRENT WEIGHT:\n %1!d! lbs.",
                [242] = "$$$ on Hand:",
                [200] = "Change Plane",
                [201] = "Weapons",
                [202] = "Fly",
                [203] = "View",
                [204] = "Contents",
                [205] = "Pick a mission below.",
                [206] = "Set the details below.",
                [207] = "Pilot",
                [208] = "Wingmen",
                [209] = "Plane:",
                [210] = "Mission:",
                [211] = "Environment:",
                [212] = "Enemy:",
                [213] = "[more ...]",
                [214] = "[back ...]",
            },
            ArchiveFiles = new List<string>
            {
                "GRAPHICS/PM_Logo.png", "GRAPHICS/PM_Frame.png", "GRAPHICS/PM_B_Campaign.png",
                "GRAPHICS/PM_B_InstantAction.png", "GRAPHICS/PM_B_MultiPlayer.png",
                "GRAPHICS/PM_B_Preferences.png", "GRAPHICS/PM_B_Credits.png", "GRAPHICS/PM_B_Quit.png",
                "GRAPHICS/PM_B_Paper.png", "GRAPHICS/PI_IA_Back.jpg", "GRAPHICS/PI_B_Exit.png",
                "GRAPHICS/PI_B_Build.png", "GRAPHICS/PI_B_Paper.png", "GRAPHICS/PI_B_Radio.png",
                "GRAPHICS/PI_B_Up.png", "GRAPHICS/PI_B_Down.png", "GRAPHICS/PI_B_ScrollBar.png",
                "GRAPHICS/PI_B_ScrollUp.png", "GRAPHICS/PI_B_ScrollDown.png",
                "GRAPHICS/PH_Back.jpg", "GRAPHICS/PH_NamePanel.png", "GRAPHICS/PH_B_OkCancel.png", "GRAPHICS/PH_B_Check8.png",
                "GRAPHICS/PH_Bar.png", "GRAPHICS/PH_B_Cancel.png", "GRAPHICS/PH_B_Ready.png", "GRAPHICS/PH_B_Sell.png",
                "GRAPHICS/PH_Tab.png", "GRAPHICS/PH_B_ScrollBar.png", "GRAPHICS/PH_B_ScrollUp.png", "GRAPHICS/PH_B_ScrollDown.png",
                "GRAPHICS/PH_B_DropUp.png", "GRAPHICS/PH_B_DropDown.png", "GRAPHICS/PH_Rule.png", "GRAPHICS/PH_Decals.tga",
                "GRAPHICS/PH_B_Paper.png", "GRAPHICS/PH_Inventory.jpg", "GRAPHICS/PH_PlaneIcons.png", "GRAPHICS/PH_B_Done.png",
                "GRAPHICS/PP_Back.png", "GRAPHICS/PP_B_GameOptions.png", "GRAPHICS/PP_B_Audio.png", "GRAPHICS/PP_B_Video.png",
                "GRAPHICS/PP_B_Controls.png", "GRAPHICS/PP_B_ReturnMainMenu.png", "GRAPHICS/PP_B_ReturnToGame.png",
                "GRAPHICS/PP_GoBack.png", "GRAPHICS/PP_VpBack.png", "GRAPHICS/PP_B_Accept.png", "GRAPHICS/PP_B_Cancel.png",
                "GRAPHICS/PP_B_Check8.png", "GRAPHICS/PP_B_ScrollBar.png", "GRAPHICS/PP_B_ScrollUp.png",
                "GRAPHICS/PP_B_ScrollDown.png", "GRAPHICS/PP_B_DropUp.png", "GRAPHICS/PP_B_DropDown.png",
                "GRAPHICS/PC_Back.jpg", "GRAPHICS/PC_B_Done.png",
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

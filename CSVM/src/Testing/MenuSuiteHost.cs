using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
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
    /// has to be joined through it.</summary>
    internal static void AddFeatures(
        MenuHost host, string dataRoot, Spec.GameData data, Action<int, BindingProfile>? saveBindings = null,
        ChapterCinema? chapterCinema = null, ClosingCinema? closingCinema = null,
        NetPlayFeature? netDoor = null)
    {
        host.Features.Add(new FreeFlightFeature());
        host.Features.Add(InstantActionFeature.ForGameData(data));
        host.Features.Add(new PlayerSetupFeature());
        // The hangar reads its labels and stock defaults as the launcher wires them: the langui
        // table, the stock fits and the root reader scope.
        var strings = UiStrings.TryLoad(dataRoot) ?? UiStrings.Empty;
        host.Features.Add(new HangarFeature(strings, StockAirframes.Node, () => StockLoadouts.Load(), data.Zrdr));
        host.Features.Add(new CampaignFeature(
            strings, StockAirframes.Node, chapterCinema, closingCinema)
        { Data = data });
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

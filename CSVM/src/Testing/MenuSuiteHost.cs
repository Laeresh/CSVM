using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;

namespace CSVM.Testing;

/// <summary>The menu host a suite stands a <see cref="LaunchMenu"/> on: the Free Flight, Instant
/// Action, player-setup, hangar and campaign features, one Built-in seat over a real poller (which reads nothing in
/// a scripted run), a silent audio service, and a sink that records every exit. The suites that
/// drive the launchscreen directly build through <see cref="Menu"/>; the host tracers register
/// the presentation itself.</summary>
internal static class MenuSuiteHost
{
    /// <summary>A bare host with no presentation registered, exits landing in <paramref name="exits"/>,
    /// the Instant Action feature reading environment defs under <paramref name="dataRoot"/>.</summary>
    internal static MenuHost Bare(List<MenuExit> exits, string dataRoot, out BuiltInSeat seat)
    {
        var host = new MenuHost(new PresentationRegistry(), new SilentMenuAudio(), exits.Add);
        AddFeatures(host, dataRoot);
        seat = new BuiltInSeat(new MenuInput { Keyboard = true });
        host.AddSeat(seat);
        return host;
    }

    /// <summary>Registers the shared features a launchscreen needs, as the launcher does. Before
    /// the seat: the host lends the setup feature's seat list once the feature is in, so seat 0
    /// has to be joined through it.</summary>
    internal static void AddFeatures(MenuHost host, string dataRoot)
    {
        host.Features.Add(new FreeFlightFeature());
        host.Features.Add(InstantActionFeature.ForDataRoot(dataRoot));
        host.Features.Add(new PlayerSetupFeature());
        // The hangar reads its labels and stock defaults the way the launcher wires them: the langui
        // table, the stock fits on first need and the zrdr scope the data root carries.
        string zrdr = SessionPaths.PreferUnzipped(System.IO.Path.Combine(dataRoot, "extracted", "zrdr.zip"));
        var strings = UiStrings.TryLoad(dataRoot) ?? UiStrings.Empty;
        host.Features.Add(new HangarFeature(strings, PlanePickerRoster.AirframeNode, () => StockLoadouts.Load(), zrdr));
        host.Features.Add(new CampaignFeature(strings, PlanePickerRoster.AirframeNode));
        // No save: a suite must never write over the keymap saved at this machine's controls.
        host.Features.Add(new ControlsFeature());
    }

    /// <summary>A launchscreen over a bare host, for a suite that drives the screens and reads
    /// nothing back from the host.</summary>
    internal static LaunchMenu Menu(TestContext ctx)
    {
        var host = Bare(new List<MenuExit>(), ctx.DataRoot, out var seat);
        return LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
    }

    /// <summary>An audio service that plays nothing and records nothing: a scripted run has no
    /// sound to hear, and the launchscreen tolerates a silent install already.</summary>
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
    }
}

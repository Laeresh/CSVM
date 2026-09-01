using System.Collections.Generic;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;

namespace CSVM.Testing;

/// <summary>The menu host a suite stands a <see cref="LaunchMenu"/> on: the Free Flight and
/// player-setup features, one Built-in seat over a real poller (which reads nothing in a scripted run), a silent audio
/// service, and a sink that records every exit. The suites that drive the launchscreen directly
/// build through <see cref="Menu"/>; the host tracer registers the presentation itself.</summary>
internal static class MenuSuiteHost
{
    /// <summary>A bare host with no presentation registered, exits landing in <paramref name="exits"/>.</summary>
    internal static MenuHost Bare(List<MenuExit> exits, out BuiltInSeat seat)
    {
        var host = new MenuHost(new PresentationRegistry(), new SilentMenuAudio(), exits.Add);
        host.Features.Add(new FreeFlightFeature());
        host.Features.Add(new PlayerSetupFeature());
        seat = new BuiltInSeat(new MenuInput { Keyboard = true });
        host.AddSeat(seat);
        return host;
    }

    /// <summary>A launchscreen over a bare host, for a suite that drives the screens and reads
    /// nothing back from the host.</summary>
    internal static LaunchMenu Menu(TestContext ctx)
    {
        var host = Bare(new List<MenuExit>(), out var seat);
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

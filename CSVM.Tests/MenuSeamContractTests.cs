using System;
using CSVM;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The menu presentation seam off-engine: two presentations with different screen graphs drive
/// the same shared feature to the same typed exit, a switch discards transient state and starts
/// the next presentation at its top level, return destinations stay semantic, and cue requests
/// reach the shared audio service.
/// </summary>
public class MenuSeamContractTests
{
    [Fact]
    public void TwoDifferentScreenGraphsDriveTheSameOperationToOneExit()
    {
        var wizardHost = HostWithFeature(out var wizardSeat);
        var wizard = new FakeWizardPresentation();
        wizard.Activate(wizardHost, MenuReturnDestination.TopLevel);
        wizardSeat.Enqueue(new MenuCommands { MoveY = 1 });
        wizardSeat.Enqueue(new MenuCommands { Accept = true });
        wizardSeat.Enqueue(new MenuCommands { MoveY = 1 });
        wizardSeat.Enqueue(new MenuCommands { Accept = true });
        wizardSeat.Enqueue(new MenuCommands { Accept = true });
        for (int i = 0; i < 5; i++)
        {
            wizard.Tick(1f / 60f);
        }

        var pageHost = HostWithFeature(out var pageSeat);
        var page = new FakePointerPresentation();
        page.Activate(pageHost, MenuReturnDestination.TopLevel);
        pageSeat.Enqueue(Click(500f, 100f));
        pageSeat.Enqueue(Click(500f, 300f));
        pageSeat.Enqueue(Click(100f, 500f));
        for (int i = 0; i < 3; i++)
        {
            page.Tick(1f / 60f);
        }

        var fromWizard = Assert.IsType<LaunchExit>(Assert.Single(wizardHost.Exits));
        var fromPage = Assert.IsType<LaunchExit>(Assert.Single(pageHost.Exits));
        Assert.Equal("C2", fromWizard.Chapter);
        Assert.Equal(fromWizard.Chapter, fromPage.Chapter);
        Assert.Equal("player_fury", fromWizard.Seats[0].PlaneNode);
        Assert.Equal(fromWizard.Seats[0].PlaneNode, fromPage.Seats[0].PlaneNode);
        Assert.Equal(MenuMode.Free, fromWizard.Mode);
        Assert.Equal(fromWizard.Mode, fromPage.Mode);
        Assert.Null(fromWizard.InstantAction);
        Assert.Null(fromPage.InstantAction);
    }

    [Fact]
    public void AReturnDestinationLandsOnEachPresentationsOwnScreen()
    {
        var debrief = new DebriefReturn("Nathan", 3);

        var wizardHost = HostWithFeature(out _);
        var wizard = new FakeWizardPresentation();
        wizard.Activate(wizardHost, debrief);

        var pageHost = HostWithFeature(out _);
        var page = new FakePointerPresentation();
        page.Activate(pageHost, debrief);

        Assert.Equal("wizard-debrief", wizard.Screen);
        Assert.Equal("page-results", page.Screen);
        Assert.NotEqual(wizard.Screen, page.Screen);
    }

    [Fact]
    public void SwitchingDiscardsTransientStateAndStartsAtTheTopLevel()
    {
        var host = HostWithFeature(out var seat);
        var registry = new PresentationRegistry();
        registry.Register(new PresentationId("fake-wizard"), () => new FakeWizardPresentation());
        registry.Register(new PresentationId("fake-pointer"), () => new FakePointerPresentation());

        Assert.True(registry.TryCreate(new PresentationId("fake-wizard"), out var active));
        active!.Activate(host, MenuReturnDestination.TopLevel);
        seat.Enqueue(new MenuCommands { Accept = true });
        active.Tick(1f / 60f);
        var feature = host.Features.Get<FakeSortieFeature>();
        Assert.Equal("C1", feature.Chapter);

        active.Deactivate();
        host.Features.DiscardTransient();
        Assert.True(registry.TryCreate(new PresentationId("fake-pointer"), out var next));
        next!.Activate(host, MenuReturnDestination.TopLevel);

        Assert.Null(feature.Chapter);
        Assert.Equal(1, feature.Discards);
        Assert.Equal("page", ((FakePointerPresentation)next).Screen);
        Assert.Equal(string.Empty, ((FakeWizardPresentation)active).Screen);
    }

    [Fact]
    public void TheRegistryHandsOutFreshInstancesAndRefusesRivals()
    {
        var registry = new PresentationRegistry();
        var id = new PresentationId("fake-wizard");
        registry.Register(id, () => new FakeWizardPresentation());

        Assert.True(registry.IsRegistered(id));
        Assert.False(registry.IsRegistered(new PresentationId("unknown")));
        Assert.True(registry.TryCreate(id, out var first));
        Assert.True(registry.TryCreate(id, out var second));
        Assert.NotSame(first, second);
        Assert.False(registry.TryCreate(new PresentationId("unknown"), out _));
        Assert.Throws<InvalidOperationException>(
            () => registry.Register(id, () => new FakeWizardPresentation()));
    }

    [Fact]
    public void CueAndNarrationRequestsReachTheSharedService()
    {
        var host = HostWithFeature(out var seat);
        var wizard = new FakeWizardPresentation();
        wizard.Activate(host, MenuReturnDestination.TopLevel);
        seat.Enqueue(new MenuCommands { MoveY = 1 });
        wizard.Tick(1f / 60f);

        Assert.Equal(new[] { "wizard-step" }, host.Recording.Cues);

        host.Audio.BeginNarration("brief01.wav");
        Assert.Equal("brief01.wav", host.Recording.Narration);
        host.Audio.EndNarration();
        Assert.Null(host.Recording.Narration);
    }

    [Fact]
    public void BackingOutOfTheTopLevelLeavesThroughTheTypedExit()
    {
        var host = HostWithFeature(out var seat);
        var wizard = new FakeWizardPresentation();
        wizard.Activate(host, MenuReturnDestination.TopLevel);
        seat.Enqueue(new MenuCommands { Back = true });
        wizard.Tick(1f / 60f);

        Assert.IsType<QuitExit>(Assert.Single(host.Exits));
    }

    [Fact]
    public void TheFeatureSetFetchesByTypeAndRefusesRivals()
    {
        var features = new MenuFeatureSet();
        var feature = new FakeSortieFeature();
        features.Add(feature);

        Assert.Same(feature, features.Get<FakeSortieFeature>());
        Assert.True(features.TryGet<FakeSortieFeature>(out var found));
        Assert.Same(feature, found);
        Assert.Throws<InvalidOperationException>(() => features.Add(new FakeSortieFeature()));

        var empty = new MenuFeatureSet();
        Assert.False(empty.TryGet<FakeSortieFeature>(out _));
        Assert.Throws<InvalidOperationException>(() => empty.Get<FakeSortieFeature>());
    }

    [Fact]
    public void APresentationIdIsANonEmptyValueToken()
    {
        Assert.Throws<ArgumentException>(() => new PresentationId(string.Empty));
        Assert.Throws<ArgumentException>(() => new PresentationId("   "));
        Assert.Equal(new PresentationId("built-in"), PresentationId.BuiltIn);
        Assert.NotEqual(PresentationId.BuiltIn, PresentationId.Original);
        Assert.Equal("original", PresentationId.Original.ToString());
    }

    private static FakeMenuHost HostWithFeature(out ScriptedMenuSeat seat)
    {
        var host = new FakeMenuHost();
        host.Features.Add(new FakeSortieFeature());
        seat = new ScriptedMenuSeat();
        host.SeatList.Add(seat);
        return host;
    }

    private static MenuCommands Click(float x, float y) =>
        new() { Pointer = new MenuPointer(x, y, Pressed: true, Clicked: true) };
}

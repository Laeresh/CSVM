using System;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The process-lifetime menu host off-engine: selection through the resolution rule over the
/// registry, a show that activates one fresh instance and re-shows the same one after an exit,
/// an exit that hides the presentation and reaches the sink, ticks that stop while hidden, and a
/// deactivation that discards transient feature state.
/// </summary>
public class MenuHostTests
{
    [Fact]
    public void AnUnknownRequestFallsBackToBuiltInWithAReasonAndKeepsTheRequest()
    {
        var host = Host(out _, out _);

        string? reason = host.Select(forceBuiltIn: false, cliOverride: null, savedRequest: "no-such-presentation");

        Assert.Equal(PresentationId.BuiltIn, host.Selected);
        Assert.Equal(new PresentationId("no-such-presentation"), host.Requested);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ARegisteredRequestIsSelectedAndTheForceFlagOverridesIt()
    {
        var host = Host(out _, out _);
        var wizard = new PresentationId("fake-wizard");

        Assert.Null(host.Select(forceBuiltIn: false, cliOverride: "fake-wizard", savedRequest: null));
        Assert.Equal(wizard, host.Selected);

        Assert.NotNull(host.Select(forceBuiltIn: true, cliOverride: "fake-wizard", savedRequest: null));
        Assert.Equal(PresentationId.BuiltIn, host.Selected);
        Assert.Equal(wizard, host.Requested);
    }

    /// <summary>Availability beyond registration: a registered presentation whose assets are
    /// missing falls back with the availability reason appended, and the request is kept.</summary>
    [Fact]
    public void ARegisteredButUnavailableRequestFallsBackWithTheAvailabilityReasonAndKeepsTheRequest()
    {
        var host = Host(out _, out _);
        host.Availability = id => id.Value == "fake-wizard" ? "its layout is missing" : null;

        string? reason = host.Select(forceBuiltIn: false, cliOverride: null, savedRequest: "fake-wizard");

        Assert.Equal(PresentationId.BuiltIn, host.Selected);
        Assert.Equal(new PresentationId("fake-wizard"), host.Requested);
        Assert.NotNull(reason);
        Assert.Contains("its layout is missing", reason);

        host.Availability = _ => null;
        Assert.Null(host.Select(forceBuiltIn: false, cliOverride: null, savedRequest: "fake-wizard"));
        Assert.Equal(new PresentationId("fake-wizard"), host.Selected);
    }

    /// <summary>A blank token cannot be a <see cref="PresentationId"/>, so it must read as an
    /// unavailable request rather than throw at a persisted or typed value.</summary>
    [Fact]
    public void AWhitespaceRequestNeverCrashesTheSelection()
    {
        var host = Host(out _, out _);

        Assert.NotNull(host.Select(forceBuiltIn: false, cliOverride: "   ", savedRequest: null));
        Assert.Equal(PresentationId.BuiltIn, host.Selected);
        Assert.Equal(PresentationId.BuiltIn, host.Requested);
    }

    [Fact]
    public void ShowActivatesOneInstanceAndReshowsItAfterAnExit()
    {
        var host = Host(out var seat, out var exits);
        host.Select(forceBuiltIn: false, cliOverride: "fake-wizard", savedRequest: null);

        host.Show(MenuReturnDestination.TopLevel);
        var first = Assert.IsType<FakeWizardPresentation>(host.Active);
        Assert.True(host.Shown);
        Assert.Equal("wizard-chapter", first.Screen);

        seat.Enqueue(new MenuCommands { Back = true });
        host.Tick(1f / 60f);
        Assert.IsType<QuitExit>(Assert.Single(exits));
        Assert.False(host.Shown);
        Assert.Equal(1, first.Hides);

        seat.Enqueue(new MenuCommands { Back = true });
        host.Tick(1f / 60f);
        Assert.Single(exits);

        host.Show(new DebriefReturn("Nathan", 3, MissionWon: true));
        Assert.Same(first, host.Active);
        Assert.True(host.Shown);
        Assert.Equal("wizard-debrief", first.Screen);
    }

    [Fact]
    public void DeactivateEndsThePresentationAndDiscardsTransientFeatureState()
    {
        var host = Host(out var seat, out _);
        host.Select(forceBuiltIn: false, cliOverride: "fake-wizard", savedRequest: null);
        host.Show(MenuReturnDestination.TopLevel);
        seat.Enqueue(new MenuCommands { Accept = true });
        host.Tick(1f / 60f);
        var feature = host.Features.Get<FakeSortieFeature>();
        Assert.Equal("C1", feature.Chapter);

        host.Deactivate();

        Assert.Null(host.Active);
        Assert.False(host.Shown);
        Assert.Null(feature.Chapter);
        Assert.Equal(1, feature.Discards);

        host.Show(MenuReturnDestination.TopLevel);
        Assert.IsType<FakeWizardPresentation>(host.Active);
        Assert.Equal("wizard-chapter", ((FakeWizardPresentation)host.Active!).Screen);
    }

    [Fact]
    public void SeatsAreLiveAndTheConstructorRefusesNulls()
    {
        var host = Host(out var seat, out _);
        Assert.Same(seat, Assert.Single(host.Seats));
        host.RemoveSeat(seat);
        Assert.Empty(host.Seats);

        Assert.Throws<ArgumentNullException>(() => new MenuHost(null!, new RecordingAudio(), _ => { }));
        Assert.Throws<ArgumentNullException>(() => new MenuHost(new PresentationRegistry(), null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new MenuHost(new PresentationRegistry(), new RecordingAudio(), null!));
    }

    [Fact]
    public void AnUnregisteredBuiltInIsAWiringError()
    {
        var host = new MenuHost(new PresentationRegistry(), new RecordingAudio(), _ => { });

        Assert.Throws<InvalidOperationException>(() => host.Select(false, null, null));
        Assert.Throws<InvalidOperationException>(() => host.Show(MenuReturnDestination.TopLevel));
    }

    // A host with Built-in standing in as the pointer page and the wizard registered beside it,
    // one scripted seat, the shared fake feature and a recording sink.
    private static MenuHost Host(out ScriptedMenuSeat seat, out System.Collections.Generic.List<MenuExit> exits)
    {
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new FakePointerPresentation());
        registry.Register(new PresentationId("fake-wizard"), () => new FakeWizardPresentation());
        var sink = new System.Collections.Generic.List<MenuExit>();
        var host = new MenuHost(registry, new RecordingAudio(), sink.Add);
        host.Features.Add(new FakeSortieFeature());
        var scripted = new ScriptedMenuSeat();
        host.AddSeat(scripted);
        seat = scripted;
        exits = sink;
        return host;
    }
}

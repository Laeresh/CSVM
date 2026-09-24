using System;
using System.Collections.Generic;
using CSVM.Utils;

namespace CSVM.UI.Menu;

/// <summary>
/// The process-lifetime menu host: owns the shared features, the audio service, the seats and the
/// exit sink, chooses the active presentation through <see cref="PresentationResolution"/> over
/// the registry, and runs one presentation at a time. <c>Launcher</c> constructs it once, shows it
/// with a semantic destination on every entry (cold start, return from flight, debrief), ticks it
/// every frame while shown, and receives every exit through the sink. A flight is not a switch:
/// the active presentation is hidden across it and re-shown on return, keeping its screens.
/// </summary>
public sealed class MenuHost : IMenuHost
{
    private readonly PresentationRegistry _registry;
    private readonly Action<MenuExit> _exitSink;
    private readonly List<IMenuInputSource> _seats = new();

    /// <summary>A host over <paramref name="registry"/>, cueing through <paramref name="audio"/>
    /// and handing every exit to <paramref name="exitSink"/>. Features are added by the owner.</summary>
    public MenuHost(PresentationRegistry registry, IMenuAudio audio, Action<MenuExit> exitSink)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Audio = audio ?? throw new ArgumentNullException(nameof(audio));
        _exitSink = exitSink ?? throw new ArgumentNullException(nameof(exitSink));
    }

    public MenuFeatureSet Features { get; } = new();

    public IMenuAudio Audio { get; }

    /// <summary>The seats: the player-setup feature's sources when that feature is registered,
    /// so a join anywhere grows this list; the host's own list otherwise.</summary>
    public IReadOnlyList<IMenuInputSource> Seats =>
        Features.TryGet<PlayerSetupFeature>(out var setup) ? setup.Sources : _seats;

    /// <summary>The presentation <see cref="Show"/> activates, settled by <see cref="Select"/>;
    /// Built-in until then.</summary>
    public PresentationId Selected { get; private set; } = PresentationId.BuiltIn;

    /// <summary>What was asked for before availability, the value the startup log reports.</summary>
    public PresentationId Requested { get; private set; } = PresentationId.BuiltIn;

    /// <summary>The live presentation, or null before the first <see cref="Show"/> and after
    /// <see cref="Deactivate"/>. Hidden between an exit and the next show, not null.</summary>
    public IMenuPresentation? Active { get; private set; }

    /// <summary>Whether the presentation is on screen: true from <see cref="Show"/> until
    /// <see cref="Exit"/>. What the owner reads instead of any presentation's own node.</summary>
    public bool Shown { get; private set; }

    /// <summary>The availability answer beyond registration: why a registered presentation cannot
    /// run this time (its assets are missing), or null when it can. Built-in is never asked. The
    /// default says every registered presentation is available.</summary>
    public Func<PresentationId, string?> Availability { get; set; } = _ => null;

    /// <summary>Adds a seat's input source, joining it through the player-setup feature when one
    /// is registered. Seat 0 first; the list is live for presentations. A refused join (every seat
    /// taken, the source already seated) is a wiring error and throws.</summary>
    public void AddSeat(IMenuInputSource seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        if (Features.TryGet<PlayerSetupFeature>(out var setup))
        {
            if (setup.Join(seat) == null)
            {
                throw new InvalidOperationException("The player setup refused the seat: every seat is taken or the source is already seated.");
            }

            return;
        }

        _seats.Add(seat);
    }

    /// <summary>Removes a seat, the un-join. Removing a seat the list lacks is a no-op.</summary>
    public void RemoveSeat(IMenuInputSource seat)
    {
        if (Features.TryGet<PlayerSetupFeature>(out var setup))
        {
            if (setup.SeatOf(seat) is { } joined)
            {
                setup.Unjoin(joined);
            }

            return;
        }

        _seats.Remove(seat);
    }

    /// <summary>Settles <see cref="Selected"/> from the force flag and the CLI override,
    /// availability being registration plus <see cref="Availability"/>; returns the reason when the
    /// selection differs from the request, else null. An unknown or unavailable request falls back
    /// to Built-in rather than throwing at a command-line word; Built-in itself being unregistered
    /// is a wiring error.</summary>
    public string? Select(bool forceBuiltIn, string? cliOverride)
    {
        string? unavailable = null;
        var (active, reason) = PresentationResolution.Resolve(
            forceBuiltIn, cliOverride,
            id => IsRegistered(id) && (unavailable = Availability(new PresentationId(id))) == null);
        if (reason != null && unavailable != null)
        {
            reason = $"{reason}: {unavailable}";
        }

        var selected = new PresentationId(active);
        if (!_registry.IsRegistered(selected))
        {
            throw new InvalidOperationException($"Presentation '{selected}' resolved but is not registered.");
        }

        Selected = selected;
        string requested = PresentationResolution.Requested(cliOverride);
        Requested = string.IsNullOrWhiteSpace(requested) ? PresentationId.BuiltIn : new PresentationId(requested);
        return reason;
    }

    /// <summary>Puts the menu on screen at <paramref name="destination"/>: a fresh instance of
    /// <see cref="Selected"/> on the first show, the same instance again after an exit.</summary>
    public void Show(MenuReturnDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (Active == null)
        {
            if (!_registry.TryCreate(Selected, out var created))
            {
                throw new InvalidOperationException($"Presentation '{Selected}' is not registered.");
            }

            Active = created;
        }

        Active.Activate(this, destination);
        Shown = true;
    }

    /// <summary>One frame of the shown presentation; nothing while hidden.</summary>
    public void Tick(float dt)
    {
        if (Shown)
        {
            Active?.Tick(dt);
        }
    }

    public void Exit(MenuExit exit)
    {
        ArgumentNullException.ThrowIfNull(exit);
        Shown = false;
        Active?.Hide();
        _exitSink(exit);
    }

    /// <summary>Ends the active presentation for good and discards every feature's transient
    /// state: the first half of a switch, after which <see cref="Show"/> starts the selected
    /// presentation fresh at its top level.</summary>
    public void Deactivate()
    {
        Shown = false;
        Active?.Deactivate();
        Active = null;
        Features.DiscardTransient();
    }

    private bool IsRegistered(string id) =>
        !string.IsNullOrWhiteSpace(id) && _registry.IsRegistered(new PresentationId(id));
}

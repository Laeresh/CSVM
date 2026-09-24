using System;
using System.Collections.Generic;
using CSVM;
using CSVM.Bindings;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="PadRumble"/>: the effect table as the original authors it, and the bands that pick a
/// row. The routing keeps one pane's rumble on its own pilot's pad. Engine-free, because the
/// one call that needs an engine sits behind <see cref="IRumbleSink"/> and a recording stub stands
/// in for it here. Every magnitude asserted below is the original's own effect strength over 10000,
/// or the gain its code writes over it. Where each came from is in docs/org/input.md.
/// </summary>
public class PadRumbleTests : IDisposable
{
    private readonly bool _wasEnabled = PadRumble.Enabled;
    private readonly IRumbleSink _wasSink = PadRumble.Sink;
    private readonly bool _wasDisabled = Pads.Disabled;
    private readonly bool _wasFocused = Pads.Focused;
    private readonly Recorder _sink = new();

    public PadRumbleTests()
    {
        PadRumble.Enabled = true;
        PadRumble.Sink = _sink;
        Pads.Disabled = false;
        Pads.Focused = true;
    }

    /// <summary>Restores the statics, since the toggle, the sink and the pad gate are all
    /// process-wide.</summary>
    public void Dispose()
    {
        PadRumble.Enabled = _wasEnabled;
        PadRumble.Sink = _wasSink;
        Pads.Disabled = _wasDisabled;
        Pads.Focused = _wasFocused;
        GC.SuppressFinalize(this);
    }

    /// <summary>The three gun-fire effects on the original's own calibre edges. Each carries its
    /// authored magnitude on the weak motor. The original's timer stops it after 0.3 s.
    /// </summary>
    [Fact]
    public void GunFireSplitsOnCalibreAndBuzzesTheWeakMotor()
    {
        Assert.Equal(RumbleEvent.GunFireSmall, PadRumble.GunFire(49f));
        Assert.Equal(RumbleEvent.GunFireMedium, PadRumble.GunFire(50f));
        Assert.Equal(RumbleEvent.GunFireMedium, PadRumble.GunFire(69f));
        Assert.Equal(RumbleEvent.GunFireLarge, PadRumble.GunFire(70f));
        Assert.Equal(new RumbleShape(0.44f, 0f, 0.30f), PadRumble.Shape(RumbleEvent.GunFireSmall));
        Assert.Equal(new RumbleShape(0.56f, 0f, 0.30f), PadRumble.Shape(RumbleEvent.GunFireMedium));
        Assert.Equal(new RumbleShape(0.95f, 0f, 0.30f), PadRumble.Shape(RumbleEvent.GunFireLarge));
    }

    /// <summary>The three launch effects, each a push on the strong motor. A torpedo is the biggest
    /// and the longest, a rearward mount the smallest. Everything else sits between them.
    /// </summary>
    [Fact]
    public void ALaunchTakesTheTorpedoRowFirstAndTheRearRowNext()
    {
        Assert.Equal(RumbleEvent.TorpedoLaunch, PadRumble.Launch(torpedo: true, rear: true));
        Assert.Equal(RumbleEvent.RearLaunch, PadRumble.Launch(torpedo: false, rear: true));
        Assert.Equal(RumbleEvent.OrdnanceLaunch, PadRumble.Launch(torpedo: false, rear: false));
        Assert.Equal(new RumbleShape(0f, 0.94f, 0.50f), PadRumble.Shape(RumbleEvent.TorpedoLaunch));
        Assert.Equal(new RumbleShape(0f, 0.55f, 0.25f), PadRumble.Shape(RumbleEvent.RearLaunch));
        Assert.Equal(new RumbleShape(0f, 0.75f, 0.25f), PadRumble.Shape(RumbleEvent.OrdnanceLaunch));
    }

    /// <summary>The three damage edges, each the original's own number: 6 on a gun round, 100 on
    /// anything else, 50.5 on a contact. A contact takes the larger of the damage pair.</summary>
    [Fact]
    public void TheDamageEdgesPickTheHeavyRow()
    {
        Assert.Equal(RumbleEvent.CannonHitLight, PadRumble.CannonHit(5.9f));
        Assert.Equal(RumbleEvent.CannonHitHeavy, PadRumble.CannonHit(6f));
        Assert.Equal(RumbleEvent.OrdnanceHitLight, PadRumble.OrdnanceHit(99f));
        Assert.Equal(RumbleEvent.OrdnanceHitHeavy, PadRumble.OrdnanceHit(100f));
        Assert.Equal(RumbleEvent.ContactLight, PadRumble.Contact(50f, 12f));
        Assert.Equal(RumbleEvent.ContactHeavy, PadRumble.Contact(12f, 51f));
        Assert.Equal(new RumbleShape(0f, 0.75f, 0.10f), PadRumble.Shape(RumbleEvent.CannonHitLight));
        Assert.Equal(new RumbleShape(0.80f, 0.80f, 0.50f), PadRumble.Shape(RumbleEvent.OrdnanceHitLight));
    }

    /// <summary>A contact splits on duration rather than on strength. The original writes full gain
    /// into both effects, and the heavy one is twice as long.</summary>
    [Fact]
    public void BothContactRowsRunFullAndDifferOnlyInLength()
    {
        Assert.Equal(new RumbleShape(1f, 1f, 0.50f), PadRumble.Shape(RumbleEvent.ContactLight));
        Assert.Equal(new RumbleShape(1f, 1f, 1.00f), PadRumble.Shape(RumbleEvent.ContactHeavy));
    }

    /// <summary>The nitro jolt and the two sustained rattles, whose seconds are the hold-over the
    /// original stops an infinite effect after rather than an authored duration.</summary>
    [Fact]
    public void TheNitroJoltAndTheSustainedRowsCarryTheirAuthoredStrength()
    {
        Assert.Equal(new RumbleShape(0f, 1f, 1.00f), PadRumble.Shape(RumbleEvent.NitroStart));
        Assert.Equal(new RumbleShape(0.45f, 0f, 0.20f), PadRumble.Shape(RumbleEvent.Overspeed));
        Assert.Equal(new RumbleShape(0.35f, 0f, 0.20f), PadRumble.Shape(RumbleEvent.TurretFire));
    }

    /// <summary>Every event has a row. The table is indexed by the enum, so a value added without a
    /// row throws here, not at the controls.</summary>
    [Fact]
    public void EveryEventHasARow()
    {
        foreach (var ev in Enum.GetValues<RumbleEvent>())
        {
            var shape = PadRumble.Shape(ev);
            Assert.InRange(shape.Weak, 0f, 1f);
            Assert.InRange(shape.Strong, 0f, 1f);
            Assert.True(shape.Seconds > 0f);
            Assert.True(shape.Weak > 0f || shape.Strong > 0f);
        }
    }

    /// <summary>⚠ The routing rule: each seat rumbles the pads its own bindings read, so a two-pane
    /// sortie never buzzes the other pilot's controller. A seat holding no pad produces nothing at
    /// all, which is what an AI rig and an unjoined splitscreen seat both hold.</summary>
    [Fact]
    public void EachSeatRumblesItsOwnPadAlone()
    {
        int[] one = { 0 };
        int[] two = { 1 };
        var p1 = OnPad(() => one);
        var p2 = OnPad(() => two);
        p1.Play(RumbleEvent.NitroStart);
        p2.Play(RumbleEvent.GunFireLarge);
        Assert.Equal(new[] { 0, 1 }, _sink.Devices);
        Assert.Equal(new RumbleShape(0f, 1f, 1.00f), _sink.Shapes[0]);
        Assert.Equal(new RumbleShape(0.95f, 0f, 0.30f), _sink.Shapes[1]);

        _sink.Clear();
        OnPad(Array.Empty<int>).Play(RumbleEvent.ContactHeavy);
        Assert.Empty(_sink.Devices);
    }

    /// <summary>A seat holding two pads rumbles both, the same set its axis reads span.</summary>
    [Fact]
    public void ASeatHoldingTwoPadsRumblesBoth()
    {
        OnPad(() => new[] { 3, 7 }).Play(RumbleEvent.ContactLight);
        Assert.Equal(new[] { 3, 7 }, _sink.Devices);
    }

    /// <summary>The toggle off produces nothing. So does the pad gate <c>--no-pads</c> and an
    /// unfocused window act on, which keeps a deterministic run off the hardware.</summary>
    [Fact]
    public void NothingPlaysWithTheToggleOffOrThePadsGated()
    {
        var seat = OnPad(() => new[] { 0 });
        PadRumble.Enabled = false;
        seat.Play(RumbleEvent.ContactHeavy);
        Assert.Empty(_sink.Devices);

        PadRumble.Enabled = true;
        Pads.Disabled = true;
        try
        {
            seat.Play(RumbleEvent.ContactHeavy);
            Assert.Empty(_sink.Devices);
        }
        finally
        {
            Pads.Disabled = false;
        }

        seat.Play(RumbleEvent.ContactHeavy);
        Assert.Equal(new[] { 0 }, _sink.Devices);
    }

    /// <summary>The overspeed drive restarts the loop on a cadence rather than every tick. It stops
    /// asking the moment the dive ends, so the next entry past the gate plays at once.</summary>
    [Fact]
    public void TheOverspeedDriveRefreshesOnACadenceAndResetsWhenItEnds()
    {
        var seat = OnPad(() => new[] { 0 });
        seat.Overspeed(true, 10.0);
        seat.Overspeed(true, 10.05);
        seat.Overspeed(true, 10.1);
        Assert.Single(_sink.Devices);
        seat.Overspeed(true, 10.2);
        Assert.Equal(2, _sink.Devices.Count);
        Assert.Equal(new RumbleShape(0.45f, 0f, 0.20f), _sink.Shapes[1]);

        seat.Overspeed(false, 10.21);
        Assert.Equal(2, _sink.Devices.Count);
        seat.Overspeed(true, 10.22);
        Assert.Equal(3, _sink.Devices.Count);
    }

    /// <summary>⚠ The hands gate: a seat whose last input came off the keyboard sends nothing to
    /// its roster's pad, however hard the event. The first pad press on that seat hands the rumble
    /// back. That is the reading that moves the seat's control prompts.</summary>
    [Fact]
    public void AKeyboardDrivenSeatIsSilentUntilItsPadIsPressed()
    {
        var device = new ActiveDevice();
        var seat = new PadRumble(() => new[] { 0 }, device);
        Assert.Equal(DeviceSide.Keyboard, device.Side);
        seat.Play(RumbleEvent.ContactHeavy);
        seat.Overspeed(true, 1.0);
        Assert.Empty(_sink.Devices);

        device.Observe(Quiet(), Pressed(InputAction.PitchUp), readsKeyboard: true);
        Assert.Equal(DeviceSide.Pad, device.Side);
        seat.Play(RumbleEvent.ContactHeavy);
        Assert.Equal(new[] { 0 }, _sink.Devices);
        Assert.Equal(new RumbleShape(1f, 1f, 1.00f), _sink.Shapes[0]);
    }

    /// <summary>A key press takes the rumble back off the pad. A player who puts the pad down and
    /// flies on the keyboard stops being buzzed the moment the keys speak.</summary>
    [Fact]
    public void AKeyPressSilencesThePadAgain()
    {
        var device = new ActiveDevice();
        var seat = new PadRumble(() => new[] { 0 }, device);
        device.Observe(Quiet(), Pressed(InputAction.PitchUp), readsKeyboard: true);
        device.Observe(Pressed(InputAction.PitchUp), Quiet(), readsKeyboard: true);
        Assert.Equal(DeviceSide.Keyboard, device.Side);
        seat.Play(RumbleEvent.NitroStart);
        Assert.Empty(_sink.Devices);
    }

    // A seat whose pad has already spoken, which is what every routing check above needs to reach
    // the sink at all.
    private static PadRumble OnPad(Func<int[]?> seatDevices)
    {
        var device = new ActiveDevice();
        device.Observe(Quiet(), Pressed(InputAction.PitchUp), readsKeyboard: true);
        return new PadRumble(seatDevices, device);
    }

    private static ActionSnapshot Quiet() => new();

    private static ActionSnapshot Pressed(InputAction action)
    {
        var snapshot = new ActionSnapshot();
        snapshot.Store(action, new ControlValue(true, 1f));
        return snapshot;
    }

    // The stub sink: what was asked of which pad, in the order it was asked.
    private sealed class Recorder : IRumbleSink
    {
        public List<int> Devices { get; } = new();

        public List<RumbleShape> Shapes { get; } = new();

        public void Play(int device, float weak, float strong, float seconds)
        {
            Devices.Add(device);
            Shapes.Add(new RumbleShape(weak, strong, seconds));
        }

        public void Clear()
        {
            Devices.Clear();
            Shapes.Clear();
        }
    }
}

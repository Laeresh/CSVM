using CSVM.Flight.Modes;
using CSVM.Testing;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A stunt seat's run control: a tap of respawn returns and a hold reruns, on both the crashed and
/// the live path. A press refused in flight leaves nothing behind for a crash. The crash cam's timer
/// returns only while the button is up. A start count holds the run clock at zero until GO, and a
/// tap's armed pose is taken once.
/// </summary>
public class StuntRunControlTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void A_tap_returns_on_release()
    {
        var control = new StuntRunControl();
        Assert.Equal(StuntRunCall.None, control.StepLive(true, Dt));
        Assert.Equal(StuntRunCall.None, control.StepLive(true, Dt));
        Assert.Equal(StuntRunCall.Return, control.StepLive(false, Dt));

        var crashed = new StuntRunControl();
        Assert.Equal(StuntRunCall.None, crashed.StepCrashed(true, Dt, Never));
        Assert.Equal(StuntRunCall.Return, crashed.StepCrashed(false, Dt, Never));
    }

    [Fact]
    public void A_hold_reruns_once_as_it_crosses_the_threshold_and_its_release_does_nothing()
    {
        int threshold = (int)(TapHoldButton.PadHoldSeconds / Dt);
        foreach (bool crashed in new[] { false, true })
        {
            var control = new StuntRunControl();
            int frames = 0;
            var call = StuntRunCall.None;
            while (call == StuntRunCall.None && frames < 60)
            {
                call = crashed ? control.StepCrashed(true, Dt, Never) : control.StepLive(true, Dt);
                frames++;
            }
            Assert.Equal(StuntRunCall.Rerun, call);
            Assert.InRange(frames, threshold - 1, threshold + 2);
            Assert.Equal(StuntRunCall.None, crashed ? control.StepCrashed(true, Dt, Never) : control.StepLive(true, Dt));
            Assert.Equal(StuntRunCall.None, crashed ? control.StepCrashed(false, Dt, Never) : control.StepLive(false, Dt));
        }
    }

    [Fact]
    public void A_refused_press_does_not_live_on_into_a_crash()
    {
        var control = new StuntRunControl();
        Assert.Equal(StuntRunCall.None, control.StepLive(true, Dt));

        // Refused from here on: the button is not read, and the press resolves to nothing now.
        control.StepLiveRefused(Dt);
        control.StepLiveRefused(Dt);

        Assert.Equal(StuntRunCall.None, control.StepCrashed(false, Dt, Never));
    }

    [Fact]
    public void The_crash_timer_returns_only_while_the_button_is_up_and_no_press_decided()
    {
        int asked = 0;
        bool Due(float dt)
        {
            asked++;
            return true;
        }

        var control = new StuntRunControl();
        Assert.Equal(StuntRunCall.None, control.StepCrashed(true, Dt, Due));
        Assert.Equal(0, asked);
        Assert.Equal(StuntRunCall.Return, control.StepCrashed(false, Dt, Due));
        Assert.Equal(0, asked);
        Assert.Equal(StuntRunCall.Return, control.StepCrashed(false, Dt, Due));
        Assert.Equal(1, asked);
    }

    [Fact]
    public void A_start_count_is_the_steps_until_go_and_a_crash_freezes_it()
    {
        var control = new StuntRunControl();
        control.StepClock(null, crashed: false, remote: false, Dt);
        Assert.False(control.Counting);
        Assert.Null(control.StepCount(Dt));

        control.BeginCount(StartCount.Rerun);
        var last = StartCountCue.None;
        int steps = 0;
        while (true)
        {
            control.StepClock(null, crashed: false, remote: false, Dt);
            if (control.StepCount(Dt) is not { } cue)
                break;
            last = cue;
            steps++;
            Assert.True(steps < 600, "the count hands over");
        }
        Assert.Equal(StartCountCue.Go, last);
        Assert.InRange(steps, 179, 181); // 3, 2, 1 at a second each, on 60 Hz steps
        Assert.False(control.Counting);
        Assert.Equal("GO", control.Count.Figure);

        // A crash mid-count: no step is the count's, and the count stands where it was.
        control.BeginCount(StartCount.Rerun);
        control.StepClock(null, crashed: false, remote: false, Dt);
        control.StepCount(Dt);
        float remaining = control.Count.Remaining;
        control.StepClock(null, crashed: true, remote: false, Dt);
        Assert.False(control.Counting);
        Assert.Null(control.StepCount(Dt));
        Assert.True(control.CountRunning);
        Assert.Equal(remaining, control.Count.Remaining);

        control.CancelCount();
        Assert.False(control.CountRunning);
    }

    [ExtractedDataFact]
    public void A_start_count_holds_the_clock_at_zero_until_go()
    {
        var run = StuntReturnTests.Load();
        var control = new StuntRunControl();
        control.BeginCount(StartCount.Rerun);
        while (true)
        {
            control.StepClock(run, crashed: false, remote: false, Dt);
            if (control.StepCount(Dt) == null)
                break;
            Assert.Equal(0f, run.Elapsed);
        }
        Assert.Equal(Dt, run.Elapsed);

        // A crash mid-count lets the clock run.
        run.Reset();
        control.BeginCount(StartCount.Rerun);
        control.StepClock(run, crashed: false, remote: false, Dt);
        control.StepCount(Dt);
        control.StepClock(run, crashed: true, remote: false, Dt);
        Assert.Equal(Dt, run.Elapsed);
    }

    [ExtractedDataFact]
    public void A_tap_return_is_armed_on_the_last_cleared_zone_and_taken_once()
    {
        var run = StuntReturnTests.Load();
        var control = new StuntRunControl();
        control.ArmReturn(run);
        Assert.Null(control.TakeReturn());

        StuntReturnTests.Fly(run, run.Zones[2], reverse: false);
        var (position, heading) = run.ReturnPose()!.Value;
        control.ArmReturn(run);
        var (pos, attitude) = control.TakeReturn()!.Value;
        Assert.Equal(position, pos);
        Assert.True((-attitude.Z).Dot(heading) > 0.9999f,
            $"the armed attitude's nose is the return heading: {(-attitude.Z).Dot(heading):0.00000}");
        Assert.True(attitude.X.Y * attitude.X.Y < 1e-6f, "wings level off world up");
        Assert.Null(control.TakeReturn());
    }

    private static bool Never(float dt) => false;
}

using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <c>PufferState.WindFactor</c> and <see cref="WorldWind"/>
/// (docs/plans/PLAN-puffer-engine-deltas.md B6).
///
/// <para>The load-bearing claim here is the DEFAULT. The puffer object's constructor
/// (<c>FUN_00550100</c>) writes <c>1.0</c> to <c>+0x6c</c> and the applier only overwrites it when
/// the authoring flag is set, so an unauthored puffer is FULLY wind-coupled — 2,802 of the
/// install's 2,863 friction-bearing compiled events are in exactly that state. Reading an absent
/// key as 0 would becalm all of them silently, which is why absent and explicit-zero are tested
/// apart.</para>
/// </summary>
public class PufferWindTests
{
    [Fact]
    public void ReaderParsesWindFactor()
    {
        // Mirrors the collide_puffer family, the only reader blocks in the install that author it
        // (3 at 0.3, plus torpuffertrail1/2 at 1.0).
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "collide_puffer" },
                "NUMBER", new List<object?> { 5f },
                "WIND_FACTOR", new List<object?> { 0.3f },
            },
        };

        var state = PufferState.FindInReader(reader, "collide_puffer");

        Assert.NotNull(state);
        Assert.Equal(0.3f, state!.WindFactor);
    }

    [Fact]
    public void ReaderWithNoWindFactorDefaultsToOne()
    {
        var reader = new List<object?>
        {
            "PUFFER_STATE",
            new List<object?>
            {
                "NAME", new List<object?> { "plain_puffer" },
                "NUMBER", new List<object?> { 5f },
            },
        };

        var state = PufferState.FindInReader(reader, "plain_puffer");

        Assert.NotNull(state);
        Assert.Equal(1f, state!.WindFactor);
    }

    [Fact]
    public void CompiledEventParsesWindFactor()
    {
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "torpuffertrail1",
            ["number"] = 5f,
            ["wind_factor"] = 1f,
        });

        Assert.Equal(1f, PufferState.FromAnimEvent(d).WindFactor);
    }

    /// <summary>An explicit 0 is a puffer deliberately opting OUT (the six-strong
    /// <c>subdoors_puffer</c> family, 12 events), and must survive as a 0 — it is not the same
    /// thing as saying nothing.</summary>
    [Fact]
    public void CompiledEventExplicitZeroIsNotTheDefault()
    {
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "subdoors_puffer1",
            ["number"] = 5f,
            ["wind_factor"] = 0f,
        });

        Assert.Equal(0f, PufferState.FromAnimEvent(d).WindFactor);
    }

    /// <summary>The compiled surface writes <c>wind_factor: null</c> when the key is unauthored —
    /// 4,423 of the install's 4,535 events. That is the ctor default, 1, not 0.</summary>
    [Fact]
    public void CompiledEventWithNullWindFactorDefaultsToOne()
    {
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "steampuffer",
            ["number"] = 5f,
            ["wind_factor"] = null,
        });

        Assert.Equal(1f, PufferState.FromAnimEvent(d).WindFactor);
    }

    [Fact]
    public void CompiledEventWithNoWindFactorAtAllDefaultsToOne()
    {
        var d = new AnimData(new Dictionary<string, object?>
        {
            ["name"] = "steampuffer",
            ["number"] = 5f,
        });

        Assert.Equal(1f, PufferState.FromAnimEvent(d).WindFactor);
    }

    /// <summary>The static vector is the whole wind before the gust builds, and the gust never
    /// touches the vertical: <c>FUN_0054ee10</c> copies <c>STATIC_VELOCITY.y</c> straight through
    /// (<c>0054ef5f</c>) and only composes x/z from <c>magnitude·cos/sin(heading)</c>.</summary>
    [Fact]
    public void GustIsHorizontalAndStartsAtTheStaticVector()
    {
        var wind = new WorldWind(new Vector3(0f, 2f, 0f), 10f, 5f, 5f, new Random(11));

        Assert.Equal(new Vector3(0f, 2f, 0f), wind.Velocity);

        for (int i = 0; i < 500; i++)
        {
            wind.Step(1f / 60f);
            Assert.Equal(2f, wind.Velocity.Y);
            Assert.InRange(wind.Magnitude, 0f, 10f);
        }
    }

    /// <summary>A zero <c>RANDOM_ACCEL</c>/<c>RANDOM_ANG_VEL</c> leaves the gust magnitude at its
    /// initial 0 forever, so the wind is exactly the static vector — the shape a mission that
    /// authored only <c>STATIC_VELOCITY</c> would get, and the shape <see cref="WorldWind.Still"/>
    /// degenerates to.</summary>
    [Fact]
    public void NoGustParametersLeaveTheStaticVectorAlone()
    {
        var wind = new WorldWind(new Vector3(1f, 2f, 3f), 10f, 0f, 0f, new Random(3));
        for (int i = 0; i < 200; i++)
            wind.Step(1f / 60f);

        Assert.Equal(0f, wind.Magnitude);
        Assert.Equal(new Vector3(1f, 2f, 3f), wind.Velocity);
    }

    /// <summary><c>RANDOM_ANG_VEL</c> is in DEGREES per second; the binary converts on the way in
    /// (<c>FUN_004bc680</c>: <c>value * 0.017453292</c>) and so do we. With the magnitude pinned at
    /// its ceiling by a huge accel, one second of stepping at 90 deg/s must turn the heading by at
    /// most 90 degrees from where it started — measured as the SHORTEST way round, since the
    /// engine wraps a negative heading by +2π (a small negative turn reads as ~6.08 rad). A
    /// degrees-as-radians bug turns it 57× further and lands anywhere.</summary>
    [Fact]
    public void AngularVelocityIsDegreesPerSecond()
    {
        var wind = new WorldWind(Vector3.Zero, 10f, 0f, 90f, new Random(5));
        for (int i = 0; i < 60; i++)
            wind.Step(1f / 60f);

        float fromZero = Math.Abs(Mathf.Wrap(wind.Heading, -MathF.PI, MathF.PI));
        Assert.InRange(fromZero, 0f, 90f * WorldWind.AngVelDegToRad);
        Assert.True(fromZero > 0f, "the heading did turn at all");
    }

    /// <summary>⚠ The magnitude step carries NO <c>dt</c> — traced, not an oversight (see
    /// <see cref="WorldWind"/>). Two winds given the same seed and the same frame COUNT must
    /// therefore reach the same magnitude regardless of the <c>dt</c> they were stepped with; only
    /// the heading differs. Asserted so the day someone "fixes" the missing <c>dt</c>, this says
    /// what they broke.</summary>
    [Fact]
    public void MagnitudeStepIsPerFrameNotPerSecond()
    {
        var fast = new WorldWind(Vector3.Zero, 10f, 5f, 5f, new Random(23));
        var slow = new WorldWind(Vector3.Zero, 10f, 5f, 5f, new Random(23));
        for (int i = 0; i < 100; i++)
        {
            fast.Step(1f / 60f);
            slow.Step(1f / 5f);
        }

        Assert.Equal(fast.Magnitude, slow.Magnitude);
        Assert.NotEqual(fast.Heading, slow.Heading);
    }

    /// <summary><see cref="EffectAmbience.Still"/> is the null object every unwired puffer reads.
    /// It refuses to be written, so a session that forgets to hand its own over fails at the
    /// writer instead of silently blowing a wind through the whole process.</summary>
    [Fact]
    public void StillAmbienceRefusesToBeWritten()
    {
        Assert.Equal(Vector3.Zero, EffectAmbience.Still.Wind);
        Assert.Throws<InvalidOperationException>(() => EffectAmbience.Still.SetWind(Vector3.One));

        var mine = new EffectAmbience();
        mine.SetWind(new Vector3(0f, 2f, 0f));
        Assert.Equal(new Vector3(0f, 2f, 0f), mine.Wind);
        Assert.Equal(Vector3.Zero, EffectAmbience.Still.Wind);
    }
}

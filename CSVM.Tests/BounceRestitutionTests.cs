using System.IO;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The decoded collision restitution: <c>FUN_0048d7f0</c>'s normal-only impulse,
/// scaled by <c>f_lin · bounce_factor</c>, ported as <see cref="FlightModel.BounceNormalSpeed"/>.
/// Decode: docs/org/flightModel.md, "Collision response and bounce_factor".
/// Its three separately-wrong-able parts (restitution, the doubled rotation term, the rebound/spin
/// partition) are asserted individually here rather than through an aggregate "feels bouncy" probe.
/// </summary>
public class BounceRestitutionTests
{
    private const float Sink = 30f;      // m/s of closing speed along the normal in every case here

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>The restitution proper, isolated: a non-rotating contact on an arm along the normal
    /// rebounds at exactly <c>bounce_factor</c>. That is the decode's [0, 0.6] ceiling touched, and
    /// the number measured on flat ground (0.62 ± 0.19 against a shipped 0.60).</summary>
    [Fact]
    public void AnAxialNonRotatingContactReboundsAtExactlyBounceFactor()
    {
        var m = Plant(0.6f);
        Rest(m);

        float outward = m.BounceNormalSpeed(Vector3.Down * Sink, Vector3.Up, new Vector3(0f, 2f, 0f));

        Assert.Equal(0.6f * Sink, outward, 3);
    }

    /// <summary>The rebound is the SHIPPED constant's, not a hardcoded one: the same contact on a
    /// plant carrying the executable's 0.8 fallback rebounds at 0.8.</summary>
    [Fact]
    public void TheReboundScalesWithTheLoadedBounceFactor()
    {
        var authored = Plant(0.6f);
        var fallback = Plant(0.8f);
        Rest(authored);
        Rest(fallback);

        var arm = new Vector3(0f, 2f, 0f);
        Assert.Equal(0.6f * Sink,
            authored.BounceNormalSpeed(Vector3.Down * Sink, Vector3.Up, arm), 3);
        Assert.Equal(0.8f * Sink,
            fallback.BounceNormalSpeed(Vector3.Down * Sink, Vector3.Up, arm), 3);
    }

    /// <summary>The contact-point velocity's rotational term is doubled, so a rotating contact
    /// leaves the surface faster than <c>bounce_factor</c> permits — the decode's answer to the
    /// above-0.6 flat-ground readings (docs/org/flightModel.md), which raising the constant instead
    /// would delete. A 2 m arm with 1 rad/s yaw gives 20.7 m/s outward, above the 18 m/s the same
    /// contact gives at rest.</summary>
    [Fact]
    public void TheContactPointRotationTermIsDoubledAndUnbounded()
    {
        var m = Plant(0.6f);
        Rest(m);
        m.BodyRates = new Vector3(0f, 1f, 0f);   // yaw left: ω × (2,0,0) = (0,0,−2)

        float outward = m.BounceNormalSpeed(
            Vector3.Forward * Sink, Vector3.Back, new Vector3(2f, 0f, 0f));

        Assert.Equal(20.69f, outward, 1);
        Assert.True(outward > 0.6f * Sink,
            $"the doubled term must be able to exceed bounce_factor outward={outward:0.00}");
    }

    /// <summary>The rebound/spin partition runs the opposite direction from the summary sentence
    /// carried with this decode ("a wingtip throws most of the impact into rotation"): a long arm
    /// partitions more into rebound. See docs/org/flightModel.md's withdrawal note.
    /// Asserted as a monotone sequence first, so it fails on direction rather than rounding.</summary>
    [Fact]
    public void ALongerContactArmPartitionsMoreIntoReboundNotLess()
    {
        var m = Plant(0.6f);
        Rest(m);

        // Arm along body X against a +Y normal: r × J is perpendicular to both, so sinθ = 1 and the
        // partition is at its strongest for a given length.
        float Rebound(float armM) =>
            m.BounceNormalSpeed(Vector3.Down * Sink, Vector3.Up, new Vector3(armM, 0f, 0f));

        float near = Rebound(0.5f), mid = Rebound(1f), tip = Rebound(5f);
        Assert.True(near < mid && mid < tip,
            $"rebound must grow with arm length near={near:0.000} mid={mid:0.000} tip={tip:0.000}");

        Assert.Equal(12.1f, mid, 1);    // f_lin ≈ 0.672
        Assert.Equal(16.4f, tip, 1);    // f_lin ≈ 0.911
        Assert.True(tip < 0.6f * Sink, "f_lin · bounce_factor stays bounded by the authored 0.6");
    }

    /// <summary>No surface dependence anywhere: the identical contact geometry rotated from a floor
    /// onto a vertical face rebounds identically. The original has no verticality test and no
    /// material lookup, so the flat-versus-vertical split must NOT arrive here as a per-surface
    /// coefficient.</summary>
    [Fact]
    public void TheImpulseHasNoSurfaceDependence()
    {
        var m = Plant(0.6f);
        Rest(m);

        float floor = m.BounceNormalSpeed(Vector3.Down * Sink, Vector3.Up, new Vector3(0f, 2f, 0f));
        float wall = m.BounceNormalSpeed(
            Vector3.Forward * Sink, Vector3.Back, new Vector3(0f, 0f, 2f));

        Assert.Equal(0.6f * Sink, floor, 3);
        Assert.Equal(floor, wall, 4);
    }

    /// <summary>A contact the aircraft is already leaving gets a damped inward answer rather than a
    /// second launch: once the contact velocity is outgoing, <c>J</c> points back into the surface,
    /// so repeated resolutions damp instead of compounding. The frame-stacking failure mode the
    /// decode ruled out, asserted rather than assumed.</summary>
    [Fact]
    public void AnOutgoingContactDampsRatherThanCompounds()
    {
        var m = Plant(0.6f);
        Rest(m);

        float outward = m.BounceNormalSpeed(Vector3.Up * Sink, Vector3.Up, new Vector3(0f, 2f, 0f));

        Assert.Equal(-0.6f * Sink, outward, 3);
        Assert.True(Mathf.Abs(outward) < Sink, "a repeat resolution must lose speed, not gain it");
    }

    /// <summary>The end-to-end read on the shipped data: a Bloodhawk built from the real archives
    /// rebounds at the authored 0.6, which is the whole point of the item — binding a constant this
    /// install already ships rather than inventing a pushback.</summary>
    [ExtractedDataFact]
    public void TheShippedBloodhawkReboundsAtTheAuthoredSixTenths()
    {
        var m = new FlightModel(PlaneStats.Load(ZrdrPath, "player_bhawk"));
        Rest(m);

        Assert.Equal(0.6f * Sink,
            m.BounceNormalSpeed(Vector3.Down * Sink, Vector3.Up, new Vector3(0f, 2f, 0f)), 3);
    }

    // Level, unrotating, at the sink speed — the state every case above varies from.
    private static void Rest(FlightModel m)
    {
        m.Reset(Vector3.Zero, Basis.Identity, Sink, 0f);
        m.BodyRates = Vector3.Zero;
    }

    // The Bloodhawk's reciprocal moments with an explicit `bounce_factor` — the
    // partition reads `I⁻¹`, so it is the one airframe number that matters here.
    private static FlightModel Plant(float bounceFactor) => new(new PlaneStats
    {
        RecInertia = new Vector3(1.18f, 1f, 1.1f),
        FdSpeed = 135f,
        VehWeight = 1900f,
        RefArea = 330f,
        BounceFactor = bounceFactor,
    });
}

/// <summary>
/// <see cref="FlightModel.Collide"/>'s graze response past restitution: the slide along the
/// struck surface (direction and the friction speed loss), the push-out, and the lever-arm
/// kick's sign. Restitution itself stays <see cref="BounceRestitutionTests"/>'s; this class
/// covers the parts D7 moved onto <c>Collide</c> alongside it.
/// The plan text naming this item also names a ground-stop threshold: that constant
/// (<c>GrazeStopSpeed</c>) is a fate decision outside <c>Collide</c>, so it sits on
/// <see cref="AircraftContactResolver"/> and is asserted there, not here.
/// </summary>
public class CollideResponseTests
{
    private const float Sink = 30f;
    private const float CrashSpeed = 25f;   // FlightController's crash-speed divisor

    /// <summary>The push-out is exact arithmetic with no severity scaling: it lands the aircraft
    /// at <c>prev + step·stopFrac + normal·0.15</c> regardless of speed, angle or pilot.</summary>
    [Fact]
    public void PositionAfterCollideIsExactlyThePrestepPlusPushOut()
    {
        var m = Plant(0.6f);
        m.Reset(Vector3.Zero, Basis.Identity, Sink, 0f);
        m.VelocityDir = Vector3.Forward;

        var prev = new Vector3(10f, 5f, 0f);
        var step = new Vector3(2f, 0f, -1f);

        m.Collide(prev, step, 0.6f, impact: Vector3.Zero, normal: Vector3.Up,
            humanPiloted: false, CrashSpeed);

        Assert.Equal(11.2f, m.Position.X, 4);
        Assert.Equal(5.15f, m.Position.Y, 4);
        Assert.Equal(-0.6f, m.Position.Z, 4);
    }

    /// <summary>A dead-on impact carries no tangential velocity to slide on: the slide is the
    /// zero vector, so friction has nothing to act on and speed is arrested outright.</summary>
    [Fact]
    public void AHeadOnImpactArrestsAllSpeedWithNoTangentialSlide()
    {
        var m = Plant(0.6f);
        m.Reset(Vector3.Zero, Basis.Identity, Sink, 0f);
        m.VelocityDir = new Vector3(0f, 0f, -1f);

        m.Collide(Vector3.Zero, Vector3.Zero, 0f, impact: Vector3.Zero,
            normal: new Vector3(0f, 0f, 1f), humanPiloted: false, CrashSpeed);

        Assert.Equal(0f, m.Speed, 4);
        Assert.Equal(0f, m.VelocityDir.X, 4);
        Assert.Equal(0f, m.VelocityDir.Y, 4);
        Assert.Equal(-1f, m.VelocityDir.Z, 4);
    }

    /// <summary>The same head-on impact, human-piloted: with no slide to carry, the outcome is
    /// restitution alone, and it reproduces the single-axis rebound
    /// <see cref="BounceRestitutionTests.AnAxialNonRotatingContactReboundsAtExactlyBounceFactor"/>
    /// already pins — straight back the way it came at <c>bounce_factor · Sink</c>.</summary>
    [Fact]
    public void AHeadOnImpactHumanPilotedReboundsStraightBackAtBounceFactor()
    {
        var m = Plant(0.6f);
        m.Reset(Vector3.Zero, Basis.Identity, Sink, 0f);
        m.VelocityDir = new Vector3(0f, 0f, -1f);

        m.Collide(Vector3.Zero, Vector3.Zero, 0f, impact: Vector3.Zero,
            normal: new Vector3(0f, 0f, 1f), humanPiloted: true, CrashSpeed);

        Assert.Equal(0.6f * Sink, m.Speed, 2);
        Assert.Equal(0f, m.VelocityDir.X, 4);
        Assert.Equal(0f, m.VelocityDir.Y, 4);
        Assert.Equal(1f, m.VelocityDir.Z, 4);
    }

    /// <summary>A shallow graze carries most of its speed tangentially: friction kills only the
    /// fraction the closing speed along the normal earns it, and the surviving direction is
    /// exactly the tangential component, normalised.</summary>
    [Fact]
    public void AShallowGrazeSlidesAlongTheSurfaceLosingOnlyItsNormalShare()
    {
        var m = Plant(0.6f);
        m.Reset(Vector3.Zero, Basis.Identity, 0f, 0f);
        // vel = (28, -6, 0): mostly tangential to an Up-normal surface, sinking in at 6 m/s.
        var vel = new Vector3(28f, -6f, 0f);
        m.VelocityDir = vel.Normalized();
        m.Speed = vel.Length();

        m.Collide(Vector3.Zero, Vector3.Zero, 0f, impact: Vector3.Zero, normal: Vector3.Up,
            humanPiloted: false, CrashSpeed);

        // slide = (28, 0, 0); friction fraction = GrazeFriction · vn/crashSpeed = 0.35 · 6/25.
        Assert.Equal(28f * (1f - 0.35f * 6f / 25f), m.Speed, 2);
        Assert.Equal(1f, m.VelocityDir.X, 4);
        Assert.Equal(0f, m.VelocityDir.Y, 4);
    }

    /// <summary>The same shallow graze, human-piloted: restitution adds an outward component the
    /// AI case has none of, so the two headings must diverge on the normal axis alone — the A/B
    /// this item's evidence rests on.</summary>
    [Fact]
    public void AShallowGrazeHumanPilotedGainsAnOutwardComponentTheAiCaseHasNone()
    {
        var vel = new Vector3(28f, -6f, 0f);

        var ai = Plant(0.6f);
        ai.Reset(Vector3.Zero, Basis.Identity, 0f, 0f);
        ai.VelocityDir = vel.Normalized();
        ai.Speed = vel.Length();

        var human = Plant(0.6f);
        human.Reset(Vector3.Zero, Basis.Identity, 0f, 0f);
        human.VelocityDir = vel.Normalized();
        human.Speed = vel.Length();

        ai.Collide(Vector3.Zero, Vector3.Zero, 0f, impact: Vector3.Zero, normal: Vector3.Up,
            humanPiloted: false, CrashSpeed);
        human.Collide(Vector3.Zero, Vector3.Zero, 0f, impact: Vector3.Zero, normal: Vector3.Up,
            humanPiloted: true, CrashSpeed);

        Assert.Equal(0f, ai.VelocityDir.Y, 4);
        Assert.True(human.VelocityDir.Y > 0f,
            $"the human case must rebound off the surface, VelocityDir.Y={human.VelocityDir.Y:0.000}");
    }

    /// <summary>The lever-arm kick's sign follows which side of the aircraft the impact landed
    /// on: a contact ahead of the aircraft and one behind it, everything else identical, must spin
    /// the body rate in opposite directions.</summary>
    [Theory]
    [InlineData(1f, true)]     // impact ahead of the push-out point
    [InlineData(-1f, false)]   // impact behind it
    public void TheKickSignFollowsWhichSideOfTheAircraftWasStruck(float impactX, bool expectPositiveZ)
    {
        var m = Plant(0.6f);
        m.Reset(Vector3.Zero, Basis.Identity, Sink, 0f);
        m.VelocityDir = new Vector3(0f, -1f, 0f);

        m.Collide(Vector3.Zero, Vector3.Zero, 0f, impact: new Vector3(impactX, 0f, 0f),
            normal: Vector3.Up, humanPiloted: false, CrashSpeed);

        if (expectPositiveZ)
            Assert.True(m.BodyRates.Z > 0f, $"BodyRates.Z={m.BodyRates.Z:0.000}");
        else
            Assert.True(m.BodyRates.Z < 0f, $"BodyRates.Z={m.BodyRates.Z:0.000}");
    }

    private static FlightModel Plant(float bounceFactor) => new(new PlaneStats
    {
        RecInertia = new Vector3(1.18f, 1f, 1.1f),
        FdSpeed = 135f,
        VehWeight = 1900f,
        RefArea = 330f,
        BounceFactor = bounceFactor,
    });
}

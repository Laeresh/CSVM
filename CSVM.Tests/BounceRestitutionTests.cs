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

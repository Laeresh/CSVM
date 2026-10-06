using System;
using CSVM.UI.Boards;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The stereo pair a cutscene plays as on the theater screen (<see cref="StereoPair"/>). It covers
/// where the two cameras stand on the authored one and how their frusta converge. It also covers the
/// depth the pair shows on the default seat. The framing is the letterbox card's, 55.76 degrees tall.
/// </summary>
[Trait("Tier", "Quick")]
public class StereoPairTests
{
    private const float Ipd = 0.063f;
    private const float Near = 0.05f;

    // The vertical angle CutsceneController.FramingFovDeg gives the shipped card: 4.0474 m half
    // height at 7.5 m, less its 2 % overscan.
    private static readonly float CardFovDeg = Mathf.RadToDeg(2f * MathF.Atan(4.0474105f / 1.02f / 7.5f));

    // A rotated camera far from the chapter origin, so no basis product is an exact 0 or 1.
    private static readonly Transform3D Authored = new(
        Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.07f)), new Vector3(-6350f, 300f, -4144f));

    [Fact]
    public void TheEyesStandOneInterocularApartAlongTheCamerasRightWithItsOrientation()
    {
        var left = StereoPair.Eye(Authored, 0, Ipd);
        var right = StereoPair.Eye(Authored, 1, Ipd);
        // World coordinates near 7 km step by half a millimetre, hence the tolerances.
        Assert.Equal(Ipd, left.Origin.DistanceTo(right.Origin), 3);
        Assert.True((right.Origin - left.Origin).Normalized().Dot(Authored.Basis.X.Normalized()) > 0.999f);
        Assert.True(((left.Origin + right.Origin) * 0.5f).DistanceTo(Authored.Origin) < 0.001f);
        Assert.True(left.Basis.IsEqualApprox(Authored.Basis) && right.Basis.IsEqualApprox(Authored.Basis));
    }

    // Each frustum's centre line meets the authored camera's at the convergence. The left eye's
    // shifts right by half the separation, scaled to the near plane, and the right's left.
    [Fact]
    public void TheFrustaMeetOnTheAuthoredAxisAtTheConvergence()
    {
        float c = StereoPair.ConvergenceM(7.5f);
        float leftShift = StereoPair.FrustumOffsetM(0, Ipd, Near, c);
        float rightShift = StereoPair.FrustumOffsetM(1, Ipd, Near, c);
        Assert.Equal(-rightShift, leftShift, 6);
        Assert.True(leftShift > 0f);
        // The left eye stands Ipd/2 left of the axis; its shifted centre line reaches the axis at c.
        Assert.Equal(Ipd * 0.5f, leftShift / Near * c, 5);
        Assert.Equal(2f * Near * MathF.Tan(Mathf.DegToRad(CardFovDeg) * 0.5f), StereoPair.FrustumHeightM(CardFovDeg, Near), 6);
    }

    // The proposed convergence is the card's own depth, so the bars stand on the screen plane. No
    // measured card falls back on the depth every chapter ships.
    [Fact]
    public void ThePairConvergesOnTheLetterboxCard()
    {
        Assert.Equal(7.5f, StereoPair.ConvergenceM(7.5f), 5);
        Assert.Equal(StereoPair.CardDepthM * StereoPair.ConvergenceOfCard, StereoPair.ConvergenceM(0f), 5);
        float bars = StereoPair.ParallaxM(7.5f, Ipd, StereoPair.ConvergenceM(7.5f), CardFovDeg, TheaterLayout.Default.HeightM);
        Assert.Equal(0f, bars, 6);
    }

    // The default seat puts the board 1.53 m tall at 2.5 m. Converged on the card, infinity lies
    // 12.1 mm behind on the screen, 0.6 % of its width, and appears 3.1 m away. A point 3 m from the
    // authored camera stands out of the screen at 1.94 m. Nothing diverges.
    [Fact]
    public void TheDefaultSeatShowsAShallowDepthBehindTheWindow()
    {
        var seat = TheaterLayout.Default;
        float c = StereoPair.ConvergenceM(7.5f);
        Assert.Equal(1.446f, StereoPair.OrthoDistanceM(CardFovDeg, seat.HeightM), 3);
        float far = StereoPair.ParallaxM(1e9f, Ipd, c, CardFovDeg, seat.HeightM);
        Assert.Equal(0.01215f, far, 4);
        Assert.True(far < Ipd);
        Assert.Equal(3.097f, StereoPair.ApparentDistanceM(far, seat.DistanceM, Ipd), 2);
        float near = StereoPair.ParallaxM(3f, Ipd, c, CardFovDeg, seat.HeightM);
        Assert.True(near < 0f);
        Assert.Equal(1.939f, StereoPair.ApparentDistanceM(near, seat.DistanceM, Ipd), 2);
        Assert.Equal(seat.DistanceM, StereoPair.ApparentDistanceM(0f, seat.DistanceM, Ipd), 5);
    }

    // The parallax law against a projection worked by hand: each eye's frustum, shifted, maps a point
    // to the screen; their difference is ParallaxM.
    [Theory]
    [InlineData(2f)]
    [InlineData(7.5f)]
    [InlineData(30f)]
    [InlineData(4000f)]
    public void TheParallaxIsTheDifferenceOfTheTwoProjections(float depth)
    {
        float c = StereoPair.ConvergenceM(7.5f);
        float height = TheaterLayout.Default.HeightM;
        float halfHeightAtNear = StereoPair.FrustumHeightM(CardFovDeg, Near) * 0.5f;
        float x = 0.4f;
        float OnScreen(int view)
        {
            float eyeX = view == 0 ? -Ipd * 0.5f : Ipd * 0.5f;
            float atNear = (x - eyeX) * Near / depth;
            float fromCentre = atNear - StereoPair.FrustumOffsetM(view, Ipd, Near, c);
            return fromCentre / halfHeightAtNear * height * 0.5f;
        }

        Assert.Equal(StereoPair.ParallaxM(depth, Ipd, c, CardFovDeg, height), OnScreen(1) - OnScreen(0), 5);
    }
}

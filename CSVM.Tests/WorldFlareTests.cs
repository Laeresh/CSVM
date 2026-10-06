using System;
using System.Collections;
using System.Reflection;
using CSVM.Launch;
using CSVM.Session.World;
using CSVM.Spec;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The world flare rig's placement law against the screen rig's, and the selection rule.
/// </summary>
public class WorldFlareTests
{
    // The flat chase camera's vertical half angle at the decoded external base, and the cockpit's.
    private static readonly float ChaseTan = Mathf.Tan(Mathf.DegToRad(46.8f) * 0.5f);
    private static readonly float CockpitTan = Mathf.Tan(Mathf.DegToRad(64.37f) * 0.5f);

    [Fact]
    public void SunDeadAheadIsInFrameAtTheAxisWithTheFullWash()
    {
        var at = WorldFlareRig.Place(Transform3D.Identity, new Vector3(0f, 0f, -30000f), ChaseTan, 16f / 9f);
        Assert.True(at.InFrame);
        Assert.Equal(Vector2.Zero, at.Tangent);
        Assert.Equal(30000f, at.Depth, 1);
        Assert.Equal(WorldFlareRig.WashMax, at.Wash, 5);
    }

    [Fact]
    public void SunBehindTheViewIsNotInFrame()
    {
        var at = WorldFlareRig.Place(Transform3D.Identity, new Vector3(0f, 100f, 30000f), ChaseTan, 16f / 9f);
        Assert.False(at.InFrame);
        Assert.Equal(0f, at.Wash);
    }

    [Fact]
    public void FrameEdgeFollowsTheVerticalAngleAndTheAspect()
    {
        // Just inside and just outside the top edge, then the side edge at 16:9.
        Assert.True(WorldFlareRig.Place(Transform3D.Identity, new Vector3(0f, ChaseTan * 0.99f, -1f), ChaseTan, 16f / 9f).InFrame);
        Assert.False(WorldFlareRig.Place(Transform3D.Identity, new Vector3(0f, ChaseTan * 1.01f, -1f), ChaseTan, 16f / 9f).InFrame);
        Assert.True(WorldFlareRig.Place(Transform3D.Identity, new Vector3(ChaseTan * 1.7f, 0f, -1f), ChaseTan, 16f / 9f).InFrame);
        Assert.False(WorldFlareRig.Place(Transform3D.Identity, new Vector3(ChaseTan * 1.8f, 0f, -1f), ChaseTan, 16f / 9f).InFrame);
    }

    [Theory]
    [InlineData(328f)]
    [InlineData(100f)]
    [InlineData(500f)]
    public void WashMatchesTheScreenRigAtTheSamePixelDistance(float px)
    {
        // A sun px pixels above centre in a 720-high frame, at both fields of view. The wash is the
        // screen rig's law in reference pixels, so the frame's angle cancels.
        foreach (float tan in new[] { ChaseTan, CockpitTan })
        {
            float t = px / 360f * tan;
            var at = WorldFlareRig.Place(Transform3D.Identity, new Vector3(0f, t, -1f), tan, 16f / 9f);
            float expected = Mathf.Clamp(0.66f - ((px - 30f) * 0.001651f), 0f, 0.66f);
            Assert.Equal(expected, at.Wash, 4);
        }
    }

    [Fact]
    public void ElementsLieOnTheSunToAxisLineAtTheScreenRigsFractions()
    {
        var sun = new Vector2(0.2f, -0.1f);
        Assert.Equal(new Vector3(0.2f * 1000f, -0.1f * 1000f, -1000f), WorldFlareRig.ElementCentre(sun, 0f, 1000f));
        Assert.Equal(new Vector3(0f, 0f, -1000f), WorldFlareRig.ElementCentre(sun, 1f, 1000f));
        var mirrored = WorldFlareRig.ElementCentre(sun, 2f, 1000f);
        Assert.Equal(-200f, mirrored.X, 3);
        Assert.Equal(100f, mirrored.Y, 3);
    }

    [Fact]
    public void ElementPixelSizeHoldsAtEveryFieldOfView()
    {
        // In a 720-high pane an element of DiaPx spans DiaPx pixels whatever the camera's angle,
        // the original's fixed pixel size in Cockpit as in Chase.
        foreach (float tan in new[] { ChaseTan, CockpitTan, 1f })
        {
            foreach (var e in WorldFlareRig.Elements)
            {
                float pixels = 2f * WorldFlareRig.HalfSize(e.DiaPx, tan) * (360f / tan);
                Assert.Equal(e.DiaPx, pixels, 3);
            }
        }
    }

    [Fact]
    public void PlacementFollowsTheViewTransform()
    {
        // A view turned to face the sun sees it on the axis; the same sun from the identity view
        // sits off it.
        var sun = new Vector3(5000f, 3000f, -5000f);
        var eye = new Vector3(10f, 20f, 30f);
        var facing = Transform3D.Identity.Translated(eye).LookingAt(sun, Vector3.Up);
        var at = WorldFlareRig.Place(facing, sun, ChaseTan, 16f / 9f);
        Assert.True(at.InFrame);
        Assert.True(at.Tangent.Length() < 1e-4f);
        Assert.Equal((sun - eye).Length(), at.Depth, 0);
    }

    [Fact]
    public void SpecMatchesTheScreenRigsMeasuredOne()
    {
        // The two rigs carry one footage measurement; the screen rig's copy is private and must not
        // be edited, so its values are read by reflection.
        var screen = typeof(LensFlareRig);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        Assert.Equal(Const(screen, "RefHeight"), WorldFlareRig.RefHeight);
        Assert.Equal(Const(screen, "WashMax"), WorldFlareRig.WashMax);
        Assert.Equal(Const(screen, "WashFullPx"), WorldFlareRig.WashFullPx);
        Assert.Equal(Const(screen, "WashSlopePerPx"), WorldFlareRig.WashSlopePerPx);
        Assert.Equal(Const(screen, "FadeOutSeconds"), WorldFlareRig.FadeOutSeconds);
        var elements = (IList)screen.GetField("Elements", flags)!.GetValue(null)!;
        Assert.Equal(WorldFlareRig.Elements.Count, elements.Count);
        for (int i = 0; i < elements.Count; i++)
        {
            object e = elements[i]!;
            Assert.Equal(WorldFlareRig.Elements[i].Frac, Prop(e, "Frac"));
            Assert.Equal(WorldFlareRig.Elements[i].DiaPx, Prop(e, "DiaPx"));
            Assert.Equal(WorldFlareRig.Elements[i].Intensity, Prop(e, "Intensity"));
        }

        static float Const(Type t, string name) => (float)t.GetField(name, flags)!.GetValue(null)!;
        static float Prop(object o, string name) => (float)o.GetType().GetProperty(name)!.GetValue(o)!;
    }

    [Fact]
    public void StereoAlwaysTakesTheWorldRigAndFlatOnlyInEnhancedOnAsk()
    {
        Assert.True(SkyStage.UsesWorldFlare(stereo: true, asked: false, enhanced: false));
        Assert.True(SkyStage.UsesWorldFlare(stereo: false, asked: true, enhanced: true));
        Assert.False(SkyStage.UsesWorldFlare(stereo: false, asked: true, enhanced: false));
        Assert.False(SkyStage.UsesWorldFlare(stereo: false, asked: false, enhanced: true));
    }

    [Fact]
    public void WorldFlareFlagParses()
    {
        Assert.True(SessionSpec.Parse(new[] { "--chapter=C3", "--world-flare" }).WorldFlare);
        Assert.False(SessionSpec.Parse(new[] { "--chapter=C3" }).WorldFlare);
    }
}

using CSVM.Flight.Hud;
using CSVM.Flight.Weapons;
using CSVM.Spec;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The two remake-only gun aids, engine-free. Both resolve from a flag, the saved file and
/// <c>--det</c>, and the file carries them. <see cref="TargetHud.LeadPoint"/> places the marker.
/// </summary>
public class GunAidOptionsTests
{
    private const float Tol = 1e-3f;

    [Fact]
    public void BothAidsAreOffWithNoFlagAndNothingSaved()
    {
        var spec = SessionSpec.Parse(new[] { "--fly" }).WithSavedLeadingAimAssist(null).WithSavedLeadMarker(null);
        Assert.False(spec.LeadingAimAssist);
        Assert.False(spec.LeadMarker);
    }

    [Fact]
    public void ASavedSwitchTurnsTheAidOnAndASavedOffLeavesItOff()
    {
        var on = SessionSpec.Parse(new[] { "--fly" }).WithSavedLeadingAimAssist(true).WithSavedLeadMarker(true);
        Assert.True(on.LeadingAimAssist);
        Assert.True(on.LeadMarker);
        var off = SessionSpec.Parse(new[] { "--fly" }).WithSavedLeadingAimAssist(false).WithSavedLeadMarker(false);
        Assert.False(off.LeadingAimAssist);
        Assert.False(off.LeadMarker);
    }

    [Fact]
    public void TheFlagBeatsASavedOff()
    {
        var spec = SessionSpec.Parse(new[] { "--fly", "--leading-assist", "--lead-marker" })
            .WithSavedLeadingAimAssist(false).WithSavedLeadMarker(false);
        Assert.True(spec.LeadingAimAssist);
        Assert.True(spec.LeadMarker);
    }

    [Fact]
    public void ADetRunReadsNoSavedSwitchButKeepsItsFlag()
    {
        var saved = SessionSpec.Parse(new[] { "--fly", "--det" }).WithSavedLeadingAimAssist(true).WithSavedLeadMarker(true);
        Assert.False(saved.LeadingAimAssist);
        Assert.False(saved.LeadMarker);
        var flagged = SessionSpec.Parse(new[] { "--fly", "--det", "--lead-marker" }).WithSavedLeadMarker(false);
        Assert.True(flagged.LeadMarker);
    }

    [Fact]
    public void TheOptionsFileCarriesBothSwitchesAndAnAbsentKeyReadsNeverSet()
    {
        var back = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { LeadingAimAssist = true, LeadMarker = false }));
        Assert.NotNull(back);
        Assert.True(back!.LeadingAimAssist);
        Assert.False(back.LeadMarker);
        var empty = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef()));
        Assert.Null(empty!.LeadingAimAssist);
        Assert.Null(empty.LeadMarker);
    }

    // The marker is the world point where the target and a round fired along the intercept meet.
    [Fact]
    public void TheLeadPointLiesOnTheInterceptARoundMeetsTheTargetAlong()
    {
        var muzzle = new Vector3(0f, 500f, 0f);
        var vs = new Vector3(0f, 0f, -60f);
        var target = new Vector3(120f, 500f, -280f);
        var vt = new Vector3(-50f, 0f, -20f);
        const float speed = 850f;
        var lead = TargetHud.LeadPoint(muzzle, vs, speed, 1000f, target, vt);
        Assert.NotNull(lead);
        Assert.True(AimAssist.TryIntercept(muzzle, speed, target, vt - vs, out var dir, out float t));
        var round = muzzle + ((dir * speed) + vs) * t;
        Assert.True(lead!.Value.DistanceTo(target + (vt * t)) < Tol * 100f);
        Assert.True(round.DistanceTo(lead.Value) < Tol * 100f);
        // On the pipper's ray: the round's world velocity, the shooter's carried, not the solved
        // direction alone.
        Assert.True((lead.Value - muzzle).Normalized().IsEqualApprox(((dir * speed) + vs).Normalized()));
        Assert.False((lead.Value - muzzle).Normalized().IsEqualApprox(dir));
    }

    [Fact]
    public void TheLeadPointIsNullPastTheRangeAndForATargetOutrunningTheRound()
    {
        var muzzle = Vector3.Zero;
        Assert.Null(TargetHud.LeadPoint(muzzle, Vector3.Zero, 850f, 1000f, new Vector3(0f, 0f, -1500f), Vector3.Zero));
        Assert.Null(TargetHud.LeadPoint(muzzle, Vector3.Zero, 100f, 5000f, new Vector3(0f, 0f, -300f), new Vector3(0f, 0f, -400f)));
        Assert.NotNull(TargetHud.LeadPoint(muzzle, Vector3.Zero, 850f, 1000f, new Vector3(0f, 0f, -900f), Vector3.Zero));
    }
}

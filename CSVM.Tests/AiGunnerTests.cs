using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The D14 gunnery gates, engine-free: <see cref="AiGunner.Solve"/> is pure over its arguments
/// (the seeded rng feeds only the per-shot scatter, which these tests never draw — the rng is
/// null here on purpose, and the scatter cone itself is pinned by the in-engine
/// <c>ai-gunnery</c> suite and the aim-assist suite's Scatter cases). Geometry: shooter and
/// target static unless stated, round at 500 m/s with 1000 m RANGE.
/// </summary>
public class AiGunnerTests
{
    private const float Speed = 500f;
    private const float Range = 1000f;

    private static readonly Vector3 TargetPos = Vector3.Zero;
    private static readonly Vector3 TargetForward = Vector3.Forward; // nose on -Z

    // A shooter dead astern (on +Z), nose on the target: every gate passes.
    [Fact]
    public void ADeadAsternShotPassesEveryGate()
    {
        var g = Gunner();
        var ownPos = new Vector3(0f, 0f, 500f);
        g.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, Speed, Range);
        Assert.True(g.WantsFire);
        // Static target, static shooter: the lead IS the bearing.
        Assert.True(g.AimDirWorld.Dot((TargetPos - ownPos).Normalized()) > 0.9999f);
    }

    [Fact]
    public void TheLeadPointsAheadOfACrossingTarget()
    {
        var g = Gunner();
        var ownPos = new Vector3(0f, 0f, 500f);
        var targetVel = new Vector3(60f, 0f, 0f); // sliding left-to-right, nose still on -Z
        g.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, targetVel, TargetForward, Speed, Range);
        Assert.True(g.WantsFire);
        Assert.True(g.AimDirWorld.X > 0.01f, $"the lead swings toward the target's motion x={g.AimDirWorld.X}");
    }

    [Fact]
    public void OutsideTheForwardGunConeTheShotIsRefused()
    {
        var g = Gunner();
        var ownPos = new Vector3(0f, 0f, 500f);
        // Nose 30° off the bearing: the lead sits outside the ±11° gun cone.
        var offNose = (TargetPos - ownPos).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(30f));
        g.Solve(ownPos, Vector3.Zero, Basis.LookingAt(offNose, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, Speed, Range);
        Assert.False(g.WantsFire);
        // Just inside the cone fires: 10° off.
        var nearNose = (TargetPos - ownPos).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(10f));
        g.Solve(ownPos, Vector3.Zero, Basis.LookingAt(nearNose, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, Speed, Range);
        Assert.True(g.WantsFire);
    }

    [Fact]
    public void BeyondTheWeaponsRangeTheShotIsRefused()
    {
        var g = Gunner();
        var ownPos = new Vector3(0f, 0f, 1200f); // past the 1000 m RANGE
        g.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, Speed, Range);
        Assert.False(g.WantsFire);
    }

    [Fact]
    public void QuickDrawAcceptsTheForeAndAftConesAndRefusesTheBeam()
    {
        var g = Gunner(); // 50°
        // Dead ahead of the target's nose (-Z side) and dead astern (+Z) both pass.
        Assert.True(g.QuickDrawAccepts(new Vector3(0f, 0f, -300f), TargetPos, TargetForward));
        Assert.True(g.QuickDrawAccepts(new Vector3(0f, 0f, 300f), TargetPos, TargetForward));
        // 40° off the tail passes a 50° cone; 60° off does not; the beam (90°) never does.
        var at40 = new Vector3(Mathf.Sin(Mathf.DegToRad(40f)), 0f, Mathf.Cos(Mathf.DegToRad(40f))) * 300f;
        var at60 = new Vector3(Mathf.Sin(Mathf.DegToRad(60f)), 0f, Mathf.Cos(Mathf.DegToRad(60f))) * 300f;
        Assert.True(g.QuickDrawAccepts(at40, TargetPos, TargetForward));
        Assert.False(g.QuickDrawAccepts(at60, TargetPos, TargetForward));
        Assert.False(g.QuickDrawAccepts(new Vector3(300f, 0f, 0f), TargetPos, TargetForward));
        // The same 60° bearing passes once the pilot's rating widens the cone (89° at rating 9).
        g.QuickDrawAngleDeg = 89f;
        Assert.True(g.QuickDrawAccepts(at60, TargetPos, TargetForward));
    }

    [Fact]
    public void HoldFireClearsTheTriggerUntilTheNextSolve()
    {
        var g = Gunner();
        var ownPos = new Vector3(0f, 0f, 500f);
        g.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, Speed, Range);
        Assert.True(g.WantsFire);
        g.HoldFire();
        Assert.False(g.WantsFire);
    }

    private static AiGunner Gunner() => new(null!)
    {
        DeadEyeAngleDeg = 4f,
        QuickDrawAngleDeg = 50f,
    };
}

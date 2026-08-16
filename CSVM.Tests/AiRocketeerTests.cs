using System.Collections.Generic;
using System.IO;
using CSVM;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The AI ordnance gates, engine-free: <see cref="AiRocketeer.Solve"/> is pure over its arguments
/// but for the launch roll, which these tests script through the constructor's delegate rather
/// than leaving to chance. Geometry: shooter dead astern of a static target with the pylon at the
/// shooter, round at 200 m/s inside the shipped 200 to 800 m band, unless a case says otherwise.
/// </summary>
public class AiRocketeerTests
{
    private const float Speed = 200f;

    private static readonly Vector3 TargetPos = Vector3.Zero;
    private static readonly Vector3 TargetForward = Vector3.Forward; // nose on -Z

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [Fact]
    public void ADeadAsternShotPassesEveryGateAndTakesThePylon()
    {
        var r = Rocketeer(rollsPass: true);
        Solve(r, 500f);
        Assert.True(r.WantsFire);
        Assert.Equal(0, r.SelectedPylon);
    }

    // The band's two edges are both live: a max-range-only gate would let an AI launch from the
    // merge, which the shipped 200 m floor exists to stop.
    [Theory]
    [InlineData(150f, false)]  // inside the 200 m floor
    [InlineData(500f, true)]
    [InlineData(900f, false)]  // past the 800 m ceiling
    public void OnlyASeparationInsideTheEngagementBandIsLaunchedAt(float separation, bool fires)
    {
        var r = Rocketeer(rollsPass: true);
        Solve(r, separation);
        Assert.Equal(fires, r.WantsFire);
    }

    // The 5° gate is the residual the traverse clamp leaves, so the employable cone is the ±11°
    // limit plus 5°, not 5° off the nose. Tighter than the gun's 10° by a weapon class.
    [Theory]
    [InlineData(8f, true)]    // inside the limits, nothing given away
    [InlineData(14f, true)]   // clamps to 11°, 3° residual
    [InlineData(18f, false)]  // clamps to 11°, 7° residual, past the ordnance gate
    public void TheAimGateIsTheResidualLeftByTheTraverseClamp(float noseOffDeg, bool fires)
    {
        var r = Rocketeer(rollsPass: true);
        var ownPos = new Vector3(0f, 0f, 500f);
        var offNose = (TargetPos - ownPos).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(noseOffDeg));
        r.Solve(ownPos, Vector3.Zero, Basis.LookingAt(offNose, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: false, Pylons(ownPos));
        Assert.Equal(fires, r.WantsFire);
    }

    // A lead the clamp had to give ground on still leaves along the CLAMPED direction, which is
    // what the original's mount fires along, not along the raw lead it never reached.
    [Fact]
    public void TheRoundLeavesAlongTheClampedAimNotTheRawLead()
    {
        var r = Rocketeer(rollsPass: true);
        var ownPos = new Vector3(0f, 0f, 500f);
        var offNose = (TargetPos - ownPos).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(14f));
        var basis = Basis.LookingAt(offNose, Vector3.Up);
        r.Solve(ownPos, Vector3.Zero, basis,
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: false, Pylons(ownPos));
        Assert.True(r.WantsFire);
        float offBoreDeg = Mathf.RadToDeg(Mathf.Acos(
            Mathf.Clamp(r.LaunchDirWorld.Dot(-basis.Z.Normalized()), -1f, 1f)));
        Assert.True(offBoreDeg > 10.5f && offBoreDeg < 11.5f,
            $"the launch sits at the 11° traverse limit, not on the nose and not on the lead: {offBoreDeg}°");
    }

    // Trap (b): the Black Hat Warhawk's torpedoes carry DAMAGES_ZEPPELIN and must never be
    // launched at an aircraft, and a plain rocket must never be launched at a gasbag.
    [Theory]
    [InlineData(false, false, true)]  // plain rocket at an aircraft
    [InlineData(true, false, false)]  // torpedo at an aircraft
    [InlineData(true, true, true)]    // torpedo at a gasbag
    [InlineData(false, true, false)]  // plain rocket at a gasbag
    public void TheZeppelinMatchHoldsBothWays(bool damagesZeppelin, bool targetIsGasbag, bool fires)
    {
        var r = Rocketeer(rollsPass: true);
        var ownPos = new Vector3(0f, 0f, 500f);
        r.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag,
            Pylons(ownPos, damagesZeppelin));
        Assert.Equal(fires, r.WantsFire);
    }

    // The two aim gates are one decoded pair, per weapon class, and the discriminating case above
    // (18°, which the gun takes and the ordnance refuses) only means anything while they stay
    // distinct. Pinned as angles as well as cosines so a later tidy cannot fold them onto one
    // shared constant without the arithmetic saying so.
    [Fact]
    public void TheOrdnanceAndGunAimGatesAreTheDecodedPairAtFiveAndTenDegrees()
    {
        Assert.Equal(0.9962f, AiRocketeer.AimQualityCos);
        Assert.Equal(0.9848f, AiGunner.AimQualityCos);
        Assert.Equal(5f, Mathf.RadToDeg(Mathf.Acos(AiRocketeer.AimQualityCos)), 1);
        Assert.Equal(10f, Mathf.RadToDeg(Mathf.Acos(AiGunner.AimQualityCos)), 1);
        Assert.True(AiRocketeer.AimQualityCos > AiGunner.AimQualityCos,
            "ordnance is aimed more precisely than a gun, not the same or less");
    }

    // The Black Hat Warhawk's own fit, read out of the install rather than invented: the vehicle
    // def gives it eight wep_14, and wep_14's weapon def carries DAMAGES_ZEPPELIN. Those two
    // authored facts together are what keeps the game's heaviest ordnance load on the rail against
    // aircraft, so the gate is exercised on the shipped numbers.
    [ExtractedDataFact]
    public void TheWarhawksAuthoredTorpedoIsRefusedAgainstAircraftAndOfferedAgainstAGasbag()
    {
        var weapons = WeaponDefs.Load(ZrdrPath, null);
        var fit = AuthoredOrdnance("bhatwarhawk", weapons);
        Assert.Equal("wep_14", fit.Id);
        Assert.Equal(8, fit.Rounds);
        var torpedo = weapons.Get(fit.Id)!;
        Assert.True(torpedo.DamagesZeppelin, "the aerial torpedo is the shipped zeppelin weapon");

        var ownPos = new Vector3(0f, 0f, 500f); // inside the authored 350 to 800 m band
        var pylon = new RocketPylonView
        {
            Index = 0,
            DamagesZeppelin = torpedo.DamagesZeppelin,
            Armed = fit.Rounds > 0,
            MountPos = ownPos,
            RoundSpeed = torpedo.Velocity ?? Speed,
        };
        var basis = Basis.LookingAt(TargetPos - ownPos, Vector3.Up);
        var pylons = new List<RocketPylonView> { pylon };

        // The authored band and interval, so the case runs on the Warhawk's numbers and not on
        // the militia defaults the fields still carry (BL-394 wires them per vehicle).
        var atAircraft = Warhawk(fit, rollsPass: true);
        atAircraft.Solve(ownPos, Vector3.Zero, basis,
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: false, pylons);
        Assert.False(atAircraft.WantsFire);
        Assert.Equal(-1, atAircraft.SelectedPylon);

        var atGasbag = Warhawk(fit, rollsPass: true);
        atGasbag.Solve(ownPos, Vector3.Zero, basis,
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: true, pylons);
        Assert.True(atGasbag.WantsFire);
        Assert.Equal(0, atGasbag.SelectedPylon);
    }

    // The walk names the pylon, so a torpedo sitting first cannot capture a launch the gates
    // cleared for the rocket behind it.
    [Fact]
    public void TheWalkSkipsAnIllegalPylonAndSelectsTheLegalOne()
    {
        var r = Rocketeer(rollsPass: true);
        var ownPos = new Vector3(0f, 0f, 500f);
        var pylons = new List<RocketPylonView>
        {
            Pylon(0, ownPos, damagesZeppelin: true),
            Pylon(1, ownPos),
        };
        r.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: false, pylons);
        Assert.True(r.WantsFire);
        Assert.Equal(1, r.SelectedPylon);
    }

    [Fact]
    public void ASpentPylonIsSkipped()
    {
        var r = Rocketeer(rollsPass: true);
        var ownPos = new Vector3(0f, 0f, 500f);
        var pylons = new List<RocketPylonView> { Pylon(0, ownPos, armed: false), Pylon(1, ownPos) };
        r.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: false, pylons);
        Assert.Equal(1, r.SelectedPylon);
    }

    // The quick draw gates the whole pass, ordnance included: the original applies it at the top
    // of the fire decision, ahead of the weapon walk.
    [Fact]
    public void ABeamAttackIsRefusedByTheQuickDrawCone()
    {
        var r = Rocketeer(rollsPass: true);
        var ownPos = new Vector3(500f, 0f, 0f); // abeam the target's nose axis
        r.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: false, Pylons(ownPos));
        Assert.False(r.WantsFire);
        Assert.Equal(-1, r.SelectedPylon);
    }

    // ⚠ The decode's sharpest detail: the shot routine stamps both cooldowns BEFORE the dice, so
    // a failed roll has already spent the whole interval and does not retry next tick.
    [Fact]
    public void AFailedRollStillSpendsTheFullRefireInterval()
    {
        var r = Rocketeer(rollsPass: false);
        Solve(r, 500f);
        Assert.False(r.WantsFire);
        Assert.Equal(r.RefireSeconds, r.LockoutRemaining, 3);
        r.Tick(1f / 60f);
        Solve(r, 500f);
        Assert.Equal(-1, r.SelectedPylon); // locked out, not merely unlucky again
    }

    // The consequence of that ordering, stated as a rate: attempts are one per interval whatever
    // the dice do, so a 60 Hz run makes ten of them over 299 s and not eighteen thousand.
    [Fact]
    public void AttemptsAreOnePerIntervalRegardlessOfTheRollOutcome()
    {
        int attempts = 0;
        var r = new AiRocketeer(() => { attempts++; return 1f; }) { QuickDrawChance = 0.05f };
        for (int i = 0; i < 299 * 60; i++)
        {
            r.Tick(1f / 60f);
            Solve(r, 500f);
        }
        Assert.Equal(10, attempts);
    }

    [Fact]
    public void TheLockoutAgesOnTicksWhereNoDecisionRuns()
    {
        var r = Rocketeer(rollsPass: false);
        Solve(r, 500f);
        Assert.Equal(30f, r.LockoutRemaining, 3);
        for (int i = 0; i < 60; i++)
        {
            r.Tick(1f / 60f); // no Solve: the AI lost its target or left Pursue
        }
        Assert.Equal(29f, r.LockoutRemaining, 3);
    }

    [Fact]
    public void TickClearsTheTriggerUntilTheNextSolve()
    {
        var r = Rocketeer(rollsPass: true);
        Solve(r, 500f);
        Assert.True(r.WantsFire);
        r.Tick(1f / 60f);
        Assert.False(r.WantsFire);
    }

    private static void Solve(AiRocketeer r, float separation)
    {
        var ownPos = new Vector3(0f, 0f, separation);
        r.Solve(ownPos, Vector3.Zero, Basis.LookingAt(TargetPos - ownPos, Vector3.Up),
            TargetPos, Vector3.Zero, TargetForward, targetIsGasbag: false, Pylons(ownPos));
    }

    // rollsPass drives the dice to either end of the comparison: 0 is under any chance, 1 is over
    // every shipped one (the highest is 0.44 at rating 9).
    private static AiRocketeer Rocketeer(bool rollsPass) =>
        new(() => rollsPass ? 0f : 1f) { QuickDrawChance = 0.05f };

    private static List<RocketPylonView> Pylons(Vector3 mountPos, bool damagesZeppelin = false) =>
        new() { Pylon(0, mountPos, damagesZeppelin) };

    private static AiRocketeer Warhawk(AuthoredFit fit, bool rollsPass) =>
        new(() => rollsPass ? 0f : 1f)
        {
            QuickDrawChance = 0.05f,
            MinRangeM = fit.MinRange,
            MaxRangeM = fit.MaxRange,
            RefireSeconds = fit.Refire,
        };

    // One AI vehicle def's first ordnance entry, straight out of vehicle.zrd.json. The engine reads
    // the 5-tuple as id, rounds, refire interval, then the engagement band; the field order is
    // decoded in docs/org/aiPilot/aiWeapons.md. Nothing in the runtime parses this block yet
    // (BL-394), so the test reads it here rather than pretending a reader exists.
    private static AuthoredFit AuthoredOrdnance(string defName, WeaponDefs weapons)
    {
        var root = Zrdr.LoadFile(ZrdrPath, "vehicle.json")[0] as List<object?>;
        Assert.NotNull(root);
        for (int i = 0; i + 1 < root!.Count; i += 2)
        {
            if (root[i] as string != defName || root[i + 1] is not List<object?> props)
                continue;
            var block = ZrdrDict.FromAlternating(props).List("weapons");
            Assert.NotNull(block);
            foreach (var slot in block!)
            {
                if (slot is not List<object?> t || t.Count < 5 || t[0] is not string id)
                    continue;
                if (weapons.Get(id) is not { IsCannon: false })
                    continue; // the gun entry rides the same block and is not the ordnance path's
                return new AuthoredFit(id, (int)(float)t[1]!, (float)t[2]!, (float)t[3]!, (float)t[4]!);
            }
        }
        Assert.Fail($"'{defName}' carries no ordnance entry in vehicle.zrd.json");
        return default!;
    }

    private static RocketPylonView Pylon(int index, Vector3 mountPos,
        bool damagesZeppelin = false, bool armed = true) =>
        new()
        {
            Index = index,
            DamagesZeppelin = damagesZeppelin,
            Armed = armed,
            MountPos = mountPos,
            RoundSpeed = Speed,
        };

    /// <summary>One AI vehicle def's authored ordnance slot, in the engine's own field order.</summary>
    private sealed record AuthoredFit(string Id, int Rounds, float Refire, float MinRange, float MaxRange);
}

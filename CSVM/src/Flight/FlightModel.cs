using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>Pilot inputs, all in [-1, 1] except Throttle in [0, 1].
/// Pitch + = pull (nose up), Roll + = bank left, Yaw + = nose left.</summary>
public struct FlightInput
{
    public float Pitch, Roll, Yaw, Throttle;

    /// <summary>Ground blow's probe result, filled by the caller because only it has the world:
    /// the WORLD-frame surface normal of the nearest hit on a ray cast forward along the nose,
    /// and the distance to it in metres. <see cref="Vector3.Zero"/> means no hit — and it is the
    /// only "no hit" signal, matching the original, which leaves its zero-initialised output vector
    /// alone on a miss, a back-facing surface or a filtered emitter and so adds nothing.</summary>
    public Vector3 GroundBlowNormal;

    /// <inheritdoc cref="GroundBlowNormal"/>
    public float GroundBlowDistM;
}

/// <summary>
/// Arcade flight dynamics parameterized by the original game's zrdr stats.
/// A velocity-vector model in arcade clothing: thrust, drag, gravity and lift
/// integrate on the velocity vector (so a vertical zoom tail-slides out through
/// zero speed instead of hanging). Lift is a DEMAND rather than a fraction of
/// gravity: the airflow is blended toward the nose over the authored
/// <c>liftAOAs</c> cosine window, the difference against the true velocity times
/// <c>lift_accel_rate</c> plus <c>nom_gravity</c> on world-up is projected onto
/// the body X/Y plane, and the wings deliver that acceleration up to a hard load
/// factor and the aerodynamic ceiling (see <see cref="LiftGMax"/>). Level flight
/// at zero incidence therefore cancels weight as an algebraic identity, not as a
/// tuned equilibrium. The arcade handling on top is the flight path chasing the
/// nose (alignment lag, exposed each step as <see cref="Alpha"/>).
/// In a knife-edge the nose sags and keeps sagging, out of the decoded bank
/// coupling and the weathervane rather than out of any term written for it —
/// see <see cref="KnifeAlignFloor"/>. Below stall speed the nose is additionally pulled toward
/// world-down and cannot be raised over the horizon. Drag is the original's
/// parabolic polar in MACH (see <see cref="DragPolarScale"/>) — there is no
/// induced-drag term at all — and thrust is its Mach curve times a LINEAR throttle
/// lever (see <see cref="ThrustAccelAt"/>), scaled by the nose's attitude so a climb
/// is penalised and a dive rewarded (see <see cref="AttitudeThrustScale"/>).
/// Gravity acts at full strength in every attitude. Neither carries a fitted constant, and
/// the level-speed equilibrium is simply where the two cross: that lands within 1%
/// of the authored fd_speed for nine of the eleven airframes without anything being
/// tuned to make it. Rudder authority follows the original's own authored speed
/// table (see <see cref="YawAuthorityAt"/>) — YAW ONLY; pitch and roll carry
/// their own, different authority curves. Bank additionally couples straight into
/// yaw and pitch rate (see <see cref="BankYawCoupling"/>), the original's
/// coordinated-turn cheat, and <c>return_rate</c> is a restoring torque onto the
/// flight path rather than extra damping (see <see cref="WeathervaneHalfAngle"/>),
/// which makes the rotational response a spring-damper instead of a lag. The torque/
/// damping/inertia/speed numbers come straight from vehicle.json 'dynamics';
/// the scale constants marked TUNE are ours, adjusted against playtests — except
/// the three *Tune rates, which are pinned to measurements of the original
/// decoded from cockpit-gauge video and must not be retuned by feel.
/// An instance flies one of the original's TWO force paths, fixed at construction — see
/// <see cref="UsesAiForcePath"/> for the three places the AI one diverges.
/// </summary>
public sealed class FlightModel
{
    public Vector3 Position;
    public Basis Attitude = Basis.Identity;       // body→world; nose −Z, up +Y (Godot frame)
    public Vector3 BodyRates;                     // rad/s: x pitch(+up), y yaw(+left), z roll(+left)
    public Vector3 VelocityDir = Vector3.Forward;
    public float Speed;                           // m/s along VelocityDir
    public float Throttle;
    // deg: angle(nose, VelocityDir) at frame start, i.e. before this step's forces move
    // VelocityDir — see Step()'s "α" comment. An emergent LAG from the nose-chase, not a modelled
    // aerodynamic incidence. Reported for instruments only — no force term reads it.
    public float Alpha;
    // The demanded load factor in G at this step — the length of the lift demand's body X/Y
    // projection over StandardG — reported BEFORE the ±5/9 clamp and the aerodynamic ceiling below
    // it. Instruments only; no force term reads it. Pre-clamp is deliberate: it is the most
    // generous reading of "the G this aircraft is pulling", which makes it the right quantity to
    // measure the authored highGs/lowGs control limiters against (ControlLimiterTests).
    public float LoadFactorDemand;

    // Thrust available, per unit of engine power and reference area. There is NO fitted constant
    // here: every number is read out of the original's own thrust-available routine.
    //
    //   V_ref  = (ThrustVRefSlope·M + ThrustVRefMach) · a        a reference speed, ft/s
    //   q_ref  = ½·ρ·V_ref²                                      lb/ft², same dense band as lift
    //   C_ref  = DragPolarScale · (DragPolarParasite − M/60)     the parasite drag coefficient,
    //                                                            with a small linear Mach trim
    //   avail  = q_ref · C_ref / (M · ThrustPowBase^(ThrustPowMach·M))
    //   Thrust = EnginePower · RefArea · avail · throttle        in weight units
    //
    // Read it as "the parasite drag force the airframe would feel at V_ref, divided by Mach" — a
    // constant-power propeller form, but with the reference speed TRACKING the current speed rather
    // than sitting at a fixed design point. That is why the curve does not fall like P/V: q_ref
    // grows as M², so the net is roughly linear in M — available thrust RISES with speed (≈ +35 %
    // between 150 and 500 mph on this atmosphere). What falls with speed in this model is the
    // thrust MARGIN, because drag grows as M² faster than thrust grows as M.
    //
    // Scaling is EnginePower × RefArea — the original's own arrangement, and the dimensionally
    // consistent one: drag carries the same q·RefArea, so RefArea cancels out of the level
    // equilibrium and top speed depends only on EnginePower/DragFactor. Force → acceleration is
    // × StandardG / Weight, the conversion lift and drag already share.
    // ⚠ EnginePower is the plane's OWN stock engine row, which is always the Lvl-2 row (the
    // Bloodhawk's `engine` is 11, power 0.62). Deriving anything from the Lvl-1 row inflates it
    // ~32 %.
    // ⚠ Mach is floored at ThrustMachFloor before ANY of this — the 1/M is a real singularity at
    // rest, and this floor is the original's own guard, not one invented here.
    private const float ThrustMachFloor = 0.1f;
    private const float ThrustVRefSlope = 0.84f;
    private const float ThrustVRefMach = 0.112f;
    private const float ThrustMachTrim = 1f / 60f;
    private const float ThrustPowMach = 1.41f;
    // Base of the Mach-dependent divisor: 1.33 × the atmosphere's own scale factor, which for the
    // dense band (the operative one — see the atmosphere constants) is 0.98842078.
    private const float ThrustPowBase = 1.33f * 0.98842078f;

    // Available thrust is then scaled by the NOSE'S ATTITUDE, before the engine-power and
    // reference-area scaling. An arcade term with no aerodynamic justification, and one no video fit
    // could ever have recovered — it would have been absorbed into gravity or drag and then failed in
    // the opposite manoeuvre.
    //
    //   a     = the world-up component of the body Z axis, i.e. −nose.Y  (see AttitudeThrustScale)
    //   scale = (1 + AttitudeThrustBoth·a) · (a ≤ 0 ? 1 + AttitudeThrustUp·a : 1)
    //
    // So a climb LOSES thrust (0.6612× pointing straight up) and a dive GAINS it (1.24× straight
    // down): two coefficients, the second one-sided. Both are branchless immediates in the
    // original's force accumulator, applied to the curve above and to the throttle lever together.
    private const float AttitudeThrustBoth = 0.24f;
    private const float AttitudeThrustUp = 0.13f;

    // The hard lift clamp, in G — a LOAD FACTOR, never an angle. The wings will not deliver more
    // than this however hard the demand asks, and the clamp is on the demanded acceleration, so
    // re-deriving it as an incidence limit gives a model that looks right at small inputs and
    // diverges at the limits.
    // ⚠ Distinct from the authored highGs/lowGs control limiters, which are a WIDER pair
    // ([9, 15] / [−6, −9] in this install) and therefore sit at or past this clamp — they can never
    // engage before lift is already capped here. Do not fold the two together. Neither they nor the
    // authored maxAOA (46°) is reachable on any of the eleven airframes, so neither is implemented
    // (D33; ControlLimiterTests asserts each airframe's measured peaks against its OWN loaded
    // thresholds, so a data edit or per-plane override that brings one into reach fails the suite
    // rather than passing silently). If one ever does, the asymmetry is the thing to get right and
    // docs/org/flightModel.md has it: the original gates only input OPPOSING the current rotation,
    // so the limiter damps recovery from a departure rather than entry into one.
    // ⚠ The demanded load factor is the LENGTH of the projected demand, so it is never negative and
    // the lower bound is unreachable in practice. It is written out because it is what the model
    // clamps to, not because this code path can reach it.
    private const float LiftGMin = -5f;
    private const float LiftGMax = 9f;

    // The aerodynamic ceiling on the delivered lift coefficient: C_L ≤ ClMaxStatic − ClMaxMach·Mach.
    // This — not the load clamp — is what makes a slow aircraft unable to carry its own weight, so
    // it is the term the stall speed falls out of, and it is the only place RefArea and air density
    // enter lift at all (below the ceiling lift is independent of both).
    private const float ClMaxStatic = 0.75f;
    private const float ClMaxMach = 0.15f;

    // Atmosphere. A single band with no altitude gradient: the aerodynamic intermediates run in
    // imperial (ft/s, slug/ft³, lb/ft²) and only the results come back to metric.
    // ⚠ The thin band the original also carries is NOT the operative one — it puts the stall of a
    // 3500/335 airframe at 309 mph against this band's 75.5 mph, which is what settles the choice.
    private const float AirDensitySlugPerFt3 = 2.2688e-3f;
    private const float SpeedOfSoundFps = 1109.5f;
    private const float FeetPerMetre = 3.28084f;
    private const float MetresPerFoot = 0.3048f;
    // The force/acceleration conversion the lift and gravity terms share: lift force is
    // (load factor × Weight) and acceleration is (force × StandardG / Weight), so a demanded load
    // factor of 1 is StandardG of acceleration. Gravity enters the demand as nom_gravity and leaves
    // it as nom_gravity, which is why level flight at zero incidence cancels weight identically.
    private const float StandardG = 9.82f;

    // The two stall thresholds are DIFFERENT numbers and neither is a TUNE. The nose-drop threshold
    // (isStalled(), below) is the aircraft's OWN computed stall speed — see StallSpeed. The STALL
    // lamp, by contrast, lights at a fixed 0.30 fd
    // (0.2989–0.2996 across four clips) — that split is unrelated to the nose-drop mechanism, so it
    // stays a fraction: the original's "Stall 0% Thrust no input" clip measured the warning leading
    // the Bloodhawk's break by 2.64 sim s / 14.9 mph, and nothing here touches that lamp or its
    // threshold.
    private const float StallWarnFrac = 0.30f;    // STALL lamp lights below this fraction of fd_speed
    private const float MaxDiveSpeedFrac = 1.75f; // numerical backstop, NOT a terminal speed — it is
                                                  // meant to catch only the loop energy pump or a dt
                                                  // spike, because a cap that binds replaces a
                                                  // measured terminal with a guess.
                                                  // It does NOT bind: the Bloodhawk's full-throttle
                                                  // 71° dive terminates at 1.11 × fd_speed on the
                                                  // aerodynamics alone, so the terminal dive is an
                                                  // emergent measurement again.
                                                  // Measured resting altitude cap — C1B IA1 footage, Bloodhawk only. NOT an energy
                                                  // limit: level full-throttle equilibrium is flat to ±0.3 mph right up to 15 m under this line,
                                                  // and holding a 22° nose-up pull against it gains no altitude at all (sub-foot over the clip's
                                                  // last 5 s) while airspeed bleeds instead — so the clamp deletes climbing velocity outright
                                                  // rather than fading thrust/lift/drag toward it.
                                                  // ⚠ Traced to ONE mission — do not assume this is global, per-chapter/zone, or per-aircraft.
    private const float AltitudeCapM = 2003f;
    // Numerical backstop (~140 ft), NOT a modelled spring — same role as MaxDiveSpeedFrac below.
    // The footage's zoom entries coast past the resting cap on their own pre-existing momentum before the
    // clamp above ever catches them, so this only needs to be at least as generous as the measured
    // 6712 ft apex; it exists to bound a runaway frame, not to shape the overshoot.
    private const float AltitudeCapOvershootM = 42.8f;
    private const float StallNoseRate = 1.0f;     // TUNE: rad/s toward world-down at full stall depth (× stall_mag)
    private const float KnifeAlignFloor = 0.35f;  // TUNE: fraction of the nose-chase that survives at 90°
                                                  // bank — the chase is the lift force turning the velocity,
                                                  // so it weakens with wing verticality (deeper knife-edge sag).
                                                  // ⚠ This is the ONE surviving use of wingVert, and it is kept
                                                  // on a measurement rather than on the decode: the original's
                                                  // knife-edge holds its nose 4.8° → 8.3° BELOW the flight path
                                                  // over 36 s, a gap that GROWS, and the chase is what sets that
                                                  // gap. Measured (Bloodhawk, 143 mph entry, +3 s → +36 s): at
                                                  // 0.35 the gap runs 2.9° → 1.2° and 36 s costs 1087 m; with
                                                  // wingVert retired (floor 1.0, the bank-independent reading) it
                                                  // collapses to 1.9° → 0.5° and the same hold costs 1334 m,
                                                  // against a measured 540. Every knife-edge observable moves the
                                                  // wrong way without it. The decode does not contradict that:
                                                  // it is silent here, because this explicit kinematic chase is
                                                  // the remake's arcade handling and the original has no such
                                                  // term — its "lift is bank-independent" applies to the LIFT
                                                  // demand above, which no longer reads wingVert at all.
                                                  // ⚠ Do NOT close the remaining gap by lowering this. Lowering
                                                  // it moves every row the right way and still cannot reach the
                                                  // footage, because the excess is in the ROTATION rate — the
                                                  // nose drifts 1.09 °/s against a measured 0.69 and the heading
                                                  // sweeps 1.7 °/s against 0.68, both ≈1.6× fast, the same ≈1.6×
                                                  // by which the sustained banked pull is fast. Retuning here
                                                  // would hide a rotation error inside a chase constant — and it
                                                  // runs into a real boundary: at 0.10 the knife-edge α peaks at
                                                  // 5.36°, past liftAOAs[0] = 5°, so the airflow blend starts
                                                  // engaging in a knife-edge, which no capture supports.

    // Ground blow's two constants that are NOT in player.json (the three that are live on
    // PlaneStats). Both are decoded, neither is a TUNE: the 0.05 is an immediate in the player
    // branch, and the 2.0 is a global whose only writer is the original's `gbc` debug console
    // command, so no data key can move it.
    private const float GroundBlowIntoFactor = 0.05f;  // what a command INTO the obstacle is met with
    private const float GroundBlowVelocitySteer = 2f;  // 1/s at contact: velocity steered onto the nose

    // Drag is a parabolic polar in MACH:
    //
    //   C_D  = DragPolarScale · (DragPolarParasite + DragPolarLinear · M + DragPolarQuad · M²)
    //   Drag = q · RefArea · DragFactor · C_D          opposing the velocity, in weight units
    //
    // All three coefficients and the 0.73 scale are the same for every aircraft — airframes differ
    // only through the authored `drag_factor` and `ref_area`.
    //
    // ⚠ The variable is Mach, NOT the lift coefficient. The original's drag routine takes the lift
    // result as an argument and never reads it — so this model has **no induced drag whatsoever**,
    // and a pull costs speed only through the lift vector's own backward tilt in the force sum.
    // Reading the same three coefficients as a C_L polar is the natural mistake (they look exactly
    // like one) and it gives a drag FLOOR that is speed-independent at fixed load factor, which no
    // measurement of the original supports.
    //
    // ⚠ The curve conflicts with four zero-thrust points measured off original footage
    // (0.36/1.11/2.82/3.74 m/s² at
    // x = V/fd_speed = 0.25/0.35/0.46/0.50): in Mach it gives 1.3/3.0/6.2/7.7 — the right *shape*
    // (a 5.9× rise over the span against the measured 10.4×) but ≈2–3.6× too strong. That is NOT a
    // units error: the force→acceleration conversion was re-read from the executable and is exactly
    // this file's — veh_weight parsed, copied and divided raw, no lb/kg factor anywhere — so there
    // is no missing constant to implement and none to tune (a scale that fixed the decel would
    // break the accel row the same footage pins). The deficit is a near-constant ΔC_D ≈ 0.11, and
    // it stands RECORDED as a decode-vs-footage conflict (docs/org/flightModel.md, "The force
    // scale — settled"); the coefficients are the binary's and are not refitted to close it.
    //
    // Because thrust grows as M and this grows as M², the two cross sharply, and that crossing —
    // not a normalization — is what puts the level equilibrium where it is. It lands within 1 % of
    // the authored fd_speed for nine of the eleven player airframes with nothing fitted.
    private const float DragPolarScale = 0.73f;
    private const float DragPolarParasite = 0.12f;
    private const float DragPolarLinear = 0.8f;
    private const float DragPolarQuad = 0.5f;

    // Per-axis control-rate calibration. Steady rate = torque · recInertia · Tune /
    // ang_momentum_damp (× eff on yaw), and a full 360° takes ≈ 1/damp spin-up + 2π/rate.
    // Fitted to stopwatch timings of the original, then confirmed against cockpit-gauge video of
    // it: 360° roll 2.05 s, sustained pitch ~33 °/s, full-rudder 360° 28.6 s — all three within a
    // few percent of what these values already gave, and none of the verdicts moves anywhere
    // inside the video's clock uncertainty. The video also settles what was an open question: the
    // original's pitch rate does NOT fall off with speed (37.9 / 33.7 / 30.7 / 36.5 °/s binned
    // over 120–280 mph round a loop, flat within the noise), so speed-independent pitch is right.
    // ⚠ The STEADY rates above are pinned; the TRANSIENT shape is a known divergence, narrowed but
    // still open. A square-wave pitch-cadence sweep of the original falls 42× between the
    // 1300 ms and 570 ms cadences; the same sweep driven into this model falls 23×. The weathervane
    // below took that from 20× to 23× — the right direction, about a sixth of the gap — so a
    // second-order response is part of the answer and not the whole of it. ⚠ Do NOT "fix" the
    // remainder by moving these constants: they set the STEADY rate, which matches, and a transient
    // chased through them breaks the thing that does.
    // ⚠ PitchTune and YawTune are refit here (0.75 → 0.89, 1.33 → 1.57) for exactly the opposite
    // reason — because the weathervane changed the steady rates and these are what re-pin them. A
    // sustained full-stick manoeuvre holds a real misalignment (α ≈ 18° pulling, β ≈ 8° on full
    // rudder), so the restoring torque opposes it and the un-refit rates came out 15% and 20% low.
    // The refit is Bloodhawk-pinned, as it always was; the other ten airframes have no measured
    // target and move with it.
    private const float PitchTune = 0.89f;        // TUNE: pinned to the measurements above
    private const float YawTune = 1.57f;          // TUNE: pinned against the authored yaw curve below
    private const float RollTune = 2.12f;         // TUNE: pinned — untouched, the weathervane cannot
                                                  // reach the roll axis (its torque is ⊥ the nose)

    // Bank coupling — the original's coordinated-turn cheat, and the only part of its rotation that
    // no airframe authors. Two float constants compiled into the executable and writable only from
    // its own developer console (`fall_off` = 0.205 → yaw, `bank_off` = 0.165 → pitch), so no data
    // file carries them and none ever will. Both key off how vertical the WINGS are:
    //
    //   yaw   += BankYawCoupling   · (starboard·up)                   signed — bank left yaws left
    //   pitch += BankPitchCoupling · |starboard·up|                   unsigned — any bank pulls up
    //          + BankYawCoupling   · |bodyUp·up|   while INVERTED     (bodyUp·up < 0 only)
    //
    // ⚠ The inverted term reuses the YAW constant on the PITCH axis — it is not a third number, and
    // it peaks wings-level inverted where the bank term is exactly zero, so an aeroplane on its back
    // is pulled toward the ground instead of flying hands-off. Reading "an extra contribution when
    // inverted" as a separate coefficient, or as an addition to the yaw term, both give a model that
    // is right upright and wrong on its back.
    // Each term enters the same accumulator the stick commands do — so it is damped identically —
    // and carries that axis' RecInertia, which is the only scaling the original applies downstream.
    // It does NOT carry the axis' *Tune: those calibrate STICK authority against measured video and
    // are ours, and extending one to a decoded constant would be tuning it. The alternative
    // (×Tune, preserving the binary's coupling:full-stick ratio) was measured — it moves the
    // Bloodhawk's settled turn 255.6 → 257.4 mph and its sink 1.66 → 2.03 ft/s, i.e. past the
    // measured sink bound — so the literal read is also the one the measurements prefer.
    private const float BankYawCoupling = 0.205f;
    private const float BankPitchCoupling = 0.165f;

    // The weathervane: the authored `return_rate` is a RESTORING TORQUE toward the velocity vector,
    // not extra damping on a released axis. It enters the same accumulator the stick and the bank
    // coupling feed, so it carries that axis' RecInertia and is damped by ang_momentum_damp — but
    // because it is a torque proportional to displacement rather than to rate, the pair is a
    // SPRING-DAMPER (second order), where folding return_rate into the damping coefficient gave a
    // first-order lag.
    //
    //   ω += ReturnRate · WeathervaneHalfAngle · angle(nose, v̂) · unit(nose × v̂)
    //
    // ⚠ The angle is HALVED, and the halving is the binary's rather than a simplification of it:
    // the original builds the shortest-arc quaternion from the nose onto the unit velocity and then
    // converts it to a rotation vector through a quaternion-log helper, which returns
    // atan2(|q.v|, q.w) · unit(q.v) — the HALF angle, never doubled back. Reading it as the full
    // misalignment doubles the spring rate.
    // ⚠ The axis is perpendicular to the nose by construction, so the ROLL component is identically
    // zero at every attitude: a weathervane cannot touch bank, and it cannot reach any roll
    // measurement.
    // ⚠ It vanishes identically when the nose is on the velocity vector, which is what keeps level
    // cruise untouched — by construction, not by scale.
    // ⚠ The original applies this to the PLAYER aircraft only, and Step gates it on
    // UsesAiForcePath (C22). The live guard is `cmp esi, [0x71c298]` at 0x48cd3e inside
    // FUN_0048c470, jumping the whole block (0x48cd3e–0x48ce45) for anything that is not the single
    // global player object. WeathervaneTorque() itself is UNGATED — it is the law, and an
    // instrument or a test may sample it on either path; the gate is on whether Step sums it in.
    private const float WeathervaneHalfAngle = 0.5f;

    // The AI's forward-velocity floor: after integration, and for AI aircraft only, the velocity's
    // component along the NOSE is raised to at least 10 mph by adding along the nose, leaving the
    // perpendicular components untouched, and |v| is recomputed from the result. Live at
    // FUN_0048e580 0x48e95e–0x48e998, guarded by `cmp edi, [0x71c298]` at 0x48e925, against the
    // negated constant at 0x608128 (the original tests the m[2] = −nose component against −4.4704,
    // which is the same comparison with both signs flipped). One-sided: it only ever raises.
    // ⚠ It is NOT a floor on Speed, and the two are different in a dive or a sideslip — a plane
    // dropping at 20 m/s with its nose on the horizon has ample speed and no forward velocity at
    // all, and the original pushes it forward while a Speed clamp would do nothing.
    private const float AiNoseSpeedFloor = 4.4704f;

    /// <param name="aiForcePath">Which of the original's two force paths this instance flows — see
    /// <see cref="UsesAiForcePath"/>. ⚠ Optional, and it defaults to the PLAYER path, so a
    /// production construction site added later gets the player plant silently. Two sites pass it
    /// today (<c>FlightRigAssembler</c>, <c>AiAircraftSpawner</c>); a third one must pass it too.
    /// The default exists for the ~18 test sites that construct a plant with no session around
    /// them, not as a statement about what a new caller wants.</param>
    public FlightModel(PlaneStats stats, bool aiForcePath = false)
    {
        Stats = stats;
        UsesAiForcePath = aiForcePath;
        StallSpeed = ComputeStallSpeed(stats);
    }

    public PlaneStats Stats { get; }

    /// <summary>Whether this plant flows the original's AI force path rather than its player one.
    /// The original selects between them INSIDE its force function, on a pointer compare against
    /// the single global player object (<c>cmp esi, [0x71c298]</c> at <c>0x4916fe</c>, guarding the
    /// weathervane block; <c>docs/org/flightModel.md</c>'s "Weathervane centring"). We cannot copy
    /// that test: it presumes one player, and this engine flies up to four in splitscreen, so the
    /// selection is made once per aircraft at construction from <c>IsHumanPiloted</c> instead. Same
    /// two paths, a different way of choosing which one an aircraft is on.
    /// <para>⚠ Named for the PATH, not for the pilot. An AI-flown aircraft put back on the player
    /// path (the temporary <c>--no-ai-plant</c> A/B switch) is still AI-flown; nothing downstream of
    /// this may read it as "is this an AI aircraft" — <see cref="FlightController.IsHumanPiloted"/>
    /// answers that.</para>
    /// <para>⚠ Immutable by construction. A plant that could change path mid-flight would make a
    /// golden shot or a suite run unreproducible, since the trajectory would depend on WHEN the
    /// switch happened rather than on the inputs.</para>
    /// <para>Three divergences hang off it today, all in <see cref="Step"/> and each carrying its
    /// own live address (C22): the airflow blend is skipped so the wind always comes straight down
    /// the nose, the weathervane torque is not summed in, and the post-integration velocity carries
    /// the <see cref="AiNoseSpeedFloor"/>. There is no density branch — A1 disproved it, the
    /// atmosphere call is shared and unbranched, so both paths fly the dense band. C23 adds the
    /// ground blow.</para></summary>
    public bool UsesAiForcePath { get; }

    /// <summary>Airspeed as a fraction of fd_speed — the single stall-proximity scale both stall
    /// thresholds are measured on, and the one the STALL lamp's blink rate ramps over. Every stall
    /// cue derives from this; nothing recomputes its own margin.</summary>
    public float StallFraction => Stats.FdSpeed > 0f ? Speed / Stats.FdSpeed : 0f;

    /// <summary>The speed (m/s) below which the wings' maximum available lift — the SAME aerodynamic
    /// ceiling <see cref="Step"/> caps lift with, <c>ClMaxStatic − ClMaxMach·Mach</c> — can no longer
    /// equal the aircraft's own weight: the solution of <c>clMax(V)·q(V)·RefArea = VehWeight</c>, a
    /// LOAD FACTOR OF EXACTLY 1 (not <c>nom_gravity / StandardG</c> ≈ 2.04, what level flight itself
    /// demands to cancel this install's arcade gravity — the two conventions differ by
    /// √(nom_gravity/StandardG) ≈ 1.43×). The decode's own worked example settles which one the
    /// binary actually compares against: it reproduces 75.5 mph for the fallback aircraft
    /// (veh_weight 3500, ref_area 335) and 309 mph for the same aircraft under the wrong atmosphere
    /// band — BOTH numbers match only the bare-Weight (1 G) read; the nom_gravity-scaled read gives
    /// 109/447 mph instead, which the decode never quotes. So 1 G is what is coded, not a
    /// simplification of it.
    /// ⚠ Evaluated against a REAL airframe rather than the fallback numbers, this surfaces a residual
    /// the coincidence was hiding: the Bloodhawk's own data (1900/330) computes a 56.5 mph stall
    /// against the video-measured ~76 mph nose-drop ("Stall 0% Thrust no input" clip). A fixed
    /// 0.25·fd_speed matches that footage only because 0.25 × the BLOODHAWK's fd_speed
    /// happens to sit close to the FALLBACK aircraft's own stall speed, not the Bloodhawk's (see
    /// docs/org/flightModel.md). Recorded as a decode-vs-footage conflict, not
    /// papered over by switching the G convention to fit one clip.</summary>
    public float StallSpeed { get; }

    /// <summary>How much of the available thrust the nose's attitude leaves: 1 wings-level and nose
    /// on the horizon, 0.6612 pointing straight up, 1.24 pointing straight down. A climb is
    /// PENALISED and a dive rewarded — the opposite of a climb-retention term, and the reason the two
    /// could not both stand.
    ///
    /// <para>⚠ The argument is the world-up component of the BODY Z AXIS, and the nose points along
    /// <b>−Z</b>: pass <c>Attitude.Z.Y</c>, which is <c>−nose.Y</c> and therefore NEGATIVE in a
    /// climb. This is the one place in the force path where a dropped sign produces flight that still
    /// looks entirely plausible — it merely swaps climb for dive — so <c>AttitudeThrustTests</c> reads
    /// the term back out of the integrator and fails under the flip, rather than leaving it to
    /// review.</para></summary>
    public static float AttitudeThrustScale(float bodyZUp)
    {
        float a = Mathf.Clamp(bodyZUp, -1f, 1f);
        float scale = 1f + AttitudeThrustBoth * a;
        return a <= 0f ? scale * (1f + AttitudeThrustUp * a) : scale;
    }

    /// <summary>Thrust acceleration along the nose, m/s², at an airspeed and lever position — the
    /// original's thrust-available curve times the lever, LINEARLY. ⚠ The attitude scale is NOT
    /// included: <see cref="AttitudeThrustScale"/> is applied by the caller, so this stays the bare
    /// curve an instrument can sample. Exposed so an instrument can report the curve without
    /// re-deriving it; the step below calls the same method.</summary>
    public float ThrustAccelAt(float speed, float throttle)
    {
        float mach = Mathf.Max(ThrustMachFloor, speed / (SpeedOfSoundFps * MetresPerFoot));
        float vRefFps = (ThrustVRefSlope * mach + ThrustVRefMach) * SpeedOfSoundFps;
        float qRef = 0.5f * AirDensitySlugPerFt3 * vRefFps * vRefFps;
        float cRef = DragPolarScale * (DragPolarParasite - mach * ThrustMachTrim);
        float avail = qRef * cRef / (mach * Mathf.Pow(ThrustPowBase, ThrustPowMach * mach));
        return Stats.VehWeight > 1e-3f
            ? Stats.EnginePower * Stats.RefArea * avail * throttle * StandardG / Stats.VehWeight
            : 0f;
    }

    /// <summary>Rudder authority at an airspeed — the original's authored piecewise speed table
    /// (<see cref="PlaneStats.YawFadeIn"/>/<see cref="PlaneStats.YawMax"/>/
    /// <see cref="PlaneStats.YawFadeOut"/>/<see cref="PlaneStats.YawLowSpeed"/>/
    /// <see cref="PlaneStats.YawHighSpeed"/>): a low-speed floor, ramping LINEARLY to full
    /// authority at yaw_max, then declining LINEARLY to a high-speed floor at yaw_fade_out and
    /// holding there. Deliberately NOT monotone, and NOT flat past the knee — the executable's
    /// compiled fallbacks (knee at 45 mph, flat 0.1 beyond) are a different, wrong reading; this
    /// install authors a curve that keeps declining out to 400 mph. YAW ONLY — pitch and roll run
    /// their own, different authority curves and are untouched here.
    /// Exposed so an instrument can sample the curve without re-deriving it; Step calls the same
    /// method.</summary>
    public float YawAuthorityAt(float speed)
    {
        var s = Stats;
        if (speed <= s.YawFadeIn)
            return s.YawLowSpeed;
        if (speed <= s.YawMax)
            return s.YawMax > s.YawFadeIn
                ? Mathf.Lerp(s.YawLowSpeed, 1f, (speed - s.YawFadeIn) / (s.YawMax - s.YawFadeIn))
                : 1f;
        if (speed <= s.YawFadeOut)
            return s.YawFadeOut > s.YawMax
                ? Mathf.Lerp(1f, s.YawHighSpeed, (speed - s.YawMax) / (s.YawFadeOut - s.YawMax))
                : s.YawHighSpeed;
        return s.YawHighSpeed;
    }

    /// <summary>The weathervane's restoring torque for the current attitude and flight path, in
    /// BODY axes and in the same units as the stick command (rad/s², before RecInertia and before
    /// the damping) — <c>return_rate · (α/2)</c> about the axis that swings the nose onto the
    /// velocity vector. Zero when the two are aligned, and its roll component is zero always.
    /// Exposed so an instrument or a test can read the torque without re-deriving it; Step calls the
    /// same method — on the PLAYER path only, and this method is not itself gated, so it answers
    /// "what would the weathervane do here" for either path. See
    /// <see cref="WeathervaneHalfAngle"/> for the decode and its traps.</summary>
    public Vector3 WeathervaneTorque()
    {
        var nose = -Attitude.Z;
        var axis = nose.Cross(VelocityDir);
        float sin = axis.Length();
        if (sin < 1e-6f)
            return Vector3.Zero;
        // atan2 of the cross/dot pair, so the angle stays exact out to a fully reversed flight path
        // where an Acos of the dot alone loses precision and a small-angle read is simply wrong.
        float angle = Mathf.Atan2(sin, nose.Dot(VelocityDir));
        var world = axis * (Stats.ReturnRate * WeathervaneHalfAngle * angle / sin);
        return Attitude.Transposed() * world;
    }

    public void Reset(Vector3 position, Basis attitude, float speed, float throttle)
    {
        Position = position;
        Attitude = attitude.Orthonormalized();
        BodyRates = Vector3.Zero;
        VelocityDir = -Attitude.Z;
        Speed = speed;
        Throttle = throttle;
    }

    public void Step(FlightInput input, float dt)
    {
        Throttle = Mathf.Clamp(input.Throttle, 0f, 1f);
        var s = Stats;

        // Tunable scale constants, read through Config so config.json can override them without a
        // recompile; each falls back to the in-code const default (and the rationale beside it)
        // above. Read unconditionally here — once per step — so every key registers even on a frame
        // that never enters the stall or knife-edge branches below, which keeps --dump-config's
        // template complete and the orphan/missing checks honest.
        float pitchTune = Config.GetFloat("flightModel.pitchTune", PitchTune);
        float yawTune = Config.GetFloat("flightModel.yawTune", YawTune);
        float rollTune = Config.GetFloat("flightModel.rollTune", RollTune);
        // Read here as well as at its own site (IsStallWarned, which Step never calls) purely so the
        // key registers on a launch that never flies — --dump-config's template and the orphan check.
        _ = Config.GetFloat("flightModel.stallWarnFrac", StallWarnFrac);
        float stallNoseRate = Config.GetFloat("flightModel.stallNoseRate", StallNoseRate);
        float knifeAlignFloor = Config.GetFloat("flightModel.knifeAlignFloor", KnifeAlignFloor);
        float liftGMin = Config.GetFloat("flightModel.liftGMin", LiftGMin);
        float liftGMax = Config.GetFloat("flightModel.liftGMax", LiftGMax);
        float altitudeCapM = Config.GetFloat("flightModel.altitudeCapM", AltitudeCapM);
        float altitudeCapOvershootM = Config.GetFloat("flightModel.altitudeCapOvershootM", AltitudeCapOvershootM);

        // --- rotation: torque·recInertia vs momentum damping (all from the dynamics block), plus
        // two decoded torques into the same accumulator — the bank coupling and the weathervane.
        // Yaw authority follows the original's authored speed table (see YawAuthorityAt) — a
        // declining function of speed — the original's own shape, not a fitted stand-in. Pitch and
        // roll carry no HIGH-speed fade here: the original fades neither with speed. Its
        // high_speed_pitch_fade IS authored, at [1000, 1001] mph, and is unreachable — past even this
        // model's own hard dive ceiling of 1.75 × fd_speed (528.5 mph at its highest, the Bloodhawk)
        // on all eleven airframes — so it is deliberately NOT implemented; do not add it "for
        // completeness". See docs/org/flightModel.md's unreachability table.
        // ⚠ They do fade at LOW speed in the original and do not here — it ramps roll and pitch
        // authority from 0 at turn_fade_in (10 mph) to 1 at turn_fade_out (50), flat above.
        // Traced and corroborated from the controls, but unimplemented; see
        // docs/org/flightModel.md. Do not read the line above as "roll never fades" — that
        // misreading is what had turn_fade_* filed as a bank effect.
        float yawEff = YawAuthorityAt(Speed);
        var cmd = new Vector3(
            Mathf.Clamp(input.Pitch, -1f, 1f) * s.PitchTorque * s.RecInertia.X * pitchTune,
            Mathf.Clamp(input.Yaw, -1f, 1f) * s.RudderTorque * s.RecInertia.Y * yawTune * yawEff,
            Mathf.Clamp(input.Roll, -1f, 1f) * s.RollTorque * s.RecInertia.Z * rollTune);

        // Bank coupling (see the two constants): banking yaws the nose the way the wings point and
        // pulls it up, with a further pull once the wings are past vertical. Read off the attitude
        // this frame ENTERED with, alongside the stick command and before anything rotates it, which
        // is the original's own ordering.
        float bankComponent = Attitude.X.Dot(Vector3.Up);
        float bodyUpComponent = Attitude.Y.Dot(Vector3.Up);
        cmd.Y += BankYawCoupling * bankComponent * s.RecInertia.Y;
        cmd.X += (BankPitchCoupling * Mathf.Abs(bankComponent)
                  + (bodyUpComponent < 0f ? -BankYawCoupling * bodyUpComponent : 0f))
                 * s.RecInertia.X;

        // Weathervane (see WeathervaneHalfAngle): a restoring torque that swings the nose onto the
        // velocity vector, half the misalignment angle about the axis that closes it. Read off the
        // attitude and the velocity direction this frame ENTERED with, alongside the stick command
        // and the bank coupling, which is the original's own ordering.
        // AI skips the whole block (0x48cd3e), so an AI aircraft's nose is never pulled back onto
        // its flight path and its rotation is a first-order lag again rather than a spring-damper.
        if (!UsesAiForcePath)
            cmd += WeathervaneTorque() * s.RecInertia;

        // Ground blow (see GroundBlowTerm): the nose-forward probe biasing the command away from
        // what it is closing on. Last of the three torques and after both of the above, which is
        // the original's own ordering — it reads the accumulator the stick, the bank coupling and
        // the weathervane have already been summed into, and multiplies THAT.
        cmd += GroundBlowTerm(input, cmd, out float groundBlowSteer);

        // Damping is the authored ang_momentum_damp alone. return_rate is NOT a damping term — it is
        // the weathervane torque above, applied whether or not a stick is deflected.
        // The original's own order: accumulate this tick's torque onto BodyRates
        // FIRST, THEN decay the WHOLE result — the freshly-added torque included — by
        // exp(−dt·ang_momentum_damp). That is an EXPONENTIAL decay, not an explicit-Euler linear
        // subtraction. The two forms are the same to first order in dt per
        // step (exp(−x) = 1 − x + O(x²), matching the linear factor (1 − x) exactly at O(x)), but the
        // linear form is unstable at a large step: once dt·damp > 2 its factor (1 − dt·damp) goes
        // below −1 and BodyRates flips sign and grows every tick, where exp(−dt·damp) stays in
        // (0, 1) for any dt ≥ 0 and only ever decays.
        BodyRates += cmd * dt;
        BodyRates *= Mathf.Exp(-dt * s.AngMomentumDamp);

        // stall: below stall speed the nose is pulled toward WORLD-down (a great-circle
        // rotation about the nose×down axis — no twist about the nose, works at any
        // attitude including inverted). Deep-stall rate exceeds full-elevator authority
        // (~0.58 rad/s steady at the calibrated rates), so the drop is decisive
        // until airspeed recovers.
        bool stalled = isStalled();
        float noseYBefore = (-Attitude.Z).Y;  // the nose's world elevation entering this frame
        if (stalled)
        {
            float depth = 1f - Speed / StallSpeed;
            var noseNow = -Attitude.Z;
            var axis = noseNow.Cross(Vector3.Down);
            if (axis.LengthSquared() > 1e-8f)
            {
                float angle = Mathf.Min(s.StallMag * stallNoseRate * depth * dt,
                                        noseNow.AngleTo(Vector3.Down));
                Attitude = Attitude.Rotated(axis.Normalized(), angle).Orthonormalized();
            }
        }

        var omegaWorld = Attitude * BodyRates;
        float omega = omegaWorld.Length();
        if (omega > 1e-6f)
            Attitude = Attitude.Rotated(omegaWorld / omega, omega * dt).Orthonormalized();

        // while stalled the nose can NOT be raised over the horizon, at any bank angle
        // (original behavior, user-observed): cap its world elevation at the
        // horizon — or where the frame started, if the stall caught it nose-high, so it
        // can only come down from there. Same great-circle rotation as the stall drop.
        if (stalled)
        {
            var noseAfter = -Attitude.Z;
            float capY = Mathf.Max(0f, noseYBefore);
            if (noseAfter.Y > capY + 1e-5f)
            {
                var axis = noseAfter.Cross(Vector3.Down);
                if (axis.LengthSquared() > 1e-8f)
                {
                    float angle = Mathf.Asin(Mathf.Clamp(noseAfter.Y, -1f, 1f))
                                - Mathf.Asin(Mathf.Clamp(capY, -1f, 1f));
                    Attitude = Attitude.Rotated(axis.Normalized(), angle).Orthonormalized();
                }
            }
        }

        // There is deliberately NO knife-edge nose-sag term here. The decoded bank→yaw coupling
        // does that job: at 90° of bank the body yaw axis is horizontal, so a yaw rate IS a nose
        // sag, and the weathervane then pulls the nose further onto the falling flight path. That
        // gives the original's own shape — a drift with no equilibrium — and its onset: at the
        // original's +3 s sample the coupling alone reads −4.94° against a measured −4.9°, sinking
        // 5.7 ft/s against a measured 0.5. ⚠ Do not add a nose-sag term to deepen the knife-edge:
        // a bounded ≈4° drop keyed on wing verticality — the retired KnifeNoseSag/KnifeNoseRate pair
        // — stacked on top, takes that same sample to −7.28° and 12.7 ft/s, and being keyed on
        // 1 − |bodyUp·up| it fought every WINGS-LEVEL pull too, at up to 11.5 °/s.
        // The remaining divergence is that the whole banked rotation runs
        // ≈1.6× fast (see KnifeAlignFloor), and a second nose-down term would double-count what is
        // already there. See docs/org/flightModel.md.

        // --- translation: forces integrate on the velocity VECTOR (v = VelocityDir·Speed),
        // so the speed can pass through zero — a vertical zoom tail-slides out downward
        // instead of freezing mid-air at a clamped 0 (a plane visibly stopped in the air
        // while the HUD mph crept back up, user-reported).
        var nose = -Attitude.Z;

        // α = angle(nose, VelocityDir), read here — before this step's forces move VelocityDir —
        // so lift (below) and anything else keyed on the pull read the SAME value for the frame.
        // Hoisted out of the near-parallel pathDot check
        // below rather than replacing it: that one re-reads pathDot AFTER the translation update,
        // to decide whether Slerp's cross-product axis is well-conditioned for THIS frame's actual
        // chase, which is a distinct question from what α reports here.
        // ⚠ α is an emergent LAG in this model (the flight path chasing the nose at a finite rate),
        // not an aerodynamic state — do not describe it as modelled incidence. Near-parallel nose
        // and path is the normal cruise state, so this must stay well-defined as α → 0: Acos of a
        // clamped dot product is, unlike Slerp's axis, safe at zero.
        Alpha = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(nose.Dot(VelocityDir), -1f, 1f)));

        // --- lift: a DEMANDED acceleration the wings deliver, not a fraction of gravity.
        //
        // Step 1 — the oncoming airflow is faked toward the nose across the authored liftAOAs
        // window. Below the low edge the true velocity vector is used; past the high edge the real
        // airflow direction is discarded entirely and the wind is taken as coming straight down the
        // nose; between the two it is a LINEAR BLEND ON cos α, because the authored degrees are
        // cosined at load and the window is a cosine window (blending on the angle instead is a
        // different, subtly wrong curve). This is the large arcade assist that lets a hard-
        // manoeuvring aircraft behave as though it has no sideslip.
        // ⚠ The window is PLAYER-ONLY. The AI path skips it outright and takes the wind fully
        // nose-aligned at every incidence — the live guard is `cmp esi, ecx` at 0x48c520 (the
        // player object loaded at 0x48c502), jumping to 0x48c6e9, where the AI branch builds
        // −speed·m[2] and m[2] is −nose. So an AI aircraft flies at permanently zero incidence.
        // ⚠ That does NOT mean it pulls harder. The demand below is the swing onto this wind PLUS
        // weight, so with the flight path above the nose the fully-nose-aligned swing points down
        // and cancels part of the weight term: measured on the Bloodhawk at α = 8°, the AI demands
        // 0.26 G against the player's 2.04 G. Past liftAOAs[1] the two agree exactly, the player
        // being nose-aligned there too, so the whole divergence lives inside the window.
        var velocity = VelocityDir * Speed;
        float cosAlpha = Mathf.Clamp(nose.Dot(VelocityDir), -1f, 1f);
        float cosSpan = s.LiftAoaCosLo - s.LiftAoaCosHi;
        float windBlend = UsesAiForcePath
            ? 1f
            : cosSpan > 1e-6f
                ? Mathf.Clamp((s.LiftAoaCosLo - cosAlpha) / cosSpan, 0f, 1f)
                : (cosAlpha < s.LiftAoaCosLo ? 1f : 0f);
        var relativeWind = velocity.Lerp(nose * Speed, windBlend);

        // Step 2 — the demand: swing the velocity onto that airflow at the authored rate, and carry
        // weight on top. Only the part of it the wings can act through counts, so it is projected
        // onto the body X/Y plane (the nose axis is body Z), and the length of that projection is
        // the demanded LOAD FACTOR in G. ⚠ G, not degrees — this is an acceleration the wings are
        // asked to produce, and nothing here is an incidence angle.
        var demand = (relativeWind - velocity) * s.LiftAccelRate;
        demand.Y += s.Gravity;
        var noseAxis = Attitude.Z;
        var liftDir = demand - noseAxis * demand.Dot(noseAxis);
        LoadFactorDemand = liftDir.Length() / StandardG;
        float loadFactor = Mathf.Clamp(LoadFactorDemand, liftGMin, liftGMax);

        // Step 3 — cap the delivered force at the aerodynamic ceiling, C_L·q·RefArea in weight
        // units, which is the same as capping the load factor at C_L·q·RefArea / Weight. This is
        // where a slow aircraft stops being able to carry itself.
        float speedFps = Speed * FeetPerMetre;
        float dynPressure = 0.5f * AirDensitySlugPerFt3 * speedFps * speedFps;
        float mach = Speed / (SpeedOfSoundFps * MetresPerFoot);
        float clMax = Mathf.Max(0f, ClMaxStatic - ClMaxMach * mach);
        // q·RefArea, in weight units (lb/ft² × ft²) — the scale a coefficient converts to a force,
        // and the divisor the delivered C_L (drag, below) comes back out through.
        float qRefArea = dynPressure * s.RefArea;
        float loadCap = s.VehWeight > 1e-3f ? clMax * qRefArea / s.VehWeight : 0f;
        loadFactor = Mathf.Clamp(loadFactor, -loadCap, loadCap);
        var liftAccel = liftDir.LengthSquared() > 1e-12f
            ? liftDir.Normalized() * (loadFactor * StandardG)
            : Vector3.Zero;

        // How much of the wings' lift points vertically — 1 level OR inverted (arcade: inverted
        // flight still carries), 0 in knife-edge. Lift itself does NOT read it: the demand's body
        // X/Y projection is bank-independent by construction, which is the original's own
        // arrangement. Its one remaining reader is the nose-chase below — see KnifeAlignFloor for
        // the measurement that keeps it there.
        float wingVert = Mathf.Abs(Attitude.Y.Dot(Vector3.Up));

        // thrust pulls along the nose (its along-path share falls out of the vector sum —
        // a stalled plane falling nose-high needs no special case), is LINEAR in the throttle
        // lever, and is scaled by the nose's attitude: less climbing, more diving (see
        // AttitudeThrustScale). Drag opposes the motion: the parabolic polar in Mach (see the
        // constants). At v ≈ 0 drag → 0 and the stale direction there is harmless.
        // Gravity acts in FULL, and at full strength in every attitude — the lift demand above
        // already carries weight, and subtracting it twice is the trap the old cross-path fraction
        // was one half of. There is no climb-retention scale on it: the original's own gravity term
        // is a plain nom_gravity/9.82 × Weight with nothing attitude-dependent anywhere near it, and
        // what the original DOES scale by attitude is the thrust above — in the opposite direction.
        // The fitted ClimbGravityScale = 0.6 that used to sit here is retired with its config key: on
        // the post-B14 drag/thrust shapes it was making the sustained climb WORSE, the error it was
        // absorbing having moved (docs/verification.md METHOD-22).
        //
        // The polar's variable is MACH. A pull therefore costs speed only through the lift vector's
        // own backward tilt in the force sum below — there is no induced-drag term here at all, and
        // adding one (α-keyed, C_L-keyed or otherwise) is not this model.
        float cd = DragPolarScale
                   * (DragPolarParasite + DragPolarLinear * mach + DragPolarQuad * mach * mach);
        // Force (weight units) → acceleration is × StandardG / Weight, the same conversion lift uses.
        float dragAccel = s.VehWeight > 1e-3f
            ? qRefArea * s.DragFactor * cd * StandardG / s.VehWeight
            : 0f;
        var accel = nose * (ThrustAccelAt(Speed, Throttle) * AttitudeThrustScale(Attitude.Z.Y))
                    - VelocityDir * dragAccel
                    + Vector3.Down * s.Gravity
                    + liftAccel;
        var vel = VelocityDir * Speed + accel * dt;

        // The AI's nose-axis floor (see AiNoseSpeedFloor), applied here because the original applies
        // it here: after the velocity integration and before |v| is recomputed and the position
        // steps. It adds along the nose only, so the perpendicular components survive it, and the
        // speed that comes out is the length of the RESULT rather than a clamped scalar.
        if (UsesAiForcePath)
        {
            float alongNose = vel.Dot(nose);
            if (alongNose < AiNoseSpeedFloor)
                vel += nose * (AiNoseSpeedFloor - alongNose);
        }

        Speed = Mathf.Min(vel.Length(), MaxDiveSpeedFrac * s.FdSpeed);
        if (vel.LengthSquared() > 1e-8f)
            VelocityDir = vel.Normalized();

        // velocity chases the nose, weakening with wing verticality (in knife-edge the wings can't
        // lift the path back to the nose, so the sag equilibrium sits visibly below it — the
        // nose-drop). The RATE is the authored lift_accel_rate, the same quantity the lift demand
        // above swings the velocity onto the faked airflow with — it is data, not a tuning knob,
        // and it is speed-independent because the demand it stands for is (the acceleration grows
        // with speed, the resulting angular rate does not).
        // (Skip when path ≈ opposite the nose — slerp axis degenerates; gravity will
        // swing the path around within a few frames anyway.)
        // Ground blow's second, smaller effect rides on this same chase: the velocity direction is
        // steered toward the nose at GroundBlowVelocitySteer · S per second, in ADDITION to the
        // chase, and it is not weakened by wing verticality. Adding the rates is exact rather than
        // approximate — two exponential steers toward the same target compose as
        // exp(−a·dt)·exp(−b·dt) = exp(−(a+b)·dt).
        float align = s.LiftAccelRate
                      * (knifeAlignFloor + (1f - knifeAlignFloor) * wingVert)
                      + groundBlowSteer;
        // Near-parallel is the normal cruise state, and there Slerp is unusable: it builds its
        // rotation axis from the cross product, whose float error swamps a sub-degree angle, and
        // Godot then throws "Argument is not normalized" — which aborts the whole physics frame,
        // so a plane holding straight and level simply stopped flying (found while verifying
        // splitscreen; it bit single player exactly the same). Under ~2.5° a normalized
        // lerp is the same rotation to well under a thousandth of a degree, and needs no axis.
        float pathDot = nose.Dot(VelocityDir);
        if (align > 0f && pathDot > -0.999f)
        {
            float t = 1f - Mathf.Exp(-align * dt);
            VelocityDir = (pathDot > 0.999f
                ? VelocityDir + (nose - VelocityDir) * t
                : VelocityDir.Slerp(nose, t)).Normalized();
        }

        // hard altitude clamp: once at or above the resting cap, this frame's
        // climbing velocity is deleted outright rather than redirected into more horizontal speed —
        // a clamp on altitude, not an energy limit, so a sustained pull against it bleeds airspeed
        // instead of gaining height. A no-op below the cap by construction: thrust/lift/drag and
        // every branch above are untouched. Whatever follows once that bleed reaches the existing
        // stall thresholds is the model's own consequence, not a mechanism built here.
        if (Position.Y >= altitudeCapM && VelocityDir.Y > 0f)
        {
            var levelVel = VelocityDir * Speed;
            levelVel.Y = 0f;
            Speed = levelVel.Length();
            if (Speed > 1e-6f)
            {
                VelocityDir = levelVel.Normalized();
            }
        }

        Position += VelocityDir * Speed * dt;
        // Backstop, not a modelled spring (same role as MaxDiveSpeedFrac): bounds a runaway frame to
        // the measured ballistic overshoot rather than ever reproducing its shape.
        Position.Y = Mathf.Min(Position.Y, altitudeCapM + altitudeCapOvershootM);
    }

    /// <summary>Below the airframe's own computed <see cref="StallSpeed"/> — the aerodynamic stall
    /// the flight model flies. NOT the cue the STALL lamp shows: that one lights earlier (a fixed
    /// fraction of fd_speed), see IsStallWarned.</summary>
    public bool isStalled() => Speed < StallSpeed;

    /// <summary>Below the warning threshold (0.30 fd) — the STALL lamp, which leads the break by a
    /// measured 2.64 sim s / 14.9 mph.</summary>
    public bool IsStallWarned() =>
        StallFraction < Config.GetFloat("flightModel.stallWarnFrac", StallWarnFrac);

    // Solves clMax(V)·q(V)·RefArea = VehWeight for V at a load factor of 1 (see StallSpeed's doc for
    // why 1, not nom_gravity/StandardG). clMax's Mach term makes this implicit; a handful of
    // fixed-point passes converge to float precision because stall speeds sit well below the speed
    // of sound, so the correction off the Mach-free start is only a percent or two. Computed once per
    // instance — VehWeight/RefArea never change after construction.
    private static float ComputeStallSpeed(PlaneStats stats)
    {
        if (stats.VehWeight <= 1e-3f || stats.RefArea <= 1e-3f)
            return 0f;
        float vFps = Mathf.Sqrt(2f * stats.VehWeight / (ClMaxStatic * AirDensitySlugPerFt3 * stats.RefArea));
        for (int i = 0; i < 5; i++)
        {
            float mach = vFps / SpeedOfSoundFps;
            float clMax = Mathf.Max(0.05f, ClMaxStatic - ClMaxMach * mach);
            vFps = Mathf.Sqrt(2f * stats.VehWeight / (clMax * AirDensitySlugPerFt3 * stats.RefArea));
        }
        return vFps * MetresPerFoot;
    }

    /// <summary>Ground blow: the original's bias of the player's control response away from
    /// anything large the nose is closing on (docs/org/flightModel.md's "Ground blow"). The probe
    /// itself belongs to the caller, which has the world; this is the law it feeds.
    /// <para>With <c>n</c> the hit normal, <c>b</c> the backward body axis, <c>d</c> the distance to
    /// the hit and <c>c = dot(b, n)</c>: the surface must face back at the aircraft (<c>c &gt; 0</c>),
    /// proximity is <c>S = sqrt(c)·(elev − d)/elev</c> (1 at contact, 0 at the ray's end), and the
    /// escape axis is <c>V = normalize(n × b)·S</c>. The command's own component along <c>V</c> is
    /// then amplified when it points away from the surface and cut when it points into it, so this
    /// is a bias on the STICK, never a force and never a rate of its own.</para>
    /// <para>⚠ It returns a torque to add to the command accumulator, NOT to BodyRates, and carries
    /// no dt: the caller's <c>cmd * dt</c> is what makes it linear in dt, as the original's is. A dt
    /// applied here as well would make the whole effect vanish at a small step.</para>
    /// <para>⚠ The escape axis is built from a WORLD normal and is converted to the body frame here,
    /// once, before both the dot and the return. Skipping that gives a term that is right
    /// wings-level and wrong at every other attitude.</para>
    /// <para>⚠ A dead-on approach must get nothing: as <c>n → b</c> the cross product collapses and
    /// the term goes to zero. That is the original's "never saves a head-on collision", so the
    /// degenerate case is returned as zero rather than special-cased or renormalised.</para></summary>
    /// <param name="cmd">This step's command accumulator, with the stick, the bank coupling and the
    /// weathervane already summed in — the original reads exactly that.</param>
    /// <param name="velocitySteerRate">The second, smaller effect: the rate in 1/s at which the
    /// velocity direction is steered onto the nose, zero while the pilot commands into the
    /// obstacle (the original zeroes its proximity on that branch, which suppresses this).</param>
    private Vector3 GroundBlowTerm(in FlightInput input, Vector3 cmd, out float velocitySteerRate)
    {
        velocitySteerRate = 0f;
        var n = input.GroundBlowNormal;
        float elev = Stats.GroundBlowElev;
        if (n.LengthSquared() < 1e-12f || elev <= 0f)
            return Vector3.Zero;
        var b = Attitude.Z;                        // backward body axis, in world coordinates
        float c = b.Dot(n);
        if (c <= 0f)
            return Vector3.Zero;
        float proximity = Mathf.Sqrt(c) * (elev - input.GroundBlowDistM) / elev;
        if (proximity <= 0f)
            return Vector3.Zero;
        var axis = n.Cross(b);
        if (axis.LengthSquared() < 1e-12f)
            return Vector3.Zero;
        // ONE scaled axis, built once and used TWICE — once in the dot, once in the add. That is
        // where the second power of proximity comes from, and building it once is what stops it
        // from silently becoming a third.
        var v = Attitude.Transposed() * (axis.Normalized() * proximity);
        float p = cmd.Dot(v);
        velocitySteerRate = p < 0f ? 0f : GroundBlowVelocitySteer * proximity;
        // Both branches push along +v, away from the surface: commanding away is amplified by
        // 1 + mag·S², commanding into is cut to 1 − 0.05·mag·S² (halved at contact with the
        // authored 10) and never reversed.
        return v * ((p >= 0f ? p : GroundBlowIntoFactor * -p) * Stats.GroundBlowMag);
    }

}

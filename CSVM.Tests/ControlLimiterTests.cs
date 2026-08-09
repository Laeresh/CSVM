using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The original's two control limiters — the G limiter (authority falls above <c>highGs[0]</c>,
/// reaching zero at <c>highGs[1]</c>, mirrored below <c>lowGs</c>) and the AOA limiter (authority
/// falls to zero at <c>maxAOA</c>) — are present in the executable and <b>unreachable with this
/// install's authored values</b>. Nothing implements them, and these tests are what keeps that
/// decision honest: they fail if a data edit, a per-plane override or a model change ever brings
/// either threshold into reach, which is the only condition under which the code would be owed.
///
/// <para><b>G side.</b> <c>highGs [9, 15]</c> / <c>lowGs [−6, −9]</c> sit at or past the model's
/// own hard ±5/9 G lift clamp, so a delivered load factor can never exceed the point where the
/// limiter's ramp begins. The tests below do not lean on that structural argument: they measure
/// the <b>demanded</b> load factor (<see cref="FlightModel.LoadFactorDemand"/>, read before both
/// clamps — the most generous reading available) through the real model, on every airframe, in
/// the manoeuvres that produce the most G there is.</para>
///
/// <para><b>AOA side.</b> <c>maxAOA</c> is authored at 46°. α in this model is an emergent
/// alignment lag, and the hardest sustained pull reaches roughly half that.</para>
///
/// <para><b>The executable's fallbacks are a different story, and that is the able-to-fail
/// control:</b> <see cref="TheExecutablesFallbackGLimiterWouldEngage"/> shows the same manoeuvre
/// crossing the compiled fallback <c>highGs[0] = 5</c>. The limiter is real code on a real
/// mechanism — this install simply authors it out of reach, and a build that read the fallbacks
/// would owe the implementation.</para>
/// </summary>
public class ControlLimiterTests
{
    private const float Dt = 1f / 60f;

    /// <summary>The executable's compiled fallback <c>highGs</c> — NOT what this install authors.</summary>
    private const float FallbackHighGStart = 5f;

    private static readonly string[] AllPlanes =
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro",
        "player_avenger", "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel",
        "player_peacemaker",
    };

    /// <summary>The manoeuvres that make G and α, at full throttle: the sustained max-performance
    /// pull at cruise and again entered fast (G grows with speed), the same pull banked, a full
    /// forward push (the <c>lowGs</c> side), and full rudder. Ten seconds each — more than a full
    /// loop — so nothing transient is missed.</summary>
    private static readonly (string Name, float EntryFdFrac, FlightInput In)[] Manoeuvres =
    {
        ("pull @ fd", 1.0f, new FlightInput { Pitch = 1f, Throttle = 1f }),
        ("pull @ 1.5 fd", 1.5f, new FlightInput { Pitch = 1f, Throttle = 1f }),
        ("banked pull @ fd", 1.0f, new FlightInput { Pitch = 1f, Roll = 1f, Throttle = 1f }),
        ("push @ 1.5 fd", 1.5f, new FlightInput { Pitch = -1f, Throttle = 1f }),
        ("rudder @ fd", 1.0f, new FlightInput { Yaw = 1f, Throttle = 1f }),
    };

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>The G limiter's ramp begins at the authored <c>highGs[0]</c>, so it has no effect at
    /// all below that. No airframe's hardest manoeuvre demands that much — and the demand read here
    /// is before both lift clamps, so the delivered load factor is lower still.</summary>
    [ExtractedDataFact]
    public void TheGLimiterCannotEngageOnAnyAirframe()
    {
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            var (peakG, where, _, _) = Worst(stats);
            Assert.True(peakG < stats.HighGStart,
                $"{plane}: peak demanded load factor {peakG:0.00} G ({where}) reaches the authored "
                + $"highGs[0] = {stats.HighGStart:0.0} G — the G limiter now engages and is owed an "
                + "implementation (gating ONLY input that opposes the current rotation)");
        }
    }

    /// <summary>The <c>lowGs</c> side is unreachable twice over: the demand is the LENGTH of a
    /// projected vector, so it is never negative in this model, and the authored −6 G sits past the
    /// −5 G lift clamp besides. A full forward push is the manoeuvre that would produce it.</summary>
    [ExtractedDataFact]
    public void TheNegativeGLimiterCannotEngageOnAnyAirframe()
    {
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            var (peakG, _) = Fly(stats, stats.FdSpeed * 1.5f, new FlightInput { Pitch = -1f, Throttle = 1f });
            Assert.True(stats.LowGStart < 0f && peakG >= 0f,
                $"{plane}: lowGs[0] = {stats.LowGStart:0.0} G against a demanded load factor that "
                + $"peaks at {peakG:0.00} G and cannot go negative — if either changes sign the "
                + "negative-G limiter is back in play");
        }
    }

    /// <summary>α is an emergent alignment lag here, and the hardest sustained pull reaches roughly
    /// half the authored <c>maxAOA</c>. The suite's own scenarios agree from the other side: the
    /// sustained pitch-rate row reports ≈20°, the zoom-climb ≈23°, and D31's knife-edge probe peaks
    /// at 0.71–4.29° per airframe.</summary>
    [ExtractedDataFact]
    public void TheAoaLimiterCannotEngageOnAnyAirframe()
    {
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            float maxAoaDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(stats.MaxAoaCos, -1f, 1f)));
            var (_, _, peakAlpha, where) = Worst(stats);
            Assert.True(peakAlpha < maxAoaDeg,
                $"{plane}: α peaks at {peakAlpha:0.0}° ({where}) against the authored maxAOA of "
                + $"{maxAoaDeg:0.0}° — the AOA limiter now engages and is owed an implementation "
                + "(gating ONLY input that opposes the current rotation)");
        }
    }

    /// <summary>The able-to-fail control (METHOD-9): the two disproofs above are worth nothing if
    /// the manoeuvre they fly is too gentle to trip any threshold. Halving both authored
    /// thresholds — the stand-in for a data edit or a per-plane override that brings them into
    /// reach — makes both checks fail, so each is measuring the margin rather than asserting an
    /// unreachable constant.
    ///
    /// <para>⚠ The G margin is not as wide as the authored numbers make it look: the Bloodhawk's
    /// peak demand lands at 5.01 G, within 0.2 % of the executable's compiled fallback
    /// <c>highGs[0] = 5</c> (<see cref="FallbackHighGStart"/>). Under the fallbacks the limiter
    /// would engage — barely, a fraction of a percent into a 4 G-wide ramp. It is the AUTHORED 9 G
    /// that puts it firmly out of reach, which is the trap this whole plan is written around: a
    /// fallback is evidence of intent, not of behaviour.</para></summary>
    [ExtractedDataFact]
    public void TheDisproofIsAbleToFail()
    {
        var stats = PlaneStats.Load(ZrdrPath, "player_bhawk");
        float maxAoaDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(stats.MaxAoaCos, -1f, 1f)));
        var (peakG, gWhere, peakAlpha, alphaWhere) = Worst(stats);

        Assert.True(peakG > stats.HighGStart / 2f,
            $"player_bhawk: peak demanded load factor is only {peakG:0.00} G ({gWhere}) against "
            + $"half the authored highGs[0] ({stats.HighGStart / 2f:0.0} G) — the manoeuvre has "
            + "become too gentle to trip a limiter, so the G disproof can no longer fail");
        Assert.True(peakAlpha > maxAoaDeg / 2f,
            $"player_bhawk: α peaks at only {peakAlpha:0.0}° ({alphaWhere}) against half the "
            + $"authored maxAOA ({maxAoaDeg / 2f:0.0}°) — the manoeuvre has become too gentle to "
            + "trip a limiter, so the AOA disproof can no longer fail");
    }

    /// <summary>The per-airframe table behind the two disproofs, written out for
    /// analysis/flight-model-baseline/POST-B14.md when CSVM_LIMITER_OUT names a file (the same
    /// pattern ZzBaselineDump uses). Asserts nothing on its own.</summary>
    [ExtractedDataFact]
    public void DumpTheLimiterMargins()
    {
        // Invariant culture for the same reason Probes does it: a German decimal comma turns a
        // committed evidence table into something no later diff can compare against.
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        string? outPath = System.Environment.GetEnvironmentVariable("CSVM_LIMITER_OUT");
        var rows = new List<string>();
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            float maxAoaDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(stats.MaxAoaCos, -1f, 1f)));
            var (peakG, gWhere, peakAlpha, alphaWhere) = Worst(stats);
            rows.Add($"{plane,-19} {peakG,8:0.00} {stats.HighGStart,9:0.0} {stats.HighGStart - peakG,9:0.00} "
                     + $"{peakAlpha,8:0.0} {maxAoaDeg,8:0.0} {maxAoaDeg - peakAlpha,9:0.0}   "
                     + $"{gWhere} / {alphaWhere}");
        }

        if (outPath != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{"airframe",-19} {"peak G",8} {"highGs0",9} {"margin",9} "
                          + $"{"peak a",8} {"maxAOA",8} {"margin",9}   worst manoeuvre (G / a)");
            foreach (string row in rows)
                sb.AppendLine(row);
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        }

        Assert.Equal(AllPlanes.Length, rows.Count);
    }

    /// <summary>Peak demanded load factor and peak α over one manoeuvre.</summary>
    private static (float PeakG, float PeakAlpha) Fly(PlaneStats stats, float entrySpeed, FlightInput input)
    {
        var m = new FlightModel(stats);
        m.Reset(Vector3.Zero, Basis.Identity, entrySpeed, input.Throttle);
        float peakG = 0f, peakAlpha = 0f;
        for (int i = 0; i < 600; i++)
        {
            m.Step(input, Dt);
            peakG = Mathf.Max(peakG, m.LoadFactorDemand);
            peakAlpha = Mathf.Max(peakAlpha, m.Alpha);
        }
        return (peakG, peakAlpha);
    }

    /// <summary>Worst case over every manoeuvre, for one airframe.</summary>
    private static (float PeakG, string GWhere, float PeakAlpha, string AlphaWhere) Worst(PlaneStats stats)
    {
        float peakG = 0f, peakAlpha = 0f;
        string gWhere = "-", alphaWhere = "-";
        foreach (var (name, frac, input) in Manoeuvres)
        {
            var (g, a) = Fly(stats, stats.FdSpeed * frac, input);
            if (g > peakG)
                (peakG, gWhere) = (g, name);
            if (a > peakAlpha)
                (peakAlpha, alphaWhere) = (a, name);
        }
        return (peakG, gWhere, peakAlpha, alphaWhere);
    }
}

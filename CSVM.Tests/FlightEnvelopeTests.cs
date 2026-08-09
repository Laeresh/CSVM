using System.IO;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Bloodhawk's flown envelope against the original's, measured off cockpit-gauge video
/// (engine-free: <c>Probes.FlightEnvelope</c> touches
/// no live Node). These are golden numbers in the same sense as the destructible census — the
/// original is a fixed artifact, so "150 → 290 mph in 3.76 s" is an invariant of it.
///
/// <para>The probe's informational rows are deliberately NOT asserted; they record open questions
/// and must not fail a build. Three of them are recorded CONFLICTS rather than open questions —
/// accel-150-290 (footage vs the byte-verified force path), terminal-dive (waiting on D32's
/// attitude-thrust terms) and sustained-turn-speed (waiting on C22's bank coupling) — each with
/// the attribution in its own comment in <c>Probes.FlightEnvelope</c> and the record in
/// <c>docs/org/flightModel.md</c>.</para>
/// </summary>
public class FlightEnvelopeTests
{
    /// <summary>How many flight scenarios carry a measured target to assert. Pinned so that
    /// silently demoting one to informational cannot read as a green run.</summary>
    private const int FlightScenarios = 7;

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [ExtractedDataFact]
    public void TheFlownEnvelopeStillMatchesTheOriginalsMeasuredManoeuvres()
    {
        var r = Probes.FlightEnvelope(ZrdrPath, "player_bhawk");
        Assert.True(r.Error == null, $"plane stats load error={r.Error ?? "-"}");
        Assert.Equal(FlightScenarios, r.Asserted);
        foreach (var row in r.Rows)
        {
            if (row.Asserted)
            {
                string measured = $"original={row.Measured:0.00}{row.Unit} "
                                  + $"tol=±{row.Tolerance:0.00} err={row.ErrorPct:+0.0;-0.0}%";
                Assert.True(row.Ok, $"{row.Name} model={row.Model:0.00}{row.Unit} {measured}");
            }
        }
    }
}

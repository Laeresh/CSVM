using System.IO;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Bloodhawk's flown envelope against the original's, measured off cockpit-gauge video
/// (engine-free: <c>Probes.FlightEnvelope</c> touches no live Node). These are golden numbers:
/// the original is a fixed artifact, so each measured figure is an invariant of it.
/// The probe's informational rows are deliberately not asserted; those recorded as conflicts
/// rather than open questions, and why, are in docs/org/flightModel.md's C23 landing note, its
/// "The *Tune rates" section, which owns yaw-360's demotion, and "The α a full pull holds", which
/// owns pitch-rate's: the decoded AOA window live reads 22.5 °/s against the footage's 33.00.
/// A demotion to informational is never the quiet way to make a run green.
/// </summary>
public class FlightEnvelopeTests
{
    // How many flight scenarios carry a measured target to assert. Pinned so that
    // silently demoting one to informational cannot read as a green run.
    private const int FlightScenarios = 5;

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

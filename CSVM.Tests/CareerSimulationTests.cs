using System.Globalization;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>The economy simulation over the default tunes. It is deterministic, and it prints
/// the money curve of 20 seeds. The mean curve must stay tight at first and pass break-even by
/// week 24, the shape the fee bands and sortie days were set to.</summary>
public class CareerSimulationTests
{
    private readonly ITestOutputHelper _output;

    public CareerSimulationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TheSimulation_IsDeterministic()
    {
        Assert.Equal(CareerSimulation.Run(3, 20), CareerSimulation.Run(3, 20));
    }

    [Fact]
    public void TheCurve_IsTightThenProfitable()
    {
        var report = new StringBuilder("week  cash   debt   net    flown won wrecks (mean of 20 seeds)\n");
        var runs = Enumerable.Range(1, 20).Select(s => CareerSimulation.Run((ulong)s, 100)).ToList();
        foreach (int week in new[] { 0, 1, 2, 4, 8, 12, 16, 24, 36, 52, 76, 99 })
        {
            var rows = runs.Select(r => r[week]).ToList();
            report.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,4} {1,6:F0} {2,6:F0} {3,7:F0} {4,5:F1} {5,4:F1} {6,4:F1}",
                week,
                rows.Average(r => r.Cash),
                rows.Average(r => r.Debt),
                rows.Average(r => r.Cash - r.Debt),
                rows.Average(r => r.Flown),
                rows.Average(r => r.Won),
                rows.Average(r => r.Wrecks)));
        }

        _output.WriteLine(report.ToString());

        // The curve the tunes were set to: tight at first, past break-even by week 24.
        Assert.True(runs.Average(r => r[2].Cash - r[2].Debt) < -5000);
        Assert.True(runs.Average(r => r[24].Cash - r[24].Debt) > 0);
    }
}

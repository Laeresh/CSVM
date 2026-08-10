using System;
using System.IO;
using System.Text;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

/// <summary>THROWAWAY baseline capture — delete once the drag/lift tuning it feeds is settled.
/// Writes every airframe's flight envelope to a file so a global constant change can be
/// diffed against it (ThrustConst scales all eleven planes together).</summary>
public class ZzBaselineDump
{
    private static readonly string[] Planes =
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro",
        "player_avenger", "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel",
        "player_peacemaker",
    };

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [ExtractedDataFact]
    public void DumpEveryAirframe()
    {
        string outPath = Environment.GetEnvironmentVariable("CSVM_DUMP_OUT")
                         ?? Path.Combine(Path.GetTempPath(), "flight-dump.txt");
        var sb = new StringBuilder();
        foreach (string plane in Planes)
        {
            var r = Probes.FlightEnvelope(ZrdrPath, plane);
            sb.AppendLine(r.Error ?? r.Text);
            sb.AppendLine();
        }

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        Assert.True(File.Exists(outPath));
    }
}

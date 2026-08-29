using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The scrapbook's results block: the outcome line, the four drawn rows and the Best to
/// Date / Most Recent tabs, reproduced against <c>Campaign Mission End screen CM01.png</c> and
/// <c>Campaign Scrapbook CM02 Mission select after another Mission.png</c>
/// (<c>docs/org/debrief.md#the-screen-is-the-scrapbook</c>).</summary>
public class CampaignScrapbookResultsTests
{
    [Fact]
    public void Cm01MostRecentReproducesTheShippedRow()
    {
        var result = new MissionResult
        {
            Seq = 0,
            Latest = Run(mask: 1, timeMs: 215000, shots: 25, hits: 4, money: 0, kills: (8, 3)), // Kestrel
        };

        var rows = CampaignScrapbookResults.Rows(result, bestToDate: false);

        Assert.True(CampaignScrapbookResults.Won(result, bestToDate: false));
        Assert.Contains(rows, l => l.Text == "Mission Completed");
        Assert.Contains(rows, l => l.Text == "03:35");
        Assert.Contains(rows, l => l.Text == "16%");
        Assert.Contains(rows, l => l.Text == "$0");
        Assert.Equal(3, CampaignScrapbookResults.PlanesDowned(result, bestToDate: false));
    }

    [Fact]
    public void Cm02MostRecentReproducesTheShippedRow()
    {
        var result = new MissionResult
        {
            Seq = 1,
            // 2 Balmoral (i 2) and 3 Peacemaker (i 9) plain, a starred 1 Peacemaker ace: 6 total.
            Latest = Run(mask: 1, timeMs: 199000, shots: 50, hits: 9, money: 500,
                kills: (2, 2), kills2: (9, 3), aceKills: (9, 1)),
        };

        var rows = CampaignScrapbookResults.Rows(result, bestToDate: false);

        Assert.Contains(rows, l => l.Text == "03:19");
        Assert.Contains(rows, l => l.Text == "18%");
        Assert.Equal(6, CampaignScrapbookResults.PlanesDowned(result, bestToDate: false));
    }

    [Fact]
    public void GunHitRatioTruncatesRatherThanRounds()
    {
        // 5/33 = 15.15...%, which rounds to 15 either way; 5/32 = 15.625%, which would round up
        // to 16 but must truncate to 15, matching the original's ftol call rather than Math.Round.
        var result = new MissionResult { Latest = Run(mask: 1, timeMs: 0, shots: 32, hits: 5, money: 0) };

        var rows = CampaignScrapbookResults.Rows(result, bestToDate: false);

        Assert.Contains(rows, l => l.Text == "15%");
    }

    [Fact]
    public void RocketsExpendedIsNotAmongTheDrawnRows()
    {
        var result = new MissionResult { Latest = Run(mask: 1, timeMs: 0, shots: 0, hits: 0, money: 0) };

        var rows = CampaignScrapbookResults.Rows(result, bestToDate: false);

        Assert.DoesNotContain(rows, l => l.Text.Contains("Rocket"));
        Assert.Equal(10, rows.Count); // outcome + heading + four (title, value) pairs
    }

    /// <summary>⚠ The original's <c>0x0040a7e6</c> reads a never-written offset for the Best to
    /// Date tab instead of the merged mask, so that tab always shows Mission Failed even for a
    /// completed, merged mission. Most Recent reads the real mask.</summary>
    [Fact]
    public void BestToDateAlwaysReadsAsMissionFailed()
    {
        var result = new MissionResult
        {
            Latest = Run(mask: 1, timeMs: 1000, shots: 1, hits: 1, money: 0),
            Best = Run(mask: 1, timeMs: 1000, shots: 1, hits: 1, money: 0),
        };

        Assert.True(CampaignScrapbookResults.Won(result, bestToDate: false));
        Assert.False(CampaignScrapbookResults.Won(result, bestToDate: true));

        var rows = CampaignScrapbookResults.Rows(result, bestToDate: true);
        Assert.Contains(rows, l => l.Text == "Mission Failed");
    }

    [Fact]
    public void TabTitlesAreTheOriginalsWords()
    {
        Assert.Equal("Most Recent", CampaignScrapbookResults.TabTitle(bestToDate: false));
        Assert.Equal("Best to Date", CampaignScrapbookResults.TabTitle(bestToDate: true));
    }

    private static MissionRun Run(
        int mask, int timeMs, int shots, int hits, int money,
        (int Index, int Count)? kills = null, (int Index, int Count)? kills2 = null,
        (int Index, int Count)? aceKills = null)
    {
        var run = new MissionRun
        {
            CompletedMask = mask,
            TimeMs = timeMs,
            Shots = shots,
            Hits = hits,
            Money = money,
        };
        if (kills is { } k)
        {
            run.Kills[k.Index] = k.Count;
        }

        if (kills2 is { } k2)
        {
            run.Kills[k2.Index] = k2.Count;
        }

        if (aceKills is { } a)
        {
            run.AceKills[a.Index] = a.Count;
        }

        return run;
    }
}

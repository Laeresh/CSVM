using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The outlaw list's rows against the 34 flags. Every flag is written by exactly one box, and each
/// page runs in the original's row order. An ammunition or rocket row stands ticked and inert under
/// its page's Outlaw All.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class OutlawRowsTests
{
    private static readonly OutlawPage[] Pages = Enum.GetValues<OutlawPage>();

    [Fact]
    public void EveryFlagIsWrittenByExactlyOneBox()
    {
        var written = new List<int>();
        foreach (var page in Pages)
        {
            for (int row = 0; row < OutlawRows.Count(page); row++)
            {
                written.Add(OutlawRows.Flag(page, row));
            }

            if (OutlawRows.AllFlag(page) >= 0)
            {
                written.Add(OutlawRows.AllFlag(page));
            }
        }

        Assert.Equal(Enumerable.Range(0, NetPlaneRules.Flags), written.OrderBy(flag => flag));
    }

    [Fact]
    public void EachPageRunsInTheOriginalsRowOrder()
    {
        Assert.Equal(NetPlaneRules.AirframeFlag + 10, OutlawRows.Flag(OutlawPage.Airframes, 10));
        Assert.Equal(NetPlaneRules.NitroFlag, OutlawRows.Flag(OutlawPage.Engines, 0));
        Assert.Equal(NetPlaneRules.GunFlag + 4, OutlawRows.Flag(OutlawPage.Guns, 4));
        Assert.Equal(NetPlaneRules.AmmoFlag + 3, OutlawRows.Flag(OutlawPage.Ammo, 3));
        Assert.Equal(NetPlaneRules.RocketFlag + 10, OutlawRows.Flag(OutlawPage.Rockets, 10));
        Assert.Equal(NetPlaneRules.AllAmmoFlag, OutlawRows.AllFlag(OutlawPage.Ammo));
        Assert.Equal(NetPlaneRules.AllRocketsFlag, OutlawRows.AllFlag(OutlawPage.Rockets));

        // ABLE-TO-FAIL CONTROL: a row past a page's end writes nothing.
        Assert.Equal(-1, OutlawRows.Flag(OutlawPage.Guns, 5));
        Assert.Equal(-1, OutlawRows.Flag(OutlawPage.Engines, 1));
    }

    [Fact]
    public void EachRowIsNamedByTheStringItsPageReads()
    {
        Assert.Equal(3003, OutlawRows.NameId(OutlawPage.Airframes, 3));
        Assert.Equal(10135, OutlawRows.NameId(OutlawPage.Engines, 0));
        Assert.Equal(3320, OutlawRows.NameId(OutlawPage.Guns, 0));
        Assert.Equal(3353, OutlawRows.NameId(OutlawPage.Ammo, 3));
        Assert.Equal(3390, OutlawRows.NameId(OutlawPage.Rockets, 10));
        Assert.True(OutlawRows.Scrolls(OutlawPage.Airframes) && OutlawRows.Scrolls(OutlawPage.Rockets));
        Assert.False(OutlawRows.Scrolls(OutlawPage.Guns) || OutlawRows.Scrolls(OutlawPage.Ammo));
        Assert.Equal(5, OutlawRows.Shown(OutlawPage.Guns));
        Assert.Equal(OutlawRows.Window, OutlawRows.Shown(OutlawPage.Rockets));
    }

    [Fact]
    public void OutlawAllTicksItsPagesRowsAndStopsTheirClicks()
    {
        var rules = default(NetPlaneRules).With(NetPlaneRules.AllRocketsFlag, true);

        Assert.True(OutlawRows.Ticked(rules, OutlawPage.Rockets, 5));
        Assert.False(OutlawRows.Takes(rules, OutlawPage.Rockets, 5));
        Assert.False(rules.Has(OutlawRows.Flag(OutlawPage.Rockets, 5)));

        // ABLE-TO-FAIL CONTROL: All Rockets leaves the Ammo page's rows alone.
        Assert.False(OutlawRows.Ticked(rules, OutlawPage.Ammo, 1));
        Assert.True(OutlawRows.Takes(rules, OutlawPage.Ammo, 1));
        Assert.True(OutlawRows.Takes(rules, OutlawPage.Airframes, 0));
    }
}

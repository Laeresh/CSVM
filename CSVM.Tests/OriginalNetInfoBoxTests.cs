using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CSVM.Net;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// GAME INFORMATION's Listing chooser on the engine-free shell over the layout fixture. It opens on
/// the hosted kind's default and is live only while the door has a master server. Without one it
/// stands greyed, and neither a press nor a sideways step changes it.
/// </summary>
public class OriginalNetInfoBoxTests
{
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void WithNoMasterServerTheChooserIsGreyedAndTakesNoInput()
    {
        var box = Ask(master: false, NetSessionKind.Dogfight);

        var listing = Row(box, OriginalNetInfoBox.ListingKey);
        Assert.False(box.ListingLive);
        Assert.False(listing.Enabled);
        Assert.Equal(CoopDoorText.PublicWord, listing.Label);
        Assert.False(Row(box, OriginalNetInfoBox.PublicKey).Enabled);
        Assert.False(Row(box, OriginalNetInfoBox.PrivateKey).Enabled);

        box.Activate(OriginalNetInfoBox.ListingKey);
        box.Activate(OriginalNetInfoBox.PrivateKey);
        var rows = Rows(box);
        Assert.False(box.StepSideways(rows, rows.FindIndex(row => row.Key == OriginalNetInfoBox.ListingKey), 1));
        Assert.False(box.Draft.Private);
    }

    [Fact]
    public void WithAMasterServerTheChooserOpensOnTheKindsDefaultAndFlips()
    {
        var coop = Ask(master: true, NetSessionKind.CampaignCoop);
        Assert.True(coop.ListingLive);
        Assert.Equal(CoopDoorText.PrivateWord, Row(coop, OriginalNetInfoBox.ListingKey).Label);

        var box = Ask(master: true, NetSessionKind.Dogfight);
        Assert.True(Row(box, OriginalNetInfoBox.ListingKey).Enabled);
        Assert.False(box.Draft.Private);

        var rows = Rows(box);
        Assert.True(box.StepSideways(rows, rows.FindIndex(row => row.Key == OriginalNetInfoBox.ListingKey), 1));
        Assert.True(box.Draft.Private);
        box.Activate(OriginalNetInfoBox.PublicKey);
        Assert.False(box.Draft.Private);
        box.Activate(OriginalNetInfoBox.ListingKey);
        Assert.True(box.Draft.Private);
    }

    private static OriginalNetInfoBox Ask(bool master, NetSessionKind kind)
    {
        var end = LoopbackTransport.Mesh(1, Clean, new Random(1))[0];
        var door = new NetPlayFeature((_, _, _) => end, (_, _) => end)
        {
            Master = master ? new MasterDirectory(_ => Task.FromResult("{\"games\":[]}")) : null,
        };
        var shell = new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), new PlayerSetupFeature(), _ => null);
        shell.StandInNetDoor(door);
        shell.AskNetInfo(kind, () => { }, new NetPlayerInfo { GameName = "Pirates", Callsign = "Zachary" });
        return shell.NetInfo;
    }

    private static List<OriginalRow> Rows(OriginalNetInfoBox box)
    {
        var rows = new List<OriginalRow>();
        box.Rows(rows);
        return rows;
    }

    private static OriginalRow Row(OriginalNetInfoBox box, string key) => Rows(box).Find(row => row.Key == key)!;
}

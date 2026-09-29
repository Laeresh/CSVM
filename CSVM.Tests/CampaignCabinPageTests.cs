using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using Xunit;

namespace CSVM.Tests;

/// <summary>The cabin hub: NEXT MISSION routing to the profile's own next mission and refusing once
/// the campaign is finished, PREVIOUS MISSIONS and PLANE CONSTRUCTION handing the shell a screen or
/// a job, CHANGE MEMENTO opening the chooser over the picture the profile hangs, and RETURN TO MAIN
/// MENU cancelling the flow.</summary>
public class CampaignCabinPageTests
{
    [Fact]
    public void FiveButtonsInTheOriginalsOwnCreationOrder()
    {
        var flow = Opened(out _);

        Assert.Equal(5, flow.Page.RowCount);
        Assert.Equal("Next Mission", flow.Page.RowText(0));
        Assert.Equal("Previous Missions", flow.Page.RowText(1));
        Assert.Equal("Plane Construction", flow.Page.RowText(2));
        Assert.Equal("Return to Main Menu", flow.Page.RowText(3));
        Assert.Equal("Change Memento", flow.Page.RowText(4));
    }

    [Fact]
    public void ChangeMementoOpensTheChooserAndTheWallDrawsTheProfilesOwnPicture()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Memento = "MS_P_Mom.jpg";
        var flow = Opened(out _, profile);

        Assert.Contains(flow.Page.Pictures, p => p.Art.Name == "SCRAPBOOK/MS_P_Mom.jpg");

        flow.FocusRow(4);
        flow.Accept();

        Assert.Equal(CampaignScreen.MementoSelection, flow.Screen);
    }

    [Fact]
    public void NextMissionSetsTheFreshProfilesFirstMissionAndOpensTheBriefing()
    {
        var flow = Opened(out _);

        flow.FocusRow(0);
        flow.Accept();

        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
        Assert.Equal(0, flow.MissionSeq);
    }

    [Fact]
    public void NextMissionAfterProgressOpensTheNextSeqNotTheFirst()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        CampaignProgression.Record(profile, Attempt(1));
        var flow = Opened(out _, profile);

        flow.FocusRow(0);
        flow.Accept();

        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
        Assert.Equal(2, flow.MissionSeq);
    }

    [Fact]
    public void NextMissionRefusesOnceAllTwentyFourAreComplete()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        for (int seq = 0; seq < 24; seq++)
        {
            CampaignProgression.Record(profile, Attempt(seq));
        }

        var flow = Opened(out _, profile);
        flow.FocusRow(0);
        flow.Accept();

        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
        Assert.NotEqual(string.Empty, flow.Message);
    }

    [Fact]
    public void PreviousMissionsOpensTheListScreen()
    {
        var flow = Opened(out _);

        flow.FocusRow(1);
        flow.Accept();

        Assert.Equal(CampaignScreen.PreviousMissions, flow.Screen);
    }

    [Fact]
    public void PlaneConstructionRequestsTheHangarWithoutLeavingTheScreen()
    {
        var flow = Opened(out _);

        flow.FocusRow(2);
        flow.Accept();

        Assert.Equal(CampaignExit.OpenHangar, flow.Exit);
        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
    }

    [Fact]
    public void ReturnToMainMenuCancelsTheFlow()
    {
        var flow = Opened(out _);

        flow.FocusRow(3);
        flow.Accept();

        Assert.Equal(CampaignExit.Cancelled, flow.Exit);
    }

    /// <summary>The pin count is the story chapter of the current position: 1 for a fresh profile,
    /// rising every five missions, and 5 once the campaign is finished (act 5 is only four missions
    /// long, so seq 23's chapter is still 5 under integer division).</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(9, 2)]
    [InlineData(10, 3)]
    [InlineData(15, 4)]
    [InlineData(20, 5)]
    [InlineData(24, 5)]
    public void MapPinCountIsTheStoryChapterOfTheCurrentPosition(int missionsCompleted, int pins)
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.MissionsCompleted = missionsCompleted;

        Assert.Equal(pins, CampaignCabinPage.MapPinCount(profile));
    }

    [Fact]
    public void TheMementoIsTheWholeScrapbookFileShrunkAndTiltedAboutThePanesCorner()
    {
        var flow = Opened(out _);
        var pictures = new List<BoardPicture>(flow.Page.Pictures);

        var memento = Assert.Single(pictures, p => p.Art.Name.StartsWith("SCRAPBOOK/", StringComparison.Ordinal));
        var frame = Assert.Single(
            pictures, p => p.Art.Name.Equals("PC_Mementopicframe.png", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(("SCRAPBOOK/" + CampaignMementos.Seeded, 169f, 325f), (memento.Art.Name, memento.X, memento.Y));
        Assert.Equal((CampaignCabinPage.MementoScale, CampaignCabinPage.MementoRevs, true), (memento.Scale, memento.Revs, memento.FromCorner));
        Assert.Null(memento.Crop);
        Assert.Equal((0f, 0f), (memento.Width, memento.Height));
        Assert.True(pictures.IndexOf(memento) < pictures.IndexOf(frame), "the frame draws over the picture's edges");
    }

    /// <summary>The scale and the tilt are read back from the shipped script's <c>pc_memento</c>
    /// pane. Its <c>GK</c>, <c>HK</c> and <c>JK</c> are what <c>CTL.SCRIPT</c> hands to
    /// <c>scale()</c> and <c>rotate()</c>.</summary>
    [ExtractedDataFact]
    public void TheMementosScaleAndTiltAreTheCabinScriptsOwn()
    {
        var path = Extraction.RofTree.Under(TestData.DataRoot!, "ASSETS/SCRIPTS/PASSENGERCABIN.SCRIPT");
        string script = File.ReadAllText(path);
        var pane = Regex.Match(script, @"(\w+)\.YC\s*=\s*""pc_memento""");
        Assert.True(pane.Success, "the script names a pc_memento pane");

        int Property(string key)
        {
            var match = Regex.Match(script, $@"\b{pane.Groups[1].Value}\.{key}\s*=\s*(-?\d+)");
            Assert.True(match.Success, $"the pane sets {key}");
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        Assert.Equal(Property("GK"), Property("HK"));
        Assert.Equal(Property("GK") / 100f, CampaignCabinPage.MementoScale, 5);
        Assert.Equal(-Property("JK") / 360f, CampaignCabinPage.MementoRevs, 5);
    }

    private static MissionAttempt Attempt(int seq) =>
        new(seq, CompletedMask: 1, TimeMs: 40000, Shots: 10, Hits: 5,
            Airframe: 5, PlaneName: "Gypsy Magic");

    private static CampaignFlow Opened(out string dir, CampaignProfileDef? profile = null)
    {
        dir = Path.Combine(TestData.TempDir(), "Profiles");
        Directory.CreateDirectory(dir);
        var flow = new CampaignFlow(new CampaignProfileStore(dir), UiStrings.Empty);
        flow.SelectProfile(profile ?? CampaignProfileDef.NewProfile("Zachary"));
        return flow;
    }
}

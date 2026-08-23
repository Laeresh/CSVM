using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The PLANENAME screen (PLAN-hangar C25): per-character entry on the launchscreen's own ←→
/// stepper, the length row that adds and removes characters, the record's own length cap, and the
/// alphabet that keeps the store's filename sanitisation with nothing to do.
/// </summary>
public class HangarNamePageTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarNamePageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-name-" + Guid.NewGuid().ToString("N"));
        _store = new CustomPlaneStore(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>One row per character, then the length row: a nameless plane opens on the length
    /// row alone, which is the only control that can start a name.</summary>
    [Fact]
    public void OneRowPerCharacterPlusTheLengthRow()
    {
        var flow = OpenOnName();

        Assert.Equal(1, flow.Page.RowCount);
        Assert.Equal("Letters: 0", flow.Page.RowText(0));

        flow.Scratch.Name = "AB";
        Assert.Equal(3, flow.Page.RowCount);
        Assert.Equal("1.  A", flow.Page.RowText(0));
        Assert.Equal("2.  B", flow.Page.RowText(1));
        Assert.Equal("Letters: 2", flow.Page.RowText(2));
    }

    /// <summary>The length row's stepper is the add/remove control: right adds a character, left
    /// takes the last one back. Adding leaves the cursor on the new character's own row, so the
    /// next step edits it rather than adding another.</summary>
    [Fact]
    public void TheLengthRowAddsAndRemovesCharacters()
    {
        var flow = OpenOnName();

        Assert.True(flow.Step(1));
        Assert.Equal("A", flow.Scratch.Name);
        Assert.Equal(0, flow.Row); // the cursor now stands on that character
        Assert.True(flow.Step(1));
        Assert.Equal("B", flow.Scratch.Name);

        int length = flow.Page.RowCount - 1;
        Assert.True(flow.Page.Step(length, 1));
        Assert.Equal("BA", flow.Scratch.Name);
        Assert.True(flow.Page.Step(flow.Page.RowCount - 1, -1));
        Assert.Equal("B", flow.Scratch.Name);
        Assert.True(flow.Page.Step(flow.Page.RowCount - 1, -1));
        Assert.Equal(string.Empty, flow.Scratch.Name);
        Assert.False(flow.Page.Step(0, -1));
    }

    /// <summary>The cap is the original's own: its saved-plane name index is 33-byte records, so
    /// 32 characters is the longest name that round-trips through it.</summary>
    [Fact]
    public void LengthStopsAtTheRecordsCap()
    {
        var flow = OpenOnName();
        flow.Scratch.Name = new string('A', HangarNamePage.MaxLength);

        Assert.False(flow.Page.Step(flow.Page.RowCount - 1, 1));
        Assert.Equal(HangarNamePage.MaxLength, flow.Scratch.Name.Length);
        Assert.Contains("Full at 32", flow.Page.Detail(flow.Page.RowCount - 1), StringComparison.Ordinal);
    }

    /// <summary>A character row steps its own cell through the alphabet and wraps, editing no
    /// other cell.</summary>
    [Fact]
    public void SteppingACellWalksTheAlphabet()
    {
        var flow = OpenOnName();
        flow.Scratch.Name = "AB";
        flow.Move(1);

        Assert.True(flow.Step(1));
        Assert.Equal("AC", flow.Scratch.Name);
        Assert.True(flow.Step(-1));
        Assert.Equal("AB", flow.Scratch.Name);

        flow.Scratch.Name = "AA";
        Assert.True(flow.Step(-1));
        Assert.Equal("A-", flow.Scratch.Name); // wraps onto the alphabet's last entry
    }

    /// <summary>The alphabet carries no character a filename cannot hold, so the store's
    /// sanitisation never rewrites a name this screen produced.</summary>
    [Fact]
    public void TheAlphabetIsFilenameSafe()
    {
        var flow = OpenOnName();
        flow.Step(1); // one character to walk

        var seen = new HashSet<char>();
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        while (seen.Add(flow.Scratch.Name[0]))
        {
            Assert.DoesNotContain(flow.Scratch.Name[0], invalid);
            flow.Page.Step(0, 1);
        }

        Assert.Equal(38, seen.Count); // A-Z, 0-9, space, hyphen
        Assert.Contains(' ', seen);
    }

    /// <summary>A character the alphabet does not carry (an imported original save's lower case)
    /// survives until its own cell is stepped.</summary>
    [Fact]
    public void ImportedCharactersSurviveUntilStepped()
    {
        var flow = OpenOnName();
        flow.Scratch.Name = "Blue Streak";

        Assert.Equal("Blue Streak", flow.Scratch.Name);
        Assert.Equal("2.  l", flow.Page.RowText(1));
        flow.Move(1);
        Assert.True(flow.Step(1));
        Assert.Equal("BAue Streak", flow.Scratch.Name);
    }

    /// <summary>The detail line assembles the whole name with the focused character marked, since
    /// the rows themselves are a vertical column of single characters.</summary>
    [Fact]
    public void DetailMarksTheFocusedCharacter()
    {
        var flow = OpenOnName();
        flow.Scratch.Name = "AB C";

        Assert.Equal("A[B] C", flow.Page.Detail(1));
        Assert.Equal("AB[ ]C", flow.Page.Detail(2));
    }

    /// <summary>A blank name says so in the original's own words (langui 203) where it can still
    /// be fixed, rather than only at the Build press.</summary>
    [Fact]
    public void ABlankNameShowsTheOriginalsRefusal()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":203,\"text\":\"You must enter a name for your new plane.\",\"dll\":\"langui\"}]");
        var flow = OpenOnName(strings);

        Assert.Contains("must enter a name", flow.Page.Detail(0), StringComparison.Ordinal);
    }

    /// <summary>Confirm advances to the purchase screen without editing the name, and the name a
    /// flow entered here is the one the store saves.</summary>
    [Fact]
    public void TheEnteredNameIsWhatTheStoreSaves()
    {
        var flow = OpenOnName();
        flow.Scratch.Engine = 0;
        flow.Step(1);       // "A"
        flow.Page.Step(0, 1); // "B"
        flow.Accept();

        Assert.Equal(HangarScreen.Purchase, flow.Screen);
        Assert.Equal("B", flow.Scratch.Name);
        Assert.True(flow.Commit());
        Assert.Equal("B", Assert.Single(_store.List()).Name);
    }

    // A flow standing on the PLANENAME screen with a fresh scratch plane.
    private HangarFlow OpenOnName(UiStrings? strings = null)
    {
        var flow = new HangarFlow(_store, strings ?? UiStrings.Empty);
        for (int guard = 0; flow.Screen != HangarScreen.Name && guard < HangarFlow.Order.Length; guard++)
        {
            flow.Accept();
        }

        Assert.Equal(HangarScreen.Name, flow.Screen);
        return flow;
    }
}

using System;
using System.Linq;
using CSVM.UI.Boards;
using Xunit;

namespace CSVM.Tests;

public sealed class ChromeTypeTests
{
    [Fact]
    public void TheLadderDescendsWithoutTwoRungsTheSameSize()
    {
        var sizes = Enum.GetValues<ChromeSize>().Select(ChromeType.Size).ToArray();
        for (int i = 1; i < sizes.Length; i++)
        {
            Assert.True(sizes[i] < sizes[i - 1], $"rung {i} ({sizes[i]}) is not below rung {i - 1} ({sizes[i - 1]})");
        }
    }

    [Fact]
    public void TheJoinBoardsDraftSizesAreRungs()
    {
        // 26, 22, 17, 15 and 13 stand as drafted; the draft's 14 was folded into 13.
        Assert.Equal(26f, ChromeType.Size(ChromeSize.Heading));
        Assert.Equal(22f, ChromeType.Size(ChromeSize.Title));
        Assert.Equal(17f, ChromeType.Size(ChromeSize.Body));
        Assert.Equal(15f, ChromeType.Size(ChromeSize.Text));
        Assert.Equal(13f, ChromeType.Size(ChromeSize.Caption));
    }

    [Fact]
    public void AFrameUnitIsTheBoardsAuthoredPixel()
    {
        Assert.Equal(BoardFit.AuthoredHeight, ChromeType.FrameHeight);
        Assert.Equal(26f, ChromeType.InReference(ChromeSize.Heading, BoardFit.AuthoredHeight));
    }

    [Theory]
    [InlineData(ChromeSize.Label, 1440f, 14.4f)]
    [InlineData(ChromeSize.Readout, 1440f, 19.2f)]
    [InlineData(ChromeSize.Note, 1440f, 26.4f)]
    [InlineData(ChromeSize.Text, 720f, 18f)]
    [InlineData(ChromeSize.Title, 720f, 26.4f)]
    public void ARungConvertsIntoAnotherReferenceByTheHeightRatio(ChromeSize rung, float reference, float expected)
    {
        Assert.Equal(expected, ChromeType.InReference(rung, reference), 4);
    }

    [Theory]
    [InlineData(640.4f, "640 m")]
    [InlineData(12.5f, "12 m")]
    [InlineData(1499.6f, "1500 m")]
    public void ADistancePrintsInWholeMetres(float metres, string expected)
    {
        Assert.Equal(expected, ChromeType.Metres(metres));
    }
}

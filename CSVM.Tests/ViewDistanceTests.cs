using System;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The view distance's words, labels and reaches. Each saved word names one reach, the
/// last is no fade at all, and an unknown word reads as the default. The mode gate reads the
/// process-wide <see cref="GraphicsMode"/>, so it is pinned in <see cref="GraphicsModeTests"/>.</summary>
public class ViewDistanceTests
{
    [Theory]
    [InlineData("normal", 1f)]
    [InlineData("far", 2f)]
    [InlineData("veryfar", 4f)]
    public void EachWord_NamesItsReach(string word, float reach)
    {
        Assert.Equal(reach, ViewDistance.Reach(word));
    }

    [Fact]
    public void Unlimited_IsNoFadeAtAll()
    {
        Assert.True(float.IsPositiveInfinity(ViewDistance.Reach("unlimited")));
    }

    /// <summary>The saved words are permanent once written, and each spells its label.</summary>
    [Fact]
    public void TheWordsAndLabels_StayInStep()
    {
        Assert.Equal(new[] { "normal", "far", "veryfar", "unlimited" }, ViewDistance.Words);
        Assert.Equal(new[] { "Normal", "Far", "Very Far", "Unlimited" }, ViewDistance.Labels);
        for (int i = 0; i < ViewDistance.Words.Length; i++)
        {
            Assert.Equal(i, ViewDistance.Index(ViewDistance.Words[i]));
            Assert.Equal(ViewDistance.Labels[i], ViewDistance.Label(ViewDistance.Words[i]));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("farther")]
    [InlineData("Far")]
    public void AnUnknownWord_ReadsAsTheDefault(string? word)
    {
        Assert.Equal(Array.IndexOf(ViewDistance.Words, ViewDistance.Default), ViewDistance.Index(word));
        Assert.Equal(ViewDistance.Reach(ViewDistance.Default), ViewDistance.Reach(word));
    }
}

using CSVM.Mech3;
using CSVM.Spec;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The near models' word: its ladder, its one fallback on every machine, and the band each word
/// draws (<see cref="VegetationModels.BandFor"/>). Engine-free.</summary>
[Trait("Tier", "Quick")]
public class VegetationModelsSettingTests
{
    /// <summary>The flag beats the saved word, which beats the config key, and with none set every machine,
    /// the Deck and Linux included, runs Near.</summary>
    [Fact]
    public void TheFlagBeatsTheSavedWordAndNearIsTheFallback()
    {
        string launched = VegetationModelsSetting.Word;
        try
        {
            var flag = VegetationModelsSetting.Resolve(VegetationModelsSetting.Off, VegetationModelsSetting.Far, VegetationModelsSetting.Far);
            Assert.Equal((VegetationModelsSetting.Off, SettingSource.Flag), (flag.Word, flag.Source));
            Assert.Equal("--vegetation-models", VegetationModelsSetting.Lookup.SourceName(flag.Source));

            var saved = VegetationModelsSetting.Resolve(null, VegetationModelsSetting.Far, VegetationModelsSetting.Off);
            Assert.Equal((VegetationModelsSetting.Far, SettingSource.Saved), (saved.Word, saved.Source));
            Assert.Equal(VegetationModelsSetting.Far, VegetationModelsSetting.Word);

            var none = VegetationModelsSetting.Lookup.Fallback(new SettingContext(true, true, 1));
            Assert.Equal((VegetationModelsSetting.Near, SettingSource.Default), (none.Word, none.Source));
            Assert.Equal(VegetationModelsSetting.Near, VegetationModelsSetting.Lookup.Fallback(new SettingContext(false, true, 1)).Word);
        }
        finally
        {
            VegetationModelsSetting.Resolve(launched, null, null);
        }
    }

    /// <summary>The command line and the options file each keep a known word and drop an unknown one.</summary>
    [Fact]
    public void TheFlagAndTheOptionsFileKeepAKnownWordAndDropAnUnknownOne()
    {
        Assert.Equal(VegetationModelsSetting.Off, SessionSpec.Parse(new[] { "--vegetation-models=off" }).WordFlags[VegetationModelsSetting.Lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--vegetation-models=on" }).WordFlags[VegetationModelsSetting.Lookup]);
        var kept = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { Words = { [VegetationModelsSetting.Lookup] = VegetationModelsSetting.Far } }));
        Assert.Equal(VegetationModelsSetting.Far, kept?.Words[VegetationModelsSetting.Lookup]);
        var dropped = OptionsStore.Deserialize("{\"version\":1,\"vegetationModels\":\"everywhere\"}");
        Assert.NotNull(dropped);
        Assert.Null(dropped!.Words[VegetationModelsSetting.Lookup]);
    }

    /// <summary>Near draws inside 150-250 m, Far inside 250-400 m, and Off draws no model, its band zero.</summary>
    [Fact]
    public void EachWordDrawsItsBand()
    {
        Assert.Equal(new Vector2(150f, 250f), VegetationModels.BandFor(VegetationModelsSetting.Near));
        Assert.Equal(new Vector2(250f, 400f), VegetationModels.BandFor(VegetationModelsSetting.Far));
        Assert.Equal(Vector2.Zero, VegetationModels.BandFor(VegetationModelsSetting.Off));
    }
}

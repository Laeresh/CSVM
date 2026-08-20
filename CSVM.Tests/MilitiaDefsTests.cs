using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The routing from a setup-screen militia to the vehicle def an AI aircraft flies. The pairing is
/// the install's own: a def's <c>title</c> resolves to "&lt;militia&gt; &lt;aircraft&gt;", which is
/// the string the wave editor writes, so these cases run against the shipped names rather than
/// against a table copied into the test.
/// </summary>
public class MilitiaDefsTests
{
    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    private static string MessagesPath =>
        Path.Combine(TestData.ExtractedRoot!, "messages.json");

    [ExtractedDataFact]
    public void AWizardWaveNamesItsMilitiasDef()
    {
        var byName = Map();

        Assert.Equal("bhatwarhawk", MilitiaDefs.ForWave(byName, "Black Hat Warhawk"));
        Assert.Equal("secgyro", MilitiaDefs.ForWave(byName, "Studio Security Autogyro"));
        Assert.Equal("rusdevastator", MilitiaDefs.ForWave(byName, "Russian Devastator"));
    }

    // The def name is no part of the lookup, which is the point: 'sti' reads like nothing in
    // particular, and the def is Sacred Trust's because MSG_VEH_STRUST_HELLHOUND says so.
    [ExtractedDataFact]
    public void TheDefNameIsNotWhatDecidesTheMilitia()
    {
        Assert.Equal("stihellhound", MilitiaDefs.ForWave(Map(), "Sacred Trust Hellhound"));
    }

    // Three defs are titled "Black Hat Brigand" — bhatbrigand, _2 and _5 — and they are different
    // aeroplanes: different pilot ratings, and _2 carries wep_06 where the plain one carries wep_05.
    // A display-name lookup takes the plain def; the variants are for missions that name them.
    [ExtractedDataFact]
    public void AVariantDefNeverWinsADisplayNameLookup()
    {
        Assert.Equal("bhatbrigand", MilitiaDefs.ForWave(Map(), "Black Hat Brigand"));
        Assert.Equal("blakepeace", MilitiaDefs.ForWave(Map(), "Blake Aviation Peacemaker"));
    }

    // The menu says "Hollywood Knight", the message table "Hollywood Knights". Same militia.
    [ExtractedDataFact]
    public void TheMilitiaHalfMatchesAcrossSingularAndPlural()
    {
        Assert.Equal("hkfirebrand", MilitiaDefs.ForWave(Map(), "Hollywood Knight Firebrand"));
    }

    // A shipped ia.json wave carries an MSG_* key naming the aircraft alone, so there is no militia
    // to route and the member flies the base def with its own shipped skins, as it did before.
    [ExtractedDataFact]
    public void AShippedWaveNameResolvesToNoDef()
    {
        var byName = Map();

        Assert.Null(MilitiaDefs.ForWave(byName, "MSG_OBJ_IVARS_FIREBRAND"));
        Assert.Null(MilitiaDefs.ForWave(byName, "MSG_VEH_BHAT_WARHAWK"));
    }

    // The player militia flies the p* family and has no AI def of its own, and two menu pairs ship
    // no def because that roster comes from .BM paint coverage rather than from vehicle.json.
    [ExtractedDataFact]
    public void ThePairsWithNoDefResolveToNothing()
    {
        var byName = Map();

        Assert.Null(MilitiaDefs.ForWave(byName, "Fortune Hunter Fury"));
        Assert.Null(MilitiaDefs.ForWave(byName, "Sacred Trust Warhawk"));
        Assert.Null(MilitiaDefs.ForWave(byName, "Broadway Bomber Peacemaker"));
    }

    // Every militia/aircraft pair the wave editor offers either resolves to a def that loads onto
    // the airframe the menu pairs it with, or is one of the three the install ships no def for.
    [ExtractedDataFact]
    public void EveryMenuPairEitherResolvesToARealDefOrIsKnownDefless()
    {
        var byName = Map();

        foreach (string militia in LaunchMenu.MilitiaNames())
        {
            foreach (string aircraft in LaunchMenu.AircraftFor(militia))
            {
                string? def = MilitiaDefs.ForWave(byName, $"{militia} {aircraft}");
                if (def == null)
                {
                    Assert.True(militia is "Fortune Hunter" or "Broadway Bomber"
                        || (militia == "Sacred Trust" && aircraft == "Warhawk"),
                        $"'{militia} {aircraft}' has no def and is not one of the known-defless pairs");
                    continue;
                }
                // Loading it proves the def exists and derives from that airframe; a mispaired def
                // throws here rather than flying the wrong aeroplane's guns.
                var stats = PlaneStats.LoadForAi(ZrdrPath, InstantAction.PlaneNodeFor(aircraft)!, def);
                Assert.Equal(def, stats.AiDefName);
                Assert.NotEmpty(stats.AiWeapons);
                Assert.NotNull(PaintScheme.ForDef(ZrdrPath, def));
            }
        }
    }

    private static System.Collections.Generic.Dictionary<string, string> Map() =>
        MilitiaDefs.ByDisplayName(ZrdrPath, Messages.Load(MessagesPath));
}

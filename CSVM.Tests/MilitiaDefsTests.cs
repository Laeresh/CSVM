using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The routing from a setup-screen militia to the vehicle def an AI aircraft flies. Every mapped
/// def is checked against the install: it has to exist, and it has to wear the militia's own paint
/// pattern, which is where the table was read from in the first place.
/// </summary>
public class MilitiaDefsTests
{
    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [Fact]
    public void AWizardWaveNamesItsMilitiasDef()
    {
        Assert.Equal("bhatwarhawk", MilitiaDefs.ForWave("Black Hat Warhawk", "Warhawk"));
        Assert.Equal("secgyro", MilitiaDefs.ForWave("Studio Security Autogyro", "Autogyro"));
        Assert.Equal("stihellhound", MilitiaDefs.ForWave("Sacred Trust Hellhound", "Hellhound"));
    }

    // A shipped ia.json wave carries an MSG_* key naming the aircraft alone, so there is no militia
    // to route and the member flies the base def with its own shipped skins, as it did before.
    [Fact]
    public void AShippedWaveNameResolvesToNoDef()
    {
        Assert.Null(MilitiaDefs.ForWave("MSG_OBJ_IVARS_FIREBRAND", "Firebrand"));
        Assert.Null(MilitiaDefs.ForWave("MSG_VEH_BHAT_WARHAWK", "Warhawk"));
    }

    // The player militia has no AI defs, and two menu pairs have no def because that roster comes
    // from .BM paint coverage rather than from vehicle.json.
    [Fact]
    public void ThePairsWithNoDefResolveToNothing()
    {
        Assert.Null(MilitiaDefs.ForWave("Fortune Hunter Fury", "Fury"));
        Assert.Null(MilitiaDefs.ForWave("Sacred Trust Warhawk", "Warhawk"));
        Assert.Null(MilitiaDefs.ForWave("Broadway Bomber Peacemaker", "Peacemaker"));
    }

    // Every militia/aircraft pair the wave editor offers either resolves to a def that exists and
    // loads, or is one of the three known-defless pairs. A typo in the table fails here.
    [ExtractedDataFact]
    public void EveryMenuPairEitherResolvesToARealDefOrIsKnownDefless()
    {
        var known = PaintScheme.LoadCatalog(ZrdrPath);
        Assert.NotEmpty(known);

        foreach (string militia in LaunchMenu.MilitiaNames())
        {
            foreach (string aircraft in LaunchMenu.AircraftFor(militia))
            {
                string? def = MilitiaDefs.For(militia, aircraft);
                if (def == null)
                {
                    Assert.True(militia is "Fortune Hunter" or "Broadway Bomber"
                        || (militia == "Sacred Trust" && aircraft == "Warhawk"),
                        $"'{militia} {aircraft}' has no def and is not one of the known-defless pairs");
                    continue;
                }
                // Resolving it proves the def exists in vehicle.json and derives from the airframe
                // the menu pairs it with; a mismatched pair throws here rather than flying wrong.
                var stats = PlaneStats.LoadForAi(ZrdrPath, InstantAction.PlaneNodeFor(aircraft)!, def);
                Assert.Equal(def, stats.AiDefName);
                Assert.NotNull(PaintScheme.ForDef(ZrdrPath, def));
            }
        }
    }
}

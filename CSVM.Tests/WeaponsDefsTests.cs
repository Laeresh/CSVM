using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Every <c>weapons.json</c> <c>BALLISTICS</c> entry reads through the typed reader, against the
/// retail install (engine-free: <c>Probes.Weapons</c>
/// touches no live Node). Golden count: 48 entries, matching <see cref="WeaponDefsTests"/>'s
/// per-key coverage on the trimmed fixture.
/// </summary>
public class WeaponsDefsTests
{
    private const int WeaponDefCount = 48;

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    private static string MessagesPath =>
        Path.Combine(TestData.ExtractedRoot!, "messages.json");

    [ExtractedDataFact]
    public void EveryBallisticsEntryReadsThroughTheTypedReader()
    {
        var r = Probes.Weapons(ZrdrPath, MessagesPath, "");
        Assert.True(r.Error == null, $"weapons.json loads error={r.Error ?? "-"}");
        Assert.Equal(WeaponDefCount, r.Total);
        Assert.Equal(0, r.UnhandledTotal);
        Assert.False(string.IsNullOrEmpty(r.EmptyClipSound));
    }

    // The engine's squared-radius convention (docs/org/ordnanceTypes.md, "The engine stores radii
    // squared"). An unchanged number proves nothing here, so this asserts the relationship between
    // the two forms over every shipped entry, and that each square is absent exactly when the
    // authored value is.
    [ExtractedDataFact]
    public void EveryStoredSquareIsTheSquareOfItsAuthoredRadius()
    {
        var defs = WeaponDefs.Load(ZrdrPath, null);
        foreach (var def in defs.All)
        {
            AssertSquare(def.Id, "RANGE", def.Range, def.RangeSqM);
            AssertSquare(def.Id, "DETONATION_DISTANCE", def.DetonationDistance, def.DetonationDistanceSqM);
            AssertSquare(def.Id, "IMPACT_PROXIMITY", def.ImpactProximity, def.ImpactProximitySqM);
        }

        // Guards the loop itself: a reader that stopped populating the authored fields would leave
        // every pair null and every assertion above vacuously true.
        Assert.Equal(46, defs.All.Count(d => d.RangeSqM.HasValue));
        Assert.Equal(13, defs.All.Count(d => d.DetonationDistanceSqM.HasValue));
        Assert.Equal(14, defs.All.Count(d => d.ImpactProximitySqM.HasValue));
    }

    private static void AssertSquare(string id, string key, float? raw, float? square)
    {
        if (raw is not { } value)
        {
            Assert.True(square == null, $"{id} {key}: absent authored value must leave no square");
            return;
        }
        Assert.True(square == value * value, $"{id} {key}: {square} != {value}^2");
    }
}

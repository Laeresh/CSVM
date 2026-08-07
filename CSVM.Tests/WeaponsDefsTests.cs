using System.IO;
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
}

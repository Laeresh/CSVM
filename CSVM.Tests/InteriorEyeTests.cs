using System;
using System.IO;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Where the eye sits in a <c>cockpit1</c> interior (<see cref="PlaneBuilder.InteriorEye"/>). Read
/// in interior units, the <c>cockpit_camera</c> marker puts every airframe's sight glass dead ahead.
/// From the interior's origin the glass sits 2.9 to 15.7 degrees off, the Kestrel's 8.7 sideways.
/// </summary>
public class InteriorEyeTests
{
    private static readonly Lazy<GameZ> Planes = new(() =>
        GameZ.Load(SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "planes.zip"))));

    public static TheoryData<string> Airframes => new()
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro", "player_avenger",
        "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel", "player_peacemaker",
    };

    [ExtractedDataTheory]
    [MemberData(nameof(Airframes))]
    public void FromTheMarkerReadInInteriorUnitsTheSightGlassIsDeadAhead(string plane)
    {
        var gamez = Planes.Value;
        var root = gamez.FindByName(plane) ?? throw new InvalidOperationException($"{plane} not in planes.zbd");
        GameZNode? interior = null;
        foreach (int child in root.Children)
        {
            if (gamez.Nodes[child].Name.Equals("cockpit1", StringComparison.OrdinalIgnoreCase))
            {
                interior = gamez.Nodes[child];
            }
        }

        Assert.NotNull(interior);
        var glass = Assert.Single(GunsightReticle.Find(gamez, interior!));
        var centre = Vector3.Zero;
        foreach (var corner in glass.Corners)
        {
            centre += corner / glass.Corners.Count;
        }

        var ray = centre - MarkerRig.FindNamedMarker(gamez, plane, "cockpit_camera");
        float yaw = Mathf.RadToDeg(Mathf.Atan2(ray.X, -ray.Z));
        float pitch = Mathf.RadToDeg(Mathf.Atan2(ray.Y, new Vector2(ray.X, ray.Z).Length()));
        Assert.True(Mathf.Abs(yaw) < 1f && Mathf.Abs(pitch) < 1.5f, $"{plane}: the glass is {yaw:0.00} deg across, {pitch:0.00} deg up");
    }
}

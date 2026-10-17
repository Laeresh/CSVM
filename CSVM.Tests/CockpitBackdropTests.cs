using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The stereo interior's dark backdrop (<see cref="CockpitBackdrop"/>): the panel it extrudes and
/// the solid it builds behind it. The shader places it with the interior and writes the interior's
/// stencil. The per-plane checks run on the player's own extraction.
/// </summary>
[Trait("Tier", "Quick")]
public class CockpitBackdropTests
{
    private static readonly Lazy<GameZ> Planes = new(() =>
        GameZ.Load(SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "planes.zip"))));

    // The gauges' bounds of a typical panel: 0.44 m ahead, below the eye.
    private static readonly Aabb Gauges = new(new Vector3(-6f, -6f, -14f), new Vector3(12f, 6f, 4f));

    public static TheoryData<string> PlayerPlanes => new()
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro", "player_avenger",
        "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel", "player_peacemaker",
    };

    [Fact]
    public void EveryVertexLiesOnItsCornersRayAtOrBehindItAndTheCapOnTheBackPlane()
    {
        var triangle = new List<Vector3> { new(-2f, -3f, -10f), new(2f, -3f, -11f), new(0f, -1f, -10.5f) };
        float back = CockpitBackdrop.Back(triangle);
        Assert.Equal(-11f - CockpitBackdrop.BackMargin, back, 4);
        var solid = CockpitBackdrop.Extrude(triangle, back);
        Assert.Equal(7 * 3, solid.Count);
        foreach (var v in solid)
        {
            Assert.Contains(triangle, c => c.Normalized().Cross(v.Normalized()).Length() < 1e-5f
                && v.Length() >= c.Length() - 1e-4f && v.Length() <= (c.Length() * CockpitBackdrop.MaxReach) + 1e-4f);
        }

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(back, solid[i].Z, 4);
        }
    }
    [Fact]
    public void ThePanelBoxStopsAtTheEyeTheSillsAndTheTub()
    {
        Assert.True(CockpitBackdrop.InPanel(Gauges, Quad(new Vector3(0f, -3f, -11f))));
        Assert.False(CockpitBackdrop.InPanel(Gauges, Quad(new Vector3(0f, 0.6f, -11f))), "above the eye");
        Assert.False(CockpitBackdrop.InPanel(Gauges, Quad(new Vector3(14.5f, -3f, -11f))), "past the reach, a canopy sill");
        Assert.False(CockpitBackdrop.InPanel(Gauges, Quad(new Vector3(0f, -14f, -11f))), "the tub's floor");
        Assert.False(CockpitBackdrop.InPanel(Gauges, Quad(new Vector3(0f, -3f, -20f))), "beyond the slab");
        Assert.False(CockpitBackdrop.InPanel(Gauges, Quad(new Vector3(0f, -3f, -1f))), "beside the eye");
    }

    [Fact]
    public void OnlyAPolygonFacingTheEyeSquarelyCounts()
    {
        var facing = new[] { new Vector3(-1f, -1f, -10f), new Vector3(1f, -1f, -10f), new Vector3(1f, 1f, -10f) };
        Assert.True(CockpitBackdrop.Facing(facing) >= CockpitBackdrop.MinFacing);
        Assert.True(CockpitBackdrop.Facing(new[] { facing[2], facing[1], facing[0] }) < 0f);
        var oblique = new[] { new Vector3(-1f, -1f, -10f), new Vector3(1f, -1f, -10f), new Vector3(1f, -1f, -12f) };
        Assert.True(Mathf.Abs(CockpitBackdrop.Facing(oblique)) < CockpitBackdrop.MinFacing, "a dash top seen edge-on");
    }

    [Fact]
    public void TheShaderPlacesItWithTheInteriorAndWritesTheInteriorsStencil()
    {
        string code = CockpitBackdrop.ShaderCode();
        Assert.Contains($"stencil_mode write, compare_always, {SceneBuilder.XrInteriorStencil};", code, StringComparison.Ordinal);
        Assert.Contains("cull_disabled", code, StringComparison.Ordinal);
        Assert.Contains(SceneBuilder.XrInteriorGlobals, code, StringComparison.Ordinal);
        Assert.Contains("VERTEX = mat3(VIEW_MATRIX) * (csky_rel - csky_eye) + EYE_OFFSET;", code, StringComparison.Ordinal);
    }

    // Each player interior has a panel at or below the eye and ahead of it. Its back plane lies
    // behind every panel corner, so the solid sits behind the interior's own surfaces.
    [ExtractedDataTheory]
    [MemberData(nameof(PlayerPlanes))]
    public void EachPlayerInteriorGetsABackdropBehindItsPanel(string plane)
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
        var panel = CockpitBackdrop.Panel(gamez, interior!, CockpitPlate.Find(gamez, interior!), GunsightReticle.IsGlass);
        Assert.True(panel.Count >= 3 * 20, $"{plane}: {panel.Count / 3} panel triangles");
        float back = CockpitBackdrop.Back(panel);
        foreach (var v in panel)
        {
            Assert.True(v.Y <= 0f && v.Z < 0f && v.Z > back, $"{plane}: corner {v} against back {back}");
        }
    }

    private static Vector3[] Quad(Vector3 at) => new[]
    {
        at + new Vector3(-0.5f, -0.5f, 0f), at + new Vector3(0.5f, -0.5f, 0f), at + new Vector3(0.5f, 0.5f, 0f),
        at + new Vector3(-0.5f, 0.5f, 0f),
    };
}

using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites measuring the airframe collision hulls against the mesh they were derived
/// from, on every player airframe.</summary>
internal static class AirframeColliderSuites
{
    // A mesh corner counts as covered when it is inside some hull by this much slack: the
    // hull's own quantisation and padding move a point by millimetres, not centimetres.
    private const float CoverTolerance = 0.02f;

    // The share of an airframe's triangle area allowed outside every hull. The clipped regions
    // between them share their boundary triangles, so a covered silhouette reads as zero here;
    // the allowance exists for the slivers ClipAxis drops as degenerate.
    private const float MaxUncoveredFraction = 0.005f;

    internal static void AirframeHullCoverage(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var report = new StringBuilder();
        report.AppendLine("airframe part box_m3 hull_m3 removed_m3 verts uncovered_box uncovered_hull");
        try
        {
            foreach (var (model, display) in MarkerRig.PlayerAirframes)
            {
                Node3D? plane = null;
                try
                {
                    plane = new PlaneBuilder(planesGamez, textures).Build(model);
                    ctx.Host.AddChild(plane);
                    MeasureOne(ctx, report, display, plane);
                }
                finally
                {
                    plane?.Free();
                }
            }
        }
        finally
        {
            textures.Dispose();
        }
        ctx.WriteArtifact("test-airframe-hull-coverage.txt", report.ToString());
    }

    private static void MeasureOne(TestContext ctx, StringBuilder report, string display, Node3D plane)
    {
        var tris = new List<PlaneCollider.Triangle>();
        PlaneCollider.Collect(plane, plane.Transform, tris);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var regions = PlaneCollider.Layout(tris);
        watch.Stop();
        ctx.Note($"{display}: layout of {tris.Count} triangles took {watch.Elapsed.TotalMilliseconds:0.0} ms");
        ctx.Check(regions.Count is >= 1 and <= 8, $"{display}: {regions.Count} hulls within the budget");
        ctx.Check(regions.Count > 0 && regions[0].Name == "fuselage", $"{display}: the fuselage hull leads the part order");

        float boxTotal = 0f, hullTotal = 0f;
        foreach (var r in regions)
        {
            float boxVol = r.Hull.Bounds.Size.X * r.Hull.Bounds.Size.Y * r.Hull.Bounds.Size.Z;
            boxTotal += boxVol;
            hullTotal += r.Hull.Volume;
            ctx.Check(r.Name is "fuselage" or "tail" or "wing" or "canard", $"{display}: {r.Name} is a part the damage mapping knows");
            ctx.Check(r.Hull.Points.Length >= 4, $"{display}: {r.Name} hull has a volume verts={r.Hull.Points.Length}");
            ctx.Check(r.Hull.Volume <= boxVol * 1.001f, $"{display}: {r.Name} hull inside its box hull={r.Hull.Volume:0.00} box={boxVol:0.00}");
            ctx.Check(r.Hull.Bounds.Size.X >= 0.29f && r.Hull.Bounds.Size.Y >= 0.29f && r.Hull.Bounds.Size.Z >= 0.29f,
                $"{display}: {r.Name} hull keeps the thickness floor size={r.Hull.Bounds.Size}");
            report.AppendLine($"{display} {r.Name} {boxVol:0.00} {r.Hull.Volume:0.00} {boxVol - r.Hull.Volume:0.00} {r.Hull.Points.Length}");
        }

        float area = 0f, outsideBoxes = 0f, outsideHulls = 0f;
        foreach (var t in tris)
        {
            float a = 0.5f * (t.B - t.A).Cross(t.C - t.A).Length();
            area += a;
            if (!Covered(regions, t, byHull: false))
                outsideBoxes += a;
            if (!Covered(regions, t, byHull: true))
                outsideHulls += a;
        }
        float boxFrac = area > 0f ? outsideBoxes / area : 0f;
        float hullFrac = area > 0f ? outsideHulls / area : 0f;
        ctx.Check(hullFrac <= MaxUncoveredFraction,
            $"{display}: triangle area outside every hull {hullFrac * 100f:0.00}% (boxes left {boxFrac * 100f:0.00}%)");
        ctx.Note($"{display}: {regions.Count} hulls, box {boxTotal:0.0} m3 -> hull {hullTotal:0.0} m3 ({(1f - hullTotal / boxTotal) * 100f:0}% of the box volume removed), uncovered area boxes {boxFrac * 100f:0.00}% hulls {hullFrac * 100f:0.00}%");
        report.AppendLine($"{display} total {boxTotal:0.00} {hullTotal:0.00} {boxTotal - hullTotal:0.00} - {boxFrac * 100f:0.00}% {hullFrac * 100f:0.00}%");
    }

    // Whether every corner of the triangle is inside some region's hull (or, for the
    // comparison column, the box that hull replaces).
    private static bool Covered(List<PlaneCollider.Region> regions, PlaneCollider.Triangle t, bool byHull)
    {
        return Inside(t.A) && Inside(t.B) && Inside(t.C);

        bool Inside(Vector3 p)
        {
            foreach (var r in regions)
            {
                var local = p - r.Centre;
                if (byHull ? r.Hull.Contains(local, CoverTolerance)
                    : r.Hull.Bounds.Grow(CoverTolerance).HasPoint(local))
                    return true;
            }
            return false;
        }
    }
}

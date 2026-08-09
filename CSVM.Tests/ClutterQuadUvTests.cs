using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Where a clutter decoration is stored: the template ground quad's own interpolated texture UV,
/// read the way <c>FUN_004dd230</c> reads it (<c>ClutterBuilder.GroundInfo</c> +
/// <c>GroundQuad.TryUv</c>, plan item B11).
///
/// <para>Two cases, chosen to distinguish the UV rule from the scalar-period rule it replaces
/// (METHOD-1). C1's <c>terpat02</c> is A2's hand-computed worked example and one of the 28
/// templates on which the two rules agree exactly — it pins the arithmetic. C2's
/// <c>parklot1</c> is one of the four on which they do not: a 16 × 32 m quad whose UVs are
/// MIRRORED, so the old <c>(x − minX) / max(extentX, extentZ)</c> came out both half-scaled and
/// back to front. That second case is the one that fails if a sign or a transpose is wrong,
/// which is the whole risk of this change.</para>
///
/// <para>The last four cases cover the other half of the journey (plan item B12): the per-triangle
/// UV lattice the decoration is stamped on, <c>FUN_004dd6e0</c> steps 4, 6 and 7, against A2's
/// worked example end to end — C1 node 2911 <c>g777</c> model 953 poly 3 tri 5 — plus the two
/// degeneracies that must be refused for different reasons.</para>
///
/// <para>Numbers from <c>analysis/bl-305-clutter-uv/FINDINGS-A2.md</c>, which derived them from
/// the extraction independently of this code.</para>
/// </summary>
public class ClutterQuadUvTests
{
    private const float UvTolerance = 1e-5f;

    [ExtractedDataFact]
    public void C1Terpat02StoresA2sWorkedDecorationAtItsQuadUv()
    {
        var quad = QuadOf("C1", "terpat02");
        Assert.Equal("terpat02.tif", quad.Texture);
        Assert.Equal(512f, quad.ExtentX, 3);
        Assert.Equal(512f, quad.ExtentZ, 3);

        // C1 node 5909 'firtree1.flt', the first decoration under terpat02's ground quad.
        var gamez = Gamez("C1");
        var deco = gamez.Nodes[5909];
        Assert.Equal("firtree1.flt", deco.Name);
        var local = (deco.Local ?? Transform3D.Identity).Origin;
        Assert.Equal(106.86228f, local.X, 4);
        Assert.Equal(66.779945f, local.Z, 4);

        Assert.True(quad.TryUv(local, out var uv));
        // u = (x + 256) / 512, v = (z + 256) / 512 — hand-computed in FINDINGS-A2.md.
        Assert.Equal(0.708715f, uv.X, UvTolerance);
        Assert.Equal(0.630430f, uv.Y, UvTolerance);
        // Already inside [0, 1), so FUN_004dd230's fmod wrap is a no-op here.
        Assert.Equal(uv.X, ClutterBuilder.GroundQuad.Wrap(uv.X), UvTolerance);
        Assert.Equal(uv.Y, ClutterBuilder.GroundQuad.Wrap(uv.Y), UvTolerance);
    }

    [ExtractedDataFact]
    public void C2Parklot1IsMirroredAndNonSquareSoTheScalarPeriodRuleWasWrong()
    {
        var quad = QuadOf("C2", "parklot1");
        Assert.Equal("parklot1.tif", quad.Texture);
        Assert.Equal(16f, quad.ExtentX, 3);
        Assert.Equal(32f, quad.ExtentZ, 3);

        var gamez = Gamez("C2");
        var root = ClutterBuilder.FindTemplateRoot(gamez, "parklot1")!;
        var ground = ClutterBuilder.FirstWithMesh(gamez, root)!;
        Vector3 local = Vector3.Zero;
        bool found = false;
        foreach (var childIndex in ground.Children)
        {
            var deco = gamez.Nodes[childIndex];
            var origin = (deco.Local ?? Transform3D.Identity).Origin;
            if (deco.Name.StartsWith("c_studebaker2") && Mathf.Abs(origin.X - -3.651f) < 0.01f)
            {
                local = origin;
                found = true;
                break;
            }
        }
        Assert.True(found, "no c_studebaker2 at local x = -3.651 under parklot1's ground quad");

        Assert.True(quad.TryUv(local, out var uv));
        // Mirrored: u = (8 − x) / 16, not (x + 8) / 16 — and the quad is 16 wide, not 32.
        Assert.Equal((8f - local.X) / 16f, uv.X, UvTolerance);
        Assert.Equal(0.728f, uv.X, 1e-3f);
        // The rule this replaced: (x − minX) / max(extentX, extentZ). It is not merely a
        // different labelling of the same point — it is 0.59 UV away, over half the quad.
        float oldRule = (local.X + 8f) / 32f;
        Assert.True(Mathf.Abs(uv.X - oldRule) > 0.5f,
            $"expected the scalar-period rule to disagree; got {oldRule} vs {uv.X}");
    }

    /// <summary>
    /// <c>FUN_004dd230</c>'s fmod wrap, both branches — including the degenerate case where the
    /// negative branch's <c>1 − frac</c> rounds to exactly 1.0 and must collapse back to 0. No
    /// retail decoration reaches the negative branch (every resolving quad spans 0..1), so this
    /// is the only thing that exercises it.
    /// </summary>
    [Fact]
    public void WrapReproducesBothFmodBranchesIncludingTheExactOneCollapse()
    {
        Assert.Equal(0.25f, ClutterBuilder.GroundQuad.Wrap(0.25f), 6);
        Assert.Equal(0.25f, ClutterBuilder.GroundQuad.Wrap(1.25f), 6);
        Assert.Equal(0.25f, ClutterBuilder.GroundQuad.Wrap(7.25f), 6);
        Assert.Equal(0.75f, ClutterBuilder.GroundQuad.Wrap(-0.25f), 6);
        Assert.Equal(0.75f, ClutterBuilder.GroundQuad.Wrap(-1.25f), 6);
        Assert.Equal(0f, ClutterBuilder.GroundQuad.Wrap(0f), 6);
        Assert.Equal(0f, ClutterBuilder.GroundQuad.Wrap(2f), 6);
        Assert.Equal(0f, ClutterBuilder.GroundQuad.Wrap(-2f), 6);
        // 1 − 1e-9 is not representable in float32: it rounds to 1.0, which is out of range.
        Assert.Equal(0f, ClutterBuilder.GroundQuad.Wrap(-1e-9f), 6);
    }

    /// <summary>
    /// The lattice stamp itself (<c>FUN_004dd6e0</c> steps 4, 6 and 7), against A2's worked
    /// example computed by hand in <c>FINDINGS-A2.md</c>: C1 node 2911 <c>g777</c>, model 953,
    /// polygon 3, triangle 5. Every number here was derived from the extraction by a Python
    /// script and re-derived on paper, independently of this code, before B12 was written.
    ///
    /// <para>Note the frame: this triangle's +U runs toward world <b>+Z</b> and its +V toward
    /// <b>−X</b>, a 90° rotation from the template quad's own axes. It is C1's dominant frame
    /// (69.6 % of <c>terpat02</c>'s triangles) and it is why the stamp cannot be done in world
    /// space — there is no world axis to align to.</para>
    /// </summary>
    [Fact]
    public void LatticeStampReproducesA2sWorkedExample()
    {
        // The triangle, verbatim from FINDINGS-A2.md's table.
        var v0 = new Vector3(-9472f, 128f, -3328f);
        var v1 = new Vector3(-9216f, 128f, -3584f);
        var v2 = new Vector3(-9408f, 128f, -3328f);
        var uv0 = new Vector2(1f, 1f);
        var uv1 = new Vector2(0f, 0f);
        var uv2 = new Vector2(1f, 0.75f);

        var tri = ClutterBuilder.UvTriangle.Build(v0, v1, v2, uv0, uv1, uv2, out _);
        Assert.NotNull(tri);

        // Step 7's affine map: A = (0, 0, 256) per +1 U, B = (−256, 0, 0) per +1 V.
        Assert.Equal(0f, tri!.AxisU.X, 3);
        Assert.Equal(0f, tri.AxisU.Y, 3);
        Assert.Equal(256f, tri.AxisU.Z, 3);
        Assert.Equal(-256f, tri.AxisV.X, 3);
        Assert.Equal(0f, tri.AxisV.Y, 3);
        Assert.Equal(0f, tri.AxisV.Z, 3);

        // Step 4: the UV bbox floored to integers is u ∈ {0, 1}, v ∈ {0, 1} — four cells.
        Assert.Equal(0, tri.MinU);
        Assert.Equal(1, tri.MaxU);
        Assert.Equal(0, tri.MinV);
        Assert.Equal(1, tri.MaxV);
        Assert.Equal(4L, tri.CellCount);

        // Steps 5/6: the decoration's stored quad UV, shifted into each cell. Exactly one of the
        // four candidates is inside the triangle in UV space.
        const double decoU = 0.708715, decoV = 0.630430;
        var inside = new List<(double U, double V)>();
        for (int uInt = tri.MinU; uInt <= tri.MaxU; uInt++)
            for (int vInt = tri.MinV; vInt <= tri.MaxV; vInt++)
                if (tri.Contains(uInt + decoU, vInt + decoV))
                    inside.Add((uInt + decoU, vInt + decoV));
        Assert.Single(inside);
        Assert.Equal(decoU, inside[0].U, 6);
        Assert.Equal(decoV, inside[0].V, 6);

        // Step 7: the world position. A2's hand arithmetic and its script agree on this to the
        // printed digits, and an independent barycentric route agrees to 1.8e-12 m.
        var p = tri.World(inside[0].U, inside[0].V);
        Assert.Equal(-9377.390f, p.X, 2);
        Assert.Equal(128.000f, p.Y, 3);
        Assert.Equal(-3402.569f, p.Z, 2);
    }

    /// <summary>
    /// A zero-area WORLD triangle carrying a healthy UV area — A2's trap, and the reason a UV
    /// degeneracy guard alone is not enough. Two of this triangle's corners are the same point,
    /// which is what a fan or strip triangulation of an n-gon with a repeated corner produces:
    /// 1,655 of them on C1's <c>terpat02</c> alone. Its affine map would be finite and
    /// meaningless — both world axes collapsed onto one line — so it must be refused, and
    /// refused for the RIGHT reason, since the two counts are reported separately.
    /// </summary>
    [Fact]
    public void ZeroAreaWorldTriangleIsRefusedEvenWithAHealthyUvArea()
    {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(64f, 0f, 0f);
        var degenerate = ClutterBuilder.UvTriangle.Build(
            a, b, b, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f),
            out var fault);
        Assert.Null(degenerate);
        Assert.Equal(ClutterBuilder.UvTriangleFault.ZeroWorldArea, fault);

        // …and the mirror case: real world area, no invertible UV map.
        var flatUv = ClutterBuilder.UvTriangle.Build(
            a, b, new Vector3(0f, 0f, 64f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f), out fault);
        Assert.Null(flatUv);
        Assert.Equal(ClutterBuilder.UvTriangleFault.ZeroUvArea, fault);
    }

    /// <summary>
    /// The extraction side of the same worked example: C1 model 953's polygon 3 really is a
    /// triangle STRIP whose triangle 5 carries A2's three corners, and its UVs really are indexed
    /// by CORNER POSITION rather than by vertex id — corners 3 and 4 of this very polygon share
    /// vertex 5 and carry different UVs. Reading the UV through the vertex id would silently
    /// stamp one corner's lattice in another corner's texture frame, and this polygon is the
    /// proof that the shipped data exercises the distinction.
    /// </summary>
    [ExtractedDataFact]
    public void C1Model953Polygon3IsAStripWhoseUvsAreIndexedByCorner()
    {
        var gamez = Gamez("C1");
        Assert.Equal("g777", gamez.Nodes[2911].Name);
        Assert.Equal(953, gamez.Nodes[2911].MeshIndex);

        var poly = gamez.Meshes[953].Polygons[3];
        Assert.True(poly.TriangleStrip);
        Assert.Equal("terpat02.tif", gamez.Materials[poly.MaterialIndex].TextureName);
        var verts = gamez.Meshes[953].Vertices;
        var uvs = poly.UvCoords!;

        // Strip triangle 5 = corners 5, 6, 7.
        Assert.Equal(new Vector3(-9472f, 128f, -3328f), verts[poly.VertexIndices[5]]);
        Assert.Equal(new Vector3(-9216f, 128f, -3584f), verts[poly.VertexIndices[6]]);
        Assert.Equal(new Vector3(-9408f, 128f, -3328f), verts[poly.VertexIndices[7]]);
        Assert.Equal(new Vector2(1f, 1f), uvs[5]);
        Assert.Equal(new Vector2(0f, 0f), uvs[6]);
        Assert.Equal(new Vector2(1f, 0.75f), uvs[7]);

        // The same vertex at two corners with two different UVs.
        Assert.Equal(poly.VertexIndices[3], poly.VertexIndices[4]);
        Assert.NotEqual(uvs[3], uvs[4]);
    }

    private static GameZ Gamez(string chapter) =>
        GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));

    private static ClutterBuilder.GroundQuad QuadOf(string chapter, string template)
    {
        var gamez = Gamez(chapter);
        var root = ClutterBuilder.FindTemplateRoot(gamez, template);
        Assert.NotNull(root);
        var ground = ClutterBuilder.FirstWithMesh(gamez, root!);
        Assert.NotNull(ground);
        var quad = ClutterBuilder.GroundInfo(gamez, ground!);
        Assert.NotNull(quad);
        return quad!;
    }
}

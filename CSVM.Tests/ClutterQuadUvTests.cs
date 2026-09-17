using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Where a clutter decoration is stored: the template ground quad's own interpolated texture UV,
/// read the way the binary reads it (<c>ClutterBuilder.GroundInfo</c> + <c>GroundQuad.TryUv</c>).
/// C1's <c>terpat02</c> pins the arithmetic; C2's mirrored, non-square <c>parklot1</c> is where a
/// sign or transpose error would show. The lattice-stamp cases replay the binary's steps 4, 6 and
/// 7 against C1 node 2911's worked example. Evidence: docs/org/clutter.md,
/// analysis/bl-305-clutter-uv/FINDINGS-A2.md.
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
        // u = (x + 256) / 512, v = (z + 256) / 512, hand-computed in FINDINGS-A2.md.
        Assert.Equal(0.708715f, uv.X, UvTolerance);
        Assert.Equal(0.630430f, uv.Y, UvTolerance);
        // Already inside [0, 1), so the binary's fmod wrap is a no-op here.
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
        // Mirrored: u = (8 − x) / 16, not (x + 8) / 16, and the quad is 16 wide, not 32.
        Assert.Equal((8f - local.X) / 16f, uv.X, UvTolerance);
        Assert.Equal(0.728f, uv.X, 1e-3f);
        // The alternative this discriminates against, the scalar-period rule
        // (x − minX) / max(extentX, extentZ), is not merely a different labelling of the same
        // point: it lands 0.59 UV away, over half the quad.
        float oldRule = (local.X + 8f) / 32f;
        Assert.True(Mathf.Abs(uv.X - oldRule) > 0.5f,
            $"expected the scalar-period rule to disagree; got {oldRule} vs {uv.X}");
    }

    /// <summary>
    /// The binary's fmod wrap, both branches, including the degenerate case where the
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
    /// The lattice stamp itself (the binary's steps 4, 6 and 7), against the worked example
    /// computed by hand in <c>analysis/bl-305-clutter-uv/FINDINGS-A2.md</c>: C1 node 2911
    /// <c>g777</c>, model 953, polygon 3, triangle 5. This triangle's frame is rotated 90° from
    /// the template quad's own axes, which is why the stamp cannot be done in world space.
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

        // Step 4: the UV bbox floored to integers is u ∈ {0, 1}, v ∈ {0, 1}, four cells.
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

        // Step 7: the world position. The hand arithmetic and its script agree on this to the
        // printed digits, and an independent barycentric route agrees to 1.8e-12 m.
        var p = tri.World(inside[0].U, inside[0].V);
        Assert.Equal(-9377.390f, p.X, 2);
        Assert.Equal(128.000f, p.Y, 3);
        Assert.Equal(-3402.569f, p.Z, 2);
    }

    /// <summary>
    /// A zero-area world triangle with a healthy UV area, and the mirror case. A UV degeneracy
    /// guard alone would miss the first: 1,655 such triangles exist on C1's <c>terpat02</c> alone
    /// (docs/org/clutter.md), and both must be refused for the right, separately-counted reason.
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
    /// The extraction side of the worked example: C1 model 953's polygon 3 is a triangle strip
    /// whose UVs are indexed by corner position, not vertex id, corners 3 and 4 share vertex 5
    /// but carry different UVs, proving the shipped data exercises the distinction.
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

    /// <summary>
    /// A decoration is a node chain, and the mesh node under its <c>.flt</c> top carries a local
    /// translation of its own. C5's <c>w_lightglow</c> hangs 4.75 m up, the height of the lamp head
    /// on the 5 m <c>lightpole</c> card beside it, and the glow quad is centred on its own origin
    /// (y in [-0.684, 0.684]), so a stamp that drops the lift lands half-buried in the road.
    /// </summary>
    [ExtractedDataFact]
    public void C5sLampGlowHangsOnItsPostWhileTheLightpoleBesideItSitsAtItsOwnNode()
    {
        var lifts = new Dictionary<string, Vector3>();
        foreach (var deco in Decorations("C5"))
            lifts[deco.Model] = deco.Lift;

        Assert.Equal(new Vector3(0f, 4.75f, 0f), lifts["w_lightglow.flt"]);
        Assert.Equal(Vector3.Zero, lifts["lightpole.flt"]);
    }

    /// <summary>
    /// The whole install's decoration chains, as the count of those whose mesh node translates at
    /// all. C5's lamp glow is the only one, which is why carrying the lift changes that glow's
    /// height and nothing else anywhere.
    /// </summary>
    [ExtractedDataFact]
    public void OnlyTheLampGlowCarriesAMeshNodeTranslationInstallWide()
    {
        int decorations = 0, lifted = 0;
        var models = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var offsets = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var chapter in new[] { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5" })
        {
            foreach (var deco in Decorations(chapter))
            {
                decorations++;
                if (deco.Lift == Vector3.Zero)
                    continue;
                lifted++;
                models.Add(deco.Model);
                offsets.Add(deco.Lift.ToString());
            }
        }

        Assert.Equal(872, decorations);
        Assert.Equal(164, lifted);
        Assert.Equal("w_lightglow.flt", Assert.Single(models));
        Assert.Equal("(0, 4.75, 0)", Assert.Single(offsets));
    }

    // Every decoration under every registered template of one chapter: its model name and the
    // translation from its own node down to the node carrying the mesh it draws.
    private static IEnumerable<(string Template, string Model, Vector3 Lift)> Decorations(string chapter)
    {
        var gamez = Gamez(chapter);
        var interp = Path.Combine(TestData.DataRoot!, "extracted", "interp.json");
        foreach (var name in ClutterBuilder.TemplateNames(interp, chapter))
        {
            // A registered template the chapter does not carry is retail-data-normal: C2B
            // registers three of them.
            if (ClutterBuilder.FindTemplateRoot(gamez, name) is not { } root)
                continue;
            if (ClutterBuilder.FirstWithMesh(gamez, root) is not { } ground)
                continue;
            foreach (var childIndex in ground.Children)
            {
                var deco = gamez.Nodes[childIndex];
                if (ClutterBuilder.FirstWithMesh(gamez, deco, false, out var toMesh) == null)
                    continue;
                yield return (name, deco.Name, toMesh.Origin);
            }
        }
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

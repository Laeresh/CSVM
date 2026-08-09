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

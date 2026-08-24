using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The GameZ node reader's transform arithmetic (<c>docs/formats/gotchas.md</c>: the Yxz Euler
/// order and the transposed stored matrix). Input is <c>fixtures/gamez-plane/</c>, an
/// eight-node hand-authored tree in the legacy extraction shape.
/// </summary>
public class GameZTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void NodesKeepTheirFlatPositionNameAndChildren()
    {
        // Child references are flat list positions, not the node's own index field.
        var gamez = Load();
        Assert.Equal(12, gamez.Nodes.Count);
        var plane = gamez.FindByName("probe_plane")!;
        Assert.Equal(0, plane.Index);
        Assert.Equal(new[] { 1 }, plane.Children);
        Assert.Equal("markers", gamez.Nodes[plane.Children[0]].Name);
    }

    [Fact]
    public void EulerAnglesComposeInYxzOrder()
    {
        // R = Ry(y) * Rx(x) * Rz(z). Composed here from three explicit axis rotations so the
        // claim is checked against the order, not restated from the reader.
        var basis = Load().FindByName("probe_euler_mixed")!.Local!.Value.Basis;
        var yxz = new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, 0.3f) * new Basis(Vector3.Back, 1.1f);
        Assert.True(Approx(basis, yxz), $"expected Yxz composition, got {basis}");

        // The control: any other order gives a different matrix, so this check can fail.
        var xyz = new Basis(Vector3.Right, 0.3f) * new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Back, 1.1f);
        Assert.False(Approx(basis, xyz));
    }

    [Fact]
    public void TheStoredMatrixIsTransposedAndAgreesWithTheEulerForm()
    {
        // Both nodes describe one quarter turn about Y, written the two ways the data spells it.
        var gamez = Load();
        var fromMatrix = gamez.FindByName("probe_matrix_y90")!.Local!.Value.Basis;
        var fromEuler = gamez.FindByName("probe_euler_y90")!.Local!.Value.Basis;
        Assert.True(Approx(fromMatrix, fromEuler), $"matrix {fromMatrix} != euler {fromEuler}");

        // A rotation this reader got backwards would equal its own transpose's reading, so
        // assert the matrix is genuinely orientation-bearing.
        Assert.False(Approx(fromMatrix, Basis.Identity));
        Assert.False(Approx(fromMatrix, fromMatrix.Transposed()));
    }

    [Fact]
    public void ANodeWithoutARotationKeepsTranslationOnly()
    {
        var markers = Load().FindByName("markers")!.Local!.Value;
        Assert.Equal(new Vector3(0f, 1f, 0f), markers.Origin);
        Assert.True(Approx(markers.Basis, Basis.Identity));
    }

    [Fact]
    public void IntersectSurfaceReadsFalseAndDefaultsTrueWhenFlagsAreAbsent()
    {
        // The original's per-node collision-participation flag; nodes without a flags
        // block (legacy extractions) must stay collidable.
        var gamez = Load();
        Assert.False(gamez.FindByName("target")!.IntersectSurface);
        Assert.True(gamez.FindByName("markers")!.IntersectSurface);
    }

    private static GameZ Load() => GameZ.Load(TestData.Fixture("gamez-plane"));

    private static bool Approx(Basis a, Basis b) =>
        a.Column0.DistanceTo(b.Column0) < Tolerance
        && a.Column1.DistanceTo(b.Column1) < Tolerance
        && a.Column2.DistanceTo(b.Column2) < Tolerance;
}

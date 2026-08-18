using Godot;

namespace CSVM.Testing;

internal static class SuiteViewers
{
    internal static Camera3D Camera(TestContext ctx, Vector3 at)
    {
        var camera = new Camera3D { Current = false };
        ctx.Host.AddChild(camera);
        camera.GlobalTransform = new Transform3D(Basis.Identity, at);
        return camera;
    }
}

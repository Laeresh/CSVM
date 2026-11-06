using CSVM.Effects;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The Enhanced cloud shadows' globals over no world. They hold what the one owner writes
/// under each graphics mode, word, cloud verdict and zone, and the off state once it leaves.
/// ⚠ The graphics mode and the word are put back in a finally, so the rest of the run reads its own.</summary>
internal static class CloudShadowSuites
{
    [Suite("cloud-shadow-globals",
        "The cloud shadows' one owner writes a darkness only under Enhanced with the word on, in a "
        + "cloudy mission's day zone: Original, the word off (the --det fallback), a night zone and a "
        + "clear sky each write the off state, the drift offset moves with the shader clock, and the "
        + "node leaving the tree writes the off state back")]
    internal static void CloudShadowGlobals(TestContext ctx)
    {
        bool wasEnhanced = GraphicsMode.Enhanced;
        var wasWord = CloudShadowSetting.Lookup.Current;
        var off = CloudShadow.OffValue;
        var host = new Node();
        ctx.Host.AddChild(host);
        try
        {
            CloudShadowSetting.Lookup.ResolveFrom(null, null, new SettingContext(Det: false, Enhanced: true, Panes: 1));
            GraphicsMode.Set(true);
            var drift = new Vector2(3f, -1f);
            var cloudy = CloudShadow.Create(drift, CloudShadowPattern.DriftSource.Fixed, cloudy: true, night: false);
            host.AddChild(cloudy);
            var on = CloudShadow.Published;
            ctx.Check(CloudShadow.Live == cloudy && on.W == CloudShadowPattern.Darkness
                && Mathf.IsEqualApprox(on.Z, 1f / CloudShadowPattern.PatternSize),
                $"Enhanced, the word on, a cloudy day: the darkness {on.W} at one over {CloudShadowPattern.PatternSize} m");
            var expected = CloudShadowPattern.Offset(drift, ShaderTime.Unwrapped);
            ctx.Check(Mathf.IsEqualApprox(on.X, expected.X) && Mathf.IsEqualApprox(on.Y, expected.Y),
                $"at the drift's offset for the shader clock ({on.X:0.##},{on.Y:0.##}) against ({expected.X:0.##},{expected.Y:0.##})");

            cloudy.FollowZone(night: true);
            ctx.Check(CloudShadow.Published == off, $"a night zone writes the off state ({CloudShadow.Published})");
            cloudy.FollowZone(night: false);
            ctx.Check(CloudShadow.Published.W > 0f, $"and the day zone after it draws again");

            GraphicsMode.Set(false);
            cloudy.FollowZone(night: false);
            ctx.Check(CloudShadow.Published == off, $"Original writes the off state whatever the word ({CloudShadow.Published})");
            GraphicsMode.Set(true);

            CloudShadowSetting.Lookup.ResolveFrom(null, null, new SettingContext(Det: true, Enhanced: true, Panes: 1));
            cloudy.FollowZone(night: false);
            ctx.Check(!CloudShadowSetting.Enabled && CloudShadow.Published == off,
                $"--det with no flag resolves the word off ({CloudShadowSetting.Lookup.Describe()}) and writes the off state");
            CloudShadowSetting.Lookup.ResolveFrom(CloudShadowSetting.On, null, new SettingContext(Det: true, Enhanced: true, Panes: 1));
            cloudy.FollowZone(night: false);
            ctx.Check(CloudShadow.Published.W > 0f, $"while --cloud-shadows=on draws them under --det");

            host.RemoveChild(cloudy);
            ctx.Check(CloudShadow.Live == null && CloudShadow.Published == off, $"leaving the tree writes the off state back");
            cloudy.Free();

            var clear = CloudShadow.Create(drift, CloudShadowPattern.DriftSource.Fixed, cloudy: false, night: false);
            host.AddChild(clear);
            ctx.Check(CloudShadow.Published == off, $"a mission with no cloud writes the off state ({CloudShadow.Published})");
        }
        finally
        {
            host.Free();
            GraphicsMode.Set(wasEnhanced);
            CloudShadowSetting.Lookup.Take(wasWord.Word == null ? CloudShadowSetting.FallbackFor(det: false) : wasWord);
        }
    }
}

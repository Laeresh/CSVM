using System;
using CSVM.Effects;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The Enhanced cloud shadows' globals over a synthetic cloud set and no world. They hold what
/// the one owner writes under each graphics mode, word, cloud set and zone, and the off state once it
/// leaves. ⚠ The graphics mode and the word are put back in a finally, so the rest of the run reads its own.</summary>
internal static class CloudShadowSuites
{
    [Suite("cloud-shadow-globals",
        "The cloud shadows' one owner publishes the map its clouds cast only under Enhanced with the word "
        + "on, by day: the card darkens the ground its sun rays reach, each zone sun was cast at load, and "
        + "Original, the word off (the --det fallback), a night zone, a grazing sun and a clear sky each "
        + "write the off state, as does the node leaving the tree")]
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
            // One opaque 200 m card 1000 m up, under a sun 60 degrees up toward +Z.
            var card = new CloudShadowRaster.Caster(new Vector3(500f, 1000f, 300f), 200f, 1f, CloudShadowRaster.Mask.Solid);
            var high = new Vector3(Mathf.DegToRad(-60f), 0f, 0f);
            var lower = new Vector3(Mathf.DegToRad(-40f), 0f, 0f);
            var grazing = new Vector3(Mathf.DegToRad(-3f), 0f, 0f);
            var cloudy = CloudShadow.Bake(new[] { card }, new[] { high, lower, grazing, high }, high, night: false);
            ctx.Check(cloudy.Timing.Split('[').Length == 4 && cloudy.Timing.Contains("none", StringComparison.Ordinal),
                $"each of the three zone suns is cast at load, once, the grazing one to nothing:{cloudy.Timing}");
            host.AddChild(cloudy);
            var on = CloudShadow.Published;
            var run = new Vector2(on.X, on.Y);
            ctx.Check(CloudShadow.Live == cloudy && on.Z == CloudShadowRaster.Darkness && CloudShadow.PublishedMap != null,
                $"Enhanced, the word on, a cloudy day: the darkness {on.Z} with a map");
            ctx.Check(Mathf.IsEqualApprox(run.Y, 1f / Mathf.Sqrt(3f), 1e-4f) && Mathf.IsZeroApprox(run.X),
                $"the sun's run per metre risen is ({run.X:0.###},{run.Y:0.###}), against (0,{1f / Mathf.Sqrt(3f):0.###})");
            if (CloudShadow.PublishedMap is { } map)
            {
                var shaded = new Vector3(500f, 0f, 300f - (1000f * run.Y));
                float under = map.ThroughAt(shaded);
                float below = map.ThroughAt(new Vector3(500f, 0f, 300f));
                ctx.Check(under < 0.01f && below > 0.99f,
                    $"the card shades the ground its sun rays reach ({under:0.###}) and not the ground below it ({below:0.###})");
            }

            var first = CloudShadow.PublishedMap;
            cloudy.FollowZone(lower, night: false);
            ctx.Check(CloudShadow.PublishedMap != null && !ReferenceEquals(first, CloudShadow.PublishedMap)
                && CloudShadow.Published.Y > on.Y,
                $"a zone with a lower sun publishes its own map with a longer run ({CloudShadow.Published.Y:0.###})");
            cloudy.FollowZone(new Vector3(Mathf.DegToRad(-50f), 0f, 0f), night: false);
            ctx.Check(CloudShadow.Published == off, $"a sun no zone authors was never cast and draws nothing ({CloudShadow.Published})");
            cloudy.FollowZone(grazing, night: false);
            ctx.Check(CloudShadow.Published == off, $"a sun 3 degrees up casts nothing ({CloudShadow.Published})");
            cloudy.FollowZone(high, night: true);
            ctx.Check(CloudShadow.Published == off && CloudShadow.PublishedMap == null, $"a night zone writes the off state ({CloudShadow.Published})");
            cloudy.FollowZone(high, night: false);
            ctx.Check(CloudShadow.Published.Z > 0f && ReferenceEquals(first, CloudShadow.PublishedMap),
                $"and the day zone after it draws its first map again");

            GraphicsMode.Set(false);
            cloudy.FollowZone(high, night: false);
            ctx.Check(CloudShadow.Published == off, $"Original writes the off state whatever the word ({CloudShadow.Published})");
            GraphicsMode.Set(true);

            CloudShadowSetting.Lookup.ResolveFrom(null, null, new SettingContext(Det: true, Enhanced: true, Panes: 1));
            cloudy.FollowZone(high, night: false);
            ctx.Check(!CloudShadowSetting.Enabled && CloudShadow.Published == off,
                $"--det with no flag resolves the word off ({CloudShadowSetting.Lookup.Describe()}) and writes the off state");
            CloudShadowSetting.Lookup.ResolveFrom(CloudShadowSetting.On, null, new SettingContext(Det: true, Enhanced: true, Panes: 1));
            cloudy.FollowZone(high, night: false);
            ctx.Check(CloudShadow.Published.Z > 0f, $"while --cloud-shadows=on draws them under --det");

            host.RemoveChild(cloudy);
            ctx.Check(CloudShadow.Live == null && CloudShadow.Published == off && CloudShadow.PublishedMap == null,
                $"leaving the tree writes the off state back");
            cloudy.Free();

            var clear = CloudShadow.Bake(Array.Empty<CloudShadowRaster.Caster>(), new[] { high }, high, night: false);
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

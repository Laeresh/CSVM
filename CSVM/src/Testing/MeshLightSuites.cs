using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites over the point-sprite lights baked into world meshes: the beacon blink the
/// data gates behind a flag of its own, and the distance fade whose slope the data carries
/// pre-divided. Both laws are decoded off the original's draw in
/// docs/formats/world-structure.md.</summary>
internal static class MeshLightSuites
{
    // The night chapter that authors blinking beacons; the only other one in the install is C1.
    private const string LightChapter = "C5";

    // The lens-flare reach the reader used to borrow as every light's fade distance. It is the
    // value on all but a handful of the chapter's lights, so its absence is the fix.
    private const float FlareReachM = 4000f;

    // The periods C5 authors, in seconds. Nothing in the install blinks at any other rate but
    // C1's five-second lighthouse lamp.
    private static readonly float[] Periods = { 1f, 2f };

    [Suite("mesh-light-blink",
        "C5's authored beacons blink at the periods the data gates behind unk04, and those "
        + "periods reach the built point-light materials while the steady lights keep none")]
    internal static void MeshLightBlink(TestContext ctx)
    {
        ctx.WithWorld(LightChapter, collision: false, world =>
        {
            var authored = new SortedSet<float>();
            int blinking = 0, steady = 0;
            foreach (var mesh in world.Gamez.Meshes)
            {
                foreach (var light in mesh.Lights)
                {
                    if (light.BlinkPeriod > 0f)
                    {
                        blinking++;
                        authored.Add(light.BlinkPeriod);
                    }
                    else
                    {
                        steady++;
                    }
                }
            }

            ctx.Check(blinking > 0, $"{LightChapter} authors blinking mesh lights count={blinking}");
            ctx.Check(authored.SetEquals(Periods),
                $"{LightChapter}'s blink periods are the authored set periods={string.Join("/", authored)}");
            // The control: a reader that took the period unconditionally would call every light
            // with a nonzero one a blinker, and would satisfy the two checks above.
            ctx.Check(steady > blinking,
                $"most of the chapter's lights stay steady steady={steady} blinking={blinking}");

            var built = Uniform(world.Stage, "blink_period");
            foreach (float period in Periods)
            {
                ctx.Check(built.Contains(period),
                    $"a built point-light material carries the authored period={period:0.###} s");
            }
            ctx.Check(built.Contains(0f),
                $"the steady lights reach a material with no period at all");
        });
    }

    [Suite("mesh-light-fade",
        "the mesh lights' distance fade comes from the data's own far edge and pre-divided "
        + "slope, so an unfaded light reaches its material unfaded instead of inheriting the "
        + "lens-flare reach")]
    internal static void MeshLightFade(TestContext ctx)
    {
        ctx.WithWorld(LightChapter, collision: false, world =>
        {
            int faded = 0, unfaded = 0, bandsAgree = 0;
            foreach (var mesh in world.Gamez.Meshes)
            {
                foreach (var light in mesh.Lights)
                {
                    if (light.FadeFar <= 0f)
                    {
                        unfaded++;
                        continue;
                    }
                    faded++;
                    // The slope is authored as the reciprocal of the fade band, so the band's
                    // near edge falls between the camera and the far edge. A slope taken off any
                    // other field lands outside that interval.
                    float near = light.FadeFar - (1f / light.FadeSlope);
                    if (near >= 0f && near < light.FadeFar)
                    {
                        bandsAgree++;
                    }
                }
            }

            ctx.Check(faded > 0, $"{LightChapter} authors faded mesh lights count={faded}");
            ctx.Same(faded, bandsAgree,
                $"every faded light's slope spans a band inside its own far edge");
            ctx.Check(unfaded > faded,
                $"most of the chapter's lights author no fade at all unfaded={unfaded} faded={faded}");

            var built = Uniform(world.Stage, "range_far");
            ctx.Check(built.Contains(0f),
                $"an unfaded light reaches its material with a far edge of zero");
            ctx.Check(!built.Contains(FlareReachM),
                $"no built material fades at the lens-flare reach={FlareReachM:0} m");
        });
    }

    // Every distinct value the named uniform takes across the subtree's point-light materials.
    // Read off the built materials, not off the shader text, so a parameter that never left the
    // builder shows up as a missing value rather than passing on the declaration's default.
    private static HashSet<float> Uniform(Node node, string name)
    {
        var found = new HashSet<float>();
        void Walk(Node n)
        {
            if (n is MeshInstance3D { Mesh: { } mesh } && n.Name.ToString() == "lights")
            {
                for (int i = 0; i < mesh.GetSurfaceCount(); i++)
                {
                    if (mesh.SurfaceGetMaterial(i) is ShaderMaterial material)
                    {
                        found.Add(material.GetShaderParameter(name).AsSingle());
                    }
                }
            }
            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }

        Walk(node);
        return found;
    }
}

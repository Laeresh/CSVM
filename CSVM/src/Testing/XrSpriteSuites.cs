using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>The stereo shader texts of the camera-facing sprites (<see cref="StereoFacing"/>). Each
/// poses its quad by the facade look-at toward the eye, builds no basis from the view, and compiles.
/// The suite runs flat, so the texts are asked for by argument. The moon's one text faces the eye
/// on every path (<c>WorldBuilder</c>), and the <c>c1-moon-xr-sim</c> golden pins it in stereo. Entry: docs/architecture/Mech3.md.</summary>
internal static class XrSpriteSuites
{
    // The rain streak's fallback for an eye looking straight down the fall line. The stereo text keeps
    // that degenerate case; it is no pose of the snow flake this suite is about.
    private const string RainFallback = "right = rl > 1e-4 ? right / rl : INV_VIEW_MATRIX[0].xyz;";

    // ⚠ Under a headset a quad copying the view's basis lies in the head's view plane. It is seen
    // turning edge-on as the head looks away and rolling with every tilt. The flat texts keep that
    // basis on purpose: the flat goldens pin them.
    [Suite("xr-sprite-facing",
        "Under the stereo path the puffer sprites, the heat shimmer, the snow flakes and the "
        + "Enhanced cloud-field grade take the facade basis toward the eye and no column of the view "
        + "matrix, each stereo text compiles, and the flat texts keep the view basis they always had")]
    internal static void XrSpriteFacing(TestContext ctx)
    {
        var texts = new List<(string Name, string? Flat, string Stereo)>
        {
            ("heat shimmer", HeatShimmer.CodeFor(stereo: false), HeatShimmer.CodeFor(stereo: true)),
            ("precipitation", Precipitation.CodeFor(stereo: false), Precipitation.CodeFor(stereo: true)),
        };
        foreach (bool mix in new[] { false, true })
        {
            foreach (bool soft in new[] { false, true })
            {
                foreach (bool enhanced in new[] { false, true })
                {
                    texts.Add(($"emitter {(mix ? "mix" : "add")}{(soft ? " soft" : "")}{(enhanced ? " enhanced" : "")}",
                        MultiMeshEmitterRenderer.VariantCode(mix, soft, enhanced, stereo: false),
                        MultiMeshEmitterRenderer.VariantCode(mix, soft, enhanced, stereo: true)));
                }
            }
        }
        foreach (bool lit in new[] { false, true })
        {
            foreach (bool pooled in new[] { false, true })
            {
                texts.Add(($"cloud-field card enhanced{(lit ? " lit" : "")}{(pooled ? " pooled" : "")}",
                    FogVolumeClutter.ShaderCodeFor(lit, fogged: true, enhanced: true, pooled, stereo: false),
                    FogVolumeClutter.ShaderCodeFor(lit, fogged: true, enhanced: true, pooled, stereo: true)));
            }
        }

        foreach (var (name, flat, stereo) in texts)
        {
            ctx.Check(stereo.Contains("csky_facade_spherical(", System.StringComparison.Ordinal)
                      && stereo.Contains(StereoFacing.Include, System.StringComparison.Ordinal),
                $"the stereo {name} poses by the facade look-at toward the eye");
            ctx.Check(!ReadsViewBasis(stereo.Replace(RainFallback, string.Empty, System.StringComparison.Ordinal)),
                $"the stereo {name} builds nothing from a column of the view matrix");
            if (flat != null)
            {
                ctx.Check(ReadsViewBasis(flat), $"the flat {name} keeps the view basis its goldens pin");
            }

            // Godot parses the text when it is set. A parse error reaches the harness as an engine
            // error, and a uniform list read waits for that parse.
            var shader = new Shader { Code = stereo };
            ctx.Check(shader.GetShaderUniformList() != null, $"the stereo {name} compiles");
        }
        ctx.Note($"{texts.Count} stereo sprite texts checked");
    }

    private static bool ReadsViewBasis(string code) =>
        code.Contains("INV_VIEW_MATRIX[0]", System.StringComparison.Ordinal)
        || code.Contains("INV_VIEW_MATRIX[1]", System.StringComparison.Ordinal)
        || code.Contains("INV_VIEW_MATRIX[2]", System.StringComparison.Ordinal);
}

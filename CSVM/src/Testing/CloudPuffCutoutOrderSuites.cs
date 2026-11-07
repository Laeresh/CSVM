using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>Whether a cloud puff in front of a cutout world surface covers it under Enhanced, on drawn
/// pixels. Coverage puts the cutout in the transparent pass beside the puff field. The field is one
/// MultiMesh sorted on its centre, so the depth sort alone draws a nearer cutout last.</summary>
internal static class CloudPuffCutoutOrderSuites
{
    private const string Chapter = "C1C";

    private const int PaneEdge = 96;

    // The card's range. The plate stands half as far again behind it, both inside the deck's fade.
    private const float Range = 600f;

    // Where the second instance stands, off the pane's axis and far out. The bounds then put the sort
    // point beyond the plate, as the whole deck's centre does in the world.
    private const float AsideRange = 8f * Range;

    [Suite("cloud-puff-cutout-order",
        "under Enhanced a C1C cloud puff card in front of a cutout world surface covers it as it "
        + "covers a surface drawn ahead of every transparent draw, although the field's MultiMesh "
        + "sorts farther than the cutout: rendered into an off-screen pane, asserted on changed pixel "
        + "counts against the same pose with the cutout forced first")]
    internal static void PuffOverCutout(TestContext ctx)
    {
        TextureSet texturesPath = ctx.ChapterTextures(Chapter);
        var chapterGamez = ctx.ChapterGamez(Chapter);
        ZrdrSet zrdr = ctx.ChapterZrdr(Chapter);
        ctx.RequireData(texturesPath, $"{Chapter} textures");
        ctx.RequireData(chapterGamez, $"{Chapter} gamez");
        ctx.RequireData(zrdr, $"{Chapter} fogvol.zrd");
        var gamez = chapterGamez!;
        using var textures = new TextureArchive(texturesPath);

        Utils.GraphicsMode.Resolve(Utils.GraphicsMode.EnhancedWord);
        FogVolumeClutter? field = null;
        Node3D? plate = null;
        MultiMeshInstance3D? cards = null;
        SubViewport? pane = null;
        try
        {
            // Configured as WorldBuilder configures the world's, as the card depth suite does.
            var scene = new SceneBuilder(gamez, textures, fullbright: true, cullBackfaces: true);
            plate = CutoutPlate(ctx, gamez, scene);
            Utils.Rng.Rewind();
            field = FogVolumeClutter.Create(gamez, textures, FogVolumeSpec.Load(zrdr), FogVolumeSpec.VolumesOf(gamez));
            var kind = field == null ? null : FirstKind(field);
            ctx.Check(plate != null, $"{Chapter} skins a world surface with an Enhanced cutout material");
            ctx.Check(kind != null, $"{Chapter} builds a cloud field with a puff kind");
            if (plate == null || kind == null)
            {
                return;
            }

            cards = Cards(kind, out float span);
            pane = new SubViewport
            {
                Size = new Vector2I(PaneEdge, PaneEdge),
                OwnWorld3D = true,
                World3D = new World3D(),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                RenderTargetClearMode = SubViewport.ClearMode.Always,
            };
            var eye = new Camera3D
            {
                Fov = Mathf.Clamp(Mathf.RadToDeg(2f * Mathf.Atan(span * 0.4f / Range)), 1f, 60f),
                Current = true,
                Near = 1f,
                Far = 2f * AsideRange,
            };
            pane.AddChild(eye);
            ctx.Host.AddChild(pane);
            pane.AddChild(plate);
            pane.AddChild(cards);

            cards.Visible = false;
            plate.Visible = false;
            byte[] empty = ClutterCardDepthSuites.Shot(pane);
            plate.Visible = true;
            ClutterCardDepthSuites.PosePlate(plate, eye, -Range * 1.5f);
            byte[] plateOnly = ClutterCardDepthSuites.Shot(pane);
            // Which of the one-sided quad's faces its winding calls the front is the mesh's business.
            if (ClutterCardDepthSuites.Differing(empty, plateOnly) == 0)
            {
                plate.Rotate(Vector3.Right, Mathf.Pi);
                plateOnly = ClutterCardDepthSuites.Shot(pane);
            }
            cards.Visible = true;
            byte[] both = ClutterCardDepthSuites.Shot(pane);
            byte[] forced = ForcedFirst(pane, plate);

            int drawnPlate = ClutterCardDepthSuites.Differing(empty, plateOnly);
            int drawnCard = ClutterCardDepthSuites.Differing(plateOnly, both);
            int reordered = ClutterCardDepthSuites.Differing(forced, both);
            ctx.Check(drawnPlate > 100, $"the cutout surface fills the pane pixels={drawnPlate}");
            ctx.Check(drawnCard > 100, $"the puff card draws over the cutout pixels={drawnCard}");
            ctx.Same(0L, reordered,
                $"the puff composites over the cutout as over a surface drawn first pixels={reordered}");
            ctx.Note($"{Chapter} puff over cutout: plate={drawnPlate} card={drawnCard} reordered={reordered}");
        }
        finally
        {
            cards?.Free();
            plate?.Free();
            field?.Free();
            pane?.Free();
            Utils.GraphicsMode.Resolve(Utils.GraphicsMode.Default);
        }
    }

    // The broadest world mesh this chapter skins with an Enhanced coverage cutout, as a plate. It
    // carries the real material, since the question is where that material sorts.
    private static Node3D? CutoutPlate(TestContext ctx, GameZ gamez, SceneBuilder scene)
    {
        ArrayMesh? bestMesh = null;
        Material? bestMaterial = null;
        float best = 0f;
        for (int i = 0; i < gamez.Meshes.Count; i++)
        {
            if (scene.SharedMesh(i) is not { } built)
            {
                continue;
            }
            var box = built.GetAabb();
            float spanXz = Mathf.Min(box.Size.X, box.Size.Z);
            if (spanXz <= best)
            {
                continue;
            }
            for (int s = 0; s < built.GetSurfaceCount(); s++)
            {
                var material = built.SurfaceGetMaterial(s);
                if (scene.AlphaOf(material) == SceneBuilder.TransparencyClass.ScissorSurface
                    && material is ShaderMaterial { Shader: { } shader }
                    && shader.Code.Contains("alpha_to_coverage", System.StringComparison.Ordinal))
                {
                    (bestMesh, bestMaterial, best) = (built, material, spanXz);
                    break;
                }
            }
        }
        if (bestMesh != null)
        {
            ctx.Note($"{Chapter} cutout plate: extent={bestMesh.GetAabb().Size} priority={bestMaterial!.RenderPriority}");
        }
        return ClutterCardDepthSuites.PlateOf(bestMesh, bestMaterial);
    }

    private static MultiMeshInstance3D? FirstKind(Node field)
    {
        foreach (var child in field.GetChildren())
        {
            if (child is MultiMeshInstance3D { Multimesh.InstanceCount: > 0, MaterialOverride: not null } kind)
            {
                return kind;
            }
        }
        return null;
    }

    // Two cards of the kind: one centred on the pane's axis facing the eye, one far aside.
    private static MultiMeshInstance3D Cards(MultiMeshInstance3D kind, out float span)
    {
        var source = kind.Multimesh;
        var basis = source.GetInstanceTransform(0).Basis;
        var size = source.Mesh.GetAabb().Size * basis.Scale;
        span = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        float band = source.GetInstanceCustomData(0).A;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = source.Mesh,
            InstanceCount = 2,
        };
        // The normal faces +Z, toward the eye, so the angled fade keeps the card whole.
        var facing = new Color(0.5f, 0.5f, 1f, band);
        mm.SetInstanceTransform(0, new Transform3D(basis, new Vector3(0f, 0f, -Range)));
        mm.SetInstanceCustomData(0, facing);
        mm.SetInstanceTransform(1, new Transform3D(basis, new Vector3(AsideRange, 0f, -AsideRange)));
        mm.SetInstanceCustomData(1, facing);
        return new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = kind.MaterialOverride,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = kind.ExtraCullMargin,
        };
    }

    // The pose again with the plate's material at the lowest render priority, the order the frame
    // should already show.
    private static byte[] ForcedFirst(SubViewport pane, Node3D plate)
    {
        if (ClutterCardDepthSuites.FirstMesh(plate) is not { MaterialOverride: { } real } mesh)
        {
            return System.Array.Empty<byte>();
        }
        var first = (Material)real.Duplicate();
        first.RenderPriority = (int)RenderingServer.MaterialRenderPriorityMin;
        mesh.MaterialOverride = first;
        byte[] shot = ClutterCardDepthSuites.Shot(pane);
        mesh.MaterialOverride = real;
        return shot;
    }
}

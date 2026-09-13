using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

internal static class CloudFieldSuites
{
    // How far a decoded normal may stray from unit length before the [0, 1] encoding is suspect.
    private const float NormalTolerance = 1e-3f;

    // Y above this is "straight up"; below this magnitude is "a wall". A slab's top face lands
    // exactly on 1 and a street prism's walls exactly on 0, so nothing shipped sits between them.
    private const float UpCosine = 0.99f;
    private const float WallCosine = 0.1f;

    // Per chapter: the pinned placement counts (base, map-edge extension) and how much of the
    // field may face straight up. C1's volumes are one flat slab, so every sprite sits on a top
    // face; C5's are tall street prisms filled through their interior, so the majority take a
    // wall and the field reads as a shell rather than a lid. The counts are
    // docs/formats/fogvol.md's own and must not move when the fade band is drawn.
    private static readonly (string Chapter, int Base, int Extension, float MaxUpFraction)[] Fields =
    {
        ("C1", 9025, 13176, 1.0f),
        ("C5", 16170, 0, 0.5f),
    };

    [Suite("cloud-field-fade",
        "every fvol cloud sprite carries the polygon normal its draw distance is scaled against "
        + "and one draw of its own fade band: C1's deck slab and its map-edge ring are +Y "
        + "throughout, C5's street prisms take their own walls, the band spans both authored "
        + "far_fade_range pairs, and neither chapter's pinned placement counts move")]
    internal static void CloudFieldFade(TestContext ctx)
    {
        foreach (var (chapter, expectBase, expectExtension, maxUp) in Fields)
        {
            string zrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, chapter);
            string texturePath = SessionPaths.ChapterTextures(ctx.DataRoot, chapter);
            ctx.RequireData(zrdr, $"{chapter} fogvol.zrd");
            ctx.RequireData(texturePath, $"{chapter} textures");
            ctx.WithWorld(chapter, collision: false, world =>
            {
                using var textures = new TextureArchive(texturePath);
                var spec = FogVolumeSpec.Load(zrdr);
                // ⚠ Once per chapter, not once per suite: the scatter takes a fresh generator off
                // the shared cloud stream, so a second field built in the same process draws a
                // different realization than a session's one-and-only field, off the pinned count.
                Utils.Rng.Rewind();
                var field = FogVolumeClutter.Create(world.Gamez, textures, spec,
                    FogVolumeSpec.VolumesOf(world.Gamez));
                ctx.Check(field != null, $"{chapter} builds an ambient cloud field");
                if (field == null)
                {
                    return;
                }
                try
                {
                    ctx.Same(expectBase, field.BaseCount, $"{chapter} cloud sprites in the authored volumes");
                    ctx.Same(expectExtension, field.ExtensionCount, $"{chapter} cloud sprites past the map edge");
                    ctx.Note($"{chapter} cloud field: {field.Summary}");
                    CheckBands(ctx, chapter, field, spec!, maxUp);
                }
                finally
                {
                    field.Free();
                }
            });
        }
    }

    // The per-instance half of the fade law: each sprite's custom data must decode to a unit
    // normal and to a draw t that really spans [0, 1], because the shader interpolates BOTH
    // authored far_fade_range pairs with that one t. A constant t would collapse the field's edge
    // back into the rim the law exists to replace.
    private static void CheckBands(TestContext ctx, string chapter, FogVolumeClutter field,
        FogVolumeSpec spec, float maxUp)
    {
        int total = 0, up = 0, wall = 0, badNormal = 0, badDraw = 0;
        float minDraw = 1f, maxDraw = 0f;
        foreach (var child in field.GetChildren())
        {
            if (child is not MultiMeshInstance3D { Multimesh: { } mm })
            {
                continue;
            }
            ctx.Check(mm.UseCustomData, $"{chapter} {child.Name} carries a custom-data slot");
            for (int i = 0; i < mm.InstanceCount; i++)
            {
                var data = mm.GetInstanceCustomData(i);
                var normal = new Vector3((data.R * 2f) - 1f, (data.G * 2f) - 1f, (data.B * 2f) - 1f);
                total++;
                if (Mathf.Abs(normal.Length() - 1f) > NormalTolerance)
                {
                    badNormal++;
                }
                if (data.A is < 0f or > 1f)
                {
                    badDraw++;
                }
                minDraw = Mathf.Min(minDraw, data.A);
                maxDraw = Mathf.Max(maxDraw, data.A);
                if (normal.Y > UpCosine)
                {
                    up++;
                }
                if (Mathf.Abs(normal.Y) < WallCosine)
                {
                    wall++;
                }
            }
        }

        ctx.Same(field.InstanceCount, total, $"{chapter} sprites carrying a band");
        ctx.Same(0, badNormal, $"{chapter} sprites whose normal is not unit length");
        ctx.Same(0, badDraw, $"{chapter} sprites whose band draw is outside [0, 1]");
        ctx.Check(minDraw < 0.01f && maxDraw > 0.99f,
            $"{chapter} band draw spans its range min={minDraw:0.000} max={maxDraw:0.000}");
        float upFraction = total == 0 ? 0f : (float)up / total;
        ctx.Check(upFraction <= maxUp,
            $"{chapter} sprites on an upward face {upFraction:0.000} within {maxUp:0.000}");
        ctx.Check(maxUp >= 1f || wall > total / 8,
            $"{chapter} sprites on a wall {wall} of {total}");
        ctx.Note($"{chapter} cloud normals: {up} up, {wall} on a wall, of {total}");

        // The two pairs the shader interpolates are the reader's, not one of them twice: a block
        // that lost a pair would fade every sprite in the same metres again.
        foreach (var block in spec.Clutter)
        {
            ctx.Check(block.FarFadeNear.Y > 0f && block.FarFade.Y > block.FarFadeNear.Y,
                $"{chapter} block bands {block.FarFadeNear} then {block.FarFade}");
        }
    }
}

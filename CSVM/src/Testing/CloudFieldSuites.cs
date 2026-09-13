using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

internal static class CloudFieldSuites
{
    // How far a decoded normal may stray from unit length before the [0, 1] encoding is suspect.
    private const float NormalTolerance = 1e-3f;

    // Y above this is "straight up"; below this magnitude is "a wall". A flat top face lands
    // exactly on 1 and nothing shipped reaches the second, which is itself the finding: every
    // authored fvol face points upward, steeply or gently.
    private const float UpCosine = 0.99f;
    private const float WallCosine = 0.1f;

    // How close a face normal must be to a sprite's own before that face is a candidate for the
    // one it was scattered on. Two authored faces never share a normal this closely unless they
    // are parallel, and a parallel face is still tested on its own distance.
    private const float SameNormal = 0.999f;

    // Per chapter: the pinned placement counts (base, map-edge extension) and the normal tally
    // (sprites on a face pointing straight up, sprites on a sloped one). C1's nine slab volumes
    // author one flat top face each, so its whole field and its ring are +Y; C1C's build-ups and
    // C5's ramped prisms carry sloped faces, which is what makes those two read as a shell. The
    // base counts are docs/formats/fogvol.md's own.
    private static readonly (string Chapter, int Base, int Extension, int Up, int Sloped)[] Fields =
    {
        ("C1", 10524, 13176, 23700, 0),
        ("C1C", 11452, 13176, 23386, 1242),
        ("C5", 19197, 0, 17095, 2102),
    };

    [Suite("cloud-field-fade",
        "every fvol cloud sprite sits on one authored face and carries that face's own normal, "
        + "which its draw distance is scaled against, plus one draw of its own fade band: C1's "
        + "flat deck faces and its map-edge ring are +Y throughout, C1C's build-ups and C5's "
        + "ramped prisms carry sloped faces, no shipped face points down or sideways, the band "
        + "spans both authored far_fade_range pairs, the card sampler fetches through the "
        + "chapter's mip LOD bias, and the pinned placement counts hold")]
    internal static void CloudFieldFade(TestContext ctx)
    {
        foreach (var (chapter, expectBase, expectExtension, expectUp, expectSloped) in Fields)
        {
            string zrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, chapter);
            string texturePath = SessionPaths.ChapterTextures(ctx.DataRoot, chapter);
            ctx.RequireData(zrdr, $"{chapter} fogvol.zrd");
            ctx.RequireData(texturePath, $"{chapter} textures");
            ctx.WithWorld(chapter, collision: false, world =>
            {
                using var textures = new TextureArchive(texturePath);
                var spec = FogVolumeSpec.Load(zrdr);
                var volumes = FogVolumeSpec.VolumesOf(world.Gamez);
                // ⚠ Once per chapter, not once per suite: the scatter takes a fresh generator off
                // the shared cloud stream, so a second field built in the same process draws a
                // different realization than a session's one-and-only field, off the pinned count.
                Utils.Rng.Rewind();
                var field = FogVolumeClutter.Create(world.Gamez, textures, spec, volumes);
                ctx.Check(field != null, $"{chapter} builds an ambient cloud field");
                if (field == null)
                {
                    return;
                }
                try
                {
                    ctx.Same(expectBase, field.BaseCount, $"{chapter} cloud sprites on the authored faces");
                    ctx.Same(expectExtension, field.ExtensionCount, $"{chapter} cloud sprites past the map edge");
                    ctx.Note($"{chapter} cloud field: {field.Summary}");
                    CheckBands(ctx, chapter, field, spec!, expectUp, expectSloped);
                    CheckNormalsAreTheirOwnFaces(ctx, chapter, field, spec!, volumes);
                    CheckCardSampler(ctx, chapter, field);
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
        FogVolumeSpec spec, int expectUp, int expectSloped)
    {
        int total = 0, up = 0, sloped = 0, down = 0, wall = 0, badNormal = 0, badDraw = 0;
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
                else if (normal.Y < -UpCosine)
                {
                    down++;
                }
                else if (Mathf.Abs(normal.Y) < WallCosine)
                {
                    wall++;
                }
                else
                {
                    sloped++;
                }
            }
        }

        ctx.Same(field.InstanceCount, total, $"{chapter} sprites carrying a band");
        ctx.Same(0, badNormal, $"{chapter} sprites whose normal is not unit length");
        ctx.Same(0, badDraw, $"{chapter} sprites whose band draw is outside [0, 1]");
        ctx.Check(minDraw < 0.01f && maxDraw > 0.99f,
            $"{chapter} band draw spans its range min={minDraw:0.000} max={maxDraw:0.000}");
        ctx.Same(expectUp, up, $"{chapter} sprites on a face pointing straight up");
        ctx.Same(expectSloped, sloped, $"{chapter} sprites on a sloped face");
        // The authored volumes are closed boxes and prisms whose walls and floors carry the
        // no_clutter bit, so the scatter only ever sees their upward skin. A sprite facing down or
        // sideways means that skip stopped working and the field became a shell of the box.
        ctx.Same(0, down + wall, $"{chapter} sprites on a downward or vertical face");
        ctx.Note($"{chapter} cloud normals: {up} up, {sloped} sloped, of {total}");

        // The two pairs the shader interpolates are the reader's, not one of them twice: a block
        // that lost a pair would fade every sprite in the same metres again.
        foreach (var block in spec.Clutter)
        {
            ctx.Check(block.FarFadeNear.Y > 0f && block.FarFade.Y > block.FarFadeNear.Y,
                $"{chapter} block bands {block.FarFadeNear} then {block.FarFade}");
        }
    }

    // The fade law wants each sprite scaled against the normal of the face it was scattered on, so
    // a sprite naming a normal no face it touches carries is the stand-in this replaced. Measured
    // as a distance: a sprite must lie within the authored perpendicular offset plus half the
    // perturbation of a face whose normal is its own.
    private static void CheckNormalsAreTheirOwnFaces(TestContext ctx, string chapter,
        FogVolumeClutter field, FogVolumeSpec spec, IReadOnlyList<FogVolumeBox> volumes)
    {
        float budget = 0.1f;
        foreach (var block in spec.Clutter)
        {
            budget = Mathf.Max(budget, Mathf.Max(
                Mathf.Abs(block.PerpDistRange.X), Mathf.Abs(block.PerpDistRange.Y))
                + (block.PerturbDistRange.Y * 0.5f) + 0.1f);
        }

        var planes = new List<Plane>();
        foreach (var volume in volumes)
        {
            foreach (var face in volume.Polygons ?? System.Array.Empty<FogVolumeFace>())
            {
                planes.Add(new Plane(face.Normal, face.Normal.Dot(face.Vertices[0])));
            }
        }

        int orphans = 0;
        float worst = 0f;
        foreach (var child in field.GetChildren())
        {
            if (child is not MultiMeshInstance3D { Multimesh: { } mm })
            {
                continue;
            }
            for (int i = 0; i < mm.InstanceCount; i++)
            {
                var data = mm.GetInstanceCustomData(i);
                var normal = new Vector3((data.R * 2f) - 1f, (data.G * 2f) - 1f, (data.B * 2f) - 1f);
                var position = mm.GetInstanceTransform(i).Origin;
                float nearest = float.MaxValue;
                foreach (var plane in planes)
                {
                    if (plane.Normal.Dot(normal) >= SameNormal)
                    {
                        nearest = Mathf.Min(nearest, Mathf.Abs(plane.DistanceTo(position)));
                    }
                }
                if (nearest > budget)
                {
                    orphans++;
                }
                else
                {
                    worst = Mathf.Max(worst, nearest);
                }
            }
        }

        ctx.Same(0, orphans, $"{chapter} sprites further than {budget:0.0} m from a face carrying their own normal");
        ctx.Note($"{chapter} worst sprite-to-own-face distance {worst:0.00} m of {budget:0.0} m allowed");
    }

    // The card declares mip levels, so it must fetch through the one function carrying the
    // chapter's authored LOD bias, like every other mip-mapped arm. Asserted here rather than in
    // chapter-census: only a game session builds this field, so a census world carries none of it.
    private static void CheckCardSampler(TestContext ctx, string chapter, FogVolumeClutter field)
    {
        int examined = 0;
        foreach (var child in field.GetChildren())
        {
            if (child is not MultiMeshInstance3D { MaterialOverride: ShaderMaterial { Shader: { } shader } })
            {
                continue;
            }
            string code = shader.Code;
            ctx.Check(code.Contains("filter_linear_mipmap", System.StringComparison.Ordinal)
                      && code.Contains("csky_sample_albedo(albedo_tex", System.StringComparison.Ordinal)
                      && !code.Contains("texture(albedo_tex", System.StringComparison.Ordinal),
                $"{chapter} {child.Name} card sampler fetches through the chapter mip bias");
            examined++;
        }
        ctx.Check(examined > 0, $"{chapter} cloud card materials examined count={examined}");
    }
}

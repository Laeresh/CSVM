using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

internal static class CloudFieldSuites
{
    // How far a decoded normal may stray from unit length before the [0, 1] encoding is suspect.
    private const float NormalTolerance = 1e-3f;

    // Y above this is "straight up"; below this magnitude is "a wall". A flat top face lands
    // exactly on 1, and nothing shipped reaches the second. That is itself the finding: every
    // authored fvol face points upward, steeply or gently.
    private const float UpCosine = 0.99f;
    private const float WallCosine = 0.1f;

    // How close a face normal must be to a sprite's own before that face is a candidate for the
    // one it was scattered on. Two authored faces never share a normal this closely unless they
    // are parallel, and a parallel face is still tested on its own distance.
    private const float SameNormal = 0.999f;

    // How far apart the two headings' away-side factors must land before the card counts as
    // directionally shaded. The authored bottom normals sit about 18 degrees off the card's own
    // +Z. A half-turn swings them right across the light, and the real gap is far wider.
    private const float DirectionalMargin = 0.05f;

    // A SUNLIGHT pair that neither flattens nor saturates the law, and below it the oblique
    // bearing it runs on. A chapter's own numbers would make this a reading of one mission.
    private const float ProbeAmbient = 0.6f;
    private const float ProbeDiffuse = 0.4f;

    // A --cloud-jitter value wide enough that nearly every lattice card moves, in metres.
    private const float ProbeJitter = 40f;

    // The authored cloud masks' constant RGB: C1's white card and C5's dark haze.
    private const float WhiteCard = 239f / 255f;
    private const float DarkHaze = 25f / 255f;

    // The rendered pool's size, data/cloud_puffs/veil_1 to veil_8.
    private const int PuffPoolSize = 8;

    // The placed clusters' pool's size, data/cloud_puffs/far_1 to far_6.
    private const int FarPoolSize = 6;

    private static readonly Vector3 ProbeSun = new Vector3(0.62f, 0.3f, 0.72f).Normalized();

    // The two fvol kinds every shipped deck chapter scatters, and the masks they are skinned with
    // (docs/formats/fogvol.md). C1's masks are RGB 239 and C5's RGB 25, so the pair shows the tint
    // carrying each chapter's own colour rather than the puff's.
    private static readonly string[] PuffKinds = { "cloudsprite1", "cloudsprite2" };
    private static readonly string[] AuthoredMasks = { "cloud1.tif", "cloud2.tif" };
    private static readonly string[] PuffChapters = { "C1", "C5" };

    // Per chapter: the pinned placement counts (base, map-edge extension), the normal tally, and
    // the authored `lighting` flag of the chapter's own cloud card. The tally is sprites on a face
    // pointing straight up, and sprites on a sloped one. C1's nine slab volumes author one flat
    // top face each, so its whole field and its ring are +Y. C1C's build-ups and C5's ramped
    // prisms carry sloped faces, which is what makes those two read as a shell. The base counts
    // are docs/formats/fogvol.md's own, the flag is docs/org/vertexLighting.md's gate.
    private static readonly (string Chapter, int Base, int Extension, int Up, int Sloped, bool Lit)[] Fields =
    {
        ("C1", 10524, 13176, 23700, 0, false),
        ("C1C", 11452, 13176, 23386, 1242, true),
        ("C5", 19197, 0, 17095, 2102, true),
    };

    // Per chapter: how many fvol volumes the gamez ships and whether its fogvol.zrd arms the
    // in-volume whiteout. The counts are docs/formats/fogvol.md's own census.
    private static readonly (string Chapter, int Volumes, bool Armed)[] CensusChapters =
    {
        ("C1", 9, false),
        ("C5", 17, true),
    };

    [Suite("cloud-field-fade",
        "every fvol cloud sprite sits on one authored face and carries that face's own normal, "
        + "which its draw distance is scaled against, plus one draw of its own fade band: C1's "
        + "flat deck faces and its map-edge ring are +Y throughout, C1C's build-ups and C5's "
        + "ramped prisms carry sloped faces, no shipped face points down or sideways, the band "
        + "spans both authored far_fade_range pairs, the card sampler fetches through the "
        + "chapter's mip LOD bias, a card authored `lighting: true` carries its three authored "
        + "normals and reads brighter on the side it turns toward the light than on the side it "
        + "turns away, a card authored false reads flat, the pinned placement counts hold, and "
        + "the remake-only --cloud-jitter moves every lattice card on X/Z within its value and "
        + "nothing else")]
    internal static void CloudFieldFade(TestContext ctx)
    {
        foreach (var (chapter, expectBase, expectExtension, expectUp, expectSloped, lit) in Fields)
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
                // ⚠ Once per chapter, not once per suite. The scatter takes a fresh generator off
                // the shared cloud stream. A second field built in the same process draws a
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
                    CheckCardPose(ctx, chapter, field);
                    CheckVertexLight(ctx, chapter, field, lit);
                    Utils.Rng.Rewind();
                    var jittered = FogVolumeClutter.Create(world.Gamez, textures, spec, volumes, ProbeJitter);
                    try
                    {
                        CheckJitter(ctx, chapter, field, jittered);
                    }
                    finally
                    {
                        jittered?.Free();
                    }
                }
                finally
                {
                    field.Free();
                }
            });
        }
    }

    [Suite("cloud-puffs",
        "the rendered cloud puff pool replaces the authored fvol masks under Enhanced Graphics "
        + "alone: on the faithful presentation every card samples its authored mask and never the "
        + "pool; under Enhanced both mapped kinds sample the whole pool, tinted by the mask's own "
        + "colour and peak opacity, with the rim sample deepened by the size ratio, and each card "
        + "picks its puff, tilt, mirror and size off a hash of its own position, so the placements "
        + "match the faithful field's and a second build's exactly; C1's white cards and C5's "
        + "dark haze both keep their authored colour; and C1's placed cloudparent sprites draw "
        + "their masks on the faithful presentation and the fuller placed-cloud pool under "
        + "Enhanced, every one of them, at the same places")]
    internal static void CloudPuffPools(TestContext ctx)
    {
        var pool = CloudPuffs.Deck();
        ctx.Check(pool != null && pool.GetLayers() == PuffPoolSize && pool.GetWidth() > 64 && pool.HasMipmaps(),
            $"the rendered puff pool reads as {PuffPoolSize} mipmapped layers: {pool?.GetLayers()} of {pool?.GetWidth()}px");
        foreach (var kindName in PuffKinds)
        {
            ctx.Check(FogVolumeClutter.DrawsRenderedPuffs(kindName), $"{kindName} draws from the rendered pool");
        }
        foreach (var chapter in PuffChapters)
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
                var authored = new List<Rid>();
                foreach (var name in AuthoredMasks)
                {
                    if (textures.Find(name) is { } mask)
                    {
                        authored.Add(mask.GetRid());
                    }
                }
                Utils.GraphicsMode.Resolve(Utils.GraphicsMode.Default);
                var faithful = CheckPuffArm(ctx, chapter, BuildField(world.Gamez, textures, spec, volumes), authored, pool, enhanced: false);
                Utils.GraphicsMode.Resolve(Utils.GraphicsMode.EnhancedWord);
                try
                {
                    var enhanced = CheckPuffArm(ctx, chapter, BuildField(world.Gamez, textures, spec, volumes), authored, pool, enhanced: true);
                    var again = CheckPuffArm(ctx, chapter, BuildField(world.Gamez, textures, spec, volumes), authored, pool, enhanced: true);
                    // The pose is a function of the placement alone, so equal placements mean equal
                    // choices. The Enhanced field must not move off the faithful one.
                    ctx.Check(SamePlacements(faithful, enhanced), $"{chapter} Enhanced placements match the faithful field's");
                    ctx.Check(SamePlacements(enhanced, again), $"{chapter} a second Enhanced build places every card identically");
                }
                finally
                {
                    Utils.GraphicsMode.Resolve(Utils.GraphicsMode.Default);
                }
            });
        }
        CheckPlacedClouds(ctx);
        ctx.Check(!Utils.GraphicsMode.Enhanced, $"the graphics setting is back on the faithful presentation");
    }

    [Suite("fogvol-census",
        "each deck chapter's gamez ships the fvol volume count docs/formats/fogvol.md records, "
        + "and its fogvol.zrd arms the in-volume whiteout exactly where the census says: C5's "
        + "street prisms and nowhere in C1")]
    internal static void FogVolumeCensus(TestContext ctx)
    {
        foreach (var (chapter, expectVolumes, armed) in CensusChapters)
        {
            string zrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, chapter);
            ctx.RequireData(zrdr, $"{chapter} fogvol.zrd");
            ctx.WithWorld(chapter, collision: false, world =>
            {
                var spec = FogVolumeSpec.Load(zrdr);
                var volumes = FogVolumeSpec.VolumesOf(world.Gamez);
                var whiteout = FogVolumeWhiteout.From(spec, volumes);
                ctx.Same(expectVolumes, volumes.Count, $"{chapter} authored fvol volumes");
                ctx.Check(whiteout.Armed == armed, $"{chapter} fogvol.zrd arms the in-volume whiteout: {whiteout.Armed}");
            });
        }
    }

    private static FogVolumeClutter? BuildField(GameZ gamez, TextureArchive textures, FogVolumeSpec? spec,
        IReadOnlyList<FogVolumeBox> volumes)
    {
        Utils.Rng.Rewind();
        return FogVolumeClutter.Create(gamez, textures, spec, volumes);
    }

    // One presentation's cards: which sprite each samples, and what the enhanced arm carries over.
    // Returns every card's transform and custom data, kind by kind, for the placement comparison.
    private static List<(Transform3D, Color)> CheckPuffArm(TestContext ctx, string chapter, FogVolumeClutter? field,
        List<Rid> authored, Texture2DArray? pool, bool enhanced)
    {
        var placements = new List<(Transform3D, Color)>();
        string arm = enhanced ? "Enhanced" : "faithful";
        ctx.Check(field != null, $"{chapter} builds its cloud field on the {arm} presentation");
        if (field == null)
        {
            return placements;
        }
        try
        {
            int examined = 0;
            foreach (var child in field.GetChildren())
            {
                if (child is not MultiMeshInstance3D { MaterialOverride: ShaderMaterial mat, Multimesh: { } mm } instance)
                {
                    continue;
                }
                examined++;
                for (int i = 0; i < mm.InstanceCount; i++)
                {
                    placements.Add((mm.GetInstanceTransform(i), mm.GetInstanceCustomData(i)));
                }
                string kind = instance.Name.ToString();
                string code = mat.Shader.Code;
                if (!enhanced || pool == null || !FogVolumeClutter.DrawsRenderedPuffs(kind))
                {
                    var sampled = mat.GetShaderParameter("albedo_tex").As<Texture2D>();
                    ctx.Check(sampled != null && authored.Contains(sampled.GetRid()), $"{chapter} {arm} {kind} samples its authored mask");
                    ctx.Check(!code.Contains("puff_tex", System.StringComparison.Ordinal),
                        $"{chapter} {arm} {kind} shader never reads the rendered pool");
                    if (!enhanced)
                    {
                        ctx.Check(!code.Contains("cloud_puffs", System.StringComparison.Ordinal),
                            $"{chapter} {arm} {kind} shader carries no puff tint");
                    }
                    continue;
                }
                var sampledPool = mat.GetShaderParameter("puff_tex").As<Texture2DArray>();
                ctx.Check(sampledPool != null && sampledPool.GetRid() == pool.GetRid(), $"{chapter} {arm} {kind} samples the rendered pool");
                ctx.Same(pool.GetLayers(), (int)mat.GetShaderParameter("puff_layers").AsSingle(), $"{chapter} {arm} {kind} picks among every layer");
                // The pose must key on the card's own position, never on its index in the buffer.
                ctx.Check(code.Contains("csky_puff_pose(MODEL_MATRIX[3].xyz", System.StringComparison.Ordinal)
                          && code.Contains("csky_puff_sample(v_puff_card_uv", System.StringComparison.Ordinal)
                          && !code.Contains("INSTANCE_ID", System.StringComparison.Ordinal),
                    $"{chapter} {arm} {kind} picks its puff off a hash of its own position and samples it upright");
                var tint = mat.GetShaderParameter("puff_tint").AsColor();
                var want = chapter == "C5" ? DarkHaze : WhiteCard;
                ctx.Check(Mathf.Abs(tint.R - want) < 0.02f && Mathf.Abs(tint.G - want) < 0.03f && tint.A > 0.9f,
                    $"{chapter} {arm} {kind} carries its authored mask colour {tint} (about {want:0.00})");
                float lod = mat.GetShaderParameter("rim_lod").AsSingle();
                float wantLod = 3f + Mathf.Log(pool.GetWidth() / 64f) / Mathf.Log(2f);
                ctx.Check(Mathf.Abs(lod - wantLod) < 1e-3f, $"{chapter} {arm} {kind} rim sample at level {lod:0.##}");
            }
            ctx.Check(examined > 0, $"{chapter} {arm} cloud card materials examined count={examined}");
        }
        finally
        {
            field.Free();
        }
        return placements;
    }

    // The placed cloud clusters, C1's cloudparent facades. The faithful world draws every one with
    // its authored mask and none from a pool. An Enhanced world draws the same sprites at the same
    // places from the fuller pool, tinted by the mask's colour and posed off its position.
    private static void CheckPlacedClouds(TestContext ctx)
    {
        var far = CloudPuffs.Far();
        ctx.Check(far != null && far.GetLayers() == FarPoolSize && far.GetWidth() > 64 && far.HasMipmaps(),
            $"the placed-cloud pool reads as {FarPoolSize} mipmapped layers: {far?.GetLayers()} of {far?.GetWidth()}px");
        if (far == null)
        {
            return;
        }
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, "C1"), $"C1 textures");
        Utils.GraphicsMode.Resolve(Utils.GraphicsMode.Default);
        (int Authored, int Pooled, Vector3 Sum) faithful = default, enhanced = default;
        ctx.WithWorld("C1", collision: false, world => faithful = PlacedSprites(ctx, world, far, "faithful"));
        Utils.GraphicsMode.Resolve(Utils.GraphicsMode.EnhancedWord);
        try
        {
            ctx.WithPrivateWorld("C1", collision: false, world => enhanced = PlacedSprites(ctx, world, far, "Enhanced"));
        }
        finally
        {
            Utils.GraphicsMode.Resolve(Utils.GraphicsMode.Default);
        }
        ctx.Check(faithful.Authored > 0 && faithful.Pooled == 0,
            $"C1 faithful placed clouds: {faithful.Authored} with the authored masks, {faithful.Pooled} from a pool");
        ctx.Check(enhanced.Authored == 0 && enhanced.Pooled == faithful.Authored,
            $"C1 Enhanced placed clouds: {enhanced.Pooled} from the pool, {enhanced.Authored} left on the masks");
        ctx.Check(faithful.Sum.IsEqualApprox(enhanced.Sum), $"C1 placed clouds stand where the faithful world puts them");
        ctx.Note($"C1 placed clouds: {faithful.Authored} sprite(s)");
    }

    // Counts one world's cloud sprites by what they sample, checking every pooled one's settings.
    private static (int Authored, int Pooled, Vector3 Sum) PlacedSprites(TestContext ctx, TestWorld world,
        Texture2DArray far, string arm)
    {
        var masks = new List<Rid>();
        foreach (var name in AuthoredMasks)
        {
            if (world.Textures.Find(name) is { } mask)
            {
                masks.Add(mask.GetRid());
            }
        }
        int authored = 0, pooled = 0, wrong = 0;
        var sum = Vector3.Zero;
        var stack = new Stack<Node>();
        stack.Push(world.Stage);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
            if (node is not MeshInstance3D { Mesh: { } mesh } mi)
            {
                continue;
            }
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if ((mi.MaterialOverride ?? mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s))
                    is not ShaderMaterial { Shader: { } shader } mat
                    || !shader.Code.Contains("csky_facade_spherical", System.StringComparison.Ordinal))
                {
                    continue;
                }
                if (mat.GetShaderParameter("puff_tex").As<Texture2DArray>() is { } pool)
                {
                    pooled++;
                    sum += mi.Position;
                    var tint = mat.GetShaderParameter("puff_tint").AsColor();
                    if (pool.GetRid() != far.GetRid() || Mathf.Abs(tint.R - WhiteCard) > 0.02f
                        || !shader.Code.Contains("csky_puff_pose(MODEL_MATRIX[3].xyz", System.StringComparison.Ordinal)
                        || !shader.Code.Contains("csky_puff_sample(v_puff_card_uv", System.StringComparison.Ordinal))
                    {
                        wrong++;
                    }
                }
                else if (mat.GetShaderParameter("albedo_tex").As<Texture2D>() is { } tex && masks.Contains(tex.GetRid()))
                {
                    authored++;
                    sum += mi.Position;
                }
                break;
            }
        }
        ctx.Same(0, wrong, $"C1 {arm} pooled placed clouds off the far pool, tint, position-keyed pose or upright UV");
        return (authored, pooled, sum);
    }

    private static bool SamePlacements(List<(Transform3D, Color)> a, List<(Transform3D, Color)> b)
    {
        if (a.Count == 0 || a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Item1 != b[i].Item1 || a[i].Item2 != b[i].Item2)
            {
                return false;
            }
        }
        return true;
    }

    // The per-instance half of the fade law. Each sprite's custom data must decode to a unit
    // normal and to a draw t that really spans [0, 1]. The shader interpolates BOTH authored
    // far_fade_range pairs with that one t. A constant t would collapse the field's edge
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

        // The two pairs the shader interpolates are the reader's, not one of them twice. A block
        // missing a pair would fade every sprite in the same metres again.
        foreach (var block in spec.Clutter)
        {
            ctx.Check(block.FarFadeNear.Y > 0f && block.FarFade.Y > block.FarFadeNear.Y,
                $"{chapter} block bands {block.FarFadeNear} then {block.FarFade}");
        }
    }

    // The fade law scales each sprite against the normal of the face it was scattered on. A sprite
    // naming a normal no face it touches carries is a stand-in, which this forbids. Measured
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

    // The remake-only jitter must lay the same decoded field and move only where each card sits.
    // Kinds, counts, heights, scales and bands hold, X/Z stays within the knob, and the map-edge
    // ring stands still.
    private static void CheckJitter(TestContext ctx, string chapter, FogVolumeClutter plain,
        FogVolumeClutter? jittered)
    {
        ctx.Check(jittered != null, $"{chapter} builds a jittered cloud field");
        if (jittered == null)
        {
            return;
        }
        ctx.Same(plain.BaseCount, jittered.BaseCount, $"{chapter} jittered cloud sprites on the authored faces");
        ctx.Same(plain.ExtensionCount, jittered.ExtensionCount, $"{chapter} jittered cloud sprites past the map edge");

        var plainMeshes = new List<MultiMesh>();
        var jitterMeshes = new List<MultiMesh>();
        foreach (var child in plain.GetChildren())
        {
            if (child is MultiMeshInstance3D { Multimesh: { } mm })
            {
                plainMeshes.Add(mm);
            }
        }
        foreach (var child in jittered.GetChildren())
        {
            if (child is MultiMeshInstance3D { Multimesh: { } mm })
            {
                jitterMeshes.Add(mm);
            }
        }
        ctx.Same(plainMeshes.Count, jitterMeshes.Count, $"{chapter} jittered cloud kinds");

        int moved = 0, still = 0, outOfBounds = 0, otherwise = 0;
        for (int k = 0; k < Mathf.Min(plainMeshes.Count, jitterMeshes.Count); k++)
        {
            var a = plainMeshes[k];
            var b = jitterMeshes[k];
            ctx.Same(a.InstanceCount, b.InstanceCount, $"{chapter} kind {k} jittered instance count");
            for (int i = 0; i < Mathf.Min(a.InstanceCount, b.InstanceCount); i++)
            {
                var ta = a.GetInstanceTransform(i);
                var tb = b.GetInstanceTransform(i);
                var d = tb.Origin - ta.Origin;
                if (d.Y != 0f || ta.Basis != tb.Basis || a.GetInstanceCustomData(i) != b.GetInstanceCustomData(i))
                {
                    otherwise++;
                }
                if (Mathf.Abs(d.X) > ProbeJitter + 1e-3f || Mathf.Abs(d.Z) > ProbeJitter + 1e-3f)
                {
                    outOfBounds++;
                }
                if (d.X == 0f && d.Z == 0f)
                {
                    still++;
                }
                else
                {
                    moved++;
                }
            }
        }
        ctx.Same(0, otherwise, $"{chapter} jittered sprites whose height, scale or band changed");
        ctx.Same(0, outOfBounds, $"{chapter} jittered sprites moved further than {ProbeJitter:0} m on an axis");
        ctx.Same(plain.BaseCount, moved, $"{chapter} lattice sprites the jitter moved");
        ctx.Same(plain.ExtensionCount, still, $"{chapter} map-edge sprites the jitter left in place");
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

    // ⚠ The pose reads the eye's POSITION and never its basis. A billboard assembled from
    // INV_VIEW_MATRIX's columns carries the camera's roll into every card, which is the one thing
    // the original's per-card tracker cannot do (docs/org/cloudCards.md).
    private static void CheckCardPose(TestContext ctx, string chapter, FogVolumeClutter field)
    {
        int examined = 0;
        foreach (var child in field.GetChildren())
        {
            if (child is not MultiMeshInstance3D { MaterialOverride: ShaderMaterial { Shader: { } shader } })
            {
                continue;
            }

            string code = shader.Code;
            ctx.Check(code.Contains("csky_facade_spherical(", System.StringComparison.Ordinal),
                $"{chapter} {child.Name} card poses through the facade look-at");
            ctx.Check(!code.Contains("INV_VIEW_MATRIX[0]", System.StringComparison.Ordinal)
                      && !code.Contains("INV_VIEW_MATRIX[1]", System.StringComparison.Ordinal)
                      && !code.Contains("INV_VIEW_MATRIX[2]", System.StringComparison.Ordinal),
                $"{chapter} {child.Name} card never builds a basis from the camera's columns");
            examined++;
        }

        ctx.Check(examined > 0, $"{chapter} cloud card materials examined for the pose count={examined}");
    }

    // The lit arm, checked where the collapsed csky_world_light cannot reach: a card's authored
    // normals turn with its pose, so one card reads down toward AMBIENT on the side it turns
    // away from the light and up toward AMBIENT + DIFFUSE on the side it turns toward it
    // (docs/org/vertexLighting.md). A card authored `lighting: false` takes no term at all.
    private static void CheckVertexLight(TestContext ctx, string chapter, FogVolumeClutter field, bool lit)
    {
        var facing = FacadeBasis(ProbeSun);
        var turned = FacadeBasis(-ProbeSun);
        int examined = 0;
        foreach (var child in field.GetChildren())
        {
            if (child is not MultiMeshInstance3D { MaterialOverride: ShaderMaterial { Shader: { } shader } } instance
                || instance.Multimesh?.Mesh is not ArrayMesh card)
            {
                continue;
            }
            string code = shader.Code;
            ctx.Check(code.Contains("csky_sun_vertex_light", System.StringComparison.Ordinal) == lit,
                $"{chapter} {child.Name} card shader carries the per-vertex sun term: {lit}");
            // ⚠ Never the collapsed factor on this population, in either arm: it stands for the
            // world's mean N.L, which no camera-facing card holds.
            ctx.Check(!code.Contains("csky_world_light", System.StringComparison.Ordinal),
                $"{chapter} {child.Name} card shader leaves the collapsed world light alone");

            var arrays = card.SurfaceGetArrays(0);
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            float toward = 0f, away = 0f, flat = 0f;
            int corners = 0;
            for (int v = 0; v < normals.Length && v < vertices.Length; v++)
            {
                // The card's lower corners carry the pair pointing out of it, the normals a turn
                // swings across the light; its top corners carry the one along the card's own +Y,
                // which the pose holds perpendicular to the eye, and so to a probe sun placed there.
                if (vertices[v].Y < 0f)
                {
                    toward += VertexLight(facing * normals[v]);
                    away += VertexLight(turned * normals[v]);
                    corners++;
                }
                else
                {
                    flat += Mathf.Abs(VertexLight(facing * normals[v]) - VertexLight(turned * normals[v]));
                }
            }

            ctx.Check(corners > 0, $"{chapter} {child.Name} card carries lower corners count={corners}");
            if (corners > 0)
            {
                toward /= corners;
                away /= corners;
            }
            if (lit)
            {
                ctx.Check(toward > away + DirectionalMargin,
                    $"{chapter} {child.Name} lit side {toward:0.000} over away side {away:0.000}");
                ctx.Check(away >= ProbeAmbient - NormalTolerance
                          && toward <= ProbeAmbient + ProbeDiffuse + NormalTolerance,
                    $"{chapter} {child.Name} card light stays inside its authored pair");
            }
            // Noted in both arms: the geometry is the same card either way. The unlit chapters'
            // numbers are what their authored flag turns off, not a shortfall in the mesh.
            ctx.Note($"{chapter} {child.Name} lighting={lit}: {toward:0.000} toward the light, {away:0.000} away, tops swing {flat:0.000}");
            examined++;
        }
        ctx.Check(examined > 0, $"{chapter} cloud card meshes examined for the vertex law count={examined}");
    }

    // The basis the card is drawn through, csky_facade_spherical's world-up look-at: the card's +Z
    // at the eye, its +Y the world's up projected off that line. The eye's own basis never enters
    // it (docs/org/cloudCards.md), and a card's authored normals reach the world through this,
    // never through a model transform.
    private static Basis FacadeBasis(Vector3 towardCamera)
    {
        var f = towardCamera.Normalized();
        var right = Vector3.Up.Cross(f);
        if (right.LengthSquared() < 1e-8f)
        {
            right = Vector3.Back.Cross(f);
        }

        right = right.Normalized();
        return new Basis(right, f.Cross(right), f);
    }

    // The decoded per-vertex term itself, on one world-space normal.
    private static float VertexLight(Vector3 worldNormal)
    {
        return ProbeAmbient + (ProbeDiffuse * Mathf.Max(worldNormal.Normalized().Dot(ProbeSun), 0f));
    }
}

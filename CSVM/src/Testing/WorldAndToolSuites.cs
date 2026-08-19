using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session;
using CSVM.UI;
using CSVM.Utils;
using Godot;

using static CSVM.Testing.SuiteConstants;
namespace CSVM.Testing;

internal static class WorldAndToolSuites
{
    internal static void GltfExport(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        Node3D? plane = null;
        string path = Path.Combine(ctx.ScratchDir, $"gltf-export-{ctx.PlaneName}.glb");
        try
        {
            Directory.CreateDirectory(ctx.ScratchDir);
            plane = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ctx.Host.AddChild(plane);

            var err = GltfExporter.Export(plane, path);
            ctx.Same((long)Error.Ok, (long)err, $"export write result");
            bool wrote = File.Exists(path) && new FileInfo(path).Length > 0;
            ctx.Check(wrote, $"exported .glb is present and non-empty path={path}");

            // Re-import the file the exporter just wrote and count the textured meshes that survived
            // the material conversion — proves the shader skins became serializable StandardMaterials.
            if (wrote)
            {
                var doc = new GltfDocument();
                var state = new GltfState();
                var readErr = doc.AppendFromFile(path, state);
                ctx.Same((long)Error.Ok, (long)readErr, $"re-import read result");
                var scene = doc.GenerateScene(state) as Node3D;
                ctx.Check(scene != null, $"re-imported scene has a Node3D root");
                int textured = scene == null ? 0 : CountTexturedMeshes(scene);
                ctx.Check(textured >= 1, $"re-imported textured meshes count={textured}");
                scene?.Free();
            }
        }
        finally
        {
            plane?.Free();
            textures.Dispose();
        }
    }

    // How many MeshInstance3D in the subtree carry a material with an albedo
    // texture — the glTF importer hands each surface back a StandardMaterial3D.
    internal static int CountTexturedMeshes(Node node)
    {
        int count = 0;
        if (node is MeshInstance3D mesh)
        {
            for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                if (mesh.GetActiveMaterial(i) is BaseMaterial3D { AlbedoTexture: not null })
                {
                    count++;
                    break;
                }
            }
        }
        foreach (var child in node.GetChildren())
        {
            count += CountTexturedMeshes(child);
        }
        return count;
    }

    // ---- needs a chapter world ------------------------------------------------------------------

    internal static void DamageStages(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var r = Probes.Damage(world.Runtime, ctx.Chapter, "", damageHd: 0f);
            ctx.WriteArtifact($"test-damage-stages-{ctx.Chapter}.txt", r.Text);
            ctx.Check(r.Rows.Count > 0, $"chapter has DAMAGE_SEQUENCE defs chapter={ctx.Chapter} rows={r.Rows.Count}");
            foreach (var row in r.Rows)
            {
                ctx.Check(row.Resolved, $"deep descendant resolves back to its destructible def={row.Def}");
                ctx.Check(row.StagesFired > 0, $"HP sweep fires a stage effect def={row.Def} stages={row.StagesFired}");
            }
            ctx.Note($"{r.Summary}");
        });
    }

    // The invisible-wall tripwire: after a chapter's world has bootstrapped — mission
    // setup script, RESET_STATEs, ON_STARTUP, the unplaced sweep — no collider may still be
    // enabled where nothing is drawn. Every chapter, because what each mission hides differs and
    // the failure is silent until someone flies into it (C1/IA1's `hk_zep`).
    internal static void CollisionVisibility(TestContext ctx)
    {
        foreach (var (chapter, _, _) in Census)
        {
            ctx.WithWorld(chapter, collision: true, world =>
            {
                var solid = Probes.InvisibleEnabledColliders(world.Session.Root);
                ctx.Same(0, solid.Count, $"{chapter} invisible-but-solid colliders");
                for (int i = 0; i < solid.Count && i < 8; i++)
                {
                    ctx.Note($"{chapter} solid where nothing is drawn: {solid[i]}");
                }
            });
        }
    }

    internal static void DamageHd(TestContext ctx)
    {
        // Collision forced on: the collider census measures which destructible geometry is solid
        // and whether the death removes it, and a world built without collision censuses zero.
        ctx.WithWorld(ctx.Chapter, collision: true, world =>
        {
            var r = Probes.Damage(world.Runtime, ctx.Chapter, "", damageHd: 25f);
            ctx.WriteArtifact($"test-damage-hd-{ctx.Chapter}.txt", r.Text);
            ctx.Check(r.Rows.Count > 0, $"chapter has destructibles chapter={ctx.Chapter} rows={r.Rows.Count}");
            int destroyed = 0;
            foreach (var row in r.Rows)
            {
                ctx.Check(row.Resolved, $"deep descendant resolves back to its destructible def={row.Def}");
                ctx.Check(row.Destroyed, $"enough weapon hits destroy it def={row.Def} hits={row.Hits}");
                if (!row.Destroyed)
                {
                    continue;
                }
                destroyed++;
                ctx.Check(row.ResetHealthy == true, $"reset restores it def={row.Def}");
                ctx.Check(row.RekillMatched == true, $"rekill takes the same hits def={row.Def} hits={row.Hits}");
            }
            ctx.Same(r.Rows.Count, destroyed, $"destructibles destroyed by weapon hits");
            ctx.Note($"{r.Summary}");
            ctx.Note($"colliders world={r.CollidableMeshes} swept={r.Rows.Count} capped={r.Capped}");
        });
    }

    // ---- needs Godot's Image, nothing else -------------------------------------------------------

    internal static void TexDropIn(TestContext ctx)
    {
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        using var textures = new TextureArchive(texturesPath);
        var colors = new Dictionary<string, Color>();
        foreach (string name in DropInSamples)
        {
            var img = textures.FindImage(name);
            ctx.Check(img != null, $"sample texture resolves texture={name}");
            if (img == null)
            {
                continue;
            }
            int w = img.GetWidth(), h = img.GetHeight();
            var format = img.GetFormat();
            bool hadAlpha = format is Image.Format.Rgba8 or Image.Format.La8 or Image.Format.Rgba4444;
            byte[] before = img.GetData();

            var color = TextureDropIn.ColorForName(name);
            colors[name] = color;
            TextureDropIn.Flatten(img, color);
            byte[] after = img.GetData();

            ctx.Check(img.GetWidth() == w && img.GetHeight() == h,
                $"flatten keeps the size texture={name} before={w}x{h} after={img.GetWidth()}x{img.GetHeight()}");
            ctx.Check(!img.HasMipmaps(), $"flatten adds no mipmaps texture={name}");
            var flatFormat = img.GetFormat();
            ctx.Check(flatFormat == (hadAlpha ? Image.Format.Rgba8 : Image.Format.Rgb8),
                $"flatten lands in the three-channel form of the original texture={name} was={format} now={flatFormat}");

            int stride = flatFormat == Image.Format.Rgba8 ? 4 : 3;
            ctx.Same(w * h * stride, after.Length, $"{name} flattened byte count");
            int wrongRgb = 0, wrongAlpha = 0;
            for (int i = 0; i + stride <= after.Length; i += stride)
            {
                if (after[i] != color.R8 || after[i + 1] != color.G8 || after[i + 2] != color.B8)
                {
                    wrongRgb++;
                }
                // Only comparable when the source was already the same layout; a converted source
                // has no byte-for-byte predecessor to check against.
                if (stride == 4 && format == Image.Format.Rgba8 && after[i + 3] != before[i + 3])
                {
                    wrongAlpha++;
                }
            }
            ctx.Same(0, wrongRgb, $"{name} texels not repainted to the flat colour");
            ctx.Same(0, wrongAlpha, $"{name} texels whose alpha the flatten moved");
        }
        // The identity every count report depends on: no two of these names share a colour, and
        // each keeps one channel pinned to full brightness.
        var seen = new Dictionary<string, string>();
        foreach (var (name, color) in colors)
        {
            string key = $"{color.R8},{color.G8},{color.B8}";
            ctx.Check(!seen.ContainsKey(key), $"census colour is unique texture={name} colour={key} clashes_with={(seen.TryGetValue(key, out var other) ? other : "-")}");
            seen[key] = name;
            ctx.Check(color.R8 == 255 || color.G8 == 255 || color.B8 == 255,
                $"census colour is full brightness texture={name} colour={key}");
        }
        ctx.Note($"{colors.Count} sample textures flattened, {seen.Count} distinct colours");
    }

    // templates.zrd's substitute and scale_range, asserted as an A/B against the build that does not
    // read the spec at all: the same ClutterBuilder over the same gamez, differing only in whether a
    // spec was handed to it. Three builds, each with its own able-to-fail control: bare, dressed, and
    // dressed again in the SAME process with no Rng reset, which is the strong form because it proves
    // the stream is a function of the data alone. See docs/org/clutter.md.
    // ⚠ Never seed it from Rng.Master; that rerolls C1's forest on every unpinned launch.
    internal static void ClutterDeterminism(TestContext ctx)
    {
        const string chapter = "C1";
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, chapter);
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, chapter);
        ctx.RequireData(texturesPath, $"{chapter} textures");
        ctx.RequireData(gamezPath, $"{chapter} gamez");
        ctx.RequireData(ctx.InterpPath, $"interp.json");

        var gamez = GameZ.Load(gamezPath);
        using var textures = new TextureArchive(texturesPath);
        var names = ClutterBuilder.TemplateNames(ctx.InterpPath, chapter);
        var props = ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, chapter));
        ctx.Check(props != null && props.Kinds.Count > 0, $"{chapter} templates.zrd read");

        // Each build's kinds as (label → placements), plus the flat transform list in kind order.
        static (Dictionary<string, int> ByKind, List<Transform3D> Placements, int Total) Take(
            ClutterBuilder builder, IReadOnlyList<string> names)
        {
            var root = builder.Build(names);
            var byKind = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var placements = new List<Transform3D>();
            foreach (var kind in builder.ExportedKinds ?? System.Array.Empty<ClutterBuilder.KindExport>())
            {
                byKind.TryGetValue(kind.Texture, out int had);
                byKind[kind.Texture] = had + kind.Placements.Count;
                placements.AddRange(kind.Placements);
            }
            int total = builder.InstanceCount + builder.SolidCount;
            root?.Free();
            return (byKind, placements, total);
        }

        var bare = Take(new ClutterBuilder(gamez, textures), names);
        var dressed = Take(new ClutterBuilder(gamez, textures, null, props), names);
        var again = Take(new ClutterBuilder(gamez, textures, null, props), names);

        ctx.Check(bare.Total > 0, $"the bare build placed something total={bare.Total}");
        ctx.Same(bare.Total, dressed.Total, $"{chapter} instance total is unchanged by substitution");

        // The mix moved, and in the authored direction: firtree1 sheds a tenth of its stamps to
        // firtree2. Asserted as a direction plus a band rather than as an exact count, so the
        // suite survives a re-seed but still fails if the roll stops happening or inverts.
        bare.ByKind.TryGetValue("firtree1.tif", out int bareFir1);
        dressed.ByKind.TryGetValue("firtree1.tif", out int dressedFir1);
        bare.ByKind.TryGetValue("firtree2.tif", out int bareFir2);
        dressed.ByKind.TryGetValue("firtree2.tif", out int dressedFir2);
        int moved = bareFir1 - dressedFir1;
        ctx.Check(moved > 0 && dressedFir2 - bareFir2 == moved,
            $"firtree1 sheds stamps and firtree2 gains exactly those lost={moved} gained={dressedFir2 - bareFir2}");
        ctx.Check(moved > bareFir1 * 0.08f && moved < bareFir1 * 0.12f,
            $"the shed fraction is the authored 9:1 moved={moved} of {bareFir1}");

        // Scale: uniform, inside C1's widest authored band, and actually varying. A build that
        // silently dropped the draw would pass every count assertion above.
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var xf in dressed.Placements)
        {
            var s = xf.Basis.Scale;
            ctx.Check(Mathf.Abs(s.X - s.Y) < 1e-5f && Mathf.Abs(s.X - s.Z) < 1e-5f,
                $"the scale is uniform scale={s}");
            lo = Mathf.Min(lo, s.X);
            hi = Mathf.Max(hi, s.X);
        }
        foreach (var xf in bare.Placements)
        {
            ctx.Check(Mathf.Abs(xf.Basis.Scale.X - 1f) < 1e-5f,
                $"the bare build leaves every instance at its authored size scale={xf.Basis.Scale.X}");
        }
        ctx.Check(lo >= 0.9f - 1e-4f && hi <= 1.5f + 1e-4f, $"scales stay inside C1's authored 0.9-1.5 lo={lo} hi={hi}");
        ctx.Check(hi - lo > 0.4f, $"scales actually vary lo={lo} hi={hi}");

        // The strong form: same process, no reseed, transform for transform.
        ctx.Same(dressed.Placements.Count, again.Placements.Count, $"the second dressed build placed the same count");
        int drift = 0;
        for (int i = 0; i < Mathf.Min(dressed.Placements.Count, again.Placements.Count); i++)
        {
            if (dressed.Placements[i] != again.Placements[i])
            {
                drift++;
            }
        }
        ctx.Same(0, drift, $"two dressed builds are identical transform for transform");
        ctx.Note($"{chapter} bare firtree1={bareFir1} firtree2={bareFir2}; dressed firtree1={dressedFir1} firtree2={dressedFir2}; scales {lo:0.000}-{hi:0.000}");
    }

    internal static void DestructibleCensus(TestContext ctx)
    {
        foreach (var (chapter, instances, anchors) in Census)
        {
            ctx.WithWorld(chapter, collision: false, world =>
            {
                // The registry totals, never the swept rows — the sweep is capped at
                // Probes.SweepCap and would silently under-count.
                var registry = world.Runtime.Destructibles;
                ctx.Same(instances, registry.Count, $"{chapter} destructible instances");
                ctx.Same(anchors, registry.DistinctAnchors, $"{chapter} destructible node groups");
            });
        }
    }

    // The DirectionalLight3D is pointed by the flown zone's authored SUNLIGHT_ORIENTATION and keeps
    // following it when the camera's weather state moves to another zone. The CSVM.Tests units pin the
    // parse and the euler-to-direction mapping; neither can see the light wired to the wrong seam, or
    // a zone edge firing without carrying it, and a screenshot cannot either.
    // ⚠ Do not simplify this onto C1/IA1. C2/MP2 is the only shape in the install whose two zones
    // author different bearings, so anywhere else a zone change would pass with the write deleted.
    internal static void SunOrientation(TestContext ctx)
    {
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C2", "MP2");
        ctx.RequireData(zrdr, $"C2/MP2 mission zrdr");

        var sun = new DirectionalLight3D { Name = "sun-orientation-probe" };
        ctx.Host.AddChild(sun);
        var camera = new Camera3D { Name = "sun-orientation-camera" };
        ctx.Host.AddChild(camera);
        try
        {
            // No --sky-zone: an explicit one disarms the state machine outright, which
            // would make the zone change below unobservable. The default request is zone2, and with
            // no horizon geometry to correct it that is what the flight builds with.
            var spec = SessionSpec.Parse(new[] { "--chapter=C2", "--mission=MP2" });
            var rig = new PlayerRig { Index = 0, Camera = camera, HudParent = ctx.Host };
            var rigs = new List<PlayerRig> { rig };

            var weatherRig = new WeatherRig(spec, ctx.Host, sun);
            weatherRig.Build(zrdr, rigs, System.Array.Empty<HorizonZone>(), _ => { });
            ctx.Check(NearDegrees(sun.RotationDegrees, -25f, 90f),
                $"C2/MP2 builds at ZONE2's bearing (got {sun.RotationDegrees.X:0.#}°/{sun.RotationDegrees.Y:0.#}°)");

            // Below the cloud band (19024–20124 m) the camera is in weather state 1, so the edge
            // trigger swaps to ZONE1 — and the light must ride along. Ticked twice: the first Tick
            // publishes the rig's new state, and the swap is asserted after it has settled.
            camera.Position = new Vector3(0f, 0f, 0f);
            weatherRig.Tick(rigs);
            ctx.Same(1, rig.CameraWeatherState, $"camera below the band is in weather state 1");
            ctx.Check(NearDegrees(sun.RotationDegrees, -65f, 90f),
                $"a zone change carries the sun to ZONE1's bearing (got {sun.RotationDegrees.X:0.#}°/{sun.RotationDegrees.Y:0.#}°)");

            // ...and back. A one-way test would pass on a light that moved once and stuck.
            camera.Position = new Vector3(0f, 25000f, 0f);
            weatherRig.Tick(rigs);
            ctx.Same(2, rig.CameraWeatherState, $"camera above the band is in weather state 2");
            ctx.Check(NearDegrees(sun.RotationDegrees, -25f, 90f),
                $"and back to ZONE2's on the return crossing (got {sun.RotationDegrees.X:0.#}°/{sun.RotationDegrees.Y:0.#}°)");

            // The other half, asserted where it would regress: shadow mapping stays off, so this
            // light cannot cast the raked plane-on-plane shadows the original never draws (the
            // real ground shadow is drawn elsewhere).
            ctx.Check(!sun.ShadowEnabled, $"the world light casts no shadow map");
        }
        finally
        {
            sun.QueueFree();
            camera.QueueFree();
        }
    }

    // Degrees, not radians, and a loose epsilon: the assertion is "this is the authored bearing",
    // not "this is bit-identical to a round trip through Basis".
    internal static bool NearDegrees(Vector3 rotationDegrees, float pitch, float yaw)
        => Mathf.Abs(rotationDegrees.X - pitch) < 0.1f && Mathf.Abs(rotationDegrees.Y - yaw) < 0.1f;

    // The lens flare's gating, chapter by chapter. ⚠ Gate it on chapter data, never on a chapter
    // name: it reads a gamez sun node in the horizon subtree and init.gw's LensFlareTexture slot
    // registrations, both true of C2 and C3 and of nothing else. Both directions are asserted, because
    // nothing about C1 looking correct would tell you a flare rig had started building there.
    // Data-only on purpose, so it stays a fast suite rather than a third eight-chapter world sweep.
    internal static void LensFlareGates(TestContext ctx)
    {
        ctx.RequireData(ctx.InterpPath, $"interp.json");
        int withFlare = 0;
        foreach (var (chapter, _, _) in Census)
        {
            bool expected = chapter is "C2" or "C3";

            var slots = LensFlareRig.FlareTextureNames(ctx.InterpPath, chapter);
            ctx.Same(expected ? 4 : 0, slots.Count, $"{chapter} LensFlareTexture slots");

            string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, chapter);
            ctx.RequireData(gamezPath, $"{chapter} gamez");
            var gamez = GameZ.Load(gamezPath);
            int sunNodes = 0;
            foreach (var n in gamez.Nodes)
            {
                if (string.Equals(n.Name, "sun", System.StringComparison.OrdinalIgnoreCase))
                    sunNodes++;
            }

            ctx.Same(expected ? 1 : 0, sunNodes, $"{chapter} gamez sun node");
            // The gates must not merely each be right — they must AGREE. A chapter with textures
            // and no sun (or the reverse) is data telling us something we have not decoded, and
            // the rig logs a warning for exactly that case.
            ctx.Check(slots.Count > 0 == sunNodes > 0, $"{chapter} both flare gates agree");
            if (expected)
            {
                withFlare++;
                ctx.Note($"{chapter} flare slots: {string.Join(",", slots)}");
            }
        }

        ctx.Same(2, withFlare, $"chapters authoring a lens flare");
        // ⚠ C2's flare is PREDICTED, not verified: the data says it has one and there is no
        // footage of it. Only C3 was ever captured.
        ctx.Note($"C2's flare is predicted from data only — no capture of the original exists");
    }

    // The authored STOP_SEQUENCE stops must actually run; nothing else in the gate measures an effect's
    // DURATION (--effects-test only proves a puffer builds, docs/verification.md). It asserts on the
    // dispatch timeline via AnimRuntime.OnEventDispatched, so it needs no textures: the rocket
    // fireball's ON_CALL stopper is named by a stop nothing runs and must stay silent, and the 30 s
    // fire's emitting poll loop is halted, since an un-halted Loop{-1} re-fires every frame forever.
    internal static void StopSequenceStops(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var program = world.Session.Program.Subset(new[] { "large_fireball", "large_30sec_fire" });
            var fireball = program.ByAnimName("large_fireball");
            var fire30 = program.ByAnimName("large_30sec_fire");
            ctx.Check(fireball.Count > 0, $"chapter program has large_fireball defs={fireball.Count}");
            ctx.Check(fire30.Count > 0, $"chapter program has large_30sec_fire defs={fire30.Count}");
            if (fireball.Count == 0 || fire30.Count == 0)
            {
                return;
            }

            var stage = new Node3D { Name = "StopSequenceStage" };
            var runtime = new AnimRuntime { AutoStart = false, ManualAdvance = true, SoundHandledElsewhere = true };
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, program);
                var timeline = new List<(float T, string Seq, string Kind)>();
                float clock = 0f;
                runtime.OnEventDispatched = d => timeline.Add((clock, d.Sequence, d.EventKind));

                // The fireball: activate_puffer names its ON_CALL stopper at EVENT_OFFSET 0.3 while
                // nothing runs under that name — the stop halts nothing and must START nothing,
                // so the stopper's teardown never dispatches at all.
                runtime.Start(fireball[0], stage);
                for (int i = 0; i < 60; i++)
                {
                    clock += 1f / 60f;
                    runtime.Advance(1f / 60f);
                }
                int stopperDispatches = 0;
                int trailPuffs = 0;
                foreach (var e in timeline)
                {
                    if (e.Seq == "stop_p1trail")
                    {
                        stopperDispatches++;
                    }
                    else if (e.Kind == "PufferState")
                    {
                        trailPuffs++;
                    }
                }
                ctx.Check(trailPuffs > 0, $"the fireball's own puffer events dispatch puffs={trailPuffs}");
                ctx.Same(0, stopperDispatches, $"stop_p1trail dispatches nothing within 1 s");

                // The 30 s fire: fire_n_smoke is a running Loop{-1} poll re-asserting its emitter
                // every frame — the ANIMATION_OFFSET 30 stop must HALT it (the halt idiom), or the
                // re-assert would revive the puffer one frame after the paired INACTIVE.
                float t0 = clock;
                runtime.Start(fire30[0], stage);
                for (int i = 0; i < 320; i++)
                {
                    clock += 0.1f;
                    runtime.Advance(0.1f);
                }
                int pollsBefore = 0;
                float lastPoll = -1f;
                int stopPuffs = 0;
                float stop30At = -1f;
                foreach (var e in timeline)
                {
                    float rel = e.T - t0;
                    if (e.Seq == "fire_n_smoke")
                    {
                        pollsBefore += rel <= 30f ? 1 : 0;
                        lastPoll = rel > lastPoll ? rel : lastPoll;
                    }
                    else if (e.Seq == "stop_fire_n_smoke" && e.Kind == "PufferState")
                    {
                        stopPuffs++;
                        stop30At = rel;
                    }
                }
                ctx.Check(pollsBefore > 100, $"the fire's poll loop runs until its stop polls={pollsBefore}");
                ctx.Check(lastPoll <= 30.2f, $"no fire_n_smoke dispatch after the authored 30 s halt last={lastPoll:0.0}");
                ctx.Same(1, stopPuffs, $"stop_fire_n_smoke PUFFER_STATE dispatches");
                ctx.Check(stop30At >= 29.5f && stop30At <= 30.5f,
                    $"the fire's own puffer-off lands at the authored 30 s t={stop30At:0.0}");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // ---- the compiled destruction slot dispatches at death --------------------------------------

    // The live-path start check the direct-Start stop-sequence suite cannot make: a real kill must
    // dispatch the def's compiled destruction slot (AnimDefinition.DeathSlot), the block carrying
    // nearly all of large_30sec_fire's death calls. An unparsed block no-ops every one of them and
    // every "the fire ends on time" check reads the absence as a pass (DIAG-20). Subject: a C1 AA gun.
    // Able to fail: with RunDeathSlot deleted, no destruction_slot lane ever dispatches.
    internal static void DeathSlotDispatches(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var runtime = world.Runtime;
            DestructibleRegistry.Instance? gun = null;
            foreach (var inst in runtime.Destructibles.All)
            {
                if (inst.Def.DeathSlot is { } s && s.Events.Any(e => e.Kind == "CallAnimation"))
                {
                    gun = inst;
                    break;
                }
            }
            ctx.Check(gun != null, $"chapter ships a destructible with a calling destruction slot chapter={ctx.Chapter}");
            if (gun == null)
            {
                return;
            }
            if (gun.Status == DestructibleRegistry.State.Destroyed)
            {
                runtime.ResetDestructible(gun);
            }

            var gunDef = gun.Def;
            var slotDispatches = new List<(string Kind, string? Name)>();
            var previous = runtime.OnEventDispatched;
            try
            {
                runtime.OnEventDispatched = d =>
                {
                    if (d.Def == gunDef && d.Sequence == "destruction_slot")
                    {
                        slotDispatches.Add((d.EventKind, d.EventName));
                    }
                };
                runtime.DamageAt(gun.Anchor, gun.MaxHealth + 1f);
                for (int i = 0; i < 30; i++)
                {
                    runtime.Advance(1f / 60f);
                }
            }
            finally
            {
                runtime.OnEventDispatched = previous;
            }

            ctx.Check(slotDispatches.Count > 0,
                $"the destruction slot dispatched on death def={gunDef.AnimName} events={slotDispatches.Count}");
            ctx.Check(slotDispatches.Any(e => e.Kind == "CallAnimation"),
                $"the slot's CALL_ANIMATION dispatched targets=[{string.Join(",", slotDispatches.Where(e => e.Kind == "CallAnimation").Select(e => e.Name))}]");
        });
    }

    // ---- WAIT_FOR_COMPLETION --------------------------------------------------------------------

    // WAIT_FOR_COMPLETION on the authored case, with its own control beside it in the same sequence.
    // player_crash_water's destroy_crash is the install's clean discriminator: eleven events, of which
    // exactly one carries the flag, followed immediately by an unflagged large_steam_spray that would
    // otherwise start with the splash instead of after it.
    // ⚠ Assert the control too, the nine unflagged calls that must still all start at t=0. A runtime
    // that held every call would pass the spray check and fail those.
    internal static void WaitForCompletion(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            const string animName = "player_crash_water";
            const string flagged = "plane_big_splash";
            const string held = "large_steam_spray";
            var program = world.Session.Program.Subset(animName);
            var defs = program.ByAnimName(animName);
            ctx.Check(defs.Count > 0, $"chapter program has {animName} defs={defs.Count}");
            if (defs.Count == 0)
            {
                return;
            }

            // The caller's own nodes plus each callee's ROOT: on a flat stage a callee whose root
            // is missing falls back to the caller's anchor, which still runs but stops being the
            // separate instance whose lifetime is the subject here.
            var nodes = new List<string>
            {
                "player", "healthy", "destroyed", "dontmove", "markers", "shadow", "cockpit1",
                "huge_splash_model", "splash_polys", "sp_1", "white_water_impact",
                "carnage_trails", "large_fire", "ripple1",
            };
            for (int i = 1; i <= 4; i++)
            {
                nodes.Add($"piece{i}");
            }

            WithEmitterStage(ctx, program, "CrashWaterStage", nodes, (stage, runtime, fake) =>
            {
                float clock = 0f;
                var startedAt = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
                runtime.OnInstanceStarted = (d, _) =>
                {
                    if (d.AnimName is { } name && !startedAt.ContainsKey(name))
                    {
                        startedAt[name] = clock;
                    }
                };
                runtime.Start(defs[0], stage);
                for (int i = 0; i < 480; i++)   // 8 s — well past the splash's authored 3.0 s
                {
                    clock += 1f / 60f;
                    runtime.Advance(1f / 60f);
                }
                runtime.OnInstanceStarted = null;

                ctx.Check(startedAt.ContainsKey(flagged), $"{flagged} became a live instance");
                ctx.Check(startedAt.ContainsKey(held), $"{held} became a live instance");
                if (!startedAt.TryGetValue(flagged, out float splashAt)
                    || !startedAt.TryGetValue(held, out float sprayAt))
                {
                    return;
                }

                ctx.Note($"destroy_crash: {flagged} t={splashAt:0.000}s, {held} t={sprayAt:0.000}s (gap {sprayAt - splashAt:0.000}s vs the authored 3.0s), holds armed={runtime.WaitsInstalled} abandoned={runtime.WaitsAbandoned}");
                ctx.Check(splashAt <= 2f / 60f,
                    $"the flagged call itself is NOT delayed — the hold is on what follows it (t={splashAt:0.000})");
                ctx.Check(sprayAt - splashAt >= 2.9f,
                    $"{held} waits out {flagged}'s authored 3.0 s choreography (gap={sprayAt - splashAt:0.000} s)");
                ctx.Check(sprayAt - splashAt <= 4.5f,
                    $"...and starts when the splash ENDS, not at some ceiling (gap={sprayAt - splashAt:0.000} s)");

                // The control: the unflagged calls ahead of it in the same sequence.
                foreach (var unflagged in new[] { "call_crash_trails", "large_10sec_fire" })
                {
                    if (startedAt.TryGetValue(unflagged, out float t))
                    {
                        ctx.Check(t <= 2f / 60f,
                            $"unflagged {unflagged} is not held (t={t:0.000}) — null and 0 are different authored states");
                    }
                }

                ctx.Check(runtime.WaitsInstalled >= 1,
                    $"the runtime armed the hold rather than the gap coming from somewhere else (installed={runtime.WaitsInstalled})");
                ctx.Same(0, runtime.WaitsAbandoned,
                    $"no hold ended at the WaitCeilingS backstop instead of at its callee");
            },
                asCrashRig: true);
        });
    }

    // ---- what a host deactivation may and may not stop ------------------------------------------

    // An OBJECT_ACTIVE_STATE INACTIVE ends the emitters under that host, but not one that started in
    // the same instant. ⚠ Assert BOTH halves: dropping the stop entirely passes the splash half, and
    // shipping the stop unconditioned passes the debris half. They are the two populations the
    // install-wide census splits, and the split is total (analysis/bl-229-emitter-host-deactivation/).
    // SPLASH is plane_big_splash, whose emitter must survive its host's deactivation and still be gone
    // by ~0.7 s; DEBRIS is m_build01, whose deactivation is the trail's only authored stop.
    internal static void EmitterHostDeactivation(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            SplashSurvivesItsOwnInstant(ctx, world);
            DebrisTrailStillEndsWithItsHost(ctx, world);
        });
    }

    internal static void SplashSurvivesItsOwnInstant(TestContext ctx, TestWorld world)
    {
        const string animName = "plane_big_splash";
        const string pufferName = "splasher";
        var program = world.Session.Program.Subset(animName);
        var defs = program.ByAnimName(animName);
        ctx.Check(defs.Count > 0, $"chapter program has {animName} defs={defs.Count}");
        if (defs.Count == 0)
        {
            return;
        }

        WithEmitterStage(ctx, program, "SplashStage",
            new[] { "huge_splash_model", "splash_polys", "sp_1", "ripple1", "ripple2", "ripple3" },
            (stage, runtime, fake) =>
        {
            runtime.Start(defs[0], stage);
            runtime.Advance(1f / 60f);

            ctx.Check(fake.Built.Any(e => e.Key == pufferName),
                $"{animName} reached the fake factory and built {pufferName}");
            ctx.Check(EmitterOn(runtime, pufferName, "sp_1") == true,
                $"{pufferName} survives the sp_1 deactivation it shares an instant with (BL-229)");

            for (int i = 0; i < 18; i++)   // 0.3 s — inside the callee's authored 0.5 s run
            {
                runtime.Advance(1f / 60f);
            }
            ctx.Check(EmitterOn(runtime, pufferName, "sp_1") == true,
                $"{pufferName} is still emitting 0.3 s in");

            for (int i = 0; i < 30; i++)   // out to 0.8 s, past the authored 0.5 + 0.1 s stop
            {
                runtime.Advance(1f / 60f);
            }
            ctx.Check(EmitterOn(runtime, pufferName, "sp_1") == false,
                $"{pufferName} ends on the run hg_splasher authors, not on its host (and its row is still known, so this is a pause, not a teardown)");
            var emitter = fake.Built.FirstOrDefault(e => e.Key == pufferName);
            ctx.Check(emitter is { Started: > 0 }, $"{pufferName} actually sustained particles");
        },
            asCrashRig: true);
    }

    internal static void DebrisTrailStillEndsWithItsHost(TestContext ctx, TestWorld world)
    {
        const string animName = "m_build01";
        const string pufferName = "trailpuffer3";
        const string host = "part3";
        const string offSequence = "sparkout3";   // where part3's own deactivation is authored
        var program = world.Session.Program.Subset(animName);
        var defs = program.ByAnimName(animName);
        ctx.Check(defs.Count > 0, $"chapter program has {animName} defs={defs.Count}");
        if (defs.Count == 0)
        {
            return;
        }

        // ⚠ Keep the fireball template roots on the stage. small_fireball declares a puffer also called
        // trailpuffer2, and with its own root missing the name resolution falls back to the call anchor,
        // so its stop lands on the building's key and ends the debris trail early, masking the assertion.
        var nodes = new List<string>
        {
            "m_bld_healthy", "m_bld_destroyed", "dbase", "flame_ball_01", "flame_ball_02",
        };
        for (int i = 1; i <= 9; i++)
        {
            nodes.Add($"part{i}");
        }
        WithEmitterStage(ctx, program, "DebrisStage", nodes, (stage, runtime, fake) =>
        {
            // Asserted against the DISPATCH MOMENT, never a fixed second: part3 is a bounce-solved launch, so
            // when it lands is computed rather than authored. The only other thing that could stop this trail,
            // the instance retiring, happens a second later, which a wall-clock check would blur.
            float clock = 0f;
            float deactivatedAt = -1f;
            float stoppedAt = -1f;
            bool everEmitted = false;
            runtime.OnEventDispatched = d =>
            {
                if (deactivatedAt < 0f && d.Sequence == offSequence && d.EventKind == "ObjectActiveState")
                {
                    deactivatedAt = clock;
                }
            };
            runtime.Start(defs[0], stage);
            for (int i = 0; i < 300; i++)   // 5 s — past the landing and past the instance's own end
            {
                clock += 1f / 60f;
                runtime.Advance(1f / 60f);
                bool? on = EmitterOn(runtime, pufferName, host);
                everEmitted |= on == true;
                if (everEmitted && stoppedAt < 0f && on == false)
                {
                    stoppedAt = clock;
                }
            }
            runtime.OnEventDispatched = null;

            ctx.Check(fake.Built.Any(e => e.Key == pufferName), $"{animName}'s death built {pufferName}");
            ctx.Check(everEmitted, $"{pufferName} trails {host} while it flies");
            ctx.Check(deactivatedAt > 0f, $"{offSequence} switched {host} off t={deactivatedAt:0.000}");
            ctx.Check(stoppedAt > 0f, $"{pufferName} stopped within the 5 s window t={stoppedAt:0.000}");
            ctx.Check(stoppedAt > 0f && deactivatedAt > 0f && Mathf.Abs(stoppedAt - deactivatedAt) <= 2f / 60f,
                $"{pufferName} ends on {host}'s own deactivation frame, not later — BL-224's stop is dated, not dropped (off={deactivatedAt:0.000} stop={stoppedAt:0.000})");
        });
    }

    // Is the emitter `name` ON `host` emitting? Null when
    // no such emitter is known. Host-qualified on purpose: puffer names are NOT unique across
    // definitions — `small_fireball` declares a `trailpuffer2` of its own, and a name-only read
    // answers about whichever row comes first, which lets a debris assertion pass against a
    // runtime with the stop deleted outright.
    internal static bool? EmitterOn(AnimRuntime runtime, string name, string host)
    {
        foreach (var row in runtime.Emitters.Census)
        {
            if (row.Name == name && row.Host == host)
            {
                return row.Emitting;
            }
        }
        return null;
    }

    // A bare stage carrying the nodes a definition names, plus a runtime bound to it
    // through a CountingEmitterFactory. Flat children, never a hierarchy: the point is
    // to give each named host its own subtree, so a stop that reaches the wrong one is visible
    // rather than being absorbed by a shared ancestor.
    internal static void WithEmitterStage(TestContext ctx, AnimProgram program, string stageName,
        IEnumerable<string> nodeNames,
        System.Action<Node3D, AnimRuntime, CountingEmitterFactory> body,
        bool asCrashRig = false)
    {
        var stage = new Node3D { Name = stageName };
        foreach (var name in nodeNames)
        {
            stage.AddChild(new Node3D { Name = name });
        }
        var fake = new CountingEmitterFactory();
        // The two role flags AnimRuntime.ForCrashRig sets, for a def the crash rig is the only
        // production caller of: the splash is played by the per-player rig, which relocates its own
        // called templates and holds no ExternalEffect, so the start and the stop meet on ONE director.
        var runtime = new AnimRuntime(AnimRuntime.NewTemplateStage(placesCalled: asCrashRig))
        {
            AutoStart = false,
            ManualAdvance = true,
            SoundHandledElsewhere = true,
            EmitterFactory = fake,
            NameResolveFallback = asCrashRig,
        };
        ctx.Host.AddChild(stage);
        ctx.Host.AddChild(runtime);
        try
        {
            runtime.Bind(stage, program);
            body(stage, runtime, fake);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }

    // ---- the template MESH half renders at the call site ----------------------------------------

    // An effect's template MESHES must be visible at the call site while it plays and dark once it is
    // over. The world-effects stage keeps every template root hidden and the engine reveals the one a
    // call lands on (TemplateStage.Shown), so both halves are engine rules.
    // ⚠ Assert both: revealing and never hiding leaves a mesh burning at the last hit point for the
    // session, while hiding eagerly or never revealing shows nothing at all. The CALLED case is
    // he_ground_effect's staged he_ring1; the ENDED case is 3040ap_gunhit's stop-retired chunk mesh.
    internal static void EffectTemplateMesh(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            CalledTemplateShowsItsMesh(ctx, world);
            EndedEffectLeavesNoMeshLit(ctx, world);
        });
    }

    internal static void CalledTemplateShowsItsMesh(TestContext ctx, TestWorld world)
    {
        EffectStageSuiteHelper.WithEffectStage(ctx, world, "he_ground_effect", new[] { "he_ring", "he_ring1", "he_trails" },
            (stage, runtime, point) =>
        {
            ctx.Check(Probes.MeshCensus.VisibleMeshes(stage) == 0,
                $"the staged templates start hidden ({Probes.MeshCensus.VisibleMeshes(stage)} visible)");
            runtime.PlayEffectAt("he_ground_effect", point);
            int peak = 0;
            for (int i = 0; i < 30; i++)
            {
                runtime.Advance(1f / 60f);
                peak = Mathf.Max(peak, Probes.MeshCensus.VisibleMeshes(stage));
            }

            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring") > 0,
                $"he_ground_effect's own template mesh (he_ring) is visible — the PlayEffectAt half ({Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring")})");
            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring1") > 0,
                $"the CALLED template's mesh (he_ring1, the upper ring) is visible too — BL-061 ({Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring1")})");
            ctx.Check(peak >= 2, $"both rings drew in the same window (peak {peak} mesh(es))");
        });
    }

    internal static void EndedEffectLeavesNoMeshLit(TestContext ctx, TestWorld world)
    {
        EffectStageSuiteHelper.WithEffectStage(ctx, world, "3040ap_gunhit", new[] { "dum_gunhit" }, (stage, runtime, point) =>
        {
            runtime.PlayEffectAt("3040ap_gunhit", point, null, 0.3f);
            runtime.Advance(1f / 60f);
            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit") > 0,
                $"the ap gun hit's chunk mesh is visible while it plays ({Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit")})");

            // Past the def's own authored ACTIVE_STATE 0 at +0.1 s, which ends the instance well
            // inside the 0.3 s TTL — the case that would otherwise leave the mesh lit for the session.
            for (int i = 0; i < 30; i++)
            {
                runtime.Advance(1f / 60f);
            }

            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit") == 0,
                $"and is dark once the effect has ended, without waiting for its TTL — BL-061 ({Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit")} still lit)");
        });
    }

    // ---- the full-screen wash reports its authored run times ------------------------------------

    // he_ground_effect's frame_buffer_effects1 is six FBFX_COLOR_FROM_TO steps washing the picture over
    // 1.2 s, reached through an If PlayerRange call. The handler must report each step's authored
    // run_time as its duration, because that is the only thing spacing them: report 0 and all six fire
    // in one instant. It then asserts the routing, that each step reports where the burst was and the
    // def's own gate, and that the gate answers to the NEAREST human rather than to one camera.
    // Full inventory: this module's docs/architecture.md entry.
    internal static void FbfxFlash(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            EffectStageSuiteHelper.WithEffectStage(ctx, world, "he_ground_effect", new[] { "he_ring", "he_ring1", "he_trails" },
                (stage, runtime, point) =>
            {
                // The authored chain, from extracted/C1/cam_anim/he_ring-he_ground_effect.json.
                var white = new Color(1f, 1f, 1f, 0.3f);
                var violet = new Color(0.2f, 0f, 1f, 0.2f);
                var wantFrom = new[] { white, violet, violet, white, violet, violet };
                var wantTo = new[] { violet, violet, white, violet, violet, white };
                var wantRun = new[] { 0.2f, 0.4f, 0.2f, 0.1f, 0.2f, 0.1f };

                const float dt = 1f / 60f;
                float clock = 0f;
                var fired = new List<(float T, Color From, Color To, float Run, Vector3 At, float GateSq)>();
                runtime.ScreenFlash = (from, to, seconds, at, gateSq) =>
                    fired.Add((clock, from, to, seconds, at, gateSq));

                // At the camera, so the def's own PLAYER_RANGE 10000 gate passes.
                runtime.PlayEffectAt("he_ground_effect", point);
                for (int i = 0; i < 150; i++)
                {
                    clock += dt;
                    runtime.Advance(dt);
                }

                string times = string.Join(", ", fired.Select(f => $"{f.T:0.####}s (run {f.Run:0.##})"));
                ctx.Note($"the wash fired at {times}");
                ctx.Check(fired.Count == 6, $"the six FBFX_COLOR_FROM_TO steps all fired ({fired.Count})");
                if (fired.Count != 6)
                    return;
                for (int i = 0; i < 6; i++)
                {
                    ctx.Check(Mathf.IsEqualApprox(fired[i].Run, wantRun[i]),
                        $"step {i + 1} reports its authored run time ({fired[i].Run:0.###} s, want {wantRun[i]:0.###})");
                    ctx.Check(fired[i].From.IsEqualApprox(wantFrom[i]) && fired[i].To.IsEqualApprox(wantTo[i]),
                        $"step {i + 1} ramps its authored colours ({fired[i].From} → {fired[i].To})");
                }
                // Each step must start one previous run time after the one before it — the
                // collapse this suite exists to catch, which no per-step assertion above can see.
                for (int i = 1; i < 6; i++)
                {
                    float gap = fired[i].T - fired[i - 1].T;
                    ctx.Check(Mathf.Abs(gap - wantRun[i - 1]) <= 2f * dt,
                        $"step {i + 1} waits step {i}'s run time ({gap:0.###} s, want {wantRun[i - 1]:0.###})");
                }
                // One step of headroom per gap: an authored run time is an exact multiple of the step here, but
                // neither it nor the accumulated clock is exact in binary float and the misses do not cancel.
                // Measured: three of the five gaps land one step late, 0.05 s over the chain.
                ctx.Check(Mathf.Abs((fired[5].T - fired[0].T) - 1.1f) <= 5f * dt,
                    $"the chain spans its authored 1.1 s first-to-last fire ({fired[5].T - fired[0].T:0.###} s)");

                // The routing half at the source: every step carries the burst point and the def's OWN gate, which
                // is what lets the overlay pick panes. 10000 is metres squared, the compiled PLAYER_RANGE
                // convention, and all 24 shipped wash defs author exactly that one gate.
                ctx.Check(fired.All(f => Mathf.IsEqualApprox(f.GateSq, 10000f)),
                    $"every step reports the def's authored PlayerRange gate ({fired[0].GateSq:0.#} m², want 10000 = 100 m)");
                float drift = fired.Max(f => f.At.DistanceTo(point));
                ctx.Note($"the wash routes from {fired[0].At} on the def's own {fired[0].GateSq:0.#} m² gate ({Mathf.Sqrt(fired[0].GateSq):0.#} m), {drift:0.###} m off the play point");
                ctx.Check(drift <= 1f,
                    $"every step reports the burst's own world point ({drift:0.###} m from where it was played)");
            });
        });
        WashPaintsOnlyThePanesItReached(ctx);
        BlendWashRoutesToTheVictimsPane(ctx);
        PlayerRangeNearestHuman(ctx);
    }

    // The victim-routed blend channel (D13): a wash addressed to player 2 paints pane 2 and leaves
    // pane 1 untouched, whatever the cameras are doing; it composites OVER a proximity ramp already
    // running in the pane and leaves that ramp's own picture unchanged where no wash is running.
    // METHOD-12: the ramp readouts are the invariant, the blended pane is what moves.
    internal static void BlendWashRoutesToTheVictimsPane(TestContext ctx)
    {
        const float gate = 10000f;
        var clear = new Color(0f, 0f, 0f, 0f);
        var white = new Color(1f, 1f, 1f, 0.3f);
        var violet = new Color(0.2f, 0f, 1f, 0.2f);
        var red = new Color(1f, 0f, 0f);

        // Both cameras at one point: the ramp's proximity gate cannot tell the panes apart, so any
        // difference between them below is the blend channel's routing alone.
        var p1 = SuiteViewers.Camera(ctx, Vector3.Zero);
        var p2 = SuiteViewers.Camera(ctx, Vector3.Zero);
        var viewers = new ViewerSet();
        viewers.Bind(new[] { p1, p2 });
        var (flash, panes) = PaneFlash(ctx, viewers);
        try
        {
            const float dt = 1f / 60f;
            // A wash addressed to player 2 (pane index 1), stepped through its 0.15 × 4 s attack.
            flash.PlayBlend(1, red, 1f, 4f);
            for (int i = 0; i < 40; i++)
                flash.Advance(dt);
            var pane2 = flash.CurrentFor(1);
            ctx.Check(pane2.A > 0.99f && pane2.R > 0.99f && pane2.G < 0.01f,
                $"a blend wash addressed to player 2 paints pane 2 red at its full weight after the attack ({pane2})");
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(clear),
                $"and pane 1, whose camera stands at the same point, stays clear — routed by victim, not by proximity ({flash.CurrentFor(0)})");
            ctx.Check(!flash.RunningFor(1),
                $"and starts no RAMP in pane 2: the two channels are separate states ({flash.RunningFor(1)})");

            // A proximity ramp reaching both panes: pane 1 shows the ramp alone, pane 2 the wash
            // over the ramp — the pixel the ramp would have painted, with red laid over it.
            flash.Play(white, violet, 0.2f, Vector3.Zero, gate);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(white),
                $"an HE ramp reaching both panes paints pane 1 exactly as before the blend channel existed ({flash.CurrentFor(0)})");
            var composite = flash.CurrentFor(1);
            var want = BlendWash.Composite(white, red, flash.BlendFor(1)!.Weight);
            ctx.Check(composite.IsEqualApprox(want),
                $"and pane 2 shows the wash composited over that ramp ({composite}, want {want})");
            ctx.Check(flash.RunningFor(0) && flash.RunningFor(1),
                $"while the ramp itself runs in both panes, its routing untouched by the wash ({flash.RunningFor(0)}/{flash.RunningFor(1)})");

            // The wash ends at its duration and pane 2 falls back to whatever the ramp channel has,
            // which by then is nothing.
            for (int i = 0; i < 260; i++)
                flash.Advance(dt);
            ctx.Check(flash.CurrentFor(1).IsEqualApprox(clear) && flash.BlendFor(1)!.Running == false,
                $"the wash is gone at its 4 s duration and pane 2 reads clear again ({flash.CurrentFor(1)})");

            // A victim with no pane (an AI's player index) addresses nothing and throws nothing.
            flash.PlayBlend(FlightRoster.ShooterIdBase, red, 1f, 4f);
            flash.Advance(dt);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(clear) && flash.CurrentFor(1).IsEqualApprox(clear),
                $"a wash addressed to an AI's player index paints no pane ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");
        }
        finally
        {
            flash.Free();
            foreach (var pane in panes)
                pane.Free();
            p2.Free();
            p1.Free();
        }
    }

    // The wash reaches the panes the burst reached and no others: two panes 120 m apart under the
    // authored 100 m gate, so one pane, the other pane, and both are each reachable by moving the burst.
    // ⚠ The wash paints every player inside the burst's own authored radius, not just a hit or nearest
    // one. That is the original's rule read literally, asked once per player here, and the two
    // ground-effect defs carrying it play on terrain impacts with no hit aircraft to route to at all.
    // Ramp state is per pane; within a pane it still replaces (docs/org/sequences.md).
    internal static void WashPaintsOnlyThePanesItReached(TestContext ctx)
    {
        // The gate every shipped wash def authors: metres SQUARED in the compiled convention.
        const float gate = 10000f;
        var clear = new Color(0f, 0f, 0f, 0f);
        var white = new Color(1f, 1f, 1f, 0.3f);
        var violet = new Color(0.2f, 0f, 1f, 0.2f);
        var green = new Color(0f, 1f, 0f, 0.5f);

        var p1 = SuiteViewers.Camera(ctx, Vector3.Zero);
        var p2 = SuiteViewers.Camera(ctx, new Vector3(0f, 0f, 120f));
        var viewers = new ViewerSet();
        viewers.Bind(new[] { p1, p2 });
        var (flash, panes) = PaneFlash(ctx, viewers);
        var (blind, blindPanes) = PaneFlash(ctx, null);
        try
        {
            ctx.Check(flash.PaneCount == 2, $"the overlay built one ramp per pane ({flash.PaneCount})");

            // 50 m ahead of P1, 170 m from P2: inside the gate for one of them only.
            flash.Play(white, violet, 0.2f, new Vector3(0f, 0f, -50f), gate);
            ctx.Check(flash.RunningFor(0) && flash.CurrentFor(0).IsEqualApprox(white),
                $"a burst 50 m from P1 washes P1's pane ({flash.CurrentFor(0)})");
            ctx.Check(!flash.RunningFor(1) && flash.CurrentFor(1).IsEqualApprox(clear),
                $"and leaves P2's pane, 170 m away, clear — the BL-340 report ({flash.CurrentFor(1)})");

            // 50 m past P2, 170 m from P1 — the same case from the other side, while P1's own ramp
            // is still running: two panes, two independent states.
            flash.Play(violet, white, 0.2f, new Vector3(0f, 0f, 170f), gate);
            ctx.Check(flash.RunningFor(1) && flash.CurrentFor(1).IsEqualApprox(violet),
                $"a second burst 50 m from P2 washes P2's pane ({flash.CurrentFor(1)})");
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(white),
                $"without touching the ramp P1 is already watching ({flash.CurrentFor(0)}) — the state is per pane");

            // Between them: 60 m from each, so BOTH are inside the burst's own radius.
            flash.Play(green, white, 0.2f, new Vector3(0f, 0f, 60f), gate);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(green) && flash.CurrentFor(1).IsEqualApprox(green),
                $"a burst 60 m from both washes both panes — every player inside the radius, not just the nearest ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");
            ctx.Check(flash.RunningFor(0) && flash.RunningFor(1),
                $"and replaces what each pane was running rather than compositing with it ({flash.RunningFor(0)}/{flash.RunningFor(1)})");

            // An ungated def (the intro cutscene's gi_scene1 authors no PlayerRange) is not a
            // proximity effect at all, so it still paints everything.
            flash.Play(white, violet, 0.2f, new Vector3(0f, 0f, -5000f), 0f);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(white) && flash.CurrentFor(1).IsEqualApprox(white),
                $"an UNGATED wash 5 km out still paints every pane ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");

            // The floor: the def's gate already fired, so something was near it. If no pane's own
            // camera agrees, the nearest pane still gets it rather than the burst washing nobody.
            flash.Play(violet, green, 0.2f, new Vector3(0f, 0f, -5000f), gate);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(violet) && flash.CurrentFor(1).IsEqualApprox(white),
                $"a gated wash no pane is in range of falls to the nearest pane alone ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");

            blind.Play(white, violet, 0.2f, new Vector3(0f, 0f, -50f), gate);
            ctx.Check(blind.CurrentFor(0).IsEqualApprox(white) && blind.CurrentFor(1).IsEqualApprox(white),
                $"ABLE-TO-FAIL CONTROL: the same burst with no viewer set bound paints both panes, which is what this did before the routing existed ({blind.CurrentFor(1)})");
        }
        finally
        {
            blind.Free();
            foreach (var pane in blindPanes)
                pane.Free();
            flash.Free();
            foreach (var pane in panes)
                pane.Free();
            p2.Free();
            p1.Free();
        }
    }

    // The wash's own gate — `If PlayerRange 10000` — answers to the NEAREST human, not
    // one camera: a burst still fires while the camera this stage was built
    // against sits 5 km off, as long as SOME entry in `PlayerPositions` is inside the 100 m
    // gate. This is upstream of B12's routing (which panes a fired wash reaches) — here nothing
    // has fired yet, so no pane would have anything to route.
    internal static void PlayerRangeNearestHuman(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            EffectStageSuiteHelper.WithEffectStage(ctx, world, "he_ground_effect", new[] { "he_ring", "he_ring1", "he_trails" },
                (stage, runtime, point) =>
            {
                const float dt = 1f / 60f;
                var fired = new List<float>();
                runtime.ScreenFlash = (from, to, seconds, at, gateSq) => fired.Add(seconds);

                // Every known human 5 km out: nowhere near the def's own 100 m gate. 150 steps
                // (2.5 s) is the same margin FbfxFlash drives the full chain for above — long
                // enough that a gate wrongly left open would have fired well within it.
                runtime.PlayerPositions = () => new[] { point + new Vector3(0f, 0f, -5000f) };
                runtime.PlayEffectAt("he_ground_effect", point);
                for (int i = 0; i < 150; i++)
                    runtime.Advance(dt);
                ctx.Check(fired.Count == 0,
                    $"the burst's own PLAYER_RANGE gate stays closed while every PlayerPositions entry is 5 km off ({fired.Count} fired)");

                // A second human standing at the burst: the NEAREST of the two is now in range,
                // and the def's gate is asked against that one, not the far singleton.
                runtime.PlayerPositions = () => new[] { point + new Vector3(0f, 0f, -5000f), point };
                runtime.PlayEffectAt("he_ground_effect", point);
                for (int i = 0; i < 150; i++)
                    runtime.Advance(dt);
                ctx.Check(fired.Count == 6,
                    $"the same def fires its six-step wash once the NEAREST PlayerPositions entry stands at the burst ({fired.Count})");
            });
        });
    }

    // A two-pane ScreenFlash over bare HUD parents — the shape
    // `GameSession` builds from the rigs, with nothing but the parents and the viewer set,
    // since that is all the routing reads.
    internal static (ScreenFlash Flash, Node[] Panes) PaneFlash(TestContext ctx, ViewerSet? viewers)
    {
        var panes = new[] { new Node { Name = "pane1_hud" }, new Node { Name = "pane2_hud" } };
        foreach (var pane in panes)
            ctx.Host.AddChild(pane);
        var flash = ScreenFlash.Build(panes, viewers);
        ctx.Host.AddChild(flash);
        return (flash, panes);
    }

    // ---- three ordnance bursts, played end to end against their authored timelines -------------

    // Plays he_ground_effect, flash_effect and sonic_ground_effect end to end on a fixed-dt clock and
    // matches each one's FULL event timeline against the authored JSON. Nothing here is a membership
    // check, which would pass a broken scheduler. Inventory: this module's docs/architecture.md entry
    // and docs/org/sequences.md.
    // ⚠ Derive the staged roots (EffectCatalogue.StageRootsFor), never hand-list them; a def whose
    // anchor root was not staged plays nothing, silently. ⚠ Do not inherit --effects-test's 0.3 s TTL.
    internal static void OrdnanceBurstTimeline(TestContext ctx)
    {
        var report = new System.Text.StringBuilder();
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            HeBurstTimeline(ctx, world, report);
            FlashBurstTimeline(ctx, world, report);
            SonicBurstTimeline(ctx, world, report);
        });
        ctx.WriteArtifact("ordnance-burst-timeline.txt", report.ToString());
    }

    // `he_ring-he_ground_effect.json` — five sequences, two of them unnamed and Initial,
    // three ON_CALL — plus `flame_ball_01-large_fireball.json`, which its fourth CALL_ANIMATION
    // reaches and which carries the parked-stopper case.
    internal static void HeBurstTimeline(TestContext ctx, TestWorld world, System.Text.StringBuilder report)
    {
        // The first unnamed Initial sequence: eight events, none carrying a start, so the whole
        // burst fires in the instant the effect starts.
        var opening = new BurstLane("", new[]
        {
            new BurstStep(0, "CallSequence", "he_light_seq", 0f),
            new BurstStep(1, "CallAnimation", "call_he_ring", 0f),
            new BurstStep(2, "CallAnimation", "call_hetrails_up", 0f),
            new BurstStep(3, "CallAnimation", "large_fireball", 0f),
            new BurstStep(4, "CallAnimation", "call_he_ring1", 0f),
            new BurstStep(5, "Sound", "ground_mixed_exp_sg", 0f),
            new BurstStep(6, "CallAnimation", "call_hetrails_up", 0f),
            new BurstStep(7, "CallSequence", "he_flashes", 0f),
        });
        // The second unnamed Initial sequence. The IF and the ENDIF are control flow the runner interprets
        // itself and never dispatches, so #1 is the only row this lane can produce, and it produces it only
        // because the burst is played at the camera, which is what makes the range condition true.
        var fbfxGate = new BurstLane("", new[]
        {
            new BurstStep(1, "CallSequence", "frame_buffer_effects1", 0f),
        });
        // he_light_seq: LIGHT_STATE on, then six LIGHT_ANIMATION ramps whose run times chain
        // (0.05, 0.025, 0.05, 0.025 → 0.15), one `Event + 0.2` gap, then 0.05 and 0.01, then off.
        var lightSeq = new BurstLane("he_light_seq", new[]
        {
            new BurstStep(0, "LightState", "he_light", 0f),
            new BurstStep(1, "LightAnimation", "he_light", 0f),
            new BurstStep(2, "LightAnimation", "he_light", 0.05f),
            new BurstStep(3, "LightAnimation", "he_light", 0.075f),
            new BurstStep(4, "LightAnimation", "he_light", 0.125f),
            new BurstStep(5, "LightAnimation", "he_light", 0.35f),
            new BurstStep(6, "LightAnimation", "he_light", 0.4f),
            new BurstStep(7, "LightState", "he_light", 0.41f),
        });
        var flashes = new BurstLane("he_flashes", new[]
        {
            new BurstStep(0, "LightState", "he_light1", 0f),
            new BurstStep(1, "LightAnimation", "he_light1", 0f),
            new BurstStep(2, "LightAnimation", "he_light1", 0.1f),
            new BurstStep(3, "LightState", "he_light1", 0.6f),
        });
        // The wash: six FBFX_COLOR_FROM_TO steps. A handler reporting 0 as its duration fires all six in
        // one instant, which is what the times here refuse. fbfx-flash asserts the colours and run times;
        // this asserts their place in the burst.
        var wash = new BurstLane("frame_buffer_effects1", new[]
        {
            new BurstStep(0, "FbfxColorFromTo", null, 0f),
            new BurstStep(1, "FbfxColorFromTo", null, 0.2f),
            new BurstStep(2, "FbfxColorFromTo", null, 0.6f),
            new BurstStep(3, "FbfxColorFromTo", null, 0.8f),
            new BurstStep(4, "FbfxColorFromTo", null, 0.9f),
            new BurstStep(5, "FbfxColorFromTo", null, 1.1f),
        });
        // The callee. `activate_puffer` shows the fireball, calls `p1trail` (which starts the
        // emitter) and then, at an authored `Event + 0.3`, STOPs `stop_p1trail` — an ON_CALL
        // sequence nothing has called, so it is parked and the stop halts nothing.
        var fireball = new[]
        {
            new BurstLane("activate_puffer", new[]
            {
                new BurstStep(0, "ObjectActiveState", "flame_ball_01", 0f),
                new BurstStep(1, "CallSequence", "p1trail", 0f),
                new BurstStep(2, "StopSequence", "stop_p1trail", 0.3f),
            }),
            new BurstLane("p1trail", new[]
            {
                new BurstStep(0, "PufferState", "fierypuffer", 0f),
            }),
        };

        WithBurst(ctx, world, "he_ground_effect", report, fired =>
        {
            CheckLanes(ctx, "he_ground_effect", fired, new[] { opening, fbfxGate, lightSeq, flashes, wash }, report);
            CheckLanes(ctx, "large_fireball", fired, fireball, report);
            // The parked stopper, and the reason `large_fireball` is asserted here at all. Its
            // `p1trail` lane above is the live control: without it, "the stopper fired nothing" is also what a
            // fireball that never started reports.
            int stopper = fired.Count(f => f.Anim == "large_fireball" && f.Sequence == "stop_p1trail");
            ctx.Check(stopper == 0,
                $"large_fireball's parked stop_p1trail dispatched nothing — a STOP_SEQUENCE halts and never starts (B12) ({stopper} event(s))");
        });
    }

    // `flash_control-flash_effect.json` — the pure light case, two sequences. The one
    // timed event in it is a `START_TIME ANIMATION 1.5`, read against the instance clock.
    internal static void FlashBurstTimeline(TestContext ctx, TestWorld world, System.Text.StringBuilder report)
    {
        var lanes = new[]
        {
            new BurstLane("", new[]
            {
                new BurstStep(0, "ObjectActiveState", "lens_flash", 0f),
                new BurstStep(1, "CallSequence", "flash_flashes", 0f),
                new BurstStep(2, "CallAnimation", "call_flasher", 0f),
                // `Animation + 1.5` — the instance clock, which for this Initial sequence is also
                // its own, so the value is the assertion and the origin is not (sonic's second
                // `sonic_light_seq` pass is where the two clocks differ).
                new BurstStep(3, "ObjectActiveState", "lens_flash", 1.5f),
            }),
            new BurstLane("flash_flashes", new[]
            {
                new BurstStep(0, "LightState", "flash_light1", 0f),
                new BurstStep(1, "LightAnimation", "flash_light1", 0f),
                new BurstStep(2, "LightAnimation", "flash_light1", 0.5f),
                new BurstStep(3, "LightState", "flash_light1", 0.75f),
            }),
        };
        WithBurst(ctx, world, "flash_effect", report,
            fired => CheckLanes(ctx, "flash_effect", fired, lanes, report));
    }

    // `sonic_effect-sonic_ground_effect.json` — four sequences, two unnamed and Initial.
    // The repeat-call case: the first Initial sequence calls `sonic_light_seq` at #0 and again at #4,
    // 1.2 s later.
    internal static void SonicBurstTimeline(TestContext ctx, TestWorld world, System.Text.StringBuilder report)
    {
        // The 15-event Initial sequence. #3 carries `START_TIME ANIMATION 1.2`; everything behind
        // it is untimed, so the whole second half fires in that instant.
        var main = new BurstLane("", new[]
        {
            new BurstStep(0, "CallSequence", "sonic_light_seq", 0f),
            new BurstStep(1, "CallAnimation", "sonic_puff1", 0f),
            new BurstStep(2, "CallAnimation", "call_flare", 0f),
            new BurstStep(3, "CallAnimation", "sonic_puff4", 1.2f),
            new BurstStep(4, "CallSequence", "sonic_light_seq", 1.2f),
            new BurstStep(5, "CallSequence", "sonic_growlight", 1.2f),
            new BurstStep(6, "CallAnimation", "call_flare", 1.2f),
            new BurstStep(7, "CallAnimation", "sonic_puff5", 1.2f),
            new BurstStep(8, "CallAnimation", "sonic_puff6", 1.2f),
            new BurstStep(9, "CallAnimation", "sonic_puff7", 1.2f),
            new BurstStep(10, "CallAnimation", "sonic_puff8", 1.2f),
            new BurstStep(11, "CallAnimation", "sonic_puff9", 1.2f),
            new BurstStep(12, "CallAnimation", "sonic_puff10", 1.2f),
            new BurstStep(13, "CallAnimation", "sonic_puff11", 1.2f),
            new BurstStep(14, "CallAnimation", "sonic_emit_downer", 1.2f),
        });
        // The second Initial sequence: four rising rings at once, then one at `START_TIME
        // SEQUENCE 1.2` — an absolute gate against this sequence's own clock, not the instance's.
        var rings = new BurstLane("", new[]
        {
            new BurstStep(0, "CallAnimation", "ring_up1", 0f),
            new BurstStep(1, "CallAnimation", "ring_up2", 0f),
            new BurstStep(2, "CallAnimation", "ring_up3", 0f),
            new BurstStep(3, "CallAnimation", "ring_up4", 0f),
            new BurstStep(4, "CallAnimation", "ring_down1", 1.2f),
        });
        // Two passes of ONE sequence, 1.2 s apart. The first must have ended for the second call to find it
        // parked and restart it: a call into a running sequence is a no-op, and a second concurrent copy is
        // not a thing the original can express.
        BurstLane LightPass(float from) => new("sonic_light_seq", new[]
        {
            new BurstStep(0, "LightState", "sonic_light", from),
            new BurstStep(1, "LightAnimation", "sonic_light", from),
            new BurstStep(2, "LightState", "sonic_light", from + 0.3f),
        });
        var grow = new BurstLane("sonic_growlight", new[]
        {
            new BurstStep(0, "LightState", "sonic_light1", 1.2f),
            new BurstStep(1, "LightAnimation", "sonic_light1", 1.2f),
            new BurstStep(2, "LightAnimation", "sonic_light1", 2.4f),
            new BurstStep(3, "LightState", "sonic_light1", 3.2f),
        });
        WithBurst(ctx, world, "sonic_ground_effect", report, fired =>
        {
            CheckLanes(ctx, "sonic_ground_effect", fired,
                new[] { main, rings, LightPass(0f), LightPass(1.2f), grow }, report);
            // Stated on its own as well as through the lanes: the lane pair above proves the two
            // passes ran, and this proves nothing else did. A third pass would be a call that
            // found the sequence parked when the original would not have.
            int passes = fired.Count(f => f.Anim == "sonic_ground_effect"
                                          && f.Sequence == "sonic_light_seq" && f.Index == 0);
            ctx.Same(2, passes, $"sonic_light_seq's two authored calls started it exactly twice (B11)");
        });
    }

    // Plays one burst on its own miniature world-effects stage and hands the recorded dispatch log to
    // body. The stage's template ROOTS are derived from the definition's own CALL_ANIMATION closure
    // against the chapter gamez, the same derivation the production bind runs, so a definition whose
    // anchor resolves nowhere throws here, naming it, instead of quietly playing nothing. Everything
    // else is the production world-effects role, with the camera as the player position.
    internal static void WithBurst(TestContext ctx, TestWorld world, string animName,
        System.Text.StringBuilder report, System.Action<IReadOnlyList<BurstFire>> body)
    {
        var roots = Session.EffectCatalogue.StageRootsFor(world.Session.Program, new[] { animName },
            Session.WorldEffectsFactory.StageRootResolver(world.Gamez));
        ctx.Check(roots.Count > 0,
            $"{animName}: its call closure's anchor roots derived ({roots.Count}: {string.Join(", ", roots)})");
        var stage = new Node3D { Name = $"BurstStage_{animName}" };
        var pool = new Node3D { Name = "pool0" };
        pool.SetMeta(AnimRuntime.PoolSlotMeta, 0);
        stage.AddChild(pool);
        int built = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
            world.Session.Builder.Scene, pool, roots);
        ctx.Same(roots.Count, built, $"{animName}: template roots staged from the chapter gamez");
        foreach (var child in pool.GetChildren())
        {
            if (child is Node3D root)
            {
                root.Visible = false;
            }
        }

        var runtime = AnimRuntime.ForEffects(
            AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
            1, new CountingEmitterFactory(), false, BurstTtl,
            () => ctx.Camera.GlobalPosition);
        runtime.ManualAdvance = true;
        ctx.Host.AddChild(stage);
        ctx.Host.AddChild(runtime);
        try
        {
            runtime.Bind(stage, world.Session.Program.Subset(animName));
            var fired = new List<BurstFire>();
            float clock = 0f;
            runtime.OnEventDispatched = d => fired.Add(new BurstFire(clock,
                d.Def.AnimName ?? d.Def.Name, d.Sequence, d.EventIndex, d.EventKind, d.EventName));
            // At the camera, so the definitions' own PLAYER_RANGE gates pass.
            ctx.Check(runtime.PlayEffectAt(animName, ctx.Camera.GlobalPosition),
                $"{animName} resolved to a definition and started");
            int steps = Mathf.RoundToInt(BurstSeconds / BurstDt);
            for (int i = 0; i < steps; i++)
            {
                clock += BurstDt;
                runtime.Advance(BurstDt);
            }

            runtime.OnEventDispatched = null;
            runtime.UnhandledEventCounts.TryGetValue("PufferState(no host node)", out int hostless);
            ctx.Same(0, hostless, $"{animName}: PUFFER_STATE events that found no host node");
            ctx.Note($"{animName}: {fired.Count} dispatch(es) over {BurstSeconds:0.#} s at {BurstDt:0.####} s steps");
            body(fired);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }

    // Matches a definition's recorded dispatches against its authored lanes and asserts both halves of
    // "the timeline is right": ORDER, each lane's rows arriving in the sequence's own order with
    // nothing unauthored arriving, and TIME, each row landing on its authored instant within
    // BurstSlack. A row is claimed by the first lane whose next unconsumed step it matches on sequence,
    // index, kind and name, so a row that arrives early or twice is reported stray. The four-part key
    // is needed because a definition's sequence NAMES are not unique.
    internal static void CheckLanes(TestContext ctx, string animName, IReadOnlyList<BurstFire> fired,
        BurstLane[] lanes, System.Text.StringBuilder report)
    {
        var own = fired.Where(f => f.Anim == animName).ToList();
        report.AppendLine($"--- {animName}: {own.Count} dispatch(es) ---");
        foreach (var f in own)
        {
            report.AppendLine($"  {f.T,7:0.0000}s  [{(f.Sequence.Length == 0 ? "<unnamed>" : f.Sequence)}] "
                              + $"#{f.Index} {f.Kind} {f.Name}");
        }

        var cursor = new int[lanes.Length];
        var at = new float[lanes.Length][];
        for (int i = 0; i < lanes.Length; i++)
        {
            at[i] = new float[lanes[i].Steps.Length];
        }

        var stray = new List<BurstFire>();
        foreach (var f in own)
        {
            int lane = -1;
            for (int l = 0; l < lanes.Length && lane < 0; l++)
            {
                if (cursor[l] >= lanes[l].Steps.Length)
                {
                    continue;
                }
                var step = lanes[l].Steps[cursor[l]];
                if (lanes[l].Sequence == f.Sequence && step.Index == f.Index
                    && step.Kind == f.Kind && step.Name == f.Name)
                {
                    lane = l;
                }
            }
            if (lane < 0)
            {
                stray.Add(f);
                continue;
            }
            at[lane][cursor[lane]] = f.T;
            cursor[lane]++;
        }

        string strays = string.Join(", ",
            stray.Select(s => $"{s.T:0.###}s [{s.Sequence}] #{s.Index} {s.Kind} {s.Name}"));
        ctx.Check(stray.Count == 0,
            $"{animName}: every dispatch is an authored event arriving in its sequence's order ({stray.Count} stray: {strays})");
        for (int l = 0; l < lanes.Length; l++)
        {
            var lane = lanes[l];
            string tag = $"{animName} [{(lane.Sequence.Length == 0 ? "<unnamed>" : lane.Sequence)}]";
            ctx.Same(lane.Steps.Length, cursor[l], $"{tag}: authored events fired, in order");
            for (int s = 0; s < cursor[l]; s++)
            {
                var step = lane.Steps[s];
                ctx.Check(Mathf.Abs(at[l][s] - step.At) <= BurstSlack,
                    $"{tag} #{step.Index} {step.Kind} fires at its authored {step.At:0.###} s ({at[l][s]:0.###} s)");
            }
        }
    }

    // ---- a new panel's tear must not steal a live panel's template copy ------------------------

    // The crash rig's damage-stage templates are pooled, and a relocating CALL_ANIMATION from a NEW
    // anchor takes its own copy instead of teleporting the one a previous anchor's burst is still
    // flying on. Reproduces the shipped shape: two authored pdpanelN defs each calling gimmeflakes at
    // their own pdpN, on a runtime carrying the crash rig's role flags plus the pool. Without the pool
    // the second call restarts the single shared planeflakes root mid-flight, which is the "panels fly
    // away repeatedly, and from the wrong site" symptom.
    internal static void DamageTemplatePool(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var stage = new Node3D { Name = "DamagePoolStage" };
            var pdp5 = PoolAnchorNode("pdp5", new Vector3(-10, 0, 0));
            var pdp4 = PoolAnchorNode("pdp4", new Vector3(10, 0, 0));
            stage.AddChild(pdp5);
            stage.AddChild(pdp4);
            var copies = new List<Node3D>();
            for (int slot = 0; slot < 2; slot++)
            {
                var pool = new Node3D { Name = $"pool{slot}" };
                pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
                stage.AddChild(pool);
                int built = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
                    world.Session.Builder.Scene, pool, new[] { "planeflakes" });
                ctx.Check(built == 1, $"slot {slot} staged its planeflakes copy");
                foreach (var child in pool.GetChildren())
                {
                    if (child is Node3D copy)
                    {
                        copies.Add(copy);
                    }
                }
            }

            var runtime = new AnimRuntime(
                Session.WorldEffectsFactory.NewCrashTemplateStage())
            {
                AutoStart = false,
                ManualAdvance = true,
                SoundHandledElsewhere = true,
                EmitterFactory = new CountingEmitterFactory(),
                NameResolveFallback = true,
            };
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, world.Session.Program.Subset(new[] { "pdpanel4", "pdpanel5" }));
                runtime.Play("pdpanel5", stage, applyReset: false);
                for (int i = 0; i < 6; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                var first = copies.Find(c => AtPoolSite(c, pdp5));
                ctx.Check(first != null, $"pdpanel5's tear placed a planeflakes copy at pdp5");

                // Mid-flight of the first burst (the def's authored RUN_TIME is 1.0 s), the
                // second panel tears.
                runtime.Play("pdpanel4", stage, applyReset: false);
                for (int i = 0; i < 6; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                var second = copies.Find(c => AtPoolSite(c, pdp4));
                ctx.Check(second != null, $"pdpanel4's tear placed a planeflakes copy at pdp4");
                ctx.Check(first != null && AtPoolSite(first, pdp5),
                    $"pdp5's copy stayed at ITS OWN site — the new tear did not steal it (BL-288)");
                ctx.Check(first != null && second != null && !ReferenceEquals(first, second),
                    $"the two tears hold two different copies");
                ctx.Check(runtime.PoolRecycles == 0,
                    $"no pool wrap for two anchors over two copies ({runtime.PoolRecycles})");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // A flat named call-site node for DamageTemplatePool — name meta set
    // the way the crash rig's own anchor scaffold sets it, so resolution finds it.
    internal static Node3D PoolAnchorNode(string name, Vector3 at)
    {
        var node = new Node3D { Name = name, Position = at };
        node.SetMeta(AnimRuntime.NameMeta, name);
        return node;
    }

    internal static bool AtPoolSite(Node3D? copy, Node3D site) =>
        copy != null
        && copy.GlobalTransform.Origin.DistanceTo(site.GlobalTransform.Origin) < 0.5f;

    // ---- the injure staging is keyed on health, not the combined progression -------------------

    // The decoded per-part panel threshold: the original divides the part's health by its health max,
    // and blocks health damage outright while that part's armour covers the hit, so an armoured zone
    // crosses no per-part threshold at all. Driven on a real plane model with the shipped injure_anims:
    // strip the zone's armour and no panel may flip, then drive health under the threshold and the
    // panel must appear. The first half is what fails when the staging is fed PartState.Fraction.
    internal static void DamageStagingPool(TestContext ctx)
    {
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);

        // Data-driven, not a hardcoded zone: any part whose authored list flips a pdpanelN, taking
        // its HIGHEST threshold so one health step crosses exactly one panel.
        DestroyablePart? part = null;
        float threshold = 0f;
        string panelAnim = "";
        foreach (var p in stats.DestroyableParts)
            foreach (var (frac, anim) in p.InjureAnims)
                if (anim.StartsWith("pdpanel", System.StringComparison.OrdinalIgnoreCase)
                    && frac > threshold)
                {
                    part = p;
                    threshold = frac;
                    panelAnim = anim;
                }

        ctx.Check(part != null, $"{ctx.PlaneName} authors a pdpanelN entry on some zone");
        if (part == null)
            return;
        ctx.Check(part.MaxArmor > 0f && part.MaxHp > 0f,
            $"precondition: {part.Name} carries both pools ({part.MaxArmor:0} armour, {part.MaxHp:0} hp)");
        if (part.MaxArmor <= 0f || part.MaxHp <= 0f)
            return;

        // The combined fraction with armour gone is MaxHp/(MaxHp+MaxArmor); the half this suite
        // pins only means anything when that already sits at or under the panel's threshold.
        float strippedCombined = part.MaxHp / (part.MaxHp + part.MaxArmor);
        ctx.Check(strippedCombined <= threshold,
            $"precondition: armour gone puts the COMBINED fraction at {strippedCombined:0.00}, already past {panelAnim}'s {threshold:0.00} — the early tear this pins");
        if (strippedCombined > threshold)
            return;

        var textures = new TextureArchive(texturesPath);
        // damagePanels: the pdpN nodes are skipped in a plain static build (PlaneBuilder 10c).
        var builder = new PlaneBuilder(planesGamez, textures, damagePanels: true);
        var model = builder.Build(ctx.PlaneName);
        ctx.Host.AddChild(model);
        try
        {
            var visuals = new DamageVisuals(builder.DamagePanels, model, stats);
            var torn = builder.DamagePanels
                .Where(p => p.Name.ToString().StartsWith("pdp", System.StringComparison.OrdinalIgnoreCase)
                            && !p.Name.ToString().EndsWith("_h", System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            ctx.Check(torn.Count > 0, $"{ctx.PlaneName} carries {torn.Count} torn-panel nodes");
            ctx.Check(torn.All(p => !p.Visible), $"…and every one of them starts hidden");

            var damage = new PlaneDamage(stats.DestroyableParts);
            var state = damage.Apply(part.Name, 0f, part.MaxArmor)!;
            ctx.Check(state.Armor <= 0f && Mathf.IsEqualApprox(state.HealthFraction, 1f),
                $"{part.Name}: armour stripped to {state.Armor:0.#}, health untouched at {state.HealthFraction * 100f:0}% (combined {state.Fraction:0.00})");

            visuals.OnPartDamage(part.Name, state.HealthFraction);
            visuals.OnHullDamage(damage.SummaryHealthFraction);
            ctx.Check(torn.All(p => !p.Visible),
                $"no panel tore on the armour spend, though the combined fraction ({state.Fraction:0.00}) is past {panelAnim}'s {threshold:0.00}");

            // Now health itself crosses: drive it just under the threshold.
            float target = (threshold - 0.02f) * part.MaxHp;
            state = damage.Apply(part.Name, part.MaxHp - target, 0f)!;
            ctx.Check(state.HealthFraction <= threshold,
                $"{part.Name} health driven to {state.HealthFraction:0.00}, under {threshold:0.00}");

            visuals.OnPartDamage(part.Name, state.HealthFraction);
            ctx.Check(torn.Any(p => p.Visible),
                $"…and {panelAnim} flipped its torn panel once HEALTH crossed");
        }
        finally
        {
            model.Free();
        }
    }

    // ---- binding the crash rig must leave the airframe under the controller --------------------

    // Builds the crash rig the way WorldEffectsFactory.BuildFlightCrashRuntime does, binds the
    // crash-rig subset, and asserts the two things that go wrong in flight. ⚠ The airframe model stays
    // a plain child of the controller, not world-pinned: on the Devastator the reset defs' authored
    // name is the model root itself, and the reset chain must not relocate the aircraft the way it
    // places effect templates. ⚠ Every pooled template copy of one root must show the same lit mesh
    // count as its slot-0 sibling; a copy the reset pass missed stays lit for the whole session.
    internal static void CrashRigAnchors(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            try
            {
                foreach (var model in new[] { "player_bhawk", "player_pfighter" })
                {
                    var controller = new Node3D { Name = "controller_replica" };
                    var runtime = AnimRuntime.ForCrashRig(
                        Session.WorldEffectsFactory.NewCrashTemplateStage(),
                        1, new CountingEmitterFactory(), false);
                    runtime.ManualAdvance = true;
                    try
                    {
                        var builder = new PlaneBuilder(planesGamez, textures);
                        var planeModel = builder.Build(model);
                        controller.AddChild(planeModel);
                        var crashRoot = new Node3D { Name = "player" };
                        crashRoot.SetMeta(AnimRuntime.NameMeta, "player");
                        crashRoot.Transform = planeModel.Transform;
                        controller.AddChild(crashRoot);
                        var rootNames = Session.WorldEffectsFactory.CrashStageRootNames(
                            world.Session.Program, world.Gamez, controller);
                        Session.WorldEffectsFactory.StageCrashTemplates(world.Gamez,
                            world.Session.Builder.Scene, crashRoot, rootNames,
                            Utils.EffectPools.Load());
                        var copies = new List<(string Root, int Slot, Node3D Copy)>();
                        foreach (var child in crashRoot.GetChildren())
                        {
                            if (child is Node3D pool && pool.HasMeta(AnimRuntime.PoolSlotMeta))
                            {
                                int slot = (int)pool.GetMeta(AnimRuntime.PoolSlotMeta);
                                foreach (var staged in pool.GetChildren())
                                {
                                    if (staged is Node3D copy)
                                    {
                                        string root = copy.HasMeta(AnimRuntime.NameMeta)
                                            ? (string)copy.GetMeta(AnimRuntime.NameMeta)
                                            : copy.Name;
                                        copies.Add((root, slot, copy));
                                    }
                                }
                            }
                        }

                        var wreck = builder.BuildDestroyed(model);
                        var restPoses = new List<(Node3D Node, Transform3D RestPose)>();
                        if (wreck != null)
                        {
                            wreck.Visible = false;
                            crashRoot.AddChild(wreck);
                            CollectRestPoses(wreck, restPoses);
                        }

                        ctx.Host.AddChild(controller);
                        ctx.Host.AddChild(runtime);
                        var restOrigin = planeModel.GlobalTransform.Origin;
                        runtime.Bind(controller,
                            world.Session.Program.Subset(Session.EffectCatalogue.CrashRigAnimNames(
                                Session.EffectCatalogue.CrashDefTable(world.Session.Program))));
                        for (int i = 0; i < 6; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        ctx.Check(!planeModel.TopLevel,
                            $"{model}: the airframe model is not world-pinned (TopLevel) by the rig's bind");
                        ctx.Check(planeModel.GetParent() == controller,
                            $"{model}: the airframe model still hangs under the controller");
                        ctx.Check(planeModel.GlobalTransform.Origin.DistanceTo(restOrigin) < 0.5f,
                            $"{model}: the airframe model has not moved off its rig position");
                        // Staged dark: a copy left lit sits at the plane's centre for the whole
                        // session (the flake/gunhit family has no authored deactivation).
                        var lit = string.Join("; ", copies
                            .Select(c => (c.Root, c.Slot, Lit: LitMeshCount(c.Copy)))
                            .Where(c => c.Lit > 0)
                            .Select(c => $"'{c.Root}' slot{c.Slot} lights {c.Lit}"));
                        ctx.Check(lit.Length == 0,
                            $"{model}: every staged template copy is dark after the bind{(lit.Length == 0 ? "" : $" — {lit}")}");

                        // A real tear: the damage sink's own call shape. The CALLed gimmeflakes
                        // copy must light at its pdp5 site, and the panel def's instance ending on
                        // the AIRFRAME anchor must not drag the model into the retire-hide.
                        runtime.Play("pdpanel5", planeModel, applyReset: false);
                        for (int i = 0; i < 6; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        ctx.Check(copies.Any(c => c.Root == "planeflakes" && LitMeshCount(c.Copy) > 0),
                            $"{model}: the tear's planeflakes copy is revealed while its burst flies");
                        for (int i = 0; i < 120; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        ctx.Check(copies.All(c => c.Root != "planeflakes" || LitMeshCount(c.Copy) == 0),
                            $"{model}: the burst's copy goes dark again once the effect is over");
                        ctx.Check(!planeModel.TopLevel
                                  && planeModel.GlobalTransform.Origin.DistanceTo(restOrigin) < 0.5f
                                  && planeModel.Visible,
                            $"{model}: the airframe model is still parented, placed and visible after the tear");

                        // Crash, respawn, move, crash again: the wreck and every template a crash reveals must play at the
                        // SECOND crash's site. The failure looks like the destroyed plane and the dirt burst replaying at
                        // the first crash's position on every crash after the first.
                        runtime.Play("player_crash_dirt", crashRoot, applyReset: false);
                        for (int i = 0; i < 180; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        // The respawn ritual, FlightController.Respawn's crash arm verbatim.
                        runtime.ResetToBaseState();
                        foreach (var (node, rest) in restPoses)
                        {
                            node.Transform = rest;
                        }

                        var leftover = string.Join("; ", copies
                            .Select(c => (c.Root, c.Slot, Lit: LitMeshCount(c.Copy)))
                            .Where(c => c.Lit > 0)
                            .Select(c => $"'{c.Root}' slot{c.Slot} lights {c.Lit}"));
                        ctx.Check(leftover.Length == 0,
                            $"{model}: respawn leaves no crash template revealed{(leftover.Length == 0 ? "" : $" — {leftover}")}");

                        controller.Position += new Vector3(400, 0, 0);
                        runtime.Play("player_crash_dirt", crashRoot, applyReset: false);
                        for (int i = 0; i < 180; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        var here = controller.GlobalTransform.Origin;
                        if (wreck != null)
                        {
                            ctx.Check(wreck.GlobalTransform.Origin.DistanceTo(here) < 150f,
                                $"{model}: the wreck flies from the SECOND crash's site ({wreck.GlobalTransform.Origin.DistanceTo(here):0} m away)");
                        }

                        var stale = string.Join("; ", copies
                            .Where(c => LitMeshCount(c.Copy) > 0
                                        && c.Copy.GlobalTransform.Origin.DistanceTo(here) > 150f)
                            .Select(c => $"'{c.Root}' slot{c.Slot} at {c.Copy.GlobalTransform.Origin.DistanceTo(here):0} m"));
                        ctx.Check(stale.Length == 0,
                            $"{model}: every template the second crash reveals plays at its own site{(stale.Length == 0 ? "" : $" — {stale}")}");
                        ctx.Check(!crashRoot.TopLevel,
                            $"{model}: the crash scaffold is never world-pinned by a crash's own calls");
                    }
                    finally
                    {
                        runtime.Free();
                        controller.Free();
                    }
                }
            }
            finally
            {
                textures.Dispose();
            }
        });
    }

    // Every wreck node's rest pose — the local mirror of
    // `WorldEffectsFactory.CollectRestPoses`, so the suite's respawn ritual can re-home the
    // flung pieces the way `FlightController.Respawn` does.
    internal static void CollectRestPoses(Node3D node, List<(Node3D Node, Transform3D RestPose)> into)
    {
        into.Add((node, node.Transform));
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D sub)
            {
                CollectRestPoses(sub, into);
            }
        }
    }

    // Meshes drawing under one staged template copy — visibility taken in-tree, so a
    // parent the reset pass switched off darkens the whole copy the way it does on screen.
    internal static int LitMeshCount(Node3D copy)
    {
        int n = copy is MeshInstance3D lit && lit.IsVisibleInTree() ? 1 : 0;
        foreach (var child in copy.GetChildren())
        {
            if (child is Node3D sub)
            {
                n += LitMeshCount(sub);
            }
        }
        return n;
    }

    // ---- an AI plane's crash picks from the ai_crash_* vector ----------------------------

    // The AI arm of the crash-family split, through the REAL factory call, which keys the family on
    // IsHumanPiloted: an AI controller's rig binds the ai_crash_* vector, a crash on a body stamped
    // dirt selects ai_crash_dirt, and a crash with no struck body takes the null-material arm to slot
    // 0, ai_crash_default, never a player_crash_* def. ⚠ Keep the human-piloted A/B control; without
    // it a family mix-up in the pick would be invisible from the AI side alone.
    internal static void AiCrashDefs(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            var factory = new Session.WorldEffectsFactory(
                SessionSpec.Parse(System.Array.Empty<string>()), ctx.Host, () => Vector3.Zero);
            FlightController? ai = null;
            FlightController? human = null;
            StaticBody3D? dirt = null;
            try
            {
                var spawn = new Vector3(0f, 500f, 0f);
                var builder = new PlaneBuilder(planesGamez, textures);
                var aiModel = builder.Build(ctx.PlaneName);
                ai = new FlightController
                {
                    PlaneModel = aiModel,
                    Collider = PlaneCollider.Build(aiModel),
                    PlayerIndex = FlightRoster.ShooterIdBase,
                    IsHumanPiloted = false,
                    Pilot = AiPilot.HoldingCourse(spawn, spawn + Vector3.Forward),
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                ai.AddChild(aiModel);
                ai.Setup(new FlightModel(stats), null, new CamParams(), spawn, spawn + Vector3.Forward);
                ctx.Host.AddChild(ai);
                // The planes gamez goes in as both spawners pass it: the destroy def's `chuteman`
                // is a template root of planes.zbd, and without it the rig cannot stage it.
                factory.BuildFlightCrashRuntime(ai, builder, ctx.PlaneName, world.Gamez,
                    world.Session.Builder.Scene, textures, world.Session.Program, verbose: false,
                    planesGamez: planesGamez);

                ctx.Check(ai.CrashRuntime != null && ai.CrashDefs != null,
                    $"the AI rig built a crash runtime with a def table");
                if (ai.CrashDefs == null)
                    return;
                ctx.Check(ai.CrashDefs.PlayableDefs.Count == 3
                          && ai.CrashDefs.PlayableDefs.All(d =>
                              d.StartsWith(Session.EffectCatalogue.AiCrashDefPrefix, System.StringComparison.Ordinal)),
                    $"the AI table's playable slots are the ai_crash_* trio [{string.Join(", ", ai.CrashDefs.PlayableDefs)}]");

                // The two subtrees one context node has to reach: `healthy` sits on the plane
                // model, `destroyed` under the crash root. Both shown first, or the wreck's
                // built-hidden state would answer for the deactivation instead of the def.
                var healthy = ai.PlaneModel?.FindChild("healthy", true, false) as Node3D;
                var wreck = ai.CrashAnchor?.FindChild("destroyed", true, false) as Node3D;
                if (healthy != null)
                    healthy.Visible = true;
                if (wreck != null)
                    wreck.Visible = true;

                // A crash on a known surface: a struck body stamped dirt(13) — the id cascade's
                // own-slot arm, through the production Crash path.
                dirt = new StaticBody3D { Name = "dirt_probe" };
                dirt.SetMeta(SceneBuilder.SurfaceIdMeta, 13);
                ctx.Host.AddChild(dirt);
                ai.DebugForceCrash(null, dirt);
                ctx.Check(ai.Crashed && ai.LastCrashDef == Session.EffectCatalogue.AiCrashDefPrefix + "dirt",
                    $"an AI crash on dirt(13) plays ai_crash_dirt def={ai.LastCrashDef ?? "-"}");
                string healthyState = healthy == null ? "-" : healthy.Visible ? "on" : "off";
                string wreckState = wreck == null ? "-" : wreck.Visible ? "on" : "off";
                ctx.Check(healthy is { Visible: false } && wreck is { Visible: false },
                    $"…and the def's own OBJECT_ACTIVE_STATE events reach BOTH subtrees off one context node: healthy={healthyState} destroyed={wreckState}");

                // No struck body: the null-material arm resolves slot 0 of the SAME family.
                ai.Respawn();
                ai.DebugForceCrash();
                ctx.Check(ai.LastCrashDef == Session.EffectCatalogue.AiCrashDefPrefix + "default",
                    $"an AI crash with no material falls to ai_crash_default def={ai.LastCrashDef ?? "-"}");

                // The A/B control: a human rig through the same factory keeps the player family.
                var humanBuilder = new PlaneBuilder(planesGamez, textures);
                var humanModel = humanBuilder.Build(ctx.PlaneName);
                human = new FlightController
                {
                    PlaneModel = humanModel,
                    Collider = PlaneCollider.Build(humanModel),
                    PlayerIndex = 0,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                human.AddChild(humanModel);
                human.Setup(new FlightModel(stats), ctx.Camera, new CamParams(),
                    spawn + new Vector3(2000f, 0f, 0f), spawn + new Vector3(2000f, 0f, -1f));
                ctx.Host.AddChild(human);
                factory.BuildFlightCrashRuntime(human, humanBuilder, ctx.PlaneName, world.Gamez,
                    world.Session.Builder.Scene, textures, world.Session.Program, verbose: false,
                    planesGamez: planesGamez);
                ctx.Check(human.CrashDefs != null && human.CrashDefs.PlayableDefs.All(d =>
                        d.StartsWith(Session.EffectCatalogue.CrashDefPrefix, System.StringComparison.Ordinal)),
                    $"the same factory keeps a human rig on player_crash_* [{string.Join(", ", human.CrashDefs?.PlayableDefs ?? System.Array.Empty<string>())}]");
                human.DebugForceCrash(null, dirt);
                ctx.Check(human.LastCrashDef == Session.EffectCatalogue.CrashDefPrefix + "dirt",
                    $"…and its dirt crash plays player_crash_dirt def={human.LastCrashDef ?? "-"}");
            }
            finally
            {
                dirt?.Free();
                human?.Free();
                ai?.Free();
                textures.Dispose();
            }
        });
    }

    // ---- the full effects sweep as suite verdicts ----------------------------------------------

    // Asserts every effects-test entry on a full replica stage so sweep verdicts fail the build.
    // The fixed ~180 m play point detects a template that failed to relocate.
    // ⚠ Puffer and mesh tallies are golden only for seed 1 with this counting factory.

}

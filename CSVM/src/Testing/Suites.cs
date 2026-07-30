using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The registered <c>--run-tests</c> suites. Each one asserts on a <see cref="Probes"/> verdict or
/// on live engine state; none of them re-implements a check the inspection reports already do.
///
/// <para>Expected counts here are <b>golden numbers measured against the retail install</b> — the
/// data is a fixed input, so 48 weapon defs and 267 C1 destructibles are invariants, not
/// guesses. A suite whose data is absent skips rather than passing.</para>
/// </summary>
public static class Suites
{
    private const int PlayerAirframes = 11;
    private const int WeaponDefCount = 48;

    /// <summary>How many flight scenarios carry a measured target to assert. Pinned so that
    /// silently demoting one to informational cannot read as a green run.</summary>
    private const int FlightScenarios = 6;

    /// <summary>Destructible instances / distinct node groups per chapter, at each chapter's
    /// default mission. Instances exceed node groups where a reader wildcard def and its compiled
    /// per-instance twin bind the same nodes.</summary>
    private static readonly (string Chapter, int Instances, int Anchors)[] Census =
    {
        ("C1", 267, 196),
        ("C1B", 108, 108),
        ("C1C", 107, 107),
        ("C2", 574, 201),
        ("C2B", 104, 104),
        ("C3", 501, 202),
        ("C4", 225, 194),
        ("C5", 568, 292),
    };

    /// <summary>C1 textures spanning the three alpha classes the flatten must leave alone: opaque,
    /// hard cutout, and the soft overlays the builder alpha-blends.</summary>
    private static readonly string[] DropInSamples =
    {
        "lkzepskin", "grass1", "cloudlayer", "sky1", // no alpha channel
        "firtree1", "bush1",                          // hard cutouts
        "abld_shadow",                                // soft baked shadow overlay
    };

    public static void Register(List<TestHarness.Suite> into)
    {
        into.Add(new TestHarness.Suite("gauge-colours",
            "the belt indicator's yellow tier is gun-only; hardpoints step green→red", GaugeColours));
        into.Add(new TestHarness.Suite("weapons-defs",
            "every weapons.json BALLISTICS entry reads through the typed reader", WeaponsDefs));
        into.Add(new TestHarness.Suite("flight-envelope",
            "the flown envelope still matches the original's measured manoeuvres", FlightEnvelope));
        into.Add(new TestHarness.Suite("markers-rig",
            "every player airframe has a firepoint/pylon rig in planes.zbd", MarkersRig));
        into.Add(new TestHarness.Suite("loadout-bind",
            "every stock loadout binds to its model with every marker resolved", LoadoutBind));
        into.Add(new TestHarness.Suite("weapons-fire",
            "all 48 weapons mount and fire from a built plane", WeaponsFire));
        into.Add(new TestHarness.Suite("damage-stages",
            "each DAMAGE_SEQUENCE def fires its stage effects across an HP sweep", DamageStages));
        into.Add(new TestHarness.Suite("damage-hd",
            "weapon hits destroy, swap, drop colliders, and survive destroy→reset→destroy", DamageHd));
        into.Add(new TestHarness.Suite("destructible-census",
            "per-chapter destructible registry totals", DestructibleCensus));
        into.Add(new TestHarness.Suite("tex-dropin",
            "the census/override flatten repaints RGB and changes nothing else", TexDropIn));
        into.Add(new TestHarness.Suite("gltf-export",
            "the viewer plane exports to glTF and re-imports with a textured mesh", GltfExport));
    }

    // ---- pure data -----------------------------------------------------------------------------

    /// <summary>The Bloodhawk's flown envelope against the original's, measured off cockpit-gauge
    /// video. These are golden numbers in the same sense as the destructible census — the original
    /// is a fixed artifact, so "150 → 290 mph in 3.76 s" is an invariant of it.
    ///
    /// <para>Why a suite and not a playtest: the flight constants are coupled (thrust sets speed,
    /// speed sets the yaw <c>eff</c>), so editing one silently moves others. The probe's
    /// informational rows — the 1/8-throttle pair and the zoom climb — are deliberately NOT asserted;
    /// they record open questions and must not fail a build.</para></summary>
    private static void FlightEnvelope(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.FlightEnvelope(ctx.ZrdrPath, "player_bhawk");
        ctx.Check(r.Error == null, $"plane stats load error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.WriteArtifact("flight_envelope.txt", r.Text);
        ctx.Same(FlightScenarios, r.Asserted, $"asserted flight scenarios");
        foreach (var row in r.Rows)
        {
            if (row.Asserted)
            {
                string measured = $"original={row.Measured:0.00}{row.Unit} "
                                  + $"tol=±{row.Tolerance:0.00} err={row.ErrorPct:+0.0;-0.0}%";
                ctx.Check(row.Ok, $"{row.Name} model={row.Model:0.00}{row.Unit} {measured}");
            }
            else
            {
                ctx.Note($"{row.Name} model={row.Model:0.00}{row.Unit} not asserted — {row.Detail}");
            }
        }
        ctx.Note($"{r.Summary}");
    }

    /// <summary>BL-024: the belt indicator's yellow tier belongs to guns only — a per-pylon
    /// hardpoint steps straight from green to red at empty, matching the original.</summary>
    private static void GaugeColours(TestContext ctx)
    {
        for (float frac = 0f; frac <= 1f; frac += 0.01f)
        {
            ctx.Check(GaugeCluster.HardpointIndicatorColor(frac) != 1, $"hardpoint colour never yellow at frac={frac:0.00}");
        }
        ctx.Check(GaugeCluster.HardpointIndicatorColor(0f) == 2, $"hardpoint colour red at empty");
        ctx.Check(GaugeCluster.HardpointIndicatorColor(1f) == 0, $"hardpoint colour green at full");

        ctx.Check(GaugeCluster.GunIndicatorColor(GaugeCluster.IndicatorLowFrac) == 1,
            $"gun colour yellow at the low threshold frac={GaugeCluster.IndicatorLowFrac:0.00}");
        ctx.Check(GaugeCluster.GunIndicatorColor(GaugeCluster.IndicatorLowFrac + 0.01f) == 0,
            $"gun colour green just above the low threshold");
        ctx.Check(GaugeCluster.GunIndicatorColor(0f) == 2, $"gun colour red at empty");
    }

    private static void WeaponsDefs(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.Weapons(ctx.ZrdrPath, ctx.MessagesPath, "");
        ctx.Check(r.Error == null, $"weapons.json loads error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.Same(WeaponDefCount, r.Total, $"weapon defs");
        ctx.Same(0, r.UnhandledTotal, $"unhandled weapon keys");
        ctx.Check(!string.IsNullOrEmpty(r.EmptyClipSound), $"empty-clip sound resolves value={r.EmptyClipSound ?? "-"}");
        ctx.Note($"{r.Summary}");
    }

    private static void MarkersRig(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var r = Probes.Markers(ctx.PlanesGamezPath, "");
        ctx.Check(r.Error == null, $"planes gamez loads error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.Same(PlayerAirframes, r.Requested, $"known player airframes");
        ctx.Same(PlayerAirframes, r.Done, $"airframes with a marker rig");
        ctx.Check(r.Missing.Count == 0, $"no airframe missing its root node missing={string.Join(",", r.Missing)}");
    }

    private static void LoadoutBind(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.Loadouts(ctx.ZrdrPath, ctx.MessagesPath, ctx.PlanesGamezPath, ctx.DataRoot,
            "", ctx.LoadoutOverride);
        ctx.Check(r.Error == null, $"loadout inputs load error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        if (ctx.LoadoutOverride != null)
        {
            ctx.Note($"cross-binding every plane to loadout={ctx.LoadoutOverride} — not the stock check");
        }
        ctx.Same(PlayerAirframes, r.Bound, $"stock loadouts bound");
        ctx.Same(0, r.Failed, $"loadout binding failures");
        foreach (string f in r.Failures)
        {
            ctx.Check(false, $"loadout binding {f}");
        }
    }

    // ---- needs a built plane in the tree --------------------------------------------------------

    private static void WeaponsFire(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, Messages.Load(ctx.MessagesPath));
        // The lab holds the archive past construction (it bakes the target material and the
        // impact stand-ins), so it is disposed only after the self-test has run.
        var textures = new TextureArchive(texturesPath);
        Node3D? plane = null;
        UI.WeaponLab? lab = null;
        try
        {
            plane = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ctx.Host.AddChild(plane);
            Loadout? loadout = null;
            foreach (var def in StockLoadouts.Load().All.Values)
            {
                if (def.Model == ctx.PlaneName)
                {
                    loadout = Loadout.Bind(def, plane, weapons);
                    break;
                }
            }
            ctx.Check(loadout != null, $"stock loadout found for plane={ctx.PlaneName}");
            lab = new UI.WeaponLab(plane, weapons, loadout, textures, ctx.Camera, ctx.PlaneName);
            ctx.Host.AddChild(lab);
            var result = lab.SelfTest();
            ctx.Same(WeaponDefCount, result.Total, $"weapons offered to the self-test");
            ctx.Same(WeaponDefCount, result.Ok, $"weapons that mounted and fired");
            ctx.Same(0, result.Errors, $"weapons that threw");
            // A skip is a success-looking outcome in the report — a weapon with no mount on this
            // plane never fires, and nothing else would notice.
            ctx.Same(0, result.Skipped, $"weapons with no mount");
        }
        finally
        {
            lab?.Free();
            plane?.Free();
            textures.Dispose();
        }
    }

    /// <summary>Exports a built plane to a temp <c>.glb</c> and asserts the file lands and re-imports
    /// with at least one textured mesh — the round trip the viewer's <c>--export-gltf=</c>/F10 path
    /// relies on, including that the shader skins convert to a glTF-serializable material.</summary>
    private static void GltfExport(TestContext ctx)
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

    /// <summary>How many <see cref="MeshInstance3D"/> in the subtree carry a material with an albedo
    /// texture — the glTF importer hands each surface back a <see cref="StandardMaterial3D"/>.</summary>
    private static int CountTexturedMeshes(Node node)
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

    private static void DamageStages(TestContext ctx)
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

    private static void DamageHd(TestContext ctx)
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

    private static void TexDropIn(TestContext ctx)
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

    private static void DestructibleCensus(TestContext ctx)
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
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over an airframe's <c>rig.json</c> applied to the built plane. It reads the
/// markers the loadout binds and the mounted ordnance rides, the prop and surface overrides the
/// animators turn, and the flare choice. It also reads the loud refusal of a name the model lacks.
/// Each rig sits in a pack folder of its own under the scratch directory, and
/// <see cref="HdModels.Current"/> is restored after every build.</summary>
internal static class PlaneRigSuites
{
    // Metres and degrees: the patch is a frame conversion, so only float rounding separates the two
    // sides of each comparison.
    private const float PosTolerance = 1e-3f;
    private const float AngleToleranceDeg = 0.5f;

    // The two spins the suite reads, each over half a second. An unclassified node turns at this
    // invented rate, and the stock prop at its own.
    private const float SpinDegPerSec = 90f;
    private const float SpinSeconds = 0.5f;

    [Suite("plane-rig",
        "an airframe's rig.json from a model pack moves pylon1 and firepoint1, and the bound " +
        "loadout's pylon, its mounted ordnance body and its muzzle midpoint follow; it adds a " +
        "missing reference marker and moves cockpit_camera, read after the patch; a rig prop spins " +
        "about its own plane-frame axis, an unclassified one included; a rig surface deflects about " +
        "its plane-frame hinge; a rig flare side replaces the original flare on that side by " +
        "position, and the fuel leak's wing_flare2 darkens the rig flare standing for it; the wing " +
        "split and every pylon's side are unchanged; a rig naming a node the model lacks fails the " +
        "build with an error naming the file and the entry; and on a player's launch the same rig " +
        "posts one notice and builds the original airframe, none of its entries applied")]
    internal static void PlaneRigSuite(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        using var textures = new TextureArchive(texturesPath);
        string plane = ctx.PlaneName;
        string gunId = ctx.SyntheticData ? "wep_probe_gun" : weapons.All.First(w => w.IsGun).Id;
        string ordnanceId = ctx.SyntheticData ? "wep_probe_torpedo" : Loadout.StockOrdnance;
        var fullFit = new HardpointSpec { Count = 8, Stock = Enumerable.Repeat(ordnanceId, 8).ToArray() };
        var portBefore = Loadout.WingPylons(fullFit, port: true);
        var starboardBefore = Loadout.WingPylons(fullFit, port: false);

        // --- the original's positions, built with no rig at all -------------------------------
        Vector3 pylon1, firepoint1, firepoint2, camera;
        string surfaceName, spinName;
        Vector3 surfaceOrigin;
        bool hasProp1;
        using (var none = HdModels.Open(ctx.ScratchDir, "plane-rig-no-pack"))
        {
            var model = Build(ctx, planesGamez, textures, none, plane, out var builder);
            try
            {
                pylon1 = MarkerAt(model, "pylon1");
                firepoint1 = MarkerAt(model, "firepoint1");
                firepoint2 = MarkerAt(model, "firepoint2");
                camera = builder.CockpitCameraOffset;
                var meshes = PlainMeshNodes(model);
                ctx.Check(meshes.Count >= 2, $"{plane} carries two plain mesh nodes to rig count={meshes.Count}");
                if (meshes.Count < 2)
                {
                    return;
                }

                surfaceName = AnimRuntime.NameOf(meshes[^1]);
                spinName = AnimRuntime.NameOf(meshes[^2]);
                surfaceOrigin = PlaneRigPatch.PlaneFrame(meshes[^1], model).Origin;
                hasProp1 = Named(model, "prop1").Any(n => PropParts.Classify(n.Name) == PropParts.Kind.PropMain);
            }
            finally
            {
                model.Free();
            }
        }

        // --- the rig: moved markers, an added marker, two props and a surface ------------------
        var rigPylon1 = pylon1 + new Vector3(-1.0f, -0.2f, 0.5f);
        var rigFirepoint1 = firepoint1 + new Vector3(-0.5f, 0.25f, -0.75f);
        var rigCamera = camera + new Vector3(0f, 0.1f, -0.2f);
        var rigLadder = new Vector3(0f, -1f, 1.5f);
        var spinAxis = new Vector3(1f, 0.5f, 0f);
        var hingeOrigin = surfaceOrigin + new Vector3(0f, 0.5f, 1f);
        var hingeAxis = new Vector3(0f, 1f, 0.25f);
        string prop1Entry = hasProp1 ? ", { \"node\": \"prop1\", \"axis\": [0, 1, 0] }" : string.Empty;
        string rig = $$"""
            {
              "markers": {
                "pylon1": {{Json(rigPylon1)}},
                "firepoint1": {{Json(rigFirepoint1)}},
                "cockpit_camera": {{Json(rigCamera)}},
                "ladder_pos": {{Json(rigLadder)}}
              },
              "props": [
                { "node": "{{spinName}}", "axis": {{Json(spinAxis)}}, "deg_per_sec": {{Num(SpinDegPerSec)}} }{{prop1Entry}}
              ],
              "surfaces": [
                { "node": "{{surfaceName}}", "kind": "rudder",
                  "hinge": { "origin": {{Json(hingeOrigin)}}, "axis": {{Json(hingeAxis)}} } }
              ]
            }
            """;
        using (var pack = Pack(ctx, "moved", plane, rig))
        {
            var model = Build(ctx, planesGamez, textures, pack, plane, out var builder);
            ProjectilePool? pool = null;
            try
            {
                ctx.Check(MarkerAt(model, "pylon1").DistanceTo(rigPylon1) < PosTolerance,
                    $"pylon1 stands at the rig's position {MarkerAt(model, "pylon1")} vs {rigPylon1}");
                ctx.Check(MarkerAt(model, "ladder_pos").DistanceTo(rigLadder) < PosTolerance,
                    $"ladder_pos stands at the rig's position, added where the model lacks it");
                ctx.Check(builder.CockpitCameraOffset.DistanceTo(rigCamera) < PosTolerance,
                    $"the cockpit camera offset is read after the patch {builder.CockpitCameraOffset} vs {rigCamera}");

                var def = new LoadoutDef
                {
                    Def = "rigprobe",
                    Model = plane,
                    Display = "Rig Probe",
                    Guns = { new GunSpec { Slot = 1, Mount = "Rig Guns", WeaponId = gunId, Markers = { "firepoint1", "firepoint2" } } },
                    Hardpoints = new HardpointSpec { Count = 1, Stock = new[] { ordnanceId } },
                };
                var loadout = Loadout.Bind(def, model, weapons);
                var hp = loadout.Hardpoints.Single();
                var boundPylon = PlaneRigPatch.PlaneFrame(hp.Pylon, model).Origin;
                ctx.Check(hp.Index == 1 && boundPylon.DistanceTo(rigPylon1) < PosTolerance,
                    $"the bound hardpoint is pylon{hp.Index}, at the moved position {boundPylon}");
                var muzzles = loadout.Guns.Single().Muzzles;
                var midpoint = muzzles.Aggregate(Vector3.Zero, (a, m) => a + PlaneRigPatch.PlaneFrame(m, model).Origin) / muzzles.Count;
                var expected = (rigFirepoint1 + firepoint2) / 2f;
                ctx.Check(midpoint.DistanceTo(expected) < PosTolerance && midpoint.DistanceTo((firepoint1 + firepoint2) / 2f) > 0.1f,
                    $"the gun group's muzzle midpoint follows the moved firepoint1 {midpoint} vs {expected}");

                // The mounted body is the round's own flyout prototype, which the synthetic templates carry.
                EffectStageSuiteHelper.WithAnimSource(ctx, "C1", source =>
                {
                    pool = new ProjectilePool(textures, null, null,
                        flyoutGamez: source.Gamez, flyoutScene: source.Scene, flyoutAnims: source.Program);
                    ctx.Host.AddChild(pool);
                    var ordnance = PylonOrdnance.Build(loadout, pool, infiniteAmmo: true);
                    var body = hp.Pylon.GetChildren().OfType<Node3D>().FirstOrDefault(n => n.Name.ToString() == "ordnance1");
                    var at = body == null ? Vector3.Inf : PlaneRigPatch.PlaneFrame(body, model).Origin;
                    ctx.Check(ordnance?.Count == 1 && at.DistanceTo(rigPylon1) < PosTolerance,
                        $"the mounted {ordnanceId} body sits at the moved pylon {at}");
                });

                var full = Loadout.ForRig(model, weapons, def);
                ctx.Check(full.Hardpoints.Count == 8 && full.Hardpoints.All(h =>
                        (h.Index % 2 == 1) == (PlaneRigPatch.PlaneFrame(h.Pylon, model).Origin.X < 0f)),
                    $"all {full.Hardpoints.Count} pylons keep their side, odd to port");
                ctx.Check(Loadout.WingPylons(fullFit, port: true).SequenceEqual(portBefore)
                          && Loadout.WingPylons(fullFit, port: false).SequenceEqual(starboardBefore),
                    $"the hangar's wing split is unchanged");

                CheckSpins(ctx, model, spinName, spinAxis, hasProp1);
                CheckSurface(ctx, model, surfaceName, hingeOrigin, hingeAxis);
            }
            finally
            {
                model.Free();
                pool?.Free();
            }
        }

        CheckFlares(ctx, planesGamez, textures, plane, pylon1);
        CheckLeakFlare(ctx, planesGamez, textures);
        CheckRefused(ctx, planesGamez, textures, plane, spinName);
        CheckPlayerFallback(ctx, planesGamez, textures, plane, pylon1, camera);
    }

    // A body cockpit read from two panes, the second standing for another pilot. Able to fail: a hide
    // through Visible takes the hide node out of the second pane too. A section that never reaches the
    // bind leaves the body hidden and the interior drawn, as cockpit1's view does.
    [Suite("plane-rig-cockpit-body",
        "a rig.json cockpit section with from body reaches PlaneBuilder.Cockpit and the pilot's " +
        "visibility bind: in the Cockpit view the pilot's own pane draws the body but none of the " +
        "hide node and none of the interior, a second pane still draws the hide node, nothing is " +
        "hidden through Visible, Nose still drops the body, and an external pose draws all of it")]
    internal static void PlaneRigCockpitBody(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        using var textures = new TextureArchive(texturesPath);
        string plane = ctx.PlaneName;

        // The hide node is a plain leaf of the body, read off a build with no pack.
        string? hide;
        using (var none = HdModels.Open(ctx.ScratchDir, "plane-rig-no-pack"))
        {
            var bare = Build(ctx, planesGamez, textures, none, plane, out _, cockpitInterior: true);
            hide = Named(bare, "healthy").Select(b => PlainMeshNodes(b).FirstOrDefault()).FirstOrDefault() is { } leaf
                ? AnimRuntime.NameOf(leaf)
                : null;
            bare.Free();
        }

        ctx.Check(hide != null, $"{plane}'s healthy body carries a plain mesh leaf to hide");
        if (hide == null)
        {
            return;
        }

        using var pack = Pack(ctx, "cockpit-body", plane, $$"""{ "cockpit": { "from": "body", "hide": ["{{hide}}"] } }""");
        var model = Build(ctx, planesGamez, textures, pack, plane, out var builder, cockpitInterior: true);
        var own = new Camera3D { Name = "pilot_pane", CullMask = SplitScreen.PlayerCullMask(0) };
        var other = new Camera3D { Name = "other_pane", CullMask = SplitScreen.OwnViewCullMask(SplitScreen.PlayerCullMask(1), 1) };
        try
        {
            ctx.Check(builder.Cockpit is { FromBody: true } section && section.Hide.Contains(hide),
                $"PlaneBuilder.Cockpit carries the applied rig's body section hiding '{hide}'");
            var view = CockpitVisibility.Bind(model, builder.CockpitInterior, 0, cockpit: builder.Cockpit);
            SplitScreen.SeatAirframe(model, own, 0);
            var interior = builder.CockpitInterior;
            var body = Named(model, "healthy").FirstOrDefault();
            var hidden = Named(model, hide).Where(n => interior?.IsAncestorOf(n) != true).ToList();
            ctx.Check(view is { BodyCockpit: true } && body != null && hidden.Count > 0,
                $"the bind is a body cockpit over the body and {hidden.Count} '{hide}' node(s)");
            if (view == null || body == null || hidden.Count == 0)
            {
                return;
            }

            view.Apply(PilotViewMode.Cockpit, firstPerson: true);
            var (shown, drawn) = WorldAndToolSuites.DrawnBy(body, own.CullMask);
            int listed = hidden.Sum(n => WorldAndToolSuites.DrawnBy(n, own.CullMask).Shown);
            ctx.Check(hidden.All(n => WorldAndToolSuites.DrawsNone(n, own.CullMask)),
                $"Cockpit: the pilot's own pane leaves out the hide node");
            ctx.Check(drawn > 0 && drawn + listed == shown,
                $"…and draws the rest of the body, {drawn} of {shown} drawables with {listed} hidden");
            ctx.Check(WorldAndToolSuites.DrawsNone(interior, own.CullMask),
                $"…and none of the cockpit1 interior");
            ctx.Check(WorldAndToolSuites.DrawsAll(body, other.CullMask) && hidden.All(n => WorldAndToolSuites.DrawsAll(n, other.CullMask)),
                $"a second pane draws the whole body, the hide node included");
            ctx.Check(body.Visible && hidden.All(n => n.Visible), $"nothing is hidden through Visible, which every pane shares");

            view.Apply(PilotViewMode.Nose, firstPerson: true);
            ctx.Check(WorldAndToolSuites.DrawsNone(body, own.CullMask) && WorldAndToolSuites.DrawsNone(interior, own.CullMask),
                $"Nose is unchanged: the body and the interior leave the pilot's pane");
            view.Apply(PilotViewMode.Cockpit, firstPerson: false);
            ctx.Check(WorldAndToolSuites.DrawsAll(body, own.CullMask) && WorldAndToolSuites.DrawsNone(interior, own.CullMask),
                $"an external pose draws the whole body again, the hide node included");
        }
        finally
        {
            model.Free();
            own.Free();
            other.Free();
        }
    }

    // A body cockpit's transplanted panel against the same airframe's stock cockpit1. Able to fail: a
    // rebuilt dial outside InteriorMaterials binds no belts or zones. A panel left on the airframe's
    // own layer draws in the second pane.
    [Suite("plane-rig-cockpit-gauges",
        "a body cockpit's gauge table moves the named assemblies and the cut-loose sight glass onto the " +
        "airframe at the rig's poses, the moved speedometer's needle takes GaugeCluster's angle, the bind " +
        "finds the stock cockpit1's needles, lamps, readouts, belts and zones, the dials keep the interior's " +
        "own materials and depth bias, the panel draws in the pilot's own Cockpit view alone, and a table " +
        "naming no gauge binds no panel")]
    internal static void PlaneRigCockpitGauges(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequirePlane("speedometer");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        using var textures = new TextureArchive(texturesPath);
        string plane = ctx.PlaneName;

        // The stock interior: its bind, and each assembly's plane-frame pose as the flat view shows it.
        CockpitGauges? stock;
        var poses = new Dictionary<string, Transform3D>();
        using (var none = HdModels.Open(ctx.ScratchDir, "plane-rig-no-pack"))
        {
            var bare = Build(ctx, planesGamez, textures, none, plane, out var bareBuilder, cockpitInterior: true);
            try
            {
                stock = CockpitGauges.Bind(bareBuilder);
                var interior = bareBuilder.CockpitInterior!;
                var gauges = Named(interior, "gauges").First();
                foreach (var assembly in gauges.GetChildren().OfType<Node3D>())
                {
                    poses[AnimRuntime.NameOf(assembly)] = PlaneRigPatch.PlaneFrame(assembly, bare);
                }

                var speedo = Named(interior, "speedometer").First();
                var carried = PlaneBuilder.InteriorToPlane(PlaneRigPatch.PlaneFrame(speedo, interior), bareBuilder.CockpitCameraOffset);
                ctx.Check(carried.IsEqualApprox(PlaneRigPatch.PlaneFrame(speedo, bare)),
                    $"InteriorToPlane carries the speedometer to where the built mount puts it {carried.Origin} vs {PlaneRigPatch.PlaneFrame(speedo, bare).Origin}");
            }
            finally
            {
                bare.Free();
            }
        }

        ctx.Check(stock is { NeedleCount: > 0 } && poses.ContainsKey("speedometer"),
            $"{plane}'s stock cockpit1 binds {stock?.NeedleCount} needles over {poses.Count} assemblies");
        if (stock == null || !poses.ContainsKey("speedometer"))
        {
            return;
        }

        // Every assembly where the original pilot saw it, but the speedometer moved, turned and scaled,
        // and the glass. The glass key and the assembly holding the glass are never both named.
        var speedAt = poses["speedometer"].Origin + new Vector3(0.3f, 0.1f, -0.2f);
        var speedPose = new RigGaugePose(speedAt, new Vector3(10f, -20f, 0f), 0.06f);
        var glassAt = new Vector3(0.1f, 0.2f, -0.5f) + poses["speedometer"].Origin;
        var entries = poses.Keys.Where(n => n is not ("g730" or "g184")).Select(n => n == "speedometer"
            ? $"\"speedometer\": {{ \"position\": {Json(speedAt)}, \"rotation\": [10, -20, 0], \"scale\": 0.06 }}"
            : $"\"{n}\": {{ \"position\": {Json(poses[n].Origin)} }}").Append($"\"glass\": {{ \"position\": {Json(glassAt)} }}");
        using var pack = Pack(ctx, "cockpit-gauges", plane, $$"""{ "cockpit": { "from": "body", "gauges": { {{string.Join(", ", entries)}} } } }""");
        var model = Build(ctx, planesGamez, textures, pack, plane, out var builder, cockpitInterior: true);
        var own = new Camera3D { Name = "pilot_pane", CullMask = SplitScreen.PlayerCullMask(0) };
        var other = new Camera3D { Name = "other_pane", CullMask = SplitScreen.OwnViewCullMask(SplitScreen.PlayerCullMask(1), 1) };
        GaugeCluster? cluster = null;
        try
        {
            var panel = builder.CockpitPanel;
            var speedo = panel == null ? null : Named(panel, "speedometer").FirstOrDefault();
            ctx.Check(panel is { Visible: false } && panel.GetParent() == model && speedo != null,
                $"the panel hangs hidden on the airframe and holds the speedometer");
            if (panel == null || speedo == null)
            {
                return;
            }

            ctx.Check(!Named(builder.CockpitInterior!, "speedometer").Any(), $"…which has left cockpit1");
            var want = CockpitTransplant.Posed(poses["speedometer"], speedPose);
            ctx.Check(PlaneRigPatch.PlaneFrame(speedo, model).IsEqualApprox(want),
                $"the speedometer stands at the rig's pose {PlaneRigPatch.PlaneFrame(speedo, model).Origin} scale={PlaneRigPatch.PlaneFrame(speedo, model).Basis.Scale.X:0.###}");
            var other1 = poses.Keys.First(n => n is not ("speedometer" or "g730" or "g184"));
            ctx.Check(Named(panel, other1).Select(n => PlaneRigPatch.PlaneFrame(n, model)).FirstOrDefault().IsEqualApprox(poses[other1]),
                $"a position-only {other1} keeps the size and facing cockpit1 showed it at");

            var panelBind = CockpitGauges.Bind(builder);
            ctx.Check(panelBind != null && (panelBind.NeedleCount, panelBind.LampCount, panelBind.ReadoutCount, panelBind.BeltCount, panelBind.DamageZoneCount)
                    == (stock.NeedleCount, stock.LampCount, stock.ReadoutCount, stock.BeltCount, stock.DamageZoneCount),
                $"the bind finds what the stock cockpit1's does: needles {panelBind?.NeedleCount}/{stock.NeedleCount} lamps {panelBind?.LampCount}/{stock.LampCount} readouts {panelBind?.ReadoutCount}/{stock.ReadoutCount} belts {panelBind?.BeltCount}/{stock.BeltCount} zones {panelBind?.DamageZoneCount}/{stock.DamageZoneCount}");
            if (panelBind != null && Named(panel, "speed").FirstOrDefault() is { } needle)
            {
                cluster = new GaugeCluster { SpeedMph = 200f };
                panelBind.Apply(cluster);
                float wantSpeed = -Mathf.DegToRad(GaugeCluster.SpeedAngleDeg(200f));
                float gotSpeed = needle.Transform.Basis.GetEuler().Z;
                ctx.Check(Mathf.Abs(Mathf.AngleDifference(gotSpeed, wantSpeed)) < 0.01f,
                    $"the moved speed needle takes GaugeCluster's angle got={gotSpeed:0.000} want={wantSpeed:0.000} rad");
                CheckDialLayers(ctx, builder, speedo, needle);
            }
            else
            {
                ctx.Check(false, $"the moved speedometer carries its speed needle");
            }

            CheckGlass(ctx, builder, panel, model, glassAt);
            CheckPanelPanes(ctx, model, builder, own, other);
        }
        finally
        {
            cluster?.Free();
            model.Free();
            own.Free();
            other.Free();
        }

        CheckUnnamed(ctx, planesGamez, textures, plane, poses["speedometer"].Origin);
    }

    // The dial keeps the interior's own materials and takes no shadow. Its layers keep the order the
    // bias gives them at any scale, since the bias is a fraction of view distance.
    private static void CheckDialLayers(TestContext ctx, PlaneBuilder builder, Node3D face, Node3D needle)
    {
        var names = builder.InteriorMaterials.ToDictionary(m => m.Material.GetInstanceId(), m => m.TextureName);
        ShaderMaterial? Wearing(Node3D node, string texture) => node.FindChildren("*", "MeshInstance3D", recursive: true, owned: false)
            .OfType<MeshInstance3D>().Prepend(node as MeshInstance3D).OfType<MeshInstance3D>()
            .SelectMany(mi => Enumerable.Range(0, mi.Mesh?.GetSurfaceCount() ?? 0).Select(mi.GetActiveMaterial))
            .OfType<ShaderMaterial>()
            .FirstOrDefault(m => names.TryGetValue(m.GetInstanceId(), out string? t) && t.StartsWith(texture, StringComparison.OrdinalIgnoreCase));
        var faceMaterial = Wearing(face, "speedometer");
        var needleMaterial = Wearing(needle, "needle");
        ctx.Check(faceMaterial != null && needleMaterial != null,
            $"the moved face and needle wear the interior's own speedometer and needle materials");
        float faceBias = faceMaterial?.GetShaderParameter("depth_bias").AsSingle() ?? 0f;
        float needleBias = needleMaterial?.GetShaderParameter("depth_bias").AsSingle() ?? 0f;
        ctx.Check(faceBias > 0f && needleBias > faceBias,
            $"the needle's depth bias stands over the face's, a fraction of view distance and so scale-free face={faceBias:0.####} needle={needleBias:0.####}");
        string code = faceMaterial?.Shader?.Code ?? string.Empty;
        ctx.Check(code.Contains("shadows_disabled", StringComparison.Ordinal),
            $"the dial receives no shadow, so the airframe's over its dashboard cannot dim it: {code.Split('\n').FirstOrDefault(l => l.StartsWith("render_mode", StringComparison.Ordinal))}");
    }

    // The glass is a mesh of its own, wearing the interior's glass material, centred where the rig puts it.
    private static void CheckGlass(TestContext ctx, PlaneBuilder builder, Node3D panel, Node3D model, Vector3 glassAt)
    {
        var glass = panel.GetChildren().OfType<Node3D>().FirstOrDefault(n => n.Name.ToString() == CockpitTransplant.GlassName);
        var meshes = glass?.GetChildren().OfType<MeshInstance3D>().ToList() ?? new List<MeshInstance3D>();
        var glassIds = builder.InteriorMaterials.Where(m => GunsightReticle.IsGlass(m.TextureName)).Select(m => m.Material.GetInstanceId()).ToHashSet();
        ctx.Check(glass != null && PlaneRigPatch.PlaneFrame(glass, model).Origin.DistanceTo(glassAt) < PosTolerance,
            $"the glass stands at the rig's position {(glass == null ? "none" : PlaneRigPatch.PlaneFrame(glass, model).Origin)} vs {glassAt}");
        ctx.Check(meshes.Count > 0 && meshes.All(m => m.Mesh.GetSurfaceCount() == 1 && glassIds.Contains(m.Mesh.SurfaceGetMaterial(0).GetInstanceId())),
            $"…as {meshes.Count} cut-loose mesh(es), each one glass surface wearing the interior's glass material");
        var box = meshes.Count == 0 ? new Aabb() : PlaneRigPatch.PlaneFrame(meshes[0], model) * meshes[0].GetAabb();
        ctx.Check(box.Grow(1e-3f).HasPoint(glassAt), $"…with the glass's centre at that position, box {box}");
    }

    // The panel draws in the pilot's own Cockpit view alone: not in a second pane, a picture, Nose or outside.
    private static void CheckPanelPanes(TestContext ctx, Node3D model, PlaneBuilder builder, Camera3D own, Camera3D other)
    {
        var view = CockpitVisibility.Bind(model, builder.CockpitInterior, 0, cockpit: builder.Cockpit, panel: builder.CockpitPanel);
        SplitScreen.SeatAirframe(model, own, 0);
        var panel = builder.CockpitPanel!;
        if (view == null)
        {
            ctx.Check(false, $"the visibility bind takes the body cockpit");
            return;
        }

        uint single = SplitScreen.OwnViewCullMask(SplitScreen.PaneCullMask(ZoneGate.OpenCullMask(0xFFFFFu)), 0);
        uint disc = SpyglassView.DiscMask(own.CullMask, SplitScreen.OwnAirframeLayer(0), enhanced: true);
        uint photo = Flight.Modes.DangerZonePhotograph.PhotoMask(own.CullMask);
        view.Apply(PilotViewMode.Cockpit, firstPerson: true);
        ctx.Check(WorldAndToolSuites.DrawsAll(panel, own.CullMask) && WorldAndToolSuites.DrawsAll(panel, single),
            $"Cockpit: the pilot's own pane draws the panel, split or alone {WorldAndToolSuites.DrawnBy(panel, own.CullMask)}");
        ctx.Check(WorldAndToolSuites.DrawsNone(panel, other.CullMask) && WorldAndToolSuites.DrawsNone(panel, disc) && WorldAndToolSuites.DrawsNone(panel, photo),
            $"…and a second pane, the spyglass disc and the Danger Zone photograph draw none of it");
        ctx.Check(!builder.CockpitInterior!.Visible, $"…while cockpit1 stays hidden");
        view.Apply(PilotViewMode.Nose, firstPerson: true);
        ctx.Check(!panel.Visible, $"Nose hides the panel");
        view.Apply(PilotViewMode.Cockpit, firstPerson: false);
        ctx.Check(!panel.Visible, $"an external pose hides the panel");
    }

    // An assembly the table leaves out stays in the hidden interior; a table naming none binds no panel.
    private static void CheckUnnamed(TestContext ctx, GameZ planesGamez, TextureArchive textures, string plane, Vector3 speedAt)
    {
        foreach (var (label, gauges) in new[] { ("cockpit-one-gauge", $$"""{ "speedometer": { "position": {{Json(speedAt)}} } }"""), ("cockpit-no-gauge", "{}") })
        {
            using var pack = Pack(ctx, label, plane, $$"""{ "cockpit": { "from": "body", "gauges": {{gauges}} } }""");
            var model = Build(ctx, planesGamez, textures, pack, plane, out var builder, cockpitInterior: true);
            try
            {
                var panel = builder.CockpitPanel;
                var bind = CockpitGauges.Bind(builder);
                int moved = panel == null ? -1 : Named(panel, "gauges").Sum(g => g.GetChildCount());
                bool glass = panel != null && Named(panel, CockpitTransplant.GlassName).Any();
                if (label == "cockpit-one-gauge")
                {
                    ctx.Check(moved == 1 && !glass && Named(builder.CockpitInterior!, "altimeter").Any() && bind is { NeedleCount: 1 },
                        $"naming only the speedometer moves it alone, no glass, the altimeter left in cockpit1, needles={bind?.NeedleCount}");
                }
                else
                {
                    ctx.Check(panel != null && panel.GetChildCount() == 0 && bind == null,
                        $"a body cockpit naming no gauge carries an empty panel and binds none, so the screen-space dials stay");
                }
            }
            finally
            {
                model.Free();
            }
        }
    }

    // The fuel leak darkens wing_flare2 by name. A rig flare standing for it is the lamp that goes
    // dark, and with no rig wing_flare2 itself is.
    private static void CheckLeakFlare(TestContext ctx, GameZ planesGamez, TextureArchive textures)
    {
        string flarePlane = Tooling.SyntheticPlane.Fighter;
        if (planesGamez.FindByName(flarePlane) == null)
        {
            return;
        }

        string side, standIn;
        using (var none = HdModels.Open(ctx.ScratchDir, "plane-rig-no-pack"))
        {
            var bare = Build(ctx, planesGamez, textures, none, flarePlane, out var bareBuilder);
            try
            {
                side = MarkerAt(bare, "wing_flare2").X < 0f ? "left" : "right";
                var plain = PlainMeshNodes(bare);
                ctx.Check(plain.Count > 0, $"{flarePlane} carries a plain mesh node to stand in for a flare");
                if (plain.Count == 0)
                {
                    return;
                }

                standIn = AnimRuntime.NameOf(plain[0]);
                CheckLeak(ctx, bareBuilder, "wing_flare2", "with no rig");
            }
            finally
            {
                bare.Free();
            }
        }

        using var pack = Pack(ctx, "leak-flare", flarePlane, $$"""{ "flares": { "{{side}}": "{{standIn}}" } }""");
        var model = Build(ctx, planesGamez, textures, pack, flarePlane, out var builder);
        try
        {
            CheckLeak(ctx, builder, standIn, $"with {standIn} rigged as the {side} flare");
        }
        finally
        {
            model.Free();
        }
    }

    private static void CheckLeak(TestContext ctx, PlaneBuilder builder, string dark, string what)
    {
        var blinker = WingLightBlinker.Build(builder.WingFlares, AnimRuntime.HighLod);
        blinker?.Suspend("wing_flare2");
        blinker?.Advance(0.001);
        var lit = builder.WingFlares.Where(f => f.Visible).Select(AnimRuntime.NameOf).ToList();
        var unlit = builder.WingFlares.Where(f => !f.Visible).Select(AnimRuntime.NameOf).ToList();
        ctx.Check(lit.Count > 0 && unlit.Count > 0 && unlit.All(n => n == dark) && lit.All(n => n != dark),
            $"{what}, the fuel leak's wing_flare2 darkens {dark}: lit [{string.Join(",", lit)}], dark [{string.Join(",", unlit)}]");
    }

    // A player's launch reports a bad rig once and builds the original airframe. The valid entries
    // ahead of the bad one change nothing; with no notice set the same rig fails the build.
    private static void CheckPlayerFallback(TestContext ctx, GameZ planesGamez, TextureArchive textures, string plane, Vector3 pylon1, Vector3 camera)
    {
        string json = $$"""
            { "markers": { "pylon1": {{Json(pylon1 + new Vector3(-1f, 0f, 0f))}}, "cockpit_camera": [0, 5, 0] },
              "surfaces": [ { "node": "no_such_node", "kind": "rudder" } ] }
            """;
        using var pack = Pack(ctx, "player-fallback", plane, json);
        string? error = null;
        try
        {
            Build(ctx, planesGamez, textures, pack, plane, out _).Free();
        }
        catch (InvalidDataException e)
        {
            error = e.Message;
        }

        ctx.Check(error?.Contains("surfaces[0] (no_such_node)", StringComparison.Ordinal) == true,
            $"with no player notice set, the rig fails the build: {error ?? "no error"}");

        var notices = new List<string>();
        PlaneRigPatch.PlayerNotice = notices.Add;
        Node3D? model = null;
        try
        {
            model = Build(ctx, planesGamez, textures, pack, plane, out var builder);
            ctx.Check(notices.Count == 1 && notices[0].Contains(plane, StringComparison.Ordinal),
                $"a player's launch posts one notice naming {plane}: [{string.Join(" | ", notices)}]");
            ctx.Check(MarkerAt(model, "pylon1").DistanceTo(pylon1) < PosTolerance && builder.CockpitCameraOffset.DistanceTo(camera) < PosTolerance,
                $"it builds the original airframe, the rig's moved pylon1 and camera left out {MarkerAt(model, "pylon1")} vs {pylon1}");
        }
        catch (InvalidDataException e)
        {
            ctx.Check(false, $"a player's launch builds {plane} without the bad rig: {e.Message}");
        }
        finally
        {
            PlaneRigPatch.PlayerNotice = null;
            model?.Free();
        }
    }

    private static void CheckSpins(TestContext ctx, Node3D model, string spinName, Vector3 spinAxis, bool hasProp1)
    {
        var spinNode = Named(model, spinName).First();
        var prop1 = hasProp1 ? Named(model, "prop1").First() : null;
        var spinBefore = PlaneRigPatch.PlaneFrame(spinNode, model).Basis;
        var propBefore = prop1 == null ? Basis.Identity : PlaneRigPatch.PlaneFrame(prop1, model).Basis;
        var props = PropAnimator.Build(model);
        props?.Advance(SpinSeconds, 1f);

        var (spinKept, spinDeg) = Turn(spinBefore, PlaneRigPatch.PlaneFrame(spinNode, model).Basis, spinAxis);
        ctx.Check(spinKept && Mathf.Abs(spinDeg - (SpinDegPerSec * SpinSeconds)) < AngleToleranceDeg,
            $"the unclassified {spinName} spins about the rig's axis by {spinDeg:0.##} deg");
        if (prop1 != null)
        {
            var (propKept, propDeg) = Turn(propBefore, PlaneRigPatch.PlaneFrame(prop1, model).Basis, Vector3.Up);
            float stock = PropParts.Spin(PropParts.Kind.PropMain, out _, out float rate) ? Mathf.Abs(rate * SpinSeconds) : 0f;
            ctx.Check(propKept && Mathf.Abs(propDeg - stock) < AngleToleranceDeg,
                $"prop1 spins about the rig's vertical axis at its own rate, {propDeg:0.##} deg of {stock:0.##}");
        }
    }

    private static void CheckSurface(TestContext ctx, Node3D model, string surfaceName, Vector3 hingeOrigin, Vector3 hingeAxis)
    {
        var surface = Named(model, surfaceName).First();
        var before = PlaneRigPatch.PlaneFrame(surface, model);
        var pivot = before.AffineInverse() * hingeOrigin;
        var animator = ControlSurfaceAnimator.Build(model);
        animator?.Advance(5.0, new FlightInput { Yaw = 1f }, animate: true);
        var after = PlaneRigPatch.PlaneFrame(surface, model);
        var (axisKept, deg) = Turn(before.Basis, after.Basis, hingeAxis);
        ctx.Check(deg > 1f && axisKept, $"the rig's {surfaceName} rudder deflects {deg:0.##} deg about the rig's hinge axis");
        ctx.Check((after * pivot).DistanceTo(hingeOrigin) < PosTolerance && after.Origin.DistanceTo(before.Origin) > PosTolerance,
            $"it turns about the rig's hinge point, which stays put while its origin moves");
    }

    // The flare a rig names for one side replaces the original flare on that side by plane-frame x.
    // A rig for one airframe leaves every other airframe as built.
    private static void CheckFlares(TestContext ctx, GameZ planesGamez, TextureArchive textures, string plane, Vector3 pylon1)
    {
        string flarePlane = Tooling.SyntheticPlane.Fighter;
        if (planesGamez.FindByName(flarePlane) == null)
        {
            ctx.Check(false, $"{flarePlane} is in the planes gamez for the flare check");
            return;
        }

        using var pack = Pack(ctx, "flares", flarePlane, """{ "flares": { "left": "wing_flare2" } }""");
        var model = Build(ctx, planesGamez, textures, pack, flarePlane, out var builder);
        var other = Build(ctx, planesGamez, textures, pack, plane, out _);
        try
        {
            var all = model.FindChildren("*", "Node3D", recursive: true, owned: false).OfType<Node3D>()
                .Where(n => WingLights.IsFlare(n.Name)).ToList();
            var port = all.Where(n => PlaneRigPatch.PlaneFrame(n, model).Origin.X < 0f).ToList();
            ctx.Note($"{flarePlane} port flares by position: {string.Join(",", port.Select(n => n.Name.ToString()))}");
            ctx.Check(builder.WingFlares.Any(f => f.Name.ToString() == "wing_flare2"), $"the rig's left flare wing_flare2 blinks");
            ctx.Check(port.Count > 0 && port.Where(n => n.Name.ToString() != "wing_flare2").All(n => !n.Visible && !builder.WingFlares.Contains(n)),
                $"every original port flare it replaced stays dark and never blinks");
            ctx.Check(MarkerAt(other, "pylon1").DistanceTo(pylon1) < PosTolerance,
                $"{plane} under {flarePlane}'s rig keeps its own pylon1");
        }
        finally
        {
            model.Free();
            other.Free();
        }
    }

    // Each rig names one thing the model cannot honour, and the build must refuse it by file and entry.
    // A firepoint the model lacks is refused too, read on a copy of the airframe with one cut away.
    private static void CheckRefused(TestContext ctx, GameZ planesGamez, TextureArchive textures, string plane, string spinName)
    {
        var cut = GameZ.Load(ctx.PlanesGamezPath);
        string? lacking = CutFirepoint(cut, plane);
        ctx.Check(lacking != null, $"{plane} carries a firepoint to cut for the added-firepoint case");
        var cases = new List<(string Label, string Json, string Entry, GameZ Gamez)>
        {
            ("unknown-prop", """{ "props": [ { "node": "no_such_node", "axis": [0, 0, 1], "deg_per_sec": 10 } ] }""", "props[0] (no_such_node)", planesGamez),
            ("unknown-surface", """{ "surfaces": [ { "node": "no_such_node", "kind": "rudder" } ] }""", "surfaces[0] (no_such_node)", planesGamez),
            ("unknown-flare", """{ "flares": { "right": "no_such_node" } }""", "flares.right", planesGamez),
            ("unknown-marker", """{ "markers": { "no_such_marker": [0, 0, 0] } }""", "markers.no_such_marker", planesGamez),
            ("spin-unspecified", $$"""{ "props": [ { "node": "{{spinName}}", "axis": [0, 0, 1] } ] }""", $"props[0] ({spinName})", planesGamez),
        };
        if (lacking != null)
        {
            cases.Add(("added-firepoint", $$"""{ "markers": { "{{lacking}}": [0, 0, -1] } }""", $"markers.{lacking}", cut));
        }

        foreach (var (label, json, entry, gamez) in cases)
        {
            using var pack = Pack(ctx, label, plane, json);
            string file = Path.Combine(PackRoot(ctx, label), "HDModels", "pack", "planes", plane, "rig.json");
            string? error = null;
            try
            {
                Build(ctx, gamez, textures, pack, plane, out _).Free();
            }
            catch (InvalidDataException e)
            {
                error = e.Message;
            }

            ctx.Check(error != null && error.Contains(file, StringComparison.Ordinal) && error.Contains(entry, StringComparison.Ordinal),
                $"a {label} rig fails the build naming the file and '{entry}': {error ?? "no error"}");
        }
    }

    // Renames the airframe's highest firepoint out of the marker set, so the model lacks it; null when it has none.
    private static string? CutFirepoint(GameZ gamez, string plane)
    {
        var stack = new Stack<int>(gamez.FindByName(plane)?.Children ?? new List<int>());
        GameZNode? best = null;
        while (stack.Count > 0)
        {
            var node = gamez.Nodes[stack.Pop()];
            if (MarkerRig.Classify(node.Name, out var kind, out int ord) && kind == MarkerRig.MarkerKind.Firepoint
                && (best == null || (MarkerRig.Classify(best.Name, out _, out int had) && ord > had)))
            {
                best = node;
            }

            node.Children.ForEach(stack.Push);
        }

        if (best == null)
        {
            return null;
        }

        string name = best.Name;
        best.Name = name + "_cut";
        return name;
    }

    private static Node3D Build(TestContext ctx, GameZ gamez, TextureArchive textures, HdModels pack, string plane, out PlaneBuilder builder,
        bool cockpitInterior = false)
    {
        var saved = HdModels.Current;
        HdModels.Current = pack;
        try
        {
            builder = new PlaneBuilder(gamez, textures, spinningProps: true, cockpitInterior: cockpitInterior);
            var model = builder.Build(plane);
            model.Name = $"rig_{plane}";
            return model;
        }
        finally
        {
            HdModels.Current = saved;
        }
    }

    private static string PackRoot(TestContext ctx, string label) => Path.Combine(ctx.ScratchDir, "plane-rig", label);

    private static HdModels Pack(TestContext ctx, string label, string plane, string json)
    {
        string root = PackRoot(ctx, label);
        string folder = Path.Combine(root, "HDModels", "pack", "planes", plane);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "rig.json"), json);
        return HdModels.Open(root, "HDModels");
    }

    // The rotation between two plane-frame bases: whether it keeps the axis's line, and its angle.
    private static (bool AxisKept, float Deg) Turn(Basis before, Basis after, Vector3 axis)
    {
        var turn = after * before.Inverse();
        var unit = axis.Normalized();
        float deg = Mathf.RadToDeg(turn.GetRotationQuaternion().GetAngle());
        return ((turn * unit).DistanceTo(unit) < PosTolerance, Mathf.Min(deg, 360f - deg));
    }

    private static Vector3 MarkerAt(Node3D model, string name) =>
        Named(model, name).Select(n => PlaneRigPatch.PlaneFrame(n, model).Origin).DefaultIfEmpty(Vector3.Inf).First();

    private static IEnumerable<Node3D> Named(Node3D model, string name) =>
        model.FindChildren("*", "Node3D", recursive: true, owned: false).OfType<Node3D>()
            .Where(n => AnimRuntime.NameOf(n).Equals(name, StringComparison.OrdinalIgnoreCase));

    // Mesh nodes no classifier claims, leaves of the airframe a rig can turn without double motion.
    private static List<Node3D> PlainMeshNodes(Node3D model) =>
        model.FindChildren("*", "Node3D", recursive: true, owned: false).OfType<Node3D>()
            .Where(n => n.HasMeta(AnimRuntime.NameMeta) && n.Visible
                && n.GetChildren().OfType<MeshInstance3D>().Any()
                && !n.GetChildren().OfType<Node3D>().Any(c => c.HasMeta(AnimRuntime.NameMeta))
                && PropParts.Classify(n.Name) == PropParts.Kind.None
                && ControlSurfaces.Classify(n.Name) == ControlSurfaces.Kind.None
                && !WingLights.IsFlare(n.Name))
            .ToList();

    private static string Json(Vector3 v) => $"[{Num(v.X)}, {Num(v.Y)}, {Num(v.Z)}]";

    private static string Num(float f) => f.ToString("R", CultureInfo.InvariantCulture);
}

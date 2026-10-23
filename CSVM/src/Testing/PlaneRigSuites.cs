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

    // Metres: an unedited template differs from cockpit1 by the printed floats' rounding alone.
    private const float TemplateTolerance = 1e-4f;

    // The two spins the suite reads, each over half a second. An unclassified node turns at this
    // invented rate, and the stock prop at its own.
    private const float SpinDegPerSec = 90f;
    private const float SpinSeconds = 0.5f;

    // A rotated pose 20 km from the chapter origin, where float32 world coordinates step by 2 mm.
    private static readonly Vector3 FarAt = new(-16384f, 900f, 12450f);
    private static readonly Basis FarAttitude = Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.13f));

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

    // Each airframe's dumped rig copied unedited into a pack with no model, against its stock cockpit1.
    // Able to fail: a template that skips the flat mount's head-pitch tilt moves every dial off its place.
    [Suite("plane-rig-cockpit-template",
        "every player airframe's --dump-markers rig.json, copied unedited into a pack with no model, " +
        "passes its checks, makes a body cockpit, and transplants every gauge assembly and the sight glass " +
        "to the plane-frame pose the stock cockpit1 drew it at, binding what the stock cockpit1 binds; " +
        "it notes how far the stereo eye's mount puts each dial from the flat one")]
    internal static void PlaneRigCockpitTemplate(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequirePlane("speedometer");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        using var textures = new TextureArchive(texturesPath);
        // The CLI's own source, then the run's plane where the player airframe list leaves it out.
        var templates = Tooling.Probes.Markers(ctx.PlanesGamezPath, string.Empty).RigTemplates.ToList();
        if (templates.All(t => t.Model != ctx.PlaneName) && MarkerRig.Extract(planesGamez, ctx.PlaneName) is { } own)
        {
            templates.Add((ctx.PlaneName, own.FormatRigTemplate()));
        }

        int withCockpit = 0;
        foreach (var (plane, json) in templates)
        {
            if (MarkerRig.Extract(planesGamez, plane)?.Cockpit != null)
            {
                withCockpit++;
                CheckTemplate(ctx, planesGamez, textures, plane, json);
            }
        }

        ctx.Check(withCockpit > 0, $"{withCockpit} of {templates.Count} dumped airframes carry a cockpit1 and its template");
    }

    // A replaced cockpit1's table, read in interior units against the stock interior. Able to fail: a
    // moved glass left in its parent mesh draws twice. A pose read as metres misplaces the dial.
    [Suite("plane-rig-cockpit-inside",
        "without from: body, the gauge table places the named assemblies inside cockpit1 in its own frame and units: the " +
        "moved speedometer stands at the rig's pose and its needle takes GaugeCluster's angle, an unnamed assembly stays, " +
        "the bind finds the stock needles, lamps, readouts, belts and zones, and a moved glass is cut loose and draws once. " +
        "A gungauge replaced by a texture-named copy and moved by the table keeps its belt lights driven and one surface " +
        "per readout character")]
    internal static void PlaneRigCockpitInside(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequirePlane("speedometer", "gungauge");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        using var textures = new TextureArchive(texturesPath);
        string plane = ctx.PlaneName;
        string gungaugeFile = Path.Combine(PackRoot(ctx, "cockpit-inside-export"), "gungauge.glb");

        // The stock interior: its bind, each assembly's interior-frame pose, its glass and surface counts.
        CockpitGauges? stock;
        var poses = new Dictionary<string, Transform3D>();
        int stockGlass, stockSurfaces, stockCells;
        using (var none = HdModels.Open(ctx.ScratchDir, "plane-rig-no-pack"))
        {
            var bare = Build(ctx, planesGamez, textures, none, plane, out var bareBuilder, cockpitInterior: true);
            try
            {
                stock = CockpitGauges.Bind(bareBuilder);
                var interior = bareBuilder.CockpitInterior!;
                foreach (var assembly in Named(interior, "gauges").First().GetChildren().OfType<Node3D>())
                {
                    poses[AnimRuntime.NameOf(assembly)] = PlaneRigPatch.PlaneFrame(assembly, interior);
                }

                (stockGlass, stockSurfaces) = GlassAndSurfaces(bareBuilder, interior);
                stockCells = Cells(interior);
                Directory.CreateDirectory(Path.GetDirectoryName(gungaugeFile)!);
                ctx.Same((long)Error.Ok, (long)Tooling.GltfExporter.Export(Named(interior, "gungauge").First(), gungaugeFile, allStates: true),
                    $"the stock gungauge's export write result");
            }
            finally
            {
                bare.Free();
            }
        }

        ctx.Check(stock is { NeedleCount: > 0 } && stockGlass > 0 && poses.ContainsKey("speedometer") && poses.ContainsKey("gungauge"),
            $"{plane}'s stock cockpit1 binds {stock?.NeedleCount} needles over {poses.Count} assemblies with {stockGlass} glass surface(s)");
        if (stock == null || stockGlass == 0 || !poses.ContainsKey("speedometer") || !poses.ContainsKey("gungauge"))
        {
            return;
        }

        var speedPose = new RigGaugePose(poses["speedometer"].Origin + new Vector3(3f, 2f, -1f), new Vector3(10f, -20f, 0f), 1.5f);
        var glassAt = poses["speedometer"].Origin + new Vector3(-2f, 6f, 1f);
        using (var pack = Pack(ctx, "cockpit-inside", plane,
            $$"""{ "cockpit": { "gauges": { "speedometer": { "position": {{Json(speedPose.Position)}}, "rotation": [10, -20, 0], "scale": 1.5 }, "glass": { "position": {{Json(glassAt)}} } } } }"""))
        {
            var model = Build(ctx, planesGamez, textures, pack, plane, out var builder, cockpitInterior: true);
            GaugeCluster? cluster = null;
            try
            {
                var interior = builder.CockpitInterior!;
                var speedo = Named(interior, "speedometer").FirstOrDefault();
                ctx.Check(builder.CockpitPanel == null && speedo != null && Named(interior, "gauges").First().IsAncestorOf(speedo),
                    $"no panel: the speedometer stays under cockpit1's gauges node");
                var frame = speedo == null ? Transform3D.Identity : PlaneRigPatch.PlaneFrame(speedo, interior);
                ctx.Check(frame.IsEqualApprox(CockpitTransplant.Posed(poses["speedometer"], speedPose)),
                    $"the speedometer stands at the rig's pose in interior units {frame.Origin} scale={frame.Basis.Scale.X:0.###}");
                var other = poses.Keys.First(n => n is not ("speedometer" or "g730" or "g184"));
                ctx.Check(Named(interior, other).Select(n => PlaneRigPatch.PlaneFrame(n, interior)).FirstOrDefault().IsEqualApprox(poses[other]),
                    $"the unnamed {other} stays where the original put it");

                var bind = CockpitGauges.Bind(builder);
                ctx.Check(SameBind(bind, stock), $"the bind finds what the stock cockpit1's does: {Counts(bind)} vs {Counts(stock)}");
                if (bind != null && Named(interior, "speed").FirstOrDefault() is { } needle)
                {
                    cluster = new GaugeCluster { SpeedMph = 200f };
                    bind.Apply(cluster);
                    float want = -Mathf.DegToRad(GaugeCluster.SpeedAngleDeg(200f));
                    float got = needle.Transform.Basis.GetEuler().Z;
                    ctx.Check(Mathf.Abs(Mathf.AngleDifference(got, want)) < 0.01f,
                        $"the moved speed needle takes GaugeCluster's angle got={got:0.000} want={want:0.000} rad");
                }
                else
                {
                    ctx.Check(false, $"the moved speedometer carries its speed needle");
                }

                var glass = interior.GetChildren().OfType<Node3D>().FirstOrDefault(n => n.Name.ToString() == CockpitTransplant.GlassName);
                var (glassSurfaces, surfaces) = GlassAndSurfaces(builder, interior);
                int cut = glass?.GetChildren().OfType<MeshInstance3D>().Count() ?? 0;
                ctx.Check(glass != null && PlaneRigPatch.PlaneFrame(glass, interior).Origin.DistanceTo(glassAt) < PosTolerance,
                    $"the glass stands at the rig's position in interior units {(glass == null ? "none" : PlaneRigPatch.PlaneFrame(glass, interior).Origin)} vs {glassAt}");
                ctx.Check(cut == stockGlass && glassSurfaces == stockGlass && surfaces == stockSurfaces,
                    $"the glass draws once: {cut} cut-loose and {glassSurfaces} glass surfaces in all against the stock {stockGlass}, and {surfaces}/{stockSurfaces} surfaces in the interior; glass on {string.Join(" ", GlassHolders(builder, interior))}");
            }
            finally
            {
                cluster?.Free();
                model.Free();
            }
        }

        CheckReplacedGungauge(ctx, planesGamez, textures, plane, gungaugeFile, poses["gungauge"], stock, stockCells);
    }

    // A body cockpit under the stereo build, lifted as XrRig lifts it, on a rotated pose 20 km out.
    // Able to fail: a twin placed through the world's composition, or an original left on the pilot's
    // layer. A dial posed through the stereo mount instead of the rig's plane frame fails too.
    [Suite("plane-rig-cockpit-stereo",
        "under the stereo build a body cockpit's panel carries a stereo-shaded twin of every drawn body mesh outside "
        + "the hide list and cockpit1, with no head-plate cut; lifted at a rotated pose 20 km from the origin, it holds "
        + "only small numbers, each twin, the moved speedometer and the glass at its plane-frame pose relative to the eye "
        + "at cockpit_camera plus the seat offset; a turned or hidden original moves its twin on the next placement; the "
        + "pilot's pane draws the twins and none of the twinned originals or the hide node, while a second pane draws "
        + "them all; the tuning lab's scale and store fields leave the real-scale mount alone; and an external pose "
        + "lowers the panel back onto the airframe")]
    internal static void PlaneRigCockpitStereo(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequirePlane("speedometer");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        using var textures = new TextureArchive(texturesPath);
        string plane = ctx.PlaneName;
        // The stereo shader reads the eye feed's globals; declared without making the run stereo.
        XrCockpit.DeclareEyeGlobals();

        string? hide;
        Vector3 camera;
        using (var none = HdModels.Open(ctx.ScratchDir, "plane-rig-no-pack"))
        {
            var bare = Build(ctx, planesGamez, textures, none, plane, out var bareBuilder, cockpitInterior: true);
            hide = Named(bare, "healthy").Select(b => PlainMeshNodes(b).FirstOrDefault()).FirstOrDefault() is { } leaf
                ? AnimRuntime.NameOf(leaf)
                : null;
            camera = bareBuilder.CockpitCameraOffset;
            bare.Free();
        }

        ctx.Check(hide != null, $"{plane}'s healthy body carries a plain mesh leaf to hide");
        if (hide == null)
        {
            return;
        }

        var speedPose = new RigGaugePose(camera + new Vector3(-0.2f, -0.15f, -0.5f), new Vector3(-15f, 10f, 0f), 0.05f);
        var glassAt = camera + new Vector3(0f, -0.02f, -0.45f);
        using var pack = Pack(ctx, "cockpit-stereo", plane, $$"""
            { "cockpit": { "from": "body", "hide": ["{{hide}}"], "gauges": {
              "speedometer": { "position": {{Json(speedPose.Position)}}, "rotation": [-15, 10, 0], "scale": 0.05 },
              "glass": { "position": {{Json(glassAt)}} } } } }
            """);
        var model = Build(ctx, planesGamez, textures, pack, plane, out var builder, cockpitInterior: true, xrInterior: true);
        var aircraft = new Node3D { Name = "stereo-body-aircraft" };
        aircraft.AddChild(model);
        ctx.Host.AddChild(aircraft);
        var own = new Camera3D { Name = "pilot_pane", CullMask = SplitScreen.PlayerCullMask(0) };
        var other = new Camera3D { Name = "other_pane", CullMask = SplitScreen.OwnViewCullMask(SplitScreen.PlayerCullMask(1), 1) };
        try
        {
            CheckStereoBody(ctx, builder, model, aircraft, hide, speedPose, glassAt, own, other);
        }
        finally
        {
            aircraft.Free();
            own.Free();
            other.Free();
        }
    }

    // The stereo body's build, then its lift, its layers, its mirroring and its lowering, in that order.
    private static void CheckStereoBody(TestContext ctx, PlaneBuilder builder, Node3D model, Node3D aircraft, string hide,
        RigGaugePose speedPose, Vector3 glassAt, Camera3D own, Camera3D other)
    {
        var panel = builder.CockpitPanel;
        var twins = builder.StereoBody;
        ctx.Check(panel != null && twins.Count > 0 && twins.All(t => t.Twin.GetParent() == panel),
            $"the panel holds {twins.Count} stereo twin(s) of the airframe");
        if (panel == null || twins.Count == 0 || builder.CockpitInterior is not { } interior)
        {
            return;
        }

        var drawn = BodyMeshes(model, hide);
        var twinned = twins.Select(t => t.Original).ToHashSet();
        ctx.Check(twinned.SetEquals(drawn), $"every drawn body mesh outside cockpit1 and the hide list has one: {twinned.Count} of {drawn.Count}");
        var hidden = Named(model, hide).Where(n => !panel.IsAncestorOf(n) && !interior.IsAncestorOf(n)).ToList();
        ctx.Check(hidden.Count > 0 && !twinned.Any(o => hidden.Any(h => h.IsAncestorOf(o))), $"the hide node '{hide}' has none");
        ctx.Check(twins.All(t => Enumerable.Range(0, t.Twin.Mesh.GetSurfaceCount()).All(s =>
                t.Twin.Mesh.SurfaceGetMaterial(s) is ShaderMaterial { Shader: { } sh } && sh.Code.Contains(SceneBuilder.XrEyeLeftGlobal, StringComparison.Ordinal))),
            $"every twin draws through the stereo interior's placement");
        ctx.Check(builder.InteriorOmittedPolygons == 0, $"cockpit1 keeps its head plate: {builder.InteriorOmittedPolygons} polygons cut");
        int overlays = panel.GetChildren().OfType<MeshInstance3D>().Count(m => m.Name.ToString() == "gunsight_reticle");
        ctx.Check(overlays == builder.GunsightReticles && !interior.FindChildren("gunsight_reticle", "", true, false).Any(),
            $"the collimated reticle hangs on the panel, {overlays} overlay(s), none on cockpit1");
        if (panel.GetChildren().OfType<MeshInstance3D>().FirstOrDefault(m => m.Name.ToString() == "gunsight_reticle") is { MaterialOverride: ShaderMaterial reticle })
        {
            ctx.Check(reticle.GetShaderParameter("authored_eye").AsVector3().IsEqualApprox(builder.CockpitCameraOffset),
                $"read from the cockpit_camera eye in the plane frame");
        }

        var view = CockpitVisibility.Bind(model, interior, 0, cockpit: builder.Cockpit, panel: panel)!;
        SplitScreen.SeatAirframe(model, own, 0);
        var seat = new Vector3(0f, 0.02f, 0.06f);
        var stereo = XrCockpit.Body(panel, seat, builder.CockpitCameraOffset);
        foreach (var (original, twin) in twins)
        {
            stereo.Mirror(original, twin);
            view.Reseat(original);
        }

        var eye = stereo.EyeInAircraft;
        ctx.Check(eye.IsEqualApprox(builder.CockpitCameraOffset + seat), $"the eye stands at cockpit_camera plus the seat offset {eye}");
        aircraft.GlobalTransform = new Transform3D(FarAttitude, FarAt);
        var origin = new Transform3D(FarAttitude, aircraft.GlobalTransform * eye);
        view.Apply(PilotViewMode.Cockpit, firstPerson: true);
        stereo.Follow(origin, aircraft);
        ctx.Check(panel.TopLevel, $"Cockpit lifts the panel to the XR origin's frame");

        // Each relative to the eye, from small numbers: what the stereo shader draws from.
        Vector3 FromEye(Node3D node) => FarAttitude * ((model.Transform * PlaneRigPatch.PlaneFrame(node, model)).Origin - eye);
        // A panel part's pose on the airframe is its pose in the panel, which is mounted at identity.
        Vector3 PanelFromEye(Node3D node) => FarAttitude * ((model.Transform * PlaneRigPatch.PlaneFrame(node, panel)).Origin - eye);
        float worst = 0f, reach = 0f;
        foreach (var (original, twin) in twins)
        {
            var at = (panel.Transform * twin.Transform).Origin;
            worst = Mathf.Max(worst, at.DistanceTo(FromEye(original)));
            reach = Mathf.Max(reach, at.Length());
        }

        ctx.Check(worst < 1e-5f && reach < 50f, $"every twin stands at its original's offset from the eye, worst {worst * 1e6f:0.##} um, none past {reach:0.##} m");
        ctx.Note($"{twins.Count} twins of {drawn.Count} body meshes, worst offset error {worst * 1e6f:0.##} um, farthest {reach:0.##} m from the eye, {overlays} reticle overlay(s)");
        var speedo = Named(panel, "speedometer").FirstOrDefault();
        ctx.Check(speedo != null && PlaneRigPatch.PlaneFrame(speedo, panel).IsEqualApprox(CockpitTransplant.Posed(Transform3D.Identity, speedPose))
                && (panel.Transform * PlaneRigPatch.PlaneFrame(speedo, panel)).Origin.DistanceTo(PanelFromEye(speedo)) < 1e-5f,
            $"the speedometer stands at the rig's plane-frame pose, relative to the eye");
        var glass = Named(panel, CockpitTransplant.GlassName).FirstOrDefault();
        ctx.Check(glass != null && PlaneRigPatch.PlaneFrame(glass, panel).Origin.DistanceTo(glassAt) < PosTolerance,
            $"the glass stands at the rig's position");

        ctx.Check(twins.All(t => WorldAndToolSuites.DrawsNone(t.Original, own.CullMask) && WorldAndToolSuites.DrawsAll(t.Original, other.CullMask)),
            $"the pilot's pane drops every twinned original, a second pane still draws them");
        var shown = twins.Where(t => t.Twin.Visible).ToList();
        ctx.Check(shown.Count > 0 && shown.All(t => WorldAndToolSuites.DrawsAll(t.Twin, own.CullMask) && WorldAndToolSuites.DrawsNone(t.Twin, other.CullMask)),
            $"…and the pilot's pane alone draws the {shown.Count} shown twins");
        ctx.Check(twins.All(t => t.Twin.Visible == t.Original.IsVisibleInTree()), $"each twin shows exactly where its original does");
        ctx.Check(hidden.All(h => WorldAndToolSuites.DrawsNone(h, own.CullMask) && WorldAndToolSuites.DrawsAll(h, other.CullMask)),
            $"the hide node leaves the pilot's pane alone");

        if (shown.Count == 0)
        {
            return;
        }

        var (moved, movedTwin) = shown[^1];
        var part = moved.GetParent<Node3D>();
        part.RotateObjectLocal(Vector3.Right, 0.3f);
        stereo.Follow(origin, aircraft);
        ctx.Check((panel.Transform * movedTwin.Transform).Origin.DistanceTo(FromEye(moved)) < 1e-5f
                && (panel.Transform * movedTwin.Transform).Basis.IsEqualApprox(FarAttitude * (model.Transform * PlaneRigPatch.PlaneFrame(moved, model)).Basis),
            $"a turned original turns its twin on the next placement");
        part.Visible = false;
        stereo.Follow(origin, aircraft);
        ctx.Check(!movedTwin.Visible, $"a hidden original hides its twin");
        part.Visible = true;

        stereo.SetMountScale(0.05f);
        stereo.PlaceStores(new Vector3(0f, 0.1f, -0.2f), 1.5f);
        ctx.Check(Mathf.IsEqualApprox(stereo.MountScale, 1f) && stereo.EyeInAircraft.IsEqualApprox(eye),
            $"the tuning lab's scale and store fields leave the real-scale mount alone ({stereo.MountScale} m per metre)");

        view.Apply(PilotViewMode.Cockpit, firstPerson: false);
        stereo.Follow(origin, aircraft);
        ctx.Check(!panel.TopLevel && panel.Transform.IsEqualApprox(Transform3D.Identity)
                && twins.All(t => WorldAndToolSuites.DrawsAll(t.Original, own.CullMask)),
            $"an external pose lowers the panel onto the airframe and gives the originals back to the pilot's pane");
    }

    // Every drawn mesh of the body a stereo body cockpit lifts: outside cockpit*, the hide list and flares.
    private static HashSet<MeshInstance3D> BodyMeshes(Node node, string hide)
    {
        var found = new HashSet<MeshInstance3D>();
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D { MaterialOverride: null } mi && mi.Name.ToString() == "mesh" && node.HasMeta(AnimRuntime.IndexMeta))
            {
                found.Add(mi);
            }
            else if (child is Node3D n3d && !AnimRuntime.NameOf(n3d).StartsWith("cockpit", StringComparison.OrdinalIgnoreCase)
                && !AnimRuntime.NameOf(n3d).Equals(hide, StringComparison.OrdinalIgnoreCase))
            {
                found.UnionWith(BodyMeshes(n3d, hide));
            }
        }

        return found;
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

    // One airframe's template build against its stock build: each assembly's plane-frame transform, the
    // glass's plane-frame vertices and the bind. It notes the stereo mount's distance from the flat one.
    private static void CheckTemplate(TestContext ctx, GameZ planesGamez, TextureArchive textures, string plane, string json)
    {
        var poses = new Dictionary<string, Transform3D>();
        var stereoGap = new List<float>();
        List<Vector3> stockGlass;
        CockpitGauges? stock;
        using (var none = HdModels.Open(ctx.ScratchDir, "plane-rig-no-pack"))
        {
            var bare = Build(ctx, planesGamez, textures, none, plane, out var bareBuilder, cockpitInterior: true);
            try
            {
                stock = CockpitGauges.Bind(bareBuilder);
                var interior = bareBuilder.CockpitInterior!;
                var stereo = PlaneBuilder.StereoMount(bareBuilder.CockpitCameraOffset, PlaneBuilder.InteriorScale);
                foreach (var assembly in Named(interior, "gauges").First().GetChildren().OfType<Node3D>())
                {
                    poses[AnimRuntime.NameOf(assembly)] = PlaneRigPatch.PlaneFrame(assembly, bare);
                    stereoGap.Add((stereo * PlaneRigPatch.PlaneFrame(assembly, interior)).Origin.DistanceTo(poses[AnimRuntime.NameOf(assembly)].Origin));
                }

                stockGlass = GlassVertices(interior, bare, bareBuilder);
            }
            finally
            {
                bare.Free();
            }
        }

        using var pack = Pack(ctx, $"cockpit-template-{plane}", plane, json);
        Node3D model;
        PlaneBuilder builder;
        try
        {
            model = Build(ctx, planesGamez, textures, pack, plane, out builder, cockpitInterior: true);
        }
        catch (InvalidDataException e)
        {
            ctx.Check(false, $"{plane}: the dumped rig loads and passes its checks: {e.Message}");
            return;
        }

        try
        {
            var panel = builder.CockpitPanel;
            var moved = panel == null ? new List<Node3D>() : Named(panel, "gauges").Take(1).SelectMany(g => g.GetChildren().OfType<Node3D>()).ToList();
            ctx.Check(builder.Cockpit is { FromBody: true } && panel != null && moved.Count == poses.Count,
                $"{plane}: the unedited template makes a body cockpit moving {moved.Count} of {poses.Count} assemblies");
            float worstPos = 0f, worstBasis = 0f;
            foreach (var assembly in moved)
            {
                var want = poses[AnimRuntime.NameOf(assembly)];
                var got = PlaneRigPatch.PlaneFrame(assembly, model);
                worstPos = Mathf.Max(worstPos, got.Origin.DistanceTo(want.Origin));
                worstBasis = Mathf.Max(worstBasis, new[] { got.Basis.X - want.Basis.X, got.Basis.Y - want.Basis.Y, got.Basis.Z - want.Basis.Z }.Max(d => d.Length()));
            }

            ctx.Check(worstPos < TemplateTolerance && worstBasis < TemplateTolerance * PlaneBuilder.InteriorScale,
                $"{plane}: every assembly stands where cockpit1 drew it, worst {worstPos * 1000f:0.####} mm and basis {worstBasis:0.#######}");
            var glass = panel == null ? new List<Vector3>() : GlassVertices(panel, model, builder);
            float worstGlass = glass.Count == stockGlass.Count ? glass.Zip(stockGlass, (a, b) => a.DistanceTo(b)).DefaultIfEmpty(0f).Max() : float.PositiveInfinity;
            ctx.Check(stockGlass.Count > 0 && worstGlass < TemplateTolerance,
                $"{plane}: the panel draws all {glass.Count} of cockpit1's {stockGlass.Count} glass vertices where cockpit1 drew them, worst {worstGlass * 1000f:0.####} mm");
            var bind = CockpitGauges.Bind(builder);
            ctx.Check(stock != null && bind != null && (bind.NeedleCount, bind.LampCount, bind.ReadoutCount, bind.BeltCount, bind.DamageZoneCount)
                    == (stock.NeedleCount, stock.LampCount, stock.ReadoutCount, stock.BeltCount, stock.DamageZoneCount),
                $"{plane}: the bind finds what the stock cockpit1's does, needles {bind?.NeedleCount}/{stock?.NeedleCount}");
            ctx.Note($"{plane}: template off cockpit1 by {worstPos * 1000f:0.####} mm (assemblies) and {worstGlass * 1000f:0.####} mm (glass); the stereo mount puts the dials {stereoGap.Min() * 1000f:0} to {stereoGap.Max() * 1000f:0} mm from the flat mount's, eye {builder.CockpitCameraOffset}");
        }
        finally
        {
            model.Free();
        }
    }

    // The gungauge and every gamez node below it drawn from a texture-named copy, moved by the table.
    private static void CheckReplacedGungauge(TestContext ctx, GameZ planesGamez, TextureArchive textures, string plane,
        string file, Transform3D original, CockpitGauges stock, int stockCells)
    {
        var at = original.Origin + new Vector3(-4f, 1f, 0.5f);
        using var pack = Pack(ctx, "cockpit-inside-gungauge", plane, $$"""{ "cockpit": { "gauges": { "gungauge": { "position": {{Json(at)}} } } } }""");
        File.Copy(file, Path.Combine(PackRoot(ctx, "cockpit-inside-gungauge"), "HDModels", "pack", "planes", plane, "gungauge.glb"));
        using var withFile = HdModels.Open(PackRoot(ctx, "cockpit-inside-gungauge"), "HDModels");
        var model = Build(ctx, planesGamez, textures, withFile, plane, out var builder, cockpitInterior: true);
        try
        {
            var interior = builder.CockpitInterior!;
            static bool Replaced(Node3D n) => n.GetChildren().OfType<MeshInstance3D>().Any(m => m.HasMeta(SceneBuilder.GamezMeshMeta));
            var gauge = Named(interior, "gungauge").FirstOrDefault();
            var meshed = gauge == null ? new List<Node3D>() : gauge.FindChildren("*", "Node3D", recursive: true, owned: false).OfType<Node3D>().Prepend(gauge)
                .Where(n => n.HasMeta(AnimRuntime.NameMeta) && n.GetChildren().OfType<MeshInstance3D>().Any()).ToList();
            ctx.Check(meshed.Count > 0 && meshed.All(Replaced),
                $"the gungauge's {meshed.Count} meshed nodes are drawn from the pack's copy; not: {string.Join(" ", meshed.Where(n => !Replaced(n)).Select(AnimRuntime.NameOf))}");
            ctx.Check(gauge != null && PlaneRigPatch.PlaneFrame(gauge, interior).Origin.DistanceTo(at) < PosTolerance,
                $"…and moved by the table to {at} in interior units");
            var bind = CockpitGauges.Bind(builder);
            ctx.Check(bind != null && bind.ReadoutCount == stock.ReadoutCount && Cells(interior) == stockCells,
                $"its readouts bind as the stock's, one surface per character: readouts {bind?.ReadoutCount}/{stock.ReadoutCount} cells {Cells(interior)}/{stockCells}");
            WorldAndToolSuites.DrivenBelts(ctx, interior, builder, planesGamez, textures);
        }
        finally
        {
            model.Free();
        }
    }

    // Every glass surface's vertices in the plane frame, in tree and surface order, as CutGlass walks them.
    private static List<Vector3> GlassVertices(Node3D root, Node3D model, PlaneBuilder builder)
    {
        var glassIds = builder.InteriorMaterials.Where(m => GunsightReticle.IsGlass(m.TextureName)).Select(m => m.Material.GetInstanceId()).ToHashSet();
        var found = new List<Vector3>();
        foreach (var instance in root.FindChildren("*", "MeshInstance3D", recursive: true, owned: false).OfType<MeshInstance3D>())
        {
            for (int s = 0; s < (instance.Mesh?.GetSurfaceCount() ?? 0); s++)
            {
                if (instance.GetActiveMaterial(s) is { } material && glassIds.Contains(material.GetInstanceId()))
                {
                    var frame = PlaneRigPatch.PlaneFrame(instance, model);
                    found.AddRange(instance.Mesh!.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(v => frame * v));
                }
            }
        }

        return found;
    }

    // Glass-material surfaces and all surfaces drawn under the interior.
    private static (int Glass, int All) GlassAndSurfaces(PlaneBuilder builder, Node3D interior)
    {
        var glassIds = builder.InteriorMaterials.Where(m => GunsightReticle.IsGlass(m.TextureName)).Select(m => m.Material.GetInstanceId()).ToHashSet();
        var surfaces = interior.FindChildren("*", "MeshInstance3D", recursive: true, owned: false).OfType<MeshInstance3D>()
            .SelectMany(mi => Enumerable.Range(0, mi.Mesh?.GetSurfaceCount() ?? 0).Select(mi.GetActiveMaterial)).ToList();
        return (surfaces.Count(m => m != null && glassIds.Contains(m.GetInstanceId())), surfaces.Count);
    }

    private static IEnumerable<string> GlassHolders(PlaneBuilder builder, Node3D interior)
    {
        var glassIds = builder.InteriorMaterials.Where(m => GunsightReticle.IsGlass(m.TextureName)).Select(m => m.Material.GetInstanceId()).ToHashSet();
        return interior.FindChildren("*", "MeshInstance3D", recursive: true, owned: false).OfType<MeshInstance3D>()
            .Where(mi => Enumerable.Range(0, mi.Mesh?.GetSurfaceCount() ?? 0).Any(s => mi.GetActiveMaterial(s) is { } m && glassIds.Contains(m.GetInstanceId())))
            .Select(mi => $"{mi.GetParent()?.Name}/{mi.Name}({mi.Mesh.GetSurfaceCount()})");
    }

    // The readout character cells: the surfaces of each 4char_ammo and 6char_type mesh.
    private static int Cells(Node3D interior) =>
        new[] { "4char_ammo", "6char_type" }.SelectMany(n => Named(interior, n))
            .SelectMany(n => n.FindChildren("*", "MeshInstance3D", recursive: true, owned: false).OfType<MeshInstance3D>())
            .Sum(mi => mi.Mesh?.GetSurfaceCount() ?? 0);

    private static bool SameBind(CockpitGauges? a, CockpitGauges b) =>
        a != null && (a.NeedleCount, a.LampCount, a.ReadoutCount, a.BeltCount, a.DamageZoneCount)
            == (b.NeedleCount, b.LampCount, b.ReadoutCount, b.BeltCount, b.DamageZoneCount);

    private static string Counts(CockpitGauges? g) =>
        $"needles {g?.NeedleCount} lamps {g?.LampCount} readouts {g?.ReadoutCount} belts {g?.BeltCount} zones {g?.DamageZoneCount}";

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
        bool cockpitInterior = false, bool xrInterior = false)
    {
        var saved = HdModels.Current;
        HdModels.Current = pack;
        try
        {
            builder = new PlaneBuilder(gamez, textures, spinningProps: true, cockpitInterior: cockpitInterior, xrInterior: xrInterior);
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

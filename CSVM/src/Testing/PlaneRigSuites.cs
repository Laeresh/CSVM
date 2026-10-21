using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
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

    private static Node3D Build(TestContext ctx, GameZ gamez, TextureArchive textures, HdModels pack, string plane, out PlaneBuilder builder)
    {
        var saved = HdModels.Current;
        HdModels.Current = pack;
        try
        {
            builder = new PlaneBuilder(gamez, textures, spinningProps: true);
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

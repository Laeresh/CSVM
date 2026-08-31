using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Godot;

using static CSVM.Testing.SuiteConstants;
namespace CSVM.Testing;

/// <summary>Suites asserting the built world and the workbench tools: chapter data gates and
/// censuses, world lighting and viewers, and the lab surfaces.</summary>
internal static class WorldAndToolSuites
{
    // Exports a built plane to a temp .glb and asserts the file lands and re-imports with at least one
    // textured mesh: the round trip the viewer's --export-gltf=/F10 path relies on, including that the
    // shader skins convert to a glTF-serializable material.
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

    // A second aircraft build must reuse the first's Shader resources rather than generating its
    // own copies of the same text. Godot compiles a Shader the first time a material takes it, so a
    // per-builder shader memo makes every mid-flight AI spawn pay that compile again; a generated
    // aircraft is built on the frame path, where the bill lands as a stall. Able to fail: with the
    // memo back on the instance, none of the second model's shaders is one of the first's.
    internal static void PlaneShaderReuse(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        Node3D? first = null, second = null;
        try
        {
            first = new PlaneBuilder(planesGamez, textures, spinningProps: true).Build(ctx.PlaneName);
            var firstShaders = ShadersUnder(first);
            second = new PlaneBuilder(planesGamez, textures, spinningProps: true).Build(ctx.PlaneName);
            var secondShaders = ShadersUnder(second);

            // The control: a model that resolved to one shader would satisfy the reuse check
            // vacuously, and so would one carrying no ShaderMaterial at all.
            ctx.Check(firstShaders.Count > 1,
                $"the first build carries several distinct shaders plane={ctx.PlaneName} count={firstShaders.Count}");
            ctx.Check(secondShaders.Count > 0, $"the second build carries shaders count={secondShaders.Count}");

            int shared = secondShaders.Count(s => firstShaders.Contains(s));
            ctx.Same(secondShaders.Count, shared,
                $"every shader of the second build is one the first already compiled (second={secondShaders.Count} shared={shared})");
        }
        finally
        {
            first?.Free();
            second?.Free();
            textures.Dispose();
        }
    }

    // The distinct Shader resources every ShaderMaterial in the subtree points at, by reference:
    // two builds sharing one memo hand back the same instances, two builds with their own hand back
    // equal text on different resources.
    internal static HashSet<Shader> ShadersUnder(Node node)
    {
        var found = new HashSet<Shader>();
        void Walk(Node n)
        {
            if (n is MeshInstance3D mesh)
            {
                for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
                {
                    if (mesh.GetActiveMaterial(i) is ShaderMaterial { Shader: { } shader })
                    {
                        found.Add(shader);
                    }
                }
            }
            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }
        Walk(node);
        return found;
    }

    // The cockpit interior's build and its per-mode hiding. Two builds of
    // the same airframe: the default one must be byte-for-byte the exterior build (an AI plane
    // pays nothing), the cockpitInterior one must gain the subtree, hidden, at the cockpit_camera
    // marker. Able to fail: without the Skip arm the interior build finds no cockpit1; without the
    // mount it sits at the plane origin at authored (~20x) scale.
    internal static void CockpitInterior(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        Node3D? exterior = null, withInterior = null;
        try
        {
            var plain = new PlaneBuilder(planesGamez, textures, spinningProps: true);
            exterior = plain.Build(ctx.PlaneName);
            ctx.Check(plain.CockpitInterior == null,
                $"the default flight build carries no interior plane={ctx.PlaneName}");

            var builder = new PlaneBuilder(planesGamez, textures, spinningProps: true,
                cockpitInterior: true);
            withInterior = builder.Build(ctx.PlaneName);
            var interior = builder.CockpitInterior;
            ctx.Check(interior != null, $"the interior build carries a cockpit1 subtree");
            if (interior == null)
                return;

            ctx.Check(!interior.Visible, $"the interior is built hidden");
            ctx.Check(interior.Position.IsEqualApprox(builder.CockpitCameraOffset),
                $"mounted at the cockpit_camera marker pos={interior.Position}");
            ctx.Check(interior.Scale.IsEqualApprox(Vector3.One * PlaneBuilder.InteriorScale),
                $"scaled to the port's interior scale scale={interior.Scale.X:0.###}");
            ctx.Check(builder.MeshInstanceCount > plain.MeshInstanceCount,
                $"the interior adds meshes plain={plain.MeshInstanceCount} with={builder.MeshInstanceCount}");
            // The panel the pilot reads and the two torn-skin panels B12 drives, both hidden.
            ctx.Check(FindNamed(interior, "gauges") != null, $"the interior carries its gauges subtree");
            foreach (var panel in new[] { "pcdp4", "pcdp6" })
            {
                var node = FindNamed(interior, panel);
                ctx.Check(node != null, $"the interior carries {panel}");
                ctx.Check(node is not { Visible: true }, $"{panel} is built hidden (torn state off)");
            }
            // ⚠ The five windshield bullet-hole groups and the two warning lamps ship active:true,
            // so an unparked build renders white splats across the sky on a pristine plane.
            foreach (var state in new[]
                     { "bullet1", "bullet2", "bullet3", "bullet4", "bullet5", "lowalt_on", "stallwarning_on" })
            {
                var node = FindNamed(interior, state);
                ctx.Check(node != null, $"the interior carries {state}");
                ctx.Check(node is not { Visible: true }, $"{state} is parked hidden on a pristine plane");
            }
            // …and the panel geometry beside them is NOT parked: the states are a named set, not a
            // blanket hide, so a wrong predicate that hid the dashboard would fail here.
            foreach (var kept in new[] { "gauges", "structure", "nosedamage", "ggindicator0" })
                ctx.Check(FindNamed(interior, kept) is { Visible: true }, $"{kept} still renders");

            // The exterior panels stay DamageVisuals' alone: the interior pair must not join them.
            foreach (var node in builder.DamagePanels)
                ctx.Check(!AnimRuntime.NameOf(node).StartsWith("pcdp", System.StringComparison.OrdinalIgnoreCase),
                    $"DamagePanels holds no cockpit panel name={AnimRuntime.NameOf(node)}");

            var cockpit = CockpitVisibility.Bind(withInterior, interior);
            ctx.Check(cockpit != null, $"the visibility rig binds to the built model");
            if (cockpit == null)
                return;
            var body = FindNamed(withInterior, "healthy");
            var markers = FindNamed(withInterior, "markers");
            var dontmove = FindNamed(withInterior, "dontmove");
            ctx.Check(body != null && markers != null && dontmove != null,
                $"the airframe's own healthy/markers/dontmove groups were found");
            // ⚠ The gauge sub-assemblies inside cockpit1 carry their own 'markers' children; the
            // one bound must be the airframe's, which is the one holding cockpit_camera.
            ctx.Check(markers != null && FindNamed(markers, "cockpit_camera") != null,
                $"the bound markers group is the airframe's, not a gauge's");

            cockpit.Apply(PilotViewMode.Cockpit, firstPerson: true);
            ctx.Check(interior.Visible && body is { Visible: false }
                && markers is { Visible: true } && dontmove is { Visible: true },
                $"Cockpit: interior in, body out, markers/dontmove kept");
            cockpit.Apply(PilotViewMode.Nose, firstPerson: true);
            ctx.Check(!interior.Visible && body is { Visible: false }
                && markers is { Visible: false } && dontmove is { Visible: false },
                $"Nose: interior out, body out, markers/dontmove out");
            cockpit.Apply(PilotViewMode.Cockpit, firstPerson: false);
            ctx.Check(!interior.Visible && body is { Visible: true }
                && markers is { Visible: true } && dontmove is { Visible: true },
                $"a held external view restores the aircraft while Cockpit stays selected");

            DrivenPanel(ctx, interior);
            DrivenBelts(ctx, interior, builder, planesGamez, textures);
        }
        finally
        {
            exterior?.Free();
            withInterior?.Free();
            textures.Dispose();
        }
    }

    // The interior's own render pass (--cockpit-pass): the panel leaves the plane model for a
    // world of its own, where both it and the camera sit at the origin, so no chapter-scale
    // coordinate enters its transform chain. Able to fail: a pass that shares the main World3D, one
    // that leaves the mount translation on the interior, a camera placed anywhere but the origin,
    // or an FOV taken from a second copy of the per-mode table instead of the camera's own.
    internal static void CockpitOverlayPass(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        Node3D? plane = null;
        Node? host = null;
        try
        {
            var builder = new PlaneBuilder(planesGamez, textures, spinningProps: true,
                cockpitInterior: true);
            plane = builder.Build(ctx.PlaneName);
            if (builder.CockpitInterior is not { } interior)
            {
                ctx.Check(false, $"the interior build carries a cockpit1 subtree");
                return;
            }
            var mountBasis = interior.Transform.Basis;
            host = new Node();
            var overlay = CockpitOverlay.Build(host, interior, null, null);
            ctx.Check(overlay != null, $"the pass builds over a rig's HUD parent");
            if (overlay == null)
                return;

            ctx.Check(interior.Position == Vector3.Zero,
                $"the interior sits at the overlay world's origin pos={interior.Position}");
            ctx.Check(interior.Transform.Basis.IsEqualApprox(mountBasis),
                $"the head-pitch tilt and the interior scale survive the move");
            ctx.Check(interior.Scale.IsEqualApprox(Vector3.One * PlaneBuilder.InteriorScale),
                $"still at the port's interior scale scale={interior.Scale.X:0.###}");
            ctx.Check(overlay.Camera.Transform.Origin == Vector3.Zero,
                $"the overlay camera sits at the origin pos={overlay.Camera.Transform.Origin}");
            var view = interior.GetParent() as SubViewport;
            ctx.Check(view != null, $"the interior hangs in the pass's own SubViewport");
            ctx.Check(view != null && view.TransparentBg,
                $"the pass renders on a transparent background, so the main view shows under it");
            ctx.Check(view != null && overlay.Camera.GetParent() == view,
                $"the camera looks at the interior from inside that same viewport");
            ctx.Check(view?.World3D != null && view.World3D != host.GetWindow()?.World3D,
                $"the pass owns its World3D rather than sharing the main one");
            // The same table the pilot's own camera reads, not a copy: 80° horizontal at 16:9.
            ctx.Check(Mathf.Abs(CameraController.FirstPersonFovDeg(PilotViewMode.Cockpit, 16f / 9f)
                    - CameraController.HorizontalToVerticalFovDeg(80f, 16f / 9f)) < 0.001f,
                $"the pass's FOV law is the camera's own per-mode law");
            // The wobble the interior inherited below the shake pivot has to reach the pass. The
            // mount's tilt is about X, so its Right axis is the witness: a Z roll of r turns it by
            // exactly r, and a pass that forgot the wobble leaves it at 0.
            float rolled = CockpitOverlay.WobbledMount(mountBasis, 0.3f).X.AngleTo(mountBasis.X);
            ctx.Check(Mathf.Abs(rolled - 0.3f) < 0.001f,
                $"the shake pivot's roll reaches the panel in the pass angle={rolled:0.###} rad");
            ctx.Check(CockpitOverlay.WobbledMount(mountBasis, 0f).IsEqualApprox(mountBasis),
                $"no wobble leaves the mount basis untouched");
            // The pass keeps the world's orientation, so a muzzle flash 11 m right of a yawed plane's
            // eye at (1000, 50, -2000) lands at that same world offset from the pass's origin; a
            // mirror that kept the eye's coordinates would put it thousands of metres out.
            var yawed = new Basis(Vector3.Up, Mathf.Pi / 2f);
            var eye = new Vector3(1000f, 50f, -2000f);
            var offset = yawed * new Vector3(11f, 0f, 0f);
            var mirrored = CockpitOverlay.ToOverlay(eye + offset, eye);
            ctx.Check(mirrored.IsEqualApprox(offset) && mirrored.Length() < 12f,
                $"a muzzle flash lands in the pass at its world offset from the eye got={mirrored}");
            // The crash cut's exit from first person, whose whole point is that it works with the
            // interior node untouched: hiding the airframe cannot reach a panel that lives outside
            // the plane model, and no Sync follows the cut to notice a hidden one.
            interior.Visible = true;
            overlay.Deactivate();
            ctx.Check(!overlay.Visible && interior.Visible,
                $"Deactivate takes the pass off the screen without touching the interior node");
            ctx.Check(view is { RenderTargetUpdateMode: SubViewport.UpdateMode.Disabled },
                $"and stops the viewport re-rendering it");
        }
        finally
        {
            host?.Free();
            plane?.Free();
            textures.Dispose();
        }
    }

    // The authored panel driven off live readings (BL-431): the needles take an absolute angle and
    // the two lamps follow the cluster's own blink state. Able to fail: a needle left at its modeled
    // rest rotation, a lamp still parked while its condition holds, or a drive that moves the panel
    // geometry around the needle instead of the needle itself.
    internal static void DrivenPanel(TestContext ctx, Node3D interior)
    {
        var panel = CockpitGauges.Bind(interior);
        ctx.Check(panel != null, $"the gauge drive binds to the built interior");
        var speed = FindNamed(interior, "speed");
        var hundreds = FindNamed(interior, "hundreds");
        var lowAlt = FindNamed(interior, "lowalt_on");
        var face = FindNamed(interior, "speedometer");
        if (panel == null || speed == null || hundreds == null || lowAlt == null || face == null)
        {
            ctx.Check(false, $"the panel's needles, lamp and face were all found");
            return;
        }

        // ⚠ The rotation axis is only right if the needle is authored flat in its own XY plane.
        // If the import left the dial in XZ, spinning about Z would tip the needle out of the face
        // instead of sweeping it, and every angle assertion below would still pass.
        if (FirstMesh(speed) is { } needleMesh)
        {
            var size = needleMesh.GetAabb().Size;
            ctx.Check(size.Z < size.Y * 0.1f && size.Y > 0f,
                $"the needle is flat in its own XY plane, so +Z is the sweep axis size={size}");
        }

        var faceRest = face.Transform;
        var horizon = FindNamed(interior, "pfhorizon");
        var horizonRest = horizon?.Transform;
        var cluster = new GaugeCluster { SpeedMph = 200f, AltitudeFt = 500f };
        try
        {
            panel.Apply(cluster);
            // 0.7199957 deg/mph clockwise, i.e. negative about +Z.
            float wantSpeed = -Mathf.DegToRad(GaugeCluster.SpeedAngleDeg(200f));
            float gotSpeed = speed.Transform.Basis.GetEuler().Z;
            ctx.Check(Mathf.Abs(Mathf.AngleDifference(gotSpeed, wantSpeed)) < 0.01f,
                $"the speed needle takes its absolute angle got={gotSpeed:0.000} want={wantSpeed:0.000} rad");
            float wantAlt = -Mathf.DegToRad(GaugeCluster.AltHundredsAngleDeg(500f));
            float gotAlt = hundreds.Transform.Basis.GetEuler().Z;
            ctx.Check(Mathf.Abs(Mathf.AngleDifference(gotAlt, wantAlt)) < 0.01f,
                $"the long altimeter needle takes its absolute angle got={gotAlt:0.000} want={wantAlt:0.000} rad");
            // ⚠ Only the needle moves: the dial it sweeps over is authored geometry.
            ctx.Check(face.Transform.IsEqualApprox(faceRest), $"the dial face is left where it was built");
            ctx.Check(!lowAlt.Visible, $"LOW ALT stays parked while the cluster's lamp is dark");

            // A second write must REPLACE the angle, not accumulate onto it.
            panel.Apply(cluster);
            ctx.Check(Mathf.Abs(Mathf.AngleDifference(speed.Transform.Basis.GetEuler().Z, wantSpeed)) < 0.01f,
                $"a second frame writes the same absolute angle rather than turning again");

            // The horizon (BL-431 B12): N = Rz(-roll) . Rx(pitch), no gain/offset/clamp. A zero
            // attitude first, since that has to equal the authored rest basis exactly.
            ctx.Check(horizon != null && horizonRest != null, $"the interior carries pfhorizon");
            if (horizon != null && horizonRest != null)
            {
                cluster.HorizonPitchRad = 0f;
                cluster.HorizonRollRad = 0f;
                panel.Apply(cluster);
                ctx.Check(horizon.Transform.Basis.IsEqualApprox(horizonRest.Value.Basis),
                    $"a zero attitude leaves pfhorizon at its authored basis");

                float pitch = Mathf.DegToRad(12f);
                float roll = Mathf.DegToRad(-25f);
                cluster.HorizonPitchRad = pitch;
                cluster.HorizonRollRad = roll;
                panel.Apply(cluster);
                var want = new Basis(Vector3.Back, -roll) * new Basis(Vector3.Right, pitch);
                ctx.Check(horizon.Transform.Basis.IsEqualApprox(want),
                    $"pfhorizon takes Rz(-roll).Rx(pitch) pitch={pitch:0.000} roll={roll:0.000} rad");
                ctx.Check(horizon.Transform.Origin.IsEqualApprox(horizonRest.Value.Origin),
                    $"pfhorizon's authored translation is untouched");
            }
        }
        finally
        {
            cluster.Free();
        }
    }

    // The belt lights take the loadout's colour tier (BL-431). A pristine plane reads all-green,
    // which proves nothing, so this drives a spent belt and reads the material back. Able to fail:
    // a drive that recolours nothing, or one that writes the shared built material and so repaints
    // every indicator at once instead of the one position. Binds through CockpitGauges.Bind(builder),
    // the same expression the live flight path calls, so a caller that regresses to the interior
    // alone breaks here rather than only at the controls.
    internal static void DrivenBelts(TestContext ctx, Node3D interior, PlaneBuilder builder,
        GameZ planesGamez, TextureArchive textures)
    {
        var cluster = GaugeCluster.Build(planesGamez, ctx.PlaneName, textures, new List<DestroyablePart>());
        var panel = CockpitGauges.Bind(builder);
        if (cluster == null || panel == null)
        {
            ctx.Check(false, $"a real cluster and panel were built for the belt drive");
            return;
        }
        try
        {
            // Not a fixed count: derived from the interior's own materials, so an empty-materials
            // regression fails here instead of an all-green panel. "greenindicator", not bare
            // "indicator", which also matches the still-unwired horizon's texture.
            int expectedBelts = builder.InteriorMaterials.Count(m =>
                m.TextureName.Contains("greenindicator", System.StringComparison.OrdinalIgnoreCase));
            int expectedZones = builder.InteriorMaterials.Count(m =>
                m.TextureName.Contains("hatchptrn", System.StringComparison.OrdinalIgnoreCase));
            ctx.Check(panel.BeltCount > 0 && panel.BeltCount == expectedBelts,
                $"belts bound matches the indicator-light materials the interior carries found={panel.BeltCount} materials={expectedBelts}");
            ctx.Check(panel.DamageZoneCount > 0 && panel.DamageZoneCount == expectedZones,
                $"damage zones bound matches the hatch materials the interior carries found={panel.DamageZoneCount} materials={expectedZones}");
            // Position 0 spent, position 1 full: one dial, two tiers, so a shared-material write
            // cannot pass this.
            cluster.GunGauge = new GaugeCluster.WeaponGauge { Slots = new[] { 0f, 1f } };
            panel.Apply(cluster);
            var names = new Dictionary<ulong, string>();
            foreach (var (material, texture) in builder.InteriorMaterials)
                names[material.GetInstanceId()] = texture;
            // ⚠ An indicator carries TWO driven surfaces on different colour cycles (the light and
            // its hilite bar), so a check that reads "the first albedo" reads whichever the mesh
            // happens to order first and proves nothing.
            var spentLight = AlbedoOf(FindNamed(interior, "ggindicator0"), names, hilite: false);
            var spentBar = AlbedoOf(FindNamed(interior, "ggindicator0"), names, hilite: true);
            var fullLight = AlbedoOf(FindNamed(interior, "ggindicator1"), names, hilite: false);
            ctx.Check(spentLight != null && spentBar != null && fullLight != null,
                $"the light and the hilite bar were both found on the indicators");
            ctx.Check(spentLight == cluster.BeltLightTexture(2),
                $"a spent belt position's light takes the red variant");
            ctx.Check(spentBar == cluster.BeltHiliteTexture(2),
                $"and its hilite bar takes the red bar, on its own cycle");
            ctx.Check(fullLight == cluster.BeltLightTexture(0),
                $"the position beside it stays green, so the write is per-node");

            // The damage zones read the PART name, which is the node's minus its "damage" suffix.
            // A wrong key reads as a permanently green dial, so drive one zone to its red band.
            cluster.PartFraction = part =>
                part.Equals("nose", System.StringComparison.OrdinalIgnoreCase) ? 0.05f : 1f;
            panel.Apply(cluster);
            var hurt = AlbedoOf(FindNamed(interior, "nosedamage"), names, hilite: true);
            var intact = AlbedoOf(FindNamed(interior, "taildamage"), names, hilite: true);
            ctx.Check(hurt != null && intact != null, $"the damage dial's zones were found");
            ctx.Check(hurt == cluster.ZoneHiliteTexture(3),
                $"a zone at 5% takes the red border");
            ctx.Check(intact == cluster.ZoneHiliteTexture(0),
                $"an untouched zone beside it stays green");
        }
        finally
        {
            cluster.Free();
        }
    }

    // The albedo the driven surface of the requested KIND is pointing at right now: the hilite bar
    // or the light beside it, told apart by the texture each was built from.
    internal static Texture2D? AlbedoOf(Node3D? node, IReadOnlyDictionary<ulong, string> names, bool hilite)
    {
        if (node == null)
        {
            return null;
        }
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(i) is not ShaderMaterial built
                    || !names.TryGetValue(built.GetInstanceId(), out string? texture)
                    || texture.Contains("hilite", System.StringComparison.OrdinalIgnoreCase) != hilite)
                {
                    continue;
                }
                if (mesh.GetSurfaceOverrideMaterial(i) is ShaderMaterial live)
                {
                    return live.GetShaderParameter("albedo_tex").As<Texture2D>();
                }
            }
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3d && AlbedoOf(n3d, names, hilite) is { } hit)
            {
                return hit;
            }
        }
        return null;
    }

    // The first mesh at or under this node, for a geometry assertion about it.
    internal static MeshInstance3D? FirstMesh(Node3D root)
    {
        if (root is MeshInstance3D mesh)
        {
            return mesh;
        }
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d && FirstMesh(n3d) is { } hit)
            {
                return hit;
            }
        }
        return null;
    }

    // The first node in the subtree carrying this ORIGINAL gamez name (SceneBuilder sanitizes and
    // Godot renames duplicate siblings, so Node.Name is not the name the data uses).
    internal static Node3D? FindNamed(Node3D root, string name)
    {
        if (AnimRuntime.NameOf(root).Equals(name, System.StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d && FindNamed(n3d, name) is { } hit)
            {
                return hit;
            }
        }
        return null;
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

                // The two 8-inch cannons are the whole install's only pools whose RESET_STATE
                // switches `dbase` ON beside `healthy` ACTIVE, so they are where a base read as
                // half a death boots destroyed and swallows every shot.
                foreach (var gun in registry.All.Where(i =>
                    AnimRuntime.NameOf(i.Anchor).StartsWith("8igun", System.StringComparison.OrdinalIgnoreCase)))
                {
                    ctx.Check(gun.Status == DestructibleRegistry.State.Healthy
                              && gun.Health == gun.MaxHealth,
                        $"{chapter} {AnimRuntime.NameOf(gun.Anchor)} boots standing hp={gun.Health}/{gun.MaxHealth} state={gun.Status}");
                }
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

    // ---- node lab tree rows must follow live Visible --------------------------------------------

    // Hides a node through the lab's own Hide action, then re-shows it through a real RESET_STATE def,
    // the same path a world animation uses, and checks the tree row both times, never through the
    // button, only through Node3D.Visible. A def re-showing a node the user hid is correct behaviour,
    // so the row must follow it. ⚠ Use a chapter other than TestContext.Chapter: that one is cached
    // and shared with damage-hd, so the candidate search would otherwise depend on suite run order.
    internal static void NodeLabVisibility(TestContext ctx)
    {
        ctx.WithWorld("C2", collision: false, world =>
        {
            DestructibleRegistry.Instance? chosen = null;
            Node3D? healthy = null;
            foreach (var inst in world.Runtime.Destructibles.All)
            {
                if (inst.Def.ResetState == null)
                {
                    continue;
                }
                if (FindVariant(inst.Anchor, "healthy") is { Visible: true } found)
                {
                    chosen = inst;
                    healthy = found;
                    break;
                }
            }
            ctx.Check(chosen != null,
                $"chapter has a destructible with a visible 'healthy' variant and a RESET_STATE chapter={ctx.Chapter}");
            if (chosen == null || healthy == null)
            {
                return;
            }

            var selection = new SelectionService(world.Session.Root, ctx.Camera);
            var lab = new NodeLab(world.Session.Root, selection, world.Runtime, world.Session.Program,
                world.Session.Builder.Scene, collisionBuilt: false);
            ctx.Host.AddChild(selection);
            ctx.Host.AddChild(lab);
            try
            {
                lab.Toggle();
                selection.Select(healthy);
                lab.RevealSelectionForTest();
                lab.ToggleHide();
                ctx.Check(!healthy.Visible, $"ToggleHide actually hides the node node={SelectionService.NameOf(healthy)}");

                var hidden = lab.RowStateForTest(healthy);
                ctx.Check(hidden is { Dim: true } row1 && row1.Text.Contains("(hidden)"),
                    $"row reads hidden right after the button node={SelectionService.NameOf(healthy)} text={hidden?.Text} dim={hidden?.Dim}");

                // The re-show is a real def, not the lab: RESET_STATE's OBJECT_ACTIVE_STATE events
                // are what an animation uses to bring the healthy subtree back, with no button
                // press and nothing telling the lab this node exists.
                world.Runtime.ResetDestructible(chosen);
                ctx.Check(healthy.Visible, $"RESET_STATE re-shows the node node={SelectionService.NameOf(healthy)}");

                lab.RefreshStatusForTest();
                var shown = lab.RowStateForTest(healthy);
                ctx.Check(shown is { Dim: false } row2 && !row2.Text.Contains("(hidden)"),
                    $"row follows the def's re-show without user input node={SelectionService.NameOf(healthy)} text={shown?.Text} dim={shown?.Dim}");
            }
            finally
            {
                lab.Free();
                selection.Free();
            }
        });
    }

    // The first descendant (inclusive) whose cs_name contains the tag — "healthy"/"destroyed" name
    // their variant subtrees exactly as CountVariants (Probes.cs) scans for, but this returns the
    // node itself rather than a count.
    internal static Node3D? FindVariant(Node3D node, string tag)
    {
        string cs = node.HasMeta(AnimRuntime.NameMeta) ? node.GetMeta(AnimRuntime.NameMeta).AsString() : node.Name.ToString();
        if (node.HasMeta(AnimRuntime.NameMeta) && cs.Contains(tag, System.StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3d && FindVariant(n3d, tag) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    // Asserts every pane of a 2-, 3- and 4-player rig is a 3D audio listener. The check reads trivial
    // and is not: a fresh SubViewport is NOT a listener, and in splitscreen the main camera stands down,
    // which takes it out of the World3D listener set. With no listener-enabled viewport left,
    // AudioStreamPlayer3D finds no listener in range, clears its bus volumes, and every 3D emitter in
    // the world is silent, with nothing logged or counted to say so.
    internal static void SplitscreenListeners(TestContext ctx)
    {
        var main = ctx.Host.GetViewport();
        ctx.Check(main.AudioListenerEnable3D,
            $"the main viewport is a 3D audio listener (the untouched 1P path)");

        // The default the rig has to override, proved rather than assumed.
        using (var bare = new SubViewport())
        {
            ctx.Check(!bare.AudioListenerEnable3D,
                $"a fresh SubViewport is NOT an audio listener, so each pane must set it");
        }

        for (int players = 2; players <= SplitScreen.MaxPlayers; players++)
        {
            var split = SplitScreen.Build(players, main);
            ctx.Host.AddChild(split);
            try
            {
                ctx.Same(players, split.Views.Count, $"{players}P panes");
                foreach (var view in split.Views)
                {
                    ctx.Check(view.AudioListenerEnable3D,
                        $"{players}P pane {view.Name} is a 3D audio listener");
                }
            }
            finally
            {
                ctx.Host.RemoveChild(split);
                split.Free();
            }
        }
    }

    // WorldLights.Commit fades and ranks
    // each light against the NEAREST of every pane's camera, not a single position. Driven
    // straight against a real WorldLights instance with synthetic positions —
    // there is no per-player placement flag to give two scripted panes independent spots (the
    // same CLI gap B11/B12 hit), so the rule is pinned here instead and the visual verdict is
    // PT-52's, alongside B11/B12's own owed at-the-controls check.
    internal static void WorldLightsNearestViewer(TestContext ctx)
    {
        var p1 = Vector3.Zero;
        // Well past FadeEnd (1500 m) from P1 alone, but 100 m from a second viewer.
        var farFromP1 = new Vector3(0f, 0f, -2000f);
        var p2 = new Vector3(0f, 0f, -2100f);

        var lights = new WorldLights();

        lights.Begin();
        lights.Add(farFromP1, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1 });
        ctx.Check(!lights.CommittedPositions.Contains(farFromP1),
            $"ABLE-TO-FAIL CONTROL: 2000 m from a lone P1 is past the 1500 m FadeEnd, so the light drops");

        lights.Begin();
        lights.Add(farFromP1, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1, p2 });
        ctx.Check(lights.CommittedPositions.Contains(farFromP1),
            $"the same light stays committed once a second viewer sits 100 m from it — nearest, not P1 alone");

        // The MaxActive budget's Significance rank must answer to the same nearest-viewer rule, not just
        // the fade: the 16-slot budget is packed with filler lights strictly farther from P1 than besideP2
        // sits from P2, so a correct nearest-viewer rank keeps besideP2 and cuts the farthest filler.
        var besideP2 = new Vector3(0f, 5f, -2100f);
        lights.Begin();
        for (int i = 0; i < WorldLights.MaxActive; i++)
            lights.Add(new Vector3(5f + i, 0f, -5f), Colors.White, 1f, 10f);
        lights.Add(besideP2, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1 });
        ctx.Check(lights.CommittedPositions.Count == WorldLights.MaxActive
                  && !lights.CommittedPositions.Contains(besideP2),
            $"ABLE-TO-FAIL CONTROL: against P1 alone the 17th light (right beside where P2 will be) is past FadeEnd and never reaches the budget");

        lights.Begin();
        for (int i = 0; i < WorldLights.MaxActive; i++)
            lights.Add(new Vector3(5f + i, 0f, -5f), Colors.White, 1f, 10f);
        lights.Add(besideP2, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1, p2 });
        ctx.Check(lights.CommittedPositions.Count == WorldLights.MaxActive
                  && lights.CommittedPositions.Contains(besideP2),
            $"with P2 present the same light is nearest to a viewer and outranks the farthest filler for a slot in the budget");

        // Single viewer must read exactly as it did before this item — the goldens' own invariant.
        var nearP1 = new Vector3(0f, 0f, -5f);
        lights.Begin();
        lights.Add(nearP1, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1 });
        ctx.Check(lights.CommittedPositions.Count == 1 && lights.CommittedPositions.Contains(nearP1),
            $"one viewer (single player) uses the single-viewer distance rule");
    }
}

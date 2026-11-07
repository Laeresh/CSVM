using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Objectives;
using CSVM.Session.World;
using Godot;

namespace CSVM.Testing;

/// <summary>The cloud band's per-object visibility. A zeppelin or an aeroplane sitting wholly on
/// the far side of the band's midpoint from the camera is not drawn at all. That is the original's
/// per-object zone assignment plus its camera zone gate. Flies C1C/M01's own band and its own
/// zeppelin records rather than invented numbers.</summary>
internal static class CloudBandObjectGateSuites
{
    // CM06's mission, CLOUD_COVER 1055-1110 with a 30 m core. The midpoint the objects are judged
    // against is 1082.5, and the camera's own state flips at 1067.5.
    private const string BandChapter = "C1C";
    private const string BandMission = "M01";

    // A fresh Camera3D's cull mask, the mask WeatherRig narrows per camera.
    private const uint FullMask = 0xFFFFFu;

    // No chapter fog volumes: C1C arms none, and the band alone is this suite's subject.
    private static readonly FogVolumeBox[] NoVolumes = Array.Empty<FogVolumeBox>();

    [Suite("cloud-band-object-gate",
        "an object wholly above the CLOUD_COVER band's midpoint earns zone 2 and one wholly below "
        + "it zone 1, so C1C/M01's zeppelin hull is culled for a camera on the other side of the "
        + "band and drawn for one on its own side; a straddling hull and a --no-fog run stay "
        + "ungated")]
    internal static void CloudBandObjectGate(TestContext ctx)
    {
        ZrdrSet missionZrdr = ctx.MissionZrdr(BandChapter, BandMission);
        ZrdrSet chapterZrdr = ctx.ChapterZrdr(BandChapter);
        ctx.RequireData(missionZrdr, $"{BandChapter}/{BandMission} zrdr");
        ctx.RequireData(chapterZrdr, $"{BandChapter} zrdr");

        var weather = WeatherState.Load(missionZrdr)
            ?? throw new SuiteSkippedException($"{BandChapter}/{BandMission} carries no weather.zrd");
        ctx.Check(weather.HasCloudBand,
            $"{BandChapter}/{BandMission} authors a CLOUD_COVER band, bottom={F(weather.CloudBottom)} top={F(weather.CloudTop)}");
        if (!weather.HasCloudBand)
        {
            return;
        }

        float centre = weather.CloudBandCentre;
        var report = new StringBuilder();
        report.AppendLine(
            $"{BandChapter}/{BandMission}: band {F(weather.CloudBottom)}-{F(weather.CloudTop)}, "
            + $"midpoint {F(centre)}, core bottom {F(weather.CloudCoreBottom)}");

        ctx.Same(2, weather.ObjectZone(centre + 100f, centre + 140f),
            $"a hull wholly above the midpoint earns zone 2");
        ctx.Same(1, weather.ObjectZone(centre - 140f, centre - 100f),
            $"a hull wholly below the midpoint earns zone 1");
        ctx.Same(-1, weather.ObjectZone(centre - 20f, centre + 20f),
            $"a hull straddling the midpoint earns -1, drawn at every camera state");

        var defs = Zeppelins.Load(missionZrdr);
        ctx.Check(defs.Count > 0, $"{BandChapter}/{BandMission} authors zeppelin records, count={defs.Count}");
        if (defs.Count == 0)
        {
            return;
        }

        var gate = new ObjectZoneGate();
        var hosts = new Dictionary<string, Node3D>();
        var hulls = new Dictionary<string, MeshInstance3D>();
        ZeppelinRuntime? runtime = null;
        try
        {
            foreach (var def in defs)
            {
                var host = new Node3D { Name = def.Node };
                // A 40 m hull, the drawn part the gate measures the object's altitude span off.
                // The record's own node carries no mesh outside a built chapter world.
                var hull = new MeshInstance3D
                {
                    Name = "hull",
                    Mesh = new BoxMesh { Size = new Vector3(20f, 40f, 120f) },
                };
                host.AddChild(hull);
                ctx.Host.AddChild(host);
                hosts[def.Node] = host;
                hulls[def.Node] = hull;
            }

            var nets = AiNets.Load(chapterZrdr);
            runtime = new ZeppelinRuntime(defs, name => hosts.TryGetValue(name, out var h) ? h : null, nets);
            ctx.Check(runtime.LiveCount > 0, $"the mission's zeppelins place live, count={runtime.LiveCount}");

            var flown = new List<Node3D>();
            runtime.CollectHosts(flown);
            ctx.Same(runtime.LiveCount, flown.Count, $"CollectHosts offers every placed hull to the gate");
            if (flown.Count == 0)
            {
                return;
            }

            var subject = flown[0];
            var subjectHull = hulls[subject.Name];
            float below = weather.CloudCoreBottom - 200f;
            float above = weather.CloudCoreBottom + 200f;

            AboveTheBand(ctx, gate, weather, flown, subject, subjectHull, centre, below, above, report);
            BelowTheBand(ctx, gate, weather, flown, subject, subjectHull, centre, report);
            Straddling(ctx, gate, weather, flown, subject, subjectHull, centre, report);
            GateOpen(ctx, gate, weather, flown, subject, subjectHull, centre, report);
            InAFogVolume(ctx, weather, centre);
        }
        finally
        {
            runtime?.Free();
            foreach (var host in hosts.Values)
            {
                host.Free();
            }
        }

        ctx.WriteArtifact("test-cloud-band-object-gate.txt", report.ToString());
        ctx.Note($"{BandChapter}/{BandMission}: a zeppelin above the band's {F(centre)} m midpoint is culled for a camera under the band and drawn for one above it, and the mirror holds");
    }

    // #287: fire puffs, smoke and tracers above C1C/M01's band drew for a camera under it, against
    // the sky dome, because only hulls were gated. The original zones every particle by its own
    // height against the band's midpoint (0x473580), and the rounds follow the same rule here.
    [Suite("cloud-band-point-gate",
        "a puffer particle and a tracer round above C1C/M01's band midpoint draw only for a camera "
        + "above the band, one below it only for a camera under it, a camera in a fog volume draws "
        + "both, and a mission with no band gates nothing")]
    internal static void CloudBandPointGate(TestContext ctx)
    {
        ZrdrSet missionZrdr = ctx.MissionZrdr(BandChapter, BandMission);
        ctx.RequireData(missionZrdr, $"{BandChapter}/{BandMission} zrdr");
        var weather = WeatherState.Load(missionZrdr)
            ?? throw new SuiteSkippedException($"{BandChapter}/{BandMission} carries no weather.zrd");
        ctx.Check(weather.HasCloudBand, $"{BandChapter}/{BandMission} authors a CLOUD_COVER band");
        if (!weather.HasCloudBand)
        {
            return;
        }
        float centre = weather.CloudBandCentre;
        uint state1 = ZoneGate.CullMask(FullMask, 1);
        uint state2 = ZoneGate.CullMask(FullMask, 2);
        uint state3 = ZoneGate.CullMask(FullMask, 3);

        uint low = ZoneGate.PointLayers(ZoneGate.PointZone(centre - 1f, centre));
        uint high = ZoneGate.PointLayers(ZoneGate.PointZone(centre, centre));
        ctx.Check((state1 & low) != 0 && (state1 & high) == 0,
            $"a camera under the band draws a point below the {F(centre)} m midpoint and not one at or above it");
        ctx.Check((state2 & high) != 0 && (state2 & low) == 0, $"a camera above the band draws the mirror");
        ctx.Check((state3 & low) != 0 && (state3 & high) != 0,
            $"a camera in a fog volume draws both, the callback's -1 for camera state 3");
        ctx.Check((FullMask & low) != 0 && (FullMask & high) != 0, $"an ungated camera draws both");
        ctx.Same(1u, ZoneGate.PointLayers(ZoneGate.PointZone(centre + 500f, null)),
            $"with no band a point stays on the world's layer 1");

        var report = new StringBuilder();
        PufferSides(ctx, centre, state1, state2, report);
        TracerSides(ctx, centre, state1, state2, report);
        ctx.WriteArtifact("test-cloud-band-point-gate.txt", report.ToString());
    }

    // A real emitter renderer behind a puffer fed the session ambience's midpoint, one puff each side.
    private static void PufferSides(TestContext ctx, float centre, uint state1, uint state2, StringBuilder report)
    {
        var state = new PufferState
        {
            Name = "band_puffer",
            Number = 1,
            TimeInterval = 1f / 60f,
            SizeMin = 1f,
            SizeMax = 1f,
            LifetimeMin = 5f,
            LifetimeMax = 5f,
        };
        var amb = new EffectAmbience();
        amb.SetBandMidpoint(centre);
        var atlas = Utils.TextureUpload.Create(Image.CreateEmpty(2, 2, false, Image.Format.Rgba8));
        var gpu = new MultiMeshEmitterRenderer(atlas, 1, new[] { false }, softParticles: false);
        var puffer = Puffer.CreateWith(state, gpu, sustained: true, ambience: amb);
        ctx.Host.AddChild(puffer);
        try
        {
            const float Step = 1f / 60f;
            puffer.Emit(new Vector3(0f, centre - 150f, 0f), Basis.Identity, Step);
            puffer.Emit(new Vector3(0f, centre + 150f, 0f), Basis.Identity, Step);
            puffer._Process(Step);
            var sides = gpu.BandSides;
            report.AppendLine($"puffer: below {sides.Below} on 0x{sides.BelowLayers:x}, above {sides.Above} on 0x{sides.AboveLayers:x}");
            ctx.Check(sides.Below > 0 && sides.Above > 0,
                $"the puffer draws its puffs in both side lists below={sides.Below} above={sides.Above}");
            ctx.Check((state1 & sides.AboveLayers) == 0 && (state1 & sides.BelowLayers) != 0,
                $"a camera under the band draws the low puff and not the high one, layers 0x{sides.BelowLayers:x}/0x{sides.AboveLayers:x}");
            ctx.Check((state2 & sides.BelowLayers) == 0 && (state2 & sides.AboveLayers) != 0,
                $"a camera above the band draws the high puff and not the low one");

            amb.SetBandMidpoint(null);
            puffer._Process(Step);
            var open = gpu.BandSides;
            ctx.Check(open.Above == 0 && open.BelowLayers == 1u,
                $"with no band every puff draws on layer 1 below={open.Below} above={open.Above} layers=0x{open.BelowLayers:x}");

            // Another system's mask (the cockpit's own-view hide, a hull's zone gate) wins: the
            // emitter keeps it and stops splitting by height.
            const uint Foreign = 1u << 9;
            MultiMeshInstance3D? lower = null;
            foreach (var child in puffer.GetChildren())
            {
                if (child is MultiMeshInstance3D list)
                {
                    lower = list;
                    break;
                }
            }
            ctx.Check(lower != null, $"the emitter's first draw list is its lower one");
            if (lower == null)
            {
                return;
            }
            lower.Layers = Foreign;
            amb.SetBandMidpoint(centre);
            puffer._Process(Step);
            var ridden = gpu.BandSides;
            ctx.Same(Foreign, lower.Layers, $"the emitter leaves a mask another system wrote");
            ctx.Same(0, ridden.Above, $"and draws every puff in that list rather than splitting them");
        }
        finally
        {
            puffer.Free();
        }
    }

    // A live projectile pool given the same ambience: a round each side of the midpoint.
    private static void TracerSides(TestContext ctx, float centre, uint state1, uint state2, StringBuilder report)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        TextureSet texturesPath = ctx.ChapterTextures(BandChapter);
        ctx.RequireData(texturesPath, $"{BandChapter} textures");
        WeaponDef? gun = null;
        foreach (var w in WeaponDefs.Load(ctx.Zrdr, null).All)
        {
            if (w.IsGun)
            {
                gun = w;
                break;
            }
        }
        ctx.Check(gun != null, $"a gun weapon loaded from the catalogue");
        if (gun == null)
        {
            return;
        }
        var amb = new EffectAmbience();
        amb.SetBandMidpoint(centre);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null, ambience: amb);
        try
        {
            ctx.Host.AddChild(pool);
            pool.Spawn(gun, new Transform3D(Basis.Identity, new Vector3(0f, centre - 150f, 0f)), Vector3.Zero, shooterId: 0);
            pool.Spawn(gun, new Transform3D(Basis.Identity, new Vector3(500f, centre + 150f, 0f)), Vector3.Zero, shooterId: 1);
            pool._Process(1f / 60f);
            var sides = pool.TracerSides();
            report.AppendLine($"tracers: below {sides.Below} on 0x{sides.BelowLayers:x}, above {sides.Above} on 0x{sides.AboveLayers:x}");
            ctx.Same(1, sides.Below, $"the low round's streak draws in the below-band pool");
            ctx.Same(1, sides.Above, $"the high round's streak draws in the above-band pool");
            ctx.Check((state1 & sides.AboveLayers) == 0 && (state1 & sides.BelowLayers) != 0,
                $"a camera under the band draws the low tracer and not the high one");
            ctx.Check((state2 & sides.BelowLayers) == 0 && (state2 & sides.AboveLayers) != 0,
                $"a camera above the band draws the high tracer and not the low one");
        }
        finally
        {
            pool.Free();
            textures.Dispose();
        }
    }

    private static void AboveTheBand(TestContext ctx, ObjectZoneGate gate, WeatherState weather,
        List<Node3D> flown, Node3D subject, MeshInstance3D hull, float centre, float below,
        float above, StringBuilder report)
    {
        subject.Position = new Vector3(0f, centre + 150f, 0f);
        gate.Tick(flown, weather, fogZoneArmed: false, NoVolumes, open: false);
        uint layer = hull.Layers;
        report.AppendLine($"hull at {F(subject.Position.Y)}: layers=0x{layer:x}, expected zone 2 = 0x{ZoneGate.LayerFor(2):x}");
        ctx.Same(ZoneGate.LayerFor(2), layer, $"a hull above the midpoint moves onto zone 2's layer");
        ctx.Check(gate.GatedObjects > 0, $"the gate reports the hull gated, count={gate.GatedObjects}");
        ctx.Same(1, CameraState(weather, below), $"a camera under the band is in state 1");
        ctx.Check((ZoneGate.CullMask(FullMask, 1) & layer) == 0,
            $"a camera in state 1 does not draw it, the whole point of the band's per-object gate");
        ctx.Same(2, CameraState(weather, above), $"a camera above the core's bottom is in state 2");
        ctx.Check((ZoneGate.CullMask(FullMask, 2) & layer) != 0,
            $"a camera in state 2 does draw it, so the hull is hidden by the band and not by the gate");
    }

    private static void BelowTheBand(TestContext ctx, ObjectZoneGate gate, WeatherState weather,
        List<Node3D> flown, Node3D subject, MeshInstance3D hull, float centre, StringBuilder report)
    {
        subject.Position = new Vector3(0f, centre - 150f, 0f);
        gate.Tick(flown, weather, fogZoneArmed: false, NoVolumes, open: false);
        uint layer = hull.Layers;
        report.AppendLine($"hull at {F(subject.Position.Y)}: layers=0x{layer:x}, expected zone 1 = 0x{ZoneGate.LayerFor(1):x}");
        ctx.Same(ZoneGate.LayerFor(1), layer, $"the mirror: a hull below the midpoint moves onto zone 1's layer");
        ctx.Check((ZoneGate.CullMask(FullMask, 2) & layer) == 0,
            $"a camera above the band does not draw it");
        ctx.Check((ZoneGate.CullMask(FullMask, 1) & layer) != 0,
            $"a camera under the band does draw it");
    }

    private static void Straddling(TestContext ctx, ObjectZoneGate gate, WeatherState weather,
        List<Node3D> flown, Node3D subject, MeshInstance3D hull, float centre, StringBuilder report)
    {
        subject.Position = new Vector3(0f, centre, 0f);
        gate.Tick(flown, weather, fogZoneArmed: false, NoVolumes, open: false);
        uint layer = hull.Layers;
        report.AppendLine($"hull at {F(subject.Position.Y)} (straddling): layers=0x{layer:x}");
        ctx.Check((ZoneGate.CullMask(FullMask, 1) & layer) != 0
            && (ZoneGate.CullMask(FullMask, 2) & layer) != 0,
            $"a hull standing in the band itself draws at both camera states, the binary's own -1");
    }

    private static void GateOpen(TestContext ctx, ObjectZoneGate gate, WeatherState weather,
        List<Node3D> flown, Node3D subject, MeshInstance3D hull, float centre, StringBuilder report)
    {
        subject.Position = new Vector3(0f, centre + 150f, 0f);
        gate.Tick(flown, weather, fogZoneArmed: false, NoVolumes, open: true);
        uint layer = hull.Layers;
        report.AppendLine($"hull at {F(subject.Position.Y)} with the gate open: layers=0x{layer:x}");
        ctx.Check((ZoneGate.CullMask(FullMask, 1) & layer) != 0,
            $"--no-fog opens the gate, so the same hull draws for a camera under the band");
        ctx.Same(0, gate.GatedObjects, $"the open gate reports nothing gated");
    }

    // The binary tests fog-volume membership before the band, so a hull inside an armed volume is
    // zone 3 whatever its altitude. A camera in that volume is state 3 and keeps drawing it.
    private static void InAFogVolume(TestContext ctx, WeatherState weather, float centre)
    {
        var span = new Aabb(new Vector3(-10f, centre + 140f, -60f), new Vector3(20f, 40f, 120f));
        var box = new Aabb(new Vector3(-500f, centre, -500f), new Vector3(1000f, 400f, 1000f));
        var volume = new FogVolumeBox("suite", box, PlanesOf(box));
        var armed = new[] { volume };
        ctx.Same(2, weather.ObjectZone(span, fogZoneArmed: false, NoVolumes),
            $"a disarmed chapter judges the hull by the band alone");
        ctx.Same(3, weather.ObjectZone(span, fogZoneArmed: true, armed),
            $"an armed volume the hull stands in wins over the band, the binary's own first arm");
    }

    private static Plane[] PlanesOf(Aabb box)
    {
        var min = box.Position;
        var max = box.End;
        return new[]
        {
            new Plane(Vector3.Left, -min.X), new Plane(Vector3.Right, max.X),
            new Plane(Vector3.Down, -min.Y), new Plane(Vector3.Up, max.Y),
            new Plane(Vector3.Forward, -min.Z), new Plane(Vector3.Back, max.Z),
        };
    }

    private static int CameraState(WeatherState weather, float altitude) =>
        weather.CameraWeatherState(new Vector3(0f, altitude, 0f), false, NoVolumes);

    private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}

using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The lighting table's night flag on the weather rig. A fixture flags one day world, which is lit under each
/// graphics mode beside the same world unlisted. Its dome draws a moon and no sun. Module entry:
/// docs/architecture/Session.md on LightingTable. ⚠ The graphics mode and the cloud shadow
/// word are put back in a finally, so the rest of the run reads its own.</summary>
internal static class MoonlitMissionSuites
{
    // Where the fixture dome draws its moon, from the dome's origin: 45 degrees up toward -X.
    private static readonly Vector3 MoonAt = new(-400f, 400f, 0f);

    [Suite("moonlit-mission",
        "A day world lighting.json flags as night is lit as night under Enhanced: the fog rule's night energies, "
        + "the light aimed at the dome's moon, the authored fog range and no cloud shadows, while the "
        + "sun keeps casting the aircraft's shadow maps. Under Original the listed world lights exactly "
        + "as unlisted, and an unlisted world is the day control")]
    internal static void MoonlitMission(TestContext ctx)
    {
        var (chapter, mission) = WorldAndToolSuites.WeatherMission(ctx, "C1C", "M01");
        ZrdrSet zrdr = ctx.MissionZrdr(chapter, mission);
        ctx.RequireData(zrdr, $"{chapter}/{mission} mission zrdr");
        if (WeatherState.Load(zrdr) is not { } authored)
        {
            ctx.Check(false, $"{chapter}/{mission} carries a weather.json");
            return;
        }

        var zone = authored.Zone(authored.ResolveZone("zone2", Array.Empty<HorizonZone>()));
        ctx.Check(!WeatherRig.IsNightZone(zone), $"{chapter}/{mission}'s flown zone is a day zone by its fog colour");
        var listed = LightingTable.Parse($"{{ \"{chapter.ToLowerInvariant()}\": {{ \"{mission}\": {{ \"night\": true }} }} }}");
        ctx.Check(listed.Warnings.Count == 0 && listed.Night(chapter, mission),
            $"the fixture table flags {chapter}/{mission} as night ({string.Join("; ", listed.Warnings)})");

        bool wasEnhanced = GraphicsMode.Enhanced;
        var wasWord = CloudShadowSetting.Lookup.Current;
        try
        {
            CloudShadowSetting.Lookup.ResolveFrom(CloudShadowSetting.On, null, new SettingContext(Det: true, Enhanced: true, Panes: 1));
            var moonward = WeatherRig.OrientationToward(MoonAt.Normalized());

            GraphicsMode.Set(true);
            var day = Lit(ctx, chapter, mission, zrdr, LightingTable.None, moonward);
            var night = Lit(ctx, chapter, mission, zrdr, listed, moonward);
            var caps = WeatherRig.EnhancedEnergies(zone with { FogColor = Colors.Black });
            ctx.Check(!day.Night && night.Night, $"Enhanced: unlisted lights as day, listed as night (day {day.Night}, listed {night.Night})");
            ctx.Check(Mathf.IsEqualApprox(night.Sun, caps.Sun) && Mathf.IsEqualApprox(night.Ambient, caps.Ambient),
                $"the listed world takes the fog rule's night energies, sun {night.Sun:0.###} ambient {night.Ambient:0.###} against {caps.Sun:0.###}/{caps.Ambient:0.###}");
            ctx.Check(night.Sun < day.Sun && night.Ambient < day.Ambient,
                $"and the cap bites on its day-level pair (day sun {day.Sun:0.###} ambient {day.Ambient:0.###})");
            ctx.Check(night.Rotation.IsEqualApprox(moonward) && day.Rotation.IsEqualApprox(zone.SunOrientation),
                $"the listed light aims at the moon {night.Rotation} ({moonward}), the unlisted keeps the authored bearing {day.Rotation}");
            var authoredRange = new Vector2(zone.FogNear, zone.FogFar);
            ctx.Check(night.FogRange.IsEqualApprox(authoredRange) && day.FogRange.IsEqualApprox(authoredRange * 2f),
                $"the listed world keeps the authored fog range {night.FogRange}, the unlisted pushes it to {day.FogRange}");
            ctx.Check(!night.CloudShadows && day.CloudShadows,
                $"cloud shadows follow the night rule: off when listed ({night.CloudShadows}), on unlisted ({day.CloudShadows})");
            ctx.Check(night.Casts && night.ShadowReach > 0f,
                $"the listed world's sun still casts the aircraft's shadow maps, out to {night.ShadowReach:0} m");

            GraphicsMode.Set(false);
            var faithful = Lit(ctx, chapter, mission, zrdr, LightingTable.None, moonward);
            var faithfulListed = Lit(ctx, chapter, mission, zrdr, listed, moonward);
            ctx.Check(!faithfulListed.Night && faithfulListed == faithful,
                $"Original lights the listed world exactly as unlisted: {faithfulListed} against {faithful}");
        }
        finally
        {
            GraphicsMode.Set(wasEnhanced);
            CloudShadowSetting.Lookup.Take(wasWord.Word == null ? CloudShadowSetting.FallbackFor(det: false) : wasWord);
        }
    }

    // One weather rig built over the mission under the dome, read back as the lit result.
    private static LitWorld Lit(TestContext ctx, string chapter, string mission, ZrdrSet zrdr, LightingTable list, Vector3 moonward)
    {
        var root = new Node3D { Name = "moonlit-mission" };
        var sun = new DirectionalLight3D { Name = "moonlit-mission-sun" };
        var camera = new Camera3D { Name = "moonlit-mission-camera" };
        var dome = new Node3D { Name = "moonlit-mission-dome" };
        dome.SetMeta(WorldBuilder.MoonCardMeta, MoonAt);
        ctx.Host.AddChild(root);
        root.AddChild(sun);
        root.AddChild(camera);
        root.AddChild(dome);
        try
        {
            ShadowQualitySetting.ApplyTo(sun, ShadowQualitySetting.PlanFor(ShadowQualitySetting.High), GraphicsMode.Enhanced,
                hard: false, renderer: (_, _) => { });
            var authored = WeatherState.Load(zrdr);
            var orientations = new List<Vector3> { moonward };
            foreach (var name in authored?.ZoneNames ?? Array.Empty<string>())
                orientations.Add(authored!.Zone(name).SunOrientation);

            // One opaque card 1000 m up, cast under every bearing the rig can choose.
            var card = new CloudShadowRaster.Caster(new Vector3(0f, 1000f, 0f), 400f, 1f, CloudShadowRaster.Mask.Solid);
            var clouds = CloudShadow.Bake(new[] { card }, orientations, orientations[^1], night: false);
            root.AddChild(clouds);

            var env = new Godot.Environment();
            var rig = new PlayerRig { Index = 0, Camera = camera, HudParent = root };
            var weather = new WeatherRig(SessionSpec.Parse(new[] { $"--chapter={chapter}", $"--mission={mission}" }),
                root, sun, env: env, lighting: list);
            weather.Build(zrdr, new List<PlayerRig> { rig }, Array.Empty<HorizonZone>(),
                _ => rig.HorizonDomes.Add(new HorizonDome(dome, -1)));
            return new LitWorld(weather.NightApplied, sun.LightEnergy, env.AmbientLightEnergy, sun.Rotation,
                weather.FogGlobals.Range, clouds.Draws, sun.ShadowEnabled, sun.DirectionalShadowMaxDistance);
        }
        finally
        {
            root.Free();
        }
    }

    // What one build lit: the night verdict, the energies, the light's bearing and the fog range.
    // Then whether cloud shadows draw, and the sun's shadow maps.
    private readonly record struct LitWorld(bool Night, float Sun, float Ambient, Vector3 Rotation, Vector2 FogRange,
        bool CloudShadows, bool Casts, float ShadowReach);
}

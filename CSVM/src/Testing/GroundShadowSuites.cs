using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The aircraft ground shadow over the flat empty stage and over C1's real terrain: that
/// one lands under the aircraft it belongs to, that it takes the surface height the probe reads,
/// that the player's own runs 1.5 times its altitude ahead while an AI aircraft's sits directly
/// beneath, that the player's own footprint alone grows with altitude, and that it is absent
/// exactly where the decode says nothing is drawn (over the cutoff altitude, past the far range,
/// and in enhanced graphics mode). Decode: docs/org/shadows.md.</summary>
internal static class GroundShadowSuites
{
    // The empty stage's ground plane, which the shadow must land on rather than on y=0 by luck.
    private const float StageGroundY = 0f;

    // Altitudes either side of the ramp, and a range inside and outside the distance fade.
    private const float LowAltitude = 40f;
    private const float MidAltitude = 155f;
    private const float OverCutoff = 400f;
    private const float InRange = 150f;
    private const float OutOfRange = 300f;

    // How close a reading has to be to count. The quad is placed from the drawn pose, so the
    // slack covers the lift off the ground and nothing else.
    private const float PlaceTolerance = 1.5f;

    [Suite("ground-shadow",
        "The projected aircraft ground shadow: under the aircraft on the flat stage and on C1's " +
        "own terrain height, running 1.5 times its altitude ahead of the player while an AI " +
        "aircraft's sits beneath, the player's footprint alone doubling by 155 m, and absent " +
        "over 250 m of altitude, past 200 m of range and under enhanced graphics")]
    internal static void GroundShadow(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var report = new StringBuilder();
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            // Every drawing check below is about the original's own path, so pin the mode rather
            // than inheriting whatever the run was launched in; the run's own mode is restored.
            GraphicsMode.Resolve(GraphicsMode.Default);
            FlatStage(ctx, planesGamez, textures, report);
            Terrain(ctx, planesGamez, textures, report);
            Gate(ctx, report);
        }
        finally
        {
            GraphicsMode.Resolve(wasEnhanced ? GraphicsMode.EnhancedWord : GraphicsMode.Default);
            textures.Dispose();
        }

        ctx.WriteArtifact("test-ground-shadow.txt", report.ToString());
        ctx.Note($"ground shadow: placed under the aircraft on both stages, and dropped where the decode drops it");
    }

    // One aircraft, parked at a pose rather than flown: the pass reads the pose the model is
    // drawn at, so moving the rig between passes is the same input a flown frame gives it.
    private static FlightController Rig(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        bool human, Vector3 at)
    {
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var nose = at + Vector3.Forward;
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = PlaneDamage.For(stats),
            PlayerIndex = human ? FlightRoster.ShooterIdBase - 1 : FlightRoster.ShooterIdBase,
            IsHumanPiloted = human,
            Pilot = human ? null : AiPilot.HoldingCourse(at, nose),
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = AimAssist.PlayerTeam,
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats, aiForcePath: !human), null, new CamParams(), at, nose);
        rig.Name = human ? "shadow_player" : "shadow_ai";
        ctx.Host.AddChild(rig);
        return rig;
    }

    // The flat stage: known ground at y=0 under every pose, so a reading that disagrees is the
    // placement law and never the terrain.
    private static void FlatStage(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        StringBuilder report)
    {
        Node3D? stage = null;
        FlightController? player = null;
        FlightController? ai = null;
        GroundShadowPass? pass = null;
        var players = new List<Vector3>();
        try
        {
            stage = EmptyStage.Build(collision: true).Root;
            ctx.Host.AddChild(stage);
            player = Rig(ctx, planesGamez, textures, human: true, new Vector3(0f, LowAltitude, 0f));
            ai = Rig(ctx, planesGamez, textures, human: false, new Vector3(InRange, LowAltitude, 0f));
            var rigs = new List<FlightController> { player, ai };
            pass = GroundShadowPass.Build(ctx.Host, () => rigs, () => players,
                () => WeatherRig.DefaultSunlightRgb);
            ctx.Check(pass != null, $"original graphics mode builds the ground-shadow pass");
            if (pass == null)
                return;

            players.Add(player.GlobalPosition);
            pass.Tick();
            ctx.Same(2, pass.Drawn, $"both aircraft over the stage cast a shadow");

            // The player's own, running ahead along its own flight direction. Nose is -Z here, so
            // the whole displacement must land in -Z and none of it in X.
            var own = pass.QuadFor(player);
            ctx.Check(own != null, $"the player's own aircraft casts one at {LowAltitude:0} m");
            float lead = -GroundShadowLaw.PlayerSkew * LowAltitude;
            if (own is { } ownQuad)
            {
                var at = ownQuad.GlobalPosition;
                report.AppendLine($"player at 0,{LowAltitude:0},0: shadow ({at.X:0.00}, {at.Y:0.00}, {at.Z:0.00}), expected z {lead:0.00}");
                ctx.Check(Mathf.Abs(at.X) <= PlaceTolerance,
                    $"the player's shadow is not displaced across its flight direction x={at.X:0.00}");
                ctx.Check(Mathf.Abs(at.Z - lead) <= PlaceTolerance,
                    $"and runs 1.5x its altitude ahead of it along that direction z={at.Z:0.00} expected={lead:0.00}");
                ctx.Check(Mathf.Abs(at.Y - StageGroundY) <= PlaceTolerance,
                    $"and lies on the stage's ground y={at.Y:0.00}");
            }

            // An AI aircraft's, which takes none of the player's three special cases.
            var other = pass.QuadFor(ai);
            ctx.Check(other != null, $"another aircraft {InRange:0} m away casts one too");
            float aiWidth = 0f;
            if (other is { } aiQuad)
            {
                var at = aiQuad.GlobalPosition;
                aiWidth = aiQuad.GlobalTransform.Basis.Scale.X;
                report.AppendLine($"ai at {InRange:0},{LowAltitude:0},0: shadow ({at.X:0.00}, {at.Y:0.00}, {at.Z:0.00}), footprint {aiWidth:0.00} m across");
                ctx.Check(Mathf.Abs(at.X - InRange) <= PlaceTolerance && Mathf.Abs(at.Z) <= PlaceTolerance,
                    $"and it sits directly beneath that aircraft ({at.X:0.00}, {at.Z:0.00})");
            }

            float ownWidth = own?.GlobalTransform.Basis.Scale.X ?? 0f;

            // Climb both. The player's footprint grows with the altitude ramp and the AI's does
            // not, which is the size law's whole content.
            player.GlobalPosition = new Vector3(0f, MidAltitude, 0f);
            ai.GlobalPosition = new Vector3(InRange, MidAltitude, 0f);
            players[0] = player.GlobalPosition;
            pass.Tick();
            float ownHigh = pass.QuadFor(player)?.GlobalTransform.Basis.Scale.X ?? 0f;
            float aiHigh = pass.QuadFor(ai)?.GlobalTransform.Basis.Scale.X ?? 0f;
            report.AppendLine($"at {MidAltitude:0} m: player footprint {ownHigh:0.00} m across (was {ownWidth:0.00}), ai {aiHigh:0.00} m (was {aiWidth:0.00})");
            ctx.Check(Mathf.Abs(ownHigh - (2f * ownWidth)) <= 0.2f,
                $"the player's footprint doubles by {MidAltitude:0} m {ownWidth:0.00} -> {ownHigh:0.00} m across");
            ctx.Check(Mathf.Abs(aiHigh - aiWidth) <= 0.2f,
                $"and no other aircraft's grows at all {aiWidth:0.00} -> {aiHigh:0.00} m across");

            // Where the decode draws nothing: over the cutoff altitude, and past the far range.
            player.GlobalPosition = new Vector3(0f, OverCutoff, 0f);
            ai.GlobalPosition = new Vector3(OutOfRange, LowAltitude, 0f);
            players[0] = player.GlobalPosition;
            pass.Tick();
            ctx.Check(pass.QuadFor(player) == null,
                $"nothing is drawn over the {GroundShadowLaw.CutoffAltitude:0} m cutoff altitude");
            ctx.Check(pass.QuadFor(ai) == null,
                $"and nothing past the {GroundShadowLaw.FarDistance:0} m far range");
            ctx.Same(0, pass.Drawn, $"so the stage draws no shadow at all in that pass");
        }
        finally
        {
            pass?.Free();
            player?.Free();
            ai?.Free();
            stage?.Free();
        }
    }

    // C1's own terrain: the same placement, with the surface height coming from the world's
    // colliders rather than from a known plane.
    private static void Terrain(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        StringBuilder report)
    {
        ctx.WithWorld("C1", collision: true, _ =>
        {
            var space = ctx.Host.GetWorld3D().DirectSpaceState;
            Vector3? found = null;
            float surfaceY = 0f;
            // The HIGHEST surface of the probed set, not the first: C1's sea sits at y=0, and a
            // shadow read off a level surface cannot tell a real height from a hardcoded one.
            foreach (var candidate in TerrainProbes())
            {
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    candidate with { Y = 4000f }, candidate with { Y = -1000f },
                    CollisionLayers.World));
                if (hit.Count == 0)
                    continue;
                float y = ((Vector3)hit["position"]).Y;
                if (found != null && y <= surfaceY)
                    continue;
                surfaceY = y;
                found = candidate;
            }

            ctx.Check(found != null, $"C1's built colliders answer a downward ray somewhere over the map");
            ctx.Check(surfaceY > 1f,
                $"and the probed set reaches ground above C1's sea level surface={surfaceY:0.00} m");
            if (found is not { } ground)
                return;

            FlightController? ai = null;
            GroundShadowPass? pass = null;
            try
            {
                var at = ground with { Y = surfaceY + MidAltitude };
                ai = Rig(ctx, planesGamez, textures, human: false, at);
                var rigs = new List<FlightController> { ai };
                var players = new List<Vector3> { at };
                pass = GroundShadowPass.Build(ctx.Host, () => rigs, () => players,
                    () => WeatherRig.DefaultSunlightRgb);
                pass?.Tick();
                var quad = pass?.QuadFor(ai);
                ctx.Check(quad != null, $"an aircraft {MidAltitude:0} m over C1's terrain casts a shadow");
                if (quad is not { } placed)
                    return;
                var shadow = placed.GlobalPosition;
                report.AppendLine($"C1 at ({at.X:0}, {at.Y:0.0}, {at.Z:0}) over a surface at {surfaceY:0.00}: shadow ({shadow.X:0.00}, {shadow.Y:0.00}, {shadow.Z:0.00})");
                ctx.Check(Mathf.Abs(shadow.Y - surfaceY) <= PlaceTolerance,
                    $"on the surface the probe read, not on a level guess y={shadow.Y:0.00} surface={surfaceY:0.00}");
                ctx.Check(Mathf.Abs(shadow.X - at.X) <= PlaceTolerance
                    && Mathf.Abs(shadow.Z - at.Z) <= PlaceTolerance,
                    $"and directly beneath the aircraft ({shadow.X:0.00}, {shadow.Z:0.00}) against ({at.X:0}, {at.Z:0})");
            }
            finally
            {
                pass?.Free();
                ai?.Free();
            }
        });
    }

    // The graphics-mode gate: the shadow is the original's own drawing, and enhanced mode lights
    // and shadows the world itself, so nothing is built there at all.
    private static void Gate(TestContext ctx, StringBuilder report)
    {
        var none = new List<FlightController>();
        var nobody = new List<Vector3>();
        GraphicsMode.Resolve(GraphicsMode.EnhancedWord);
        var absent = GroundShadowPass.Build(ctx.Host, () => none, () => nobody,
            () => WeatherRig.DefaultSunlightRgb);
        ctx.Check(absent == null, $"enhanced graphics mode builds no ground-shadow pass");
        GraphicsMode.Resolve(GraphicsMode.Default);
        var present = GroundShadowPass.Build(ctx.Host, () => none, () => nobody,
            () => WeatherRig.DefaultSunlightRgb);
        ctx.Check(present != null, $"and original graphics mode builds one");
        report.AppendLine($"graphics gate: enhanced={(absent == null ? "no pass" : "a pass")}, original={(present == null ? "no pass" : "a pass")}");
        present?.Free();
    }

    // Where to look for C1 ground: a coarse grid over the map rather than one named spot, so the
    // suite does not depend on any particular place still being an island.
    private static IEnumerable<Vector3> TerrainProbes()
    {
        const float reach = 8000f;
        const float step = 1000f;
        for (float x = -reach; x <= reach; x += step)
        {
            for (float z = -reach; z <= reach; z += step)
                yield return new Vector3(x, 0f, z);
        }
    }
}

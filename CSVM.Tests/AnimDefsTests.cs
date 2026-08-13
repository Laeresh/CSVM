using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The zrdr animation front-end (<c>docs/formats/anim-definitions.md</c>): reader ops normalized
/// into the same event model the compiled archives produce, including the two places where the
/// reader and the compiler disagree on units. Input is <c>fixtures/zrdr/demo_anims.json</c>.
/// </summary>
public class AnimDefsTests
{
    [Theory]
    [InlineData("OBJECT_ACTIVE_STATE", "ObjectActiveState")]
    [InlineData("OBJECT_MOTION_SI_SCRIPT", "ObjectMotionSiScript")]
    [InlineData("IF", "If")]
    [InlineData("ELSEIF", "Elseif")]
    [InlineData("PUFFER_STATE", "PufferState")]
    public void SnakeCaseOpsBecomeTheCompiledPascalCaseTags(string reader, string compiled)
    {
        Assert.Equal(compiled, AnimDefs.PascalCase(reader));
    }

    [Fact]
    public void DefinitionsAreReadWithTheirSourceFileAndHeaderFields()
    {
        var defs = Load();
        Assert.Equal(3, defs.Count);
        var tower = Def("probe_tower");
        Assert.Equal("demo_anims.json", tower.SourceFile);
        Assert.Equal("probe_tower_root", tower.RootName);
        Assert.Equal(250f, tower.Health);
        Assert.True(tower.LocalNodesOnly);
    }

    [Fact]
    public void ActivationIsNormalizedToTheCompiledSpelling()
    {
        Assert.Equal("OnStartup", Def("probe_tower").Activation);
        Assert.Equal("OnCall", Def("probe_sign").Activation);
    }

    [Fact]
    public void AnimNameDefaultsToNameSoAReaderDefDedupesAgainstItsCompiledTwin()
    {
        // Without the mirror the two copies run side by side — the bug that killed the C1
        // waterfall's puffers.
        Assert.Equal("probe_tower", Def("probe_tower").AnimName);
        Assert.Equal("probe_sign_flicker", Def("probe_sign").AnimName); // explicit wins
    }

    [Fact]
    public void SequenceOpsKeepTheirAuthoredOrder()
    {
        var events = Sequence(Def("probe_tower"), "probe_spin").Events;
        var kinds = new List<string>();
        foreach (var e in events)
        {
            kinds.Add(e.Kind);
        }
        Assert.Equal(
            new[]
            {
                "ObjectMotion", "If", "ObjectOpacityState", "Elseif", "PufferState",
                "ObjectAddChild", "Sound", "ObjectRotateState", "ObjectMotionFromTo",
            },
            kinds);
    }

    [Fact]
    public void PlayerRangeIsSquaredOnTheWayInBecauseTheCompilerSquaresIt()
    {
        // Reader metres 270 <-> compiled metres squared 72900.
        var condition = Event(Sequence(Def("probe_tower"), "probe_spin"), "If").Data.Obj("condition")!;
        Assert.Equal(72900f, condition.Num("PlayerRange"));
    }

    [Fact]
    public void TheAnimationLodTokenBecomesItsCompiledNumber()
    {
        var condition = Event(Sequence(Def("probe_tower"), "probe_spin"), "Elseif").Data.Obj("condition")!;
        Assert.Equal(2f, condition.Num("AnimationLod"));
    }

    [Fact]
    public void ObjectMotionDegreesBecomeRadiansInTheCompiledSplitShape()
    {
        var motion = Event(Sequence(Def("probe_tower"), "probe_spin"), "ObjectMotion").Data;
        var spin = motion.Obj("xyz_rotation")!;
        Assert.Equal(Mathf.DegToRad(-40f), spin.Obj("initial")!.Num("z")!.Value, 5);
        Assert.Equal(Mathf.DegToRad(90f), spin.Obj("delta")!.Num("z")!.Value, 5);
        Assert.Equal(3f, motion.Num("run_time"));
        Assert.Equal("probe_rotor", motion.Str("node"));
    }

    [Fact]
    public void ObjectRotateStateDegreesBecomeRadians()
    {
        // Reader rotations are degrees against the compiled form's radians. Unconverted,
        // C2's police_blockade [0,135,0] reached PoseRotate as 135 rad ≈ 21.5 turns.
        var rotate = Event(Sequence(Def("probe_tower"), "probe_spin"), "ObjectRotateState").Data;
        Assert.Equal(Mathf.DegToRad(135f), rotate.Vec3("state").Y, 5);
        Assert.Equal("probe_car", rotate.Str("node"));
    }

    [Fact]
    public void FromToRotateChannelDegreesBecomeRadiansButTranslateStaysMetres()
    {
        // The C2 roadblock swerve: ROTATE 135→100 must arrive as 2.356→1.745 rad, while the
        // translate channel is metres and must pass through untouched.
        var motion = Event(Sequence(Def("probe_tower"), "probe_spin"), "ObjectMotionFromTo").Data;
        var rotate = motion.Obj("rotate")!;
        Assert.Equal(Mathf.DegToRad(135f), rotate.Vec3("from").Y, 5);
        Assert.Equal(Mathf.DegToRad(100f), rotate.Vec3("to").Y, 5);
        var translate = motion.Obj("translate")!;
        Assert.Equal(new Vector3(-5860f, 8f, -4226f), translate.Vec3("from"));
        Assert.Equal(new Vector3(-5862f, 8f, -4229f), translate.Vec3("to"));
        Assert.Equal(0.25f, motion.Num("run_time"));
    }

    [Fact]
    public void OpacityStateReadsTokenAndValueByTypeNotByPosition()
    {
        // The install ships ["ON",0.6] and [0.4,"ON"] alike.
        var opacity = Event(Sequence(Def("probe_tower"), "probe_spin"), "ObjectOpacityState").Data;
        Assert.True(opacity.Bool("state"));
        Assert.Equal(0.4f, opacity.Num("opacity"));
    }

    [Fact]
    public void PufferStateCarriesItsOwnFieldsInTheCompiledShape()
    {
        // A puffer arriving with no active_state defaults to "stop" downstream, so the
        // normalizer must fill every field the compiled form has.
        var puffer = Event(Sequence(Def("probe_tower"), "probe_spin"), "PufferState").Data;
        Assert.Equal(1f, puffer.Num("active_state"));
        Assert.Equal("probe_tower_root", puffer.Str("at_node"));
        Assert.Equal(new Vector3(11f, 8f, -8f), puffer.Vec3("translate"));
        Assert.Equal(2f, puffer.Obj("size_range")!.Num("min"));
        Assert.Equal(6f, puffer.Obj("size_range")!.Num("max"));
        Assert.Equal(4f, puffer.Obj("lifetime_range")!.Num("max"));
        Assert.Equal(new[] { "probe_smoke1", "probe_smoke2" }, TextureNames(puffer));
        // GROWTH_FACTOR is one scalar in the reader; compiled it is the two-stop SCALE_SEQUENCE
        // ramp (0,1),(1,G) under `min`/`max` labels that mean (age, scale). The normalizer must
        // synthesise both stops, or a consumer reading stop #1 gets 1 for every reader puffer.
        Assert.Equal(new[] { (0f, 1f), (1f, 0.25f) }, GrowthStops(puffer));
    }

    [Fact]
    public void LightStateOnlyWritesTheFieldsTheReaderActuallyCarries()
    {
        // Partiality is the semantic that must survive: a flicker event carrying only RANGE
        // must not reset position, colour or active state.
        var light = Event(Sequence(Def("probe_sign"), "probe_flicker"), "LightState").Data;
        Assert.True(light.Bool("active_state"));
        var atNode = light.Obj("translate")!.Obj("AtNode")!;
        Assert.Equal("probe_sign_root", atNode.Str("name"));
        Assert.Equal(new Vector3(0f, 4f, 0f), atNode.Vec3("pos"));
        Assert.Equal(5f, light.Obj("range")!.Num("min"));
        Assert.Equal(40f, light.Obj("range")!.Num("max"));
        Assert.Equal(0.8f, light.Obj("color")!.Num("g"));
    }

    [Fact]
    public void ParentChildBecomesSeparateParentAndChildFields()
    {
        var add = Event(Sequence(Def("probe_tower"), "probe_spin"), "ObjectAddChild").Data;
        Assert.Equal("probe_tower_root", add.Str("parent"));
        Assert.Equal("probe_emitter", add.Str("child"));
    }

    [Fact]
    public void AOneShotSoundNamesTheSoundAndPositionsItAtANode()
    {
        var sound = Event(Sequence(Def("probe_tower"), "probe_spin"), "Sound").Data;
        Assert.Equal("snd_probe_hum", sound.Str("name"));
        Assert.Equal("probe_tower_root", sound.Str("at_node"));
        Assert.Equal(new Vector3(1f, 2f, 3f), sound.Vec3("translate"));
    }

    [Fact]
    public void CallAnimationsTargetNodeDoesNotOverwriteTheCalleeName()
    {
        var call = Event(Sequence(Def("probe_sign"), "probe_flicker"), "CallAnimation").Data;
        Assert.Equal("probe_fire_template", call.Str("name"));
        Assert.Equal("probe_sign_root", call.Obj("parameters")!.Obj("WithNode")!.Str("node"));
    }

    [Fact]
    public void DamageSequenceArrivesAsASequenceOfThatMagicName()
    {
        // The compiled archives deliver the damage cascade as an ordinary sequence literally
        // named DAMAGE_SEQUENCE; the reader front-end must mirror that or the stages are lost.
        var damage = Sequence(Def("probe_tower"), "DAMAGE_SEQUENCE");
        Assert.NotNull(damage);
        var condition = Event(damage, "If").Data.Obj("condition")!;
        Assert.Equal(0.5f, condition.Num("AnimHealth"));
    }

    [Fact]
    public void ASequenceLevelActivationMarksItOnCallOnly()
    {
        Assert.True(Sequence(Def("probe_sign"), "probe_flicker").OnCallOnly);
        Assert.False(Sequence(Def("probe_tower"), "probe_spin").OnCallOnly);
    }

    [Fact]
    public void UnclaimedOpBodiesStayReachableUnderRaw()
    {
        var motion = Event(Sequence(Def("probe_tower"), "probe_spin"), "ObjectMotion").Data;
        Assert.NotNull(motion.List("raw"));
    }

    [Fact]
    public void Name1PairsParseIntoMultiTargetsWithAnEmptyName()
    {
        // The multi-target zeppelin sub-part form (M4 F18): NAME stays empty (the def has no
        // single anchor name), each (pattern, path) pair is preserved, and the first pattern
        // becomes the dedupe AnimName so two NAME1 defs no longer collide on ("", "").
        var def = Load().Find(d => d.MultiTargets.Count > 0)!;
        Assert.Equal("", def.Name);
        Assert.Equal("probe_rtur*", def.AnimName);
        Assert.Equal(2, def.MultiTargets.Count);
        Assert.Equal("probe_rtur*", def.MultiTargets[0].AnimName);
        Assert.Equal(new[] { "probe_zep", "rtur*" }, def.MultiTargets[0].Path);
        Assert.Equal(new[] { "probe_zep", "ltur*" }, def.MultiTargets[1].Path);
        Assert.Equal(10f, def.Health);
    }

    [Fact]
    public void ActivationPrerequisiteParsesMinToSatisfyAndTheAnimList()
    {
        // The zeppelin hull-death gate shape (all_pzep_gasbags: 3 of the finish anims).
        var def = Load().Find(d => d.PrereqAnims.Count > 0)!;
        Assert.Equal(3, def.PrereqMinToSatisfy);
        Assert.Equal(
            new[] { "finish_bag1", "finish_bag2", "finish_bag3", "finish_bag4" },
            def.PrereqAnims);
    }

    private static List<AnimDefinition> Load() => AnimDefs.LoadArchive(TestData.Fixture("zrdr"));

    private static AnimDefinition Def(string name) => Load().Find(d => d.Name == name)!;

    private static AnimSequence Sequence(AnimDefinition def, string name) =>
        def.Sequences.Find(s => s.Name == name)!;

    private static AnimEvent Event(AnimSequence sequence, string kind) =>
        sequence.Events.Find(e => e.Kind == kind)!;

    private static string[] TextureNames(AnimData puffer)
    {
        var names = new List<string>();
        foreach (var t in puffer.Objects("textures"))
        {
            names.Add(t.Str("name")!);
        }
        return names.ToArray();
    }

    private static (float Age, float Scale)[] GrowthStops(AnimData puffer)
    {
        var stops = new List<(float Age, float Scale)>();
        foreach (var g in puffer.Objects("growth_factors"))
        {
            stops.Add((g.Num("min") ?? 0f, g.Num("max") ?? 0f));
        }
        return stops.ToArray();
    }
}

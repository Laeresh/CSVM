using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Tests.Oracle;
using Godot;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace CSVM.Tests;

/// <summary>
/// The model-diff harness, seen to fail before any equal result is believed. Each test seeds one
/// difference at some depth of a loader model type and expects it named by its path.
/// </summary>
[Trait("Tier", "Quick")]
public class ModelDiffTests
{

    [Fact]
    public void AModelBuiltTwiceFromAFixtureIsEqualAndCountsItsLeaves()
    {
        Assert.True(ModelDiff.AssertEqual(Fixture(), Fixture()) > 100);
    }

    [Fact]
    public void AFlippedLowBitInATransformIsNamedByItsPath()
    {
        var expected = Fixture();
        var actual = Fixture();
        var node = actual.Nodes[3];
        var local = node.Local!.Value;
        local.Origin.Y = Flip(local.Origin.Y);
        node.Local = local;

        var diff = ModelDiff.FirstDifference(expected, actual);
        Assert.StartsWith("GameZ.Nodes[3].Local.Origin.Y: expected ", diff);
    }

    [Fact]
    public void AFlippedVertexBitDeepInAMeshIsNamedByItsPath()
    {
        var expected = SmallWorld();
        var actual = SmallWorld();
        var v = actual.Meshes[0].Vertices[1];
        actual.Meshes[0].Vertices[1] = new Vector3(v.X, Flip(v.Y), v.Z);

        Assert.Equal(
            "GameZ.Meshes[0].Vertices[1].Y: expected 2 (0x40000000), actual 2.0000002 (0x40000001)",
            ModelDiff.FirstDifference(expected, actual));
    }

    [Fact]
    public void AListOneShorterIsReportedAsItsCount()
    {
        var actual = SmallWorld();
        actual.Meshes[0].Polygons[0].VertexIndices.RemoveAt(2);

        Assert.Equal(
            "GameZ.Meshes[0].Polygons[0].VertexIndices.Count: expected 3, actual 2",
            ModelDiff.FirstDifference(SmallWorld(), actual));
    }

    [Theory]
    [InlineData(0xFFC00000u, 0x7FC00000u)] // two NaNs, different sign bit
    [InlineData(0xFFC00000u, 0xFFC00001u)] // two NaNs, different payload
    [InlineData(0x00000000u, 0x80000000u)] // zero and negative zero
    public void FloatsThatCompareEqualOrAreBothNaNStillDifferByTheirBits(uint expectedBits, uint actualBits)
    {
        var diff = ModelDiff.FirstDifference(Script(expectedBits), Script(actualBits));
        Assert.StartsWith("SiScript.Frames[0].Translate.X.Value: expected ", diff);
        Assert.Contains($"(0x{actualBits:X8})", diff);
    }

    [Fact]
    public void TheSameNaNPayloadOnBothSidesIsEqual()
    {
        // The shipped spline blocks hold 0xFFC00000; an oracle must not fail against itself.
        Assert.Null(ModelDiff.FirstDifference(Script(0xFFC00000u), Script(0xFFC00000u)));
    }

    [Fact]
    public void ARenamedKeyInAPrivateDictionaryIsReportedMissing()
    {
        Assert.Null(ModelDiff.FirstDifference(AnimDef("time"), AnimDef("time")));
        Assert.Equal(
            "AnimDefinition.Sequences[0].Events[0].Data._props[\"time\"]: missing from actual",
            ModelDiff.FirstDifference(AnimDef("time"), AnimDef("Time")));
    }

    [Fact]
    public void AKeyDifferingOnlyInCaseIsADifferenceUnderACaseBlindComparer()
    {
        var expected = AnimDef("time");
        var actual = AnimDef("time");
        expected.NodeRefs["hull"] = 4;
        actual.NodeRefs["Hull"] = 4;

        Assert.Equal(
            "AnimDefinition.NodeRefs[\"hull\"]: missing from actual",
            ModelDiff.FirstDifference(expected, actual));
    }

    [Fact]
    public void AnExtraKeyAndADifferentComparerAreBothReported()
    {
        var expected = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 };
        var extra = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1, ["b"] = 2 };
        var blind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["a"] = 1 };

        Assert.Equal("Dictionary[\"b\"]: not in expected", ModelDiff.FirstDifference(expected, extra));
        Assert.StartsWith("Dictionary.Comparer: expected ", ModelDiff.FirstDifference(expected, blind));
    }

    [Fact]
    public void BoxedValuesOfDifferentTypesAndDoubleBitsAreReported()
    {
        var expected = new List<object?> { 1f, new List<object?> { 2.0 } };
        var asDouble = new List<object?> { 1.0, new List<object?> { 2.0 } };
        var nudged = new List<object?> { 1f, new List<object?> { BitConverter.Int64BitsToDouble(0x4000000000000001) } };

        Assert.Equal(
            "List[0]: expected 1 (0x3F800000) (Single), actual 1 (0x3FF0000000000000) (Double)",
            ModelDiff.FirstDifference(expected, asDouble));
        Assert.StartsWith("List[1][0]: expected 2 (0x4000000000000000), actual ", ModelDiff.FirstDifference(expected, nudged));
    }

    [Fact]
    public void ARecordsListElementAndANullSlotAreNamed()
    {
        var expected = AnimDef("time");
        var actual = AnimDef("time");
        expected.PrereqNodes.Add(new AnimNodePrereq(new[] { "zep", "hull" }, true, true));
        actual.PrereqNodes.Add(new AnimNodePrereq(new[] { "zep", "hul1" }, true, true));
        Assert.Equal(
            "AnimDefinition.PrereqNodes[0].Path[1]: expected \"hull\", actual \"hul1\"",
            ModelDiff.FirstDifference(expected, actual));

        actual = AnimDef("time");
        actual.ResetState = new AnimSequence();
        Assert.StartsWith(
            "AnimDefinition.ResetState: expected null, actual ",
            ModelDiff.FirstDifference(AnimDef("time"), actual));
    }

    [Fact]
    public void ACycleTerminatesAndADifferenceInsideItIsStillFound()
    {
        Assert.Null(ModelDiff.FirstDifference(Ring(2), Ring(2)));
        Assert.Equal("Ring.Next.Value: expected 2, actual 3", ModelDiff.FirstDifference(Ring(2), Ring(3)));
    }

    [Fact]
    public void APixelBufferNamesItsFirstDifferingByte()
    {
        var expected = TgaImage.FromRgba(1, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var actual = TgaImage.FromRgba(1, 2, new byte[] { 1, 2, 3, 4, 5, 9, 7, 8 });
        Assert.Equal("TgaImage.Rgba[5]: expected 6, actual 9", ModelDiff.FirstDifference(expected, actual));
    }

    [Fact]
    public void ReadOnlyMemoryComparesByContent()
    {
        var expected = new ReadOnlyMemory<byte>(new byte[] { 0, 1, 2, 3 }, 1, 2);
        Assert.Null(ModelDiff.FirstDifference(expected, new ReadOnlyMemory<byte>(new byte[] { 1, 2 })));
        Assert.Equal(
            "ReadOnlyMemory[1]: expected 2, actual 7",
            ModelDiff.FirstDifference(expected, new ReadOnlyMemory<byte>(new byte[] { 1, 7 })));
    }

    [Fact]
    public void LazyCachesAreSkippedAndASkipNamesAMemberOrAPath()
    {
        var expected = SmallWorld();
        var actual = SmallWorld();
        actual.IsMarkerGizmo(0); // builds the private cache on one side only
        Assert.Null(ModelDiff.FirstDifference(expected, actual));

        actual.Meshes[0].Fog = false;
        Assert.NotNull(ModelDiff.FirstDifference(expected, actual));
        Assert.Null(ModelDiff.FirstDifference(expected, actual, "GameZMesh.Fog"));
        Assert.Null(ModelDiff.FirstDifference(expected, actual, "GameZ.Meshes[0].Fog"));
    }

    [Fact]
    public void AssertEqualFailsWithThePath()
    {
        var actual = SmallWorld();
        actual.Meshes[0].Polygons[0].VertexIndices[1] = 7;
        var failure = Assert.ThrowsAny<XunitException>(() => ModelDiff.AssertEqual(SmallWorld(), actual));
        Assert.Contains("GameZ.Meshes[0].Polygons[0].VertexIndices[1]: expected 1, actual 7", failure.Message);
    }

    internal static float Flip(float f) => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(f) ^ 1);

    private static GameZ Fixture() => GameZ.Load(TestData.Fixture("gamez-plane"));

    private static GameZ SmallWorld()
    {
        var gz = new GameZ();
        var mesh = new GameZMesh();
        mesh.Vertices.AddRange(new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f) });
        var polygon = new GameZPolygon { MaterialIndex = 0, UvCoords = new() { Vector2.Zero, Vector2.One, Vector2.Up } };
        polygon.VertexIndices.AddRange(new[] { 0, 1, 2 });
        mesh.Polygons.Add(polygon);
        gz.Meshes.Add(mesh);
        gz.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "probe_box", MeshIndex = 0 });
        gz.Materials.Add(new GameZMaterial { TextureName = "probe.tif", Color = new Color(0.5f, 0.25f, 1f) });
        return gz;
    }

    private static SiScript Script(uint bits)
    {
        var frame = new SiFrame
        {
            EndTime = 1f,
            Translate = new SiVectorChannel { X = new SiCubic(BitConverter.UInt32BitsToSingle(bits), 0f, 0f, 0f) },
        };
        var script = new SiScript { ScriptName = "probe" };
        script.Frames.Add(frame);
        return script;
    }

    private static AnimDefinition AnimDef(string key)
    {
        var def = new AnimDefinition { Name = "probe_def" };
        var seq = new AnimSequence { Name = "s" };
        seq.Events.Add(new AnimEvent
        {
            Kind = "Sound",
            Data = new AnimData(new Dictionary<string, object?> { [key] = 1.5f, ["name"] = "snd" }),
        });
        def.Sequences.Add(seq);
        return def;
    }

    private static Ring Ring(int second)
    {
        var a = new Ring { Value = 1 };
        a.Next = new Ring { Value = second, Next = a };
        return a;
    }
}

/// <summary>
/// The harness over the extraction. Real models built twice compare equal and count their leaves,
/// and one flipped bit in a whole chapter world is found by its path.
/// </summary>
public class ModelDiffDataTests
{
    private readonly ITestOutputHelper _output;

    public ModelDiffDataTests(ITestOutputHelper output) => _output = output;

    /// <summary>One label per real model, each built twice from the extraction by <see cref="Build"/>.</summary>
    public static TheoryData<string> RealModels => new() { "gamez C1", "cam_anim C1", "mis_anim C5/M02", "zrdr shared", "messages" };

    [ExtractedDataTheory]
    [MemberData(nameof(RealModels))]
    public void ARealModelBuiltTwiceShowsNoDifference(string model)
    {
        int compared = ModelDiff.AssertEqual(Build(model), Build(model));
        _output.WriteLine($"{model}: {compared} leaf values compared");
        Assert.True(compared > 1000);
    }

    [ExtractedDataFact]
    public void OneFlippedBitInAWholeChapterWorldIsFound()
    {
        var expected = (GameZ)Build("gamez C1");
        var actual = (GameZ)Build("gamez C1");
        int mesh = actual.Meshes.FindLastIndex(m => m.Vertices.Count > 10);
        var v = actual.Meshes[mesh].Vertices[7];
        actual.Meshes[mesh].Vertices[7] = new Vector3(v.X, v.Y, ModelDiffTests.Flip(v.Z));

        Assert.StartsWith($"GameZ.Meshes[{mesh}].Vertices[7].Z: expected ", ModelDiff.FirstDifference(expected, actual));
    }

    private static object Build(string model)
    {
        string root = TestData.ExtractedRoot!;
        switch (model)
        {
            case "gamez C1":
                return GameZ.Load(SessionPaths.ChapterGamez(TestData.ExtractedRoot!, "C1"));
            case "cam_anim C1":
            case "mis_anim C5/M02":
                // C5/M02 holds the one script frame whose floats are NaN (carneypkup_cam).
                var archive = model == "cam_anim C1" ? Path.Combine(root, "C1", "cam_anim.zip")
                    : Path.Combine(root, "C5", "M02", "mis_anim.zip");
                var anim = AnimArchive.Load(SessionPaths.PreferUnzipped(archive), model)!;
                for (int i = 0; i < anim.ScriptCount; i++)
                {
                    anim.Script(i); // the scripts load lazily; both sides need the same set loaded
                }

                return anim;
            case "zrdr shared":
                return Zrdr.LoadFilesNamed(SessionPaths.PreferUnzipped(Path.Combine(root, "zrdr.zip")), _ => true)
                    .ToDictionary(f => f.Name, f => f.Root);
            case "messages":
                return MessagesLegacy.Load(Path.Combine(root, "messages.json"));
            default:
                throw new ArgumentException(model);
        }
    }
}

/// <summary>A two-node reference cycle, the shape of a definition pointing back at its archive.</summary>
internal sealed class Ring
{
    public Ring? Next;
    public int Value;
}

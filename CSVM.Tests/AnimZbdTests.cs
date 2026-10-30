using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Mech3;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// <see cref="AnimZbd.Read"/> over every chapter's <c>cam_anim.zbd</c> and every mission's
/// <c>mis_anim.zbd</c> against the model today's extraction loads.
/// Layout: docs/formats/anim-definitions/compiled-archives.md.
/// </summary>
public class AnimZbdTests
{
    // The archive's own folder differs by construction: the extraction reads a path, the reader bytes.
    private static readonly string[] SourceSkips = { "AnimArchive._path", "AnimArchive._isDir" };

    private readonly ITestOutputHelper _output;

    public AnimZbdTests(ITestOutputHelper output) => _output = output;

    /// <summary>The 61 archives: each chapter's <c>cam_anim</c> and each mission's <c>mis_anim</c>.</summary>
    public static IEnumerable<object[]> AnimArchives() =>
        ZbdManifest.Expected.Where(IsAnim).Select(path => new object[] { path });

    [Fact]
    public void ThereAreSixtyOneAnimArchives()
    {
        Assert.Equal(61, ZbdManifest.Expected.Count(IsAnim));
    }

    /// <summary>The install's chapter loader. A repeat of one mission shares its two archives. A
    /// second mission decodes its own pair, since the program writes onto the defs it is handed,
    /// but keeps the chapter's world. A mission folder without an archive reads null.</summary>
    [GameFilesFact]
    public void TheChapterLoaderHoldsTheLastMissionsArchivesUnderTheirLabels()
    {
        var load = CSVM.Spec.GameData.Load(GameFilesData.Source).LoadChapter;

        var m02 = load("C1", "M02");
        var again = load("c1", "m02");
        var m04 = load("C1", "M04");

        Assert.Same(m02.ChapterAnim, again.ChapterAnim);
        Assert.Same(m02.MissionAnim, again.MissionAnim);
        Assert.NotSame(m02.ChapterAnim, m04.ChapterAnim);
        Assert.Same(m02.GamezSource, m04.GamezSource);
        Assert.Equal(("cam_anim", "mis_anim"), (m04.ChapterAnim!.Label, m04.MissionAnim!.Label));
        Assert.Equal(m02.ChapterAnim!.Defs.Count, m04.ChapterAnim.Defs.Count);
        Assert.Null(load("C1", "M01").MissionAnim);
    }

    [Fact]
    public void ARepeatedNameTakesTheFirstFreeNumberBeforeItsExtension()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Assert.Equal(new[] { "a.zan", "a-1.zan", "x", "x-1", "x-2", "X" },
            new[] { "a.zan", "a.zan", "x", "x", "x", "X" }.Select(n => AnimZbd.Rename(seen, n)));
    }

    [Theory]
    [InlineData("..\\data\\c1\\zrdr\\fueltruck1.zan", "fuel_truck01", "data-c1-zrdr-fueltruck1-fuel_truck01.zan")]
    [InlineData("plain", "obj", "plain-obj.zan")]
    public void AScriptIsNamedByItsSourcePathAndObject(string script, string obj, string expected)
    {
        Assert.Equal(expected, AnimZbd.ScriptFileName(script, obj));
    }

    [Fact]
    public void AHandEncodedArchiveReadsIntoTheModel()
    {
        var archive = AnimZbd.Read(TinyArchive(), "T/cam_anim");

        var def = Assert.Single(archive.Defs);
        Assert.Same(archive, def.Archive);
        Assert.Equal(("prop", "spin", "prop", "OnStartup", "prop-spin"), (def.Name, def.AnimName, def.RootName, def.Activation, def.SourceFile));
        Assert.Equal((true, 0f, 2500f, 5f), (def.ByRange, def.RangeMin, def.RangeMax, def.Health));
        Assert.Equal(new[] { "rotor", "rotor~2" }, def.NodeList);
        Assert.Equal((8, 9, 7), (def.NodeRefs["rotor"], def.NodeRefs["rotor~2"], def.NodeRefs["prop"]));
        Assert.Equal(new[] { 0 }, def.SiScriptIds);
        var prereq = Assert.Single(def.PrereqNodes);
        Assert.Equal(new[] { "door" }, prereq.Path);
        Assert.Equal((false, true, (int?)11), (prereq.Active, prereq.Required, prereq.Ptr));

        var reset = Assert.Single(def.ResetState!.Events);
        Assert.Equal(("ObjectActiveState", "rotor~2", (bool?)false), (reset.Kind, reset.Data.Str("node"), reset.Data.Get("state") as bool?));

        var seq = Assert.Single(def.Sequences);
        Assert.Equal(("run", false), (seq.Name, seq.OnCallOnly));
        Assert.Equal(new[] { "ObjectMotionSiScript", "Sound", "If", "Loop", "Else", "Endif" }, seq.Events.Select(e => e.Kind));
        var motion = seq.Events[0];
        Assert.Equal(("Sequence", 1.5f, "rotor", (float?)0f), (motion.StartOffset, motion.StartTime, motion.Data.Str("name"), motion.Data.Num("index")));
        Assert.Null(seq.Events[1].StartOffset);
        Assert.Equal(("whirr", "INPUT_NODE"), (seq.Events[1].Data.Str("name"), seq.Events[1].Data.Obj("at_node")!.Str("name")));
        Assert.Equal(100f, seq.Events[2].Data.Obj("condition")!.Num("PlayerRange"));
        Assert.Equal(-1f, seq.Events[3].Data.Num("Count"));

        var script = archive.Script(0)!;
        Assert.Equal(("..\\zan\\spin.zan", "rotor", false), (script.ScriptName, script.ObjectName, script.SplineInterp));
        Assert.Equal(2, script.Frames.Count);
        var translate = script.Frames[0].Translate!;
        Assert.Equal((new Godot.Vector3(1, 2, 3), Godot.Vector3.Zero), (translate.Base, translate.Delta)); // the NaN delta reads +0
        Assert.Equal(0, BitConverter.SingleToInt32Bits(translate.Delta.X));
        Assert.Null(script.Frames[1].Translate);
        Assert.Equal(2f, script.Duration);
    }

    [Fact]
    public void ATrailingByteIsRefused()
    {
        var data = TinyArchive().Append((byte)0).ToArray();
        Assert.Contains("end of file", Assert.Throws<InvalidDataException>(() => AnimZbd.Read(data, "T")).Message);
    }

    [Fact]
    public void AnUnknownEventTypeIsRefused()
    {
        var data = TinyArchive();
        int at = new ReadOnlySpan<byte>(data).IndexOf(new byte[] { 12, 2, 0, 0, 76 }); // the script motion's header
        data[at] = 3; // EFFECT, which no shipped archive holds
        Assert.Contains("event type 3", Assert.Throws<InvalidDataException>(() => AnimZbd.Read(data, "T")).Message);
    }

    [Fact]
    public void NoSourceReadsAsNoArchive()
    {
        Assert.Null(AnimZbd.Load(null, "ZBD/C1/cam_anim.zbd", "C1/cam_anim"));
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [MemberData(nameof(AnimArchives))]
    public void TheReaderBuildsTheModelTheExtractionBuilds(string path)
    {
        var bytes = GameFilesData.Source!.ReadAllBytes("ZBD/" + path);
        var clock = Stopwatch.StartNew();
        var actual = AnimZbd.Read(bytes, Label(path));
        double decodeMs = clock.Elapsed.TotalMilliseconds;
        var expected = FromExtraction(path);

        int compared = ModelDiff.AssertEqual(expected, actual, SourceSkips);
        _output.WriteLine($"{path}: {actual.Defs.Count} defs, {actual.ScriptCount} scripts, {bytes.Length} bytes, "
                          + $"decoded in {decodeMs:F1} ms; {compared} leaf values compared");
        Assert.True(compared > 0);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("C1/cam_anim.zbd")]
    public void OneChangedFieldInTheDirectModelIsFound(string path)
    {
        var expected = FromExtraction(path);
        var bytes = GameFilesData.Source!.ReadAllBytes("ZBD/" + path);

        var actual = AnimZbd.Read(bytes, Label(path));
        actual.Script(3)!.Frames[1].StartTime += 1e-3f;
        Assert.Contains(".Frames[1].StartTime: expected ", ModelDiff.FirstDifference(expected, actual, SourceSkips));

        actual = AnimZbd.Read(bytes, Label(path));
        var (d, s, e) = FirstScriptMotion(actual);
        var ev = actual.Defs[d].Sequences[s].Events[e];
        ev.Data = new AnimData(new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = ev.Data.Str("name"), ["index"] = ev.Data.Num("index") + 1 });
        Assert.StartsWith($"AnimArchive.Defs[{d}].Sequences[{s}].Events[{e}].Data.", ModelDiff.FirstDifference(expected, actual, SourceSkips));
    }

    private static bool IsAnim(string path) => Path.GetFileName(path) is "cam_anim.zbd" or "mis_anim.zbd";

    private static (int Def, int Seq, int Event) FirstScriptMotion(AnimArchive archive)
    {
        for (int d = 0; d < archive.Defs.Count; d++)
        {
            for (int s = 0; s < archive.Defs[d].Sequences.Count; s++)
            {
                int e = archive.Defs[d].Sequences[s].Events.FindIndex(x => x.Kind == "ObjectMotionSiScript");
                if (e >= 0)
                {
                    return (d, s, e);
                }
            }
        }

        throw new InvalidOperationException("no script motion");
    }

    // One definition: two nodes of one name, an object, a static sound, a node prerequisite, a
    // reset sequence and six events. One non-spline script, whose first delta is NaN.
    private static byte[] TinyArchive()
    {
        var w = new Writer();
        w.U32(0x08170616).U32(53).U32(0).U32(0);
        int info = w.Pos;
        w.Fill(108);
        w.Fill(272); // the reserved placeholder definition

        int def = w.Pos;
        w.Fill(272);
        w.Fill(92).Name("prop", 32).U32(0).U32(7).Fill(52);
        w.Fill(44).U32(0).Name("rotor", 32).U32(0).U32(8);
        w.U32(0).Name("rotor", 32).U32(0).U32(9);
        w.Fill(40).Name("whirr", 40);
        w.U32(0).U32(2).U32(2).Name("door", 32).U32(11);
        w.U32(0);

        w.Name("RESET_SEQUENCE", 32).Fill(28).U32(20);
        w.Event(6, 1, 8, 0f).U32(0).I16(2).I16(0);

        var events = new Writer();
        events.Event(12, 2, 64, 1.5f).U32(0).U32(1).U32(0).Fill(52);
        events.Event(1, 1, 16, 0f).I16(1).I16(-200).F32(1).F32(2).F32(3);
        events.Event(31, 1, 16, 0f).U32(2).U32(0).F32(100).U32(0);
        events.Event(30, 1, 8, 0f).U32(1).I16(-1).I16(0);
        events.Event(32, 1, 0, 0f);
        events.Event(34, 1, 0, 0f);
        w.Name("run", 32).Fill(28).U32((uint)events.Pos).Raw(events.ToArray());

        var frames = new Writer();
        frames.U32(1).F32(0).F32(1).F32(1).F32(2).F32(3).U32(0).F32(float.NaN).F32(0).F32(0).Fill(48);
        frames.U32(0).F32(1).F32(2);
        byte[] scriptName = System.Text.Encoding.ASCII.GetBytes("..\\zan\\spin.zan\0"), objectName = System.Text.Encoding.ASCII.GetBytes("rotor\0");
        w.U32(1).U32(1).Raw(new[] { (byte)scriptName.Length, (byte)objectName.Length, (byte)0, (byte)0 }).U32(0).U32(2).U32((uint)frames.Pos).U32(1);
        w.Raw(scriptName).Raw(objectName).Raw(frames.ToArray());

        var data = w.ToArray();
        data[info + 10] = 2; // two definitions, the placeholder included
        data[info + 16] = 1; // one script
        new Writer().Name("spin", 32).ToArray().CopyTo(data, def);
        new Writer().Name("prop", 32).ToArray().CopyTo(data, def + 40);
        new Writer().Name("prop", 32).ToArray().CopyTo(data, def + 76);
        data[def + 156] = 0x02; // execution by range
        data[def + 161] = 4; // OnStartup
        BitConverter.GetBytes(2500f).CopyTo(data, def + 168);
        BitConverter.GetBytes(5f).CopyTo(data, def + 180);
        data[def + 208] = 1; // a reset sequence follows
        byte[] counts = { 1, 2, 3, 0, 0, 0, 2, 0, 1, 1, 0, 1 }; // sequences .. script ids, at +216
        counts.CopyTo(data, def + 216);
        return data;
    }

    private static string Label(string path) => path[..^".zbd".Length];

    private static AnimArchive FromExtraction(string path)
    {
        var archive = AnimArchive.Load(SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, Label(path) + ".zip")), Label(path))!;
        for (int i = 0; i < archive.ScriptCount; i++)
        {
            archive.Script(i); // the scripts load lazily; both sides need the same set loaded
        }

        return archive;
    }

    private sealed class Writer
    {
        private readonly List<byte> _bytes = new();

        public int Pos => _bytes.Count;

        public byte[] ToArray() => _bytes.ToArray();

        public Writer Fill(int n) => Raw(new byte[n]);

        public Writer Name(string name, int width)
        {
            var bytes = new byte[width];
            System.Text.Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
            return Raw(bytes);
        }

        public Writer I16(short v) => Raw(BitConverter.GetBytes(v));

        public Writer U32(uint v) => Raw(BitConverter.GetBytes(v));

        public Writer F32(float v) => Raw(BitConverter.GetBytes(v));

        public Writer Event(byte type, byte offset, int payload, float time) =>
            Raw(new[] { type, offset, (byte)0, (byte)0 }).U32((uint)(12 + payload)).F32(time);

        public Writer Raw(byte[] b)
        {
            _bytes.AddRange(b);
            return this;
        }
    }
}

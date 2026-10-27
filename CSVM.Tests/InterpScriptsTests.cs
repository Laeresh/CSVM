using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// <see cref="InterpScripts.Read"/> over hand-encoded files (CI-safe), and over the install's
/// <c>interp.zbd</c> against the model the extraction's <c>interp.json</c> builds
/// (<see cref="InterpScripts.FromExtracted"/>, the modding path). Layout: docs/formats/interp.md.
/// </summary>
public class InterpScriptsTests
{
    private readonly ITestOutputHelper _output;

    public InterpScriptsTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AnEncodedFileReadsBackItsNamesAndLinesInOrder()
    {
        var scripts = InterpScripts.Read(Encode(
            ("support\\c9\\adjust.gw", new[] { "MipBias -0.8", "AddClutterTemplates a b c" }),
            ("support\\c9\\ia1.gw", new[] { "Quit" }),
            ("support\\empty.gw", Array.Empty<string>())));

        Assert.Equal(new[] { "support\\c9\\adjust.gw", "support\\c9\\ia1.gw", "support\\empty.gw" }, scripts.Scripts.Select(s => s.Name));
        Assert.Equal(new[] { "MipBias -0.8", "AddClutterTemplates a b c" }, scripts.Lines("SUPPORT\\C9\\ADJUST.GW"));
        Assert.Empty(scripts.Lines("support\\empty.gw")!);
        Assert.Null(scripts.Lines("support\\c9\\m02.gw"));
    }

    [Fact]
    public void NoSourceReadsAsNoScripts()
    {
        Assert.Null(InterpScripts.Load(null));
    }

    [Fact]
    public void AWrongSignatureIsRefused()
    {
        var data = Encode(("support\\a.gw", new[] { "Quit" }));
        data[0] ^= 1;
        Assert.Contains("signature", Assert.Throws<InvalidDataException>(() => InterpScripts.Read(data)).Message);
    }

    /// <summary>The argument count is the number of nulls in the line. A count that disagrees
    /// means the line was not split the way the format splits it.</summary>
    [Fact]
    public void AnArgumentCountTheNullsDoNotMatchIsRefused()
    {
        var data = Encode(("support\\a.gw", new[] { "FindNode hk_zep" }));
        data[12 + 128 + 4] = 3;
        Assert.Contains("argument count", Assert.Throws<InvalidDataException>(() => InterpScripts.Read(data)).Message);
    }

    [Fact]
    public void BytesPastTheLastScriptAreRefused()
    {
        var data = Encode(("support\\a.gw", new[] { "Quit" })).Append((byte)0).ToArray();
        Assert.Contains("end of file", Assert.Throws<InvalidDataException>(() => InterpScripts.Read(data)).Message);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData(InterpScripts.GamePath)]
    public void TheReaderBuildsTheModelTheExtractionBuilds(string path)
    {
        var (expected, actual) = Both(path);
        int compared = ModelDiff.AssertEqual(expected, actual);
        _output.WriteLine($"{path}: {actual.Scripts.Count} scripts, {compared} leaf values compared");
        Assert.True(compared > 0);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData(InterpScripts.GamePath)]
    public void OneChangedCharacterInTheDirectModelIsFound(string path)
    {
        var (expected, actual) = Both(path);
        int script = actual.Scripts.Count - 1;
        var lines = (List<string>)actual.Scripts[script].Lines;
        lines[0] += "x";

        Assert.StartsWith($"InterpScripts._scripts[{script}].Lines[0]: expected ", ModelDiff.FirstDifference(expected, actual));
    }

    private static (InterpScripts Expected, InterpScripts Actual) Both(string path) =>
        (InterpScripts.FromExtracted(Path.Combine(TestData.ExtractedRoot!, "interp.json")),
         InterpScripts.Read(GameFilesData.Source!.ReadAllBytes(path)));

    // The layout of docs/formats/interp.md: header, one 128-byte entry per script, then each
    // script's lines (size, argument count, null-separated text) ending in a zero size.
    private static byte[] Encode(params (string Name, string[] Lines)[] scripts)
    {
        var bodies = scripts.Select(s =>
        {
            var body = new MemoryStream();
            var w = new BinaryWriter(body);
            foreach (var line in s.Lines)
            {
                var text = Encoding.ASCII.GetBytes(line + " ");
                w.Write(text.Length);
                w.Write(text.Count(b => b == ' '));
                w.Write(text.Select(b => b == ' ' ? (byte)0 : b).ToArray());
            }

            w.Write(0);
            return body.ToArray();
        }).ToList();

        var file = new MemoryStream();
        var writer = new BinaryWriter(file);
        writer.Write(0x08971119u);
        writer.Write(7u);
        writer.Write(scripts.Length);
        int start = 12 + (128 * scripts.Length);
        for (int i = 0; i < scripts.Length; i++)
        {
            var name = new byte[120];
            Encoding.ASCII.GetBytes(scripts[i].Name).CopyTo(name, 0);
            writer.Write(name);
            writer.Write(0u);
            writer.Write(start);
            start += bodies[i].Length;
        }

        bodies.ForEach(writer.Write);
        return file.ToArray();
    }
}

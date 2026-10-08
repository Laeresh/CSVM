using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSVM.Extraction;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic data root behind <c>--synthetic-data</c>: an <c>extracted/</c> tree of invented
/// records and code-generated files. It is written at run time, so every loader takes its normal
/// path with no install. The hand-authored records are the <c>CSVM.Tests/fixtures/</c> files, read
/// from the repo checkout: the tests and the engine share one copy, and an export carries none.
/// Each record family is one entry in <see cref="Families"/> or, for the Original shell, in
/// <c>SyntheticShell.TreeFamilies</c>. The tree's stamp carries a
/// <see cref="StampField"/> field, which is how <see cref="Marks"/> tells a synthetic tree apart.
/// Engine-free, so <c>CSVM.Tests</c> builds the same tree the engine does.
/// </summary>
public static class SyntheticData
{
    /// <summary>The stamp field naming the tree synthetic, beside the <c>schema</c> every
    /// extraction writes. The boot stamp check reads only <c>schema</c>, so the field is inert there.</summary>
    public const string StampField = "synthetic";

    /// <summary>The folder under the repo's <c>.scratch/</c> that holds one tree per process.</summary>
    public const string ScratchFolder = "synthetic-data";

    /// <summary>The record families written from this namespace, in this order. Adding a family is one
    /// entry here and one writer that touches only its own folder under <c>extracted/</c>. The
    /// Original shell's family reads UI types, so it joins in <c>SyntheticShell.TreeFamilies</c>.</summary>
    public static readonly IReadOnlyList<SyntheticFamily> Families = new SyntheticFamily[]
    {
        new("chapter-textures", SyntheticTextures.WriteChapter),
        new("plane", SyntheticPlane.WritePlane),
        new("armament", SyntheticPlane.WriteArmament),
        new("sounds", SyntheticSounds.WriteArchive),
        new("voice", SyntheticSounds.WriteVoice),
        new("mission", SyntheticMission.WriteMission),
        new("effects", SyntheticEffects.Write),
    };

    /// <summary>Where the hand-authored records sit in a repo checkout. An exported build has no
    /// such folder, which is what keeps the switch out of a shipped game.</summary>
    public static string FixturesUnder(string repoRoot) => Path.Combine(repoRoot, "CSVM.Tests", "fixtures");

    /// <summary>This process's data root under <paramref name="repoRoot"/>'s scratch folder, after
    /// removing the trees of processes that have exited. Per process, so concurrent shards never
    /// write into a tree another one is reading.</summary>
    public static string ScratchRoot(string repoRoot)
    {
        string parent = Path.Combine(repoRoot, ".scratch", ScratchFolder);
        SweepStale(parent);
        return Path.Combine(parent, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Writes and stamps the whole tree under <paramref name="dataRoot"/>, replacing a
    /// synthetic tree an earlier run left there. Throws when a fixture is missing or a write fails.
    /// The caller then reads nothing, since falling back to the install is the one wrong answer.
    /// <paramref name="families"/> is the whole tree's list, <c>SyntheticShell.TreeFamilies</c>.</summary>
    public static void Build(string fixturesRoot, string dataRoot, IReadOnlyList<SyntheticFamily> families)
    {
        if (!Directory.Exists(fixturesRoot))
        {
            throw new DirectoryNotFoundException($"no hand-authored records at {fixturesRoot} (a repo checkout carries them, an export does not)");
        }

        string extracted = Path.Combine(dataRoot, ExtractionRun.ExtractedFolder);
        if (Directory.Exists(extracted))
        {
            // ⚠ Never delete a tree this class did not stamp; a real extraction at the root stops the build.
            if (!Marks(dataRoot))
            {
                throw new IOException($"{extracted} holds a tree not stamped synthetic, refusing to replace it");
            }

            Directory.Delete(extracted, recursive: true);
        }

        // Stamped first, so a tree a failed build leaves half written is still one a rebuild may replace.
        var tree = new SyntheticTree(fixturesRoot, dataRoot);
        var names = new JsonArray(families.Select(f => (JsonNode?)JsonValue.Create(f.Name)).ToArray());
        ExtractionStampWriter.Merge(tree.Extracted, StampField, new JsonObject
        {
            ["script"] = "CSVM",
            ["families"] = names,
        });
        foreach (var family in families)
        {
            family.Write(tree);
        }
    }

    /// <summary>Whether the tree under <paramref name="dataRoot"/> is stamped synthetic. False for a
    /// missing, unreadable or real stamp, so only a tree this class wrote reads true.</summary>
    public static bool Marks(string dataRoot)
    {
        string path = Path.Combine(dataRoot, ExtractionRun.ExtractedFolder, ExtractionStampWriter.FileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(StampField, out _);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return false;
        }
    }

    // Removes the trees whose process has exited, so the scratch folder holds one per live run rather
    // than one per run ever made. A live owner, or one this process cannot query, keeps its tree.
    private static void SweepStale(string parent)
    {
        if (!Directory.Exists(parent))
        {
            return;
        }

        foreach (string dir in Directory.EnumerateDirectories(parent))
        {
            if (!int.TryParse(Path.GetFileName(dir), NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
                || pid == Environment.ProcessId || Alive(pid))
            {
                continue;
            }

            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A tree another tool still holds open is left for CleanScratch.ps1.
            }
        }
    }

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }
}

/// <summary>One record family of the synthetic tree: a name for the stamp and the log, and the
/// writer that puts its files under <see cref="SyntheticTree.Extracted"/>.</summary>
public sealed record SyntheticFamily(string Name, Action<SyntheticTree> Write);

/// <summary>What a family writer is handed: where the hand-authored records are and where the tree
/// is. It also holds the two moves every family makes, copying a record and writing generated bytes.</summary>
public sealed class SyntheticTree
{
    internal SyntheticTree(string fixturesRoot, string dataRoot)
    {
        FixturesRoot = fixturesRoot;
        DataRoot = dataRoot;
        Extracted = Path.Combine(dataRoot, ExtractionRun.ExtractedFolder);
    }

    /// <summary>The <c>CSVM.Tests/fixtures/</c> folder the records are read from.</summary>
    public string FixturesRoot { get; }

    /// <summary>The data root the run reads, the folder <see cref="Extracted"/> sits in.</summary>
    public string DataRoot { get; }

    /// <summary>The tree's <c>extracted/</c> folder.</summary>
    public string Extracted { get; }

    /// <summary>A record's path under <see cref="FixturesRoot"/>. It throws when the record is
    /// absent, so a renamed fixture fails the tree's build instead of leaving a hole.</summary>
    public string Fixture(string relative)
    {
        string path = Path.Combine(FixturesRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"synthetic record {relative} is missing from {FixturesRoot}", path);
        }

        return path;
    }

    /// <summary>A path under <see cref="Extracted"/>, in the case given, with its folder created.
    /// The caller spells the case the loader resolves: <c>C1/texture</c>, as a chapter folder and an
    /// unpacked <c>texture.zip</c> are named.</summary>
    public string Under(string relative)
    {
        string path = Path.Combine(Extracted, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    /// <summary>Copies the record at <paramref name="fixture"/> to <paramref name="relative"/> under
    /// <see cref="Extracted"/>, and returns where it landed.</summary>
    public string CopyFixture(string fixture, string relative)
    {
        string to = Under(relative);
        File.Copy(Fixture(fixture), to, overwrite: true);
        return to;
    }

    /// <summary>Writes generated <paramref name="bytes"/> to <paramref name="relative"/> under
    /// <see cref="Extracted"/>.</summary>
    public void WriteBytes(string relative, byte[] bytes) => File.WriteAllBytes(Under(relative), bytes);

    /// <summary>Creates the folder <paramref name="relative"/> under <see cref="Extracted"/>, for a
    /// scope a loader is handed whole even when the tree has nothing to put in it yet.</summary>
    public string Folder(string relative)
    {
        string path = Path.Combine(Extracted, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);
        return path;
    }
}

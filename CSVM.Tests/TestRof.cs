using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CSVM.Extraction;

namespace CSVM.Tests;

/// <summary>The UI archive views the units hand the engine. A unit that drives a screen over
/// <see cref="TestData.DataRoot"/> reads the install in place, bound once for the assembly. Every
/// <see cref="TestData.TempDir"/> is bound to a live view of its <see cref="Folder"/>. A unit lays
/// its own files out there, in the layout <c>extracted/rof</c> had, and the engine reads them.
/// </summary>
internal static class TestRof
{
    private static readonly Lazy<string> LazyInstallRoot = new(() =>
    {
        string root = TestData.TempDir();
        RofFiles.Bind(root, RofFiles.Open(GameFilesData.Source));
        return root;
    });

    /// <summary>A data root of its own bound to the install's view, for a
    /// <see cref="GameFilesFactAttribute"/> unit that hands a screen a data root.</summary>
    internal static string InstallRoot => LazyInstallRoot.Value;

    /// <summary>Binds the install's view to <see cref="TestData.DataRoot"/>, read on first use.</summary>
    [ModuleInitializer]
    internal static void BindInstall()
    {
        if (TestData.DataRoot is { } root)
        {
            RofFiles.Bind(root, RofFiles.Open(GameFilesData.Source));
        }
    }

    /// <summary>The folder a unit writes its UI archive files into under <paramref name="root"/>.</summary>
    internal static string Folder(string root) => Path.Combine(root, "extracted", "rof");

    /// <summary>Binds a live view of <see cref="Folder"/> to <paramref name="root"/>: each lookup
    /// lists and reads the folder as it stands, so files written later are found.</summary>
    internal static void BindFolder(string root)
    {
        string folder = Folder(root);
        RofFiles.Bind(root, RofFiles.Live(
            () => Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(folder, f).Replace('\\', '/')).ToList()
                : Array.Empty<string>(),
            path => File.ReadAllBytes(Path.Combine(folder, path))));
    }
}

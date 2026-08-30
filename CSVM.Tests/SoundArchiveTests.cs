using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The archive's lifetime contract. A zip-backed archive is closed at the end of the world build
/// (<c>SoundsOutliveBuild = false</c>) while the session keeps it and keeps asking; a
/// directory-backed one is immune, which is why the shipped failure never appeared in development.
/// These pin the zip half.
/// </summary>
public sealed class SoundArchiveTests
{
    [Fact]
    public void ZipBackedArchiveStillResolvesAfterDispose()
    {
        using var temp = new TempZip(("gun.wav", "gun-bytes"), ("impact.wav", "impact-bytes"));
        var archive = new SoundArchive(temp.Path);

        // Cache one before the close and leave the other cold: the cold one is the shipped bug
        // (a gun loop first needed in flight, long after the build closed the handle).
        Assert.Equal("gun-bytes", Text(archive.ReadWavBytes("gun.wav")));
        archive.Dispose();

        Assert.Equal("impact-bytes", Text(archive.ReadWavBytes("impact.wav")));
    }

    [Fact]
    public void DisposeIsIdempotentAndStillLeavesTheArchiveReadable()
    {
        using var temp = new TempZip(("gun.wav", "gun-bytes"));
        var archive = new SoundArchive(temp.Path);

        archive.Dispose();
        archive.Dispose();

        Assert.Equal("gun-bytes", Text(archive.ReadWavBytes("gun.wav")));
    }

    [Fact]
    public void AnUnreadableArchiveReadsAsSilenceRatherThanThrowing()
    {
        var temp = new TempZip(("gun.wav", "gun-bytes"));
        var archive = new SoundArchive(temp.Path);
        archive.Dispose();

        // The file the reopen needs is gone. A throw here would escape the session step and skip
        // every later phase, the AI included; null is silence and the step survives.
        temp.Dispose();

        Assert.Null(archive.ReadWavBytes("gun.wav"));
    }

    [Fact]
    public void AWavTheArchiveNeverHadIsNull()
    {
        using var temp = new TempZip(("gun.wav", "gun-bytes"));
        using var archive = new SoundArchive(temp.Path);

        Assert.Null(archive.ReadWavBytes("absent.wav"));
    }

    private static string? Text(byte[]? bytes) => bytes == null ? null : Encoding.UTF8.GetString(bytes);

    private sealed class TempZip : System.IDisposable
    {
        internal TempZip(params (string Name, string Body)[] entries)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"csvm-sounds-{System.Guid.NewGuid():N}.zip");
            using var zip = ZipFile.Open(Path, ZipArchiveMode.Create);
            foreach (var (name, body) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(body);
            }
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}

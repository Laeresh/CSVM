using System.IO;
using System.Linq;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Every Image handed to a new texture goes through <c>Utils/TextureUpload.cs</c>, which holds it
/// until the render thread lets go. A direct <c>ImageTexture.CreateFromImage</c> leaves the
/// render thread's release to swap the wrapper's GC handle, unlocked, off the main thread. A
/// source scan, since the call is a static method a type-level metadata scan cannot tell apart from
/// the class's other members.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class TextureUploadSourceTests
{
    private const string Call = "ImageTexture.CreateFromImage(";

    [Fact]
    public void OnlyTextureUploadCreatesATextureFromAnImage()
    {
        string src = Path.Combine(TestData.RepoRoot, "CSVM", "src");
        Assert.True(Directory.Exists(src), src);
        string owner = Path.Combine("Utils", "TextureUpload.cs");
        var callers = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains(Call, System.StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(src, f))
            .ToList();

        // Able to fail: the owner makes the call itself, so an empty list means the scan read nothing.
        Assert.Contains(owner, callers);
        Assert.Equal(new[] { owner }, callers);
    }
}

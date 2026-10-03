using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace CSVM.Master.Tests;

/// <summary>
/// The container build sees only what server/Dockerfile copies and server/Dockerfile.dockerignore
/// lets through, while a local build sees the whole repository. So a game file linked into the
/// server project builds here and fails only on the VPS unless both name it.
/// </summary>
public class DockerContextTests
{
    [Fact]
    public void EveryGameFileTheServerLinksIsCopiedIntoTheImage()
    {
        string root = RepositoryRoot();
        string project = Path.Combine(root, "server", "MasterServer");
        string dockerfile = File.ReadAllText(Path.Combine(root, "server", "Dockerfile"));
        string[] ignore = File.ReadAllLines(Path.Combine(root, "server", "Dockerfile.dockerignore"));
        var linked = XDocument.Load(Path.Combine(project, "MasterServer.csproj"))
            .Descendants("Compile")
            .Select(item => (string?)item.Attribute("Include"))
            .Where(include => include != null)
            .Select(include => Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(project, include!))).Replace('\\', '/'))
            .Where(path => !path.StartsWith("server/", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(linked);
        foreach (string path in linked)
        {
            Assert.True(dockerfile.Contains($"COPY {path} {path}", StringComparison.Ordinal), $"server/Dockerfile copies {path}");
            Assert.True(ignore.Any(line => line.Trim() == $"!{path}"), $"server/Dockerfile.dockerignore lets {path} through");
        }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "server", "Dockerfile")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("no server/Dockerfile above the test's own directory");
    }
}

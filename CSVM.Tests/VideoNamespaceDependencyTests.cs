using System;
using System.IO;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The video decoder's boundary, enforced over compiled metadata: no type in
/// <c>CSVM.Video</c> may reference anything under <c>Godot</c>, in a signature or in a method
/// body. That is what lets a plain unit test play a whole file with no engine present, and what
/// the frame surface above it relies on. The scanner throws when the subject filter matches no
/// type, so this cannot pass by scanning nothing.
/// </summary>
public class VideoNamespaceDependencyTests
{
    [Fact]
    public void TheVideoDecoderReferencesNoEngineType()
    {
        var violations = AssemblyDependencyScan.Violations(
            Path.Combine(AppContext.BaseDirectory, "CSVM.dll"),
            ns => ns == "CSVM.Video",
            name => name.StartsWith("Godot.", StringComparison.Ordinal));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}

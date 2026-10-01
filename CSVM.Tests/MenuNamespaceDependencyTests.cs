using System;
using System.IO;
using System.Linq;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The menu seam's dependency direction, enforced over compiled metadata: no type in the shared
/// <c>CSVM.UI.Menu</c> namespace may reference a presentation. Shared means that namespace
/// exactly; every sub-namespace of it belongs to one presentation, and the rest of
/// <c>CSVM.UI</c> plus everything under <c>Godot</c> is presentation-side by definition. The
/// scanner's own tests prove it sees signature and body-only references alike.
/// </summary>
public class MenuNamespaceDependencyTests
{
    [Fact]
    public void SharedMenuTypesReferenceNoPresentationAndNoEngine()
    {
        var violations = AssemblyDependencyScan.Violations(
            Assembly("CSVM.dll"),
            ns => ns == "CSVM.UI.Menu",
            BannedForSharedMenu);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void TheScannerSeesASignatureReference()
    {
        Assert.Contains(
            ScanFixtureViolations(),
            v => v.StartsWith("CSVM.Tests.SeamScan.Feature.SignatureOffender", StringComparison.Ordinal));
    }

    [Fact]
    public void TheScannerSeesABodyOnlyReference()
    {
        Assert.Contains(
            ScanFixtureViolations(),
            v => v.StartsWith("CSVM.Tests.SeamScan.Feature.BodyOffender", StringComparison.Ordinal));
    }

    [Fact]
    public void TheScannerDoesNotFlagACleanType()
    {
        Assert.DoesNotContain(
            ScanFixtureViolations(),
            v => v.StartsWith("CSVM.Tests.SeamScan.Feature.CleanType", StringComparison.Ordinal));
    }

    [Fact]
    public void AScanMatchingNoTypesFailsRatherThanPassing()
    {
        Assert.Throws<InvalidOperationException>(() => AssemblyDependencyScan.Violations(
            Assembly("CSVM.dll"),
            ns => ns == "CSVM.No.Such.Namespace",
            _ => true));
    }

    private static string Assembly(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, fileName);

    private static System.Collections.Generic.IReadOnlyList<string> ScanFixtureViolations() =>
        AssemblyDependencyScan.Violations(
            Assembly("CSVM.Tests.dll"),
            ns => ns == "CSVM.Tests.SeamScan.Feature",
            name => name.StartsWith("CSVM.Tests.SeamScan.Presentation", StringComparison.Ordinal));

    // A referenced name is fine when it lives in CSVM.UI.Menu itself; anything else under
    // CSVM.UI, any sub-namespace of CSVM.UI.Menu, and all of Godot is a presentation dependency.
    private static bool BannedForSharedMenu(string fullName) =>
        fullName.StartsWith("Godot.", StringComparison.Ordinal)
        || (fullName.StartsWith("CSVM.UI.", StringComparison.Ordinal) && !IsSharedMenuType(fullName));

    private static bool IsSharedMenuType(string fullName)
    {
        string outer = fullName.Split('/')[0];
        int dot = outer.LastIndexOf('.');
        return dot > 0 && outer[..dot] == "CSVM.UI.Menu";
    }
}

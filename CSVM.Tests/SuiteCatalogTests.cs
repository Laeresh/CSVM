using System.Linq;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

public sealed class SuiteCatalogTests
{
    [Fact]
    public void Names_preserve_the_registered_order()
    {
        string[] names = TestHarness.All.Select(suite => suite.Name).ToArray();

        Assert.Equal(115, names.Length);
        Assert.Equal(SuiteCatalog.Names, names);
        Assert.Equal("emitter-lifetime", names[0]);
        Assert.Equal("roster-spawn-names", names[^1]);
        Assert.Equal(names.Length, names.Distinct().Count());
    }
}

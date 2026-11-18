using System.Linq;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>The shader warm list keeps what a load wore first and ages the rest by one load. An entry
/// past the idle limit drops, so a stale generator's texts leave the launch compile.</summary>
public class ShaderWarmListTests
{
    [Fact]
    public void WornTextsLeadAndTheRestAgeOut()
    {
        var list = new[]
        {
            new ShaderTwins.WarmEntry("bias", "a", 3),
            new ShaderTwins.WarmEntry("bias", "b", 0),
            new ShaderTwins.WarmEntry("fade-twin", "c", ShaderTwins.WarmListIdleLimit),
        };

        var aged = ShaderTwins.Aged(list, new[] { "b" });

        Assert.Equal(new[] { "b", "a" }, aged.Select(e => e.Text));
        Assert.Equal(new[] { 0, 4 }, aged.Select(e => e.Idle));
        Assert.Equal("bias", aged[0].Family);
    }
}

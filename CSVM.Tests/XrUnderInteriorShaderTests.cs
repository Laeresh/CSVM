using System;
using System.Linq;
using System.Reflection;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The shader text of a part the stereo interior draws over (<c>SceneBuilder.XrUnderInterior</c>, the
/// pylon stores' copies). It reads the interior's stencil mark in the transparent pass, so the
/// interior covers it at any depth. The flat cockpit pass composites the interior the same way.
/// </summary>
[Trait("Tier", "Quick")]
public class XrUnderInteriorShaderTests
{
    [Fact]
    public void AnOpaquePartUnderTheInteriorReadsTheMarkInTheTransparentPass()
    {
        string under = Text(underInterior: true);
        string interior = Text(underInterior: false);

        Assert.Contains($"stencil_mode read, compare_not_equal, {SceneBuilder.XrInteriorStencil};", under);
        Assert.DoesNotContain("stencil_mode write", under);
        Assert.Contains("depth_draw_always", under);
        Assert.Contains("ALPHA = 1.0;", under);
        Assert.Contains($"stencil_mode write, compare_always, {SceneBuilder.XrInteriorStencil};", interior);
        Assert.DoesNotContain("ALPHA", interior);
    }

    // An opaque, textured, lit, fogged stereo-interior surface, the store copies' common case.
    private static string Text(bool underInterior)
    {
        var method = typeof(SceneBuilder).GetMethod("BiasShaderCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = method.GetParameters().Select(p => p.ParameterType == typeof(bool)
            ? (object)(p.Name is "textured" or "lit" or "fogged" or "xrInterior" || (p.Name == "underInterior" && underInterior))
            : Activator.CreateInstance(p.ParameterType)!).ToArray();
        return (string)method.Invoke(null, args)!;
    }
}

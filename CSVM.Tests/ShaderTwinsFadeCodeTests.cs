using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>The fade copy's ALPHA line lands inside fragment(), before its closing brace, whether or
/// not a light() follows it. In light() the col local is unknown and the shader fails to compile.</summary>
public class ShaderTwinsFadeCodeTests
{
    private static readonly string Head = "shader_type spatial;\n" + SceneBuilder.InstanceUniformsInclude + "\n";

    [Fact]
    public void TheAlphaLineGoesBeforeFragmentsBrace()
    {
        string fragment = "void fragment() {\n    vec4 col = vec4(1.0);\n    ALBEDO = col.rgb;\n}\n";
        string light = "\nvoid light() {\n    DIFFUSE_LIGHT += vec3(1.0);\n}\n";
        string faded = ShaderTwins.FadeCode(Head + fragment + light)!;
        Assert.Contains("    ALBEDO = col.rgb;\n    ALPHA = col.a" + SceneBuilder.OpacityTerm + ";\n}\n\nvoid light()", faded);
        Assert.EndsWith("    DIFFUSE_LIGHT += vec3(1.0);\n}\n", faded);
        Assert.EndsWith("    ALPHA = col.a" + SceneBuilder.OpacityTerm + ";\n}\n", ShaderTwins.FadeCode(Head + fragment)!);
    }
}

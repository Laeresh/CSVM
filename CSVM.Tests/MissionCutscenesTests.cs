using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The mission cutscene classifier (<c>docs/formats/anim-definitions/cutscenes.md</c>): which of
/// a mission's <c>ANIMATION_DEFINITION_FILE</c> entries sit under its <c>cutscenes\</c> directory,
/// and the <c>ANIMATION_NAME</c>s those files define. Input is
/// <c>fixtures/mission-cutscenes/</c>, whose mis_anim lists one cutscene file and the same ambient
/// file twice, once by path and once by bare name.
/// </summary>
public class MissionCutscenesTests
{
    [Fact]
    public void OnlyTheCutscenesDirectoryContributesItsAnimationNames()
    {
        Assert.Equal(new[] { "probe_drop", "probe_drop_cam" }, Load());
    }

    [Fact]
    public void ADefinitionFileOutsideThatDirectoryContributesNothing()
    {
        // probe_ambient.zrd is listed twice and readable both times; neither entry is a cutscene,
        // so the ambient loop it defines must not reach the host or the range gate.
        Assert.DoesNotContain("probe_ambient_loop", Load());
    }

    [Fact]
    public void AMissionWhoseReaderListsNoCutsceneIsEmpty()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, MissionCutscenes.FileName),
            "[[\"ANIMATION_DEFINITIONS\",[\"ANIMATION_LIST\",[\"ANIMATION_DEFINITION_FILE\","
            + "[\"..\\\\data\\\\probe\\\\m01\\\\zrdr\\\\envmodels\\\\probe_ambient.zrd\"]]]]]");
        Assert.Empty(MissionCutscenes.AnimNames(dir));
    }

    [Fact]
    public void AMissionWithNoReaderAtAllIsEmptyRatherThanAThrow()
    {
        Assert.Empty(MissionCutscenes.AnimNames(TestData.TempDir()));
    }

    private static System.Collections.Generic.List<string> Load() =>
        MissionCutscenes.AnimNames(TestData.Fixture("mission-cutscenes"));
}

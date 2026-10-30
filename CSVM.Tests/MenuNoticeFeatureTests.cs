using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The launcher's pending menu notice: every message posted before a show reaches the player as its
/// own paragraph. A presentation takes the whole notice once.
/// </summary>
public class MenuNoticeFeatureTests
{
    [Fact]
    public void ASecondPostAddsAParagraphAndARepeatAddsNothing()
    {
        var notice = new MenuNoticeFeature();
        notice.Post("VR fell back to flat.");
        notice.Post("The rig for player_bhawk has an error.");
        notice.Post("The rig for player_bhawk has an error.");
        notice.Post("The rig for player_fury has an error.");

        Assert.Equal(
            "VR fell back to flat.\n\nThe rig for player_bhawk has an error.\n\nThe rig for player_fury has an error.",
            notice.Take());
        Assert.Null(notice.Take());
        notice.Post("Later.");
        Assert.Equal("Later.", notice.Pending);
    }
}

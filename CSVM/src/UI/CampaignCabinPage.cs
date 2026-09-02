using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The cabin hub (<c>Campaign Cabin.png</c>, <c>PASSENGERCABIN.SCRIPT</c>): NEXT MISSION,
/// PREVIOUS MISSIONS, PLANE CONSTRUCTION, RETURN TO MAIN MENU. CHANGE MEMENTO is not shipped
/// (the original does not ship it). <c>CAP-44</c> settled the ambience question at the
/// controls: the screen has no idle behaviour, only background music over static art, so this
/// page draws once and does nothing between presses.
/// </summary>
public sealed class CampaignCabinPage : CampaignPage
{
    /// <summary>PLANE CONSTRUCTION's row, the one the shell has to reach by name: it is the press
    /// that leaves this flow standing while the hangar runs over the profile's wallet.</summary>
    public const int PlaneConstructionRow = 2;

    // The disabled-Next-Mission reason. Not a decoded langui id: docs/formats/strings.md's
    // 1200-1219 mission-results block has no "campaign finished" row, and uiData 2600 (the
    // original's own gate) is a boolean with no accompanying message string.
    private const string FinishedReason = "Every mission in the campaign has been completed.";

    // The memento's window inside PC_Mementopicframe, chosen rather than read: it is the block each
    // hangar photograph keys out for it, where the layout's PC_MEMENTO pane sits at 169,325 with
    // no size. The frame's own border covers the edges either way.
    private const int MementoX = 179;
    private const int MementoY = 330;
    private const int MementoWidth = 73;
    private const int MementoHeight = 84;

    // The four buttons, in the original's own order (docs/formats/campaign-screens.md, "The
    // cabin"). SAVE GAME is deactivated there and never drawn here.
    private static readonly string[] Rows =
    {
        "Next Mission",
        "Previous Missions",
        "Plane Construction",
        "Return to Main Menu",
    };

    // The cabin scene, decoded on first sight and kept: one 800x600 PNG per session, and a miss
    // must not re-probe the disk every frame.
    private HangarArt? _scene;
    private bool _sceneProbed;

    /// <summary>Binds the page to its flow.</summary>
    public CampaignCabinPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Cabin;

    /// <inheritdoc/>
    public override string Title => "CAMPAIGN CABIN";

    /// <inheritdoc/>
    public override int RowCount => Rows.Length;

    /// <inheritdoc/>
    /// <remarks>The cabin scene, <c>PC_BackGround.png</c>, through <see cref="PngImage"/>. The
    /// pilot's own aircraft is not read here: the campaign screens render as composed boards, so
    /// <see cref="Pictures"/> is what draws the photograph and this stays the one flat picture the
    /// page can hand a caller that wants pixels.</remarks>
    public override HangarArt? Art => CabinScene();

    /// <summary>The scene as the original layers it: the pilot's own aircraft first, then the
    /// painted cabin over it, whose colour-keyed hole is where the window is. The photograph ships
    /// as JPG, which the shell's own loader reads. The panes' positions and the two fixed bitmaps
    /// are <c>[@PassengerCabin@]</c>'s <c>PC_PLANE</c>, <c>PC_BACKGROUND</c> and <c>PC_FRAME</c>.</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            var layout = Flow.Layout;
            const string section = CampaignLayout.CabinSection;
            var pictures = new List<BoardPicture>(4);
            if (PlaneArtName() is { } photo)
            {
                var (planeX, planeY) = layout.At(section, "PC_PLANE", 46, 69);
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, photo), planeX, planeY));
            }

            // The memento, over the hangar photograph's own keyed-out block for it and under the
            // painting. Picking one is not shipped, so it is always the campaign's opening keepsake.
            pictures.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Rimage, "ms_p_initialpinup1"),
                MementoX, MementoY, 0, false, 1f, 0f, MementoWidth, MementoHeight));
            var (backX, backY) = layout.At(section, "PC_BACKGROUND", 0, 0);
            pictures.Add(new BoardPicture(
                layout.Art(section, "PC_BACKGROUND", new BoardArt(BoardArtLibrary.Ui, "PC_BackGround.png")), backX, backY));

            // The frame is painted over the painting rather than seen through its hole, which is
            // the one element the layout puts above the background.
            var (frameX, frameY) = layout.At(section, "PC_FRAME", 169, 317);
            pictures.Add(new BoardPicture(
                layout.Art(section, "PC_FRAME", new BoardArt(BoardArtLibrary.Ui, "PC_Mementopicframe.png")), frameX, frameY));
            return pictures;
        }
    }

    /// <summary>The map's pin count: one per story chapter reached, the story chapter of the
    /// campaign's current position (<c>gosCallback</c> 7 with -2, docs/formats/campaign-screens.md,
    /// "The cabin"). The count is engine-free and tested; drawing the pins needs their authored
    /// positions on the map, which nothing decodes yet. <c>PC_mappins.png</c> itself is an ordinary
    /// 50x100 RGBA sheet the art seam reads.</summary>
    public static int MapPinCount(CampaignProfileDef profile) =>
        (CampaignProgression.NextMissionSeq(profile) / 5) + 1;

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row) => row switch
    {
        0 => new BoardButtonRef(BoardButton.NextMission),
        1 => new BoardButtonRef(BoardButton.PreviousMissions),
        PlaneConstructionRow => new BoardButtonRef(BoardButton.PlaneConstruction),
        3 => new BoardButtonRef(BoardButton.ReturnToMainMenu),
        _ => BoardButtonRef.None,
    };

    /// <inheritdoc/>
    public override string RowText(int row) => Rows[row];

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var profile = Flow.Profile;
        if (profile == null)
        {
            return "No player is selected";
        }

        return row switch
        {
            0 when CampaignProgression.Complete(profile) => FinishedReason,
            0 => "Opens the briefing for the next mission",
            1 => CampaignProgression.CompletedSeqs(profile).Count == 0
                ? "No missions finished yet"
                : "Review or replay a finished mission",
            2 => "Buy, sell and fit aircraft",
            _ => "Leaves the campaign for the main menu",
        };
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        var profile = Flow.Profile;
        if (profile == null)
        {
            Flow.Cancel();
            return true;
        }

        switch (row)
        {
            case 0:
                if (CampaignProgression.Complete(profile))
                {
                    Flow.SetMessage(FinishedReason);
                    return true;
                }

                Flow.SetMission(CampaignProgression.NextMissionSeq(profile));
                Flow.GoTo(CampaignScreen.Briefing);
                return true;
            case 1:
                Flow.GoTo(CampaignScreen.PreviousMissions);
                return true;
            case PlaneConstructionRow:
                // The shell owns opening the hangar over the profile's wallet and calling
                // Flow.Resume when it closes.
                Flow.Request(CampaignExit.OpenHangar);
                return true;
            default:
                Flow.Cancel();
                return true;
        }
    }

    // The cabin's own flat art, decoded once. The composition A6 describes (background, then the
    // window-hole photograph, the memento frame and the map pins over it) needs a compositor the
    // art column does not have, so the background stands for the scene and MapPinCount stands for
    // the pins.
    private HangarArt? CabinScene()
    {
        if (_sceneProbed)
        {
            return _scene;
        }

        _sceneProbed = true;
        if (Flow.DataRoot is { } root)
        {
            var path = Path.Combine(root, "extracted", "rof", "ASSETS", "GRAPHICS", "PC_BACKGROUND.PNG");
            _scene = PngImage.TryLoad(path) is { } image
                ? new HangarArt(image, Flow.Profile?.Name ?? "Cabin")
                : null;
        }

        return _scene;
    }

    // The hangar photograph's own filename for the profile's selected aircraft, or null where the
    // profile owns nothing to photograph.
    private string? PlaneArtName()
    {
        var profile = Flow.Profile;
        if (profile == null || profile.Planes.Count == 0)
        {
            return null;
        }

        int index = Math.Clamp(profile.SelectedPlane, 0, profile.Planes.Count - 1);
        return $"PC_P_HANGAR{profile.Planes[index].Airframe}.JPG";
    }

}

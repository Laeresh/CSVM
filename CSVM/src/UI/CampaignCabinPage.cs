using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The cabin hub (<c>Campaign Cabin.png</c>, <c>PASSENGERCABIN.SCRIPT</c>): NEXT MISSION,
/// PREVIOUS MISSIONS, PLANE CONSTRUCTION, RETURN TO MAIN MENU. CHANGE MEMENTO is not shipped
/// (decision 3, PLAN-M5-campaign.md). <c>CAP-44</c> settled the ambience question at the
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

    // The four buttons, in the original's own order (docs/formats/campaign-screens.md, "The
    // cabin"). SAVE GAME is deactivated there and never drawn here.
    private static readonly string[] Rows =
    {
        "Next Mission",
        "Previous Missions",
        "Plane Construction",
        "Return to Main Menu",
    };

    // Cached per airframe, so a photo miss (see PlanePhoto below) is probed once, not once per
    // frame, mirroring HangarAirframePage.BlueprintFor.
    private readonly Dictionary<int, HangarArt?> _photoArt = new();

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
    /// <remarks>The cabin scene, <c>PC_BackGround.png</c>. The pilot's own aircraft behind the
    /// window hole (<c>PC_P_HANGAR&lt;airframe&gt;.JPG</c>) is preferred where it decodes, but it
    /// ships as JPG and no engine-free JPG decoder exists, so the photograph stays a named gap the
    /// plan's C22 section records rather than papering over. The background itself is PNG and
    /// draws through <see cref="PngImage"/>.</remarks>
    public override HangarArt? Art => PlanePhoto() ?? CabinScene();

    /// <summary>The map's pin count: one per story chapter reached, the story chapter of the
    /// campaign's current position (<c>gosCallback</c> 7 with -2, docs/formats/campaign-screens.md,
    /// "The cabin"). A pure, tested stand-in for the pixel composition <see cref="Art"/> cannot yet
    /// draw: <c>PC_mappins.png</c> hits the same JPG/PNG gap as the plane photo below.</summary>
    public static int MapPinCount(CampaignProfileDef profile) =>
        (CampaignProgression.NextMissionSeq(profile) / 5) + 1;

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
                // Flow.Resume when it closes (docs/PLAN-M5-campaign.md, C22's wiring contract).
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

    private HangarArt? PlanePhoto()
    {
        var profile = Flow.Profile;
        if (profile == null || profile.Planes.Count == 0)
        {
            return null;
        }

        int index = Math.Clamp(profile.SelectedPlane, 0, profile.Planes.Count - 1);
        var plane = profile.Planes[index];
        if (_photoArt.TryGetValue(plane.Airframe, out var cached))
        {
            return cached;
        }

        HangarArt? art = null;
        if (Flow.DataRoot is { } root)
        {
            var path = Path.Combine(root, "extracted", "rof", "ASSETS", "GRAPHICS",
                $"PC_P_HANGAR{plane.Airframe}.JPG");
            if (TgaImage.TryLoad(path) is { } image)
            {
                art = new HangarArt(image, plane.Name);
            }
        }

        _photoArt[plane.Airframe] = art;
        return art;
    }
}

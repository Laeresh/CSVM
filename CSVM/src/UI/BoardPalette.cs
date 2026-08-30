using Godot;

namespace CSVM.UI;

/// <summary>
/// The ink a campaign board writes in. One palette per background family, because the screens are
/// painted art: the same grey the flight check's forms use is invisible on the cabin's dark hangar.
/// The flight check and ammo values are the layout rows' own ARGB fields; the rest are chosen to
/// read on their background, and <c>docs/org/campaign-board.md</c> says which is which.
/// </summary>
public sealed record BoardPalette(
    Color Row, Color Focus, Color Heading, Color Detail,
    Color LabelNormal, Color LabelRollover, Color LabelActivate, Color Hint)
{
    /// <summary>The flight check's and ammo screen's printed forms.</summary>
    public static readonly BoardPalette Paper = new(
        Row: new Color(0.07f, 0.075f, 0.083f),
        Focus: new Color(0.075f, 0.239f, 0.467f),
        Heading: new Color(0.176f, 0.220f, 0.263f),
        Detail: new Color(0.176f, 0.220f, 0.263f),
        LabelNormal: new Color(0.075f, 0.239f, 0.467f),
        LabelRollover: new Color(0.216f, 0.302f, 0.420f),
        LabelActivate: new Color(0f, 0f, 0f),
        Hint: new Color(0.176f, 0.220f, 0.263f));

    /// <summary>The scrapbook's own three screens: the book, its table of contents and a scrap's
    /// detail view, all black ink on paper the way their layout rows ask for.</summary>
    public static readonly BoardPalette Album = new(
        Row: new Color(0.07f, 0.06f, 0.05f),
        Focus: new Color(0.55f, 0.10f, 0.06f),
        Heading: new Color(0.07f, 0.06f, 0.05f),
        Detail: new Color(0.20f, 0.17f, 0.13f),
        LabelNormal: new Color(0.10f, 0.09f, 0.07f),
        LabelRollover: new Color(0.55f, 0.10f, 0.06f),
        LabelActivate: new Color(0f, 0f, 0f),
        Hint: new Color(0.16f, 0.13f, 0.10f));

    /// <summary>The briefing: dark on the objectives parchment, cream on the gold plaques.</summary>
    public static readonly BoardPalette Parchment = new(
        Row: new Color(0.16f, 0.12f, 0.06f),
        Focus: new Color(0.55f, 0.10f, 0.06f),
        Heading: new Color(0.16f, 0.12f, 0.06f),
        Detail: new Color(0.93f, 0.93f, 0.93f),
        LabelNormal: new Color(0.94f, 0.91f, 0.82f),
        LabelRollover: new Color(1f, 1f, 1f),
        LabelActivate: new Color(1f, 0.82f, 0.38f),
        Hint: new Color(0.95f, 0.95f, 0.95f));

    /// <summary>The profile screen's brass panel and the cabin's hangar, both dark.</summary>
    public static readonly BoardPalette Panel = new(
        Row: new Color(0.91f, 0.89f, 0.86f),
        Focus: new Color(1f, 0.30f, 0.20f),
        Heading: new Color(0.91f, 0.89f, 0.86f),
        Detail: new Color(0.86f, 0.84f, 0.78f),
        LabelNormal: new Color(0.94f, 0.91f, 0.82f),
        LabelRollover: new Color(1f, 1f, 1f),
        LabelActivate: new Color(1f, 0.82f, 0.38f),
        Hint: new Color(0.90f, 0.88f, 0.80f));

    /// <summary>The load screen's blackboard, where everything is chalk.</summary>
    public static readonly BoardPalette Chalk = new(
        Row: new Color(0.86f, 0.88f, 0.90f),
        Focus: new Color(1f, 0.82f, 0.38f),
        Heading: new Color(0.96f, 0.97f, 0.98f),
        Detail: new Color(0.80f, 0.83f, 0.86f),
        LabelNormal: new Color(0.94f, 0.91f, 0.82f),
        LabelRollover: new Color(1f, 1f, 1f),
        LabelActivate: new Color(1f, 0.82f, 0.38f),
        Hint: new Color(0.86f, 0.88f, 0.90f));

    /// <summary>The palette a screen writes in.</summary>
    public static BoardPalette For(CampaignScreen screen) => screen switch
    {
        CampaignScreen.FlightCheck or CampaignScreen.Ammo => Paper,
        CampaignScreen.PreviousMissions or CampaignScreen.Scrapbook
            or CampaignScreen.ScrapbookZoom => Album,
        CampaignScreen.Briefing => Parchment,
        _ => Panel,
    };
}

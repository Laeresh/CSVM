using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI;

/// <summary>
/// The AIRFRAME screen: all 11 airframes as rows (the availability threshold gates nothing here,
/// so it does not gate this screen), named from langui 3000+id, the chosen one ticked. Moving the cursor
/// previews an airframe (detail figures, stars and blueprint follow focus) and confirm picks it,
/// so the ←→ stepper is inert here.
///
/// <para>Confirming an airframe that is not already the pick raises the defaults ask (string 206,
/// <see cref="HangarFlow.DefaultsAsk"/>), an inline two-row confirm: OK loads the airframe's
/// defaults, Cancel keeps every current pick; the switch stands either way. Confirming the row
/// that already is the pick advances instead, so a new plane leaves this screen only through a
/// pick.</para>
/// </summary>
public sealed class HangarAirframePage : HangarPage
{
    private readonly Dictionary<int, HangarArt?> _art = new();
    private readonly Dictionary<int, HangarArt?> _diagrams = new();

    /// <summary>Binds the page to its flow.</summary>
    public HangarAirframePage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.Airframe;

    /// <inheritdoc/>
    public override int RowCount => Flow.DefaultsAsk is null ? HangarEconomy.Airframes.Length : 2;

    /// <inheritdoc/>
    public override int OpeningRow => Flow.AirframeChosen ? Scratch.Airframe : 0;

    /// <inheritdoc/>
    public override HangarArt? Art => BlueprintFor(FocusedAirframe);

    // Which airframe the art follows: the row the cursor is on, or the one the defaults ask is
    // about, since that ask replaces the list with two buttons and its own row index means nothing.
    private int FocusedAirframe =>
        Flow.DefaultsAsk ?? Math.Clamp(Flow.Row, 0, HangarEconomy.Airframes.Length - 1);

    /// <summary>The focused airframe's plan view under its blueprint, the frame it owns in the
    /// diagram sheet the ammo screen draws from. The blueprint is a schematic in the original's
    /// grid style and this is the aircraft's actual silhouette, so the pair says both what the
    /// airframe is called and what it looks like from above before the pick is made.</summary>
    public override HangarArt? RowArt(int row) => DiagramFor(FocusedAirframe);

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        if (Flow.DefaultsAsk is not null)
        {
            return row == 0 ? "OK" : "Cancel";
        }

        // Nothing is ticked until an airframe is picked: a new plane carries airframe 0 in the
        // model, and ticking it would tell the pilot a choice was made for them (E49).
        bool chosen = Flow.AirframeChosen && Scratch.Airframe == row;
        return Flow.AirframeName(row) + (chosen ? "  ✓" : string.Empty);
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        if (Flow.DefaultsAsk is not null)
        {
            return Flow.DefaultsAskText;
        }

        var stats = HangarEconomy.Airframes[row];
        var bill = BillFor(row);
        return $"${stats.Cost}   {stats.Weight} lbs.   Capacity {stats.Capacity} lbs.   " +
               $"Agility {Stars(bill.AgilityStars)}   Armor {Stars(bill.ArmourStars)}";
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        if (Flow.DefaultsAsk is not null)
        {
            Flow.AnswerDefaultsAsk(row == 0);
            return true;
        }

        // A pick keeps the press (and raises the ask); confirming the standing pick hands it back,
        // and the flow advances.
        return Flow.PickAirframe(row);
    }

    private static string Stars(int filled) => new string('★', filled) + new string('☆', 4 - filled);

    // The ratings exactly as the economy computes them for this airframe carrying the scratch
    // plane's other picks: swap the airframe in, price, swap back. Cheaper than a deep copy and
    // safe because pages run on the one menu thread.
    private HangarBill BillFor(int airframe)
    {
        int keep = Scratch.Airframe;
        Scratch.Airframe = airframe;
        var bill = HangarEconomy.Price(Scratch);
        Scratch.Airframe = keep;
        return bill;
    }

    // One airframe's blueprint, decoded once and kept — including a miss, so an absent
    // extraction is probed once per airframe, not once per frame.
    private HangarArt? BlueprintFor(int airframe)
    {
        if (_art.TryGetValue(airframe, out var art))
        {
            return art;
        }

        art = null;
        if (Flow.DataRoot is { } root)
        {
            var path = Path.Combine(root, "extracted", "rof", "ASSETS", "GRAPHICS",
                $"PX_{airframe}_BLUEPRINT.TGA");
            if (ArtImage.TryLoad(path) is { } image)
            {
                art = new HangarArt(image, Flow.AirframeName(airframe));
            }
        }

        _art[airframe] = art;
        return art;
    }

    // The plan-view frame, cached the same way and for the same reason as the blueprint above.
    private HangarArt? DiagramFor(int airframe)
    {
        if (_diagrams.TryGetValue(airframe, out var art))
        {
            return art;
        }

        art = Flow.DataRoot is { } root
              && PlaneDiagrams.Frame(root, PlaneDiagrams.Top, airframe) is { } frame
            ? new HangarArt(frame, Flow.AirframeName(airframe))
            : null;
        _diagrams[airframe] = art;
        return art;
    }
}

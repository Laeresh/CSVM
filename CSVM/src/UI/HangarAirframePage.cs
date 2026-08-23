using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI;

/// <summary>
/// The AIRFRAME screen: all 11 airframes as rows (the availability threshold gates nothing here,
/// PLAN-hangar Decision 9), named from langui 3000+id, the chosen one ticked. The ←→ stepper makes
/// the focused row the scratch plane's airframe and touches nothing else: guns and hardpoints are
/// count-valid on every airframe (the wrong-claims disproof), so no other pick needs re-clamping.
/// The detail line is the stat table's cost, weight and capacity plus the two star ratings, all
/// through <see cref="HangarEconomy"/>; the art is the focused airframe's blueprint TGA.
///
/// <para>Picking a DIFFERENT airframe, and a new plane's first arrival, raise the defaults ask
/// (string 206, <see cref="HangarFlow.DefaultsAsk"/>), an inline two-row confirm: OK loads the
/// airframe's defaults, Cancel keeps every current pick; the switch stands either way.</para>
/// </summary>
public sealed class HangarAirframePage : HangarPage
{
    private readonly Dictionary<int, HangarArt?> _art = new();

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
    public override HangarArt? Art =>
        BlueprintFor(Flow.DefaultsAsk ?? Math.Clamp(Flow.Row, 0, HangarEconomy.Airframes.Length - 1));

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        if (Flow.DefaultsAsk is not null)
        {
            return row == 0 ? "OK" : "Cancel";
        }

        return Flow.AirframeName(row) + (Scratch.Airframe == row ? "  ✓" : string.Empty);
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
    public override bool Step(int row, int dir)
    {
        if (Flow.DefaultsAsk is not null || Scratch.Airframe == row)
        {
            return false;
        }

        int was = Scratch.Airframe;
        Scratch.Airframe = row;
        Flow.RaiseDefaultsAsk(row, was);
        return true;
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        if (Flow.DefaultsAsk is null)
        {
            return false; // no ask showing: the flow advances as on every other screen
        }

        Flow.AnswerDefaultsAsk(row == 0);
        return true;
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
            if (TgaImage.TryLoad(path) is { } image)
            {
                art = new HangarArt(image, Flow.AirframeName(airframe));
            }
        }

        _art[airframe] = art;
        return art;
    }
}

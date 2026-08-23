using CSVM.Flight;

namespace CSVM.UI;

/// <summary>
/// The ARMOR screen: the four zones as rows, named through their own langui formats (1191-1194,
/// "Nose: %1!d! units" and kin, which carry the number themselves). The ←→ stepper walks the
/// focused zone's units 0-12 with wraparound, the original's 13-row dropdown as a cycle, writing
/// the scratch def; Confirm advances without editing. The detail line keeps the three factors
/// distinct: the units bought, cost at units x4, weight at units x4, and the units x5 lb figure
/// the original's dropdown displayed (format 1170), which is display-only and never priced.
/// </summary>
public sealed class HangarArmourPage : HangarPage
{
    private static readonly string[] ZoneFallbacks = { "Nose", "Tail", "Left Wing", "Right Wing" };

    /// <summary>Binds the page to its flow.</summary>
    public HangarArmourPage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.Armour;

    /// <inheritdoc/>
    public override int RowCount => ZoneFallbacks.Length;

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        int units = UnitsOf(row);
        string text = Flow.Strings.Format(1191 + row, units);
        return text.Length > 0 ? text : $"{ZoneFallbacks[row]}: {units} units";
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        int units = UnitsOf(row);
        string bought = Flow.Strings.Format(1170, units);
        if (bought.Length == 0)
        {
            bought = $"{units} units";
        }

        return $"{bought}   ${units * HangarEconomy.ArmourUnitCost}   " +
               $"{units * HangarEconomy.ArmourUnitWeight} lbs.   {units * 5} lb shown";
    }

    /// <inheritdoc/>
    public override bool Step(int row, int dir)
    {
        int count = CustomPlaneDef.MaxArmourUnits + 1;
        SetUnits(row, ((UnitsOf(row) + dir) % count + count) % count);
        return true;
    }

    // The zones in the record's own order, the order 1191-1194 name them in.
    private int UnitsOf(int row) => row switch
    {
        0 => Scratch.ArmourNose,
        1 => Scratch.ArmourTail,
        2 => Scratch.ArmourLeftWing,
        _ => Scratch.ArmourRightWing,
    };

    private void SetUnits(int row, int units)
    {
        switch (row)
        {
            case 0: Scratch.ArmourNose = units; break;
            case 1: Scratch.ArmourTail = units; break;
            case 2: Scratch.ArmourLeftWing = units; break;
            default: Scratch.ArmourRightWing = units; break;
        }
    }
}

using CSVM.Flight;

namespace CSVM.UI;

/// <summary>
/// The ENGINE screen: the airframe's six engines (langui 3100+af*6+id: each manufacturer's three
/// displacements, then the same three with nitro) plus the explicit no-engine row, langui 1165
/// "None" (the decoded dropdown's own last row, callback 2218; 1171 "No Engine Selected" is the
/// PURCHASE screen's wording, not this one's), the chosen one ticked. The ←→ stepper makes the
/// focused row the scratch plane's engine and Confirm advances without editing, the airframe
/// page's idiom. The detail line is the engine's decoded cost and weight through
/// <see cref="HangarEconomy.EngineLine"/>, plus the original's power stat (the stat-table rating
/// times the decoded per-id factor, display-only).
/// </summary>
public sealed class HangarEnginePage : HangarPage
{
    /// <summary>Binds the page to its flow.</summary>
    public HangarEnginePage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.Engine;

    /// <inheritdoc/>
    public override int RowCount => CustomPlaneDef.EngineNone + 1;

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        string name = row == CustomPlaneDef.EngineNone
            ? Flow.Strings.Text(1165, "None")
            : Flow.EngineName(Scratch.Airframe, row);
        return name + (Scratch.Engine == row ? "  ✓" : string.Empty);
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var line = HangarEconomy.EngineLine(Scratch.Airframe, row);
        if (row == CustomPlaneDef.EngineNone)
        {
            return $"${line.Cost}   {line.Weight} lbs.";
        }

        return $"${line.Cost}   {line.Weight} lbs.   Power {HangarEconomy.PowerStat(Scratch.Airframe, row)}";
    }

    /// <inheritdoc/>
    public override bool Step(int row, int dir)
    {
        if (Scratch.Engine == row)
        {
            return false;
        }

        Scratch.Engine = row;
        return true;
    }
}

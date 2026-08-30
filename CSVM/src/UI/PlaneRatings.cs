using System;
using CSVM.Flight;

namespace CSVM.UI;

/// <summary>
/// The four ratings the plane selection screen prints beside an aircraft (<c>PS_T_TOPSPEEDP</c> and
/// its three neighbours), each as a 0-to-4 index into langui 501-505, Poor to Excellent.
///
/// <para>⚠ Only <see cref="Agility"/> is decoded. The other three are stand-ins built from the
/// airframe stat table, with thresholds chosen so the two aircraft the reference screenshots show
/// read as they do there, and nothing else pins them: `BL-653` carries what a real decode has to
/// settle. Do not read a number here as the original's.</para>
/// </summary>
public static class PlaneRatings
{
    // Where a plane's power-to-weight ratio crosses from one word to the next. Bloodhawk (0.124)
    // and Devastator (0.072) both read Average in the reference, so the middle band spans both.
    private static readonly double[] SpeedBands = { 0.04, 0.07, 0.13, 0.16 };

    // Where an offense score crosses. Bloodhawk's fit scores 16 and reads Fair, Devastator's
    // scores 28 and reads Average, which is what sets the first two.
    private static readonly int[] OffenseBands = { 10, 20, 32, 44 };

    /// <summary>The four ratings in the order the screen prints them: top speed, armour, agility,
    /// offense.</summary>
    public static int[] For(int airframe, PlaneFit fit)
    {
        int id = Math.Clamp(airframe, 0, HangarEconomy.Airframes.Length - 1);
        return new[] { Speed(id, fit), Armour(id, fit), Agility(id), Offense(fit) };
    }

    /// <summary>The one decoded rating: <see cref="HangarEconomy"/>'s own agility star formula,
    /// which reproduces both reference aircraft (Bloodhawk 19 reads Excellent, Devastator 10 reads
    /// Average).</summary>
    public static int Agility(int airframe) =>
        Math.Clamp((HangarEconomy.Airframes[airframe].Agility - 1) / 4, 0, 4);

    // ⚠ Stand-in. The hangar's own armour star formula ((armour + units*5 - 1) / 0x49) reads Fair
    // for both reference aircraft where the screen prints Average, so it is not what this widget
    // shows; halving-to-fifty is the coarser rule that matches both. BL-653.
    private static int Armour(int airframe, PlaneFit fit)
    {
        int armour = HangarEconomy.Airframes[airframe].Armour + (fit.ArmourUnits * 5);
        return Math.Clamp((int)Math.Round(armour / 50.0), 0, 4);
    }

    // ⚠ Stand-in. Power over weight, the two quantities the airframe table does carry. It ranks the
    // autogyro highest, which no reading of the original would; BL-653 names that as the thing a
    // decode has to fix.
    private static int Speed(int airframe, PlaneFit fit)
    {
        var stats = HangarEconomy.Airframes[airframe];
        double power = HangarEconomy.EngineBases[airframe].Power
            * HangarEconomy.EnginePowerFactors[
                Math.Clamp(fit.Engine, 0, HangarEconomy.EnginePowerFactors.Length - 1)];
        return Band(power / Math.Max(1, stats.Weight), SpeedBands);
    }

    // ⚠ Stand-in. One point per ten millimetres of every barrel, plus one per hardpoint. BL-653.
    private static int Offense(PlaneFit fit)
    {
        int score = fit.Hardpoints;
        foreach (var (calibre, barrels) in fit.Barrels)
        {
            score += calibre / 10 * barrels;
        }

        int at = 0;
        while (at < OffenseBands.Length && score >= OffenseBands[at])
        {
            at++;
        }

        return at;
    }

    private static int Band(double value, double[] bands)
    {
        int at = 0;
        while (at < bands.Length && value >= bands[at])
        {
            at++;
        }

        return at;
    }
}

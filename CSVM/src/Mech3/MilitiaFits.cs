using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// The militia table read once against <c>vehicle.json</c>: each militia's paint pattern
/// (<see cref="MilitiaPaint"/>) and the def it flies each airframe as
/// (<see cref="VehicleDefs.MilitiaDefFor"/>). A bot seat is painted and armed through it.
/// <see cref="Empty"/> names no pattern and no def, for an install whose file cannot be read.
/// </summary>
public sealed class MilitiaFits
{
    private readonly IReadOnlyDictionary<string, string> _patterns;
    private readonly VehicleDefs _defs;

    private MilitiaFits(IReadOnlyDictionary<string, string> patterns, VehicleDefs defs)
    {
        _patterns = patterns;
        _defs = defs;
    }

    /// <summary>A reading with no pattern and no def for any militia.</summary>
    public static MilitiaFits Empty { get; } =
        new(new Dictionary<string, string>(), VehicleDefs.FromRoot(new List<object?>()));

    /// <summary>Reads the shared zrdr scope's <c>vehicle.json</c> once for both halves, naming each
    /// militia through <paramref name="messages"/>.</summary>
    public static MilitiaFits Load(string zrdrPath, Messages messages)
    {
        var root = Zrdr.LoadFile(zrdrPath, "vehicle.json")[0] as List<object?>
            ?? throw new InvalidOperationException("vehicle.json: unexpected root shape");
        return new MilitiaFits(MilitiaPaint.PatternByMilitia(root, messages), VehicleDefs.FromRoot(root));
    }

    /// <summary>The pattern a militia of the militia table wears, null for Fortune Hunter and
    /// Broadway Bomber, which name none.</summary>
    public string? PatternFor(string militia) => MilitiaPaint.PatternFor(_patterns, militia);

    /// <summary>The def a militia flies <paramref name="planeNode"/> as, whose weapons block its
    /// aircraft carry. Null where the militia names no pattern or the install ships no such def.</summary>
    public string? DefFor(string militia, string planeNode) =>
        PatternFor(militia) is { } pattern ? _defs.MilitiaDefFor(pattern, planeNode) : null;
}

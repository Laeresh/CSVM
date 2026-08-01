using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Per-part hit points for the flying aircraft, backed by the
/// vehicle def's 'destroyable_parts' (see <see cref="DestroyablePart"/>): the
/// player planes carry nose / tail / leftwing / rightwing, all 'critical' — the
/// plane is destroyed when any reaches 0 HP. FlightController applies
/// severity-scaled damage on survivable collisions and crashes outright on hard
/// ones; this class only owns the HP state and the collider-box → data-part
/// mapping. Respawn calls <see cref="Reset"/>.
/// </summary>
public sealed class PlaneDamage
{
    private readonly Dictionary<string, PartState> _parts = new(System.StringComparer.OrdinalIgnoreCase);

    public PlaneDamage(IEnumerable<DestroyablePart> defs)
    {
        foreach (var def in defs)
            _parts[def.Name] = new PartState { Def = def, Hp = def.MaxHp };
    }

    public IReadOnlyDictionary<string, PartState> Parts => _parts;

    /// <summary>Worst (lowest) HP fraction across all parts — 1f (pristine) when there are no parts
    /// or none has taken damage. Drives whole-plane damage feedback keyed to "how hurt is the
    /// airframe" rather than any one part (e.g. FlightAudio's damaged-engine loop).</summary>
    public float WorstFraction => _parts.Count == 0 ? 1f : _parts.Values.Min(p => p.Fraction);

    /// <summary>Maps the struck collider box (fuselage/wing/canard/tail, or the
    /// backstop ray's "center") + the impact point in the PLANE's local frame to
    /// the data's part name: wings split by side (x &lt; 0 = left — verified against
    /// the planes.zbd node boxes), the fuselage fore/aft between nose and tail.</summary>
    public static string MapStruckPart(string colliderPart, Vector3 localImpact) => colliderPart switch
    {
        "wing" or "canard" => localImpact.X < 0f ? "leftwing" : "rightwing",
        "tail" => "tail",
        _ => localImpact.Z < 0f ? "nose" : "tail", // fuselage / center backstop
    };

    public void Reset()
    {
        foreach (var p in _parts.Values)
            p.Hp = p.Def.MaxHp;
    }

    /// <summary>Subtracts damage from a part; returns its state after (null if the
    /// data defines no such part — then nothing was tracked).</summary>
    public PartState? Apply(string partName, float damage)
    {
        if (!_parts.TryGetValue(partName, out var p))
            return null;
        p.Hp = Mathf.Max(0f, p.Hp - damage);
        return p;
    }

    /// <summary>"nose 100% · tail 85% · …" — parts below full only; "" when pristine.</summary>
    public string Summary()
    {
        var hurt = _parts.Values.Where(p => p.Hp < p.Def.MaxHp).ToList();
        return hurt.Count == 0
            ? ""
            : string.Join(" · ", hurt.Select(p => $"{p.Def.Name} {p.Fraction * 100f:0}%"));
    }

    public sealed class PartState
    {
        public float Hp;
        public required DestroyablePart Def { get; init; }
        public float Fraction => Def.MaxHp > 0f ? Hp / Def.MaxHp : 0f;
    }
}

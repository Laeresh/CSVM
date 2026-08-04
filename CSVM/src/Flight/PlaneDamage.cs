using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Per-part armor and hit points for the flying aircraft, backed by the
/// vehicle def's 'destroyable_parts' (see <see cref="DestroyablePart"/>): the
/// player planes carry nose / tail / leftwing / rightwing, all 'critical' — the
/// plane is destroyed when any reaches 0 HP. Armor at 0 is a stripped zone, not a
/// dead one; it just takes the round's health magnitude from then on. FlightController
/// applies severity-scaled damage on survivable collisions and crashes outright on hard
/// ones; this class only owns the two pools and the collider-box → data-part
/// mapping. Respawn calls <see cref="Reset"/>.
/// </summary>
public sealed class PlaneDamage
{
    private readonly Dictionary<string, PartState> _parts = new(System.StringComparer.OrdinalIgnoreCase);

    public PlaneDamage(IEnumerable<DestroyablePart> defs)
    {
        foreach (var def in defs)
            _parts[def.Name] = new PartState { Def = def, Hp = def.MaxHp, Armor = def.MaxArmor };
    }

    public IReadOnlyDictionary<string, PartState> Parts => _parts;

    /// <summary>Worst (lowest) combined armor+HP fraction across all parts — 1f (pristine) when
    /// there are no parts or none has taken damage. Drives whole-plane damage feedback keyed to
    /// "how hurt is the airframe" rather than any one part (e.g. FlightAudio's damaged-engine
    /// loop).</summary>
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
        {
            p.Hp = p.Def.MaxHp;
            p.Armor = p.Def.MaxArmor;
        }
    }

    /// <summary>Spends one round's two damage magnitudes (the weapon data's ARMOR_DAMAGE /
    /// HEALTH_DAMAGE) against a part: armor absorbs first, and the share of the round armor
    /// could not absorb carries over into health within the same shot, so a nearly-stripped
    /// zone never wastes a hit. Health takes the round's *health* magnitude, scaled by that
    /// carried-over share — an unarmored zone therefore takes exactly HEALTH_DAMAGE, which is
    /// what makes AP (armor-heavy, health-light) punch through armor and do little to bare
    /// airframe. For a round whose two magnitudes are equal the carry-over is point-for-point.
    /// Returns the state after (null if the data defines no such part — then nothing was
    /// tracked).</summary>
    public PartState? Apply(string partName, float healthDamage, float armorDamage)
    {
        if (!_parts.TryGetValue(partName, out var p))
            return null;
        float unabsorbed = 1f; // share of the round left over once armor took its bite
        if (armorDamage > 0f)
        {
            float spent = Mathf.Min(p.Armor, armorDamage);
            p.Armor = Mathf.Max(0f, p.Armor - spent);
            unabsorbed = (armorDamage - spent) / armorDamage;
        }
        if (healthDamage > 0f)
            p.Hp = Mathf.Max(0f, p.Hp - healthDamage * unabsorbed);
        return p;
    }

    /// <summary>Spends a single damage magnitude — a collision, which the data gives equal
    /// armor and health ranges (player.json's 'crash' block). Armor still shields first, and
    /// the total spent across both pools is exactly the magnitude.</summary>
    public PartState? Apply(string partName, float damage) => Apply(partName, damage, damage);

    /// <summary>"nose a0% h85% · tail a60% · …" — parts below full only, armor pool then health
    /// pool, each omitted while it is untouched; "" when pristine.</summary>
    public string Summary()
    {
        var hurt = _parts.Values.Where(p => p.Hp < p.Def.MaxHp || p.Armor < p.Def.MaxArmor).ToList();
        return hurt.Count == 0 ? "" : string.Join(" · ", hurt.Select(PoolText));
    }

    private static string PoolText(PartState p)
    {
        string text = p.Def.Name;
        if (p.Def.MaxArmor > 0f)
            text += $" a{p.ArmorFraction * 100f:0}%";
        if (p.Hp < p.Def.MaxHp || p.Def.MaxArmor <= 0f)
            text += $" h{p.HealthFraction * 100f:0}%";
        return text;
    }

    public sealed class PartState
    {
        public float Hp;
        public float Armor;
        public required DestroyablePart Def { get; init; }

        /// <summary>The combined sequential fraction — both pools against both maxima. Damage
        /// walks it down through armor and then health as one progression, which is the scale
        /// the def's injure_anims thresholds are on.</summary>
        public float Fraction => Def.MaxHp + Def.MaxArmor > 0f
            ? (Hp + Armor) / (Def.MaxHp + Def.MaxArmor)
            : 0f;

        public float HealthFraction => Def.MaxHp > 0f ? Hp / Def.MaxHp : 0f;

        /// <summary>0f for a part the data gives no armor pool — an unarmored zone, not a full
        /// one.</summary>
        public float ArmorFraction => Def.MaxArmor > 0f ? Armor / Def.MaxArmor : 0f;
    }
}

using System.Collections.Generic;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="AircraftContactResolver.Resolve"/> — the decoded contact rules as a table, taken
/// apart from <c>FlightController</c> and the physics world entirely: no <c>Node</c>, no
/// <see cref="GodotWorldQuery"/>, a synthetic <see cref="IWorldQuery"/> only where the un-embed
/// loop needs one. Decode: docs/org/flightModel.md's "Collision damage" and its entity-cut note.
/// ⚠ Entity detection (the 0.2 cut and the doom rule) is the non-player branch only, and only
/// against another aeroplane — a case that only checks
/// <see cref="ContactOutcome.DamageStruckAircraft"/> would pass whether or not the cut actually
/// applied, so every row here asserts the damage MAGNITUDE, not just the flags.
/// </summary>
public class AircraftContactResolverTests
{
    // player.json's crash block, the same floor/scale CollisionDamageTests uses.
    private const float Scale = 200f;

    // A head-on contact: severity 1, so Term collapses to the authored scale alone.
    private static readonly Vector3 HeadOnVelocity = Vector3.Forward;
    private static readonly Vector3 HeadOnNormal = -Vector3.Forward;

    /// <summary>An AI that rams anything OTHER than an aeroplane dies outright (local_11), whatever
    /// health it carries — asserted against a striker with a live ledger so the rule is not merely
    /// unreachable through the no-ledger speed fallback above it.</summary>
    [Fact]
    public void AnAiRammingANonAeroplaneIsDoomedOutright()
    {
        var resolver = new AircraftContactResolver(new NeverOverlaps());
        var effects = new FakeContactEffects();
        var ledger = OneZone();

        var outcome = resolver.Resolve(HeadOn(struckIsAircraft: false), Striker(humanPiloted: false, ledger), effects);

        Assert.True(outcome.Dooms);
        Assert.Equal(ContactFate.Crash, outcome.Fate);
        // No entity cut on a non-aircraft target: the full pair, not the fifth.
        Assert.Equal(Scale, outcome.ArmorDamage, 3);
        Assert.Equal(Scale, outcome.HealthDamage, 3);
        Assert.False(outcome.DamageStruckAircraft);
        // ShatterStruck ran (asked whether the struck def broke) and answered false: a real
        // def, not set-dressing, so the contact fell through to the doom rule below it.
        Assert.True(effects.ShatterStruckCalled);
    }

    /// <summary>An AI into another aeroplane takes the entity cut (a fifth) instead of the doom
    /// rule, and survives to hand the struck plane its share — the trap case: a table that only
    /// checked <c>DamageStruckAircraft</c> would pass with the cut silently missing.</summary>
    [Fact]
    public void AiIntoAiTakesTheEntityCutNotTheDoomRule()
    {
        var resolver = new AircraftContactResolver(new NeverOverlaps());
        var effects = new FakeContactEffects();

        var outcome = resolver.Resolve(HeadOn(struckIsAircraft: true), Striker(humanPiloted: false), effects);

        Assert.False(outcome.Dooms);
        Assert.True(outcome.DamageStruckAircraft);
        Assert.Equal(Scale * CollisionDamage.EntityCut, outcome.ArmorDamage, 3);
        Assert.Equal(Scale * CollisionDamage.EntityCut, outcome.HealthDamage, 3);
    }

    /// <summary>The player skips entity detection outright: ramming another aeroplane costs the
    /// FULL pair, not the fifth, and never dooms — the asymmetry <see cref="ContactConditions"/>
    /// warns against losing.</summary>
    [Fact]
    public void APlayerIsExemptFromBothTheCutAndTheDoomRule()
    {
        var resolver = new AircraftContactResolver(new NeverOverlaps());
        var effects = new FakeContactEffects();

        var outcome = resolver.Resolve(HeadOn(struckIsAircraft: true), Striker(humanPiloted: true), effects);

        Assert.False(outcome.Dooms);
        Assert.True(outcome.DamageStruckAircraft);
        Assert.Equal(Scale, outcome.ArmorDamage, 3);
        Assert.Equal(Scale, outcome.HealthDamage, 3);
    }

    /// <summary>A plane ground to (near) standstill is destroyed rather than left parked there: the
    /// ground stop reads <see cref="ContactResponse.Speed"/> alone, assertable with no
    /// <c>FlightModel</c> behind it at all.</summary>
    [Fact]
    public void SlidingBelowGrazeStopSpeedDestroysThePlane()
    {
        var resolver = new AircraftContactResolver(new NeverOverlaps());
        var effects = new FakeContactEffects
        {
            ApplyResponseResult = new ContactResponse(AircraftContactResolver.GrazeStopSpeed - 1f, Transform3D.Identity),
        };
        // A player striking terrain (StruckIsAircraft false): no doom rule, no entity cut, and the
        // report never reaches ShatterStruck because a graze on real geometry does not shatter.
        var striker = Striker(humanPiloted: true, ledger: OneZone());

        var outcome = resolver.Resolve(HeadOn(struckIsAircraft: false), striker, effects);

        Assert.Equal(ContactFate.Crash, outcome.Fate);
        Assert.True(effects.ApplyResponseCalled);
    }

    /// <summary>A plane that clears <see cref="AircraftContactResolver.GrazeStopSpeed"/> but is
    /// still embedded after the response is pushed out along the contact normal, three tries at
    /// a time; giving up EXPLODES it rather than letting it tunnel — the un-embed loop's own
    /// worked case.</summary>
    [Fact]
    public void AnEmbeddedPlaneIsPushedOutThreeTimesThenExplodes()
    {
        var resolver = new AircraftContactResolver(new AlwaysOverlaps());
        var effects = new FakeContactEffects
        {
            ApplyResponseResult = new ContactResponse(AircraftContactResolver.GrazeStopSpeed + 1f, Transform3D.Identity),
        };
        var striker = Striker(humanPiloted: true, ledger: OneZone()) with
        {
            // The fake IWorldQuery below ignores Parts entirely; a real BoxShape3D needs a live
            // engine to construct at all, and this suite runs with none.
            Parts = new List<PlaneCollider.Part> { new("center", null!, Transform3D.Identity) },
        };

        var outcome = resolver.Resolve(HeadOn(struckIsAircraft: false), striker, effects);

        Assert.Equal(ContactFate.Crash, outcome.Fate);
        Assert.Equal(0.9f, outcome.PushOut.Length(), 3);
    }

    /// <summary>Struck set-dressing that shatters ends the contact immediately: the striker keeps
    /// its full-motion pose and flies through, so nothing past
    /// <see cref="IContactEffects.ShatterStruck"/> runs at all — not the graze reaction, not the
    /// damage spend, not the response.</summary>
    [Fact]
    public void ShatteringSetDressingEndsTheContactWithNothingElseRun()
    {
        var resolver = new AircraftContactResolver(new NeverOverlaps());
        var effects = new FakeContactEffects { ShatterStruckResult = true };
        var striker = Striker(humanPiloted: false, ledger: OneZone());

        var outcome = resolver.Resolve(HeadOn(struckIsAircraft: false), striker, effects);

        Assert.Equal(ContactFate.Graze, outcome.Fate); // the default: no fate is ever decided
        Assert.True(effects.ShatterStruckCalled);
        Assert.False(effects.PlayGrazeReactionCalled);
        Assert.False(effects.SpendDamageCalled);
        Assert.False(effects.ApplyResponseCalled);
    }

    /// <summary>A graze spends real armour before health on the struck zone: the decoded pair runs
    /// through <see cref="PlaneDamage.Apply"/>'s armour-first split, so a fully-armoured zone loses
    /// no health at all, and only once the armour is gone does health start to drain — the control
    /// half, which a symmetric or health-first spend fails.</summary>
    [Fact]
    public void AGrazeSpendsArmourBeforeHealthOnTheStruckZone()
    {
        var resolver = new AircraftContactResolver(new NeverOverlaps());
        var ledger = OneZone();
        var effects = new LedgerContactEffects(ledger);
        // A shallow scrape: severity 0.1, so both terms sit on the compiled floor (15).
        var striker = Striker(humanPiloted: true, ledger) with
        {
            VelocityDir = new Vector3(-0.99499f, 0f, -0.1f),
            DamageCooldownElapsed = true,
        };
        var shallow = HeadOn(struckIsAircraft: false) with { Normal = new Vector3(0f, 0f, 1f) };

        var first = resolver.Resolve(shallow, striker, effects);
        Assert.Equal(ContactFate.Graze, first.Fate);
        Assert.Equal(5f, effects.LastState!.Armor, 3);            // 20 − 15: armour spent
        Assert.Equal(20f, effects.LastState!.Hp, 3);              // health untouched behind it

        var second = resolver.Resolve(shallow, striker, effects);
        Assert.Equal(ContactFate.Graze, second.Fate);
        Assert.Equal(0f, effects.LastState!.Armor, 3);            // the remaining 5 absorbs a third
        Assert.Equal(10f, effects.LastState!.Hp, 3);              // 20 − 15·(1 − 5/15)
    }

    private static ContactReport HeadOn(bool struckIsAircraft) => new()
    {
        Impact = Vector3.Zero,
        Normal = HeadOnNormal,
        Part = "center",
        ColliderName = struckIsAircraft ? "P2" : "m_build01",
        StopFraction = 1f,
        StruckIsAircraft = struckIsAircraft,
    };

    private static ContactConditions Striker(bool humanPiloted, PlaneDamage? ledger = null) => new()
    {
        IsHumanPiloted = humanPiloted,
        VelocityDir = HeadOnVelocity,
        Speed = 50f,
        Pose = Transform3D.Identity,
        Stats = null,             // falls back to the compiled Floor/Scale defaults
        Ledger = ledger,
        DamageCooldownElapsed = false,
        Parts = null,
        ExcludeSelf = null,
    };

    private static PlaneDamage OneZone() => new(new List<DestroyablePart>
    {
        new() { Name = "nose", MaxHp = 20f, MaxArmor = 20f, Critical = true },
    });

    // ---- fakes ---------------------------------------------------------------------------------

    /// <summary>Every <see cref="IContactEffects"/> stub, each individually scriptable and each
    /// recording whether it ran — the shatter-and-fly-through row needs to prove the ones AFTER it
    /// did not.</summary>
    private sealed class FakeContactEffects : IContactEffects
    {
        public bool ShatterStruckResult;
        public ContactResponse ApplyResponseResult;

        public bool ShatterStruckCalled;
        public bool PlayGrazeReactionCalled;
        public bool SpendDamageCalled;
        public bool ApplyResponseCalled;

        public bool ShatterStruck(float healthDamage)
        {
            ShatterStruckCalled = true;
            return ShatterStruckResult;
        }

        public void PlayGrazeReaction() => PlayGrazeReactionCalled = true;

        public PlaneDamage.PartState? SpendDamage(string zone, float healthDamage, float armorDamage)
        {
            SpendDamageCalled = true;
            return null;
        }

        public ContactResponse ApplyResponse()
        {
            ApplyResponseCalled = true;
            return ApplyResponseResult;
        }
    }

    /// <summary>Effects whose damage spend routes into a REAL <see cref="PlaneDamage"/>, so the
    /// armour-before-health row asserts the ledger's split rather than a stub's echo.</summary>
    private sealed class LedgerContactEffects : IContactEffects
    {
        private readonly PlaneDamage _ledger;

        public LedgerContactEffects(PlaneDamage ledger) => _ledger = ledger;

        public PlaneDamage.PartState? LastState { get; private set; }

        public bool ShatterStruck(float healthDamage) => false;

        public void PlayGrazeReaction()
        {
        }

        public PlaneDamage.PartState? SpendDamage(string zone, float healthDamage, float armorDamage)
        {
            LastState = _ledger.Apply(zone, healthDamage, armorDamage);
            return LastState;
        }

        public ContactResponse ApplyResponse() =>
            new(AircraftContactResolver.GrazeStopSpeed + 30f, Transform3D.Identity);
    }

    private sealed class NeverOverlaps : IWorldQuery
    {
        public bool Sweep(IReadOnlyList<PlaneCollider.Part> parts, Transform3D baseTransform, Vector3 motion,
            uint mask, Godot.Collections.Array<Rid>? exclude, out SweepReport report)
        {
            report = default;
            return false;
        }

        public bool Ray(Vector3 from, Vector3 to, uint mask, Godot.Collections.Array<Rid>? exclude,
            out RayReport report)
        {
            report = default;
            return false;
        }

        public bool Overlaps(IReadOnlyList<PlaneCollider.Part> parts, Transform3D pose, uint mask,
            Godot.Collections.Array<Rid>? exclude) => false;
    }

    private sealed class AlwaysOverlaps : IWorldQuery
    {
        public bool Sweep(IReadOnlyList<PlaneCollider.Part> parts, Transform3D baseTransform, Vector3 motion,
            uint mask, Godot.Collections.Array<Rid>? exclude, out SweepReport report)
        {
            report = default;
            return false;
        }

        public bool Ray(Vector3 from, Vector3 to, uint mask, Godot.Collections.Array<Rid>? exclude,
            out RayReport report)
        {
            report = default;
            return false;
        }

        public bool Overlaps(IReadOnlyList<PlaneCollider.Part> parts, Transform3D pose, uint mask,
            Godot.Collections.Array<Rid>? exclude) => true;
    }
}

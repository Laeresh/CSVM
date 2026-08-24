using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CSVM.Flight;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The plant's constant inventory: every non-authored number in the live translational and
/// rotational path, its classification, and the reachability measurement behind each CSVM
/// safeguard. The table this mirrors is docs/org/flightModel.md, "The plant's constant inventory".
/// A constant added, removed or moved off its recorded value fails <see cref="TheInventoryIsComplete"/>,
/// which is what stops a new fitted number arriving unclassified; the safeguard tests below fail
/// when a classification's binding claim goes stale.
/// </summary>
public class FlightConstantInventoryTests
{
    private const float Dt = 1f / 60f;

    // The four classes docs/org/flightModel.md's inventory table uses: read out of crimson.exe, a
    // mirror of an authored data key, a conversion factor, or a deliberate CSVM exception with a
    // reachability measurement behind it. The contact rules are censused with the plant, so a
    // fitted contact term cannot come back where the plant's own census cannot see it.
    private const string Decoded = "decoded";
    private const string Authored = "authored";
    private const string Unit = "unit";
    private const string ProductException = "exception";

    private static readonly string[] AllPlanes =
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro",
        "player_avenger", "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel",
        "player_peacemaker",
    };

    // The inventory itself: every const the plant carries, its value and its class. Evidence per
    // row (executable address, data key or footage capture) is in docs/org/flightModel.md; this
    // table is the machine-checked half, so a row here is a claim that the doc row still applies.
    private static readonly (string Type, string Name, double Value, string Class)[] Inventory =
    {
        ("FlightModel", "ThrustMachFloor", 0.1, Decoded),
        ("FlightModel", "ThrustVRefSlope", 0.84, Decoded),
        ("FlightModel", "ThrustVRefMach", 0.112, Decoded),
        ("FlightModel", "ThrustMachTrim", 1.0 / 60.0, Decoded),
        ("FlightModel", "ThrustPowMach", 1.41, Decoded),
        ("FlightModel", "ThrustPowBase", 1.33 * 0.98842078, Decoded),
        ("FlightModel", "AttitudeThrustBoth", 0.24, Decoded),
        ("FlightModel", "AttitudeThrustUp", 0.13, Decoded),
        ("FlightModel", "LiftGMin", -5.0, Decoded),
        ("FlightModel", "LiftGMax", 9.0, Decoded),
        ("FlightModel", "ClMaxStatic", 0.75, Decoded),
        ("FlightModel", "ClMaxMach", 0.15, Decoded),
        ("FlightModel", "AirDensitySlugPerFt3", 2.2688e-3, Decoded),
        ("FlightModel", "SpeedOfSoundFps", 1109.5, Decoded),
        ("FlightModel", "FeetPerMetre", 3.28084, Unit),
        ("FlightModel", "MetresPerFoot", 0.3048, Unit),
        ("FlightModel", "StandardG", 9.82, Decoded),
        ("FlightModel", "StallWarnFrac", 0.30, ProductException),
        ("FlightModel", "MaxDiveSpeedFrac", 1.75, ProductException),
        ("FlightModel", "AltitudeCapM", 2003.0, ProductException),
        ("FlightModel", "GroundBlowIntoFactor", 0.05, Decoded),
        ("FlightModel", "GroundBlowVelocitySteer", 2.0, Decoded),
        ("FlightModel", "NoseChaseFactor", 0.0, Decoded),
        ("FlightModel", "AoaLimiterFactorDefault", 0.0, ProductException),
        ("FlightModel", "BounceLeverScale", 2.25, Decoded),
        ("FlightModel", "ContactPushOut", 0.03, Decoded),
        ("FlightModel", "BounceAngularHalf", 0.5, Decoded),
        ("FlightModel", "DragPolarScale", 0.73, Decoded),
        ("FlightModel", "DragPolarParasite", 0.12, Decoded),
        ("FlightModel", "DragPolarLinear", 0.8, Decoded),
        ("FlightModel", "DragPolarQuad", 0.5, Decoded),
        ("FlightModel", "PitchTune", 1.0, Decoded),
        ("FlightModel", "YawTune", 1.0, Decoded),
        ("FlightModel", "RollTune", 1.0, Decoded),
        ("FlightModel", "BankYawCoupling", 0.205, Decoded),
        ("FlightModel", "BankPitchCoupling", 0.165, Decoded),
        ("FlightModel", "WeathervaneHalfAngle", 0.5, Decoded),
        ("FlightModel", "AiNoseSpeedFloor", 4.4704, Decoded),
        ("FlightModel", "ReverseAuthorityFloor", 0.2, Decoded),
        ("FlightModel", "FarFieldRangeM", 1000.0, Decoded),
        ("FlightModel", "FarFieldAiSpeedBonus", 5.0, Decoded),
        ("PhysicsConstants", "NomGravity", 20.0, Authored),
        ("PhysicsConstants", "MphToMs", 0.44704, Decoded),
        ("StickRamp", "Rate", 2.5, Decoded),
        ("CollisionDamage", "EntityCut", 0.2, Decoded),
        ("CollisionDamage", "EntityGrace", 1.0, Decoded),
        ("CollisionDamage", "SpawnGrace", 1.5, Decoded),
        ("AircraftContactResolver", "EmbedPushOut", 0.3, ProductException),
        ("AircraftContactResolver", "EmbedTries", 3.0, ProductException),
    };

    // The plant's whole config surface. Each key read-throughs one inventory row above, so a key
    // added without a constant (or a constant exposed without a doc row) fails the census below.
    private static readonly string[] ConfigKeys =
    {
        "altitudeCapM", "aoaLimiterFactor", "liftGMax", "liftGMin", "noseChaseFactor", "pitchTune",
        "rollTune", "stallWarnFrac", "yawTune",
    };

    private static readonly Type[] InventoryTypes =
    {
        typeof(FlightModel), typeof(PhysicsConstants), typeof(StickRamp), typeof(CollisionDamage),
        typeof(AircraftContactResolver),
    };

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>Every constant the plant carries is in the inventory at the recorded value, and the
    /// inventory names nothing the plant has dropped. A new constant is unclassified until its row
    /// is added here and its evidence in docs/org/flightModel.md.</summary>
    [Fact]
    public void TheInventoryIsComplete()
    {
        var live = LiveConstants();
        var known = Inventory.ToDictionary(x => $"{x.Type}.{x.Name}", x => x);

        var added = live.Keys.Where(k => !known.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(added.Count == 0,
            $"unclassified plant constant(s): {string.Join(", ", added)} — add a row to "
            + "FlightConstantInventoryTests.Inventory and its evidence to docs/org/flightModel.md, "
            + "\"The plant's constant inventory\". A constant with no provenance is a fitted "
            + "constant until someone proves otherwise");

        var gone = known.Keys.Where(k => !live.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(gone.Count == 0,
            $"the inventory names constant(s) the plant no longer has: {string.Join(", ", gone)} — "
            + "delete the row here and the matching row in docs/org/flightModel.md");

        foreach (var (key, value) in live.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var row = known[key];
            Assert.True(Math.Abs(value - row.Value) <= Math.Abs(row.Value) * 1e-6 + 1e-9,
                $"{key} is {value:R}, the inventory records {row.Value:R} as {row.Class} — a moved "
                + "value needs its evidence re-checked in docs/org/flightModel.md before the row "
                + "here is updated to match it");
        }
    }

    /// <summary>The three per-axis calibration factors are exactly 1, which is the decode's finding
    /// rather than a tuning choice: <c>FUN_0048c470</c> builds each axis' stick torque as
    /// torque · stick · authority · dt and multiplies in nothing else. They survive as named
    /// constants so the config keys stay available for an A/B at the controls.</summary>
    [Fact]
    public void NoAxisCarriesACalibrationFactor()
    {
        foreach (string axis in new[] { "PitchTune", "YawTune", "RollTune" })
        {
            double v = LiveConstants()[$"FlightModel.{axis}"];
            Assert.True(v == 1.0,
                $"{axis} is {v:R} — the original's torque chain carries no per-axis factor "
                + "(docs/org/flightModel.md, \"The *Tune rates\"), so restoring one needs a "
                + "mechanism traced in the binary and never a rate timed off footage");
        }
    }

    /// <summary>The plant exposes exactly the config keys the inventory accounts for. The dump is
    /// the same registry <c>--dump-config</c> emits, so a key added to Step without an inventory row
    /// shows up here rather than in a template nobody reads.</summary>
    [Fact]
    public void TheConfigSurfaceIsAccountedFor()
    {
        var warm = new FlightModel(new PlaneStats());
        warm.Reset(Vector3.Zero, Basis.Identity, 100f, 1f);
        warm.Step(default, Dt);
        _ = warm.IsStallWarned();

        string path = Path.Combine(TestData.TempDir(), "config-template.json");
        Config.DumpConfig(path);
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        var keys = doc.RootElement.GetProperty("flightModel").EnumerateObject()
            .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(ConfigKeys.SequenceEqual(keys),
            $"the flightModel config block is [{string.Join(", ", keys)}] against the accounted-for "
            + $"[{string.Join(", ", ConfigKeys)}] — every key overrides one inventory row, so add "
            + "or drop the row alongside it");
    }

    /// <summary>The dive-speed cap is a numerical backstop and must not bind: a cap that binds
    /// replaces a measured terminal with a guess. Flown on every airframe in the manoeuvres that
    /// make the most speed, the peak stays under the cap by a wide margin, and the aerodynamics
    /// terminate every dive on their own.</summary>
    [ExtractedDataFact]
    public void TheDiveSpeedCapNeverBinds()
    {
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            var (peak, where) = FastestFlight(stats);
            Assert.True(peak < 1.35f,
                $"{plane}: peak speed reaches {peak:0.000} x fd_speed ({where}) against the "
                + "MaxDiveSpeedFrac backstop at 1.75 — the cap is close enough to bind, and a bound "
                + "cap reports itself instead of the model's own terminal speed");
        }
    }

    /// <summary>The able-to-fail control for the dive-cap disproof: the manoeuvres must still make
    /// real speed. A set that no longer exceeds fd_speed would pass the test above while measuring
    /// nothing (METHOD-9).</summary>
    [ExtractedDataFact]
    public void TheDiveSpeedCapDisproofIsAbleToFail()
    {
        var stats = PlaneStats.Load(ZrdrPath, "player_bhawk");
        var (peak, where) = FastestFlight(stats);
        Assert.True(peak > 1.05f,
            $"player_bhawk: the fastest manoeuvre peaks at only {peak:0.000} x fd_speed ({where}) — "
            + "it has gone too gentle to approach any cap, so the dive-cap disproof measures nothing");
    }

    /// <summary>The altitude cap is a product exception that DOES bind, which is why it is kept: a
    /// sustained climb settles against it rather than climbing on. If a climb stops reaching it the
    /// clamp has become dead code and the exception is no longer earning its place.</summary>
    [ExtractedDataFact]
    public void TheAltitudeCapBinds()
    {
        var stats = PlaneStats.Load(ZrdrPath, "player_bhawk");
        var m = Climb(stats, 1900f, 22f, 240f, out float overshoot);
        Assert.True(m.Position.Y >= 2003f - 1f && overshoot < 5f,
            $"player_bhawk: a 22° full-throttle climb settles at {m.Position.Y:0.0} m against the "
            + $"2003 m cap, overshooting {overshoot:0.00} m — the cap is meant to bind here "
            + "(docs/org/flightModel.md, \"The resting altitude cap\")");
    }

    /// <summary>Overshoot above the cap is bounded by one frame's climbing velocity, because the
    /// clamp deletes that velocity instead of fading it. Measured on every airframe from the
    /// steepest entry, which is why no separate overshoot constant is carried.</summary>
    [ExtractedDataFact]
    public void OvershootAboveTheAltitudeCapIsOneFrameOfClimb()
    {
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            Climb(stats, 1950f, 89f, 60f, out float overshoot);
            float perFrame = 1.75f * stats.FdSpeed * Dt;
            Assert.True(overshoot <= perFrame,
                $"{plane}: the aircraft reached {overshoot:0.00} m above the 2003 m cap, more than "
                + $"the {perFrame:0.00} m one frame of climb can carry it — something now coasts "
                + "past the clamp and the overshoot needs a mechanism, not a constant");
        }
    }

    /// <summary>No shipped airframe reaches the resolver's no-damage-data arm, where every contact
    /// is fatal because there is no pool to survive on. Every player load authors zones and every AI
    /// load a whole armour/health pair, so the arm covers a bare rig alone and carries no speed
    /// threshold of its own.</summary>
    [ExtractedDataFact]
    public void NoStockAirframeFliesWithoutADamageLedger()
    {
        foreach (string plane in AllPlanes)
        {
            var player = PlaneStats.Load(ZrdrPath, plane);
            Assert.True(player.DestroyableParts.Count > 0,
                $"{plane}: the player load authors no destroyable_parts, so a contact would take the "
                + "resolver's no-ledger arm and be fatal at any speed");
            var ai = PlaneStats.LoadForAi(ZrdrPath, plane);
            Assert.True(ai.DestroyableParts.Count > 0 || ai.VehicleHealth is > 0f,
                $"{plane}: the AI load authors neither zones nor a whole health pair, so an AI "
                + "contact would take the resolver's no-ledger arm");
        }
    }

    /// <summary>The per-airframe margins behind the two safeguard disproofs, written to whatever
    /// file CSVM_SAFEGUARD_OUT names (the pattern ZzBaselineDump and ControlLimiterTests use).
    /// Asserts only that every airframe was flown.</summary>
    [ExtractedDataFact]
    public void DumpTheSafeguardMargins()
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        string? outPath = System.Environment.GetEnvironmentVariable("CSVM_SAFEGUARD_OUT");
        var sb = new StringBuilder();
        sb.AppendLine($"{"airframe",-19} {"fd mph",8} {"peak/fd",8} {"cap 1.75",9} {"margin",8} "
                      + $"{"settle m",9} {"over m",8} {"frame m",8}  fastest manoeuvre");
        int rows = 0;
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            var (peak, where) = FastestFlight(stats);
            var m = Climb(stats, 1900f, 22f, 240f, out float settleOver);
            Climb(stats, 1950f, 89f, 60f, out float steepOver);
            sb.AppendLine($"{plane,-19} {stats.FdSpeed / 0.44704f,8:0.0} {peak,8:0.000} {1.75f,9:0.00} "
                          + $"{1.75f - peak,8:0.000} {m.Position.Y,9:0.0} "
                          + $"{Mathf.Max(settleOver, steepOver),8:0.00} {1.75f * stats.FdSpeed * Dt,8:0.00}  {where}");
            rows++;
        }

        if (outPath != null)
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        Assert.Equal(AllPlanes.Length, rows);
    }

    // Every private const the plant types carry, as type-qualified name -> value. Only numeric
    // literals count: a string or bool const is not a plant quantity.
    private static Dictionary<string, double> LiveConstants()
    {
        var found = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var type in InventoryTypes)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var f in type.GetFields(Flags).Where(f => f.IsLiteral && !f.IsInitOnly))
            {
                object? raw = f.GetRawConstantValue();
                if (raw is float or double or int)
                    found[$"{type.Name}.{f.Name}"] = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            }
        }

        return found;
    }

    // Peak speed as a fraction of fd_speed over the manoeuvres that make the most of it: a vertical
    // and a shallow dive, and a held loop, which is the energy pump the cap exists for. Each is
    // entered at fd_speed, so the peak is the model's own terminal rather than the entry the
    // instrument chose (METHOD-21) — a dive entered above terminal only ever decelerates.
    private static (float Peak, string Where) FastestFlight(PlaneStats stats)
    {
        (string Name, float PitchDeg, float EntryFrac, float Stick, float StartM)[] runs =
        {
            ("vertical dive", -89f, 1f, 0f, 2000f),
            ("70.7° dive", -70.7f, 1f, 0f, 2000f),
            ("held loop", 0f, 1f, 1f, 500f),
        };
        float peak = 0f;
        string where = "-";
        foreach (var (name, pitchDeg, entryFrac, stick, startM) in runs)
        {
            var m = new FlightModel(stats);
            var attitude = Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(pitchDeg));
            m.Reset(new Vector3(0f, startM, 0f), attitude, stats.FdSpeed * entryFrac, 1f);
            var input = new FlightInput { Pitch = stick, Throttle = 1f };
            for (int i = 0; i < 7200; i++)
            {
                m.Step(input, Dt);
                if (m.Speed > peak * stats.FdSpeed)
                {
                    peak = m.Speed / stats.FdSpeed;
                    where = name;
                }
            }
        }

        return (peak, where);
    }

    // A held-attitude climb from below the cap, reporting the greatest height reached above it.
    private static FlightModel Climb(PlaneStats stats, float startM, float pitchDeg, float seconds,
        out float overshoot)
    {
        var m = new FlightModel(stats);
        m.Reset(new Vector3(0f, startM, 0f), Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(pitchDeg)),
            stats.FdSpeed, 1f);
        overshoot = 0f;
        for (float t = 0f; t < seconds; t += Dt)
        {
            m.Step(new FlightInput { Throttle = 1f }, Dt);
            overshoot = Mathf.Max(overshoot, m.Position.Y - 2003f);
        }

        return m;
    }
}

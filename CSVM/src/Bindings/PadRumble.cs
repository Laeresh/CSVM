using System;
using Godot;

namespace CSVM.Bindings;

/// <summary>One force-feedback effect the original loads out of <c>crimsonff.ifr</c>, named for the
/// effect it stands in for. The bands are the original's own. Gun fire splits on the weapon's
/// CALIBER. A round taken and a contact split on damage, an ordnance launch on the TORPEDO and
/// REAR flags. See docs/org/input.md for which slot each effect loads into, and which routine
/// starts it under what condition.</summary>
public enum RumbleEvent
{
    /// <summary>Gun fire below 50 calibre (FireGun_small).</summary>
    GunFireSmall,

    /// <summary>Gun fire from 50 to 69 calibre (FireGun_medium).</summary>
    GunFireMedium,

    /// <summary>Gun fire at 70 calibre and above (FireGun_large).</summary>
    GunFireLarge,

    /// <summary>A forward ordnance launch (RocketFire_small).</summary>
    OrdnanceLaunch,

    /// <summary>A rearward ordnance launch (RocketFire_small_rear).</summary>
    RearLaunch,

    /// <summary>A torpedo launch (RocketFire_large).</summary>
    TorpedoLaunch,

    /// <summary>A gun round taken below 6 damage (GunHit).</summary>
    CannonHitLight,

    /// <summary>A gun round taken at 6 damage and above (GunHit).</summary>
    CannonHitHeavy,

    /// <summary>Any other round taken below 100 damage (RocketHit).</summary>
    OrdnanceHitLight,

    /// <summary>Any other round taken at 100 damage and above (RocketHit).</summary>
    OrdnanceHitHeavy,

    /// <summary>A contact below 50.5 damage (Collision_small).</summary>
    ContactLight,

    /// <summary>A contact at 50.5 damage and above (Collision_large).</summary>
    ContactHeavy,

    /// <summary>The nitro engaging (NitroStart).</summary>
    NitroStart,

    /// <summary>Flight beyond the rated maximum (ExcessiveSpeed).</summary>
    Overspeed,

    /// <summary>The pilot's own turret gunner firing (TurretFire).</summary>
    TurretFire,
}

/// <summary>Where a rumble goes. The one seam the unit tests replace, so the table and the routing
/// are checked without a pad or a running engine.</summary>
public interface IRumbleSink
{
    /// <summary>Run <paramref name="device"/>'s two motors at these magnitudes for this long.
    /// </summary>
    void Play(int device, float weak, float strong, float seconds);
}

/// <summary>One pad shape: how hard each motor runs and for how long. Magnitude only, since a pad
/// has two motors that cannot be aimed where the original's hardware took a bearing as well.
/// </summary>
public readonly record struct RumbleShape(float Weak, float Strong, float Seconds);

/// <summary>The pad half of the flight cues, one seat's rumble routed to the pads that seat's
/// bindings came from and never another pilot's. The shapes are the original's own
/// <c>crimsonff.ifr</c> effects, scaled to Godot's 0..1 motors. A periodic effect drives the weak
/// motor, a vector-force or pop effect the strong one. ⚠ Magnitude only: the original aims each
/// impact effect along the bearing to its cause, and a pad cannot aim, so the bearing is dropped.
/// ⚠ Nothing here shakes the camera, since the airframe wobble is <c>PlaneShake</c>'s and doubling
/// it here would read as two cues for one event.</summary>
public sealed class PadRumble
{
    /// <summary>Whether the pad rumbles at all, the Game Options toggle. Held off under
    /// <c>--det</c>, so no golden sweep or scripted run reaches the player's hardware.</summary>
    public static bool Enabled = true;

    /// <summary>Where every seat's rumble goes. The unit tests swap in a recording sink.</summary>
    public static IRumbleSink Sink = new JoyRumbleSink();

    // Shorter than the 0.2 s the original stops the overspeed loop after. A steady dive refreshes
    // the effect before its window lapses, so it does not stutter at the seam.
    private const float OverspeedRefresh = 0.15f;

    // One row per RumbleEvent, in declaration order. Weak is the periodic child's Magnitude over
    // 10000, Strong the vector-force or pop child's. Where the code writes a gain over either, that
    // gain wins. Seconds is the effect's own Duration. The three infinite effects (gun fire,
    // overspeed, turret) instead take the hold-over the original stops them after.
    private static readonly RumbleShape[] Table =
    {
        new(0.44f, 0f, 0.30f),
        new(0.56f, 0f, 0.30f),
        new(0.95f, 0f, 0.30f),
        new(0f, 0.75f, 0.25f),
        new(0f, 0.55f, 0.25f),
        new(0f, 0.94f, 0.50f),
        new(0f, 0.75f, 0.10f),
        new(0f, 1.00f, 0.10f),
        new(0.80f, 0.80f, 0.50f),
        new(1.00f, 1.00f, 0.50f),
        new(1.00f, 1.00f, 0.50f),
        new(1.00f, 1.00f, 1.00f),
        new(0f, 1.00f, 1.00f),
        new(0.45f, 0f, 0.20f),
        new(0.35f, 0f, 0.20f),
    };

    private readonly Func<int[]?> _seatDevices;
    private readonly ActiveDevice _device;
    private double _speedRefreshAt;

    /// <summary>A seat rumbling on the pads <paramref name="seatDevices"/> names. That list is
    /// re-asked on every event, since a seat's devices change when a splitscreen player joins.
    /// The <paramref name="device"/> argument is the seat's own device memory, the one its control
    /// prompts read.</summary>
    public PadRumble(Func<int[]?> seatDevices, ActiveDevice device)
    {
        _seatDevices = seatDevices ?? throw new ArgumentNullException(nameof(seatDevices));
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>The shape an event plays, the whole table in one call.</summary>
    public static RumbleShape Shape(RumbleEvent ev) => Table[(int)ev];

    /// <summary>Which gun-fire effect a calibre falls in, on the original's own two edges.</summary>
    public static RumbleEvent GunFire(float caliber) =>
        caliber < 50f ? RumbleEvent.GunFireSmall
        : caliber < 70f ? RumbleEvent.GunFireMedium
        : RumbleEvent.GunFireLarge;

    /// <summary>Which launch effect a weapon fires: a torpedo first, then a rearward mount.
    /// </summary>
    public static RumbleEvent Launch(bool torpedo, bool rear) =>
        torpedo ? RumbleEvent.TorpedoLaunch
        : rear ? RumbleEvent.RearLaunch
        : RumbleEvent.OrdnanceLaunch;

    /// <summary>Which gun-hit effect an incoming round's damage falls in.</summary>
    public static RumbleEvent CannonHit(float damage) =>
        damage < 6f ? RumbleEvent.CannonHitLight : RumbleEvent.CannonHitHeavy;

    /// <summary>Which hit effect any other incoming round's damage falls in.</summary>
    public static RumbleEvent OrdnanceHit(float damage) =>
        damage < 100f ? RumbleEvent.OrdnanceHitLight : RumbleEvent.OrdnanceHitHeavy;

    /// <summary>Which of the two contact effects a contact's damage falls in. The original takes
    /// the larger of the pair, armour or health.</summary>
    public static RumbleEvent Contact(float armorDamage, float healthDamage) =>
        Math.Max(armorDamage, healthDamage) < 50.5f ? RumbleEvent.ContactLight : RumbleEvent.ContactHeavy;

    /// <summary>Rumble this seat's pads for one event. ⚠ A pad in the seat's roster is not a pad in
    /// the player's hands. The seat speaks through the pad only once the pad has spoken, the same
    /// reading that moves its control prompts. Nothing happens with the toggle off or with pad
    /// input blocked. Nothing happens when the seat holds no pad, or while its last input came from
    /// the keyboard side.</summary>
    public void Play(RumbleEvent ev)
    {
        if (!Enabled || _device.Side != DeviceSide.Pad)
            return;
        var shape = Table[(int)ev];
        foreach (int pad in Pads.For(_seatDevices()))
        {
            Sink.Play(pad, shape.Weak, shape.Strong, shape.Seconds);
        }
    }

    /// <summary>The per-tick overspeed drive. The original's effect is an infinite loop it stops
    /// 0.2 s after the last tick past the gate. This restarts it before that window lapses, and
    /// lets it die on its own once the dive ends. <paramref name="now"/> is the sim clock.</summary>
    public void Overspeed(bool beyondRated, double now)
    {
        if (!beyondRated)
        {
            _speedRefreshAt = 0d;
            return;
        }

        if (now < _speedRefreshAt)
            return;
        _speedRefreshAt = now + OverspeedRefresh;
        Play(RumbleEvent.Overspeed);
    }
}

/// <summary>The Godot sink: Godot's own two-motor call. Godot owns the stop, so nothing here
/// keeps a timer.</summary>
public sealed class JoyRumbleSink : IRumbleSink
{
    /// <inheritdoc/>
    public void Play(int device, float weak, float strong, float seconds) =>
        Input.StartJoyVibration(device, weak, strong, seconds);
}

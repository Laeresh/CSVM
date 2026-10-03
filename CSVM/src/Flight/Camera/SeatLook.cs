using CSVM.Bindings;
using CSVM.Flight.Airframe;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Camera;

/// <summary>One flight seat's look controls, read once a frame into the head's own conventions.
/// They are the snap cluster, the mouse pan under the held free-look control, the look stick, the
/// centre key and the three mode selectors. The run's scripted twins (<c>--view=</c> digits and
/// <c>--look=</c>) stand behind the live controls. It also holds the chase view's own swing of the
/// look stick and the Auto Head Turn option. The camera never learns about pads, mice or key
/// layouts; they stop here. A caller passes <c>muted</c> while a network pause's sheet is up, so
/// the sheet's menu keys aim nothing.</summary>
public sealed class SeatLook
{
    private readonly PlayerActions _seat;
    private readonly PlayerActions _pad;
    private readonly SeatMouse _mouse;

    // The look stick as the CHASE swing reads it. It has its own filter because that swing has no
    // return of its own: a released stick eases home at the head's decoded rates. In free-look the
    // stick turns the head instead (HeadLook.PadRates), so this swing reads it as released.
    private readonly StickLookFilter _chase = new(HeadLook.AzimuthSmoothRate, HeadLook.ElevationSmoothRate);

    // Set by any frame another camera placed, so the chase comes back unswung rather than easing.
    private bool _chaseStale;

    /// <summary>Reads <paramref name="seat"/>, the whole seat's resolved actions, for the keys and
    /// selectors, <paramref name="pad"/>, the pad half alone, for the look stick, and
    /// <paramref name="mouse"/> for the pan.</summary>
    public SeatLook(PlayerActions seat, PlayerActions pad, SeatMouse mouse)
    {
        _seat = seat;
        _pad = pad;
        _mouse = mouse;
    }

    /// <summary>The numpad snap direction held for the whole run (<c>--view=</c>), digits 1 to 9
    /// with 5 unbound. It is the scripted twin of holding the key. 0, the default, leaves the head
    /// straight ahead. A key held at the controls wins while it is down. The head swings to the
    /// pinned direction rather than cutting to it, since it reaches the camera through one head.</summary>
    public int PinnedView { get; set; }

    /// <summary>The right-stick deflection held for the whole run (<c>--look=x,y</c>, +x right and
    /// +y up), the scripted twin of pushing the look stick. Zero, the default, is a centred stick,
    /// and a live stick wins while deflected. It feeds the chase swing and the first-person head
    /// through the one reader, so a run can compare them.</summary>
    public Vector2 PinnedLook { get; set; }

    /// <summary>The Auto Head Turn option, the original's GAME OPTIONS checkbox: true turns the head
    /// with the aircraft in the cockpit. Seeded at build and read every frame, so a page accepted
    /// over the pause takes hold at once. ⚠ Null, the default, is "never set". It leaves the
    /// <c>headLook.autohead</c> config key deciding, which ships OFF.</summary>
    public bool? AutoHeadTurn { get; set; }

    /// <summary>Whether the free-look control is down, this port's reading of <c>DAT_00654120</c>.
    /// It decides whether the mouse aims the head or the stick. ⚠ Keep it a hold under both mouse
    /// schemes; a toggle would leave the mouse on the head for the rest of the sortie.</summary>
    public bool FreeLookHeld => _seat.Held(InputAction.FreeLook);

    /// <summary>One frame of head-look input. The chase view sets <paramref name="chase"/>: its
    /// stick reaches the head only as free-look's rate, since outside free-look it swings that view
    /// itself (<see cref="StepChase"/>). Muted, it reads as no input at all and the mouse's pan is
    /// not consumed.</summary>
    public HeadLookInput Read(bool muted, bool chase = false)
    {
        if (muted)
            return default;
        var (snapX, snapY) = SnapDirection();
        var pan = _mouse.LookTravel(FreeLookHeld);
        var (lookX, lookY) = Stick(muted);
        // lookY is the stick's +down and HeadLook wants +up. A chase head looking left carries the
        // camera right, so x is mirrored there and stick right puts the view right in both modes.
        return new HeadLookInput(snapX, snapY, pan.X, -pan.Y, _seat.Held(InputAction.LookCenter),
            chase ? -lookX : lookX, -lookY, FreeLookHeld,
            _seat.Held(InputAction.SnapLookMode), _seat.Held(InputAction.SmoothLookMode),
            _seat.Held(InputAction.TrackTarget), PadRatesOnly: chase);
    }

    /// <summary>One frame of the chase view's absolute swing: the look stick through its own
    /// filter, eased home once let go. While <paramref name="aims"/> is false, in free-look where
    /// the stick turns the head instead, it reads as let go. It starts from centre after any frame
    /// another view placed (<see cref="CutAway"/>). Returns the swing and whether it is off
    /// centre.</summary>
    public (float X, float Y, bool Swinging) StepChase(float dt, bool muted, bool aims)
    {
        var (x, y) = aims ? Stick(muted) : (0f, 0f);
        if (_chaseStale)
        {
            _chase.Reset();
            _chaseStale = false;
        }

        _chase.Step(dt, x, y);
        return (_chase.X, _chase.Y, _chase.Swinging);
    }

    /// <summary>Another view placed this frame, so the chase swing comes back unswung rather than
    /// easing from where it was.</summary>
    public void CutAway() => _chaseStale = true;

    /// <summary>The autohead aim for a frame with no look input. Null where the option is off, or
    /// where the selected view is not Cockpit (the original's option byte AND mode not 7). The body
    /// rates are already in the plane's own frame, which the original rotates its world rates
    /// into.</summary>
    public (float Elevation, float Azimuth)? Autohead(PilotViewMode selected, FlightModel model)
    {
        if (selected != PilotViewMode.Cockpit || !(AutoHeadTurn ?? Config.GetBool("headLook.autohead", false)))
            return null;
        return HeadLook.AutoheadTarget(model.BodyRates, model.Stats.AutoheadTurnTime,
            model.Stats.AutoheadTurnMax, model.Stats.AutoheadTurnMinPitch);
    }

    // The look stick, curved as every pad axis is. Both components read exactly 0 inside the
    // deadzone, which tells a reader the look-around is inactive. ONE reader for both views, so the
    // chase swing and the first-person head cannot take different sticks.
    private (float X, float Y) Stick(bool muted)
    {
        if (muted)
            return (0f, 0f);
        float x = AnalogAxes.PadCurve(_pad.Axis(InputAction.LookAimRight, InputAction.LookAimLeft));
        float y = AnalogAxes.PadCurve(_pad.Axis(InputAction.LookAimDown, InputAction.LookAimUp));
        // A live stick beats the scripted pin. PinnedLook's Y is +up and this pair's is the
        // stick's own +down, so it is negated back here and every caller reads one convention.
        return x != 0f || y != 0f ? (x, y) : (PinnedLook.X, -PinnedLook.Y);
    }

    // The snap cluster as a composed direction, the original's own Views 2 rows. Kp8 is Look Up,
    // Kp4 and Kp6 the flanks, Kp2 Look Back, and the corners the four diagonals.
    private (float X, float Y) SnapDirection()
    {
        var (x, y) = SnapLookRows.Compose(_seat.Value);
        if (x != 0f || y != 0f || PinnedView < 1 || PinnedView > 9)
            return (x, y);
        // A pinned digit's column is the left/right component and its row the up/down one. Live
        // keys beat the pin, the rule the stick follows against PinnedLook.
        return (((PinnedView - 1) % 3) - 1, ((PinnedView - 1) / 3) - 1);
    }
}

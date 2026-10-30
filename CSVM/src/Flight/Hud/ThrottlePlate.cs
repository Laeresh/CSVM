using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The commanded throttle as a 1930s lever plate, inboard of the speedometer. A remake-only
/// readout: the original draws no throttle on its HUD. Plate and knob are SVGs (CSVM/data/hud,
/// written by render_throttle_plate.py), rasterised at the size the pane draws them, so they stay
/// sharp at any resolution. The knob shows the lever the pilot sets, not the live one the engine
/// slews towards it, so a digit press moves it at once.
/// </summary>
public sealed partial class ThrottlePlate : Control
{
    // The plate's own geometry in SVG units, as render_throttle_plate.py draws it.
    internal const float SvgWidth = 242f;
    internal const float SvgHeight = 389f;
    internal const float SvgPad = 6f;     // the shadow margin round the plate
    internal const float SlotX = 66f;     // the slot's centre line
    internal const float MaxY = 84f;      // the knob centre at full lever, level with MAX
    internal const float IdleY = 319f;    // and at idle, level with IDLE

    // Placement in the HUD's 1440p reference. The plate is as tall as the speedometer, centred level
    // with it, its right edge 25 px inboard of that dial's bezel.
    private const float HeightRef = 2f * GaugeCluster.SpdRadius;
    private const float GapRef = 25f;

    private const string PlatePath = "res://data/hud/throttle_plate.svg.bin";
    private const string KnobPath = "res://data/hud/throttle_knob.svg.bin";

    private readonly string _plateSvg;
    private readonly string _knobSvg;
    private ImageTexture? _plate;
    private ImageTexture? _knob;
    private float _rasterUnit = -1f;      // px per SVG unit the two textures hold
    private float _drawnLever = -1f;

    private ThrottlePlate(string plateSvg, string knobSvg)
    {
        _plateSvg = plateSvg;
        _knobSvg = knobSvg;
    }

    /// <summary>The commanded lever, 0 idle to 1 full, fed by <see cref="FlightHud.Draw"/>.</summary>
    public float Lever { get; set; }

    /// <summary>The plate, or null when either SVG is missing from the build.</summary>
    public static ThrottlePlate? Build()
    {
        if (!FileAccess.FileExists(PlatePath) || !FileAccess.FileExists(KnobPath))
        {
            Log.Warn("flight", $"throttle plate: {PlatePath} or {KnobPath} missing, no throttle readout");
            return null;
        }

        var plate = new ThrottlePlate(FileAccess.GetFileAsString(PlatePath), FileAccess.GetFileAsString(KnobPath))
        {
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Visible = false, // until the Game Options box shows it (FlightHud.SetThrottlePlateShown)
        };
        plate.SetAnchorsPreset(LayoutPreset.FullRect);
        return plate;
    }

    /// <summary>The knob centre's height on the plate, in SVG units from its top. Public for
    /// CSVM.Tests: idle sits on IDLE, full on MAX, and out-of-range levers clamp to the ends.</summary>
    public static float KnobY(float lever) => IdleY + ((MaxY - IdleY) * Mathf.Clamp(lever, 0f, 1f));

    /// <summary>The plate's top-left corner on screen and its scale in px per SVG unit, for a
    /// pane's reading box and HUD scale.</summary>
    public static (Vector2 Origin, float Unit) Placement(Rect2 box, float scale)
    {
        float unit = HeightRef * scale / SvgHeight;
        float right = GaugeCluster.ColumnAnchors(box, scale).Right
            - ((GaugeCluster.SpdCenterFromRight + GaugeCluster.SpdRadius + GapRef) * scale);
        float centreY = GaugeCluster.FromBottom(GaugeCluster.SpdCenterY, scale, box.End.Y);
        return (new Vector2(right - (SvgWidth * unit), centreY - (SvgHeight * unit / 2f)), unit);
    }

    public override void _Process(double delta)
    {
        if (!Mathf.IsEqualApprox(Lever, _drawnLever))
            QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _Draw()
    {
        var (origin, unit) = Placement(HudMetrics.ReadingBox(this), HudMetrics.Scale(this));
        if (unit <= 0f)
            return;
        Rasterise(unit);
        if (_plate == null || _knob == null)
            return;

        DrawTexture(_plate, origin - (new Vector2(SvgPad, SvgPad) * unit));
        var knobCentre = origin + (new Vector2(SlotX, KnobY(Lever)) * unit);
        DrawTexture(_knob, knobCentre - (_knob.GetSize() / 2f));
        _drawnLever = Lever;
    }

    private static ImageTexture? Raster(string svg, float unit)
    {
        var image = new Image();
        if (image.LoadSvgFromString(svg, unit) != Error.Ok)
        {
            Log.Warn("flight", $"throttle plate: an SVG did not rasterise at {unit:0.000} px per unit");
            return null;
        }
        return TextureUpload.Create(image);
    }

    // Re-rasterises both SVGs when the drawn size moves, which is what keeps them sharp.
    private void Rasterise(float unit)
    {
        if (Mathf.Abs(unit - _rasterUnit) < 0.001f)
            return;
        _rasterUnit = unit;
        _plate = Raster(_plateSvg, unit);
        _knob = Raster(_knobSvg, unit);
    }
}

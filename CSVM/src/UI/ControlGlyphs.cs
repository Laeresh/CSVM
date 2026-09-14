using System.Globalization;
using CSVM.Bindings;
using Godot;

namespace CSVM.UI;

/// <summary>One control as a picture set addresses it, keyed the way <see cref="BindingControl"/>
/// is: the kind, the index inside that kind, and the sign an axis binding names. Deadzone and
/// modifiers are left out because they decide when a control fires, not what it looks like.</summary>
public readonly record struct GlyphKey(ControlKind Kind, int Index, int Sign)
{
    /// <summary>The key one binding's control sits on.</summary>
    public static GlyphKey Of(BindingControl control) => new(control.Kind, control.Index, control.Sign);
}

/// <summary>
/// The glyph set in force, and the one question a composing site asks it: does this control draw as
/// a picture. One holder rather than a parameter on every prompt, so swapping the whole set is one
/// assignment and no composing site has to be told about it.
/// </summary>
public static class ControlGlyphs
{
    private static ControlGlyphSet _set = new PadVectorGlyphs();

    /// <summary>The set every control line draws through. Assigning a different one changes every
    /// prompt and board hint at once; null puts the shipped set back.</summary>
    public static ControlGlyphSet Set
    {
        get => _set;
        set => _set = value ?? new PadVectorGlyphs();
    }

    /// <summary>The glyph that stands for <paramref name="control"/>, or null where the set draws
    /// none and the line keeps its words.</summary>
    public static GlyphKey? For(BindingControl control)
    {
        var key = GlyphKey.Of(control);
        return _set.Draws(key) ? key : null;
    }
}

/// <summary>
/// A per-control picture set: which controls it draws, how wide one is at a line's height, and how
/// to draw one. A set that declines a control leaves the line its words, which is how a keyboard
/// seat reads its own keys. Swappable whole through <see cref="ControlGlyphs.Set"/>, because the
/// original ships no such art to copy and how these should look is a judgement at the controls.
/// </summary>
public abstract class ControlGlyphSet
{
    /// <summary>Whether this set has a picture for that control.</summary>
    public abstract bool Draws(GlyphKey key);

    /// <summary>How wide the picture is when it is <paramref name="height"/> tall.</summary>
    public abstract float Width(Font font, GlyphKey key, float height);

    /// <summary>Draws the control inside <paramref name="box"/>, in <paramref name="color"/>.
    /// </summary>
    public abstract void Draw(CanvasItem into, Font font, GlyphKey key, Rect2 box, Color color);
}

/// <summary>
/// The shipped set: pad controls as plain vector shapes built in code, a ring for a face button, a
/// cross with one arm filled for a d-pad direction, a ring with a deflection arrow for a stick and
/// a bordered plaque for everything that reads as letters. It declines keys, mouse buttons and
/// hats, so those seats keep the words <see cref="BindingLabels.Describe"/> gives them.
/// </summary>
public sealed class PadVectorGlyphs : ControlGlyphSet
{
    // Shape metrics as fractions of the glyph's height, so one set scales with the line it sits on.
    // All TUNE: nothing in the original fixes them.
    private const float RingRadius = 0.40f;
    private const float Stroke = 0.09f;
    private const float LetterHeight = 0.52f;
    private const float ArrowHalf = 0.16f;
    private const float PlaquePad = 0.30f;
    private const int RingSegments = 24;

    /// <inheritdoc/>
    public override bool Draws(GlyphKey key) => key.Kind is ControlKind.Button or ControlKind.Axis;

    /// <inheritdoc/>
    public override float Width(Font font, GlyphKey key, float height)
    {
        if (Label(key) is not { } label)
        {
            return height;
        }

        float text = font?.GetStringSize(label, HorizontalAlignment.Left, -1f, LetterSize(height)).X ?? 0f;
        return Mathf.Max(height, text + (height * PlaquePad * 2f));
    }

    /// <inheritdoc/>
    public override void Draw(CanvasItem into, Font font, GlyphKey key, Rect2 box, Color color)
    {
        if (into == null)
        {
            return;
        }

        if (key.Kind == ControlKind.Axis)
        {
            Stick(into, font, key, box, color);
            return;
        }

        switch ((JoyButton)key.Index)
        {
            case JoyButton.A:
            case JoyButton.B:
            case JoyButton.X:
            case JoyButton.Y:
                Face(into, font, FaceLetter(key.Index), box, color);
                break;
            case JoyButton.DpadUp:
            case JoyButton.DpadDown:
            case JoyButton.DpadLeft:
            case JoyButton.DpadRight:
                Dpad(into, (JoyButton)key.Index, box, color);
                break;
            case JoyButton.LeftStick:
            case JoyButton.RightStick:
                Face(into, font, key.Index == (int)JoyButton.LeftStick ? "L" : "R", box, color, doubled: true);
                break;
            default:
                Plaque(into, font, Label(key) ?? "?", box, color);
                break;
        }
    }

    // The face buttons carry their own letters, which is what the player is looking at.
    private static string FaceLetter(int index) => (JoyButton)index switch
    {
        JoyButton.A => "A",
        JoyButton.B => "B",
        JoyButton.X => "X",
        _ => "Y",
    };

    // The controls this set writes as letters on a plaque rather than as a shape, and null for the
    // ones it draws. A pad button no name is known for falls back to its index, so an unusual
    // controller still reads as something a player can look for.
    private static string? Label(GlyphKey key)
    {
        if (key.Kind == ControlKind.Axis)
        {
            return key.Index is (int)JoyAxis.TriggerLeft ? "LT"
                : key.Index is (int)JoyAxis.TriggerRight ? "RT" : null;
        }

        return (JoyButton)key.Index switch
        {
            JoyButton.A or JoyButton.B or JoyButton.X or JoyButton.Y => null,
            JoyButton.DpadUp or JoyButton.DpadDown or JoyButton.DpadLeft or JoyButton.DpadRight => null,
            JoyButton.LeftStick or JoyButton.RightStick => null,
            JoyButton.LeftShoulder => "LB",
            JoyButton.RightShoulder => "RB",
            JoyButton.Back => "BACK",
            JoyButton.Start => "START",
            JoyButton.Guide => "GUIDE",
            _ => "#" + key.Index.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static int LetterSize(float height) => Mathf.Max(1, Mathf.RoundToInt(height * LetterHeight));

    // A letter centred on the box's own middle, which is where every shape here puts its text.
    private static void Letter(CanvasItem into, Font font, string text, Rect2 box, Color color)
    {
        if (font == null)
        {
            return;
        }

        int size = LetterSize(box.Size.Y);
        var span = font.GetStringSize(text, HorizontalAlignment.Left, -1f, size);
        var at = box.Position + new Vector2(
            (box.Size.X - span.X) / 2f,
            ((box.Size.Y - span.Y) / 2f) + font.GetAscent(size));
        into.DrawString(font, at, text, HorizontalAlignment.Left, -1f, size, color);
    }

    // A face button: a ring with its own letter inside. The doubled stroke is the stick click, a
    // ring pressed rather than a button pressed, and the only difference between the two shapes.
    private static void Face(CanvasItem into, Font font, string letter, Rect2 box, Color color,
        bool doubled = false)
    {
        float h = box.Size.Y;
        var centre = box.Position + (box.Size / 2f);
        into.DrawArc(centre, h * RingRadius, 0f, Mathf.Tau, RingSegments, color,
            h * Stroke * (doubled ? 2f : 1f));
        Letter(into, font, letter, box, color);
    }

    // A d-pad direction: the whole cross in outline with the named arm filled, so the shape says
    // which way and the fill says which arm.
    private static void Dpad(CanvasItem into, JoyButton button, Rect2 box, Color color)
    {
        float h = box.Size.Y;
        var centre = box.Position + (box.Size / 2f);
        float arm = h * RingRadius;
        float half = h * 0.13f;
        into.DrawRect(new Rect2(centre.X - half, centre.Y - arm, half * 2f, arm * 2f), color, false, h * Stroke);
        into.DrawRect(new Rect2(centre.X - arm, centre.Y - half, arm * 2f, half * 2f), color, false, h * Stroke);
        var filled = button switch
        {
            JoyButton.DpadUp => new Rect2(centre.X - half, centre.Y - arm, half * 2f, arm - half),
            JoyButton.DpadDown => new Rect2(centre.X - half, centre.Y + half, half * 2f, arm - half),
            JoyButton.DpadLeft => new Rect2(centre.X - arm, centre.Y - half, arm - half, half * 2f),
            _ => new Rect2(centre.X + half, centre.Y - half, arm - half, half * 2f),
        };
        into.DrawRect(filled, color);
    }

    // A stick direction: the stick's ring with its own letter, and a filled arrow just outside the
    // ring on the side the binding's sign names. A trigger is a plaque instead, having no travel to
    // point at.
    private static void Stick(CanvasItem into, Font font, GlyphKey key, Rect2 box, Color color)
    {
        if (Label(key) is { } trigger)
        {
            Plaque(into, font, trigger, box, color);
            return;
        }

        bool right = key.Index is (int)JoyAxis.RightX or (int)JoyAxis.RightY;
        Face(into, font, right ? "R" : "L", box, color);
        bool vertical = key.Index is (int)JoyAxis.LeftY or (int)JoyAxis.RightY;
        float h = box.Size.Y;
        var centre = box.Position + (box.Size / 2f);
        var along = vertical ? new Vector2(0f, key.Sign) : new Vector2(key.Sign, 0f);
        var across = new Vector2(-along.Y, along.X);
        var tip = centre + (along * h * 0.5f);
        var baseAt = centre + (along * h * (RingRadius + (Stroke * 1.5f)));
        into.DrawColoredPolygon(
            new[] { tip, baseAt + (across * h * ArrowHalf), baseAt - (across * h * ArrowHalf) }, color);
    }

    // Everything that reads as letters: a bordered plaque sized to its own label, which is how a
    // shoulder, a trigger and the two menu buttons appear on a pad's own artwork.
    private static void Plaque(CanvasItem into, Font font, string label, Rect2 box, Color color)
    {
        float h = box.Size.Y;
        var frame = new Rect2(box.Position + new Vector2(0f, h * 0.12f), box.Size.X, h * 0.76f);
        into.DrawRect(frame, color, false, h * Stroke);
        Letter(into, font, label, box, color);
    }
}


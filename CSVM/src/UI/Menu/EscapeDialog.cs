using System;
using System.Collections.Generic;
using CSVM.Mech3;

namespace CSVM.UI.Menu;

/// <summary>A rectangle of whole authored pixels, which is how a <c>MAP</c> primitive's
/// <c>CLIP</c> and <c>WORLD</c> corners are stored: the original truncates each authored float
/// toward zero on the way in, so a bound of <c>-1571.47</c> is <c>-1571</c>
/// (docs/org/pause-screen.md).</summary>
public readonly record struct EscapeRect(int X0, int Y0, int X1, int Y1)
{
    /// <summary>The rectangle's signed width, which a world window authors negative.</summary>
    public int Width => X1 - X0;

    /// <summary>The rectangle's signed height, which a world window authors negative.</summary>
    public int Height => Y1 - Y0;
}

/// <summary>
/// One dialog's <c>MAP</c> primitive: which chart sheet it draws, where the sheet's cropped region
/// lands, and the world window that region stands for.
///
/// <para>⚠ <see cref="Clip"/> is a rectangle in the bitmap, not on the screen. The crop's top left
/// lands on <see cref="Position"/>, so the map occupies <see cref="Position"/> to
/// <see cref="Position"/> plus the crop's size, and that is the rectangle
/// <see cref="TryProject"/> maps the world window onto.</para>
/// </summary>
public sealed record EscapeMap(string Bitmap, BriefingPoint Position, EscapeRect Clip, EscapeRect World)
{
    /// <summary>The map's left edge on the authored screen.</summary>
    public float ScreenX0 => Position.X;

    /// <summary>The map's top edge on the authored screen.</summary>
    public float ScreenY0 => Position.Y;

    /// <summary>The map's right edge on the authored screen.</summary>
    public float ScreenX1 => Position.X + Clip.Width;

    /// <summary>The map's bottom edge on the authored screen.</summary>
    public float ScreenY1 => Position.Y + Clip.Height;

    /// <summary>Projects a world position onto the chart and answers whether it lands on it. The
    /// chart is a plan view of negated world Z against world X, so altitude moves nothing, and a
    /// position outside the window draws no icon at all rather than one clamped to an edge.</summary>
    public bool TryProject(float worldX, float worldZ, out BriefingPoint at)
    {
        at = default;
        if (World.Width == 0 || World.Height == 0)
        {
            return false;
        }

        float u = (worldX - World.X0) / World.Width;
        float v = (-worldZ - World.Y0) / World.Height;

        // Truncated toward zero, the way the original's own ftol lands a projection on a pixel.
        at = new BriefingPoint(
            (int)(ScreenX0 + ((ScreenX1 - ScreenX0) * u)),
            (int)(ScreenY0 + ((ScreenY1 - ScreenY0) * v)));
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }
}

/// <summary>The shared objectives parchment: its backing bitmap, its title, the box its rows flow
/// in and the mark a completed row takes. Shared with the loading dialog, which is why it sits in
/// the definition file's own <c>LOADINGDIALOG</c> block rather than in a mission's dialog.
/// Decode: docs/formats/objectives.md.</summary>
public sealed record EscapeObjectivesList(
    string Background,
    BriefingPoint BackgroundAt,
    string TitleKey,
    BriefingPoint TitleAt,
    BriefingPoint ListAt,
    float WrapWidth,
    float WrapHeight,
    float Spacing,
    string CheckMark)
{
    /// <summary>The title's face size in authored pixels: <c>ObjListTitle</c>, which
    /// <c>fonts.zrd</c> gives at 17. Held here rather than on either screen so the two that draw
    /// this one shared widget cannot come to write it in two sizes.</summary>
    public const float TitleFont = 17f;

    /// <summary>A row's face size, <c>ObjList</c>, which <c>fonts.zrd</c> gives as Andy Bold at
    /// 14.</summary>
    public const float RowFont = 14f;

    /// <summary>The height the stacked rows stop at: none. The authored <c>WORDWRAP</c> height is
    /// the box one row wraps in, not the list's, and the executable's list walks its whole row
    /// vector, so a list taller than the parchment runs on rather than losing its last rows.
    /// Decode: docs/org/pause-screen.md.</summary>
    public const float RowStop = 0f;

    // How much wider than the authored WORDWRAP a row is allowed to run, in authored pixels.
    private const float RowWrapGain = 20f;

    /// <summary>The measure a row wraps in: the authored <c>WORDWRAP</c> of 190 widened to the room
    /// the 240-wide parchment still holds. Andy Bold is narrower per character than the face the
    /// extraction leaves us, so rows the original breaks once break twice inside the authored box,
    /// and the wider measure buys those characters back without touching the face size.
    /// Decode: docs/org/pause-screen.md.</summary>
    public float RowWrap => WrapWidth + RowWrapGain;
}

/// <summary>One button strip: the three bitmaps its three states draw and the label centred over
/// them at the strip's own offset. There is no disabled frame, so a strip with nothing behind it is
/// still drawn live.</summary>
public sealed record EscapeButton(
    string Key,
    BriefingPoint At,
    string Normal,
    string Rollover,
    string Activate,
    string LabelKey,
    BriefingPoint LabelOffset);

/// <summary>The dialog's own pointer: the bitmap it wears, the bitmap it wears while it stands on
/// a live widget, and whether its middle rather than its corner lands on the pointer's point. Every
/// campaign dialog authors the same pair, which is the evidence that the screen is pointed at.
/// Decode: docs/org/pause-screen.md.</summary>
public sealed record EscapeCursor(string Bitmap, string Rollover, bool Centered);

/// <summary>The definition file's <c>LOADINGDIALOG</c> block: what every dialog in the file shares.
/// The two icons carry no position because the screen gives them one from a world position through
/// the mission dialog's own map window.</summary>
public sealed record EscapeShared(
    EscapeObjectivesList? Objectives,
    string OwnShip,
    string MyZep,
    IReadOnlyList<EscapeButton> Buttons)
{
    /// <summary>The strip a widget name asks for, or null when the file authors none.</summary>
    public EscapeButton? Button(string key)
    {
        foreach (var button in Buttons)
        {
            if (string.Equals(button.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }
}

/// <summary>
/// One pause dialog: the frame it hangs on, its mission's chart sheet, the memento slot and the
/// beat sheet that turns the parchment, the pins and the icons on. A campaign dialog carries all of
/// them; an Instant Action dialog carries no <c>PRIMITIVES</c> block at all and is the blackboard
/// instead.
/// </summary>
public sealed record EscapeState(
    string Key,
    string Background,
    EscapeMap? Map,
    string MementoBitmap,
    BriefingPoint MementoAt,
    EscapeCursor? Cursor,
    IReadOnlyList<BriefingStep> Steps);

/// <summary>
/// The definition file behind the pause screen and the campaign load screen: <c>escape.zrd</c> for
/// a campaign pause, <c>ia_escape.zrd</c> for an Instant Action one and <c>Loading.zrd</c> for the
/// load screen, all carrying dialogs keyed the same way. The shared <c>LOADINGDIALOG</c> block
/// holds the objectives parchment, the two world-placed icons and the button strips; a dialog holds
/// its own map, memento and beat sheet. Decode: docs/org/pause-screen.md and
/// docs/org/loading-screen.md.
/// </summary>
public sealed class EscapeDialog
{
    /// <summary>The campaign definition file, which a multiplayer session also reads.</summary>
    public const string CampaignFile = "escape.json";

    /// <summary>The Instant Action definition file, which ships and so is always preferred for an
    /// Instant Action session.</summary>
    public const string InstantActionFile = "ia_escape.json";

    /// <summary>The load screen's own definition file. ⚠ Its campaign dialogs are not a copy of
    /// <see cref="CampaignFile"/>'s: the <c>PRIMITIVES</c> agree block for block, but every
    /// <c>LOADING_SCRIPT</c> is a superset of the matching <c>ESC_SCRIPT</c>, adding the propeller
    /// cycle and the mission's device icons, so reading one screen's content out of the other
    /// file's dialog understates it.</summary>
    public const string LoadingFile = "Loading.json";

    // How far a beat sheet is run out, and in what step. Long enough for every authored wait and
    // tween in the shipped dialogs, which is a tenth of a second and a few seconds respectively.
    private const double SettleSeconds = 1.0;
    private const int SettleSteps = 64;

    private readonly Dictionary<string, EscapeState> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every dialog in the file, by key.</summary>
    public IReadOnlyDictionary<string, EscapeState> States => _states;

    /// <summary>What every dialog in the file shares, or null when the file carries no
    /// <c>LOADINGDIALOG</c> block.</summary>
    public EscapeShared? Shared { get; private set; }

    /// <summary>The dialog key for a campaign mission's storage address. ⚠ The digits are the ZBD
    /// world folder and its <c>M0n</c> number, so <c>CM01</c> (<c>C3/M01</c>) is
    /// <c>loading_c61</c>; docs/formats/campaign-missions.md is the lookup.</summary>
    public static string CampaignKey(int campaign, int mission) => $"loading_c{campaign}{mission}";

    /// <summary>The dialog key an Instant Action or multiplayer session resolves. The original's
    /// builder has no multiplayer branch, so both take this form.</summary>
    public static string InstantActionKey(int environment, char letter) =>
        $"loading_i{environment}{letter}";

    /// <summary>Loads a definition file from a shared zrdr scope. Every dialog is kept, including
    /// <c>default</c>, since a key that misses falls back to it the way the original's lookup
    /// does.</summary>
    public static EscapeDialog Load(string zrdrPath, string file)
    {
        var dialog = new EscapeDialog();
        var root = Zrdr.LoadFile(zrdrPath, file);
        if (root.Count == 0 || root[0] is not List<object?> entries)
        {
            return dialog;
        }

        // The top level pairs a key with a list but tolerates a bare flag between two of them, so
        // step by what is there rather than by two.
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] is not string key || i + 1 >= entries.Count
                || entries[i + 1] is not List<object?> body)
            {
                continue;
            }

            i++;
            if (string.Equals(key, "LOADINGDIALOG", StringComparison.OrdinalIgnoreCase))
            {
                dialog.Shared = ParseShared(body);
                continue;
            }

            if (key.StartsWith("loading_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "default", StringComparison.OrdinalIgnoreCase))
            {
                dialog._states[key] = ParseState(key, body);
            }
        }

        return dialog;
    }

    /// <summary>A dialog's beat sheet run out to the still both screens draw, rather than played:
    /// a wait releases and a spin lands at its end revolutions. The reveal is given no narration
    /// cue points, which settles it at once, since neither script carries a sound or a marker to
    /// wait on.</summary>
    public static BriefingReveal Settled(IReadOnlyList<BriefingStep> steps)
    {
        // ⚠ Do not build the reveal and leave it. An authored Wait blocks every beat after it, and
        // 19 of the 24 loading dialogs (7 of the pausing ones) place their pins past one, so those
        // charts would draw with no flags at all. One Advance releases one wait, hence the loop.
        var reveal = new BriefingReveal(steps, Array.Empty<double>());
        for (int i = 0; i < SettleSteps && !reveal.Complete; i++)
        {
            reveal.Advance(SettleSeconds);
        }

        return reveal;
    }

    /// <summary>One widget label resolved through the message table. A key the table does not
    /// carry comes back as itself, which is worse than nothing on a strip or over a parchment, so
    /// it yields the empty string instead.</summary>
    public static string Label(Messages messages, string key)
    {
        if (key.Length == 0)
        {
            return string.Empty;
        }

        string text = messages.Get(key);
        return text.StartsWith("MSG_", StringComparison.Ordinal) ? string.Empty : text;
    }

    /// <summary>The dialog a key names, falling back to <c>default</c> the way the original's own
    /// by-name lookup does, and null when the file carries neither.</summary>
    public EscapeState? Find(string key)
    {
        if (_states.TryGetValue(key, out var state))
        {
            return state;
        }

        return _states.TryGetValue("default", out var fallback) ? fallback : null;
    }

    private static EscapeShared ParseShared(List<object?> body)
    {
        var d = ZrdrDict.FromAlternating(body);
        var primitives = d.Dict("PRIMITIVES");
        return new EscapeShared(
            primitives?.Dict("OBJECTIVESLIST") is { } list ? ParseObjectives(list) : null,
            primitives?.Dict("OWNSHIP")?.Str("BITMAP") ?? string.Empty,
            primitives?.Dict("MYZEP")?.Str("BITMAP") ?? string.Empty,
            ParseButtons(d.List("BUTTONS")));
    }

    private static EscapeObjectivesList ParseObjectives(ZrdrDict list)
    {
        var background = list.Dict("BACKGROUND");
        var title = list.Dict("TITLE");
        var rows = list.Dict("LIST");
        return new EscapeObjectivesList(
            background?.Str("BITMAP") ?? string.Empty,
            Point(background, "POSITION"),
            title?.Str("TEXT") ?? string.Empty,
            Point(title, "POSITION"),
            Point(rows, "POSITION"),
            rows?.Float("WORDWRAP") ?? 0f,
            rows?.Float("WORDWRAP", 0f, 1) ?? 0f,
            rows?.Float("SPACING") ?? 0f,
            list.Dict("CHECKMARK")?.Str("BITMAP") ?? string.Empty);
    }

    // The block pairs a widget name with its own alternating body, so walk it by pairs rather than
    // through a dict: the order the file authors is the order the strips are drawn in.
    private static IReadOnlyList<EscapeButton> ParseButtons(List<object?>? block)
    {
        var buttons = new List<EscapeButton>();
        if (block == null)
        {
            return buttons;
        }

        for (int i = 0; i + 1 < block.Count; i++)
        {
            if (block[i] is not string key || block[i + 1] is not List<object?> body)
            {
                continue;
            }

            i++;
            var d = ZrdrDict.FromAlternating(body);
            string normal = d.Str("BITMAP") ?? string.Empty;
            var label = d.Dict("LABEL");
            buttons.Add(new EscapeButton(
                key,
                Point(d, "POSITION"),
                normal,
                d.Dict("ROLLOVER")?.Str("BITMAP") ?? normal,
                d.Dict("ACTIVATE")?.Str("BITMAP") ?? normal,
                LabelKey(d.List("LABEL")),
                Point(label, "offset")));
        }

        return buttons;
    }

    // A label's value list opens with its bare string key and then alternates, so the key is the
    // first entry rather than a named field.
    private static string LabelKey(List<object?>? label) =>
        label is { Count: > 0 } && label[0] is string key ? key : string.Empty;

    private static EscapeState ParseState(string key, List<object?> body)
    {
        var d = ZrdrDict.FromAlternating(body);
        string background = d.List("BACKGROUND_IMAGES") is { Count: > 0 } images
            && images[0] is List<object?> { Count: > 0 } first && first[0] is string bitmap
            ? bitmap
            : string.Empty;

        var primitives = d.Dict("PRIMITIVES");
        var memento = primitives?.Dict("MEMENTO");
        var script = d.List("ESC_SCRIPT") ?? d.List("LOADING_SCRIPT") ?? d.List("SCRIPT");
        return new EscapeState(
            key,
            background,
            primitives?.Dict("MAP") is { } map ? ParseMap(map) : null,
            memento?.Str("BITMAP") ?? string.Empty,
            Point(memento, "POSITION"),
            ParseCursor(d.Dict("CURSOR")),
            script == null
                ? Array.Empty<BriefingStep>()
                : BriefingDialog.ParseScript(script));
    }

    // A cursor with no bitmap is no cursor at all, so the screen keeps whatever pointer it was
    // shown with; a block that names no rollover wears its one bitmap over a live widget too.
    private static EscapeCursor? ParseCursor(ZrdrDict? cursor)
    {
        if (cursor?.Str("BITMAP") is not { Length: > 0 } bitmap)
        {
            return null;
        }

        string rollover = cursor.Dict("ROLLOVER")?.Str("BITMAP") ?? string.Empty;
        return new EscapeCursor(
            bitmap, rollover.Length > 0 ? rollover : bitmap, cursor.Float("CENTER") != 0f);
    }

    private static EscapeMap ParseMap(ZrdrDict map) => new(
        map.Str("BITMAP") ?? string.Empty,
        Point(map, "POSITION"),
        Corners(map.Dict("CLIP")),
        Corners(map.Dict("WORLD")));

    // Each corner pair is truncated toward zero, which is the conversion the original's reader
    // applies to an authored float on its way into the control's integer members.
    private static EscapeRect Corners(ZrdrDict? corners)
    {
        if (corners == null)
        {
            return default;
        }

        return new EscapeRect(
            (int)corners.Float("topleft"),
            (int)corners.Float("topleft", 0f, 1),
            (int)corners.Float("bottomright"),
            (int)corners.Float("bottomright", 0f, 1));
    }

    private static BriefingPoint Point(ZrdrDict? d, string key) =>
        d == null ? default : new BriefingPoint(d.Float(key), d.Float(key, 0f, 1));
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSVM.Utils;

/// <summary>The instruments a HUD layout places. Each moves as one piece.</summary>
public enum HudElement
{
    /// <summary>The left dial column: rockets, altimeter, damage display.</summary>
    LeftDials,

    /// <summary>The right dial column: guns, speedometer, nitro.</summary>
    RightDials,

    /// <summary>The heading tape.</summary>
    CompassTape,

    /// <summary>The throttle plate.</summary>
    ThrottlePlate,
}

/// <summary>Which edge of the reading box, or its centre, an element measures from on one axis.</summary>
public enum HudAnchor
{
    /// <summary>The left edge, or the top edge.</summary>
    Start,

    /// <summary>The centre line.</summary>
    Centre,

    /// <summary>The right edge, or the bottom edge.</summary>
    End,
}

/// <summary>Where one element stands: on each axis an anchor and an offset in 1440p reference pixels.
/// The element's point at the anchor's fraction (left edge, centre or right edge) stands that offset
/// from the reading box's point at the same fraction. A margin kept from the right edge is kept on
/// every pane.</summary>
public readonly record struct HudPlacement(HudAnchor X, HudAnchor Y, float Dx, float Dy)
{
    /// <summary>The fraction of a span <paramref name="anchor"/> names: 0, a half or 1.</summary>
    public static float Fraction(HudAnchor anchor) => anchor switch
    {
        HudAnchor.Start => 0f,
        HudAnchor.Centre => 0.5f,
        _ => 1f,
    };
}

/// <summary>
/// One placement per <see cref="HudElement"/>, the stored half of a HUD layout. Immutable: an edit
/// returns a new layout through <see cref="With"/>. <see cref="Format"/> and <see cref="TryParse"/>
/// are the options file's spelling, one <c>element:x,y,dx,dy</c> clause per element. A layout
/// measures from the reading box, or from the pane's own edges when <see cref="PaneRelative"/>, as
/// every custom layout does. The pixels are <c>Flight.Hud.HudLayoutPlacement</c>'s.
/// docs/architecture/Utils.md.
/// </summary>
public sealed class HudLayout : IEquatable<HudLayout>
{
    /// <summary>The largest offset a stored layout may carry, in reference pixels on either axis. A
    /// 32:9 reading box is 5120 wide, and nothing placed past that can be on a pane.</summary>
    public const float MaxOffset = 6000f;

    private static readonly string[] ElementWords = { "leftDials", "rightDials", "compassTape", "throttlePlate" };
    private static readonly string[] AnchorWords = { "start", "centre", "end" };

    private readonly HudPlacement[] _placements;

    /// <summary>A layout with one placement per element, in <see cref="HudElement"/> order, measured
    /// from the pane's edges when <paramref name="paneRelative"/>, else from the reading box.</summary>
    public HudLayout(IReadOnlyList<HudPlacement> placements, bool paneRelative = false)
    {
        PaneRelative = paneRelative;
        ArgumentNullException.ThrowIfNull(placements);
        if (placements.Count != ElementWords.Length)
        {
            throw new ArgumentException($"a HUD layout places {ElementWords.Length} elements, got {placements.Count}", nameof(placements));
        }

        _placements = new HudPlacement[placements.Count];
        for (int i = 0; i < placements.Count; i++)
        {
            _placements[i] = placements[i];
        }
    }

    /// <summary>Every element, in the order a layout stores and an editor steps through them.</summary>
    public static IReadOnlyList<HudElement> Elements { get; } =
        new[] { HudElement.LeftDials, HudElement.RightDials, HudElement.CompassTape, HudElement.ThrottlePlate };

    /// <summary>Whether the anchors are the pane's own edges rather than the reading box's. A custom
    /// layout's are, so an element can stand at a wide pane's real edge.</summary>
    public bool PaneRelative { get; }

    /// <summary>The placement of <paramref name="element"/>.</summary>
    public HudPlacement this[HudElement element] => _placements[(int)element];

    /// <summary>The word the options file names <paramref name="element"/> by.</summary>
    public static string WordOf(HudElement element) => ElementWords[(int)element];

    /// <summary>Reads <see cref="Format"/>'s spelling. False for anything else: a missing or repeated
    /// element, an unknown word, an offset that is not a finite number within
    /// <see cref="MaxOffset"/>. A hand-edited layout that fails reads as never saved. Only a custom
    /// layout is stored, so what parses is pane-relative.</summary>
    public static bool TryParse(string? text, out HudLayout layout)
    {
        layout = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var placements = new HudPlacement?[ElementWords.Length];
        foreach (string clause in text.Split(';'))
        {
            int colon = clause.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return false;
            }

            int element = Array.IndexOf(ElementWords, clause[..colon].Trim());
            string[] parts = clause[(colon + 1)..].Split(',');
            if (element < 0 || placements[element] != null || parts.Length != 4)
            {
                return false;
            }

            int x = Array.IndexOf(AnchorWords, parts[0].Trim());
            int y = Array.IndexOf(AnchorWords, parts[1].Trim());
            if (x < 0 || y < 0 || !TryOffset(parts[2], out float dx) || !TryOffset(parts[3], out float dy))
            {
                return false;
            }

            placements[element] = new HudPlacement((HudAnchor)x, (HudAnchor)y, dx, dy);
        }

        var all = new HudPlacement[ElementWords.Length];
        for (int i = 0; i < all.Length; i++)
        {
            if (placements[i] is not { } p)
            {
                return false;
            }

            all[i] = p;
        }

        layout = new HudLayout(all, paneRelative: true);
        return true;
    }

    /// <summary>Whether <paramref name="text"/> reads as a layout, the options file's shape test.</summary>
    public static bool IsValid(string? text) => TryParse(text, out _);

    /// <summary>This layout with <paramref name="element"/> moved to <paramref name="placement"/>.</summary>
    public HudLayout With(HudElement element, HudPlacement placement)
    {
        var copy = (HudPlacement[])_placements.Clone();
        copy[(int)element] = placement;
        return new HudLayout(copy, PaneRelative);
    }

    /// <summary>These placements measured from the pane's edges, the same numbers re-read rather than
    /// converted. A layout that already is returns itself.</summary>
    public HudLayout InPane() => PaneRelative ? this : new HudLayout(_placements, paneRelative: true);

    /// <summary>The options file's spelling, e.g. <c>leftDials:start,centre,24,0;...</c>, every
    /// element in order, offsets with invariant decimals.</summary>
    public string Format()
    {
        var text = new StringBuilder();
        for (int i = 0; i < _placements.Length; i++)
        {
            var p = _placements[i];
            if (i > 0)
            {
                text.Append(';');
            }

            text.Append(ElementWords[i]).Append(':')
                .Append(AnchorWords[(int)p.X]).Append(',')
                .Append(AnchorWords[(int)p.Y]).Append(',')
                .Append(p.Dx.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                .Append(p.Dy.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <inheritdoc/>
    public bool Equals(HudLayout? other)
    {
        if (other is null || other.PaneRelative != PaneRelative)
        {
            return false;
        }

        for (int i = 0; i < _placements.Length; i++)
        {
            if (_placements[i] != other._placements[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as HudLayout);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(PaneRelative);
        foreach (var p in _placements)
        {
            hash.Add(p);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => Format();

    private static bool TryOffset(string text, out float value) =>
        float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && float.IsFinite(value) && Math.Abs(value) <= MaxOffset;
}

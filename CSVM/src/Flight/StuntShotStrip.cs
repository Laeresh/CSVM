using System.Collections.Generic;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// A stunt run's Danger Zone photographs as a board section, shared by
/// <see cref="StuntScoreboard"/> and <see cref="IaWrapupBoard"/>: a rule and one captioned
/// thumbnail per shot in marker order, sized so the whole strip fits the board. It follows its
/// <see cref="StuntCapture"/> while it is in the tree: a shot latched after the board was drawn
/// joins the strip, and a cell drawn before its frame landed is filled on
/// <see cref="StuntCapture.ShotLanded"/>. Hidden while the run has no shot to draw.
/// </summary>
public sealed partial class StuntShotStrip : VBoxContainer
{
    /// <summary>The thumbnail row's node name, so a suite can read the strip's own cells rather
    /// than pattern-matching the labels of the split table above it.</summary>
    internal const string StripName = "shot_strip";

    // Base metrics at 720p, scaled by the board's own scale. A long course shrinks each frame to
    // fit rather than pushing the panel off the pane. All TUNE.
    private const int CaptionFont = 12;
    private const float StripWidth = 560f;
    private const float StripGap = 6f;

    // Cells drawn before their shot landed, filled by OnShotLanded.
    private readonly Dictionary<StuntShot, (TextureRect Picture, float Width)> _pending = new();

    private StuntCapture _shots = null!;
    private HBoxContainer _row = null!;
    private float _scale;

    /// <summary>Appends the section to <paramref name="body"/> at scale <paramref name="s"/>.
    /// Null <paramref name="shots"/> adds nothing; a run with none yet adds the hidden section,
    /// which shows once a shot latches.</summary>
    public static void Add(VBoxContainer body, StuntCapture? shots, float s)
    {
        if (shots == null)
        {
            return;
        }

        var strip = new StuntShotStrip { _shots = shots, _scale = s };
        strip.AddThemeConstantOverride("separation", Mathf.RoundToInt(7f * s));
        strip.AddChild(ResultsBoard.Separator(s));
        strip._row = new HBoxContainer { Name = StripName };
        strip._row.AddThemeConstantOverride("separation", Mathf.RoundToInt(StripGap * s));
        strip.AddChild(ResultsBoard.Centered(strip._row));
        strip.Rebuild();
        body.AddChild(strip);
    }

    public override void _EnterTree()
    {
        _shots.ShotLatched += OnShotLatched;
        _shots.ShotLanded += OnShotLanded;
    }

    public override void _ExitTree()
    {
        _shots.ShotLatched -= OnShotLatched;
        _shots.ShotLanded -= OnShotLanded;
    }

    private static void Fill(TextureRect picture, Image thumb, float width)
    {
        picture.Texture = ImageTexture.CreateFromImage(thumb);
        picture.CustomMinimumSize = new Vector2(width, width * thumb.GetHeight() / Mathf.Max(1, thumb.GetWidth()));
    }

    // Marker order rather than the flown order the splits use: the strip is the course, and a
    // pilot reads it against the markers they know.
    private void Rebuild()
    {
        foreach (var cell in _row.GetChildren())
        {
            _row.RemoveChild(cell);
            cell.QueueFree();
        }
        _pending.Clear();

        float s = _scale;
        int count = _shots.Count;
        float width = count == 0 ? 0f : Mathf.Min(StuntCapture.ThumbWidth * s,
            ((StripWidth * s) - (StripGap * s * (count - 1))) / count);
        foreach (var shot in _shots.InMarkerOrder())
        {
            if (shot.Landed && shot.Thumb == null)
            {
                continue;
            }
            var cell = new VBoxContainer();
            var picture = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
            };
            cell.AddChild(picture);
            if (shot.Thumb is { } thumb)
            {
                Fill(picture, thumb, width);
            }
            else
            {
                // The run's last crossing usually wakes the board before its frame has landed.
                picture.CustomMinimumSize = new Vector2(width, width * 3f / 4f);
                _pending[shot] = (picture, width);
            }
            var caption = ResultsBoard.Label(shot.DzName, Mathf.Max(8, (int)(CaptionFont * s)), ResultsBoard.HeaderColor);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            cell.AddChild(caption);
            _row.AddChild(cell);
        }

        Visible = _row.GetChildCount() > 0;
    }

    // Every cell narrows by one more shot, so the row is drawn again rather than appended to.
    private void OnShotLatched(StuntShot shot) => Rebuild();

    private void OnShotLanded(StuntShot shot)
    {
        if (!_pending.Remove(shot, out var cell) || !IsInstanceValid(cell.Picture))
        {
            return;
        }

        if (shot.Thumb is { } thumb)
        {
            Fill(cell.Picture, thumb, cell.Width);
        }
        else
        {
            cell.Picture.GetParent<Control>().Visible = false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>One Danger Zone photograph: the marker it was latched at, the run clock at that
/// frame, the file it was written to, and the strip thumbnail the scoreboard draws.</summary>
public sealed class StuntShot
{
    public required string DzName { get; init; }

    /// <summary>Run clock, seconds, at the latch frame. The file name carries it, so a scripted
    /// run names the same file every time.</summary>
    public required float At { get; init; }

    public required string Path { get; init; }

    /// <summary>The downscaled copy the scoreboard strip draws, or null when the resize failed.
    /// Kept in memory so the strip costs no disk read at run end.</summary>
    public Image? Thumb { get; init; }
}

/// <summary>
/// The Danger Zone camera: one latched photograph of the pilot's own pane per <c>dzN</c> marker
/// per stunt run. <see cref="Update"/> tests the plane against each marker centre at
/// <see cref="StuntMission.DzRadius"/> every physics frame and latches on the frame the aircraft
/// first crosses inside, writing a PNG under <see cref="ShotDir"/> and firing <see cref="Sting"/>.
/// A marker that has been photographed is not photographed again in the same run, and one that is
/// still being flown through does not re-trigger while the aircraft stays inside its radius.
///
/// <para>Per pilot, like the run itself: each pane latches its own pane's pixels through the
/// <c>pane</c> delegate it was built with. <see cref="StuntScoreboard"/> draws the strip.</para>
/// </summary>
public sealed class StuntCapture
{
    /// <summary>Strip thumbnail width in pixels, near the 164-wide scrap region the original's
    /// scrapbook forces a player capture into. The height follows the pane's aspect.</summary>
    public const int ThumbWidth = 164;

    private readonly StuntMission _run;
    private readonly string _chapter;
    private readonly Func<Image?> _pane;
    private readonly StuntShot?[] _shots;
    private readonly bool[] _inside;

    /// <summary>Builds the camera for one pilot's run. <paramref name="chapter"/> names the file,
    /// and <paramref name="pane"/> yields that pilot's pane as an image (in splitscreen the seat's
    /// own SubViewport), or null on a frame with nothing rendered yet.</summary>
    public StuntCapture(StuntMission run, string chapter, Func<Image?> pane)
    {
        _run = run;
        _chapter = chapter;
        _pane = pane;
        _shots = new StuntShot?[run.TotalCount];
        _inside = new bool[run.TotalCount];
    }

    /// <summary>Where the shots go instead of <c>user://</c> while set, so a suite writes into its
    /// own scratch directory rather than beside the player's saves.</summary>
    public static string? DirectoryOverride { get; set; }

    /// <summary>Fired once per latched photograph, the <c>snd_dangerzone_camera</c> sting. Null
    /// leaves the latch silent, which is what a rig with no audio gets.</summary>
    public Action? Sting { get; set; }

    /// <summary>How many photographs this run has latched.</summary>
    public int Count { get; private set; }

    /// <summary><c>screenshots/stunts/</c> beside the saves: the engine's writable user directory,
    /// where <c>stunt_scores.json</c> and the profiles already live.</summary>
    public static string ShotDir() => Path.Combine(
        DirectoryOverride ?? ProjectSettings.GlobalizePath("user://"), "screenshots", "stunts");

    /// <summary>The file one latch writes: chapter, marker and run clock, e.g.
    /// <c>C4_dz3_0m12.4s.png</c>. Invariant culture and the run clock rather than the wall clock,
    /// so a scripted run is reproducible and a German-locale machine names the same file.</summary>
    public static string FileName(string chapter, string dzName, float at)
    {
        int min = (int)(at / 60f);
        return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}m{3:00.0}s.png",
            chapter, dzName, min, at - min * 60f);
    }

    /// <summary>This run's photographs in marker order, the order the zones are authored in, which
    /// is what the scoreboard strip shows. Skips markers not yet photographed.</summary>
    public IEnumerable<StuntShot> InMarkerOrder()
    {
        foreach (var shot in _shots)
        {
            if (shot != null)
            {
                yield return shot;
            }
        }
    }

    /// <summary>Physics-frame test against this frame's committed plane position: latches the
    /// markers the aircraft has just crossed into.</summary>
    public void Update(Vector3 planePos)
    {
        float radius = StuntMission.DzRadius * StuntMission.DzRadius;
        for (int i = 0; i < _shots.Length; i++)
        {
            if (planePos.DistanceSquaredTo(_run.Zones[i].Position) > radius)
            {
                _inside[i] = false;
                continue;
            }
            // Inside: the latch is the CROSSING, so a pass that lingers inside the radius takes one
            // photograph on its first frame and none after it.
            if (_inside[i])
            {
                continue;
            }
            if (_shots[i] != null)
            {
                _inside[i] = true;
                continue;
            }
            // ⚠ Mark the pass only once a frame was actually photographed. A session whose first
            // rendered frame has not landed yet would otherwise spend the marker on nothing.
            if (Latch(_run.Zones[i]) is { } shot)
            {
                _shots[i] = shot;
                _inside[i] = true;
            }
        }
    }

    /// <summary>A fresh run: every marker photographable again. ⚠ Does not clear which markers the
    /// aircraft is standing inside; a restart under a marker must not latch it without flying
    /// through it again.</summary>
    public void Reset()
    {
        Array.Clear(_shots, 0, _shots.Length);
        Count = 0;
    }

    // The strip copy, made once here rather than at run end: the full pane image is the frame that
    // was just rendered, and holding one per marker is far more memory than the strip needs.
    private static Image Thumbnail(Image full)
    {
        var thumb = (Image)full.Duplicate();
        int height = Math.Max(1, ThumbWidth * full.GetHeight() / full.GetWidth());
        thumb.Resize(ThumbWidth, height, Image.Interpolation.Bilinear);
        return thumb;
    }

    private StuntShot? Latch(StuntZone zone)
    {
        var img = _pane();
        if (img == null || img.GetWidth() <= 0 || img.GetHeight() <= 0)
        {
            Log.Warn("flight", $"stunt capture: no pane image at {zone.DzName}, nothing latched");
            return null;
        }

        float at = _run.Elapsed;
        string dir = ShotDir();
        string path = Path.Combine(dir, FileName(_chapter, zone.DzName, at));
        Directory.CreateDirectory(dir);
        var err = img.SavePng(path);
        if (err != Error.Ok)
        {
            Log.Error("flight", $"stunt capture: could not write {path} ({err})");
        }
        else
        {
            Log.Info("flight", $"stunt capture: {zone.DzName} at {StuntMission.FormatTime(at)} -> {path}");
        }

        Count++;
        Sting?.Invoke();
        return new StuntShot
        {
            DzName = zone.DzName,
            At = at,
            Path = path,
            Thumb = Thumbnail(img),
        };
    }
}

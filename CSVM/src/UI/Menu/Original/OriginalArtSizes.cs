using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The art measurer every host of <see cref="OriginalShell"/> hands it: one art name answered with
/// that file's pixel size, cached per name over the extraction root the art loads from. The shell
/// needs it because the layout carries a widget's position and its art but not the art's size, and
/// a name that does not measure leaves the row it sizes on a fallback rectangle, which is the
/// rectangle the pointer then hits. A movie's size comes from its sequence header, which no bitmap
/// loader can read; a file that is not there measures as null once and is logged once.
/// </summary>
public sealed class OriginalArtSizes
{
    private readonly Dictionary<string, (int Width, int Height)?> _sizes = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _dataRoot;
    private readonly string _who;

    /// <summary>A measurer over <paramref name="dataRoot"/>'s extraction, naming itself
    /// <paramref name="who"/> in the line it logs for art it cannot read.</summary>
    public OriginalArtSizes(string dataRoot, string who)
    {
        _dataRoot = dataRoot ?? throw new ArgumentNullException(nameof(dataRoot));
        _who = who ?? throw new ArgumentNullException(nameof(who));
    }

    /// <summary>The named art's pixel size, or null where the file does not read.</summary>
    public (int Width, int Height)? Measure(string art)
    {
        if (_sizes.TryGetValue(art, out var cached))
        {
            return cached;
        }

        (int Width, int Height)? size = null;
        string path = OriginalAvailability.ArtPath(_dataRoot, art);
        if (OriginalAvailability.IsMovie(art))
        {
            size = MovieSize(path);
        }
        else if (File.Exists(path) && Image.LoadFromFile(path) is { } image && !image.IsEmpty())
        {
            size = (image.GetWidth(), image.GetHeight());
        }

        if (size == null)
        {
            // Once per name, since the answer is cached: the row keeps its fallback rectangle and
            // the screen draws, which is what an optional file's absence degrades to.
            Log.Info("ui", $"{_who}: {art} does not read at {path}; the row it sizes keeps its fallback rectangle");
        }

        _sizes[art] = size;
        return size;
    }

    // A movie's picture size, which its sequence header carries and no bitmap loader can read.
    // Opened for the header alone and dropped; the surface the board draws from opens it again,
    // once, and that copy is the one that decodes.
    private static (int Width, int Height)? MovieSize(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var movie = CSVM.Video.MpegMovie.FromFile(path);
            return (movie.Width, movie.Height);
        }
        catch (Exception e)
            when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

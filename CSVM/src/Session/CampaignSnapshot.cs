using System.Globalization;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The campaign Danger Zone photograph: one still per zone, written into the flying profile's own
/// directory under the <c>Snap_&lt;mission&gt;_&lt;objective&gt;</c> name the scrapbook's capture
/// rows resolve against (<c>docs/formats/campaign-screens.md</c>, "The danger-zone slot"). The
/// objective number is the mission's own <c>dzones.zrd</c> <c>objective_numbers</c> entry, so the
/// file the pilot writes and the row that draws it are named by the same data.
///
/// <para>A latch stages the file under a pending extension and <see cref="Commit"/> keeps it only
/// for a won mission, which is how the original drops the photographs of a failed attempt.</para>
/// </summary>
public static class CampaignSnapshot
{
    /// <summary>The region the scrapbook forces a capture into, so the file is written at exactly
    /// that size: the page draws it at 25% and the zoom at full size, both off this one bitmap.
    /// A pane of any other shape is squashed into it, which is what the original's own forced
    /// region does to whatever the file holds.</summary>
    public const int Width = 164;

    /// <summary>The forced region's height, the partner of <see cref="Width"/>.</summary>
    public const int Height = 123;

    // The objective-number band the original's commit sweep walks (FUN_004072a0): every pending
    // file in it is renamed on a win and deleted on a loss. Wider than the 18-31 the shipped
    // dzones.zrd assigns and wider than the 18-30 the debrief turns into mask bits.
    private const int FirstPending = 10;
    private const int LastPending = 31;

    // The staged extension the original writes during the flight, renamed to .PNG at mission end.
    private const string PendingExtension = "PN_";

    /// <summary>The name a scrapbook capture row resolves against the profile directory:
    /// <c>Snap_&lt;mission&gt;_&lt;objective&gt;.PNG</c>, the mission being the 1-based campaign
    /// ordinal (<c>CampaignMission.Ordinal</c>) the book's own slot numbering uses.</summary>
    public static string FileName(int mission, int objective) =>
        string.Format(CultureInfo.InvariantCulture, "Snap_{0}_{1}.PNG", mission, objective);

    /// <summary>Writes one zone's photograph into <paramref name="directory"/> as a pending file,
    /// scaled to the forced region. Answers the path written, or null when there was no pane to
    /// photograph or the write failed.</summary>
    public static string? Stage(string directory, int mission, int objective, Image? pane)
    {
        if (pane == null || pane.GetWidth() <= 0 || pane.GetHeight() <= 0)
        {
            Log.Warn("core", $"danger zone snapshot: no pane image for objective {objective}, nothing written");
            return null;
        }

        var shot = (Image)pane.Duplicate();
        shot.Resize(Width, Height, Image.Interpolation.Bilinear);
        string path = Path.Combine(directory, Pending(mission, objective));
        Directory.CreateDirectory(directory);
        var err = shot.SavePng(path);
        if (err != Error.Ok)
        {
            Log.Error("core", $"danger zone snapshot: could not write {path} ({err})");
            return null;
        }

        Log.Info("core", $"danger zone snapshot: objective {objective} -> {path}");
        return path;
    }

    /// <summary>Mission end: a won mission's staged photographs replace whatever the profile held
    /// under their scrapbook names, a lost one's are dropped. Answers how many files it acted on,
    /// so a caller can log the sweep.</summary>
    public static int Commit(string directory, int mission, bool won)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        int acted = 0;
        for (int objective = FirstPending; objective <= LastPending; objective++)
        {
            string staged = Path.Combine(directory, Pending(mission, objective));
            if (!File.Exists(staged))
            {
                continue;
            }

            acted++;
            if (!won)
            {
                File.Delete(staged);
                continue;
            }

            string kept = Path.Combine(directory, FileName(mission, objective));
            File.Delete(kept);
            File.Move(staged, kept);
        }

        return acted;
    }

    private static string Pending(int mission, int objective) =>
        string.Format(CultureInfo.InvariantCulture, "Snap_{0}_{1}.{2}", mission, objective, PendingExtension);
}

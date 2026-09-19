using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The campaign Danger Zone photograph: one still per zone, written into the flying profile's own
/// directory. Its <c>Snap_&lt;mission&gt;_&lt;objective&gt;</c> name is the one the scrapbook's
/// capture rows resolve against (<c>docs/formats/campaign-screens.md</c>, "The danger-zone slot").
/// The objective number is the mission's own <c>dzones.zrd</c> <c>objective_numbers</c> entry.
/// The file the pilot writes and the row that draws it are named by the same data.
///
/// A latch stages the file under a pending extension, and <see cref="Commit"/> keeps it only
/// for a won mission. That is how the original drops the photographs of a failed attempt.
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

    // Staged paths whose frame has not landed yet, with the verdict Commit left for them (null while
    // the mission is still being flown). Stage and Commit run on the main thread, a landing on a
    // worker, so every read and write holds Gate.
    private static readonly object Gate = new();
    private static readonly Dictionary<string, bool?> InFlight = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The name a scrapbook capture row resolves against the profile directory:
    /// <c>Snap_&lt;mission&gt;_&lt;objective&gt;.PNG</c>, the mission being the 1-based campaign
    /// ordinal (<c>CampaignMission.Ordinal</c>) the book's own slot numbering uses.</summary>
    public static string FileName(int mission, int objective) =>
        string.Format(CultureInfo.InvariantCulture, "Snap_{0}_{1}.PNG", mission, objective);

    /// <summary>Requests one zone's photograph through <paramref name="pane"/> (the session hands
    /// the pilot's <see cref="Flight.DangerZonePhotograph"/>), written into
    /// <paramref name="directory"/> as a pending file scaled to the forced region once the frame
    /// lands. Answers the path it will be staged at, or null when there was no pane to
    /// photograph.</summary>
    public static string? Stage(string directory, int mission, int objective, PaneRequest? pane)
    {
        string staged = Path.Combine(directory, Pending(mission, objective));
        string kept = Path.Combine(directory, FileName(mission, objective));
        lock (Gate)
        {
            InFlight[staged] = null;
        }

        if (pane == null || !pane(frame => Develop(frame, directory, staged, kept, objective)))
        {
            lock (Gate)
            {
                InFlight.Remove(staged);
            }

            Log.Warn("core", $"danger zone snapshot: no pane image for objective {objective}, nothing written");
            return null;
        }

        return staged;
    }

    /// <summary>Mission end: a won mission's staged photographs replace whatever the profile held
    /// under their scrapbook names, a lost one's are dropped. A photograph still on its way takes
    /// the same verdict when it lands. Answers how many photographs it acted on, so a caller can
    /// log the sweep.</summary>
    public static int Commit(string directory, int mission, bool won)
    {
        int acted = 0;
        lock (Gate)
        {
            for (int objective = FirstPending; objective <= LastPending; objective++)
            {
                string staged = Path.Combine(directory, Pending(mission, objective));
                if (InFlight.ContainsKey(staged))
                {
                    InFlight[staged] = won;
                    acted++;
                }
                else if (File.Exists(staged))
                {
                    Settle(staged, Path.Combine(directory, FileName(mission, objective)), won);
                    acted++;
                }
            }
        }

        return acted;
    }

    // Worker thread: the resize and the encode, then the mission-end verdict if Commit has already
    // passed this photograph by.
    private static void Develop(Image? frame, string directory, string staged, string kept, int objective)
    {
        bool written = false;
        if (frame == null || frame.GetWidth() <= 0 || frame.GetHeight() <= 0)
        {
            Log.Warn("core", $"danger zone snapshot: the pane never arrived for objective {objective}, nothing written");
        }
        else
        {
            frame.Resize(Width, Height, Image.Interpolation.Bilinear);
            Directory.CreateDirectory(directory);
            var err = frame.SavePng(staged);
            written = err == Error.Ok;
            if (written)
            {
                Log.Info("core", $"danger zone snapshot: objective {objective} -> {staged}");
            }
            else
            {
                Log.Error("core", $"danger zone snapshot: could not write {staged} ({err})");
            }
        }

        lock (Gate)
        {
            if (InFlight.Remove(staged, out bool? verdict) && verdict is { } won && written)
            {
                Settle(staged, kept, won);
            }
        }
    }

    private static void Settle(string staged, string kept, bool won)
    {
        if (!won)
        {
            File.Delete(staged);
            return;
        }

        File.Delete(kept);
        File.Move(staged, kept);
    }

    private static string Pending(int mission, int objective) =>
        string.Format(CultureInfo.InvariantCulture, "Snap_{0}_{1}.{2}", mission, objective, PendingExtension);
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;
using CSVM.UI.Menu;

namespace CSVM.UI;

/// <summary>One row of the pause parchment: the objective's own words and whether it is done.
/// A done row keeps the colour an open row has and takes the mark instead
/// (<c>docs/formats/objectives.md</c>).</summary>
public readonly record struct PauseObjective(string Text, bool Completed);

/// <summary>An icon the screen places by world position through the mission map's own window,
/// which the chart turns into a pixel. <see cref="Revs"/> is its turn in revolutions clockwise, 0
/// for an icon that does not face anywhere.</summary>
public readonly record struct PauseWorldIcon(string Bitmap, float WorldX, float WorldZ, float Revs);

/// <summary>
/// The pause screen's authored half: the dialog its mission resolves to, the shared block every
/// dialog in the file borrows, the reveal that places the pins and picks the parchment's rows, and
/// the labels the four strips carry. Built once per mission, since none of it changes while the
/// mission runs. Decode: docs/org/pause-screen.md.
/// </summary>
public sealed record PauseSheet(
    EscapeState State,
    EscapeShared Shared,
    BriefingReveal Reveal,
    string ObjectivesTitle,
    IReadOnlyList<string> ButtonLabels)
{
    // How far the script is run out, and in what step. Long enough for every authored wait and
    // tween in the shipped dialogs, which is a tenth of a second and a few seconds respectively.
    private const double SettleSeconds = 1.0;
    private const int SettleSteps = 64;

    /// <summary>Reads one dialog and its shared block, runs its script, and resolves every label
    /// through the message table. The reveal is given no narration cue points, which settles the
    /// whole sheet at once: an <c>ESC_SCRIPT</c> carries no sound and no marker to wait on.</summary>
    public static PauseSheet? Load(
        string zrdrPath, string messagesPath, string dialogKey, bool instantAction)
    {
        EscapeDialog dialog;
        Messages messages;
        try
        {
            dialog = EscapeDialog.Load(
                zrdrPath,
                instantAction ? EscapeDialog.InstantActionFile : EscapeDialog.CampaignFile);
            messages = Messages.Load(messagesPath);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // Shown over a running mission, so an unreadable extraction leaves the screen to the
            // built-in board rather than taking the session down with it.
            return null;
        }

        if (dialog.Find(dialogKey) is not { } state || dialog.Shared is not { } shared)
        {
            return null;
        }

        var labels = new List<string>();
        foreach (string key in PauseScreens.ButtonKeys)
        {
            labels.Add(Text(messages, shared.Button(key)?.LabelKey ?? string.Empty));
        }

        return new PauseSheet(
            state,
            shared,
            Settled(state.Steps),
            Text(messages, shared.Objectives?.TitleKey ?? string.Empty),
            labels);
    }

    // The sheet is a still, so its script is run out rather than played. ⚠ Do not build the reveal
    // and leave it: a script's authored Wait blocks every beat after it, and 7 of the 24 campaign
    // dialogs place their flag pins past one, so those screens would draw a map with no flags at
    // all. One Advance releases one wait, hence the loop; the cap only bounds a script that cannot
    // complete, and a spin's own tween lands at its end revolutions the way a settled screen shows.
    private static BriefingReveal Settled(IReadOnlyList<BriefingStep> steps)
    {
        var reveal = new BriefingReveal(steps, Array.Empty<double>());
        for (int i = 0; i < SettleSteps && !reveal.Complete; i++)
        {
            reveal.Advance(SettleSeconds);
        }

        return reveal;
    }

    // A key the table does not carry comes back as itself, which is worse than nothing on a strip.
    private static string Text(Messages messages, string key)
    {
        if (key.Length == 0)
        {
            return string.Empty;
        }

        string text = messages.Get(key);
        return text.StartsWith("MSG_", StringComparison.Ordinal) ? string.Empty : text;
    }
}

/// <summary>What the pause screen shows of the running mission: the objectives and their marks, the
/// profile's memento, and the icons the chart places by world position.</summary>
public sealed record PauseReadout(
    IReadOnlyList<PauseObjective> Objectives,
    string Memento,
    IReadOnlyList<PauseWorldIcon> Icons)
{
    /// <summary>A readout with nothing in it, for a dialog that carries no map or parchment.</summary>
    public static PauseReadout Empty { get; } = new(
        Array.Empty<PauseObjective>(), string.Empty, Array.Empty<PauseWorldIcon>());
}

/// <summary>
/// What the Original presentation's pause screen is made of, engine-free: the mission's chart at
/// its authored crop, the pins and icons the dialog's script places, the objectives parchment, the
/// profile's memento and the four button strips. Composed the way the load and briefing screens
/// are, through <see cref="ComposedBoard"/>, and sharing <see cref="MissionMap"/> with both.
/// </summary>
public static class PauseScreens
{
    /// <summary>RESUME's row.</summary>
    public const int ResumeRow = 0;

    /// <summary>RESTART's row.</summary>
    public const int RestartRow = 1;

    /// <summary>PREFERENCES' row.</summary>
    public const int PreferencesRow = 2;

    /// <summary>QUIT's row.</summary>
    public const int QuitRow = 3;

    // The list's own face, which fonts.zrd gives as Andy Bold at 14 px, and the title's beside it.
    private const float ListFont = 14f;
    private const float TitleFont = 17f;

    /// <summary>The four strips, in the order the shared <c>BUTTONS</c> block authors them, which
    /// is also the order a cursor walks them.</summary>
    public static IReadOnlyList<string> ButtonKeys { get; } = new[]
    {
        "RESUME_MISSION_BTN", "RESTART_MISSION_BTN", "CONFIGURE_BTN", "MAINMENU_BTN",
    };

    /// <summary>Composes the screen for one pause. <paramref name="focusedRow"/> is the strip the
    /// cursor stands on and <paramref name="pressed"/> whether it is held, which is what picks
    /// between the three authored strip bitmaps and their three label inks.</summary>
    public static ComposedBoard For(
        PauseSheet sheet, PauseReadout readout, int focusedRow, bool pressed)
    {
        var backdrop = new List<BoardPicture>();
        if (sheet.State.Background.Length > 0)
        {
            backdrop.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Rimage, sheet.State.Background), 0, 0, 0, false, 1f, 0f,
                BoardFit.AuthoredWidth, BoardFit.AuthoredHeight));
        }

        var pictures = new List<BoardPicture>();
        if (sheet.State.Map is { Bitmap.Length: > 0 } map)
        {
            pictures.Add(MissionMap.Sheet(map));
        }

        if (sheet.Shared.Objectives is { Background.Length: > 0 } list)
        {
            pictures.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Rimage, list.Background),
                list.BackgroundAt.X, list.BackgroundAt.Y));
        }

        MissionMap.Elements(pictures, sheet.Reveal, back: true);
        MissionMap.Elements(pictures, sheet.Reveal, back: false);
        AddWorldIcons(pictures, sheet, readout);
        AddMemento(pictures, sheet, readout);

        var lines = new List<BoardLine>();
        if (sheet.Shared.Objectives is { } titled && sheet.ObjectivesTitle.Length > 0)
        {
            lines.Add(new BoardLine(
                sheet.ObjectivesTitle, titled.TitleAt.X, titled.TitleAt.Y, 0f, TitleFont,
                BoardInk.Heading));
        }

        return new ComposedBoard(
            pictures,
            MissionMap.Strokes(sheet.Reveal),
            lines,
            Plaques(sheet, focusedRow, pressed),
            Notes(sheet, readout),
            backdrop);
    }

    // The parchment's rows: one per objective the script revealed, in the order it revealed them,
    // with the mark riding the row rather than a column of its own.
    private static IReadOnlyList<BoardNote> Notes(PauseSheet sheet, PauseReadout readout)
    {
        if (sheet.Shared.Objectives is not { } list || sheet.Reveal.RevealedObjectives.Count == 0)
        {
            return Array.Empty<BoardNote>();
        }

        var entries = new List<string>();
        var marked = new List<bool>();
        foreach (int index in sheet.Reveal.RevealedObjectives)
        {
            if (index >= 0 && index < readout.Objectives.Count)
            {
                entries.Add(readout.Objectives[index].Text);
                marked.Add(readout.Objectives[index].Completed);
            }
        }

        if (entries.Count == 0)
        {
            return Array.Empty<BoardNote>();
        }

        return new[]
        {
            new BoardNote(
                entries, list.ListAt.X, list.ListAt.Y, list.WrapWidth, list.WrapHeight,
                list.Spacing, ListFont, BoardInk.Row,
                list.CheckMark.Length > 0
                    ? new BoardArt(BoardArtLibrary.Rimage, list.CheckMark)
                    : null,
                marked),
        };
    }

    // Each strip is three separate bitmaps rather than one stacked frame, so the state picks the
    // art and the ink together; there is no disabled frame to pick.
    private static IReadOnlyList<BoardPlaque> Plaques(PauseSheet sheet, int focusedRow, bool pressed)
    {
        var plaques = new List<BoardPlaque>();
        for (int row = 0; row < ButtonKeys.Count; row++)
        {
            if (sheet.Shared.Button(ButtonKeys[row]) is not { } button)
            {
                continue;
            }

            bool focused = row == focusedRow;
            bool held = focused && pressed;
            string art = held ? button.Activate : focused ? button.Rollover : button.Normal;
            if (art.Length == 0)
            {
                continue;
            }

            plaques.Add(new BoardPlaque(
                new BoardArt(BoardArtLibrary.Rimage, art), button.At.X, button.At.Y, row, 0,
                row < sheet.ButtonLabels.Count ? sheet.ButtonLabels[row] : string.Empty,
                ComposedBoard.PlaqueInk(focused, held)));
        }

        return plaques;
    }

    private static void AddWorldIcons(
        List<BoardPicture> into, PauseSheet sheet, PauseReadout readout)
    {
        if (sheet.State.Map is not { } map)
        {
            return;
        }

        foreach (var icon in readout.Icons)
        {
            if (MissionMap.Icon(map, icon.Bitmap, icon.WorldX, icon.WorldZ, icon.Revs) is { } placed)
            {
                into.Add(placed);
            }
        }
    }

    // The primitive's position is authored and its picture is not: the runtime replaces the
    // placeholder with the profile's own memento image.
    private static void AddMemento(
        List<BoardPicture> into, PauseSheet sheet, PauseReadout readout)
    {
        string bitmap = readout.Memento.Length > 0 ? readout.Memento : sheet.State.MementoBitmap;
        if (bitmap.Length > 0)
        {
            into.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Rimage, bitmap),
                sheet.State.MementoAt.X, sheet.State.MementoAt.Y));
        }
    }
}

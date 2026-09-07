using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The credits screen, the shell's partial over the decoded <c>[@Credits@]</c> section behind the
/// top level's fifth row. The section is three widgets and nothing more: a full-screen background
/// pane, ABOUT and the DONE plaque. The credit names are painted into the background art, so the
/// screen lays out no roster of its own and the pane is the whole composition; the two buttons are
/// drawn over it by the shell's row loop. DONE and Escape both land on the top level, which is the
/// plaque's own <c>ScriptToExe</c> and what <c>CREDITS.SCRIPT</c>'s <c>gui_char</c> does.
/// The screen and its unbuilt parts: <c>docs/org/menu-inventory.md</c>.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The layout section the screen is composed from.</summary>
    public const string CreditsSection = "Credits";

    /// <summary>The top level's door onto it.</summary>
    public const string CreditsDoorKey = "MM_B_CREDITS";

    /// <summary>ABOUT, which the original answers with a message box this screen does not raise.</summary>
    public const string CreditsAboutKey = "CR_B_About";

    /// <summary>The DONE plaque, back to the top level.</summary>
    public const string CreditsExitKey = "CR_B_Exit";

    // The pane the credit names are painted into. It wears no art of its own beyond the section's
    // row, so a layout that renames the file moves ours with it.
    private const string CreditsBackgroundKey = "CR_BackGround";

    // ⚠ ABOUT is drawn disabled; do not enable it without the box behind it. CREDITS.SCRIPT answers
    // its press by setting @globals@OR.XR, which switches the messagebox's whole widget set to the
    // ma_ prefix over CR_AboutMessageBox.png, and the shared chrome composes the mb_ set alone. The
    // words are decoded and are not what is missing.
    private void BuildCreditsRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(CreditsSection);
        if (screen?.Widget(CreditsAboutKey) is { } about)
        {
            rows.Add(Button(about, false));
        }

        if (screen?.Widget(CreditsExitKey) is { } exit)
        {
            rows.Add(Button(exit, true));
        }
    }

    private void ComposeCredits(List<BoardPicture> pictures)
    {
        if (_layout.Screen(CreditsSection)?.Widget(CreditsBackgroundKey) is { Art.Count: > 0 } pane)
        {
            pictures.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Ui, pane.Art[0], Math.Max(1, pane.Frames)),
                pane.Int("X"), pane.Int("Y")));
        }
    }

    // Only DONE answers. ABOUT is present as a disabled row, so no press reaches here for it, and
    // Escape needs no arm because the shell's own Back falls through to the top level.
    private MenuExit? ActivateCredits(OriginalRow row)
    {
        if (row.Key == CreditsExitKey)
        {
            Open(OriginalScreen.TopLevel);
        }

        return null;
    }
}

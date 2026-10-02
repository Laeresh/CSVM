using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Bindings;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The CONTROLS page behind the Options hub, one page module over the decoded
/// <c>[@ControlsPrefs@]</c> section and the shared <see cref="ControlsFeature"/>. The seat chooser
/// takes the Controller Type row and the flying-scheme chooser the Mouse panel's title line. The
/// authored Mouse Sensitivity slider and the KEYS AND BUTTONS door (<see cref="OriginalKeysPage"/>)
/// follow. Its edits are staged in the feature, so its ACCEPT CHANGES writes the keymaps and its
/// CANCEL CHANGES drops the whole visit.
/// </summary>
public sealed class OriginalControlsPage : IOriginalOptionsPage
{
    /// <summary>The CONTROLS page's layout section.</summary>
    public const string Section = "ControlsPrefs";

    /// <summary>The page's seat chooser, which takes the Controller Type row.</summary>
    public const string PlayerKey = "CP_D_Fly";

    /// <summary>The page's flying-scheme chooser, on the Mouse Sensitivity panel's title line.
    /// Remake-only, so the layout names no widget for it.</summary>
    public const string MouseKey = "MOUSESCHEME";

    /// <summary>The page's Mouse Sensitivity slider, the authored one, which sets the Fly scheme's
    /// sensitivity.</summary>
    public const string SensitivityKey = "CP_S_MOUSE";

    /// <summary>The page's door onto the KEYS AND BUTTONS page.</summary>
    public const string KeysDoorKey = "CP_B_KEYS";

    /// <summary>The page's ACCEPT CHANGES, which writes the staged keymaps.</summary>
    public const string AcceptKey = "CP_B_ACCEPTCHANGES";

    /// <summary>The page's CANCEL CHANGES, which drops them.</summary>
    public const string CancelKey = "CP_B_CANCELCHANGES";

    // The page's authored row shape, used where a layout does not carry the section.
    private const float TitleX = 136f;
    private const float TitleWidth = 173f;
    private const float DropX = 134f;
    private const float DropY = 315f;
    private const float DropWidth = 175f;
    private const float ItemHeight = 17f;
    private const float DescX = 349f;
    private const float DescWidth = 310f;
    private const float MouseX = 142f;
    private const float MouseY = 391f;
    private const float MouseTitleY = 364f;
    private const float MouseDescY = 373f;

    private readonly OriginalOptionsChrome _chrome;
    private readonly IOriginalScreenHost _host;
    private readonly IOriginalOptionsForm _form;
    private readonly ControlsFeature? _controls;
    private readonly OriginalKeysPage _keys;

    internal OriginalControlsPage(
        OriginalOptionsChrome chrome, IOriginalOptionsForm form, ControlsFeature? controls, OriginalKeysPage keys)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _host = chrome.Host;
        _controls = controls;
    }

    OriginalScreen IOriginalOptionsPage.Screen => OriginalScreen.ControlsPrefs;

    string IOriginalOptionsPage.AcceptKey => AcceptKey;

    string IOriginalOptionsPage.CancelKey => CancelKey;

    // Whether a seat is registered at all. Without one the feature holds no keymap to read, so the
    // seat and scheme rows stand disabled rather than drawing somebody's blanks.
    private bool Seated => _controls is { } controls && controls.Players.Count > 0;

    // Whether the flying-scheme row can be pressed at all. A seat has to be registered, and it has
    // to read the keyboard. A pad-only splitscreen seat holds no mouse to hand the stick.
    private bool MouseSchemeLive => Seated && _controls!.ReadsKeyboard;

    /// <summary>Opens the page on its first row, the hub's CONTROLS door and the screenshot aid's
    /// door alike.</summary>
    public void Open()
    {
        _host.Open(OriginalScreen.ControlsPrefs);
        _host.FocusedRow = -1;
    }

    // The seat chooser, the flying scheme on the Mouse panel's title line and the authored slider.
    // The KEYS AND BUTTONS door and the exit pair follow, all one column. Without the section they
    // stand as text buttons so the page is still walkable.
    void IOriginalOptionsPage.BuildRows(List<OriginalRow> rows)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            rows.Add(_host.PlaqueRow(PlayerKey, PlayerWord(), 0, Seated, 0));
            rows.Add(_host.PlaqueRow(MouseKey, MouseWord(), 1, MouseSchemeLive, 0));
            rows.Add(_host.PlaqueRow(SensitivityKey, SensitivityWord(), 2, MouseSchemeLive, 0));
            rows.Add(_host.PlaqueRow(KeysDoorKey, "KEYS AND BUTTONS", 3, true, 0));
            _chrome.AddFallbackPlaques(rows, AcceptKey, CancelKey, 4);
            return;
        }

        var box = PlayerBox(screen);
        var arrow = OriginalOptionsChrome.StripArt(screen.Widget(PlayerKey)?.Art ?? Array.Empty<string>(), 4);
        rows.Add(new OriginalRow(PlayerKey, PlayerWord(), OriginalRowKind.Dropdown,
            box.X, box.Y, box.Width, box.Height, Seated, 0, arrow));
        var mouseBox = MouseBox(screen);
        rows.Add(new OriginalRow(MouseKey, MouseWord(), OriginalRowKind.Dropdown,
            mouseBox.X, mouseBox.Y, mouseBox.Width, mouseBox.Height, MouseSchemeLive, 0, arrow));
        // The authored slider as the original draws it, over the one scale both presentations step.
        rows.Add(_chrome.SliderRow(screen.Widget(SensitivityKey), SensitivityKey,
            MouseX, MouseY, 0, SensitivityScale.MaxLevel,
            SensitivityScale.Level(Seated ? _controls!.MouseSensitivity : SensitivityScale.Default),
            level =>
            {
                if (MouseSchemeLive)
                {
                    _controls!.MouseSensitivity = SensitivityScale.FromLevel(level);
                }
            },
            MouseSchemeLive));
        _chrome.AddStrip(screen, rows, KeysDoorKey, OriginalRowKind.Button, true, 0);
        _chrome.AddPlaques(screen, rows, AcceptKey, CancelKey);
    }

    void IOriginalOptionsPage.Lists(List<OriginalList> lists)
    {
    }

    // A sideways step on the seat chooser picks the next player and one on the scheme row flips it;
    // anything else crosses columns. The slider is the shell's own step and never reaches here. The
    // text-button page drawn without the section has no slider, so its sensitivity row steps here.
    bool IOriginalOptionsPage.StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count)
        {
            return false;
        }

        return rows[focus].Key switch
        {
            PlayerKey => StepPlayer(direction),
            MouseKey => StepMouseScheme(),
            SensitivityKey => StepSensitivity(direction),
            _ => false,
        };
    }

    bool IOriginalOptionsPage.CloseDropdown() => false;

    MenuExit? IOriginalOptionsPage.Activate(OriginalRow row)
    {
        switch (row.Key)
        {
            case PlayerKey:
                StepPlayer(1);
                break;
            case MouseKey:
                StepMouseScheme();
                break;
            case KeysDoorKey:
                _keys.Open();
                break;
        }

        return null;
    }

    MenuExit? IOriginalOptionsPage.Accept()
    {
        _controls?.Accept();
        _form.Leave();
        return null;
    }

    void IOriginalOptionsPage.Cancel()
    {
        _controls?.Cancel();
        _form.Leave();
    }

    // A pending steal is dropped first, then the page leaves the way its own CANCEL CHANGES does,
    // the declining answer the layout gives it.
    void IOriginalOptionsPage.Back()
    {
        if (_controls is { Pending: not null } pending)
        {
            pending.DiscardSteal();
            return;
        }

        _controls?.Cancel();
        _form.Leave();
    }

    // The plate in the backdrop and the page title. The seat and mouse rows carry their own titles
    // and descriptions beside the authored KEYS AND BUTTONS one. The rows follow, the slider among
    // them in its own slot and thumb.
    void IOriginalOptionsPage.Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            _host.ComposePlainPage("CONTROLS", rows, focus, layers);
            return;
        }

        _chrome.ComposePlate(screen, "CP_BACKGROUND", layers.Backdrop);
        _chrome.ComposePageTitle(screen, "CP_T_TITLE", "CONTROLS", layers.Lines);
        var lines = layers.Lines;
        if (screen.Widget("CP_T_JoystickTitle") is { } title)
        {
            lines.Add(new BoardLine("Player", title.Int("X", (int)TitleX), title.Int("Y"),
                title.Int("Width", (int)TitleWidth), OriginalOptionsChrome.TitleFont, BoardInk.Row));
        }

        if (screen.Widget("CP_T_JoystickDesc") is { } description)
        {
            lines.Add(new BoardLine(
                "Whose keymap KEYS AND BUTTONS edits. Each seat holds its own.",
                description.Int("X", (int)DescX), description.Int("Y"),
                description.Int("Width", (int)DescWidth), OriginalOptionsChrome.DescriptionFont, BoardInk.Row));
        }

        if (screen.Widget("CP_T_MouseTitle") is { } mouseTitle)
        {
            // The title stops where the scheme chooser on its line starts.
            float titleX = mouseTitle.Int("X", (int)TitleX);
            float titleWidth = Math.Min(mouseTitle.Int("Width", (int)TitleWidth), MouseBox(screen).X - titleX);
            lines.Add(new BoardLine("Mouse", titleX, mouseTitle.Int("Y", (int)MouseTitleY),
                Math.Max(1f, titleWidth), OriginalOptionsChrome.TitleFont, BoardInk.Row));
        }

        if (screen.Widget("CP_T_MouseDesc") is { } mouseDescription)
        {
            lines.Add(new BoardLine(
                "Fly steers the aeroplane with the mouse, Look turns the head. The slider sets how "
                + "fast a flying mouse moves the stick.",
                mouseDescription.Int("X", (int)DescX),
                mouseDescription.Int("Y", (int)MouseDescY),
                mouseDescription.Int("Width", (int)DescWidth), OriginalOptionsChrome.DescriptionFont, BoardInk.Row));
        }

        if (screen.Widget("CP_T_KeysDesc") is { } keys)
        {
            lines.Add(new BoardLine(OriginalOptionsChrome.Untagged(keys.Text ?? string.Empty),
                keys.Int("X"), keys.Int("Y"), keys.Int("Width"), OriginalOptionsChrome.DescriptionFont, BoardInk.Row));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Kind == OriginalRowKind.Button && row.Art != null)
            {
                layers.Plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i,
                    row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, i == focus, i == _host.PressedRow) : 0,
                    string.Empty, BoardInk.LabelNormal));
                continue;
            }

            _host.ComposeGenericRow(row, i == focus, i == _host.PressedRow, i, layers);
        }
    }

    // The seat chooser at the Controller Type dropdown's own box.
    private static (float X, float Y, float Width, float Height) PlayerBox(MenuLayoutScreen screen)
    {
        var drop = screen.Widget(PlayerKey);
        return (
            drop?.Int("X", (int)DropX) ?? DropX,
            drop?.Int("Y", (int)DropY) ?? DropY,
            drop?.Int("Width", (int)DropWidth) ?? DropWidth,
            drop?.Int("ItemHeight", (int)ItemHeight) ?? ItemHeight);
    }

    // The scheme chooser, on the Mouse Sensitivity title's own line, since the authored slider keeps
    // the panel's only other line. It takes the right half of the Controller Type box's column at
    // that box's item height. Its arrow then ends where the seat row's does, and the panel's title
    // keeps the left half. It stops short of the slider's press region, which reaches ten pixels
    // above the slot.
    private (float X, float Y, float Width, float Height) MouseBox(MenuLayoutScreen screen)
    {
        var column = PlayerBox(screen);
        float y = screen.Widget("CP_T_MouseTitle")?.Int("Y", (int)MouseTitleY) ?? MouseTitleY;
        float half = column.Width / 2f;
        float sliderTop = _chrome.SliderBox(screen.Widget(SensitivityKey), MouseX, MouseY).Y;
        return (column.X + half, y, half, Math.Max(1f, Math.Min(column.Height, sliderTop - y)));
    }

    private string PlayerWord() => _controls is { } controls && controls.Players.Count > 0
        ? "Player " + controls.Player.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    private string MouseWord() => !MouseSchemeLive ? string.Empty
        : _controls!.MouseFlying ? "Fly" : "Look";

    private string SensitivityWord() => !MouseSchemeLive ? string.Empty
        : "Sensitivity " + SensitivityScale.Label(_controls!.MouseSensitivity);

    // The seat chooser steps to the next registered player with wrap. A lone seat has nobody to
    // step to, and the row stays where it is.
    private bool StepPlayer(int direction)
    {
        if (_controls is not { } controls || controls.Players.Count < 2)
        {
            return false;
        }

        var players = controls.Players;
        int at = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == controls.Player)
            {
                at = i;
            }
        }

        int next = ((at + direction) % players.Count + players.Count) % players.Count;
        controls.Player = players[next];
        _host.FocusKey(PlayerKey);
        return true;
    }

    // The scheme has two values, so a step either way is the other one. Staged like every other edit
    // on this page: ACCEPT CHANGES is what reaches the seat, and CANCEL CHANGES puts it back.
    private bool StepMouseScheme()
    {
        if (_controls is not { } controls || !MouseSchemeLive)
        {
            return false;
        }

        controls.MouseFlying = !controls.MouseFlying;
        _host.FocusKey(MouseKey);
        return true;
    }

    private bool StepSensitivity(int direction)
    {
        if (_controls is not { } controls || !MouseSchemeLive)
        {
            return false;
        }

        controls.MouseSensitivity = SensitivityScale.Step(controls.MouseSensitivity, direction);
        _host.FocusKey(SensitivityKey);
        return true;
    }
}

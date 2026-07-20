using System;
using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.UI;

/// <summary>
/// The static viewer's livery lab (<c>--viewer</c>, 2026-07-20): an interactive editor for
/// one aircraft's <see cref="PaintScheme"/> — pattern, the three paint colours as RGB
/// sliders, and the three decal slots — repainting the parked plane live.
///
/// It exists because the paint region table is hand-authored rather than extracted (see
/// <c>docs/formats/paint.md</c>): checking a colour against the original means seeing it on
/// the model, and dialling one in through <c>--paint-color=</c> restarts the viewer for every
/// guess. Every edit calls <see cref="PlaneBuilder.Repaint"/>, which re-resolves the built
/// materials in place — a few ms for an aircraft's ~8 small skins, so slider drags stay
/// interactive where a full model rebuild would not.
///
/// "copy CLI args" writes the current livery to the clipboard as the exact
/// <c>--paint…</c> arguments that reproduce it, which is how a livery found here becomes a
/// scripted screenshot. L toggles the panel (DamageLab owns H), so the two labs can be open
/// together and either can be hidden for a clean F12 shot.
/// </summary>
public sealed partial class LiveryLab : Node
{
    private const int DecalCount = 50; // the gapless 00-49 set every chapter ships

    private readonly PlaneBuilder _builder;
    private readonly IReadOnlyList<PaintScheme> _catalog;
    private readonly TextureArchive _textures;
    private readonly RandomNumberGenerator _rng = new();

    // The live scheme. Null = unpainted (the shipped skins), which is what a --viewer
    // launch starts on unless --paint named something, so existing screenshots are unchanged.
    private PaintScheme? _scheme;
    private int _patternIndex = -1;

    private CanvasLayer _ui = null!;
    private Label _patternLabel = null!;
    private Label _cliLabel = null!;
    private readonly ColorRect[] _swatches = new ColorRect[3];
    private readonly HSlider[,] _rgb = new HSlider[3, 3];
    private readonly Label[] _decalLabels = new Label[3];
    private CheckButton _paintedToggle = null!;
    private bool _suppressCallbacks; // set while rewriting widgets from a scheme change

    /// <summary>--debug-livery[=N]: open the panel at launch (it is hidden by default so an
    /// unadorned --viewer screenshot stays byte-identical) and, with N, step the pattern N
    /// times first — so one scripted screenshot exercises the stepper, the repaint and the
    /// widget sync, not just the layout. Same role as --debug-scoreboard / --debug-join.</summary>
    public bool DebugShow { get; init; }
    public int DebugPatternSteps { get; init; }

    public LiveryLab(PlaneBuilder builder, IReadOnlyList<PaintScheme> catalog,
        TextureArchive textures, PaintScheme? initial)
    {
        _builder = builder;
        _catalog = catalog;
        _textures = textures;
        _scheme = initial;
        _rng.Randomize();
        if (initial != null)
            _patternIndex = IndexOfPattern(initial.Pattern);
        Name = "livery_lab";
    }

    public override void _Ready()
    {
        BuildUi();
        // The lab is the single owner of the livery in --viewer: PlaneViewer builds the model
        // bare and the initial scheme is applied HERE, through the same Repaint every slider
        // uses. So `--viewer --paint=X --screenshot` exercises the repaint path end to end —
        // if Repaint broke, that shot would show an unpainted plane. An unpainted viewer
        // (Repaint(null)) hands back the archive's own textures, so it stays byte-identical.
        Apply();

        if (DebugPatternSteps != 0)
        {
            SelectPattern(DebugPatternSteps);
            GD.Print($"[livery] --debug-livery stepped {DebugPatternSteps} → {CliArgs()}");
        }
        if (DebugShow)
            _ui.Visible = true;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.L })
            _ui.Visible = !_ui.Visible;
    }

    private int IndexOfPattern(string pattern)
    {
        for (int i = 0; i < _catalog.Count; i++)
            if (string.Equals(_catalog[i].Pattern, pattern, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    // ---- edits -------------------------------------------------------------------------

    /// <summary>Loads a shipped pattern wholesale — colours and decals — so the twelve can be
    /// stepped through and compared against the original.</summary>
    private void SelectPattern(int delta)
    {
        if (_catalog.Count == 0)
            return;
        _patternIndex = Mathf.PosMod(_patternIndex + delta, _catalog.Count);
        var src = _catalog[_patternIndex];
        _scheme = new PaintScheme
        {
            Pattern = src.Pattern,
            Color1 = src.Color1,
            Color2 = src.Color2,
            Color3 = src.Color3,
            NoseDecal = src.NoseDecal,
            TailDecal = src.TailDecal,
            WingDecal = src.WingDecal,
        };
        Apply();
    }

    private void SetColor(int slot, Color c)
    {
        if (_scheme == null)
            return;
        switch (slot)
        {
            case 0: _scheme.Color1 = c; break;
            case 1: _scheme.Color2 = c; break;
            default: _scheme.Color3 = c; break;
        }
        // A hand-edited colour is no longer the named pattern's, so the label says so.
        Apply();
    }

    private void StepDecal(int slot, int delta)
    {
        if (_scheme == null)
            return;
        int cur = slot switch { 0 => _scheme.NoseDecal, 1 => _scheme.TailDecal, _ => _scheme.WingDecal };
        int next = Mathf.PosMod(cur + delta, DecalCount);
        switch (slot)
        {
            case 0: _scheme.NoseDecal = next; break;
            case 1: _scheme.TailDecal = next; break;
            default: _scheme.WingDecal = next; break;
        }
        Apply();
    }

    private void Randomize()
    {
        _scheme = PaintScheme.Random(_rng, _catalog);
        _patternIndex = IndexOfPattern(_scheme.Pattern);
        Apply();
    }

    private void SetPainted(bool on)
    {
        if (on && _scheme == null)
        {
            _patternIndex = 0;
            SelectPattern(0);
            return;
        }
        if (!on)
            _scheme = null;
        Apply();
    }

    /// <summary>Repaints the model and refreshes the panel. The single write path.</summary>
    private void Apply()
    {
        _builder.Repaint(_scheme);
        SyncWidgets();
    }

    // ---- panel -------------------------------------------------------------------------

    private void SyncWidgets()
    {
        _suppressCallbacks = true;
        bool painted = _scheme != null;
        _paintedToggle.ButtonPressed = painted;
        _patternLabel.Text = !painted
            ? "unpainted — shipped skin"
            : $"{_scheme!.Label}{(_patternIndex >= 0 ? $"   [{_patternIndex + 1}/{_catalog.Count}]" : "   (edited)")}";

        for (int slot = 0; slot < 3; slot++)
        {
            var c = painted ? _scheme!.ColorFor(slot) : new Color(0.35f, 0.35f, 0.35f);
            _swatches[slot].Color = c;
            _rgb[slot, 0].Value = Mathf.Round(c.R * 255f);
            _rgb[slot, 1].Value = Mathf.Round(c.G * 255f);
            _rgb[slot, 2].Value = Mathf.Round(c.B * 255f);
            _decalLabels[slot].Text = painted ? DecalText(slot) : "—";
        }
        _cliLabel.Text = CliArgs();
        _suppressCallbacks = false;
    }

    private string DecalText(int slot)
    {
        int index = slot switch { 0 => _scheme!.NoseDecal, 1 => _scheme!.TailDecal, _ => _scheme!.WingDecal };
        var name = _textures.FindByDecalIndex(index);
        return name != null ? $"{index:00}  {name}" : $"{index:00}  (absent)";
    }

    /// <summary>The exact CLI arguments that reproduce the current livery — the lab's output.
    /// A pattern is emitted only when the colours and decals still match it verbatim;
    /// otherwise the explicit colour/decal overrides carry the whole scheme.</summary>
    private string CliArgs()
    {
        if (_scheme == null)
            return "--paint=none";
        var s = _scheme;
        bool clean = _patternIndex >= 0 && SameAs(_catalog[_patternIndex], s);
        if (clean)
            return $"--paint={s.Pattern}";
        string pattern = _patternIndex >= 0 ? $"--paint={s.Pattern} " : "";
        return pattern
            + $"--paint-color={Rgb(s.Color1)}/{Rgb(s.Color2)}/{Rgb(s.Color3)} "
            + $"--paint-decal={s.NoseDecal},{s.TailDecal},{s.WingDecal}";
    }

    private static bool SameAs(PaintScheme a, PaintScheme b) =>
        a.Color1 == b.Color1 && a.Color2 == b.Color2 && a.Color3 == b.Color3
        && a.NoseDecal == b.NoseDecal && a.TailDecal == b.TailDecal && a.WingDecal == b.WingDecal;

    private static string Rgb(Color c) =>
        $"{Mathf.RoundToInt(c.R * 255f)},{Mathf.RoundToInt(c.G * 255f)},{Mathf.RoundToInt(c.B * 255f)}";

    private void BuildUi()
    {
        // Starts HIDDEN: an unadorned --viewer must render the same clean orbit the pre-paint
        // viewer did, so every scripted screenshot stays deterministic (and byte-identical).
        // L brings the panel up. The livery itself still applies whether or not it is shown.
        _ui = new CanvasLayer { Layer = 1, Visible = false };
        // Anchored top-RIGHT so it never collides with the damage lab's top-left panel —
        // --viewer --damage opens both at once.
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.85f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.Position = new Vector2(-8, 8);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 10);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        box.AddChild(new Label { Text = "LIVERY LAB" });
        box.AddChild(Small("L hides this panel · H the damage lab"));

        _paintedToggle = new CheckButton { Text = "painted" };
        _paintedToggle.Toggled += on => { if (!_suppressCallbacks) SetPainted(on); };
        box.AddChild(_paintedToggle);

        // pattern stepper
        _patternLabel = new Label();
        var patternRow = new HBoxContainer();
        patternRow.AddChild(StepButton("<", () => SelectPattern(-1)));
        _patternLabel.CustomMinimumSize = new Vector2(230, 0);
        _patternLabel.VerticalAlignment = VerticalAlignment.Center;
        patternRow.AddChild(_patternLabel);
        patternRow.AddChild(StepButton(">", () => SelectPattern(1)));
        box.AddChild(patternRow);

        box.AddChild(Separator());

        // three colour slots: swatch + R/G/B
        string[] slotNames = { "1  body", "2  dark trim", "3  light trim" };
        for (int slot = 0; slot < 3; slot++)
        {
            int captured = slot;
            var header = new HBoxContainer();
            _swatches[slot] = new ColorRect { CustomMinimumSize = new Vector2(26, 14) };
            header.AddChild(_swatches[slot]);
            header.AddChild(new Label { Text = "  colour " + slotNames[slot] });
            box.AddChild(header);

            var row = new HBoxContainer();
            for (int ch = 0; ch < 3; ch++)
            {
                int cc = ch;
                row.AddChild(Small("RGB"[ch].ToString()));
                var slider = new HSlider
                {
                    MinValue = 0,
                    MaxValue = 255,
                    Step = 1,
                    CustomMinimumSize = new Vector2(78, 0),
                };
                slider.ValueChanged += _ =>
                {
                    if (_suppressCallbacks || _scheme == null)
                        return;
                    var c = _scheme.ColorFor(captured);
                    float v = (float)_rgb[captured, cc].Value / 255f;
                    SetColor(captured, cc switch
                    {
                        0 => new Color(v, c.G, c.B),
                        1 => new Color(c.R, v, c.B),
                        _ => new Color(c.R, c.G, v),
                    });
                };
                _rgb[slot, ch] = slider;
                row.AddChild(slider);
            }
            box.AddChild(row);
        }

        box.AddChild(Separator());

        // three decal slots
        string[] decalNames = { "nose", "tail", "wing" };
        for (int slot = 0; slot < 3; slot++)
        {
            int captured = slot;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = decalNames[slot].PadRight(5) });
            row.AddChild(StepButton("<", () => StepDecal(captured, -1)));
            _decalLabels[slot] = new Label
            {
                CustomMinimumSize = new Vector2(190, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            row.AddChild(_decalLabels[slot]);
            row.AddChild(StepButton(">", () => StepDecal(captured, 1)));
            box.AddChild(row);
        }
        box.AddChild(Small("00–20 squadron logos · 21–49 nose art"));

        box.AddChild(Separator());

        var random = new Button { Text = "random livery" };
        random.Pressed += Randomize;
        box.AddChild(random);

        _cliLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _cliLabel.CustomMinimumSize = new Vector2(300, 0);
        _cliLabel.AddThemeFontSizeOverride("font_size", 11);
        box.AddChild(_cliLabel);

        var copy = new Button { Text = "copy CLI args" };
        copy.Pressed += () =>
        {
            var args = CliArgs();
            DisplayServer.ClipboardSet(args);
            GD.Print($"[livery] {args}");
        };
        box.AddChild(copy);

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _ui.AddChild(root);
        AddChild(_ui);
    }

    private static Button StepButton(string text, Action pressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(28, 0) };
        b.Pressed += pressed;
        return b;
    }

    private static HSeparator Separator() => new();

    private static Label Small(string text)
    {
        var label = new Label { Text = text, Modulate = new Color(1, 1, 1, 0.65f) };
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }
}

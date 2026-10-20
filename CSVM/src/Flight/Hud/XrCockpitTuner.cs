using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Bindings;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>The seat the tuning lab tunes this frame: the flown stereo interior and its plane node.
/// <c>Silenced</c> says a pause or the photo mode holds the controls.</summary>
public readonly record struct XrTuneSeat(XrCockpit Cockpit, string Plane, bool Silenced);

/// <summary>
/// The <c>--xr-tune</c> lab: live nudges of the flown plane's VR interior scale, eye offset and
/// pylon stores, with a readout on the HUD panel. A save writes the plane's values back into
/// <c>CSVM/data/vr_cockpits.json</c> on disk. The right grip holds the interior and eye controls, the
/// left the stores', so nothing reads while both are up. F22 to F24 are the keyboard's twin for all
/// but the stores. A step swaps the next plane in the file's order, and a plane flown again takes
/// the values it was left with. Writer: <see cref="VrCockpitTuning"/>.
/// </summary>
public sealed partial class XrCockpitTuner : Node
{
    // The readout on the panel's 2560x1440 surface, about 31 rows per degree: 28 px text is near a
    // degree tall. The top left corner keeps it off the gunsight and the forward view being judged.
    private const int ReadoutFontPx = 28;
    private const float ReadoutPad = 14f;
    private const float ReadoutTop = 24f;
    private const float ReadoutLeft = 160f;

    // How long a key keeps the full card up after the keyboard's last press, in seconds.
    private const float KeyCardS = 4f;

    private readonly Dictionary<string, VrCockpit> _working = new(StringComparer.OrdinalIgnoreCase);
    private readonly VrTuneRepeat _scale = new();
    private readonly VrTuneRepeat _aft = new();
    private readonly VrTuneRepeat _up = new();
    private readonly VrTuneRepeat _plane = new(repeats: false);
    private IDeviceState _hands = null!;
    private Func<XrTuneSeat?> _seat = null!;
    private bool _silenced;
    private Action<string>? _swapTo;
    private string? _filePath;
    private VrCockpits _file = VrCockpits.Defaults;
    private Label _label = null!;
    private XrCockpit? _cockpit;
    private bool _saveWas;
    private bool _resetWas;
    private VrTuneKnob _selected = VrTuneKnob.Scale;
    private string _status = string.Empty;
    // The full card shows while the grip is held or a key was pressed lately; else one line.
    private bool _expanded;
    private float _keyCard;

    /// <summary>The flown plane's node name, or null while no stereo interior is flown.</summary>
    public string? Plane { get; private set; }

    /// <summary>The flown plane's values as nudged, or null while no stereo interior is flown.
    /// </summary>
    public VrCockpit? Current => Plane is { } plane ? Working(plane) : null;

    /// <summary>The headset readout's text.</summary>
    public string Readout => _label.Text;

    /// <summary>The disk file a save writes, or null where there is none (an exported build).
    /// </summary>
    public string? FilePath => _filePath;

    /// <summary>Builds the lab. Its readout goes on a layer under <paramref name="readoutParent"/>,
    /// the HUD panel's surface. Each frame, <paramref name="seat"/> reads the flown seat. The plane
    /// step calls <paramref name="swapTo"/>. The controllers default to the live ones and the file to
    /// the committed one on disk (<paramref name="hands"/>, <paramref name="filePath"/>).</summary>
    public static XrCockpitTuner Build(Node readoutParent, Func<XrTuneSeat?> seat, Action<string>? swapTo,
        IDeviceState? hands = null, string? filePath = null)
    {
        var tuner = new XrCockpitTuner
        {
            Name = "xr_cockpit_tuner",
            _hands = hands ?? XrControllers.Live,
            _seat = seat,
            _swapTo = swapTo,
        };
        string why = string.Empty;
        tuner._filePath = filePath ?? DiskPath(out why);
        tuner._file = tuner._filePath != null ? VrCockpits.Load(tuner._filePath) : VrCockpits.Load();
        if (tuner._filePath == null)
        {
            tuner._status = $"cannot save: {why}";
        }

        var layer = new CanvasLayer { Name = "xr_tune_readout", Layer = HudLayers.Lab };
        var backing = new ColorRect
        {
            Name = "xr_tune_backing",
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        tuner._label = new Label
        {
            Name = "xr_tune_label",
            Position = new Vector2(ReadoutPad, ReadoutPad),
            LabelSettings = new LabelSettings { FontSize = ReadoutFontPx, OutlineSize = 6, OutlineColor = Colors.Black },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backing.AddChild(tuner._label);
        layer.AddChild(backing);
        readoutParent.AddChild(layer);
        tuner.TreeExiting += () =>
        {
            if (GodotObject.IsInstanceValid(layer))
            {
                layer.QueueFree();
            }
        };
        tuner.Refresh();
        Log.Info("flight", $"xr tune: on, {tuner._file.Names.Count} plane(s) from {tuner._filePath ?? VrCockpits.DefaultPath}, saving {(tuner._filePath != null ? "to that file" : "disabled")}");
        return tuner;
    }

    /// <summary>What the file holds for <paramref name="plane"/>, as last read or saved.</summary>
    public VrCockpit Saved(string plane) => _file.For(plane);

    public override void _Process(double delta) => Poll((float)delta);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Keycode: Key.F22 or Key.F23 or Key.F24 } key
            || Plane == null || _silenced)
        {
            return;
        }

        _keyCard = KeyCardS;
        _expanded = true;
        switch (key.Keycode)
        {
            case Key.F22:
                StepSelected(-1, key.Echo);
                break;
            case Key.F23:
                StepSelected(1, key.Echo);
                break;
            case Key.F24 when !key.Echo && key.ShiftPressed:
                Save();
                break;
            case Key.F24 when !key.Echo && key.CtrlPressed:
                Reset();
                break;
            case Key.F24 when !key.Echo:
                _selected = (VrTuneKnob)(((int)_selected + 1) % 4);
                Refresh();
                break;
            default:
                Refresh();
                break;
        }

        GetViewport()?.SetInputAsHandled();
    }

    /// <summary>One frame: follows the flown seat to a new aircraft, then reads the controllers while
    /// either grip is held. <see cref="_Process"/>'s body; a suite calls it with its own step.</summary>
    public void Poll(float dt)
    {
        Follow();
        float Axis(XrAxis axis) => _hands.AxisValue(DeviceId.Xr, (int)axis);
        bool Button(XrButton button) => _hands.IsButtonDown(DeviceId.Xr, (int)button);

        // The left grip, which no flight action holds, turns the right hand's nudges onto the stores.
        bool stores = Button(XrButton.LeftGrip);
        bool grip = Plane != null && !_silenced && (stores || Button(XrButton.RightGrip));
        _keyCard = Math.Max(0f, _keyCard - dt);
        if ((grip || _keyCard > 0f) != _expanded)
        {
            _expanded = !_expanded;
            Refresh();
        }

        if (!grip)
        {
            _scale.Clear();
            _aft.Clear();
            _up.Clear();
            _plane.Clear();

            // A button already held as the grip closes is no press.
            _saveWas = Button(XrButton.RightTrigger);
            _resetWas = Button(XrButton.RightStickClick);
            return;
        }

        Nudge(stores ? VrTuneKnob.StoreSpread : VrTuneKnob.Scale, _scale.Step(Axis(XrAxis.RightStickX), dt));

        // The stick pushed forward moves the eye forward, toward the panel: a smaller aft offset.
        Nudge(stores ? VrTuneKnob.StoreAft : VrTuneKnob.Aft, _aft.Step(-Axis(XrAxis.RightStickY), dt));
        Nudge(stores ? VrTuneKnob.StoreUp : VrTuneKnob.Up, _up.Step((Button(XrButton.RightUpperFace) ? 1f : 0f) - (Button(XrButton.RightLowerFace) ? 1f : 0f), dt));
        bool save = Button(XrButton.RightTrigger);
        bool reset = Button(XrButton.RightStickClick);
        if (save && !_saveWas)
        {
            Save();
        }

        if (reset && !_resetWas)
        {
            Reset();
        }

        _saveWas = save;
        _resetWas = reset;
        if (_plane.Step(Axis(XrAxis.LeftStickX), dt) is var step && step != 0)
        {
            CyclePlane(step);
        }
    }

    /// <summary>Moves the flown plane's <paramref name="knob"/> by <paramref name="steps"/> steps and
    /// applies it to the interior at once. A plane knob swaps the airframe instead.</summary>
    public void Nudge(VrTuneKnob knob, int steps)
    {
        if (steps == 0 || Plane is not { } plane)
        {
            return;
        }

        if (knob == VrTuneKnob.Plane)
        {
            CyclePlane(steps);
            return;
        }

        var was = Working(plane);
        var now = VrCockpitTuning.Step(was, knob, steps);
        _status = now == was ? $"{knob} is at the file's limit" : string.Empty;
        _working[plane] = now;
        Apply();
        Log.Info("flight", $"xr tune: {VrCockpitTuning.Describe(plane, now)}{(now == Saved(plane) ? string.Empty : " (unsaved)")}");
        Refresh();
    }

    /// <summary>Writes the flown plane's values into the file on disk, keeping every other byte. A
    /// failure is logged and shown, and the file is left as it was.</summary>
    public bool Save()
    {
        if (Plane is not { } plane)
        {
            return false;
        }

        var entry = Working(plane);
        bool saved = TrySave(plane, entry, out string why);
        _status = saved ? $"saved {plane} to {Path.GetFileName(_filePath)}" : $"save failed: {why}";
        if (saved)
        {
            Log.Info("flight", $"xr tune: saved {VrCockpitTuning.Describe(plane, entry)} to {_filePath}");
        }
        else
        {
            Log.Warn("flight", $"xr tune: {plane} not saved, {why}");
        }

        Refresh();
        return saved;
    }

    /// <summary>Puts the flown plane back on the file's values.</summary>
    public void Reset()
    {
        if (Plane is not { } plane)
        {
            return;
        }

        _working.Remove(plane);
        Apply();
        _status = "reset to the file";
        Log.Info("flight", $"xr tune: reset {VrCockpitTuning.Describe(plane, Working(plane))}");
        Refresh();
    }

    /// <summary>Swaps the flown airframe for the plane <paramref name="direction"/> steps along the
    /// file's order. The nudged values of the plane left stay for when it is flown again.</summary>
    public void CyclePlane(int direction)
    {
        if (_swapTo == null || VrCockpitTuning.NextPlane(_file.Names, Plane, direction) is not { } index)
        {
            return;
        }

        string next = _file.Names[index];
        Log.Info("flight", $"xr tune: swapping {Plane ?? "nothing"} for {next}");
        _swapTo(next);
    }

    // The disk file res://data/vr_cockpits.json maps to, or null where it is no file on disk.
    private static string? DiskPath(out string why)
    {
        string path = ProjectSettings.GlobalizePath(VrCockpits.DefaultPath);
        why = File.Exists(path) ? string.Empty : $"{VrCockpits.DefaultPath} is no file on disk here";
        return why.Length == 0 ? path : null;
    }

    private VrCockpit Working(string plane) => _working.TryGetValue(plane, out var entry) ? entry : _file.For(plane);

    private void StepSelected(int direction, bool echo)
    {
        if (_selected == VrTuneKnob.Plane)
        {
            if (!echo)
            {
                CyclePlane(direction);
            }

            return;
        }

        Nudge(_selected, direction);
    }

    // A new stereo interior under the flown seat: a swap or the first frame. It takes the values the
    // plane was left with, since the build baked the file's.
    private void Follow()
    {
        var seat = _seat();
        _silenced = seat?.Silenced ?? false;
        var cockpit = seat?.Cockpit;
        if (ReferenceEquals(cockpit, _cockpit))
        {
            return;
        }

        _cockpit = cockpit;
        Plane = seat?.Plane;
        if (_filePath != null)
        {
            _status = string.Empty;
        }

        if (Plane != null)
        {
            Apply();
            Log.Info("flight", $"xr tune: flying {VrCockpitTuning.Describe(Plane, Working(Plane))}");
        }

        Refresh();
    }

    private void Apply()
    {
        if (_cockpit == null || Plane is not { } plane)
        {
            return;
        }

        var entry = Working(plane);
        _cockpit.EyeOffset = entry.EyeOffsetInAircraft;
        _cockpit.SetMountScale(entry.InteriorScale);
        _cockpit.PlaceStores(entry.StoreOffsetInAircraft, entry.StoreSpread);
    }

    private bool TrySave(string plane, VrCockpit entry, out string why)
    {
        if (_filePath == null)
        {
            DiskPath(out why);
            return false;
        }

        try
        {
            byte[] before = File.ReadAllBytes(_filePath);
            if (!VrCockpitTuning.TryRewrite(before, plane, entry, out byte[] after, out why))
            {
                return false;
            }

            // A whole new file moved over the old, so a failed write never leaves half of one.
            string temp = _filePath + ".tmp";
            File.WriteAllBytes(temp, after);
            File.Move(temp, _filePath, overwrite: true);
            _file = VrCockpits.Parse(after, new List<string>());
            _working.Remove(plane);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            why = ex.Message;
            return false;
        }
    }

    private void Refresh()
    {
        if (Plane is not { } plane)
        {
            _label.Text = "VR cockpit tune: no stereo cockpit is flown" + (_status.Length > 0 ? "\n" + _status : string.Empty);
        }
        else
        {
            int index = 0;
            for (int i = 0; i < _file.Names.Count; i++)
            {
                if (string.Equals(_file.Names[i], plane, StringComparison.OrdinalIgnoreCase))
                {
                    index = i + 1;
                }
            }

            _label.Text = _expanded
                ? VrCockpitTuning.Readout(plane, index, _file.Names.Count, Working(plane), Saved(plane), _selected, _status)
                : VrCockpitTuning.Summary(plane, index, _file.Names.Count, Working(plane), Saved(plane), _status);
        }

        if (_label.GetParent() is ColorRect backing)
        {
            backing.Size = _label.GetMinimumSize() + new Vector2(2f * ReadoutPad, 2f * ReadoutPad);
            backing.Position = new Vector2(ReadoutLeft, ReadoutTop);
        }
    }
}

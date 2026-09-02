using System.Collections.Generic;

namespace CSVM.Bindings;

/// <summary>The live joypad index-to-identity table. A connection index is a slot Godot reuses the
/// instant a pad unplugs, so nothing may cache one past a <see cref="Refresh"/>; a binding stays
/// keyed on the stable <see cref="DeviceId"/> instead and looks its current index up here every
/// time. Pure and engine-free: <see cref="Refresh"/> takes already-read tuples, so the mapping rule
/// is testable without a joypad (<c>CSVM.Tests/DeviceRegistryTests.cs</c>). Reading those tuples off
/// Godot's <c>Input</c> singleton is <see cref="GodotDeviceState"/>'s job.</summary>
public sealed class DeviceRegistry
{
    private readonly Dictionary<string, int> _indexById = new();
    private readonly Dictionary<int, DeviceId> _idByIndex = new();

    /// <summary>Rebuilds the live table from this tick's connected pads. Call once at launch and
    /// again on every <c>joy_connection_changed</c> signal; a table left stale after an unplug is
    /// the reused-index bug this type exists to prevent. The GUID-before-name preference and the
    /// same-GUID collision rule are in <c>docs/architecture.md</c> under this file.</summary>
    public void Refresh(IReadOnlyList<(int Index, string Guid, string Name)> connected)
    {
        _indexById.Clear();
        _idByIndex.Clear();
        foreach (var (index, guid, name) in connected)
        {
            string stable = string.IsNullOrWhiteSpace(guid) ? name : guid;
            if (string.IsNullOrWhiteSpace(stable))
                continue;
            string trimmed = stable.Trim();
            if (_indexById.ContainsKey(trimmed))
                continue;
            _indexById[trimmed] = index;
            _idByIndex[index] = DeviceId.Joypad(trimmed);
        }
    }

    /// <summary>The live index for a joypad identity, or null when it is unplugged, was never seen,
    /// or lost a stable-id collision to another connected pad. Never call for
    /// <see cref="DeviceKind.Keyboard"/>: the keyboard has no index and this always answers null for
    /// it.</summary>
    public int? IndexOf(DeviceId device) =>
        device.Kind == DeviceKind.Joypad && _indexById.TryGetValue(device.Id, out int index) ? index : null;

    /// <summary>The identity this tick's roster gives a live index, or the default
    /// (<see cref="DeviceId.IsNone"/>) value if that index is not currently connected. Rebinding
    /// capture (D31) uses this to turn "the next input came from index 2" into the stable id worth
    /// saving.</summary>
    public DeviceId IdentityOf(int index) => _idByIndex.TryGetValue(index, out var id) ? id : default;
}

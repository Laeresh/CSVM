using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;

namespace CSVM.Sticks;

/// <summary>How a rebinding screen names a stick: its active profile's short name ("R", "L"). Without
/// one it is "Stick" and its model, so two unnamed sticks never read as one. Only the narrow Stick
/// column drops that prefix, and keeps the model when two unnamed sticks share a row
/// (<see cref="Columns(IReadOnlyList{Binding})"/>). Registered as
/// <see cref="BindingLabels.StickName"/> once per process, whether or not sticks are on, so a stick
/// row always reads as a stick.</summary>
public static class StickLabels
{
    /// <summary>Makes <see cref="BindingLabels"/> name stick identities through
    /// <see cref="Live"/>.</summary>
    public static void Register() => BindingLabels.StickName = Live;

    /// <summary>The caption prefix of <paramref name="device"/> from the live profiles, or null for
    /// a device that is no stick.</summary>
    public static string? Live(DeviceId device) => Prefix(device, LiveName);

    /// <summary>The Stick column's caption: the profile's short name and the control, or the control
    /// alone for an unnamed stick ("Button 5"). The column is half a panel wide, so the model is left
    /// out; <see cref="Columns(IReadOnlyList{Binding}, Func{StickModel, string?})"/> keeps it when a
    /// row needs it.</summary>
    public static string Column(Binding binding, Func<StickModel, string?> nameOf) =>
        Column(binding, nameOf, withModel: false);

    /// <summary>The Stick column's captions of one row's stick bindings, named from the live
    /// profiles.</summary>
    public static IReadOnlyList<string> Columns(IReadOnlyList<Binding> row) => Columns(row, LiveName);

    /// <summary>The Stick column's captions of one row's stick bindings, in the row's order. When two
    /// unnamed models share the row, each unnamed caption leads with its model ("231D/0200 Button 5"),
    /// since the control alone would read alike.</summary>
    public static IReadOnlyList<string> Columns(IReadOnlyList<Binding> row, Func<StickModel, string?> nameOf)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(nameOf);
        var unnamed = new HashSet<StickModel>();
        foreach (var binding in row)
        {
            if (StickModel.TryFromDevice(binding.Device, out var model) && string.IsNullOrWhiteSpace(nameOf(model)))
                unnamed.Add(model);
        }

        bool withModel = unnamed.Count > 1;
        return row.Select(binding => Column(binding, nameOf, withModel)).ToList();
    }

    /// <summary>The caption prefix of <paramref name="device"/> with its short name taken from
    /// <paramref name="nameOf"/>, or null for a device that is no stick. A blank name is no name.
    /// </summary>
    public static string? Prefix(DeviceId device, Func<StickModel, string?> nameOf)
    {
        ArgumentNullException.ThrowIfNull(nameOf);
        if (!StickModel.TryFromDevice(device, out var model))
            return null;
        string? name = nameOf(model);
        return string.IsNullOrWhiteSpace(name) ? "Stick " + model : name.Trim();
    }

    private static string Column(Binding binding, Func<StickModel, string?> nameOf, bool withModel)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(nameOf);
        string control = BindingLabels.StickControl(binding.Control);
        if (!StickModel.TryFromDevice(binding.Device, out var model))
            return control;
        string? name = nameOf(model);
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim() + " " + control;
        return withModel ? model + " " + control : control;
    }

    private static string? LiveName(StickModel model) => StickProfiles.Live?.ActiveFor(model)?.Name;
}

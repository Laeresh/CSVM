using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>One mission <c>pickups.zrd</c> proximity sensor and its radius in metres. Entering an
/// active sensor starts the shared <c>pickup_timing</c> choreography.</summary>
public readonly record struct PickupSpec(string Node, float Radius);

/// <summary>Reads a mission's compact pickup-sensor table.</summary>
public static class Pickups
{
    public const string FileName = "pickups.json";
    public const string TimingAnim = "pickup_timing";

    public static List<PickupSpec> Load(string missionZrdrPath)
    {
        var found = new List<PickupSpec>();
        List<object?> root;
        try
        {
            root = Zrdr.LoadFileOrEmpty(missionZrdrPath, FileName);
        }
        catch (Exception e) when (e is System.IO.IOException or System.Text.Json.JsonException)
        {
            return found;
        }
        foreach (var value in root)
        {
            Read(value, found);
        }
        return found;
    }

    private static void Read(object? value, List<PickupSpec> found)
    {
        if (value is not List<object?> list)
        {
            return;
        }
        if (list.Count == 2 && list[0] is string node && list[1] is float radius)
        {
            found.Add(new PickupSpec(node, radius));
            return;
        }
        foreach (var child in list)
        {
            Read(child, found);
        }
    }
}

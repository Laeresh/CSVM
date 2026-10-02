using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Session.World;

/// <summary>The per-view fog table: every live camera drawing the shared world, each with the fog
/// and world light of its own zone. It is published as the <c>csky_view_count</c> and
/// <c>csky_view_0</c>..<c>_7</c> globals that <c>shaders/csky_atmosphere.gdshaderinc</c> searches. A
/// fragment reads the entry whose eye stands nearest its own <c>CAMERA_POSITION_WORLD</c>. While
/// every view agrees the count is 0 and the shaders read the plain <c>csky_fog_*</c> globals.
/// Static, because its globals are one set for the process; <see cref="WeatherRig"/> fills it each
/// frame.</summary>
public static class FogViewTable
{
    /// <summary>The most views the table holds: four panes and one spyglass picture each.</summary>
    public const int MaxViews = 8;

    private const string CountParam = "csky_view_count";

    // One mat4 global per view, by column: the eye with the world light, the fog colour, then the
    // range and altitude pairs. The shader include reads the same layout.
    private static readonly StringName[] ViewParams = Names();
    private static readonly List<View> Published = new();
    private static int _lastCount;

    /// <summary>The views the shaders search this frame, empty while every view agrees. Read by
    /// the suite.</summary>
    public static IReadOnlyList<View> Views => Published;

    /// <summary>Registers the globals the atmosphere include declares. Called once per process,
    /// before any material using them is built. Count 0 leaves every shader on the plain
    /// globals.</summary>
    public static void RegisterGlobals()
    {
        RenderingServer.GlobalShaderParameterAdd(CountParam,
            RenderingServer.GlobalShaderParameterType.Int, 0);
        foreach (var name in ViewParams)
        {
            RenderingServer.GlobalShaderParameterAdd(name,
                RenderingServer.GlobalShaderParameterType.Mat4, Projection.Zero);
        }
    }

    /// <summary>The entry a fragment drawn from <paramref name="eye"/> reads, by the shader's own
    /// rule: the nearest eye, ties to the lower index. Pure, so a suite can say which view it
    /// expects a camera to match.</summary>
    public static int Nearest(IReadOnlyList<View> views, Vector3 eye)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < views.Count && i < MaxViews; i++)
        {
            float d = views[i].Eye.DistanceSquaredTo(eye);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Publishes this frame's views. Views that all carry one record publish nothing, so
    /// a single pane, and every pane in the same zone, render through the plain globals.</summary>
    public static void Publish(IReadOnlyList<View> views)
    {
        int n = Math.Min(views.Count, MaxViews);
        Published.Clear();
        if (Agree(views, n))
        {
            WriteCount(0);
            return;
        }

        for (int i = 0; i < n; i++)
        {
            var view = views[i];
            Published.Add(view);
            var fog = view.Fog;
            RenderingServer.GlobalShaderParameterSet(ViewParams[i], new Projection(
                new Vector4(view.Eye.X, view.Eye.Y, view.Eye.Z, fog.WorldLight),
                new Vector4(fog.ColorLinear.X, fog.ColorLinear.Y, fog.ColorLinear.Z, 0f),
                new Vector4(fog.Range.X, fog.Range.Y, fog.Altitude.X, fog.Altitude.Y),
                Vector4.Zero));
        }
        WriteCount(n);
    }

    /// <summary>Drops the table, called when a session is torn down and between suites, so the next
    /// world does not search the last one's cameras. Idempotent.</summary>
    public static void Clear()
    {
        Published.Clear();
        RenderingServer.GlobalShaderParameterSet(CountParam, 0);
        _lastCount = 0;
    }

    // Whether the first n views carry one record, the case the plain globals already draw.
    private static bool Agree(IReadOnlyList<View> views, int n)
    {
        for (int i = 1; i < n; i++)
        {
            if (views[i].Fog != views[0].Fog)
            {
                return false;
            }
        }
        return true;
    }

    private static StringName[] Names()
    {
        var names = new StringName[MaxViews];
        for (int i = 0; i < MaxViews; i++)
        {
            names[i] = "csky_view_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return names;
    }

    private static void WriteCount(int n)
    {
        if (_lastCount != n)
        {
            RenderingServer.GlobalShaderParameterSet(CountParam, n);
        }
        _lastCount = n;
    }

    /// <summary>One camera drawing the world: where it stands and the fog record its zone resolves
    /// to.</summary>
    public readonly record struct View(Vector3 Eye, WeatherRig.FogWritten Fog);
}

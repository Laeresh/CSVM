using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// What the resolved <see cref="AntiAliasingSetting"/> and <see cref="RenderScaleSetting"/> write on
/// a 3D viewport. Four viewports take them: the launcher's root one and the SubViewports of the
/// cockpit pass, the spyglass picture and the splitscreen panes.
/// Each calls <see cref="Apply"/> once at construction, after the resolvers have run. A live mode
/// switch or an Options apply calls <see cref="ReapplyAll"/>, which writes every live one again.
/// A later render setting is one more write here rather than a fifth edit at each site. MSAA is not
/// this module's: the project setting carries it, and the three SubViewports copy it
/// themselves.
/// </summary>
public static class ViewportQuality
{
    // Every viewport Apply wrote, so a live change can reach them all. Weak, since a pane or a pass
    // goes with its session and nothing here may keep it.
    private static readonly List<WeakReference<Viewport>> Applied = new();

    /// <summary>Put <paramref name="viewport"/> on the anti-aliasing method and the render scale the
    /// run resolved. ⚠ Nothing is written for <see cref="AntiAliasingMethod.Off"/> at
    /// <see cref="RenderScaleSetting.Native"/>, the faithful default. The pinned goldens are
    /// faithful <c>--det</c> runs, and they have to read back Godot's own defaults.</summary>
    public static void Apply(Viewport viewport)
    {
        // ⚠ A transparent viewport (the cockpit pass) takes neither. Godot's TAA and FSR resolves
        // write an opaque alpha, so the pass comes out black everywhere but the interior and hides
        // the world. It keeps the project's MSAA, which covers the interior's edges.
        if (viewport.TransparentBg)
            return;
        Track(viewport);
        var method = AntiAliasingSetting.Method;
        float scale = RenderScaleSetting.Scale;
        switch (method)
        {
            case AntiAliasingMethod.Fxaa:
                viewport.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa;
                break;
            case AntiAliasingMethod.Smaa:
                viewport.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Smaa;
                break;
            case AntiAliasingMethod.Taa:
                viewport.UseTaa = true;
                break;
        }

        // FSR 2.2 carries its own temporal pass, so it is a scaling mode rather than a flag, and at
        // native it runs as anti-aliasing alone. Its sharpness stays at Godot's own default.
        bool fsr2 = method == AntiAliasingMethod.Fsr2;
        if (scale < RenderScaleSetting.Native)
        {
            viewport.Scaling3DMode = fsr2 ? Viewport.Scaling3DModeEnum.Fsr2 : Viewport.Scaling3DModeEnum.Fsr;
            viewport.Scaling3DScale = scale;
        }
        else if (scale > RenderScaleSetting.Native)
        {
            // Bilinear is the one mode Godot supersamples in. The resolvers clamp FSR 2.2 to native,
            // so no method reaches here that this mode would silently drop.
            viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
            viewport.Scaling3DScale = scale;
        }
        else if (fsr2)
        {
            viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Fsr2;
            viewport.Scaling3DScale = RenderScaleSetting.Native;
        }
    }

    /// <summary>Puts every viewport <see cref="Apply"/> has written back on Godot's defaults, then
    /// on the settings resolved now. A method or a scale the run moved off leaves nothing behind,
    /// and the result is what a fresh <see cref="Apply"/> on a new viewport writes.</summary>
    public static void ReapplyAll()
    {
        var live = new List<Viewport>();
        for (int i = Applied.Count - 1; i >= 0; i--)
        {
            if (Applied[i].TryGetTarget(out var viewport) && GodotObject.IsInstanceValid(viewport))
                live.Add(viewport);
            else
                Applied.RemoveAt(i);
        }
        foreach (var viewport in live)
        {
            viewport.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled;
            viewport.UseTaa = false;
            viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
            viewport.Scaling3DScale = RenderScaleSetting.Native;
            Apply(viewport);
        }
    }

    // Remembered once, however often Apply writes it.
    private static void Track(Viewport viewport)
    {
        foreach (var weak in Applied)
        {
            if (weak.TryGetTarget(out var known) && known == viewport)
                return;
        }
        Applied.Add(new WeakReference<Viewport>(viewport));
    }
}

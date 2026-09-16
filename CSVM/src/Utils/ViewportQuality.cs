using Godot;

namespace CSVM.Utils;

/// <summary>
/// What the resolved <see cref="GraphicsMode"/>, <see cref="TemporalPassSetting"/> and
/// <see cref="RenderScaleSetting"/> write on a 3D viewport, in one place because there are four of
/// them: the root viewport the launcher owns, and the SubViewports the cockpit pass, the spyglass
/// picture and the splitscreen panes build. <see cref="Apply"/> is called once per viewport at
/// construction, after the resolvers have run; a later render setting is one more write here rather
/// than a fifth edit at each site. MSAA is not this module's: the project setting carries it for
/// both presentations, and the three SubViewports copy it themselves.
/// </summary>
public static class ViewportQuality
{
    /// <summary>Put <paramref name="viewport"/> on the presentation the mode resolved to, the temporal pass that mode
    /// picked, and the scale the render-scale setting resolved to. ⚠ Nothing is written at
    /// <see cref="RenderScaleSetting.Native"/> and nothing under the faithful mode: the pinned goldens are faithful
    /// <c>--det</c> runs, which drop the saved scale, so both have to read back Godot's own defaults. The temporal pass
    /// is the mode's rather than a display setting, so a deterministic run keeps what its launch mode gives it.</summary>
    public static void Apply(Viewport viewport)
    {
        // Supersampling only: Godot accepts a factor above 1 in bilinear mode alone, the FSR modes
        // being upscalers that refuse one. The scale is a display setting rather than the mode's,
        // so it is written whichever presentation the run is in.
        bool supersampling = RenderScaleSetting.Scale > RenderScaleSetting.Native;

        // ⚠ FSR 2.2 applies at native alone. Above it Godot would downsample bilinearly anyway, so
        // the scale the player asked for by name wins and the run keeps Godot's TAA, which leaves
        // it with a temporal pass rather than none.
        bool fsr2 = GraphicsMode.Enhanced && TemporalPassSetting.Fsr2 && !supersampling;

        // FSR 2.2 carries a temporal pass of its own and replaces Godot's rather than joining it.
        viewport.UseTaa = GraphicsMode.Enhanced && !fsr2;

        // The sharpness stays at Godot's own default, so the trial judges FSR 2.2 as it ships.
        if (fsr2)
        {
            viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Fsr2;
            viewport.Scaling3DScale = RenderScaleSetting.Native;
        }
        else if (supersampling)
        {
            viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
            viewport.Scaling3DScale = RenderScaleSetting.Scale;
        }
    }
}

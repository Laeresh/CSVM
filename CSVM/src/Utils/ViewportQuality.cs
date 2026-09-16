using Godot;

namespace CSVM.Utils;

/// <summary>
/// What the resolved <see cref="GraphicsMode"/> and <see cref="RenderScaleSetting"/> write on a 3D
/// viewport, in one place because there are four of them: the root viewport the launcher owns, and
/// the SubViewports the cockpit pass, the spyglass picture and the splitscreen panes build.
/// <see cref="Apply"/> is called once per viewport at construction, after both resolvers have run;
/// a later render setting is one more write here rather than a fifth edit at each site. MSAA is not
/// this module's: the project setting carries it for both presentations, and the three SubViewports
/// copy it themselves.
/// </summary>
public static class ViewportQuality
{
    /// <summary>Put <paramref name="viewport"/> on the presentation the mode resolved to and the
    /// scale the render-scale setting resolved to. ⚠ Nothing is written at
    /// <see cref="RenderScaleSetting.Native"/> and nothing under the faithful mode: the pinned
    /// goldens are faithful <c>--det</c> runs, which drop the saved scale, so both have to read
    /// back Godot's own defaults. Temporal anti-aliasing is the mode's rather than a display
    /// setting, so a deterministic run keeps whatever its launch mode gives it.</summary>
    public static void Apply(Viewport viewport)
    {
        viewport.UseTaa = GraphicsMode.Enhanced;

        // Supersampling only: Godot accepts a factor above 1 in bilinear mode alone, the FSR modes
        // being upscalers that refuse one. The scale is a display setting rather than the mode's,
        // so it is written whichever presentation the run is in.
        if (RenderScaleSetting.Scale > RenderScaleSetting.Native)
        {
            viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
            viewport.Scaling3DScale = RenderScaleSetting.Scale;
        }
    }
}

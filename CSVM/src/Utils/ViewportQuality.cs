using Godot;

namespace CSVM.Utils;

/// <summary>
/// What the resolved <see cref="GraphicsMode"/> writes on a 3D viewport, in one place because
/// there are four of them: the root viewport the launcher owns, and the SubViewports the cockpit
/// pass, the spyglass picture and the splitscreen panes build. <see cref="Apply"/> is called once
/// per viewport at construction, after <see cref="GraphicsMode.Resolve"/> has run; a later render
/// setting under the same switch is one more write here rather than a fifth edit at each site.
/// MSAA is not this module's: the project setting carries it for both presentations, and the three
/// SubViewports copy it themselves.
/// </summary>
public static class ViewportQuality
{
    /// <summary>Put <paramref name="viewport"/> on the presentation the mode resolved to.
    /// ⚠ Every flag written here stays under <see cref="GraphicsMode.Enhanced"/>: the faithful
    /// presentation is what the pinned goldens render, so it must read back Godot's own defaults.
    /// Temporal anti-aliasing is the mode's rather than a display setting, so a deterministic run
    /// keeps whatever the mode it was launched in gives it.</summary>
    public static void Apply(Viewport viewport)
    {
        viewport.UseTaa = GraphicsMode.Enhanced;
    }
}

using System.Collections.Generic;

namespace CSVM.UI.Menu;

/// <summary>
/// What the process-lifetime menu host lends the active presentation: the shared features, the
/// shared audio service, one input source per seat, and the single typed way out. The host owns
/// all four across presentation switches; a presentation borrows them between
/// <see cref="IMenuPresentation.Activate"/> and <see cref="IMenuPresentation.Deactivate"/> and
/// keeps no reference past that.
/// </summary>
public interface IMenuHost
{
    /// <summary>The shared feature set every presentation configures play through.</summary>
    MenuFeatureSet Features { get; }

    /// <summary>The shared audio service cues and narration are requested from.</summary>
    IMenuAudio Audio { get; }

    /// <summary>One menu input source per seat, seat 0 first. The list is live: the join flow
    /// grows it, so a presentation reads it each frame rather than copying it once.</summary>
    IReadOnlyList<IMenuInputSource> Seats { get; }

    /// <summary>Leaves the menu with one typed exit. The host hides the presentation and hands
    /// the exit to <c>Launcher</c>; the presentation does nothing further.</summary>
    void Exit(MenuExit exit);
}

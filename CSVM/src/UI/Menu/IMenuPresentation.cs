namespace CSVM.UI.Menu;

/// <summary>
/// One menu presentation: a screen graph plus its navigation, interaction, animation and cue
/// selection over the shared features, not just its drawing. The host creates a fresh instance
/// per activation through <see cref="PresentationRegistry"/>, so transient presentation state
/// cannot survive a switch by construction; the shared features' transient state is discarded
/// separately through <see cref="MenuFeatureSet.DiscardTransient"/>. A presentation offers the
/// features' operations however it likes and leaves only through <see cref="IMenuHost.Exit"/>.
/// </summary>
public interface IMenuPresentation
{
    /// <summary>The identity this presentation registered under.</summary>
    PresentationId Id { get; }

    /// <summary>Shows the presentation, standing on whatever screen of its own graph it maps
    /// <paramref name="destination"/> to. Called once on a cold start and again after every
    /// <see cref="Hide"/>; a switch always arrives with
    /// <see cref="MenuReturnDestination.TopLevel"/>. The host stays valid until
    /// <see cref="Deactivate"/>.</summary>
    void Activate(IMenuHost host, MenuReturnDestination destination);

    /// <summary>One frame while active: poll the host's seats, drive the graph, request cues.</summary>
    void Tick(float dt);

    /// <summary>Takes the presentation off screen after the host consumed its exit, keeping its
    /// state: a return from flight, or a failed build, calls <see cref="Activate"/> again on the
    /// same instance and lands on the screens as they were left. A flight is not a switch.</summary>
    void Hide();

    /// <summary>Tears down everything <see cref="Activate"/> built. The instance is discarded
    /// afterwards and never reactivated.</summary>
    void Deactivate();
}

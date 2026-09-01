namespace CSVM.UI.Menu;

/// <summary>
/// A seat's input source with no device behind it: every frame idle, labelled "no device". The
/// screenshot aid that seats extra players on a one-controller machine joins one of these per
/// seat, so the shot is deterministic and the seat still has a source to claim; each instance is
/// its own claim, since a claim is an identity.
/// </summary>
public sealed class MenuIdleSource : IMenuInputSource
{
    public string DeviceLabel => "no device";

    public bool CapturingText { get; set; }

    public MenuCommands Poll(float dt) => MenuCommands.None;

    public void Prime()
    {
    }
}

using CSVM.UI.Boards;
using CSVM.UI.Menu;

namespace CSVM.UI.Screens;

/// <summary>
/// A seat's input source over one <see cref="MenuInput"/>, the raw poller translated into one frame
/// of semantic commands. Seat 0's reads the keyboard plus every unclaimed pad, under a pointer seat
/// for the mouse. A joined seat's reads its one pad. The devices behind the poller stay bound by
/// <see cref="MenuSeatDevices"/>, through claiming, joining and hotplug.
/// </summary>
public sealed class MenuInputSeat : IMenuInputSource
{
    /// <summary>A seat over <paramref name="input"/>, which stays the seat devices' to bind.</summary>
    public MenuInputSeat(MenuInput input)
    {
        Input = input ?? throw new System.ArgumentNullException(nameof(input));
    }

    /// <summary>The raw poller behind the seat.</summary>
    public MenuInput Input { get; }

    public string DeviceLabel => Input.DeviceLabel;

    public bool CapturingText
    {
        get => Input.TextEntry;
        set => Input.TextEntry = value;
    }

    public MenuCommands Poll(float dt)
    {
        Input.Poll(dt);
        return new MenuCommands
        {
            MoveY = Input.Move,
            MoveX = Input.MoveX,
            Accept = Input.Accept,
            KeylessAccept = Input.KeylessAccept,
            Back = Input.Back,
            Join = Input.Start,
            Loadout = Input.Loadout,
            Contents = Input.Presets,
            Typed = Input.Typed,
            Erase = Input.Erase,
            Paste = Input.Paste,
            Unbind = Input.Unbind,
            OnPad = Input.Device == CSVM.Bindings.DeviceSide.Pad,
        };
    }

    public void Prime() => Input.Prime();
}

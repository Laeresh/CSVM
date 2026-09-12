using System.Collections.Generic;

namespace CSVM.Bindings;

/// <summary>One seat's whole input: an <see cref="ActionMap"/> and a <see cref="PlayerActions"/>
/// per <see cref="InputContext"/>, plus the keyboard gate that applies to all of them. This is what
/// a polling site is handed, and what a rebinding screen edits.
/// ⚠ Poll once per tick, from the loop that owns the tick, and read the snapshots afterwards. Two
/// polls in one tick is not an error but it is the drift <see cref="ActionSnapshot"/> exists to
/// prevent.</summary>
public sealed class BindingProfile
{
    private readonly Dictionary<InputContext, PlayerActions> _seats = new();

    private bool _readsKeyboard;

    public BindingProfile(IReadOnlyDictionary<InputContext, ActionMap> maps, bool readsKeyboard)
    {
        foreach (var pair in maps)
        {
            _seats[pair.Key] = new PlayerActions(pair.Value, readsKeyboard);
        }

        ReadsKeyboard = readsKeyboard;
    }

    /// <summary>Whether this seat reads the keyboard at all, in every context at once. False for a
    /// pad-only splitscreen player, whose keyboard bindings stay in the maps and read as idle.
    /// </summary>
    public bool ReadsKeyboard
    {
        get => _readsKeyboard;
        set
        {
            _readsKeyboard = value;
            foreach (var seat in _seats.Values)
            {
                seat.ReadsKeyboard = value;
            }
        }
    }

    /// <summary>Which side of this seat's hardware produced its last real input, the side every
    /// control prompt names. Moved by <see cref="ObserveDevice"/>, which whoever polls the seat's
    /// two halves calls once a tick.</summary>
    public ActiveDevice Device { get; } = new();

    /// <summary>The shipped keymap for a seat flying <paramref name="pad"/>, which may be
    /// <c>default</c> for a seat with no pad.</summary>
    public static BindingProfile Defaults(DeviceId pad, bool readsKeyboard)
    {
        var maps = new Dictionary<InputContext, ActionMap>();
        foreach (var context in System.Enum.GetValues<InputContext>())
        {
            maps[context] = DefaultBindings.MapFor(context, pad);
        }

        return new BindingProfile(maps, readsKeyboard);
    }

    /// <summary>That context's actions as a polling site reads them. Migrating a site means holding
    /// this and calling <see cref="PlayerActions.Held"/> where it called <c>IsKeyPressed</c>.
    /// </summary>
    public PlayerActions Actions(InputContext context) => _seats[context];

    /// <summary>That context's editable keymap, for a rebinding screen.</summary>
    public ActionMap Map(InputContext context) => _seats[context].Map;

    /// <summary>Resolves every context out of this tick's hardware. All of them, rather than only
    /// the mode in front of the player: a pause board and the aeroplane behind it are two contexts
    /// live on one tick.</summary>
    public void Poll(IDeviceState state)
    {
        foreach (var seat in _seats.Values)
        {
            seat.Poll(state);
        }
    }

    /// <summary>Hands <see cref="Device"/> this tick's keyboard-side and pad-side readings and
    /// answers whether the side moved. The keyboard gate is read off this seat rather than taken
    /// from the caller, so a pad-only seat cannot be switched to a key it never reads.</summary>
    public bool ObserveDevice(ActionSnapshot keyboardSide, ActionSnapshot padSide) =>
        Device.Observe(keyboardSide, padSide, ReadsKeyboard);
}

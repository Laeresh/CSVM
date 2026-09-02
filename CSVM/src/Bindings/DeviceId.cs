namespace CSVM.Bindings;

/// <summary>What kind of hardware a <see cref="DeviceId"/> names. Keyboard, joypad and mouse are the
/// three the game reads; <c>None</c> is the default-constructed value and names nothing.</summary>
public enum DeviceKind
{
    None,
    Keyboard,
    Joypad,
    Mouse,
}

/// <summary>Which physical device a binding is on, as a value that survives a replug and a
/// restart. A joypad is named by a stable hardware string (its GUID, or its name where the
/// platform reports no GUID), never by a connection index, because Godot reuses an index the
/// moment a pad is unplugged and another takes its slot. Resolving an id to a live index is the
/// device registry's job, not this type's.</summary>
public readonly record struct DeviceId
{
    private readonly string? _id;

    private DeviceId(DeviceKind kind, string id)
    {
        Kind = kind;
        _id = id;
    }

    /// <summary>The one keyboard. There is no second keyboard identity: the platform reports a
    /// single key state whatever hardware produced it.</summary>
    public static DeviceId Keyboard => new(DeviceKind.Keyboard, string.Empty);

    /// <summary>The one mouse, on the same reasoning as <see cref="Keyboard"/>: the platform reports
    /// a single pointer state, so there is nothing to distinguish a second identity by.</summary>
    public static DeviceId Mouse => new(DeviceKind.Mouse, string.Empty);

    public DeviceKind Kind { get; }

    /// <summary>The hardware string for a joypad, empty for every other kind.</summary>
    public string Id => _id ?? string.Empty;

    /// <summary>Whether this names no device at all, which is the default-constructed value. A
    /// binding holding one resolves false rather than throwing.</summary>
    public bool IsNone => Kind == DeviceKind.None;

    /// <summary>The joypad with this hardware string. The caller supplies whatever its platform
    /// reports as stable; this type only requires that it is not blank.</summary>
    public static DeviceId Joypad(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException("A joypad needs a non-blank hardware id.", nameof(id));
        return new DeviceId(DeviceKind.Joypad, id.Trim());
    }

    public override string ToString() => Kind switch
    {
        DeviceKind.Keyboard => "keyboard",
        DeviceKind.Mouse => "mouse",
        DeviceKind.Joypad => $"pad:{Id}",
        _ => "none",
    };
}

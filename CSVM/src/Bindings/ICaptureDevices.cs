namespace CSVM.Bindings;

/// <summary>The hardware a rebinding screen captures through, one reader per
/// <see cref="InputContext"/> together with the pad identity that context's bindings are authored
/// on.
/// ⚠ The identity and the reader are one thing, which is why they are one interface. A capture
/// stamps a pad control with <see cref="PadOf"/>, and a seat's reader answers for exactly one
/// identity and returns false or zero for every other (<see cref="SeatDeviceState"/>). Pair them at
/// a call site and a mismatch is silent: every pad read is false, no pad control can be captured,
/// and the keyboard keeps working, so the screen looks alive while half the hardware is dead.
/// </summary>
public interface ICaptureDevices
{
    /// <summary>The identity that context's pad rows sit on, and therefore the identity a captured
    /// pad control carries. It has to be the map's own or <see cref="ActionMap.SameControl"/> reads
    /// a captured control and an existing row as two controls and the steal rule goes blind.
    /// </summary>
    DeviceId PadOf(InputContext context);

    /// <summary>This frame's hardware for that context, answering for <see cref="PadOf"/>.
    /// </summary>
    IDeviceState For(InputContext context);
}

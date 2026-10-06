namespace CSVM.Bindings;

/// <summary>Which hand an XR control, or the screen pointer, belongs to.</summary>
public enum XrHand
{
    Left,
    Right,
}

/// <summary>The buttons of the headset's two hand controllers, the index a button binding on
/// <see cref="DeviceId.Xr"/> carries. Each is named by its role, not by one controller's lettering.
/// The lower and upper face buttons are X and Y, A and B on Meta Touch. Another controller maps onto
/// the same roles in the action map (<c>CSVM/openxr_action_map.tres</c>).
/// ⚠ Append only. A keymap file names a member, and a label or a default reads it positionally.
/// </summary>
public enum XrButton
{
    LeftTrigger,
    LeftGrip,
    LeftStickClick,
    LeftLowerFace,
    LeftUpperFace,
    LeftMenu,
    RightTrigger,
    RightGrip,
    RightStickClick,
    RightLowerFace,
    RightUpperFace,
    RightMenu,
}

/// <summary>The analogue inputs of the two hand controllers, the index an axis binding on
/// <see cref="DeviceId.Xr"/> carries. A stick's Y is positive up, as OpenXR reports it.
/// ⚠ Append only, as <see cref="XrButton"/>.</summary>
public enum XrAxis
{
    LeftTrigger,
    LeftGrip,
    LeftStickX,
    LeftStickY,
    RightTrigger,
    RightGrip,
    RightStickX,
    RightStickY,
}

/// <summary>
/// Where each XR control comes from and what a screen calls it. A control reads one action of the
/// OpenXR action map on one hand's tracker (<c>left_hand</c>, <c>right_hand</c>). The action names
/// are Godot's default map's, which <c>CSVM/openxr_action_map.tres</c> ships unchanged. Engine-free,
/// so the table unit-tests. Captions are Meta Touch's, the first profile. Reading the trackers is
/// <see cref="XrControllers"/>.
/// </summary>
public static class XrControls
{
    /// <summary>The action map's float action on <see cref="XrAxis.LeftTrigger"/> and its right twin,
    /// and the boolean one the triggers click through.</summary>
    public const string Trigger = "trigger";

    /// <summary>The triggers' click, which the runtime thresholds from the pull.</summary>
    public const string TriggerClick = "trigger_click";

    // One row per XrButton, in its order: the hand, the action map's boolean action, the caption.
    private static readonly (XrHand Hand, string Action, string Caption)[] Buttons =
    {
        (XrHand.Left, TriggerClick, "L Trigger"),
        (XrHand.Left, "grip_click", "L Grip"),
        (XrHand.Left, "primary_click", "L Stick Click"),
        (XrHand.Left, "ax_button", "X"),
        (XrHand.Left, "by_button", "Y"),
        (XrHand.Left, "menu_button", "Menu"),
        (XrHand.Right, TriggerClick, "R Trigger"),
        (XrHand.Right, "grip_click", "R Grip"),
        (XrHand.Right, "primary_click", "R Stick Click"),
        (XrHand.Right, "ax_button", "A"),
        (XrHand.Right, "by_button", "B"),
        (XrHand.Right, "menu_button", "R Menu"),
    };

    // One row per XrAxis, in its order: the hand, the action, which component of a vector action
    // (-1 for a float one), and the caption.
    private static readonly (XrHand Hand, string Action, int Component, string Caption)[] Axes =
    {
        (XrHand.Left, Trigger, -1, "L Trigger"),
        (XrHand.Left, "grip", -1, "L Grip"),
        (XrHand.Left, "primary", 0, "L Stick"),
        (XrHand.Left, "primary", 1, "L Stick"),
        (XrHand.Right, Trigger, -1, "R Trigger"),
        (XrHand.Right, "grip", -1, "R Grip"),
        (XrHand.Right, "primary", 0, "R Stick"),
        (XrHand.Right, "primary", 1, "R Stick"),
    };

    /// <summary>How many buttons the table names, the capture's scan range.</summary>
    public static int ButtonCount => Buttons.Length;

    /// <summary>How many analogue inputs the table names.</summary>
    public static int AxisCount => Axes.Length;

    /// <summary>The hand and action map action a button index reads, or null past the table.
    /// </summary>
    public static (XrHand Hand, string Action)? Button(int index) =>
        index >= 0 && index < Buttons.Length ? (Buttons[index].Hand, Buttons[index].Action) : null;

    /// <summary>The hand, action and vector component an axis index reads, or null past the table.
    /// A float action's component is -1.</summary>
    public static (XrHand Hand, string Action, int Component)? Axis(int index) =>
        index >= 0 && index < Axes.Length ? (Axes[index].Hand, Axes[index].Action, Axes[index].Component) : null;

    /// <summary>The trigger button of one hand, which the screen pointer clicks with.</summary>
    public static XrButton TriggerOf(XrHand hand) => hand == XrHand.Left ? XrButton.LeftTrigger : XrButton.RightTrigger;

    /// <summary>The other hand.</summary>
    public static XrHand Other(XrHand hand) => hand == XrHand.Left ? XrHand.Right : XrHand.Left;

    /// <summary>Whether that button or axis is one of the two triggers. They are the screen
    /// pointer's click, as the mouse's left button is, so a rebinding screen never captures them.
    /// </summary>
    public static bool IsTrigger(ControlKind kind, int index) => kind switch
    {
        ControlKind.Button => index is (int)XrButton.LeftTrigger or (int)XrButton.RightTrigger,
        ControlKind.Axis => index is (int)XrAxis.LeftTrigger or (int)XrAxis.RightTrigger,
        _ => false,
    };

    /// <summary>A control's caption without the device: "Menu", "L Stick Click", "R Stick Up".
    /// A stick axis names the direction its sign fires on.</summary>
    public static string Caption(BindingControl control)
    {
        if (control.Kind == ControlKind.Button)
        {
            return control.Index >= 0 && control.Index < Buttons.Length ? Buttons[control.Index].Caption : Unknown(control);
        }

        if (control.Kind != ControlKind.Axis || control.Index < 0 || control.Index >= Axes.Length)
        {
            return Unknown(control);
        }

        var axis = Axes[control.Index];
        return axis.Component switch
        {
            0 => axis.Caption + (control.Sign < 0 ? " Left" : " Right"),
            1 => axis.Caption + (control.Sign < 0 ? " Down" : " Up"),
            _ => axis.Caption,
        };
    }

    private static string Unknown(BindingControl control) =>
        control.Kind + " #" + control.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

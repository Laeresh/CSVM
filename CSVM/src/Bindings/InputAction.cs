namespace CSVM.Bindings;

/// <summary>Every named action the game binds a control to. One member per binding that exists at
/// a polling site today, so a migration is a lookup swap and never a rename or a regrouping.
/// ⚠ The members are contiguous from zero and <see cref="ActionMap"/> indexes arrays by them, so
/// never give one an explicit value and never renumber. Adding a member at the end is safe.
/// The debug and lab keys (the F13-F18 overlays, the viewer and weapon-lab panels) are deliberately
/// absent: they are development instruments, not player controls, and a rebinding screen that
/// offered them would invite a player to break their own diagnostics.</summary>
public enum InputAction
{
    // Flight, the six attitude half-axes plus the throttle pair. Each half is its own action
    // because a player binds the two ends of a stick separately (`BindingControl.Axis`).
    PitchUp,
    PitchDown,
    RollLeft,
    RollRight,
    YawLeft,
    YawRight,
    ThrottleUp,
    ThrottleDown,

    // Weapons and the engine command.
    FireGuns,
    FireRockets,
    SelectGunGroup,
    SelectOrdnance,
    Nitro,

    // Session commands.
    Respawn,
    AutoLand,
    Pause,

    // Target selection.
    TargetNextEnemy,
    TargetNextAlly,
    TargetNextNonAircraft,
    TargetNearest,
    TargetClear,
    CycleStuntTarget,

    // Views and the head. The snap-look diagonals are one control on two actions, which is what a
    // binding list expresses and four typed slots cannot.
    CycleCockpitViews,
    SelectChaseView,
    LookUp,
    LookDown,
    LookLeft,
    LookRight,
    LookCenter,
    LookBack,
    LookAimUp,
    LookAimDown,
    LookAimLeft,
    LookAimRight,
    FreeLook,

    // Menu and board navigation.
    MenuUp,
    MenuDown,
    MenuLeft,
    MenuRight,
    MenuAccept,
    MenuBack,
    MenuStart,
    MenuLoadout,
    MenuPresets,
    MenuJoin,

    // The free-flying spectator and anim-lab camera.
    CameraForward,
    CameraBack,
    CameraLeft,
    CameraRight,
    CameraUp,
    CameraDown,
    CameraBoost,
    CameraSlow,
    CameraLockTarget,
    CameraLookUp,
    CameraLookDown,
    CameraLookLeft,
    CameraLookRight,

    // The locked orbit's dolly. Its own pair rather than a second reading of the boost and slow
    // triggers, whose digital threshold would leave the first half of trigger travel inert here.
    CameraDollyOut,
    CameraDollyIn,

    // The backward half of each weapon selector, which the original carries as its own bound action
    // per weapon class. Appended here rather than filed beside their forward twins above, because
    // the members are positional and inserting one would renumber every action after it.
    SelectGunGroupPrev,
    SelectOrdnancePrev,
}

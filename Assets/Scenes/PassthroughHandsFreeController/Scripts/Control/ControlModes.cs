using System.Collections.Generic;

/// <summary>Current mode: gaze-dwell target selection on the static MRUK floor mesh.</summary>
public class GazeDwellControlMode : IControlMode
{
    public ControlModeId Id => ControlModeId.GazeDwell;
    public string DisplayName => "Gaze Dwell";
    public string Description => "Dwell on the floor to pick a target";
    public bool IsAvailable => true;
    public bool HasDistinctRotateAction => true;
    public string MoveActionSubtitle => "Gaze at floor to choose destination";

    public IApplicationState CreateMoveEntryState(StateContext context) => new SelectingMovementTargetState(context);
    public IApplicationState CreateRotateEntryState(StateContext context) => new RotatingState(context);
}

/// <summary>
/// Placeholder for a mode that is not implemented yet. Shown disabled in Settings.
/// Continuous modes should implement ITeleopInput and use RestThruAPI.ScheduleTeleop (keeps the X+Y emergency stop).
/// </summary>
public class UnavailableControlMode : IControlMode
{
    public ControlModeId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsAvailable => false;
    public bool HasDistinctRotateAction => false;
    public string MoveActionSubtitle => string.Empty;

    public UnavailableControlMode(ControlModeId id, string displayName, string description)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }

    public IApplicationState CreateMoveEntryState(StateContext context) => null;
    public IApplicationState CreateRotateEntryState(StateContext context) => null;
}

/// <summary>Continuous teleoperation: left stick drives forward/back, right stick rotates in place.</summary>
public class ControllerJoystickControlMode : IControlMode
{
    public ControlModeId Id => ControlModeId.ControllerJoystick;
    public string DisplayName => "Controller Joystick";
    public string Description => "Left stick drives, right stick rotates. Left Menu button to exit.";
    public bool IsAvailable => true;
    public bool HasDistinctRotateAction => false;
    public string MoveActionSubtitle => "Drive with the thumbsticks";

    public IApplicationState CreateMoveEntryState(StateContext context) => new JoystickTeleopState(context);
    public IApplicationState CreateRotateEntryState(StateContext context) => new JoystickTeleopState(context);
}

/// <summary>
/// Hands-free continuous teleoperation: a gaze pad anchored to the wheelchair. Looking at an arrow wedge moves
/// the chair (after a short arm delay); looking away stops it. Armrest buttons open the menu and minimize the pad.
/// </summary>
public class FloatingJoystickControlMode : IControlMode
{
    public ControlModeId Id => ControlModeId.FloatingJoystick;
    public string DisplayName => "Floating Joystick";
    public string Description => "Look at an arrow to move, look away to stop.";
    public bool IsAvailable => true;
    public bool HasDistinctRotateAction => false;
    public string MoveActionSubtitle => "Drive by looking at the pad";

    public IApplicationState CreateMoveEntryState(StateContext context) => new FloatingJoystickState(context);
    public IApplicationState CreateRotateEntryState(StateContext context) => new FloatingJoystickState(context);
}

public static class ControlModeRegistry
{
    private static readonly List<IControlMode> s_modes = new()
    {
        new GazeDwellControlMode(),
        new ControllerJoystickControlMode(),
        new FloatingJoystickControlMode(),
    };

    public static IReadOnlyList<IControlMode> All => s_modes;

    public static IControlMode Default => s_modes[0];

    public static IControlMode Get(ControlModeId id)
    {
        foreach (IControlMode mode in s_modes)
        {
            if (mode.Id == id) return mode;
        }
        return null;
    }

    /// <summary>The mode selected in settings, falling back to the default when it is missing or unavailable.</summary>
    public static IControlMode Active
    {
        get
        {
            IControlMode selected = Get(SettingsStore.Current.ControlMode);
            return selected != null && selected.IsAvailable ? selected : Default;
        }
    }
}

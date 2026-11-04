using System.Collections.Generic;

/// <summary>Current mode: gaze-dwell target selection on the static MRUK floor mesh.</summary>
public class GazeDwellControlMode : IControlMode
{
    public ControlModeId Id => ControlModeId.GazeDwell;
    public string DisplayName => "Gaze Dwell";
    public string Description => "Dwell on the floor to pick a target";
    public bool IsAvailable => true;
    public bool HasDistinctRotateAction => true;

    public IApplicationState CreateMoveEntryState(StateContext context) => new SelectingMovementTargetState(context);
    public IApplicationState CreateRotateEntryState(StateContext context) => new RotatingState(context);
}

/// <summary>
/// Placeholder for a mode that is not implemented yet. Shown disabled in Settings.
/// Implementing joystick teleoperation will need a continuous vel2 streaming path in RestThruAPI
/// and an emergency stop that works outside scheduled operations.
/// </summary>
public class UnavailableControlMode : IControlMode
{
    public ControlModeId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsAvailable => false;
    public bool HasDistinctRotateAction => false;

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

    public IApplicationState CreateMoveEntryState(StateContext context) => new JoystickTeleopState(context);
    public IApplicationState CreateRotateEntryState(StateContext context) => new JoystickTeleopState(context);
}

public static class ControlModeRegistry
{
    private static readonly List<IControlMode> s_modes = new()
    {
        new GazeDwellControlMode(),
        new ControllerJoystickControlMode(),
        new UnavailableControlMode(ControlModeId.FloatingJoystick, "Floating Joystick", "Coming soon"),
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

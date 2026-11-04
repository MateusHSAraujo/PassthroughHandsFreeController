/// <summary>
/// A way of commanding the wheelchair. The main menu asks the active mode for the entry state of
/// each high-level action instead of hardcoding states, so new modes (e.g. joystick teleoperation)
/// plug in without touching MainMenuState.
/// </summary>
public interface IControlMode
{
    ControlModeId Id { get; }
    string DisplayName { get; }
    string Description { get; }

    /// <summary>False for modes that are listed in Settings but not implemented yet.</summary>
    bool IsAvailable { get; }

    /// <summary>False when Rotate is redundant with Move (e.g. continuous joystick teleop drives both at once) —
    /// the main menu greys out the Rotate button in that case.</summary>
    bool HasDistinctRotateAction { get; }

    /// <summary>State entered when the user chooses "Move" in the main menu.</summary>
    IApplicationState CreateMoveEntryState(StateContext context);

    /// <summary>State entered when the user chooses "Rotate" in the main menu.</summary>
    IApplicationState CreateRotateEntryState(StateContext context);
}

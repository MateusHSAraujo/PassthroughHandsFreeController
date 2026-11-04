public class JoystickTeleopState : ApplicationState
{
    public JoystickTeleopState(StateContext context) : base(context) {}

    public override void Enter()
    {
        MovementController.OnMovementSequenceEnded += OnTeleopEnded;
        MovementController.StartJoystickTeleop();
    }

    public override void Exit()
    {
        MovementController.OnMovementSequenceEnded -= OnTeleopEnded;
    }

    private void OnTeleopEnded(bool success)
    {
        DebugLogger.Log($"Joystick teleoperation ended: success={success}");
        Controller.TransitionTo(new MainMenuState(_context));
    }
}

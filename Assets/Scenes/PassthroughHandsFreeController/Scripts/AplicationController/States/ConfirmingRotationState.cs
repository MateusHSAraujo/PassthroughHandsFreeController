public class ConfirmingRotationState : ApplicationState
{
    public ConfirmingRotationState(StateContext context) : base(context) {}

    public override void Enter()
    {
        ConfirmRotationUI.ShowCanvas();
        ConfirmRotationUI.PrimaryButton.onValueChanged.AddListener(OnStartRotation);
        ConfirmRotationUI.SecondaryButton.onValueChanged.AddListener(OnCancelRotation);
        
    }

    public override void Exit()
    {
        ConfirmRotationUI.PrimaryButton.onValueChanged.RemoveListener(OnStartRotation);
        ConfirmRotationUI.SecondaryButton.onValueChanged.RemoveListener(OnCancelRotation);
        ConfirmRotationUI.HideCanvas();
    }

    private void OnStartRotation(bool isOn)
    {
        if (!isOn) DebugLogger.Log("Something went wrong. This isON should not return false");
        DebugLogger.Log("Starting system rotation");
        Controller.TransitionTo(new RotatingState(_context));
    }
    
    private void OnCancelRotation(bool isOn)
    {
        if (!isOn) DebugLogger.Log("Something went wrong. This isON should not return false");
        DebugLogger.Log("Rotation canceled");
        Controller.TransitionTo(new SelectingMovementTargetState(_context));
    }

}
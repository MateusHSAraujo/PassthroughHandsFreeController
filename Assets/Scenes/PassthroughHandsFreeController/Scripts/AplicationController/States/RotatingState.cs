using UnityEditorInternal;

public class RotatingState : ApplicationState
{
    public RotatingState(StateContext context) : base(context) {}

    public override void Enter()
    {
        MovementController.OnMovementSequenceEnded += OnRotationEnded;
        MovementController.FollowTargetAligment(OVRUtils.CenterEyeAnchor);
    }

    public override void Exit()
    {
        MovementController.OnMovementSequenceEnded -= OnRotationEnded;
    }

    private void OnRotationEnded(bool success)
    {
        if (!success) DebugLogger.LogError("Rotation operation finished with fail. Resuming movement target selection");
        else DebugLogger.Log("Rotation finished with success. Resuming movement target selection.");
        Controller.TransitionTo(new SelectingMovementTargetState(_context));
    }
}
using UnityEngine;

public class MovingState : ApplicationState
{
    private readonly Vector3 _movementTarget;

    public MovingState(StateContext context, Vector3 movementTarget) : base(context)
    {
        _movementTarget = movementTarget;
    }

    public override void Enter()
    {
        MovementController.OnMovementSequenceEnded += OnMovementEnded;
        MovementController.PerformLinearDisplacement(_movementTarget);
    }

    public override void Exit()
    {
        _context.MovementController.OnMovementSequenceEnded -= OnMovementEnded;
    }

    private void OnMovementEnded(bool success)
    {
        if (!success) DebugLogger.LogError("Rotation operation finished with fail. Resuming movement target selection");
        else DebugLogger.Log("Rotation finished with success. Resuming movement target selection.");
        Controller.TransitionTo(new ConfirmingRotationState(_context));
    }

}
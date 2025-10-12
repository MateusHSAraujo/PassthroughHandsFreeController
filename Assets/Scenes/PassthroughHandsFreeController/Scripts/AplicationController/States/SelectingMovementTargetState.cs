// State that system is when the user is selecting a point on the ground to move
using UnityEngine;

public class SelectingMovementTargetState : ApplicationState
{
    public SelectingMovementTargetState(StateContext context) : base(context) {}

    public override void Enter()
    {
        CameraGazeCursor.Activate();
        CameraGazeCursor.OnSelectionFinished += OnTargetSelected;
        CameraGazeCursor.OnMainMenuRequested += OnReturnToMainMenu;
    }

    public override void Exit()
    {
        CameraGazeCursor.OnSelectionFinished -= OnTargetSelected;
        CameraGazeCursor.OnMainMenuRequested -= OnReturnToMainMenu;
    }

    private void OnTargetSelected(Vector3 targetPosition)
    {
        DebugLogger.Log("Target selected. Selection state finished.");
        Controller.TransitionTo(new MovingState(_context, targetPosition));
    }
    
    private void OnReturnToMainMenu(Vector3 position)
    {
        DebugLogger.Log("Main menu selected. Selection state finished.");
        Controller.TransitionTo(new MainMenuState(_context, position));
    }
}
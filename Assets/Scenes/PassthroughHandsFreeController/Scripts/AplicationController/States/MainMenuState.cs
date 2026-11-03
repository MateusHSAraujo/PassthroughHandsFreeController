using UnityEngine;

public class MainMenuState : ApplicationState
{
    private readonly Vector3? positionToOpenMenu = null;

    public MainMenuState(StateContext context) : base(context) { }

    public MainMenuState(StateContext context, Vector3 menuCanvasPosition) : base(context)
    {
        positionToOpenMenu = menuCanvasPosition;
    }

    public override void Enter()
    {
        if (!positionToOpenMenu.HasValue)
        {
            MainMenuUI.ShowCanvas();
        }
        else
        {
            MainMenuUI.ShowCanvasAtDirectionOf(positionToOpenMenu.Value);
        }

        MainMenuUI.OnMoveRequested += OnStartMovementTargetSelection;
        MainMenuUI.OnRotateRequested += OnStartRotation;
        MainMenuUI.OnServerConfigRequested += OnOpenServerConfig;
    }

    public override void Exit()
    {
        MainMenuUI.OnMoveRequested -= OnStartMovementTargetSelection;
        MainMenuUI.OnRotateRequested -= OnStartRotation;
        MainMenuUI.OnServerConfigRequested -= OnOpenServerConfig;
        MainMenuUI.HideCanvas();
    }

    private void OnStartMovementTargetSelection()
    {
        Controller.TransitionTo(new SelectingMovementTargetState(_context));
    }

    private void OnStartRotation()
    {
        Controller.TransitionTo(new RotatingState(_context));
    }

    private void OnOpenServerConfig()
    {
        Controller.TransitionTo(new ServerConfigState(_context, MainMenuUI.transform.position));
    }
}

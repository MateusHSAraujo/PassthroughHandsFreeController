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

        MainMenuUI.OnMoveRequested += OnStartMovement;
        MainMenuUI.OnRotateRequested += OnStartRotation;
        MainMenuUI.OnSettingsRequested += OnOpenSettings;
    }

    public override void Exit()
    {
        MainMenuUI.OnMoveRequested -= OnStartMovement;
        MainMenuUI.OnRotateRequested -= OnStartRotation;
        MainMenuUI.OnSettingsRequested -= OnOpenSettings;
        MainMenuUI.HideCanvas();
    }

    private void OnStartMovement()
    {
        TransitionToModeState(_context.ActiveControlMode.CreateMoveEntryState(_context));
    }

    private void OnStartRotation()
    {
        TransitionToModeState(_context.ActiveControlMode.CreateRotateEntryState(_context));
    }

    private void TransitionToModeState(IApplicationState next)
    {
        if (next == null)
        {
            DebugLogger.LogError($"Control mode {_context.ActiveControlMode.DisplayName} has no state for this action.");
            return;
        }
        Controller.TransitionTo(next);
    }

    private void OnOpenSettings()
    {
        Vector3 position = MainMenuUI.transform.position;
        if (SettingsUI != null)
        {
            Controller.TransitionTo(new SettingsState(_context, position));
        }
        else
        {
            // Settings hub not wired in the scene: keep the previous behavior.
            Controller.TransitionTo(new ServerConfigState(_context, position));
        }
    }
}

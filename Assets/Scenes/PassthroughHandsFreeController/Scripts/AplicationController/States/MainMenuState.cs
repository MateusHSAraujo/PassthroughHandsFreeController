// State that system is when the main menu is on
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuState : ApplicationState
{
    private readonly Vector3? positionToOpenMenu = null;

    public MainMenuState(StateContext context) : base(context) {}
    public MainMenuState(StateContext context, Vector3 menuCanvasPosition) : base(context)
    {
        positionToOpenMenu = menuCanvasPosition;
    }
    
    public override void Enter()
    {
        if (!positionToOpenMenu.HasValue)
        {
            MainMenuUI.ShowCanvas();
        } else
        {
            MainMenuUI.ShowCanvasAtDirectionOf(positionToOpenMenu.Value);
        }
        MainMenuUI.MoveButton.onValueChanged.AddListener(OnStartMovementTargetSelection);
        MainMenuUI.RotateButton.onValueChanged.AddListener(OnStartRotation);
    }

    public override void Exit()
    {
        MainMenuUI.MoveButton.onValueChanged.RemoveListener(OnStartMovementTargetSelection);
        MainMenuUI.RotateButton.onValueChanged.RemoveListener(OnStartRotation);
        MainMenuUI.HideCanvas();
    }

    private void OnStartMovementTargetSelection(bool isOn)
    {
        if (!isOn) DebugLogger.Log("Something went wrong. This isON should not return false");
        Controller.TransitionTo(new SelectingMovementTargetState(_context));
    }

    private void OnStartRotation(bool isOn)
    {
        if (!isOn) DebugLogger.Log("Something went wrong. This isON should not return false");
        Controller.TransitionTo(new RotatingState(_context));
    }
}
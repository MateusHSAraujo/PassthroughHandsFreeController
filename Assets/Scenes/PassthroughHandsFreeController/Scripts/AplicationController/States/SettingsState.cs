using UnityEngine;

/// <summary>
/// Settings hub: motion speeds, mesh visibility and control mode are edited in place;
/// the server address opens the dedicated keypad screen (ServerConfigState), which returns here.
/// </summary>
public class SettingsState : ApplicationState
{
    private readonly Vector3? _positionToOpen;

    public SettingsState(StateContext context) : base(context) { }

    public SettingsState(StateContext context, Vector3 menuCanvasPosition) : base(context)
    {
        _positionToOpen = menuCanvasPosition;
    }

    public override void Enter()
    {
        if (SettingsUI == null)
        {
            DebugLogger.LogError("SettingsUI is not assigned. Returning to MainMenuState.");
            Controller.TransitionTo(new MainMenuState(_context));
            return;
        }

        SettingsUI.OnBackRequested += OnBack;
        SettingsUI.OnServerConfigRequested += OnOpenServerConfig;

        if (_positionToOpen.HasValue) SettingsUI.ShowCanvasAtDirectionOf(_positionToOpen.Value);
        else SettingsUI.ShowCanvas();
    }

    public override void Exit()
    {
        if (SettingsUI == null) return;
        SettingsUI.OnBackRequested -= OnBack;
        SettingsUI.OnServerConfigRequested -= OnOpenServerConfig;
        SettingsUI.HideCanvas();
    }

    private void OnBack()
    {
        Controller.TransitionTo(new MainMenuState(_context, SettingsUI.transform.position));
    }

    private void OnOpenServerConfig()
    {
        if (ServerConfigUI == null)
        {
            DebugLogger.LogError("ServerConfigUI is not assigned.");
            return;
        }
        Controller.TransitionTo(new ServerConfigState(_context, SettingsUI.transform.position));
    }
}

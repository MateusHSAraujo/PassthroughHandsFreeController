using UnityEngine;

public class ServerConfigState : ApplicationState
{
    private readonly Vector3? _positionToOpen = null;

    public ServerConfigState(StateContext context) : base(context) { }

    public ServerConfigState(StateContext context, Vector3 menuCanvasPosition) : base(context)
    {
        _positionToOpen = menuCanvasPosition;
    }

    public override void Enter()
    {
        if (ServerConfigUI != null)
        {
            ServerConfigUI.OnConfigurationConfirmed += OnConfigurationConfirmed;
            ServerConfigUI.OnConfigurationCancelled += OnConfigurationCancelled;

            if (_positionToOpen.HasValue)
            {
                ServerConfigUI.ShowCanvasAtDirectionOf(_positionToOpen.Value);
            }
            else
            {
                ServerConfigUI.ShowCanvas();
            }
        }
        else
        {
            DebugLogger.LogError("ServerConfigUI is not assigned. Skipping to MainMenuState.");
            Controller.TransitionTo(new MainMenuState(_context));
        }
    }

    public override void Exit()
    {
        if (ServerConfigUI != null)
        {
            ServerConfigUI.OnConfigurationConfirmed -= OnConfigurationConfirmed;
            ServerConfigUI.OnConfigurationCancelled -= OnConfigurationCancelled;
            ServerConfigUI.HideCanvas();
        }
    }

    private void OnConfigurationConfirmed()
    {
        DebugLogger.Log($"Server configuration confirmed ({RestThruServer.BaseUrl}). Returning to settings.");
        Controller.TransitionTo(CreateReturnState());
    }

    private void OnConfigurationCancelled()
    {
        DebugLogger.Log("Server configuration cancelled. Returning to settings.");
        Controller.TransitionTo(CreateReturnState());
    }

    // Server config is opened from the Settings hub; fall back to the main menu if Settings is not wired.
    private IApplicationState CreateReturnState()
    {
        Vector3 position = ServerConfigUI.transform.position;
        return SettingsUI != null
            ? new SettingsState(_context, position)
            : new MainMenuState(_context, position);
    }
}

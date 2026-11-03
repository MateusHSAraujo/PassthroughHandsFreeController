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
        DebugLogger.Log($"Server configuration confirmed ({RestThruServer.BaseUrl}). Transitioning to MainMenuState.");
        Controller.TransitionTo(new MainMenuState(_context, ServerConfigUI.transform.position));
    }

    private void OnConfigurationCancelled()
    {
        DebugLogger.Log("Server configuration cancelled. Transitioning back to MainMenuState.");
        Controller.TransitionTo(new MainMenuState(_context, ServerConfigUI.transform.position));
    }
}

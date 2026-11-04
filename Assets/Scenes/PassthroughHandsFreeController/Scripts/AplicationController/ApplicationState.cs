public class StateContext
{
    public CameraGazeCursor CameraGazeCursor { get; private set; }
    public MovementSequenceController MovementController { get; private set; }
    public TwoButtonsCameraGazeControlledCanvas ConfirmRotationUI { get; private set; }
    public MainMenuUI MainMenuUI { get; private set; }
    public ServerConfigUI ServerConfigUI { get; private set; }
    public SettingsUI SettingsUI { get; private set; }
    public ApplicationController Controller { get; private set; }

    /// <summary>The control mode currently selected in settings (falls back to the default mode).</summary>
    public IControlMode ActiveControlMode => ControlModeRegistry.Active;

    public StateContext(CameraGazeCursor cameraGazeCursor,
                        MovementSequenceController movementController,
                        TwoButtonsCameraGazeControlledCanvas confirmRotationUI,
                        MainMenuUI mainMenuUI,
                        ApplicationController controller,
                        ServerConfigUI serverConfigUI = null,
                        SettingsUI settingsUI = null)
    {
        CameraGazeCursor = cameraGazeCursor;
        MovementController = movementController;
        ConfirmRotationUI = confirmRotationUI;
        MainMenuUI = mainMenuUI;
        ServerConfigUI = serverConfigUI;
        SettingsUI = settingsUI;
        Controller = controller;
    }
}


public abstract class ApplicationState : IApplicationState
{
    protected readonly StateContext _context;

    protected ApplicationController Controller => _context.Controller;
    protected CameraGazeCursor CameraGazeCursor => _context.CameraGazeCursor;
    protected MovementSequenceController MovementController => _context.MovementController;
    protected TwoButtonsCameraGazeControlledCanvas ConfirmRotationUI => _context.ConfirmRotationUI;
    protected MainMenuUI MainMenuUI => _context.MainMenuUI;
    protected ServerConfigUI ServerConfigUI => _context.ServerConfigUI;
    protected SettingsUI SettingsUI => _context.SettingsUI;

    public ApplicationState(StateContext context)
    {
        _context = context;
    }

    public abstract void Enter();
    public abstract void Exit();

    public override string ToString()
    {
        return this.GetType().Name;
    }
}

public class StateContext
{
    public CameraGazeCursor CameraGazeCursor { get; private set; }
    public MovementSequenceController MovementController { get; private set; }
    public TwoButtonsCameraGazeControlledCanvas ConfirmRotationUI { get; private set; }
    public MainMenuUI MainMenuUI { get; private set; }
    public ApplicationController Controller { get; private set; }

    public StateContext(CameraGazeCursor cameraGazeCursor, 
                        MovementSequenceController movementController, 
                        TwoButtonsCameraGazeControlledCanvas confirmRotationUI, 
                        MainMenuUI mainMenuUI, 
                        ApplicationController controller)
    {
        CameraGazeCursor = cameraGazeCursor;
        MovementController = movementController;
        ConfirmRotationUI = confirmRotationUI;
        MainMenuUI = mainMenuUI;
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
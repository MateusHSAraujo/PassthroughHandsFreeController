/// <summary>
/// Floating Joystick mode. One state with two sub-modes, so the armrest buttons stay visible (and keep their
/// dwell "triggered until the gaze leaves" guard) while switching between them:
/// - Driving: the gaze pad is shown and RestThruAPI streams its commands.
/// - Docked: the pad is minimized; no operation is running, so the chair cannot move.
/// Left armrest = Menu, right armrest = Minimize / Open pad. Leaving Driving always goes through the streaming
/// loop's exit path, which stops the chair before the sequence ends.
/// </summary>
public class FloatingJoystickState : ApplicationState
{
    private enum SubMode { Driving, Docked }


    private SubMode _subMode;
    private bool _loopRunning;     // a streaming operation exists (it may still be finishing its stop)
    private bool _restorePending;  // "Open pad" pressed while the previous loop was still finishing
    private bool _active;

    private FloatingJoystickUI Pad => _context.FloatingJoystickUI;
    private ArmrestButtonUI MenuButton => _context.LeftArmrestButton;
    private ArmrestButtonUI MinimizeButton => _context.RightArmrestButton;

    public FloatingJoystickState(StateContext context) : base(context) { }

    public override void Enter()
    {
        if (Pad == null || MenuButton == null || MinimizeButton == null)
        {
            DebugLogger.LogError("FloatingJoystickState: pad or armrest buttons are not assigned on ApplicationController. Returning to main menu.");
            Controller.TransitionTo(new MainMenuState(_context));
            return;
        }

        _active = true;
        MovementController.OnMovementSequenceEnded += OnSequenceEnded;
        MenuButton.OnActivated += OnMenuActivated;
        MinimizeButton.OnActivated += OnMinimizeActivated;

        MenuButton.SetIcon(ArmrestIcon.Menu);
        MinimizeButton.SetIcon(ArmrestIcon.Minimize);
        MenuButton.ShowCanvas();
        MinimizeButton.ShowCanvas();

        StartDriving();
    }

    public override void Exit()
    {
        if (!_active) return;
        _active = false;

        // Safety net: never leave the state with the stream still accepting commands.
        if (_loopRunning) Pad.RequestExit();

        MovementController.OnMovementSequenceEnded -= OnSequenceEnded;
        MenuButton.OnActivated -= OnMenuActivated;
        MinimizeButton.OnActivated -= OnMinimizeActivated;

        Pad.HideCanvas();
        MenuButton.HideCanvas();
        MinimizeButton.HideCanvas();
    }

    private void StartDriving()
    {
        DebugLogger.Log("FloatingJoystick: driving (pad open).");
        _subMode = SubMode.Driving;
        _restorePending = false;
        _loopRunning = true;
        MinimizeButton.SetIcon(ArmrestIcon.Minimize);
        Pad.ShowCanvas();
        MovementController.StartTeleop(Pad);
    }

    // The UI never waits for the robot: Minimize / Menu react at once and the streaming loop finishes stopping the
    // chair in the background (the pad is hidden and the loop is told to exit, so no command can be sent meanwhile).
    private void OnMinimizeActivated()
    {
        if (_subMode == SubMode.Docked)
        {
            if (_loopRunning)
            {
                // The previous loop is still finishing its stop: start the new one as soon as it has ended.
                _restorePending = true;
                MinimizeButton.SetIcon(ArmrestIcon.Minimize);
            }
            else
            {
                StartDriving();
            }
            return;
        }

        DebugLogger.Log("FloatingJoystick: minimize requested. Stopping in background.");
        _subMode = SubMode.Docked;
        _restorePending = false;
        Pad.RequestExit();
        Pad.HideCanvas();
        MinimizeButton.SetIcon(ArmrestIcon.Open);
    }

    private void OnMenuActivated()
    {
        DebugLogger.Log("FloatingJoystick: menu requested.");
        GoToMainMenu(); // Exit() asks the loop to stop the chair; it finishes in the background.
    }

    // Invoked by ScheduleOperation before RestThruAPI returns to IDLE: never schedule a new operation from here.
    private void OnSequenceEnded(bool success)
    {
        _loopRunning = false;

        if (_restorePending && success)
        {
            _restorePending = false;
            Controller.StartCoroutine(StartDrivingNextFrame());
            return;
        }

        if (_subMode == SubMode.Docked && success) return; // expected end after Minimize

        DebugLogger.LogWarning($"FloatingJoystick: teleoperation cancelled (success={success}: emergency or safety stop). Returning to main menu.");
        GoToMainMenu();
    }

    private System.Collections.IEnumerator StartDrivingNextFrame()
    {
        yield return null; // RestThruAPI returns to IDLE right after the completion callback
        if (_active && _subMode == SubMode.Docked) StartDriving();
    }

    private void GoToMainMenu()
    {
        Controller.TransitionTo(new MainMenuState(_context, Pad.transform.position));
    }
}

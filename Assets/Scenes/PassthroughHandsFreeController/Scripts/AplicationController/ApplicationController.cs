using System.Collections;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    [SerializeField] private CameraGazeCursor cameraGazeCursor;
    [SerializeField] private MovementSequenceController movementSequenceController;
    [SerializeField] private TwoButtonsCameraGazeControlledCanvas confirmRotationUI;
    [SerializeField] private ServerConfigUI serverConfigUI;
    [SerializeField] private SettingsUI settingsUI;

    [Header("Floating Joystick mode")]
    [SerializeField] private FloatingJoystickUI floatingJoystickUI;
    [Tooltip("Left armrest dwell button: Menu.")]
    [SerializeField] private ArmrestButtonUI leftArmrestButton;
    [Tooltip("Right armrest dwell button: Minimize / Open pad.")]
    [SerializeField] private ArmrestButtonUI rightArmrestButton;

    private MainMenuUI mainMenuUI;

    private Coroutine pingControlCoroutine;

    private IApplicationState currentState;
    private StateContext stateContext;

    void Start()
    {
        // Registering for callbacks
        if (cameraGazeCursor == null) DebugLogger.LogError("CameraGazeCursor not attributed on Unity.");

        if (movementSequenceController == null) DebugLogger.LogError("MovementSequenceController not attributed on Unity.");

        mainMenuUI = MainMenuUI.Instance;
        if (mainMenuUI == null) DebugLogger.LogError("MainMenuUI instance not found.");

        if (serverConfigUI == null) serverConfigUI = ServerConfigUI.Instance;
        if (serverConfigUI == null) DebugLogger.LogError("ServerConfigUI instance not found.");

        if (settingsUI == null) settingsUI = SettingsUI.Instance;
        if (settingsUI == null) DebugLogger.LogWarning("SettingsUI instance not found. The settings button will open the server config directly.");

        if (floatingJoystickUI == null) floatingJoystickUI = FloatingJoystickUI.Instance;
        if (floatingJoystickUI == null || leftArmrestButton == null || rightArmrestButton == null)
            DebugLogger.LogWarning("Floating Joystick UI or armrest buttons not assigned. That control mode will fall back to the main menu.");

        DebugLogger.Log($"Initial RestThru server address: {RestThruServer.BaseUrl} (Saved in PlayerPrefs: {RestThruServer.HasSavedConfiguration}); Settings: {JsonUtility.ToJson(SettingsStore.Current)}");

        // Creating context
        stateContext = new(cameraGazeCursor,
                            movementSequenceController,
                            confirmRotationUI,
                            mainMenuUI,
                            this,
                            serverConfigUI,
                            settingsUI,
                            floatingJoystickUI,
                            leftArmrestButton,
                            rightArmrestButton);

        // Always start directly in MainMenuState (with default 192.168.0.2:8080 if not configured yet)
        TransitionTo(new MainMenuState(stateContext));

        DebugLogger.LogWarning("Starting vibration");
        pingControlCoroutine = StartCoroutine(VibrateRoutine());
    }

    private IEnumerator VibrateRoutine()
    {
        while (true)
        {
            OVRInput.SetControllerVibration(1, 0.001f, OVRInput.Controller.RTouch);
            yield return new WaitForSeconds(2f);
            OVRInput.SetControllerVibration(1, 0, OVRInput.Controller.RTouch);
            yield return new WaitForSeconds(2f);
        }
    }

    public void TransitionTo(IApplicationState destState)
    {
        DebugLogger.Log($"Transitioning state. {currentState} -> {destState}");
        currentState?.Exit();
        currentState = destState;
        currentState.Enter();
    }
}

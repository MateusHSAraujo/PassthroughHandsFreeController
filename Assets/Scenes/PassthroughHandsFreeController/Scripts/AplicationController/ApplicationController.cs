using System.Collections;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    [SerializeField] private CameraGazeCursor cameraGazeCursor;
    [SerializeField] private MovementSequenceController movementSequenceController;
    [SerializeField] private TwoButtonsCameraGazeControlledCanvas confirmRotationUI;
    
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
        if (movementSequenceController == null) DebugLogger.LogError("MainMenuUI instance not found.");

        // Creating context
        stateContext = new(cameraGazeCursor,
                            movementSequenceController,
                            confirmRotationUI,
                            mainMenuUI,
                            this);

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

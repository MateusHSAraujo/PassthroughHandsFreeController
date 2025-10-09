using System.Threading.Tasks;
using PassthroughHandsFreeController.MainScene;
using Unity.VisualScripting;
using UnityEngine;

public class AplicationController : MonoBehaviour
{
    [SerializeField] private CameraGazeCursor m_cameraGazeCursor;
    [SerializeField] private MovementSequenceController m_movementSequenceController;

    private enum ApplicationControllerStates
    {
        IDLE,
        MOVING
    }

    private ApplicationControllerStates m_state;

    void Start()
    {
        // Registering for callbacks
        if (!m_cameraGazeCursor) DebugLogger.LogError("CameraGazeCursor not attributed on Unity.");
        else m_cameraGazeCursor.OnRequestedMovement += MoveSystemTo;

        if (!m_movementSequenceController) DebugLogger.LogError("MovementSequenceController not attributed on Unity.");
        else m_movementSequenceController.OnMovementSequenceEnded += SystemMovementEnded;

        TransitionTo(ApplicationControllerStates.IDLE);
        m_cameraGazeCursor.Activate();
    }


    void MoveSystemTo(Vector3 TargetPosition)
    {
        DebugLogger.Log($"Callback invoked. state={m_state} ; movementSuccess={TargetPosition}");
        switch (m_state)
        {
            case ApplicationControllerStates.IDLE:
                DebugLogger.Log("Invoking MovementSequenceController to perform movement");
                m_movementSequenceController.PerformMovementSequence(TargetPosition);
                TransitionTo(ApplicationControllerStates.MOVING);
                break;
            default:
                DebugLogger.LogWarning("Invalid state for MoveSystemTo. Ignoring");
                break;
        }
    }


    void SystemMovementEnded(bool movementSuccess)
    {
        DebugLogger.Log($"Callback invoked. state={m_state} ; movementSuccess={movementSuccess}");
        switch (m_state)
        {
            case ApplicationControllerStates.MOVING:
                DebugLogger.Log("Resuming CameraGazeController");
                m_cameraGazeCursor.Activate();
                TransitionTo(ApplicationControllerStates.IDLE);
                break;
            default:
                DebugLogger.LogWarning("Invalid state for SystemMovementEnded. Ignoring");
                break;
        }

    }

    private void TransitionTo(ApplicationControllerStates dest)
    {
        DebugLogger.Log($"Transitioning state. {m_state} -> {dest}");
        m_state = dest;
    }

    void OnDisable()
    {
        if (m_cameraGazeCursor) m_cameraGazeCursor.OnRequestedMovement -= MoveSystemTo;
        if (m_movementSequenceController) m_movementSequenceController.OnMovementSequenceEnded -= SystemMovementEnded;
    }
}

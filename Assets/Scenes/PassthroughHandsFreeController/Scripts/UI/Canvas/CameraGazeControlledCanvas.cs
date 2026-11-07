using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(MetaXRAudioSource))]
[RequireComponent(typeof(CanvasGroup))]
public class CameraGazeControlledCanvas : MonoBehaviour
{
    private static bool s_initialHmdTrackingConfirmed = false;

    [Header("Camera Gaze Controlled Canvas configurations:")]
    [Space(5)]
    [SerializeField] private GameObject SceneUIHelpers = null;
    [SerializeField] private CameraGazeControlledCanvasSettings SettingsAsset;

    [Header("Spatial Follow & Input Guard")]
    [Tooltip("If true, smoothly recenters the canvas in front of the user when they look away for over 1 second.")]
    public bool enableSoftFollow = false;

    [Tooltip("Delay in seconds before enabling raycast interaction after opening, preventing accidental gaze-through.")]
    public float inputActivationDelay = 0.35f;

    private LaserPointer LaserPointer;
    private AudioSource AudioSource;
    private CanvasGroup CanvasGroup;
    private OVRRaycaster RayCaster;
    protected ICanvasPositioner Positioner;

    public Vector3 offset;
    public float scale;

    private bool visible;
    private bool _isPositioned;
    private float _inputUnlockTime;
    private float _outOfViewTimer;
    private bool _isSmoothRecentering;
    private Vector3 _recenterTargetPos;
    private Quaternion _recenterTargetRot;
    private Coroutine _showRoutine;

    public bool IsVisible => visible;

    /// <summary>True once the canvas is visible and the anti-gaze-through input delay has elapsed.</summary>
    protected bool IsInputUnlocked => visible && CanvasGroup != null && CanvasGroup.interactable;

    protected virtual void Awake()
    {
        DebugLogger.Log("Initiating a Camera Gaze Controlled Canvas");

        if (SceneUIHelpers != null)
        {
            LaserPointer = SceneUIHelpers.GetComponentInChildren<LaserPointer>();
        }
        if (!LaserPointer) DebugLogger.LogError("A Camera Gaze Controlled Canvas requires a LaserPointer object");

        if (LaserPointer != null)
        {
            LaserPointer.LaserBeamBehavior = LaserPointer.LaserBeamBehaviorEnum.Off;
        }

        RayCaster = gameObject.GetComponent<OVRRaycaster>();
        if (!RayCaster) DebugLogger.LogError("Camera Gaze Controlled Canvas requires a OVRRaycaster");

        if (RayCaster != null && LaserPointer != null)
        {
            RayCaster.pointer = LaserPointer.gameObject;
        }

        CanvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (!CanvasGroup) DebugLogger.LogError("CameraGaze Controller Canvas requires a CanvasGroup");

        AudioSource = gameObject.GetComponent<AudioSource>();
        if (!AudioSource) DebugLogger.LogError("CameraGaze Controller Canvas requires a AudioSource");

        Positioner = gameObject.GetComponent<ICanvasPositioner>();
        if (Positioner == null) DebugLogger.LogError("CameraGaze Controller Canvas requires a ICanvasPositioner component");

        // Initially hide canvas
        CanvasGroup.alpha = 0f;
        visible = false;
        _isPositioned = false;
        CanvasGroup.interactable = false;
        CanvasGroup.blocksRaycasts = false;
    }

    public virtual void Update()
    {
        float targetAlpha = visible ? 1.0f : 0.0f;
        if (!Mathf.Approximately(CanvasGroup.alpha, targetAlpha))
        {
            float speed = SettingsAsset != null ? SettingsAsset.animationSpeed : 8.0f;
            CanvasGroup.alpha = Mathf.Lerp(CanvasGroup.alpha, targetAlpha, speed * Time.deltaTime);
            if (Mathf.Abs(CanvasGroup.alpha - targetAlpha) < 0.005f)
            {
                CanvasGroup.alpha = targetAlpha;
            }
        }

        if (!visible) return;

        // Enable interactivity after the anti-gaze-through guard delay
        if (!CanvasGroup.interactable && Time.unscaledTime >= _inputUnlockTime)
        {
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
        }

        if (enableSoftFollow && _isPositioned && Positioner != null && Positioner.targetCamera != null)
        {
            UpdateSoftFollow();
        }
    }

    private void UpdateSoftFollow()
    {
        Transform cam = Positioner.targetCamera;
        Vector3 toCanvas = transform.position - cam.position;
        Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 flatToCanvas = Vector3.ProjectOnPlane(toCanvas, Vector3.up).normalized;

        float angle = Vector3.Angle(flatForward, flatToCanvas);
        float distance = toCanvas.magnitude;

        if (!_isSmoothRecentering)
        {
            if (angle > 48f || distance > 1.6f || distance < 0.35f)
            {
                _outOfViewTimer += Time.deltaTime;
                if (_outOfViewTimer >= 0.9f)
                {
                    _outOfViewTimer = 0f;
                    _isSmoothRecentering = true;
                }
            }
            else
            {
                _outOfViewTimer = 0f;
            }
        }

        if (_isSmoothRecentering)
        {
            Vector3 desiredPos = cam.position + flatForward * 1.0f;
            desiredPos.y = cam.position.y - 0.2f;
            Vector3 lookDir = desiredPos - cam.position;
            Quaternion desiredRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                : transform.rotation;

            transform.position = Vector3.Lerp(transform.position, desiredPos, 6f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 6f * Time.deltaTime);

            if (Vector3.Distance(transform.position, desiredPos) < 0.03f && Quaternion.Angle(transform.rotation, desiredRot) < 2f)
            {
                _isSmoothRecentering = false;
            }
        }
    }

    public virtual void ShowCanvas()
    {
        DebugLogger.Log($"{this} - Showing canvas");
        if (!s_initialHmdTrackingConfirmed && Application.isPlaying)
        {
            if (_showRoutine != null) StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(WaitForInitialTrackingThenShow(null));
            return;
        }

        Positioner.Position();
        _isPositioned = true;
        SetCanvasVisible(true);
    }

    public virtual void ShowCanvasAt(Vector3 position)
    {
        DebugLogger.Log($"{this} - Showing canvas at position {position}.");
        Positioner.Position(position);
        _isPositioned = true;
        SetCanvasVisible(true);
    }

    public virtual void ShowCanvasAtDirectionOf(Vector3 targetPosition)
    {
        DebugLogger.Log($"{this} - Showing canvas towards {targetPosition} at standard distance.");
        if (!s_initialHmdTrackingConfirmed && Application.isPlaying)
        {
            if (_showRoutine != null) StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(WaitForInitialTrackingThenShow(targetPosition));
            return;
        }

        Positioner.PositionAtDirectionOf(targetPosition);
        _isPositioned = true;
        SetCanvasVisible(true);
    }

    public virtual void ShowCanvasAtDirectionOf(Vector3 targetPosition, float distance)
    {
        DebugLogger.Log($"{this} - Showing canvas towards {targetPosition}.");
        Positioner.PositionAtDirectionOf(targetPosition, distance);
        _isPositioned = true;
        SetCanvasVisible(true);
    }

    private IEnumerator WaitForInitialTrackingThenShow(Vector3? targetDirection)
    {
        yield return new WaitUntil(() => OVRManager.isHmdPresent && OVRManager.tracker.isPositionTracked);
        yield return new WaitForSeconds(1f);
        s_initialHmdTrackingConfirmed = true;

        if (targetDirection.HasValue)
        {
            Positioner.PositionAtDirectionOf(targetDirection.Value);
        }
        else
        {
            Positioner.Position();
        }

        _isPositioned = true;
        SetCanvasVisible(true);
    }

    public virtual void HideCanvas()
    {
        DebugLogger.Log($"{this} - Hiding Canvas.");
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }
        _isPositioned = false;
        _isSmoothRecentering = false;
        _outOfViewTimer = 0f;
        SetCanvasVisible(false);
    }

    private void SetCanvasVisible(bool isVisible)
    {
        visible = isVisible;
        if (isVisible)
        {
            ResetAllChildGazeInteractors();
            _inputUnlockTime = Time.unscaledTime + inputActivationDelay;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
            if (AudioSource != null && AudioSource.clip != null)
            {
                AudioSource.Play();
            }
        }
        else
        {
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
            ResetAllChildGazeInteractors();
        }
    }

    private void ResetAllChildGazeInteractors()
    {
        ToggleGazeInteractor[] interactors = GetComponentsInChildren<ToggleGazeInteractor>(true);
        foreach (ToggleGazeInteractor interactor in interactors)
        {
            interactor.ResetVisualStateImmediate();
        }
    }

    public override string ToString()
    {
        return $"{GetType().Name}";
    }
}

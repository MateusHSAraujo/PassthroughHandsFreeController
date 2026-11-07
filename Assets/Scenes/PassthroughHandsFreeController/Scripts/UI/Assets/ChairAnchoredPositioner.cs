using UnityEngine;

/// <summary>
/// Keeps a canvas attached to the wheelchair frame (from <see cref="AnchorManager"/>) instead of the head or the room,
/// so it does not slide out of the gaze while the chair drives or rotates.
/// - ChairFront: in front of the controller midpoint (the front of the armrests), tilted toward the user like a lectern.
/// - LeftController / RightController: floating above a Touch controller, turned to face the head.
/// Runs after AnchorManager.LateUpdate and follows every frame once positioned.
/// </summary>
[DefaultExecutionOrder(100)]
public class ChairAnchoredPositioner : MonoBehaviour, ICanvasPositioner
{
    public enum AnchorMode
    {
        ChairFront,
        LeftController,
        RightController
    }

    [Header("Chair Anchored Positioner")]
    [SerializeField] private AnchorManager anchorManager;
    [SerializeField] private AnchorMode mode = AnchorMode.ChairFront;

    [Tooltip("Height above the anchor point (m). ChairFront: above the controller midpoint. Controller modes: above the controller.")]
    [SerializeField] private float heightOffset = 0.30f;

    [Tooltip("Distance ahead of the anchor point along the chair's forward direction (m).")]
    [SerializeField] private float forwardOffset = 0.45f;

    [Tooltip("ChairFront only: tilt from vertical (deg). The top edge leans away from the user.")]
    [SerializeField] private float tiltFromVerticalDeg = 30f;

    [Tooltip("Controller modes: turn the canvas to face the head every frame.")]
    [SerializeField] private bool billboardToHead = false;

    [Tooltip("Smoothing time constant (s) against controller pose jitter. Keep it small so in-place rotation does not drag the canvas off the gaze. 0 = no smoothing.")]
    [SerializeField] private float smoothingTime = 0.06f;

    public Transform targetCamera { get; private set; }

    private bool _positioned;

    void Awake()
    {
        targetCamera = OVRUtils.CenterEyeAnchor;
        if (anchorManager == null) anchorManager = FindFirstObjectByType<AnchorManager>();
        if (anchorManager == null) DebugLogger.LogError($"{name}: ChairAnchoredPositioner requires an AnchorManager.");
    }

    void LateUpdate()
    {
        if (!_positioned) return;
        if (!TryComputePose(out Vector3 position, out Quaternion rotation)) return;

        if (smoothingTime <= 0f)
        {
            transform.SetPositionAndRotation(position, rotation);
            return;
        }

        float t = 1f - Mathf.Exp(-Time.deltaTime / smoothingTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, position, t),
            Quaternion.Slerp(transform.rotation, rotation, t));
    }

    /// <summary>Snaps to the anchored pose and starts following.</summary>
    public void Position()
    {
        if (TryComputePose(out Vector3 position, out Quaternion rotation))
        {
            transform.SetPositionAndRotation(position, rotation);
        }
        _positioned = true;
    }

    // The pose is always derived from the chair; explicit positions are ignored.
    public void Position(Vector3 position) => Position();
    public void PositionAtDirectionOf(Vector3 referenceObjectPosition, float distance) => Position();
    public void PositionAtDirectionOf(Vector3 referenceObjectPosition) => Position();

    private bool TryComputePose(out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;
        if (anchorManager == null || anchorManager.LeftControllerAnchor == null || anchorManager.RightControllerAnchor == null) return false;

        Transform chair = anchorManager.GetWheelchairTransform();
        if (chair == null) return false;

        // The chair drives on the floor: use a flattened forward and world up so controller roll/pitch jitter
        // does not wobble the canvas.
        Vector3 chairForward = Vector3.ProjectOnPlane(chair.forward, Vector3.up);
        if (chairForward.sqrMagnitude < 1e-6f) return false;
        chairForward.Normalize();

        Vector3 anchor = mode switch
        {
            AnchorMode.LeftController => anchorManager.LeftControllerAnchor.position,
            AnchorMode.RightController => anchorManager.RightControllerAnchor.position,
            _ => anchorManager.ControllerMidpoint
        };

        position = anchor + Vector3.up * heightOffset + chairForward * forwardOffset;

        if (mode == AnchorMode.ChairFront || !billboardToHead || targetCamera == null)
        {
            // Canvas forward points away from the viewer (same convention as LineOfSightPositioner).
            // Pitching it down tilts the top edge away from the user.
            float tilt = mode == AnchorMode.ChairFront ? tiltFromVerticalDeg : 0f;
            rotation = Quaternion.LookRotation(chairForward, Vector3.up) * Quaternion.Euler(tilt, 0f, 0f);
        }
        else
        {
            Vector3 away = position - targetCamera.position;
            rotation = away.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(away.normalized, Vector3.up) : rotation;
        }
        return true;
    }
}

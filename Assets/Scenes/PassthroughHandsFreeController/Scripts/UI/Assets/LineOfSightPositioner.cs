using UnityEngine;

public class LineOfSightPositioner : MonoBehaviour, ICanvasPositioner
{
    [Header("Line Of Sight Positioner configures")]
    [Tooltip("Height offset from the target camera Y to position the game object.")]
    [SerializeField] private float heightOffset = -0.2f;

    [Tooltip("Distance from the target camera to position the game object.")]
    [SerializeField] private float standardDistance = 1f;

    public Transform targetCamera { get; private set; }

    void Awake()
    {
        targetCamera = OVRUtils.CenterEyeAnchor;
    }

    // Positions at standard distance and in front of the camera
    public void Position()
    {
        PositionAtDirection(targetCamera.forward, standardDistance);
    }

    public void PositionAtDirectionOf(Vector3 referenceObjectPosition)
    {
        PositionAtDirectionOf(referenceObjectPosition, standardDistance);
    }
    
    public void PositionAtDirectionOf(Vector3 referenceObjectPosition, float distance)
    {
        Vector3 direction = referenceObjectPosition - targetCamera.position;
        Vector3 position = targetCamera.position + distance * direction.normalized;
        Position(position);
    }


    private void PositionAtDirection(Vector3 direction, float distance)
    {
        Vector3 position = targetCamera.position + distance * direction.normalized;
        Position(position);
    }

    public void Position(Vector3 position)
    {
        position.y = targetCamera.position.y + heightOffset;
        transform.position = position;
        transform.LookAt(targetCamera);
        transform.Rotate(0f, 180f, 0f, Space.Self);
    }

}

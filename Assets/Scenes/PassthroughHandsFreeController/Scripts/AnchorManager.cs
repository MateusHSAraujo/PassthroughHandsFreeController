using UnityEngine;

public class AnchorManager : MonoBehaviour
{
    [SerializeField] private Transform LeftAnchor;
    [SerializeField] private Transform RightAnchor;

    [SerializeField] private GameObject Wheelchair_prefab;
    private GameObject Wheelchair;
    [SerializeField] private float BackwardsOffset = 0.2f;
    [SerializeField] private LineRendererDebugTool lineRendererDebugTool;
    [SerializeField] private LineRendererDebugTool lineRendererDebugToolL;
    [SerializeField] private LineRendererDebugTool lineRendererDebugToolR;

    private void OnEnable()
    {
        SettingsStore.Changed += OnSettingsChanged;
        ApplyDebugAnchorsVisibility(SettingsStore.Current.DebugAnchorsVisible);
    }

    private void OnDisable()
    {
        SettingsStore.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings settings) => ApplyDebugAnchorsVisibility(settings.DebugAnchorsVisible);

    /// <summary>
    /// Shows or hides the debug visuals: the line-renderer axes and the wheelchair anchor gizmo's meshes.
    /// Pose tracking is unaffected: the wheelchair object stays active and keeps following the controllers.
    /// </summary>
    private void ApplyDebugAnchorsVisibility(bool visible)
    {
        if (Wheelchair)
        {
            foreach (Renderer r in Wheelchair.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
        if (lineRendererDebugTool) lineRendererDebugTool.gameObject.SetActive(visible);
        if (lineRendererDebugToolL) lineRendererDebugToolL.gameObject.SetActive(visible);
        if (lineRendererDebugToolR) lineRendererDebugToolR.gameObject.SetActive(visible);
    }

    void Start()
    {
        if (!LeftAnchor) DebugLogger.LogError("Missing Left Anchor");
        if (!RightAnchor) DebugLogger.LogError("Missing Left Anchor");
        DebugLogger.Log("Anchor Manager initiated");
        if (Wheelchair_prefab)
        {
            DebugLogger.Log("Wheelchair has a prefab. So setting it");
            Wheelchair = Instantiate(Wheelchair_prefab);
        }
        else
        {
            // No visual is needed: the object only carries the wheelchair pose.
            Wheelchair = new GameObject("Wheelchair");
        }
        ApplyDebugAnchorsVisibility(SettingsStore.Current.DebugAnchorsVisible); // spawned after OnEnable

        if (lineRendererDebugTool) lineRendererDebugTool.Follow(Wheelchair);
        if (lineRendererDebugToolL) lineRendererDebugToolL.Follow(LeftAnchor.gameObject);
        if (lineRendererDebugToolR) lineRendererDebugToolR.Follow(RightAnchor.gameObject);
        return;
    }

    void LateUpdate()
    {
        Vector3 WheelchairRight = (LeftAnchor.position - RightAnchor.position).normalized;
        if (WheelchairRight == Vector3.zero) WheelchairRight = RightAnchor.right;

        Vector3 WheelchairUp = ((LeftAnchor.up + RightAnchor.up) / 2).normalized;

        Vector3 WheelchairFoward = Vector3.Cross(WheelchairUp, WheelchairRight).normalized;

        Vector3 WheelchairPosition = (LeftAnchor.transform.position + RightAnchor.transform.position) / 2;
        WheelchairPosition = WheelchairPosition - BackwardsOffset * WheelchairFoward;
        Wheelchair.transform.position = WheelchairPosition;
        Wheelchair.transform.rotation = Quaternion.LookRotation(WheelchairFoward, WheelchairUp);
    }

    public Transform GetWheelchairTransform()
    {
        return Wheelchair.transform;
    }
}

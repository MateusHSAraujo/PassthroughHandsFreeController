using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(MetaXRAudioSource))]
[RequireComponent(typeof(CanvasGroup))]
public class CameraGazeControlledCanvas : MonoBehaviour
{
    [Header("Camera Gaze Controlled Canvas configurations:")]
    [Space(5)]
    [SerializeField] private GameObject SceneUIHelpers = null;
    [SerializeField] private CameraGazeControlledCanvasSettings SettingsAsset;
    
    private LaserPointer m_lp;
    private AudioSource AudioSource;
    private CanvasGroup CanvasGroup;
    private OVRRaycaster RayCaster;
    protected ICanvasPositioner Positioner;

    public Vector3 offset;
    public float scale;

    private bool visible;

    protected virtual void Awake()
    {
        DebugLogger.Log("Initiating a Camera Gaze Controlled Canvas");

        // Laser pointer
        m_lp = SceneUIHelpers.GetComponentInChildren<LaserPointer>();
        if (!m_lp) DebugLogger.LogError("A Camera Gaze Controlled Canvas requires a LaserPointer object");
            
        // Switch this to on if you want the laser pointer to be active
        m_lp.LaserBeamBehavior = LaserPointer.LaserBeamBehaviorEnum.Off;

        RayCaster = gameObject.GetComponent<OVRRaycaster>();
        if (!RayCaster) DebugLogger.LogError("Camera Gaze Controlled Canvas requires a OVRRaycaster");
            
        RayCaster.pointer = m_lp.gameObject;

        CanvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (!CanvasGroup) DebugLogger.LogError("CameraGaze Controller Canvas requires a CanvasGroup");
           

        AudioSource = gameObject.GetComponent<AudioSource>();
        if (!AudioSource) DebugLogger.LogError("CameraGaze Controller Canvas requires a AudioSource");

        Positioner = gameObject.GetComponent<ICanvasPositioner>();
        if (Positioner == null) DebugLogger.LogError("CameraGaze Controller Canvas requires a ICanvasPositioner component");

        // Initially hide canvas
        CanvasGroup.alpha = 0;
        visible = false;
        CanvasGroup.interactable = false;
        CanvasGroup.blocksRaycasts = false;
    }

    public virtual void Update()
    {
        CanvasGroup.alpha = Mathf.Lerp(CanvasGroup.alpha, visible ? 1.0f : 0.0f, SettingsAsset.animationSpeed * Time.deltaTime);
    }

    public virtual void ShowCanvas()
    {
        DebugLogger.Log($"{this} - Showing canvas");
        Positioner.Position();
        SetCanvasVisible(true);
    }

    public virtual void ShowCanvasAt(Vector3 position)
    {
        Debug.Log($"{this} - Showing canvas at position {position}.");
        Positioner.Position(position);
        SetCanvasVisible(true);
    }

    public virtual void ShowCanvasAtDirectionOf(Vector3 targetPosition)
    {
        Debug.Log($"{this} - Showing canvas towards {targetPosition} at standard distance.");
        Positioner.PositionAtDirectionOf(targetPosition);
        SetCanvasVisible(true);
    }

    public virtual void ShowCanvasAtDirectionOf(Vector3 targetPosition, float distance)
    {
        DebugLogger.Log($"{this} - Showing canvas towards {targetPosition}.");
        Positioner.PositionAtDirectionOf(targetPosition, distance);
        SetCanvasVisible(true);
    }

    public void HideCanvas()
    {
        DebugLogger.Log($"{this} - Hiding Canvas.");
        SetCanvasVisible(false);
    }

    private void SetCanvasVisible(bool isVisible)
    {
        visible = isVisible;
        CanvasGroup.interactable = isVisible;
        CanvasGroup.blocksRaycasts = isVisible;
        if (isVisible)
        {
            AudioSource.Play();
        }
    }

    public override string ToString()
    {
        return $"{this.GetType().Name}";
    }
}


using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class RotationUI : MonoBehaviour
{
    [Header("RotationUI configurations:")]
    [Space(5)]
    [SerializeField] private Vector3 offset;
    [SerializeField] private float animationSpeed;

    private CanvasGroup canvasGroup;
    private bool visible;

    private RadialFill radialFill;

    void Awake()
    {
        radialFill = gameObject.GetComponentInChildren<RadialFill>(true);
        if (!radialFill) DebugLogger.LogError("RadialFill not found on children");

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (!canvasGroup) DebugLogger.LogError("CanvasGroup not found on game object");
    }

    void Start()
    {
        SetPosition();
        HideCanvas();
    }
    
    void Update()
    {
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, visible ? 1.0f : 0.0f, animationSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        SetPosition();
    }

    void SetPosition()
    {
        Vector3 targetRotation = OVRUtils.CenterEyeAnchor.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(targetRotation.x, targetRotation.y, 0f);
        transform.position = OVRUtils.CenterEyeAnchor.TransformPoint(offset);
    }

    public void StartFilling(float FillingDuration)
    {
        radialFill.StartFilling(FillingDuration);
    }

    public void ResetFilling()
    {
        radialFill.ResetFilling();
    }

    public virtual void ShowCanvas()
    {
        DebugLogger.Log("Showing canvas");
        // Showing canvas
        visible = true;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void HideCanvas()
    {
        DebugLogger.Log("Hiding Canvas.");
        // Hide canvas
        visible = false;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        radialFill.ResetFilling();
    }
}

    


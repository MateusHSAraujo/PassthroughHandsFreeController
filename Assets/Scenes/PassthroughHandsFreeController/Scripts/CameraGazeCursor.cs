using UnityEngine;
using System;

public class CameraGazeCursor : MonoBehaviour
{
    public GameObject MainCamera;
    public GameObject prefabCursor;

    private SelecTargetUI GazeCursorCanvas;
    private CursorController CursorController;
    private HitpointIndicator HitPointIndicator;

    public event Action<Vector3> OnSelectionFinished; // Action for when the target is selected
    public event Action<Vector3> OnMainMenuRequested; // Action for when user selects to return to main menu

    private Vector3 canvasPosition;
    // Awake is called when the script instance is being loaded
    void Awake()
    {
        CursorController = Instantiate(prefabCursor, Vector3.zero, Quaternion.identity).GetComponent<CursorController>();
        HitPointIndicator = CursorController.GetComponentInChildren<HitpointIndicator>(true);
        if (HitPointIndicator == null) DebugLogger.LogError("Hitpoint indicator not found");
        // Await for controller to activate
        gameObject.SetActive(false);
    }

    void Start()
    {
        GazeCursorCanvas = SelecTargetUI.Instance;
        if (GazeCursorCanvas == null) DebugLogger.LogError("MovementCanvas component not found on the scene");
    }

    public void Activate()
    {
        gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = new(MainCamera.transform.position, MainCamera.transform.forward);

        // Logic to make de camera gaze cursor work with the raycast based on the scene mesh pre-loaded
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Quaternion cursorRotation = Quaternion.LookRotation(hit.normal, MainCamera.transform.up);
            DebugLogger.Log("Hit: " + hit.collider.gameObject.name);
            if (hit.collider.gameObject.name == "FLOOR_EffectMesh")
            {
                DebugLogger.Log("Hit on floor object. Sending to cursor controller.");
                CursorController.UpdateCursorPosition(hit.point, cursorRotation);
            }
            else
            {
                DebugLogger.Log("Hit on non-floor object. Deactivating CursorController.");
                if (CursorController.gameObject.activeSelf) CursorController.DeactivateCursor();
            }
        }
        else
        {
            DebugLogger.Log("No hit detected");
        }
    }

    private void ActivateCanvas()
    {
        DebugLogger.Log("Activating canvas.");
        canvasPosition = CursorController.gameObject.transform.position;
        GazeCursorCanvas.ShowCanvasAt(canvasPosition);
        gameObject.SetActive(false);
    }

    public void OnMovementConfirmed()
    {
        DebugLogger.Log("Confirm toggle was activated. Starting movement sequence.");
        GazeCursorCanvas.HideCanvas();
        OnSelectionFinished?.Invoke(CursorController.gameObject.transform.position);
        gameObject.SetActive(false);
    }

    public void OnMovementCancelled()
    {
        DebugLogger.Log("Cancel toggle was activated. Restarting CameraGazeCursor functionalities");
        GazeCursorCanvas.HideCanvas();
        gameObject.SetActive(true);
    }

    public void ReturnToMainMenu()
    {
        DebugLogger.Log("Main Menu toggle was activated. Deactivanting and invoking action");
        GazeCursorCanvas.HideCanvas();
        OnMainMenuRequested?.Invoke(canvasPosition);
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (HitPointIndicator != null) HitPointIndicator.OnIndicatorFilled += ActivateCanvas;
    }

    void OnDisable()
    {
        if(HitPointIndicator != null) HitPointIndicator.OnIndicatorFilled -= ActivateCanvas;
    }
}


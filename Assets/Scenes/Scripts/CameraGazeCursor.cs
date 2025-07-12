using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Meta.XR;
using Meta.XR.MRUtilityKit;

public class CameraGazeCursor : MonoBehaviour
{
    public Transform cameraTransform;
    public GameObject prefabCursor;
    public GameObject canvasPrefab;

    private GameObject cursorObject;
    private GameObject canvasObject;

    private MovementCanvas MovementCanvas;
    private CursorController CursorController;

    private bool isCanvasOpen;

    // Awake is called when the script instance is being loaded
    void Awake()
    {
        cursorObject = Instantiate(prefabCursor, Vector3.zero, Quaternion.identity);
        canvasObject = Instantiate(canvasPrefab, Vector3.zero, Quaternion.identity);
        CursorController = cursorObject.GetComponent<CursorController>();
        HitpointIndicator indicator = cursorObject.GetComponentInChildren<HitpointIndicator>(true);
        if (indicator == null) DebugLogger.LogError("HitpointIndicator component not found on the cursor object.");
        else
        {
            indicator.OnIndicatorFilled += ActivateCanvas;
        }
        MovementCanvas = canvasObject.GetComponent<MovementCanvas>();
        if (MovementCanvas == null) DebugLogger.LogError("MovementCanvas component not found on the scene");
    }

    // Update is called once per frame
    void Update()
    {
        if (!isCanvasOpen)
        {
            Ray ray = new(cameraTransform.position, cameraTransform.forward);

            // Logic to make de camera gaze cursor work with the raycast based on the scene mesh pre-loaded
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Quaternion cursorRotation = Quaternion.LookRotation(hit.normal, cameraTransform.up);
                DebugLogger.Log("Hit: " + hit.collider.gameObject.name);
                if (hit.collider.gameObject.name == "FLOOR_EffectMesh")
                {
                    DebugLogger.Log("Hit on floor object. Sending to cursor controller.");
                    CursorController.UpdateCursorPosition(hit.point, cursorRotation);
                }
                else
                {
                    DebugLogger.Log("Hit on non-floor object. Deactivating CursorController.");
                    if (cursorObject.activeSelf) CursorController.DeactivateCursor();
                }
            }
            else
            {
                DebugLogger.Log("No hit detected");
            }
        }
        else
        {
            DebugLogger.Log("Canvas open. Not updating");
        }
    
    }
    
    private void ActivateCanvas()
    {
        DebugLogger.Log("Activating canvas.");
        isCanvasOpen = true;
        Vector3 canvasPosition = cursorObject.transform.position;
        canvasPosition.y = cameraTransform.position.y - 0.2f; // Align the canvas with the camera height
        MovementCanvas.ShowCanvas(canvasPosition, cameraTransform);
    }
    
    
}

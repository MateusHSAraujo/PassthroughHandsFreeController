using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Meta.XR;
using Meta.XR.MRUtilityKit;

public class CameraGazeCursor : MonoBehaviour
{
    public Transform cameraTransform;
    public GameObject prefabCursor;

    private GameObject cursorObject;
    private CursorController CursorController;

    // Awake is called when the script instance is being loaded
    void Awake()
    {
        cursorObject = Instantiate(prefabCursor, Vector3.zero, Quaternion.identity);
        HitpointIndicator indicator = cursorObject.GetComponentInChildren<HitpointIndicator>(true);
        if (indicator == null) Debug.LogError("HitpointIndicator component not found on the cursor object.");
        else
        {
            indicator.OnIndicatorFilled += ActivateCanvas;
        }
        CursorController = cursorObject.GetComponent<CursorController>();
    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = new(cameraTransform.position, cameraTransform.forward);
        
        // Logic to make de camera gaze cursor work with the raycast based on the scene mesh pre-loaded
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Quaternion cursorRotation = Quaternion.LookRotation(hit.normal, cameraTransform.up);
            Debug.Log("Hit: " + hit.collider.gameObject.name);
            if (hit.collider.gameObject.name == "FLOOR_EffectMesh")
            {
                Debug.Log("Hit on floor object. Sending to cursor controller.");
                CursorController.UpdateCursorPosition(hit.point, cursorRotation);
            }
            else
            {
                Debug.Log("Hit on non-floor object. Deactivating CursorController.");
                if (cursorObject.activeSelf) CursorController.DeactivateCursor();
            }
        }
        else
        {
            Debug.Log("No hit detected");
        }

    }

    private void ActivateCanvas()
    {
        Debug.Log("Activating canvas.");
        // Logic to activate the canvas or any other UI element
        // For example, you can enable a UI panel or display a message
        // canvasObject.SetActive(true);
    }
    
    
}

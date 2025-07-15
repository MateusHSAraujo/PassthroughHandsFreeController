using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Meta.XR;
using Meta.XR.MRUtilityKit;

namespace PassthroughHandsFreeController.MainScene
{
    /// <summary>
    /// This script manages the camera gaze cursor functionality, allowing interaction with the environment.
    /// </summary>
    public class CameraGazeCursor : MonoBehaviour
    {
        public GameObject MainCamera;
        public GameObject prefabCursor;

        private MovementCanvas MovementCanvas;
        private CursorController CursorController;

        // Awake is called when the script instance is being loaded
        void Awake()
        {
            CursorController = Instantiate(prefabCursor, Vector3.zero, Quaternion.identity).GetComponent<CursorController>();
            HitpointIndicator indicator = CursorController.GetComponentInChildren<HitpointIndicator>(true);
            if (indicator == null) DebugLogger.LogError("HitpointIndicator component not found on the cursor object.");
            else
            {
                indicator.OnIndicatorFilled += ActivateCanvas;
            }
            MovementCanvas = MovementCanvas.Instance;
            if (MovementCanvas == null) DebugLogger.LogError("MovementCanvas component not found on the scene");
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
            Vector3 canvasPosition = CursorController.gameObject.transform.position;
            canvasPosition.y = MainCamera.transform.position.y - 0.2f; // Align the canvas with the camera height
            MovementCanvas.ShowCanvas(canvasPosition, MainCamera.transform);
            gameObject.SetActive(false);
        }

        public void OnMovementConfirmed()
        {
            DebugLogger.Log("Confirm toggle was activated. Starting movement sequence.");
            MovementCanvas.HideCanvas();
        }

        public void OnMovementCancelled()
        {
            DebugLogger.Log("Cancel toggle was activated. Restarting CameraGazeCursor functionalities");
            MovementCanvas.HideCanvas();
            gameObject.SetActive(true);
        }
    }
}

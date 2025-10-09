using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using System;
using Unity.VisualScripting;

namespace PassthroughHandsFreeController.MainScene
{
    /// <summary>
    /// This script manages the camera gaze cursor functionality, allowing interaction with the environment.
    /// </summary>
    public class CameraGazeCursor : MonoBehaviour
    {
        public GameObject MainCamera;
        public GameObject prefabCursor;

        private CameraGazeCursorSelecTargetUI m_GazeCursorCanvas;
        private CursorController m_CursorController;
        private HitpointIndicator m_HitPointIndicator;

        public event Action<Vector3> OnRequestedMovement; // Callback for activation events

        // Awake is called when the script instance is being loaded
        void Awake()
        {
            m_CursorController = Instantiate(prefabCursor, Vector3.zero, Quaternion.identity).GetComponent<CursorController>();
            m_HitPointIndicator = m_CursorController.GetComponentInChildren<HitpointIndicator>(true);
            if (m_HitPointIndicator == null) DebugLogger.LogError("Hitpoint indicator not found");
            // Await for controller to activate
            gameObject.SetActive(false);
        }

        void Start()
        {
            m_GazeCursorCanvas = CameraGazeCursorSelecTargetUI.Instance;
            if (m_GazeCursorCanvas == null) DebugLogger.LogError("MovementCanvas component not found on the scene");
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
                    m_CursorController.UpdateCursorPosition(hit.point, cursorRotation);
                }
                else
                {
                    DebugLogger.Log("Hit on non-floor object. Deactivating CursorController.");
                    if (m_CursorController.gameObject.activeSelf) m_CursorController.DeactivateCursor();
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
            Vector3 canvasPosition = m_CursorController.gameObject.transform.position;
            canvasPosition.y = MainCamera.transform.position.y - 0.2f; // Align the canvas with the camera height
            m_GazeCursorCanvas.ShowCanvas(canvasPosition, MainCamera.transform, Vector3.Distance(MainCamera.transform.position, canvasPosition));
            gameObject.SetActive(false);
        }

        public void OnMovementConfirmed()
        {
            DebugLogger.Log("Confirm toggle was activated. Starting movement sequence.");
            m_GazeCursorCanvas.HideCanvas();
            OnRequestedMovement?.Invoke(m_CursorController.gameObject.transform.position);
        }

        public void OnMovementCancelled()
        {
            DebugLogger.Log("Cancel toggle was activated. Restarting CameraGazeCursor functionalities");
            m_GazeCursorCanvas.HideCanvas();
            gameObject.SetActive(true);
        }

        void OnEnable()
        {
            if (m_HitPointIndicator != null) m_HitPointIndicator.OnIndicatorFilled += ActivateCanvas;
        }

        void OnDisable()
        {
            if(m_HitPointIndicator != null) m_HitPointIndicator.OnIndicatorFilled -= ActivateCanvas;
        }
    }
}

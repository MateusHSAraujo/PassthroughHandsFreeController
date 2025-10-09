using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace PassthroughHandsFreeController.MainScene
{
    /// <summary>
    /// This script manages the movement canvas functionality, allowing interaction with the environment.
    /// </summary>

    public class CameraGazeCursorSelecTargetUI : CameraGazeControlledCanvas
    {
        public static CameraGazeCursorSelecTargetUI Instance;

        private readonly float scaleFactor = 0.001f;
        private readonly float minScale = 0.002f;
        private readonly float maxScale = 0.008f;

        [SerializeField] private Toggle ConfirmButton;
        [SerializeField] private Toggle CancelButton;


        protected override void Awake()
        {
            // Initialize the canvas or any other setup if needed
            Debug.Log("CameraGazeCursorSelectTarget Awake called.");
            Debug.Assert(Instance == null);
            Instance = this;
            base.Awake();          
        }

        public void ShowCanvas(Vector3 position, Transform lookAt, float distance)
        {
            DebugLogger.Log($"Distance from canvas to camera: {distance}");
            transform.position = position;
            transform.LookAt(lookAt);
            transform.Rotate(0f, 180f, 0f, Space.Self);

            // Distance scaling
            float calculatedScaling = Math.Clamp(scaleFactor * distance, minScale, maxScale);
            transform.localScale = Vector3.one * calculatedScaling;

            ConfirmButton.SetIsOnWithoutNotify(false);
            CancelButton.SetIsOnWithoutNotify(false);

            Debug.Log($"Showing Movement Canvas at position: {position}, with scale: {transform.localScale.x}");
            base.ShowCanvas();
        }
    }
}
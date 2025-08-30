using System;
using UnityEngine;
using UnityEngine.UI;

namespace PassthroughHandsFreeController.MainScene
{
    /// <summary>
    /// This script manages the movement canvas functionality, allowing interaction with the environment.
    /// </summary>

    public class MovementCanvas : MonoBehaviour
    {
        [SerializeField]
        private GameObject uiHelpersPrefab = null;
        private GameObject m_uiHelpers;

        public static MovementCanvas Instance;

        
        private readonly float scaleFactor = 0.001f;
        private readonly float minScale = 0.002f;
        private readonly float maxScale = 0.008f;

        private Toggle[] m_toggles;
        private LaserPointer m_lp;
        private AudioSource m_audioSource;
        private CanvasGroup m_canvasGroup;

        void Awake()
        {
            // Initialize the canvas or any other setup if needed
            Debug.Log("MovementCanvas Awake called.");
            Debug.Assert(Instance == null);
            Instance = this;

            m_uiHelpers = Instantiate(uiHelpersPrefab);

            // Laser pointer
            m_lp = FindFirstObjectByType<LaserPointer>();
            if (!m_lp)
            {
                DebugLogger.LogError("Debug UI requires use of a LaserPointer and will not function without it. " +
                            "Add one to your scene, or assign the UIHelpers prefab to the DebugUIBuilder in the inspector.");
                return;
            }

            // Switch this to on if you want the laser pointer to be active
            m_lp.LaserBeamBehavior = LaserPointer.LaserBeamBehaviorEnum.Off;

            GetComponent<OVRRaycaster>().pointer = m_lp.gameObject;

            m_toggles = GetComponentsInChildren<Toggle>(true);

            if (m_toggles.Length != 2) DebugLogger.LogError($@"Movement cancelation or confirmation toggles not found. 
                                                               Toggles count:{m_toggles.Length}");

            m_audioSource = GetComponent<AudioSource>();

            m_canvasGroup = GetComponent<CanvasGroup>();

            // Initially hide canvas
            m_canvasGroup.alpha = 0;
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;
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

            Debug.Log($"Showing Movement Canvas at position: {position}, with scale: {transform.localScale.x}");

            foreach (Toggle t in m_toggles)
            {
                t.SetIsOnWithoutNotify(false);
            }

            gameObject.SetActive(true); // Activate the canvas

            m_audioSource.Play();

            // Showing canvas
            m_canvasGroup.alpha = 1;
            m_canvasGroup.interactable = true;
            m_canvasGroup.blocksRaycasts = true;
        }

        public void HideCanvas()
        {
            Debug.Log("Hiding Movement Canvas.");
            // Hide canvas
            m_canvasGroup.alpha = 0;
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;
        }
    }
}
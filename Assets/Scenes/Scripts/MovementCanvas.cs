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
        private GameObject m_uiHelpersToInstantiate = null;

        public static MovementCanvas Instance;
        
        private Toggle[] m_toggles;
        private LaserPointer m_lp;

        void Awake()
        {
            // Initialize the canvas or any other setup if needed
            Debug.Log("MovementCanvas Awake called.");
            Debug.Assert(Instance == null);
            Instance = this;

            _ = Instantiate(m_uiHelpersToInstantiate);

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

            gameObject.SetActive(false); // Initially deactivate the canvas
        }

        public void ShowCanvas(Vector3 position, Transform lookAt)
        {
            Debug.Log("Showing Movement Canvas at position: " + position);
            transform.position = position;
            transform.LookAt(lookAt);
            transform.Rotate(0f, 180f, 0f, Space.Self);
            foreach (Toggle t in m_toggles)
            {
                t.SetIsOnWithoutNotify(false);
            }
            gameObject.SetActive(true); // Activate the canvas
        }

        public void HideCanvas()
        {
            Debug.Log("Hiding Movement Canvas.");
            gameObject.SetActive(false); // Deactivate the canvas
        }
    }
}
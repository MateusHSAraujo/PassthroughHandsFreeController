using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
namespace PassthroughHandsFreeController.MainScene
{
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(MetaXRAudioSource))]
    [RequireComponent(typeof(CanvasGroup))]
    public class CameraGazeControlledCanvas : MonoBehaviour
    {
        private LaserPointer m_lp;

        [SerializeField] private GameObject SceneUIHelpers = null;
        [SerializeField] private AudioSource MyAudioSource;
        [SerializeField] private CanvasGroup MyCanvasGroup;
        [SerializeField] private OVRRaycaster MyRayCaster;

        public Vector3 offset;
        public float scale;

        [SerializeField] private float animationSpeed;
        private bool visible;

        protected virtual void Awake()
        {
            DebugLogger.Log("Initiating a Camera Gaze Controlled Canvas");
            

            // Laser pointer
            m_lp = SceneUIHelpers.GetComponentInChildren<LaserPointer>();
            if (!m_lp)
            {
                DebugLogger.LogError("A Camera Gaze Controlled Canvas requires a LaserPointer object");
                return;
            }

            // Switch this to on if you want the laser pointer to be active
            m_lp.LaserBeamBehavior = LaserPointer.LaserBeamBehaviorEnum.Off;

            if (!MyRayCaster)
            {
                DebugLogger.LogError("Camera Gaze Controlled Canvas requires a OVRRaycaster");
                return;
            }
            MyRayCaster.pointer = m_lp.gameObject;

            if (!MyCanvasGroup)
            {
                DebugLogger.LogError("CameraGaze Controller Canvas requires a CanvasGroup");
                return;
            }

            // Initially hide canvas
            MyCanvasGroup.alpha = 0;
            visible = false;
            MyCanvasGroup.interactable = false;
            MyCanvasGroup.blocksRaycasts = false;
        }

        public virtual void Update()
        {
            MyCanvasGroup.alpha = Mathf.Lerp(MyCanvasGroup.alpha, visible ? 1.0f : 0.0f, animationSpeed * Time.deltaTime);
        }


        public virtual void ShowCanvas()
        {
            DebugLogger.Log("Showing canvas");
            // Showing canvas
            visible = true;
            MyCanvasGroup.interactable = true;
            MyCanvasGroup.blocksRaycasts = true;
            MyAudioSource.Play();
        }

        public void HideCanvas()
        {
            DebugLogger.Log("Hiding Canvas.");
            // Hide canvas
            visible = false;
            MyCanvasGroup.interactable = false;
            MyCanvasGroup.blocksRaycasts = false;
        }
    }
}

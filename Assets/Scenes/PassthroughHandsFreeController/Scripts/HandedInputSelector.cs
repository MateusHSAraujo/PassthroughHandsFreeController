
using UnityEngine;
using UnityEngine.EventSystems;

namespace PassthroughHandsFreeController.MainScene
{
    
    public class HandedInputSelector : MonoBehaviour
    {

        public bool DebugMode = false;
        private OVRCameraRig m_cameraRig;
        private OVRInputModule m_inputModule;

        private void Start()
        {
            m_cameraRig = FindFirstObjectByType<OVRCameraRig>();
            m_inputModule = FindFirstObjectByType<OVRInputModule>();
            if (m_cameraRig == null || m_inputModule == null)
            {
                DebugLogger.LogError("OVRCameraRig or OVRInputModule not found in the scene.");
                return;
            }
            m_inputModule.rayTransform = m_cameraRig.centerEyeAnchor;
        }

        private void Update()
        {
            if (DebugMode)
            {
                if (OVRInput.GetActiveController() == OVRInput.Controller.LTouch)
                {
                    SetActiveController(OVRInput.Controller.LTouch);
                }
                else
                {
                    SetActiveController(OVRInput.Controller.RTouch);
                }
            }
            
        }

        private void SetActiveController(OVRInput.Controller c)
        {
            var t = c == OVRInput.Controller.LTouch ? m_cameraRig.leftHandAnchor : m_cameraRig.rightHandAnchor;
            m_inputModule.rayTransform = t;
        }
    }
}

using UnityEngine;

[CreateAssetMenu(fileName = "NewCanvasSettings", menuName = "Canvas/Camera Gaze Controlled Canvas Settings")]
public class CameraGazeControlledCanvasSettings : ScriptableObject
{
    [Tooltip("Fading-in and fade-out animation speed")]
    [SerializeField] public float animationSpeed = 4f;
}

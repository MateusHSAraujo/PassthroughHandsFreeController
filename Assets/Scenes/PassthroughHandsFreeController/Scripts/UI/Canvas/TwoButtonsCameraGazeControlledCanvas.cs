using UnityEngine;
using UnityEngine.UI;

public class TwoButtonsCameraGazeControlledCanvas : CameraGazeControlledCanvas
{
    [Header("Two Buttons Camera Gaze Controlled Canvas configurations:")]
    [Space(5)]
    [SerializeField] public Toggle PrimaryButton;
    [SerializeField] public Toggle SecondaryButton;

    override public void ShowCanvas()
    {
        PrimaryButton.SetIsOnWithoutNotify(false);
        SecondaryButton.SetIsOnWithoutNotify(false);
        base.ShowCanvas();
    }
}

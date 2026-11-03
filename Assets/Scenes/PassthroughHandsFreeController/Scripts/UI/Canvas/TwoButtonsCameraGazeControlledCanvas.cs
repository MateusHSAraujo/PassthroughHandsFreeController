using UnityEngine;
using UnityEngine.UI;

public class TwoButtonsCameraGazeControlledCanvas : CameraGazeControlledCanvas
{
    [Header("Two Buttons Camera Gaze Controlled Canvas configurations:")]
    [Space(5)]
    [SerializeField] public Toggle PrimaryButton;
    [SerializeField] public Toggle SecondaryButton;

    protected override void Awake()
    {
        base.Awake();
        ApplyButtonStyles();
    }

    private void ApplyButtonStyles()
    {
        ConfigureDialogButton(PrimaryButton, GazeButtonStyle.Primary);
        ConfigureDialogButton(SecondaryButton, GazeButtonStyle.Danger);
    }

    private void ConfigureDialogButton(Toggle buttonToggle, GazeButtonStyle style)
    {
        if (buttonToggle == null) return;

        Transform iconTf = buttonToggle.transform.Find("Content/Background/Elements/Icon");
        if (iconTf != null && iconTf.TryGetComponent<Graphic>(out var iconGraphic))
        {
            iconGraphic.color = Color.white;
        }

        Transform labelTf = buttonToggle.transform.Find("Content/Background/Elements/Text/Label");
        if (labelTf != null && labelTf.TryGetComponent<Graphic>(out var labelGraphic))
        {
            labelGraphic.color = Color.white;
        }

        if (buttonToggle.TryGetComponent<ToggleGazeInteractor>(out var interactor))
        {
            interactor.stylePreset = style;
            interactor.InitializeReferences();
            interactor.ApplyStylePresetColors();
            interactor.ResetVisualStateImmediate();
        }
    }

    public override void ShowCanvas()
    {
        PrimaryButton.SetIsOnWithoutNotify(false);
        SecondaryButton.SetIsOnWithoutNotify(false);
        ApplyButtonStyles();
        base.ShowCanvas();
    }
}

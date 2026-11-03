using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineOfSightPositioner))]
public class SelecTargetUI : CameraGazeControlledCanvas
{
    public static SelecTargetUI Instance;

    private float scaleFactor = 0.001f;
    private float minScale;
    private float maxScale = 0.008f;

    [Header("Selec Target UI configurations:")]
    [Space(5)]
    [SerializeField] private Toggle ConfirmButton;
    [SerializeField] private Toggle CancelButton;
    [SerializeField] private Toggle MenuButton;

    protected override void Awake()
    {
        DebugLogger.Log("CameraGazeCursorSelectTarget Awake called.");
        Debug.Assert(Instance == null);
        Instance = this;
        minScale = transform.localScale.x;
        base.Awake();

        ApplyButtonStyles();
    }

    private void ApplyButtonStyles()
    {
        ConfigureDialogButton(ConfirmButton, GazeButtonStyle.Primary);
        ConfigureDialogButton(CancelButton, GazeButtonStyle.Danger);
        ConfigureDialogButton(MenuButton, GazeButtonStyle.SubtleIcon);
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

    public override void ShowCanvasAt(Vector3 position)
    {
        // Distance scaling
        float distance = Vector3.Distance(Positioner.targetCamera.position, position);
        float calculatedScaling = Mathf.Clamp(scaleFactor * distance, minScale, maxScale);
        transform.localScale = Vector3.one * calculatedScaling;

        ConfirmButton.SetIsOnWithoutNotify(false);
        CancelButton.SetIsOnWithoutNotify(false);
        MenuButton.SetIsOnWithoutNotify(false);

        ApplyButtonStyles();
        base.ShowCanvasAt(position);
    }
}

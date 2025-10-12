using System;
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
        // Initialize the canvas or any other setup if needed
        Debug.Log("CameraGazeCursorSelectTarget Awake called.");
        Debug.Assert(Instance == null);
        Instance = this;
        minScale = transform.localScale.x;
        base.Awake();          
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

        base.ShowCanvasAt(position);
    }    
}

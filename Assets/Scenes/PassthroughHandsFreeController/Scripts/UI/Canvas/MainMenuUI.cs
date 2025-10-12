using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(LineOfSightPositioner))]
public class MainMenuUI : CameraGazeControlledCanvas
{
    public static MainMenuUI Instance;
    private bool StartingUp = true;

    [Header("Main Menu UI configurations:")]
    [Space(5)]
    public Toggle MoveButton;
    public Toggle RotateButton;
   
    protected override void Awake()
    {
        // Initialize the canvas or any other setup if needed
        Debug.Log("CameraGazeCursorSelectTarget Awake called.");
        Debug.Assert(Instance == null);
        Instance = this;
        base.Awake();
    }

    public override void ShowCanvas()
    {
        StartCoroutine(WaitToPositionCanvas());
    }

    public override void ShowCanvasAtDirectionOf(Vector3 targetPosition)
    {
        MoveButton.SetIsOnWithoutNotify(false);
        RotateButton.SetIsOnWithoutNotify(false);
        base.ShowCanvasAtDirectionOf(targetPosition);
    }

    // We need to use a coroutine here because it is possible that, during the initialization, 
    // this function is invoked before Meta Quest trackers have been set.
    private IEnumerator WaitToPositionCanvas()
    {
        if (StartingUp)
        {
            yield return new WaitUntil(() => OVRManager.isHmdPresent && OVRManager.tracker.isPositionTracked);
            yield return new WaitForSeconds(1f);
        }
        StartingUp = false;
        base.ShowCanvas();
    }

}

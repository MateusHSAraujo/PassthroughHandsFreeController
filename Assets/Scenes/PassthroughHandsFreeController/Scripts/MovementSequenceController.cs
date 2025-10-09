using System;
using UnityEngine;

public class MovementSequenceController : MonoBehaviour
{
    [Tooltip("The QR code scanner on the scene")]
    [SerializeField] private AnchorManager MyAnchorManager;

    private ResThruAPI ServerAPI;

    [Tooltip("Debug prfed to indicate the center of the wheelchair")]
    [SerializeField] private GameObject d_prefab;

    
    [SerializeField] LineRenderer TrajectoryLine;

    public Action<bool> OnMovementSequenceEnded;

    void Start()
    {
        if (!MyAnchorManager) DebugLogger.LogError("AnchorManager not attributed on Unity.");

        ServerAPI = ResThruAPI.Instance;
        if (!ServerAPI) DebugLogger.LogError("ResThruServer singleton not found.");

        if (!TrajectoryLine) DebugLogger.LogError("Trajectory line renderer not assigned on Unity.");
        else TrajectoryLine.enabled = false;
    }

    public void PerformMovementSequence(Vector3 TargetPosition)
    {
        DebugLogger.Log($"Movement sequence controller triggered: TargetPosition={TargetPosition}");
        
        Transform WheelchairTransform = MyAnchorManager.GetWheelchairTransform();
        DebugLogger.Log($"WheelchairPosition={WheelchairTransform.position}");

        TrajectoryLine.enabled = true;
        TrajectoryLine.SetPosition(0, new Vector3(WheelchairTransform.position.x,TargetPosition.y, WheelchairTransform.position.z));
        TrajectoryLine.SetPosition(1, TargetPosition);
        
        ServerAPI.ScheduleGotoPoint2d(TargetPosition,WheelchairTransform,AbortMovementSequence);
        return;
    }


    private void AbortMovementSequence(bool res)
    {
        DebugLogger.Log($"Aborting movement sequence: res={res}");
        TrajectoryLine.enabled = false;
        OnMovementSequenceEnded?.Invoke(res);
    }
}

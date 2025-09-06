using Meta.XR.MRUtilityKit;
using UnityEngine;

public class WheelchairTrackers : MonoBehaviour
{
    public enum AvailableTags
    {
        LEFT_ARM,
        RIGHT_ARM
    }

    private MRUKTrackable LeftArm;
    private RadialFill canvasLRF;
    private MRUKTrackable RightArm;
    private RadialFill canvasRRF;

    public bool InitiateLeftSide(MRUKTrackable trackable, GameObject scanUIPrefab)
    {
        if (!trackable.PlaneRect.HasValue)
        {
            DebugLogger.LogWarning("Unable to init left side. Trackable without boundry box");
            return false;
        }

        LeftArm = trackable;
        GameObject canvasL = Instantiate(scanUIPrefab, trackable.transform);
        canvasLRF = canvasL.GetComponent<RadialFill>();
        canvasLRF.Initialize(trackable.PlaneRect.Value);
        DebugLogger.Log("Left side initiated");
        return true;
    }

    public bool InitiateRightSide(MRUKTrackable trackable, GameObject scanUIPrefab)
    {
        if (!trackable.PlaneRect.HasValue)
        {
            DebugLogger.LogWarning("Unable to init left side. Trackable without boundry box");
            return false;
        }

        RightArm = trackable;
        GameObject canvasR = Instantiate(scanUIPrefab, trackable.transform);
        canvasRRF = canvasR.GetComponent<RadialFill>();
        canvasRRF.Initialize(trackable.PlaneRect.Value);
        DebugLogger.Log("Right side initiated");
        return true;
    }

    public void DeinitSide(AvailableTags which)
    {
        DebugLogger.Log($"Deiniting {which}");
        if (which == AvailableTags.LEFT_ARM) Destroy(LeftArm.gameObject);
        else Destroy(RightArm.gameObject);
    }

    public bool isReady()
    {
        DebugLogger.Log($"Asserting if wheelchairtrackers are ready: LeftArm={LeftArm} ; RightArm={RightArm} ; canvasLRF={canvasLRF} ; canvasRRF={canvasRRF}");
        return (LeftArm != null) && (RightArm != null) && (canvasLRF != null) && (canvasRRF != null);
    }


    public bool TryGetPosition(AvailableTags which, out Vector3 pos)
    {
        bool ans = true;
        pos = Vector3.zero;
        if (which == AvailableTags.LEFT_ARM && LeftArm)
        {
            pos = LeftArm.transform.position;
        }
        else if (which == AvailableTags.RIGHT_ARM && RightArm)
        {
            pos = RightArm.transform.position;
        }
        else
        {
            ans = false;
        }
        return ans;
    }

    public bool TryGetRotation(AvailableTags which, out Quaternion rot)
    {
        bool ans = true;
        rot = Quaternion.identity;
        if (which == AvailableTags.LEFT_ARM && LeftArm)
        {
            rot = LeftArm.transform.rotation;
        }
        else if (which == AvailableTags.RIGHT_ARM && RightArm)
        {
            rot = RightArm.transform.rotation;
        }
        else
        {
            ans = false;
        }
        return ans;
    }

    public void StartCanvasAnimation(AvailableTags which, float fillingDuration)
    {
        DebugLogger.Log($"Start canvas animation for {which}");
        RadialFill canvas = (which == AvailableTags.LEFT_ARM) ? canvasLRF : canvasRRF;
        canvas.StartFilling(fillingDuration);
    }

    public void ResetCanvasAnimation(AvailableTags which)
    {
        DebugLogger.Log($"Reset canvas animation for {which}");
        RadialFill canvas = (which == AvailableTags.LEFT_ARM) ? canvasLRF : canvasRRF;
        canvas.ResetFilling();
    }

    public void ShowCanvasOnSide(AvailableTags which)
    {
        DebugLogger.Log($"Showing canvas for {which}");
        RadialFill canvas = (which == AvailableTags.LEFT_ARM) ? canvasLRF : canvasRRF;
        canvas.ShowCanvas();
    }

    public void HideCanvasOnSide(AvailableTags which)
    {
        DebugLogger.Log($"Hiding canvas for {which}");
        RadialFill canvas = (which == AvailableTags.LEFT_ARM) ? canvasLRF : canvasRRF;
        canvas.HideCanvas();
        canvas.ResetFilling();
    }

    public void HideCanvasOnBothSides()
    {
        HideCanvasOnSide(AvailableTags.LEFT_ARM);
        HideCanvasOnSide(AvailableTags.RIGHT_ARM);
    }
    
}

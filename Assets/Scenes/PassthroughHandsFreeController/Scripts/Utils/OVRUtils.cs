// TODO : fix name typo
using UnityEngine;

public static class OVRUtils
{
    public static OVRCameraRig CameraRig { get; private set; }
    public static Transform CenterEyeAnchor { get; private set; }

    static OVRUtils()
    {
       
        CameraRig = Object.FindFirstObjectByType<OVRCameraRig>();

        if (CameraRig != null)
        {
            CenterEyeAnchor = CameraRig.centerEyeAnchor;
        }
        else
        {
            Debug.LogError("OVRUtils couldn't find CameraRig");
        }
    }
}
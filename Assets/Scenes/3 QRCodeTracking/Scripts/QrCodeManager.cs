using System.Collections.Generic;
using PassthroughCameraSamples;
using UnityEngine;
using Meta.XR;
using System;
using ZXing.QrCode.Internal;

public class QrCodeDisplayManager : MonoBehaviour
{
#if ZXING_ENABLED
    [SerializeField] private QrCodeScanner scanner;
    [SerializeField] private EnvironmentRaycastManager envRaycastManager;

    [SerializeField] private LineRendererDebugTool LineRendererDebugL;
    [SerializeField] private LineRendererDebugTool LineRendererDebugR;

    private WebCamTextureManager _webCamTextureManager;
    private PassthroughCameraEye _passthroughCameraEye;

    private class QrCodePose
    {
        public Vector3 up;
        public Vector3 right;
        public Vector3 normal;
        public Vector3 center;

        public QrCodePose(Vector3 up, Vector3 right, Vector3 normal, Vector3 center)
        {
            this.up = up;
            this.right = right;
            this.normal = normal;
            this.center = center;
        }

        public QrCodePose()
        {
            this.up = Vector3.zero;
            this.right = Vector3.zero;
            this.normal = Vector3.zero;
            this.center = Vector3.zero;
        }

        public static QrCodePose operator +(QrCodePose a, QrCodePose b)
        {
            return new QrCodePose(
                a.up + b.up,
                a.right + b.right,
                a.normal + b.normal,
                a.center + b.center
            );
        }

        public static QrCodePose operator /(QrCodePose a, int b)
        {
            return new QrCodePose(
                a.up / b,
                a.right / b,
                a.normal / b,
                a.center / b
            );
        }
    }

    private Queue<QrCodePose> poseWindow_l = new Queue<QrCodePose>();
    private Queue<QrCodePose> poseWindow_r = new Queue<QrCodePose>();

    private bool poseRequested = false;

    [SerializeField, Range(1, 10)] private int poseWindowSize = 10;

    private void Awake()
    {
        _webCamTextureManager = FindAnyObjectByType<WebCamTextureManager>();
        _passthroughCameraEye = _webCamTextureManager.Eye;
    }

    private void Update()
    {
        ScanMarkers();
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
        {
            poseRequested = true;
        }

        if (poseRequested && poseWindow_l.Count == poseWindowSize && poseWindow_r.Count == poseWindowSize)
        {
            ReturnEstimation();
            poseRequested = false;
        }
        DebugLogger.Log($"current poseWindow count: L -> {poseWindow_l.Count} , R -> {poseWindow_r.Count}");
    }

    private async void ScanMarkers()
    {
        var qrResults = await scanner.ScanFrameAsync() ?? Array.Empty<QrCodeResult>();
        foreach (var qrResult in qrResults)
        {
            if (qrResult?.corners != null && qrResult.corners.Length == 4)
            {
                DebugLogger.Log($"qrResults recieved. Processing...");
                Queue<QrCodePose> destQueue = null;
                if (qrResult.text == "ADROBLAB_WELLCHAIR_L")
                {
                    DebugLogger.Log("Left arm QR Code read. Setting left queue");
                    destQueue = poseWindow_l;
                    LineRendererDebugL.gameObject.SetActive(true);
                    LineRendererDebugR.gameObject.SetActive(false);
                }
                else if (qrResult.text == "ADROBLAB_WELLCHAIR_R")
                {
                    DebugLogger.Log("Right arm QR Code read. Setting right queue");
                    destQueue = poseWindow_r;
                    LineRendererDebugR.gameObject.SetActive(true);
                    LineRendererDebugL.gameObject.SetActive(false);
                }
                else
                {
                    DebugLogger.Log("Invalid QR code read. Ignoring");
                    LineRendererDebugL.gameObject.SetActive(false);
                    LineRendererDebugR.gameObject.SetActive(false);
                    continue;
                }

                var count = qrResult.corners.Length;
                var uvs = qrResult.corners;
                for (var i = 0; i < count; i++)
                {
                    uvs[i] = new Vector2(qrResult.corners[i].x, qrResult.corners[i].y);
                }

                var centerUV = Vector2.zero;
                foreach (var uv in uvs) centerUV += uv;
                centerUV /= count;

                var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(_passthroughCameraEye);
                var centerPixel = new Vector2Int(
                    Mathf.RoundToInt(centerUV.x * intrinsics.Resolution.x),
                    Mathf.RoundToInt(centerUV.y * intrinsics.Resolution.y)
                );

                var centerRay = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, centerPixel);
                if (!envRaycastManager || !envRaycastManager.Raycast(centerRay, out var centerHitInfo))
                {
                    DebugLogger.LogWarning("EnviromentRaycaster null or the ray did not hit any object.");
                    return;
                }
                DebugLogger.Log($"Calculating worldCorners...");
                var center = centerHitInfo.point;
                var distance = Vector3.Distance(centerRay.origin, center);
                var worldCorners = new Vector3[count];

                for (var i = 0; i < count; i++)
                {
                    var pixelCoord = new Vector2Int(
                        Mathf.RoundToInt(uvs[i].x * intrinsics.Resolution.x),
                        Mathf.RoundToInt(uvs[i].y * intrinsics.Resolution.y)
                    );
                    var r = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, pixelCoord);


                    if (envRaycastManager.Raycast(r, out var cornerHit))
                    {
                        worldCorners[i] = cornerHit.point;
                    }
                    else
                    {
                        worldCorners[i] = r.origin + r.direction * distance;
                    }
                }
                // Pose estimation
                center = Vector3.zero;
                foreach (var c in worldCorners)
                {
                    center += c;
                }
                center /= count;

                var up = (worldCorners[1] - worldCorners[0]).normalized;
                var right = (worldCorners[2] - worldCorners[1]).normalized;
                var normal = -Vector3.Cross(up, right).normalized;
                var poseRot = Quaternion.LookRotation(normal, up);

                if (destQueue.Count >= poseWindowSize) destQueue.Dequeue();

                DebugLogger.Log($"Enqueuing new qrcode pose");
                destQueue.Enqueue(new QrCodePose(up, right, normal, center));

                if (qrResult.text == "ADROBLAB_WELLCHAIR_L")
                {
                    LineRendererDebugL.UpdateAll(center, up, right, normal);
                }
                else
                {
                    LineRendererDebugR.UpdateAll(center, up, right, normal);
                }
            }

            else
            {
                DebugLogger.LogWarning("No QR code detected.");
                return;
            }
        }


    }

    private void ReturnEstimation()
    {
        DebugLogger.Log($"Iterating over queue.");

        QrCodePose meanQrPoseL = CalculateMeanQrCodePose(poseWindow_l);
        QrCodePose meanQrPoseR = CalculateMeanQrCodePose(poseWindow_r);
        
        poseWindow_l.Clear();
        poseWindow_r.Clear();

        
        Debug.Log($"Positioning debugs");
        LineRendererDebugL.UpdateAll(meanQrPoseL.center, meanQrPoseL.up, meanQrPoseL.right, meanQrPoseL.normal);

        LineRendererDebugR.UpdateAll(meanQrPoseR.center, meanQrPoseR.up, meanQrPoseR.right, meanQrPoseR.normal);
    }

    private QrCodePose CalculateMeanQrCodePose(Queue<QrCodePose> queue)
    {
        QrCodePose meanQrPose = new();
        var count = queue.Count;
        foreach (QrCodePose itr in queue)
        {
            meanQrPose += itr;
        }
        meanQrPose /= count;
        return meanQrPose;
    }
#endif
}

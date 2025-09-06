using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Threading.Tasks;
using System.Threading;

public class QrCodeManager : MonoBehaviour
{
    [Tooltip("Debug Prefab attached to each marker")]
    [SerializeField] private GameObject _prefab;

    [Tooltip("Scan UI Prefab attached to each marker")]
    [SerializeField] private GameObject scanUIPrefab;


    [Tooltip("Time, in seconds, to scan to guarantee markers stability.")]
    [SerializeField] private float scanTimeout = 5.0f;

    [Tooltip("Distance threshold for stopping the scan.")]
    [SerializeField] private float distanceThreshold = 0.1f;

    [Tooltip("Rotation treshold for stopping the scan")]
    [SerializeField] private float rotationThreshold = 1.0f;

    [Tooltip("Timeout, in seconds, to cancel position calculation.")]
    [SerializeField] private float calculatePositionTimeout = 20.0f;

    [Header("Translation info")]
    [SerializeField] private float LeftArmFowardTranslation ;
    [SerializeField] private float LeftArmRightTranslation ;
    [SerializeField] private float RightArmFowardTranslation ;
    [SerializeField] private float RightArmRightTranslation ;

    // Debug options:
    [Header("Debug options")]
    [Tooltip("Enable this for debuging")]
    [SerializeField] private bool debug = false;


    private struct StabilityResult
    {
        public readonly bool stable;
        public readonly Vector3 pos;
        public readonly Quaternion rot;

        public StabilityResult(bool stable, Vector3 pos, Quaternion rot)
        {
            this.stable = stable;
            this.pos = pos;
            this.rot = rot;
        }

        public StabilityResult(bool stable)
        {
            this.stable = stable;
            this.pos = Vector3.zero;
            this.rot = Quaternion.identity;
        }

        public override string ToString()
        {
            return $"(stable={stable} , pos={pos}, rot={rot})";
        }
    }

    [SerializeField] private WheelchairTrackers m_WheelchairTrackers;
    private CancellationTokenSource _calculatePositionCancellationSource;
    private bool _isCalculating = false;

    public async Task<Vector3?> TriggerWheelchairPositionCalculation()
    {
        bool trackersReady = m_WheelchairTrackers.isReady();
        Vector3? WheelchairPosition = null;
        if (trackersReady && !_isCalculating)
        {
            DebugLogger.Log("Position calculation triggered. Starting.");
            WheelchairPosition = await CalculatePosition();
            if (WheelchairPosition.HasValue)
            {
                DebugLogger.Log($"Position calculation completed with success. Returning {WheelchairPosition}");
            }
        }
        else if (!trackersReady)
        {
            DebugLogger.LogWarning($"Wheelchair trackers not ready yet. Returning {WheelchairPosition}");
        }
        else
        {
            DebugLogger.LogWarning($"Wheelchair trackers not ready yet. Returning {WheelchairPosition}");
        }
        return WheelchairPosition;
    }

    private async Task<Vector3?> CalculatePosition()
    {
        _isCalculating = true;
        _calculatePositionCancellationSource = new CancellationTokenSource(System.TimeSpan.FromSeconds(calculatePositionTimeout));
        Vector3? WheelchairPosition = null;
        try
        {
            DebugLogger.Log("Starting position calculation.");

            Task<StabilityResult> leftSideTask = CalculatePositionParallel(WheelchairTrackers.AvailableTags.LEFT_ARM, _calculatePositionCancellationSource.Token);
            Task<StabilityResult> rightSideTask = CalculatePositionParallel(WheelchairTrackers.AvailableTags.RIGHT_ARM, _calculatePositionCancellationSource.Token);

            StabilityResult[] results = await Task.WhenAll(leftSideTask, rightSideTask);

            DebugLogger.Log($"Parallel position calculation ended. Results: {WheelchairTrackers.AvailableTags.LEFT_ARM}={results[0]} ; {WheelchairTrackers.AvailableTags.RIGHT_ARM}={results[1]}");
            m_WheelchairTrackers.HideCanvasOnBothSides();
            WheelchairPosition = CaculateWheelchairCenterPositon(results[0], results[1]);
            DebugLogger.Log($"Wheelchair center position calculation ={WheelchairPosition}");
        }
        catch (System.Exception ex)
        {
            DebugLogger.LogError($"Calculate position timed out: {ex.Message}");
        }
        finally
        {
            _calculatePositionCancellationSource.Dispose();
            _calculatePositionCancellationSource = null;
            _isCalculating = false;
        }
        return WheelchairPosition;

    }


    private async Task<StabilityResult> CalculatePositionParallel(WheelchairTrackers.AvailableTags which, CancellationToken token)
    {
        DebugLogger.Log($"Starting parallel position calculation. which={which}");
        bool ready = false;
        StabilityResult result = new(false);
        m_WheelchairTrackers.ShowCanvasOnSide(which);
        while (!ready)
        {
            // Breake the execution if timeout happen
            token.ThrowIfCancellationRequested();

            // Firing task and getting result
            DebugLogger.Log($"Firing scanning tasks. which={which} ; ready={ready}");

            result = await StabilityCheck(which, token);
            ready = result.stable;
        }
        DebugLogger.Log($"This side is stable. Result: {which}={result}");
        return result;
    }


    private async Task<StabilityResult> StabilityCheck(WheelchairTrackers.AvailableTags which, CancellationToken token)
    {
        bool validPos = m_WheelchairTrackers.TryGetPosition(which, out Vector3 pos_i);
        bool validRot = m_WheelchairTrackers.TryGetRotation(which, out Quaternion rot_i);
        StabilityResult standardFailReturn = new(false);

        if (!validPos || !validRot)
        {
            DebugLogger.Log($"{which} Stability Check failed. validPosI={validPos} ; validRotI={validRot}");
            return new StabilityResult(false);
        }

        float timePassed = 0.0f;
        DebugLogger.Log($"Starting timer and animation. scanTimeout={scanTimeout}");
        m_WheelchairTrackers.StartCanvasAnimation(which, scanTimeout);
        while (timePassed < scanTimeout)
        {
            // Add timer
            timePassed += Time.deltaTime;
            DebugLogger.Log($"which = {which} ; timePassed = {timePassed} ; cancelled = {token.IsCancellationRequested}");

            // Cancel execution if timeout happened
            token.ThrowIfCancellationRequested();

            // Get current position and rotation
            validPos = m_WheelchairTrackers.TryGetPosition(which, out Vector3 pos);
            validRot = m_WheelchairTrackers.TryGetRotation(which, out Quaternion rot);

            float distance = Vector3.Distance(pos_i, pos);
            float rotation = Quaternion.Angle(rot, rot_i);
            bool thresholdChecksFailed = false;

            // Check thresholds
            if (!validPos || !validRot)
            {
                DebugLogger.Log($"{which} Stability Check failed. validPos={validPos} ; validRot={validRot}");
                thresholdChecksFailed = true;
            }
            else if (Vector3.Distance(pos_i, pos) > distanceThreshold)
            {
                DebugLogger.Log($"{which} Stability Check failed. Distance threshold broke. pos_i={pos_i} ; pos={pos} ; distance={distance}");
                thresholdChecksFailed = true;
            }
            else if (Quaternion.Angle(rot, rot_i) > rotationThreshold)
            {
                DebugLogger.Log($"{which} Stability Check failed. Rotation threshold broke. rot_i={rot_i} ; rot={rot} ; rotarion={rotation}");
                thresholdChecksFailed = true;
            }

            if (thresholdChecksFailed)
            {
                m_WheelchairTrackers.ResetCanvasAnimation(which);
                return standardFailReturn;
            }
            else
            {
                await Task.Yield();
            }

        }
        DebugLogger.Log($"Timer ended and no major oscilation happened. which={which}");
        return new StabilityResult(true, pos_i, rot_i);
    }
    

    private Vector3 CaculateWheelchairCenterPositon(StabilityResult leftResult, StabilityResult rightResult)
    {
        Vector3 centerPositon = Vector3.zero;
        DebugLogger.Log($"Calculating wheelchair position based on translation info. leftResult={leftResult} ; rightResult={rightResult}");
        Vector3 leftContribution = (leftResult.rot*Vector3.forward).normalized * LeftArmFowardTranslation + -1*(leftResult.rot*Vector3.right).normalized * LeftArmRightTranslation + leftResult.pos;
        Vector3 rightContribution = (rightResult.rot*Vector3.forward).normalized * RightArmFowardTranslation + -1*(rightResult.rot*Vector3.right).normalized * RightArmRightTranslation + rightResult.pos;
        centerPositon = (leftContribution + rightContribution) / 2;
        return centerPositon;
    }

    public void OnTrackableAdded(MRUKTrackable trackable)
    {
        DebugLogger.Log($"QrCodeManager OnTrackableAdded invoked");
        if (trackable.TrackableType == OVRAnchor.TrackableType.QRCode && trackable.MarkerPayloadString != null)
        {
            DebugLogger.Log($"Detected QR code: {trackable.MarkerPayloadString}");
            if (trackable.MarkerPayloadString == "ADROBLAB_WELLCHAIR_L")
            {
                DebugLogger.Log($"Left arm detected. Tracker positioned");

                if (debug)
                {
                    Instantiate(_prefab, trackable.transform);
                }

                if (!m_WheelchairTrackers.InitiateLeftSide(trackable, scanUIPrefab))
                {
                    DebugLogger.Log($"Something when wrong during left side initiation");
                    OnTrackableRemoved(trackable);
                    return;
                }

            }
            else if (trackable.MarkerPayloadString == "ADROBLAB_WELLCHAIR_R")
            {
                DebugLogger.Log($"Right arm detected. Tracker Positioned");

                if (debug)
                {
                    Instantiate(_prefab, trackable.transform);
                }

                if (!m_WheelchairTrackers.InitiateRightSide(trackable, scanUIPrefab))
                {
                    DebugLogger.Log($"Something when wrong during left side initiation");
                    OnTrackableRemoved(trackable);
                    return;
                }
            }
            else
            {
                DebugLogger.Log($"Invalid QR Code detected. Skipping");
            }
        }
    }


    public void OnTrackableRemoved(MRUKTrackable trackable)
    {
        if (!trackable)
        {
            DebugLogger.Log($"Trackable null. Ignoring");
            return;
        }

        DebugLogger.Log($"QrCodeManager OnTrackableRemoved invoked");
        DebugLogger.Log($"Trackable removed: {trackable.name}");
        if (trackable.MarkerPayloadString == "ADROBLAB_WELLCHAIR_L")
        {
            DebugLogger.Log($"Deleting left arm tracker");
            m_WheelchairTrackers.DeinitSide(WheelchairTrackers.AvailableTags.LEFT_ARM);
        }
        else if (trackable.MarkerPayloadString == "ADROBLAB_WELLCHAIR_R")
        {
            DebugLogger.Log($"Deleting right arm tracker");
            m_WheelchairTrackers.DeinitSide(WheelchairTrackers.AvailableTags.RIGHT_ARM);
        }
        else
        {
            Destroy(trackable.gameObject);
        }
    }

}

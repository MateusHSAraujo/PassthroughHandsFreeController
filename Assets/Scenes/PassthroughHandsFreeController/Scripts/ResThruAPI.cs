using System; 
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Threading;

public class ResThruAPI : MonoBehaviour
{
    public static ResThruAPI Instance;

    #region Endpoints and payloads
    //private readonly string LinearDisplacementEndpoint = "http://192.168.0.2:8080/motion/displ";
    //private readonly string LinearDisplacementStatusEndpoint = "http://192.168.0.2:8080/motion/displ/status";
    //private readonly string HeadingEndpoint = "http://192.168.0.2:8080/motion/heading";
    //private readonly string HeadingStatusEndpoint = "http://192.168.0.2:8080/motion/heading/status";
    //private readonly string PoseEndpoint = "http://192.168.0.2:8080/motion/pose";
    private readonly string Velocity2Endpoint = "http://192.168.0.2:8080/motion/vel2";
    private readonly string StopEndpoint = "http://192.168.0.2:8080/motion/stop";


    private readonly string POST = "POST";
    private readonly string PUT = "PUT";
    private readonly string GET = "GET";

    [System.Serializable] private class LinearDisplacementPayload { public float displ; }
    [System.Serializable] private class HeadingPayload { public float heading; }

    [System.Serializable]
    private class VelocityData
    {
        public float right;
        public float left;

        public VelocityData() { }

        public VelocityData(float r, float l)
        {
            right = r;
            left = l;
        }
    }
    [System.Serializable]
    private class Velocity2Payload
    {
        public VelocityData vel2;

        public Velocity2Payload() { }

        public Velocity2Payload(float r, float l)
        {
            vel2 = new(r, l);
        }
    }

    [System.Serializable]
    private class PoseData
    {
        public float x { get; private set; }
        public float y { get; private set; }
        public float th { get; private set; }

        public PoseData(float x, float y, float th)
        {
            this.x = x;
            this.y = y;
            this.th = th;
        }
    }
    [System.Serializable]
    private class PosePayload
    {
        public PoseData pose { get; private set; }

        public PosePayload(float x, float y, float th)
        {
            pose = new(x, y, th);
        }
    }
    #endregion

    private enum APIStates
    {
        BUSY,
        IDLE,
        EMERGENCY_STOP
    }

    private APIStates State;
    private CancellationTokenSource currtOperationCt;

    #region Unity standard functions
    void Awake()
    {
        DebugLogger.Log("ResThruAPI Awake called.");
        Debug.Assert(Instance == null);
        Instance = this;
        State = APIStates.IDLE;
    }

    void Update()
    {
        // Emergency stop
        if (OVRInput.GetDown(OVRInput.Button.Four) &&
            OVRInput.GetDown(OVRInput.Button.Three))
        {
            if (State != APIStates.EMERGENCY_STOP)
            {
                DebugLogger.LogWarning("Emergency stop triggered. Stoping");
                State = APIStates.EMERGENCY_STOP;
                _ = ResThruServer.SendPayloadToServer(StopEndpoint, PUT, "");
                currtOperationCt.Cancel();
            }
        }
    }
    #endregion
    
    #region Operations scheduler
    private async void ScheduleOperation(Func<CancellationToken, Task<bool>> operation, Action<bool> onCompleted)
    {
        DebugLogger.Log($"Scheduling new operation: {operation}");
        if (State != APIStates.IDLE)
        {
            DebugLogger.LogWarning("API not in idle. Ignoring request");
            onCompleted?.Invoke(false);
        }
        else State = APIStates.BUSY;

        currtOperationCt?.Dispose();
        currtOperationCt = new CancellationTokenSource();

        try
        {
            bool res = await operation(currtOperationCt.Token);
            onCompleted?.Invoke(res);
        }
        catch (OperationCanceledException)
        {
            DebugLogger.LogWarning("Last operation was canceled");
            onCompleted?.Invoke(false);
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"An error occurred during operation execution: {ex.Message}");
            onCompleted?.Invoke(false);
        }
        finally
        {
            if (State == APIStates.EMERGENCY_STOP)
            {
                _ = await ResThruServer.SendPayloadToServer(StopEndpoint, PUT, "");
            }
            currtOperationCt?.Dispose();
            currtOperationCt = null;
            State = APIStates.IDLE;
        }

    }


    public void ScheduleGotoPoint2d(Vector3 targetPosition, Transform robotTransform, Action<bool> onCompleted)
    {
        ScheduleOperation(token => GotoPoint2D(new Vector2(targetPosition.x, targetPosition.z), robotTransform, token), onCompleted);
    }
    #endregion

    #region Operations
    [Header("GotoPoint Control parameters")]
    [Tooltip("Tolerance in degrees to consider the heading reached")]
    [SerializeField] private float HeadingTolerance = 5;
    [Tooltip("Tolerance in m to consider the final goal point reached")]
    [SerializeField] private float TravelTolerance = 0.1f;

    [Tooltip("First aligment rotation speed (deg/s)")]
    [SerializeField] private float Vrot = 50;

    [Tooltip("Linear travel speed (mm/s)")]
    [SerializeField] private float Vlin = 100;

    [Tooltip("Control period (sec)")]
    [SerializeField] private float dt = 0.5f;

    [Tooltip("Proportional gain for distance error")]
    [SerializeField] private float Kdist = 0.1f;
    [Tooltip("Proportional gain for heading error")]
    [SerializeField] private float Khead = 15;
    [Tooltip("Integral gain for distance error")]
    [SerializeField] private float Kint = 0.001f;

    private async Task<bool> GotoPoint2D(Vector2 TargetPosition, Transform RobotTransform, CancellationToken ct)
    {
        DebugLogger.Log($"TargetPosition={TargetPosition} ; RobotTransform.position = {RobotTransform.position} ; RobotTransform.foward = {RobotTransform.forward}");

        Velocity2Payload vel2;
        string jsonData;
        ResThruServer.ServerResponse response;

        // First, rotate to face the goal point
        Vector2 RobotFoward2 = new(RobotTransform.forward.x, RobotTransform.forward.z);
        Vector2 Distance2 = new(TargetPosition.x - RobotTransform.position.x, TargetPosition.y - RobotTransform.position.z);
        float Angle = Vector2.SignedAngle(RobotFoward2, Distance2);

        if (Math.Abs(Angle) > HeadingTolerance)
        {
            vel2 = (Angle > 0) ? new(Vrot, -Vrot) : new(-Vrot, Vrot);
            jsonData = JsonUtility.ToJson(vel2);

            response = await ResThruServer.SendPayloadToServer(Velocity2Endpoint, PUT, jsonData);

            if (response.Result != UnityWebRequest.Result.Success)
            {
                DebugLogger.LogError($"Unable to start initial rotation. response={response}");
                return false;
            }

            while (Math.Abs(Angle) > HeadingTolerance)
            {
                ct.ThrowIfCancellationRequested(); // Throw exception if task cancel was requested

                RobotFoward2 = new(RobotTransform.forward.x, RobotTransform.forward.z);
                Distance2 = new(TargetPosition.x - RobotTransform.position.x, TargetPosition.y - RobotTransform.position.z);
                Angle = Vector2.SignedAngle(RobotFoward2, Distance2);

                DebugLogger.Log($"Rotating. Angle={Angle}");

                await Task.Delay((int)(dt * 500), ct);
            }
        }
        // Robot is aligned 

        // Then, move to target
        vel2 = new(Vlin, Vlin);
        jsonData = JsonUtility.ToJson(vel2);
        response = await ResThruServer.SendPayloadToServer(Velocity2Endpoint, PUT, jsonData);


        if (response.Result != UnityWebRequest.Result.Success)
        {
            DebugLogger.LogError($"Unable to start linear movement. response={response}");
            return false;
        }
        
        Vector2 InitialPos = new(RobotTransform.position.x, RobotTransform.position.z);
        Vector2 IdealTrajectory = TargetPosition - InitialPos;

        float prevErrorDist = 0f;
        float errorInt = 0f;
        float currKdist = Kdist;
        float currKhead = Khead;
        float currdt = dt * 1000;

        while (true)
        {
            ct.ThrowIfCancellationRequested(); // Throw exception if task cancel was requested

            Vector2 CurrentPos = new(RobotTransform.position.x, RobotTransform.position.z);

            Vector2 CurrentDistance = TargetPosition - CurrentPos;

            if (CurrentDistance.magnitude < TravelTolerance) break;

            // Heading error
            RobotFoward2 = new(RobotTransform.forward.x, RobotTransform.forward.z);
            float errorHead = Vector2.SignedAngle(RobotFoward2, IdealTrajectory) * Mathf.Deg2Rad;

            // Lateral distance error
            Vector2 vectorFromStartToCurrent = CurrentPos - InitialPos;
            Vector2 IdealTrajectoryDir = IdealTrajectory.normalized;
            Vector2 pointOnIdealTrajectory = Vector2.Dot(vectorFromStartToCurrent, IdealTrajectoryDir) * IdealTrajectoryDir;
            float errorDist = Vector2.Distance(vectorFromStartToCurrent, pointOnIdealTrajectory);

            // Cross vector to identify if we the ideal trajectory is at our left or our ritgh
            float crossProduct2D = (IdealTrajectoryDir.x * vectorFromStartToCurrent.y) - (IdealTrajectoryDir.y * vectorFromStartToCurrent.x);
            if (crossProduct2D < 0)
            {
                errorDist *= -1;
            }

            // Suavization filter
            errorDist = 0.4f * prevErrorDist + 0.6f * errorDist;
            prevErrorDist = errorDist;

            // Acumulate integral error
            errorInt += errorDist * (currdt/1000); // Should dt be used here ?
            // Clamping to prevent integral windup
            errorInt = Mathf.Clamp(errorInt, -20f, 20f);

            // Calculating velocity correction
            float Delta = currKdist * errorDist - currKhead * errorHead + Kint * errorInt;
            float Vr = Vlin - Delta;
            float Vl = Vlin + Delta;


            // Frequency reduction if we are getting close to the target
            if (CurrentDistance.magnitude < 5 * TravelTolerance)
            {
                Vr /= 2;
                Vl /= 2;
                currdt = dt * 1000 / 4;

                // Gain reduction to low velocity scenario
                currKdist = Kdist / 2;
                currKhead = Khead / 2;
            }

            // Logs for debuging PID controller
            DebugLogger.LogWarning($@"
                <control_info>
                {{
                ""CurrentPos"":""{CurrentPos}"",
                ""CurrentDistance"":""{CurrentDistance}"",
                ""TargetPosition"":""{TargetPosition}"",
                ""ErrorHead"":""{errorHead}"",
                ""ErrorDistance"":""{errorDist}"",
                ""ErrorInt"":""{errorInt}"",
                ""currKdist"":""{currKdist}"",
                ""currKhead"":""{currKhead}"",
                ""currKdist*errorDist"":""{Kdist * errorDist}"",
                ""currKhead*errorHead"":""{Khead * errorHead}"",
                ""Kint*errorInt"":""{Kint * errorInt}"",
                ""Vr"":{Vr},
                ""Vl"":{Vl},
                ""currdt"":{currdt}
                }}
                </control_info>
            ");

            // We will measure how much time the request took to be answered and discard this time from the delay
            Stopwatch stopwatch = new Stopwatch();

            // Sending new velocity command
            vel2 = new Velocity2Payload(Vr, Vl);
            jsonData = JsonUtility.ToJson(vel2);

            stopwatch.Start();
            response = await ResThruServer.SendPayloadToServer(Velocity2Endpoint, PUT, jsonData);
            stopwatch.Stop();
            DebugLogger.Log($"Last control action took {stopwatch.ElapsedMilliseconds} to be answered by the server. respose={response}");
            float delayTime = currdt;
            delayTime = (stopwatch.ElapsedMilliseconds < delayTime) ? delayTime - stopwatch.ElapsedMilliseconds : 0;

            await Task.Delay((int)delayTime, ct);
        }

        // We've arrived. Stop movement.
        response = await ResThruServer.SendPayloadToServer(StopEndpoint, PUT, "");
        if (response.Result != UnityWebRequest.Result.Success) DebugLogger.LogError($"Error while stopping robot after initial rotation: response={response}");

        return true;
    }
    #endregion
}

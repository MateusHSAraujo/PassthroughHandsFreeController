using System; 
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Threading;

// TODO : Fix name typo
public class RestThruAPI : MonoBehaviour
{
    public static RestThruAPI Instance;

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
        DebugLogger.Log("RestThruAPI Awake called.");
        Debug.Assert(Instance == null);
        Instance = this;
        DebugLogger.LogWarning("State -> IDLE");
        State = APIStates.IDLE;
    }

    void Update()
    {
        // Emergency stop
        
        if (OVRInput.Get(OVRInput.RawButton.X) &&  OVRInput.Get(OVRInput.RawButton.Y))
        {
            if (State == APIStates.BUSY)
            {
                DebugLogger.LogWarning("Emergency stop triggered. Stoping");
                DebugLogger.LogWarning("State -> EMERGENCY_STOP");
                State = APIStates.EMERGENCY_STOP;
                currtOperationCt.Cancel();
            }
        }
    }
    #endregion

    #region Operations scheduler
    private async void ScheduleOperation(Func<CancellationToken, Task<bool>> operation, Action<bool> onCompleted)
    {
        bool res = false;
        DebugLogger.Log($"Scheduling new operation: {operation}");
        if (State != APIStates.IDLE)
        {
            DebugLogger.LogWarning("API not in idle. Ignoring request");
            onCompleted?.Invoke(res);
            return;
        }
        else
        {
            DebugLogger.LogWarning("State -> BUSY");
            State = APIStates.BUSY;
            currtOperationCt?.Dispose();
            currtOperationCt = new CancellationTokenSource();

            try
            {
                res = await operation(currtOperationCt.Token);
            }
            catch (OperationCanceledException)
            {
                DebugLogger.LogWarning("Last operation was canceled");
                res = false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"An error occurred during operation execution: {ex.Message}");
                res = false;
            }
            finally
            {
                if (State == APIStates.EMERGENCY_STOP)
                {
                    try
                    {
                        await ForceStopRobot();
                    }
                    catch (RestThruServer.ServerConnectionException ex)
                    {
                        DebugLogger.LogError($"Couldn't reach server to emergency stop. {ex}");
                    }
                }
                currtOperationCt?.Dispose();
                currtOperationCt = null;
                onCompleted?.Invoke(res);
                DebugLogger.LogWarning("State -> IDLE");
                State = APIStates.IDLE;
            }
            return;
        }

    }

    public void CancelLastOperation()
    {
        DebugLogger.Log("Canceling last operation");
        currtOperationCt?.Cancel();
    }

    public void ScheduleGotoPoint2d(Vector3 targetPosition, Transform RobotTransform, Action<bool> onCompleted)
    {
        ScheduleOperation(token => GotoPoint2D(new Vector2(targetPosition.x, targetPosition.z), RobotTransform, token), onCompleted);
    }

    public void ScheduleAlignHeading2D(Transform TransformToFollow, Transform RobotTransform, Action<bool> onCompleted)
    {
        ScheduleOperation(token => AlignHeading2D(TransformToFollow, RobotTransform, token), onCompleted);
    }

    #endregion

    #region Operations

    #region GotoPoint

    [System.Serializable]
    private class GotoPoint2DConfig
    {
        [Tooltip("Tolerance in degrees to consider the heading reached")]
        public float HeadingTolerance = 5;

        [Tooltip("Tolerance in m to consider the final goal point reached")]
        public float TravelTolerance = 0.1f;

        [Tooltip("First aligment rotation speed (deg/s)")]
        public float Vrot = 50;

        [Tooltip("Linear travel speed (mm/s)")]
        public float Vlin = 100;

        [Tooltip("Control period (sec)")]
        public float dt = 0.5f;

        [Tooltip("Proportional gain for distance error")]
        public float Kdist = 100;

        [Tooltip("Proportional gain for heading error")]
        public float Khead = 0.1f;

        [Tooltip("Integral gain for distance error")]
        public float Kint = 0.01f;
    }

    [SerializeField] private GotoPoint2DConfig GotoPoint2DConfigurations;

    private async Task<bool> GotoPoint2D(Vector2 TargetPosition, Transform RobotTransform, CancellationToken ct)
    {
        DebugLogger.Log($"TargetPosition={TargetPosition} ; RobotTransform.position = {RobotTransform.position} ; RobotTransform.foward = {RobotTransform.forward}");

        // ==================================================================
        // Deconstructing config to local variables
        float HeadingTolerance = GotoPoint2DConfigurations.HeadingTolerance;
        float TravelTolerance = GotoPoint2DConfigurations.TravelTolerance;
        float Vrot = GotoPoint2DConfigurations.Vrot;
        float Vlin = GotoPoint2DConfigurations.Vlin;
        float dt = GotoPoint2DConfigurations.dt;
        float Kdist = GotoPoint2DConfigurations.Kdist;
        float Khead = GotoPoint2DConfigurations.Khead;
        float Kint = GotoPoint2DConfigurations.Kint;
        // ==================================================================

        RestThruServer.Velocity2Payload vel2;
        RestThruServer.ServerResponse response;

        // First, rotate to face the goal point
        Vector2 RobotFoward2 = new(RobotTransform.forward.x, RobotTransform.forward.z);
        Vector2 Distance2 = new(TargetPosition.x - RobotTransform.position.x, TargetPosition.y - RobotTransform.position.z);
        float Angle = Vector2.SignedAngle(RobotFoward2, Distance2);

        if (Math.Abs(Angle) > HeadingTolerance)
        {
            vel2 = (Angle > 0) ? new(Vrot, -Vrot) : new(-Vrot, Vrot);

            response = await RestThruServer.SendPayloadToServer(RestThruServer.Velocity2Endpoint, "PUT", vel2, ct);

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
        response = await RestThruServer.SendPayloadToServer(RestThruServer.Velocity2Endpoint, "PUT", vel2, ct);


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

        // We will measure how much time the request took to be answered and discard this time from the delay
        Stopwatch stopwatch = new();
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
            errorInt += errorDist * (currdt / 1000); // Should dt be used here ?
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

            // Sending new velocity command
            vel2 = new(Vr, Vl);

            stopwatch.Restart();
            response = await RestThruServer.SendPayloadToServer(RestThruServer.Velocity2Endpoint, "PUT", vel2, ct);
            stopwatch.Stop();
            DebugLogger.Log($"Last control action took {stopwatch.ElapsedMilliseconds} ms to be answered by the server. respose={response}");
            float delayTime = currdt;
            delayTime = (stopwatch.ElapsedMilliseconds < delayTime) ? delayTime - stopwatch.ElapsedMilliseconds : 0;

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

            await Task.Delay((int)delayTime, ct);
        }

        // We've arrived. Stop movement.
        await ForceStopRobot();
        return true;
    }
    #endregion

    #region AlignHeading
    [System.Serializable]
    private class AlignHeadingConfig
    {
        [Tooltip("Tolerance in degrees to consider the heading is aligned")]
        public float HeadingTolerance = 10;

        [Tooltip("Max rotational velocity")]
        public float maxVrot = 100;
        
        [Tooltip("Control period (sec)")]
        public float dt = 0.5f;

        [Tooltip("How many seconds should the robot wait without target updates")]
        public float TimeoutToCompletion = 3;

        [Tooltip("Proportional gain for heading error")]
        public float Kp = 15;

        [Tooltip("Integral gain for distance error")]
        public float Ki = 0.001f;

        [Tooltip("Derivative gain for distance error")]
        public float Kd = 0.001f;

        [Tooltip("UI RadialFill that should appear when inside the tolerance zone.")]
        public RotationUI UIFeedback;
    }

    [SerializeField] private AlignHeadingConfig AlignHeadingConfigurations;

    private async Task<bool> AlignHeading2D(Transform TransformToFollow, Transform RobotTransform, CancellationToken ct)
    {
        // ==================================================================
        // Deconstructing config to local variables
        float HeadingTolerance = AlignHeadingConfigurations.HeadingTolerance;
        float dt = AlignHeadingConfigurations.dt;
        float Kp = AlignHeadingConfigurations.Kp;
        float Ki = AlignHeadingConfigurations.Ki;
        float Kd = AlignHeadingConfigurations.Kd;
        float maxVrot = AlignHeadingConfigurations.maxVrot;
        float timeout = AlignHeadingConfigurations.TimeoutToCompletion;
        RotationUI canvas = AlignHeadingConfigurations.UIFeedback;
        // ==================================================================

        float prevErr = 0f;
        float intErr = 0f;
        float derErr = 0f;
        float headingErr = 0f;


        Stopwatch stopwatch = new();
        RestThruServer.Velocity2Payload vel2;
        RestThruServer.ServerResponse response;

        float StableTime = 0;
        bool isStopped = false;
        bool isCompleted = false;

        canvas.ShowCanvas();
        try
        {
            while (!isCompleted)
            {
                ct.ThrowIfCancellationRequested();
                Vector2 TargetFoward2 = new(TransformToFollow.forward.x, TransformToFollow.forward.z);
                Vector2 RobotFoward2 = new(RobotTransform.forward.x, RobotTransform.forward.z);

                // If outside the tolerance, start control process
                if (Vector2.Angle(RobotFoward2, TargetFoward2) > HeadingTolerance)
                {
                    canvas.ResetFilling();
                    // Reseting stable time. Robot resume moving
                    isStopped = false;
                    StableTime = 0;

                    headingErr = Vector2.SignedAngle(RobotFoward2, TargetFoward2) * Mathf.Deg2Rad;

                    float Delta = 0;
                    if(Math.Abs(headingErr) > 30 * Mathf.Deg2Rad)
                    {
                        // Target is far, go full power
                        Delta = (headingErr > 0) ? maxVrot : -maxVrot;

                        // Reset integral error variables
                        intErr = 0f;
                        derErr = 0f;
                    }
                    else
                    {
                        // Target is close, use PID controller

                        // Suavization filter
                        headingErr = 0.4f * prevErr + 0.6f * headingErr;

                        intErr += headingErr;
                        derErr = (headingErr - prevErr) / dt;


                        Delta = headingErr * Kp + intErr * Ki + derErr * Kd;
                        Delta = Math.Clamp(Delta, -maxVrot, maxVrot);
                    }
                    prevErr = headingErr;

                    float Vl = - Delta;
                    float Vr = Delta;
                    // Sending velocity comand
                    vel2 = new(Vr, Vl);

                    stopwatch.Restart();
                    response = await RestThruServer.SendPayloadToServer(RestThruServer.Velocity2Endpoint, "PUT", vel2, ct); 
                    stopwatch.Stop();
                    DebugLogger.Log($"Last control action took {stopwatch.ElapsedMilliseconds} ms to be answered by the server. respose={response}");
                    float delayTime = dt;
                    delayTime = (stopwatch.ElapsedMilliseconds < delayTime) ? delayTime - stopwatch.ElapsedMilliseconds : 0;

                    // Logs for debuging PID controller
                    DebugLogger.LogWarning($@"
                        <control_info>
                        {{
                        ""RobotFoward2"":""{RobotFoward2}"",
                        ""TargetFoward2"":""{TargetFoward2}"",
                        ""headingErr"":""{headingErr}"",
                        ""intErr"":""{intErr}"",
                        ""derErr"":""{derErr}"",
                        ""Proportional contribution"":""{headingErr * Kp}"",
                        ""Derivative contribution"":""{derErr * Kd}"",
                        ""Integral contribution"":""{intErr * Ki}"",
                        ""Delta"":""{Delta}"",
                        ""Vr"":{Vr},
                        ""Vl"":{Vl},
                        ""dt"":{dt}
                        }}
                        </control_info>
                    ");

                    await Task.Delay((int)delayTime, ct);
                }
                else // If inside the tolerance...
                {
                    if (!isStopped)
                    {
                        DebugLogger.LogWarning($"<control_info> Stopped </control_info>");
                        // Stop robot
                        await ForceStopRobot();
                        isStopped = true;

                        // Reset control variables
                        prevErr = 0f;
                        intErr = 0f;
                        derErr = 0f;

                        //Start UI feedback
                        canvas.StartFilling(timeout);
                    }
                    else
                    {
                        // check timeout out for activation
                        if (StableTime < timeout)
                        {
                            StableTime += Time.deltaTime;  
                            DebugLogger.LogWarning($"<control_info> Stopped for {StableTime} </control_info>");
                        } 
                        else isCompleted = true;
                    }
                    await Task.Yield();
                }
            }
        }
        catch(Exception ex)
        {
            stopwatch.Stop();
            canvas.HideCanvas();
            throw ex;
        }
        

        // Task finished. Garanteeing the robot stopped and returning.
        await ForceStopRobot();

        // Hiding UI
        canvas.HideCanvas();
        
        return true;
    }

    #endregion

    #endregion

    #region Utils
    private async Task ForceStopRobot()
    {
        RestThruServer.ServerResponse response;
        int retryAttemps = 0;
        int maxRetrys = 3;
        do
        {
            retryAttemps++;
            response = await RestThruServer.SendPayloadToServer(RestThruServer.StopEndpoint, "PUT");
            if (response.Result != UnityWebRequest.Result.Success) DebugLogger.LogError($"Error while attempting to stopping robot. Retrying");
        } while (response.Result != UnityWebRequest.Result.Success && retryAttemps < maxRetrys);
    }


    #endregion
}

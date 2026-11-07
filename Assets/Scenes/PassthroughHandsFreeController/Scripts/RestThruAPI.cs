using System; 
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Threading;
using UnityEngine.Serialization;

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

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        OVRManager.HMDUnmounted += OnHmdUnmounted;
        OVRManager.InputFocusLost += OnInputFocusLost;
    }

    void OnDisable()
    {
        OVRManager.HMDUnmounted -= OnHmdUnmounted;
        OVRManager.InputFocusLost -= OnInputFocusLost;
    }

    // Safety: stop any running operation when the app can no longer be driven by the user.
    // In the Editor, focus changes happen constantly (switching windows), so only device builds react to them.
    void OnApplicationPause(bool paused)
    {
        if (paused && !Application.isEditor) SafetyStop("application paused");
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !Application.isEditor) SafetyStop("application lost focus");
    }

    private void OnHmdUnmounted() => SafetyStop("headset unmounted");
    private void OnInputFocusLost() => SafetyStop("input focus lost");

    /// <summary>
    /// Cancels the running operation through the emergency-stop path, so ScheduleOperation sends /motion/stop.
    /// Best effort: if the OS suspends the app, the stop is sent when it resumes. A server-side watchdog is still recommended.
    /// </summary>
    private void SafetyStop(string reason)
    {
        if (State != APIStates.BUSY) return;
        DebugLogger.LogWarning($"Safety stop: {reason}. State -> EMERGENCY_STOP");
        State = APIStates.EMERGENCY_STOP;
        currtOperationCt?.Cancel();
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

    /// <summary>Streams continuous velocity commands from <paramref name="input"/> until it requests exit, fails or is cancelled.</summary>
    public void ScheduleTeleop(ITeleopInput input, Action<bool> onCompleted, ITeleopCommandFilter filter = null)
    {
        ScheduleOperation(token => StreamTeleopLoop(input, filter ?? new PassThroughTeleopFilter(), token), onCompleted);
    }

    public float TeleopDeadzone => TeleopStreamConfigurations.Deadzone;

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

        // Vrot (initial alignment speed) and Vlin (cruise speed) come from SettingsStore
        // (AppSettings.AngularSpeed / LinearSpeed). See GetEffectiveGotoSpeeds().

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
        (float Vlin, float Vrot) = GetEffectiveGotoSpeeds();
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

        [Tooltip("Hard cap for rotational velocity. The user's AngularSpeed setting scales this down but never above it.")]
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
        float maxVrot = GetEffectiveAlignMaxVrot();
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
                    float delayTime = dt * 1000f;
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

    #region Teleop streaming
    [System.Serializable]
    private class TeleopStreamConfig
    {
        [Tooltip("Keepalive / maximum send period for non-zero commands (sec)")]
        public float dt = 0.1f;

        [Tooltip("Thumbstick deadzone (0-1), used by the controller-joystick mode")]
        public float Deadzone = 0.15f;

        [Tooltip("HTTP timeout for streamed vel2/stop requests (sec). Generous because the robot is reached over WiFi and answered by a small on-board computer. A stalled vel2 never delays a stop: it is aborted as soon as the command changes or is released.")]
        public int RequestTimeoutSeconds = 5;

        [Tooltip("While a stop is not acknowledged by the server, it is re-sent at this period (sec).")]
        public float StopRetryInterval = 0.5f;

        [Tooltip("When the user leaves the mode (Menu/Minimize), the stop is attempted for at most this long (sec). The mode is left even if the robot cannot be reached, so a dead link never traps the user in the app.")]
        public float ExitStopGraceSeconds = 2f;
    }

    [FormerlySerializedAs("JoystickTeleopConfigurations")]
    [SerializeField] private TeleopStreamConfig TeleopStreamConfigurations;

    private enum RequestOutcome { Ok, Failed, Aborted }

    /// <summary>One in-flight streamed request (vel2 or stop). Aborted by cancelling <see cref="Cts"/>.</summary>
    private sealed class TeleopRequest
    {
        public Task<RestThruServer.ServerResponse> Task;
        public CancellationTokenSource Cts;
        public bool IsStop;
        public TeleopCommand Command;
        public float StartedAt;
    }

    private TeleopRequest StartTeleopRequest(bool isStop, TeleopCommand sendCommand, TeleopCommand identity, float linearSpeed, float angularSpeed, int timeout, CancellationToken ct)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<RestThruServer.ServerResponse> task;
        if (isStop)
        {
            task = RestThruServer.SendPayloadToServer<object>(RestThruServer.StopEndpoint, "PUT", null, cts.Token, timeout);
        }
        else
        {
            float linear = sendCommand.Linear * linearSpeed;
            float rotation = sendCommand.Angular * angularSpeed; // +Angular = CCW: Vr = +rotation, Vl = -rotation
            RestThruServer.Velocity2Payload vel2 = new(linear + rotation, linear - rotation);
            task = RestThruServer.SendPayloadToServer(RestThruServer.Velocity2Endpoint, "PUT", vel2, cts.Token, timeout);
        }
        return new TeleopRequest { Task = task, Cts = cts, IsStop = isStop, Command = identity, StartedAt = Time.realtimeSinceStartup };
    }

    /// <summary>Cancels an in-flight request (aborts the web request) and makes sure its exception is observed.</summary>
    private static void AbortTeleopRequest(TeleopRequest request)
    {
        if (request == null) return;
        request.Cts.Cancel();
        request.Task.ContinueWith(t => { _ = t.Exception; request.Cts.Dispose(); }, TaskScheduler.Default);
    }

    private static RequestOutcome EvaluateTeleopRequest(TeleopRequest request, out string detail)
    {
        detail = string.Empty;
        Task<RestThruServer.ServerResponse> task = request.Task;
        request.Cts.Dispose();
        if (task.IsCanceled) return RequestOutcome.Aborted;
        if (task.IsFaulted)
        {
            Exception ex = task.Exception?.GetBaseException();
            if (ex is OperationCanceledException) return RequestOutcome.Aborted;
            detail = ex?.Message ?? "unknown error";
            return RequestOutcome.Failed;
        }
        if (task.Result.Result != UnityWebRequest.Result.Success)
        {
            detail = task.Result.ToString();
            return RequestOutcome.Failed;
        }
        return RequestOutcome.Ok;
    }

    /// <summary>
    /// Continuous teleoperation shared by all streaming control modes. Polls the input every frame without ever
    /// blocking on the network:
    /// - A zero command aborts any in-flight vel2 and sends /motion/stop immediately; an unacknowledged stop is re-sent
    ///   every StopRetryInterval until the server answers.
    /// - A non-zero command sends vel2 when it changes or every dt as keepalive.
    /// - A failed or timed-out request never ends the operation: it latches a link fault (reported to the input when it
    ///   implements <see cref="ITeleopLinkListener"/>), the command is treated as zero until the stop is acknowledged and
    ///   the input has been released, and only then can motion resume.
    /// - An exit request ends the operation as soon as a stop attempt made after the request finished (acknowledged or
    ///   failed) or ExitStopGraceSeconds elapsed: an unreachable robot never traps the user in the app.
    /// - Any cancellation (emergency stop, safety stop) aborts the request in flight and stops the robot before propagating.
    /// </summary>
    private async Task<bool> StreamTeleopLoop(ITeleopInput input, ITeleopCommandFilter filter, CancellationToken ct)
    {
        float dt = TeleopStreamConfigurations.dt;
        int timeout = Mathf.Max(1, TeleopStreamConfigurations.RequestTimeoutSeconds);
        float stopRetryInterval = Mathf.Max(0.1f, TeleopStreamConfigurations.StopRetryInterval);
        float exitGrace = Mathf.Max(0.5f, TeleopStreamConfigurations.ExitStopGraceSeconds);
        (float Vlin, float Vrot) = GetEffectiveGotoSpeeds();
        ITeleopLinkListener linkListener = input as ITeleopLinkListener;

        TeleopRequest inFlight = null;
        TeleopCommand sentCommand = TeleopCommand.Zero;
        bool robotStopped = false;   // a stop was acknowledged and no vel2 has been sent since
        bool faulted = false;        // link fault latched: motion is blocked until stop acknowledged and input released
        float lastSendTime = float.NegativeInfinity;
        float nextStopTime = 0f;
        float extraStopAt = -1f;     // after aborting a vel2, send one more stop in case that vel2 reached the server late
        float lastFrameTime = Time.realtimeSinceStartup;
        float exitStart = -1f;       // when the exit request was first seen
        bool exitStopAttempted = false; // a stop that started after the exit request has finished (ok or failed)

        filter.Reset();
        input.OnTeleopStarted();
        DebugLogger.Log($"Teleop streaming started: input={input.GetType().Name} Vlin={Vlin} Vrot={Vrot} dt={dt} timeout={timeout}s");

        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                float now = Time.realtimeSinceStartup;
                float frameDt = now - lastFrameTime;
                lastFrameTime = now;

                // 1. Collect the result of the request in flight, if it finished.
                if (inFlight != null && inFlight.Task.IsCompleted)
                {
                    bool wasStop = inFlight.IsStop;
                    bool startedAfterExit = exitStart >= 0f && inFlight.StartedAt >= exitStart;
                    RequestOutcome outcome = EvaluateTeleopRequest(inFlight, out string detail);
                    inFlight = null;
                    if (wasStop && startedAfterExit && outcome != RequestOutcome.Aborted) exitStopAttempted = true;

                    if (outcome == RequestOutcome.Ok)
                    {
                        if (wasStop) robotStopped = true;
                    }
                    else if (outcome == RequestOutcome.Failed)
                    {
                        DebugLogger.LogError($"Teleop streaming: {(wasStop ? "stop" : "vel2")} request failed ({detail}). Link fault: stopping robot and waiting for the input to be released.");
                        robotStopped = false;
                        nextStopTime = wasStop ? now + stopRetryInterval : now;
                        if (!faulted)
                        {
                            faulted = true;
                            linkListener?.OnTeleopLinkFault();
                        }
                    }
                }

                // 2. Effective command.
                bool exiting = input.ExitRequested;
                if (exiting && exitStart < 0f) exitStart = now;
                TeleopCommand raw = input.Current;

                if (faulted && !exiting && raw.IsZero && robotStopped && inFlight == null)
                {
                    DebugLogger.Log("Teleop streaming: link recovered (stop acknowledged, input released).");
                    faulted = false;
                    linkListener?.OnTeleopLinkRecovered();
                }

                TeleopCommand target = (faulted || exiting) ? TeleopCommand.Zero : raw;

                // 3. Act.
                if (exiting && inFlight == null && (robotStopped || exitStopAttempted))
                {
                    if (robotStopped) DebugLogger.Log("Teleop streaming: exit requested and stop acknowledged.");
                    else DebugLogger.LogWarning("Teleop streaming: exit requested but the stop was not acknowledged (link down). Leaving the mode anyway.");
                    return true;
                }

                if (exiting && now - exitStart >= exitGrace)
                {
                    DebugLogger.LogWarning($"Teleop streaming: exit requested but no stop acknowledgement within {exitGrace:0.#}s. Leaving the mode and sending one last stop.");
                    AbortTeleopRequest(inFlight);
                    inFlight = null;
                    _ = ForceStopDetached(timeout);
                    return true;
                }

                if (target.IsZero)
                {
                    filter.Reset();
                    if (inFlight != null && !inFlight.IsStop)
                    {
                        AbortTeleopRequest(inFlight);
                        inFlight = null;
                        robotStopped = false;
                        nextStopTime = now;
                        extraStopAt = now + 0.3f;
                    }

                    if (inFlight == null && !robotStopped && now >= nextStopTime)
                    {
                        inFlight = StartTeleopRequest(true, TeleopCommand.Zero, TeleopCommand.Zero, Vlin, Vrot, timeout, ct);
                        nextStopTime = now + stopRetryInterval;
                    }
                    else if (inFlight == null && robotStopped && extraStopAt > 0f && now >= extraStopAt)
                    {
                        extraStopAt = -1f;
                        robotStopped = false; // force one more stop
                        nextStopTime = now;
                    }
                }
                else
                {
                    extraStopAt = -1f;
                    // A stop in flight must finish first; a vel2 for a different command is replaced right away.
                    if (inFlight != null && !inFlight.IsStop && !inFlight.Command.Approximately(target))
                    {
                        AbortTeleopRequest(inFlight);
                        inFlight = null;
                    }

                    if (inFlight == null && (robotStopped || !sentCommand.Approximately(target) || now - lastSendTime >= dt))
                    {
                        TeleopCommand shaped = filter.Apply(target, frameDt); // hook for a future acceleration ramp
                        inFlight = StartTeleopRequest(false, shaped, target, Vlin, Vrot, timeout, ct);
                        sentCommand = target;
                        lastSendTime = now;
                        robotStopped = false;
                    }
                }

                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            DebugLogger.LogWarning("Teleop streaming cancelled. Stopping robot.");
            AbortTeleopRequest(inFlight);
            await TryStopRobot();
            throw;
        }
        finally
        {
            input.OnTeleopEnded();
            DebugLogger.Log("Teleop streaming ended.");
        }
    }

    /// <summary>Last-chance stop sent after the loop has given up waiting. Not tied to the operation token and never throws.</summary>
    private static async Task ForceStopDetached(int timeout)
    {
        try
        {
            await RestThruServer.SendPayloadToServer<object>(RestThruServer.StopEndpoint, "PUT", null, CancellationToken.None, timeout);
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Final stop could not be delivered: {ex.Message}");
        }
    }

    /// <summary>ForceStopRobot that never throws (used on cancellation paths).</summary>
    private async Task TryStopRobot()
    {
        try
        {
            await ForceStopRobot();
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Couldn't reach server to stop the robot. {ex.Message}");
        }
    }
    #endregion

    #endregion

    #region Utils
    private static float ClampToServerMax(float speed)
    {
        float? serverMax = RestThruServer.ServerMaxWheelSpeed;
        return serverMax.HasValue ? Mathf.Min(speed, serverMax.Value) : speed;
    }

    /// <summary>Nominal GotoPoint2D speeds from user settings, clamped by the server-reported max wheel speed.</summary>
    public (float linear, float angular) GetEffectiveGotoSpeeds()
    {
        AppSettings settings = SettingsStore.Current;
        float linear = ClampToServerMax(settings.LinearSpeed);
        float angular = ClampToServerMax(settings.AngularSpeed);
        DebugLogger.Log($"Effective GotoPoint2D speeds: Vlin={linear} Vrot={angular} (serverMax={RestThruServer.ServerMaxWheelSpeed})");
        return (linear, angular);
    }

    /// <summary>
    /// AlignHeading2D max rotation: the configured maxVrot scaled by AngularSpeed / AngularSpeedDefault,
    /// never above the configured maxVrot, then clamped by the server-reported max wheel speed.
    /// </summary>
    public float GetEffectiveAlignMaxVrot()
    {
        float configuredMax = AlignHeadingConfigurations.maxVrot;
        float scale = SettingsStore.Current.AngularSpeed / AppSettings.AngularSpeedDefault;
        float effective = ClampToServerMax(Mathf.Min(configuredMax, configuredMax * scale));
        DebugLogger.Log($"Effective AlignHeading2D maxVrot={effective} (configured={configuredMax}, scale={scale})");
        return effective;
    }

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

using UnityEngine;

/// <summary>
/// Normalized teleoperation command. Both axes are in [-1, 1] and are scaled by the configured speeds in
/// <see cref="RestThruAPI"/>. Linear +1 = forward. Angular +1 = counter-clockwise (turn left), matching the
/// AlignHeading2D convention (Vr = +Delta, Vl = -Delta).
/// </summary>
public readonly struct TeleopCommand
{
    public const float Epsilon = 0.02f;

    public readonly float Linear;
    public readonly float Angular;

    public TeleopCommand(float linear, float angular)
    {
        Linear = Mathf.Clamp(linear, -1f, 1f);
        Angular = Mathf.Clamp(angular, -1f, 1f);
    }

    public static TeleopCommand Zero => new(0f, 0f);

    public bool IsZero => Mathf.Abs(Linear) < Epsilon && Mathf.Abs(Angular) < Epsilon;

    public bool Approximately(TeleopCommand other) =>
        Mathf.Abs(Linear - other.Linear) < Epsilon && Mathf.Abs(Angular - other.Angular) < Epsilon;

    public override string ToString() => $"(linear={Linear:0.00}, angular={Angular:0.00})";
}

/// <summary>
/// Source of continuous teleoperation commands, polled every frame by RestThruAPI's streaming loop.
/// Implementations must return <see cref="TeleopCommand.Zero"/> whenever they are unsure (lost tracking,
/// hidden UI, ...): the loop stops the wheelchair as soon as the command becomes zero.
/// </summary>
public interface ITeleopInput
{
    TeleopCommand Current { get; }

    /// <summary>When true, the loop stops the wheelchair and ends the operation successfully once a stop has been acknowledged by the server.</summary>
    bool ExitRequested { get; }

    void OnTeleopStarted();
    void OnTeleopEnded();
}

/// <summary>
/// Optional: an <see cref="ITeleopInput"/> that implements this is told when the link to the robot fails (a request
/// failed or timed out) and when it is usable again. While faulted the loop ignores non-zero commands and keeps trying
/// to stop the robot; the input should drop its active command and show the problem to the user.
/// </summary>
public interface ITeleopLinkListener
{
    void OnTeleopLinkFault();
    void OnTeleopLinkRecovered();
}

/// <summary>
/// Shapes the commanded value before it is sent (e.g. an acceleration ramp). The streaming loop always
/// sends a stop immediately when the raw input becomes zero, regardless of the filter.
/// </summary>
public interface ITeleopCommandFilter
{
    TeleopCommand Apply(TeleopCommand target, float dt);
    void Reset();
}

/// <summary>
/// Default filter: no shaping. A future acceleration ramp (soft start) can be implemented as another
/// <see cref="ITeleopCommandFilter"/> and passed to RestThruAPI.ScheduleTeleop.
/// </summary>
public class PassThroughTeleopFilter : ITeleopCommandFilter
{
    public TeleopCommand Apply(TeleopCommand target, float dt) => target;
    public void Reset() { }
}

/// <summary>
/// Controller-joystick teleoperation: left stick Y drives forward/back, right stick X rotates in place
/// (clockwise when pushed right). The left Menu button exits.
/// </summary>
public class ThumbstickTeleopInput : ITeleopInput
{
    private readonly float _deadzone;

    public ThumbstickTeleopInput(float deadzone)
    {
        _deadzone = deadzone;
    }

    public TeleopCommand Current
    {
        get
        {
            Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

            float linear = Mathf.Abs(leftStick.y) > _deadzone ? leftStick.y : 0f;
            float turn = Mathf.Abs(rightStick.x) > _deadzone ? rightStick.x : 0f;

            // A right-pushed stick (turn > 0) must produce a clockwise turn, i.e. negative angular.
            return new TeleopCommand(linear, -turn);
        }
    }

    public bool ExitRequested => OVRInput.Get(OVRInput.RawButton.Start);

    public void OnTeleopStarted() { }
    public void OnTeleopEnded() { }
}

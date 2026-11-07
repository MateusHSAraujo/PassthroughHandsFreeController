using UnityEngine;

/// <summary>Direction commanded by the gaze pad.</summary>
public enum PadDirection { None = 0, Forward = 1, Back = 2, TurnLeft = 3, TurnRight = 4 }

/// <summary>Nominal zone a gaze point falls in. Gap = inside the ring but between wedges.</summary>
public enum PadZone { Outside, Center, Forward, Back, TurnLeft, TurnRight, Gap }

/// <summary>High-level state of the pad, for UI feedback.</summary>
public enum PadStatus { Locked, NeedsCenter, Ready, Arming, Active, TrackingLost }

/// <summary>Geometry and timing of the 4-wedge gaze pad, in local canvas units.</summary>
[System.Serializable]
public class GazePadGeometry
{
    [Tooltip("Radius of the central dead zone.")]
    public float innerRadius = 45f;

    [Tooltip("Outer radius of the wedges.")]
    public float outerRadius = 150f;

    [Tooltip("Total angular gap between adjacent wedges, in degrees.")]
    public float gapDeg = 6f;

    [Tooltip("Extra angular margin (degrees) an active wedge keeps before releasing.")]
    public float angleHysteresisDeg = 8f;

    [Tooltip("Extra radial margin an active wedge keeps, and margin beyond the outer radius before the session closes.")]
    public float radiusHysteresis = 10f;

    [Tooltip("Seconds the gaze must stay in a wedge before it becomes active.")]
    public float armTime = 0.25f;

    /// <summary>Nominal half-width of each wedge in degrees.</summary>
    public float HalfWidthDeg => 45f - gapDeg * 0.5f;
}

/// <summary>
/// Pure logic deciding which direction is active on a 4-wedge gaze pad. The gaze must first
/// visit the center to open a session; a wedge must then be held for armTime to become active.
/// Time is supplied by the caller. Conservative: any doubt results in no active direction.
/// </summary>
public class GazePadZoneTracker
{
    private readonly GazePadGeometry g;
    private bool sessionOpen;
    private float candidateStart;

    public PadDirection Active { get; private set; } = PadDirection.None;
    public PadDirection Candidate { get; private set; } = PadDirection.None;
    public float ArmProgress { get; private set; }
    public PadStatus Status { get; private set; } = PadStatus.NeedsCenter;
    public PadZone LastZone { get; private set; } = PadZone.Outside;

    public GazePadZoneTracker(GazePadGeometry geometry)
    {
        g = geometry ?? new GazePadGeometry();
    }

    /// <summary>Clears everything: session closed, nothing active or arming.</summary>
    public void Reset()
    {
        sessionOpen = false;
        Active = PadDirection.None;
        Candidate = PadDirection.None;
        ArmProgress = 0f;
        candidateStart = 0f;
        Status = PadStatus.NeedsCenter;
        LastZone = PadZone.Outside;
    }

    /// <summary>Angle in degrees of the wedge center for a direction (0 = +X, CCW).</summary>
    public static float CenterAngle(PadDirection d)
    {
        switch (d)
        {
            case PadDirection.Forward: return 90f;
            case PadDirection.TurnLeft: return 180f;
            case PadDirection.Back: return 270f;
            case PadDirection.TurnRight: return 0f;
            default: return 0f;
        }
    }

    private static PadZone ToZone(PadDirection d)
    {
        switch (d)
        {
            case PadDirection.Forward: return PadZone.Forward;
            case PadDirection.Back: return PadZone.Back;
            case PadDirection.TurnLeft: return PadZone.TurnLeft;
            case PadDirection.TurnRight: return PadZone.TurnRight;
            default: return PadZone.Outside;
        }
    }

    private static PadDirection ToDirection(PadZone z)
    {
        switch (z)
        {
            case PadZone.Forward: return PadDirection.Forward;
            case PadZone.Back: return PadDirection.Back;
            case PadZone.TurnLeft: return PadDirection.TurnLeft;
            case PadZone.TurnRight: return PadDirection.TurnRight;
            default: return PadDirection.None;
        }
    }

    private static float AngleOf(Vector2 p)
    {
        float a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
        if (a < 0f) a += 360f;
        return a;
    }

    private static readonly PadDirection[] Wedges =
    {
        PadDirection.Forward, PadDirection.TurnLeft, PadDirection.Back, PadDirection.TurnRight
    };

    /// <summary>Classifies a local point (pad center at origin) into a nominal zone.</summary>
    public static PadZone Classify(Vector2 localPoint, GazePadGeometry g)
    {
        float r = localPoint.magnitude;
        if (r < g.innerRadius) return PadZone.Center;
        if (r > g.outerRadius) return PadZone.Outside;

        float angle = AngleOf(localPoint);
        float half = g.HalfWidthDeg;
        for (int i = 0; i < Wedges.Length; i++)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angle, CenterAngle(Wedges[i]))) <= half)
                return ToZone(Wedges[i]);
        }
        return PadZone.Gap;
    }

    private bool InExtendedRegion(PadDirection d, Vector2 p)
    {
        float r = p.magnitude;
        if (r < g.innerRadius - g.radiusHysteresis || r > g.outerRadius + g.radiusHysteresis) return false;
        float delta = Mathf.Abs(Mathf.DeltaAngle(AngleOf(p), CenterAngle(d)));
        return delta <= g.HalfWidthDeg + g.angleHysteresisDeg;
    }

    private void ClearMotion()
    {
        Active = PadDirection.None;
        Candidate = PadDirection.None;
        ArmProgress = 0f;
    }

    /// <summary>Advances the tracker. hasHit=false means the gaze ray misses the pad plane.</summary>
    public void Update(bool hasHit, Vector2 localPoint, bool inputUnlocked, bool trackingOk, float time)
    {
        // Rule 1 and 2: tracking lost or input locked.
        if (!trackingOk)
        {
            ClearMotion();
            sessionOpen = false;
            LastZone = PadZone.Outside;
            Status = PadStatus.TrackingLost;
            return;
        }
        if (!inputUnlocked)
        {
            ClearMotion();
            sessionOpen = false;
            LastZone = PadZone.Outside;
            Status = PadStatus.Locked;
            return;
        }

        // Invalid data is treated as no hit.
        if (float.IsNaN(localPoint.x) || float.IsNaN(localPoint.y) ||
            float.IsInfinity(localPoint.x) || float.IsInfinity(localPoint.y))
            hasHit = false;

        float r = hasHit ? localPoint.magnitude : 0f;
        PadZone zone = hasHit ? Classify(localPoint, g) : PadZone.Outside;
        LastZone = zone;

        // Rule 3 and 7: session handling.
        if (!hasHit || r > g.outerRadius + g.radiusHysteresis)
        {
            sessionOpen = false;
            ClearMotion();
        }
        else if (zone == PadZone.Center)
        {
            sessionOpen = true;
        }

        // Rule 4: keep an active wedge while inside its extended region.
        if (Active != PadDirection.None)
        {
            if (sessionOpen && hasHit && InExtendedRegion(Active, localPoint))
            {
                Candidate = Active;
                ArmProgress = 1f;
                Status = PadStatus.Active;
                return;
            }
            ClearMotion();
        }

        // Rule 5: arming. A wedge never becomes active on the frame it is first seen.
        PadDirection wedge = sessionOpen ? ToDirection(zone) : PadDirection.None;
        if (wedge == PadDirection.None)
        {
            Candidate = PadDirection.None;
            ArmProgress = 0f;
            Status = sessionOpen ? PadStatus.Ready : PadStatus.NeedsCenter;
            return;
        }

        if (wedge != Candidate)
        {
            Candidate = wedge;
            candidateStart = time;
            ArmProgress = 0f;
            Status = PadStatus.Arming;
            return;
        }

        float armTime = Mathf.Max(g.armTime, 0.0001f);
        float progress = Mathf.Clamp01((time - candidateStart) / armTime);
        if (progress >= 1f)
        {
            Active = Candidate;
            ArmProgress = 1f;
            Status = PadStatus.Active;
        }
        else
        {
            ArmProgress = progress;
            Status = PadStatus.Arming;
        }
    }

    /// <summary>Short English text for UI feedback.</summary>
    public static string Describe(PadStatus status, PadDirection active)
    {
        switch (status)
        {
            case PadStatus.NeedsCenter: return "Look at the center";
            case PadStatus.Ready: return "Look at an arrow to move";
            case PadStatus.Arming: return "Hold…";
            case PadStatus.TrackingLost: return "Tracking lost";
            case PadStatus.Active:
                switch (active)
                {
                    case PadDirection.Forward: return "Forward";
                    case PadDirection.Back: return "Back";
                    case PadDirection.TurnLeft: return "Turning left";
                    case PadDirection.TurnRight: return "Turning right";
                    default: return "";
                }
            default: return "";
        }
    }
}

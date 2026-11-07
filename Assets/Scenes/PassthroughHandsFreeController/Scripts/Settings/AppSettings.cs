using System;
using UnityEngine;

public enum ServerScheme
{
    Http = 0,
    Https = 1
}

public enum ControlModeId
{
    GazeDwell = 0,
    ControllerJoystick = 1,
    FloatingJoystick = 2
}

public enum SpeedPreset
{
    Slow,
    Normal,
    Fast
}

/// <summary>
/// Persisted, user-editable application settings. Serialized as JSON by <see cref="SettingsStore"/>.
/// Speed values are wheel velocities sent in /motion/vel2 (same units as RestThruAPI's Vlin/Vrot).
/// </summary>
[Serializable]
public class AppSettings
{
    public const int CurrentVersion = 1;

    // Server
    public const string DefaultHost = "192.168.0.2";
    public const int DefaultPort = 8080;

    // Linear speed (nominal cruise speed of GotoPoint2D)
    public const float LinearSpeedMin = 20f;
    public const float LinearSpeedMax = 200f;
    public const float LinearSpeedDefault = 100f;
    public const float LinearSpeedStep = 10f;

    // Angular speed (in-place rotation speed). The default matches the previous hardcoded
    // GotoPoint2D initial-alignment Vrot. AlignHeading2D scales from it but never exceeds its configured maxVrot.
    public const float AngularSpeedMin = 20f;
    public const float AngularSpeedMax = 100f;
    public const float AngularSpeedDefault = 50f;
    public const float AngularSpeedStep = 5f;

    public int Version = CurrentVersion;

    public ServerScheme Scheme = ServerScheme.Http;
    public string Host = DefaultHost;
    public int Port = DefaultPort;

    public float LinearSpeed = LinearSpeedDefault;
    public float AngularSpeed = AngularSpeedDefault;

    // Debug visuals default to hidden: end users should not see the room mesh or the anchor axes.
    public bool MeshVisible = false;

    // Debug axes drawn on both controllers and on the wheelchair anchor (AnchorManager).
    public bool DebugAnchorsVisible = false;

    public ControlModeId ControlMode = ControlModeId.GazeDwell;

    public string SchemePrefix => Scheme == ServerScheme.Https ? "https" : "http";

    public static float GetLinearPreset(SpeedPreset preset) => preset switch
    {
        SpeedPreset.Slow => 60f,
        SpeedPreset.Fast => 150f,
        _ => LinearSpeedDefault
    };

    public static float GetAngularPreset(SpeedPreset preset) => preset switch
    {
        SpeedPreset.Slow => 30f,
        SpeedPreset.Fast => 80f,
        _ => AngularSpeedDefault
    };

    /// <summary>Clamps every field into its valid range. Never throws.</summary>
    public void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(Host)) Host = DefaultHost;
        Host = Host.Trim();
        if (Port < 1 || Port > 65535) Port = DefaultPort;
        if (!Enum.IsDefined(typeof(ServerScheme), Scheme)) Scheme = ServerScheme.Http;

        LinearSpeed = float.IsNaN(LinearSpeed) ? LinearSpeedDefault : Mathf.Clamp(LinearSpeed, LinearSpeedMin, LinearSpeedMax);
        AngularSpeed = float.IsNaN(AngularSpeed) ? AngularSpeedDefault : Mathf.Clamp(AngularSpeed, AngularSpeedMin, AngularSpeedMax);

        if (!Enum.IsDefined(typeof(ControlModeId), ControlMode)) ControlMode = ControlModeId.GazeDwell;

        Version = CurrentVersion;
    }

    public AppSettings Clone()
    {
        return JsonUtility.FromJson<AppSettings>(JsonUtility.ToJson(this));
    }
}

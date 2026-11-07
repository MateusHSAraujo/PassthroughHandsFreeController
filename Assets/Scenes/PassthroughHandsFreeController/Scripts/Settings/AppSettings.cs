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

    // Speed ranges, defaults and presets live in the SpeedLimits asset (Inspector-editable). They are the only
    // limits on the speeds the app sends: nothing is read from the server.
    public static SpeedRange LinearRange => SpeedLimits.Instance.EffectiveLinear;
    public static SpeedRange AngularRange => SpeedLimits.Instance.EffectiveAngular;

    public int Version = CurrentVersion;

    public ServerScheme Scheme = ServerScheme.Http;
    public string Host = DefaultHost;
    public int Port = DefaultPort;

    // NaN = "not set yet": Sanitize replaces it with the asset default (field initializers run during
    // deserialization, where Resources.Load is not allowed).
    public float LinearSpeed = float.NaN;
    public float AngularSpeed = float.NaN;

    // Debug visuals default to hidden: end users should not see the room mesh or the anchor axes.
    public bool MeshVisible = false;

    // Debug axes drawn on both controllers and on the wheelchair anchor (AnchorManager).
    public bool DebugAnchorsVisible = false;

    public ControlModeId ControlMode = ControlModeId.GazeDwell;

    public string SchemePrefix => Scheme == ServerScheme.Https ? "https" : "http";

    public static float GetLinearPreset(SpeedPreset preset) => LinearRange.GetPreset(preset);

    public static float GetAngularPreset(SpeedPreset preset) => AngularRange.GetPreset(preset);

    /// <summary>Clamps every field into its valid range. Never throws.</summary>
    public void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(Host)) Host = DefaultHost;
        Host = Host.Trim();
        if (Port < 1 || Port > 65535) Port = DefaultPort;
        if (!Enum.IsDefined(typeof(ServerScheme), Scheme)) Scheme = ServerScheme.Http;

        LinearSpeed = LinearRange.Clamp(LinearSpeed);
        AngularSpeed = AngularRange.Clamp(AngularSpeed);

        if (!Enum.IsDefined(typeof(ControlModeId), ControlMode)) ControlMode = ControlModeId.GazeDwell;

        Version = CurrentVersion;
    }

    public AppSettings Clone()
    {
        return JsonUtility.FromJson<AppSettings>(JsonUtility.ToJson(this));
    }
}

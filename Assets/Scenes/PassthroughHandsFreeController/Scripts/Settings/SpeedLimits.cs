using System;
using UnityEngine;

/// <summary>Range, default and presets of one user-adjustable speed (wheel velocity sent in /motion/vel2, mm/s).</summary>
[Serializable]
public class SpeedRange
{
    [Min(1f)] public float Min = 20f;
    [Tooltip("Hard upper limit. Nothing in the app sends a faster wheel speed for this setting (the RestThru server does not clamp vel2).")]
    [Min(1f)] public float Max = 200f;
    [Tooltip("Value used on first launch and by the Normal preset.")]
    [Min(1f)] public float Default = 100f;
    [Tooltip("Increment of the -/+ buttons.")]
    [Min(0.1f)] public float Step = 10f;
    [Min(1f)] public float SlowPreset = 60f;
    [Min(1f)] public float FastPreset = 150f;

    public SpeedRange() { }

    public SpeedRange(float min, float max, float def, float step, float slow, float fast)
    {
        Min = min; Max = max; Default = def; Step = step; SlowPreset = slow; FastPreset = fast;
    }

    public float Clamp(float value) => float.IsNaN(value) ? Default : Mathf.Clamp(value, Min, Max);

    public float GetPreset(SpeedPreset preset) => preset switch
    {
        SpeedPreset.Slow => SlowPreset,
        SpeedPreset.Fast => FastPreset,
        _ => Default
    };

    /// <summary>
    /// A consistent copy: Min &lt;= Default/presets &lt;= Max, Step &gt; 0. Never modifies this instance, so editing
    /// the asset in the Inspector (where a float field applies on every keystroke) cannot destroy other values.
    /// </summary>
    public SpeedRange Sanitized()
    {
        float min = Mathf.Max(1f, Min);
        float max = Mathf.Max(min, Max);
        return new SpeedRange(min, max,
            Mathf.Clamp(Default, min, max),
            Mathf.Max(0.1f, Step),
            Mathf.Clamp(SlowPreset, min, max),
            Mathf.Clamp(FastPreset, min, max));
    }

    /// <summary>True when the raw values are already consistent (no clamping needed).</summary>
    public bool IsConsistent =>
        Min >= 1f && Max >= Min && Step >= 0.1f &&
        Default >= Min && Default <= Max &&
        SlowPreset >= Min && SlowPreset <= Max &&
        FastPreset >= Min && FastPreset <= Max;
}

/// <summary>
/// Single source of truth for the user-adjustable speed limits (linear and rotation), editable in the Inspector.
/// The asset must live in a Resources folder as "SpeedLimits" so static code (<see cref="AppSettings"/>,
/// <see cref="SettingsStore"/>) can load it without a scene reference. When the asset is missing, the built-in
/// defaults below are used and a warning is logged.
/// Changing these values changes how fast the real wheelchair can move.
/// </summary>
[CreateAssetMenu(fileName = "SpeedLimits", menuName = "PTHFC/Speed Limits")]
public class SpeedLimits : ScriptableObject
{
    public const string ResourcePath = "SpeedLimits";

    [Tooltip("Linear (forward/back) wheel speed, mm/s.")]
    public SpeedRange Linear = new(20f, 200f, 100f, 10f, 60f, 150f);

    [Tooltip("Rotation speed: each wheel turns at +/- this value, mm/s. Its Default is also the reference that scales the AlignHeading2D max rotation.")]
    public SpeedRange Angular = new(20f, 100f, 50f, 5f, 30f, 80f);

    private static SpeedLimits _instance;

    public static SpeedLimits Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<SpeedLimits>(ResourcePath);
            if (_instance == null)
            {
                DebugLogger.LogWarning($"SpeedLimits asset not found at Resources/{ResourcePath}; using built-in defaults.");
                _instance = CreateInstance<SpeedLimits>();
            }
            WarnIfInconsistent(_instance.Linear, "Linear");
            WarnIfInconsistent(_instance.Angular, "Angular");
            return _instance;
        }
    }

    /// <summary>Linear range as used by the app (consistent copy of the asset values).</summary>
    public SpeedRange EffectiveLinear => (Linear ?? new SpeedRange(20f, 200f, 100f, 10f, 60f, 150f)).Sanitized();

    /// <summary>Angular range as used by the app (consistent copy of the asset values).</summary>
    public SpeedRange EffectiveAngular => (Angular ?? new SpeedRange(20f, 100f, 50f, 5f, 30f, 80f)).Sanitized();

    // Values are deliberately NOT corrected in OnValidate: the Inspector applies each keystroke, so an intermediate
    // value (e.g. Max = 5 while typing 500) would permanently clamp Default and the presets.
    private static void WarnIfInconsistent(SpeedRange range, string label)
    {
        if (range != null && !range.IsConsistent)
            DebugLogger.LogWarning($"SpeedLimits.{label} is inconsistent (Default/presets outside [Min, Max] or Step too small); the app clamps it at runtime. Fix the asset.");
    }
}

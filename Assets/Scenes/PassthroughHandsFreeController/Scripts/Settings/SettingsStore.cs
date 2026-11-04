using System;
using UnityEngine;

/// <summary>
/// Single source of truth for persisted user settings.
/// Stores <see cref="AppSettings"/> as JSON under one PlayerPrefs key and migrates the legacy
/// RestThru_ServerIP / RestThru_ServerPort keys on first load.
/// </summary>
public static class SettingsStore
{
    private const string PrefsKey = "PTHFC_AppSettings";
    private const string LegacyPrefsKeyServerIP = "RestThru_ServerIP";
    private const string LegacyPrefsKeyServerPort = "RestThru_ServerPort";

    private static AppSettings _current;

    /// <summary>Raised after settings change and are saved. Receives a copy of the new settings.</summary>
    public static event Action<AppSettings> Changed;

    public static bool HasSavedSettings => PlayerPrefs.HasKey(PrefsKey) || PlayerPrefs.HasKey(LegacyPrefsKeyServerIP);

    /// <summary>A copy of the current settings. Mutating it has no effect; use <see cref="Update"/>.</summary>
    public static AppSettings Current
    {
        get
        {
            EnsureLoaded();
            return _current.Clone();
        }
    }

    /// <summary>Applies <paramref name="mutate"/> to a copy, sanitizes, saves and notifies listeners.</summary>
    public static void Update(Action<AppSettings> mutate)
    {
        EnsureLoaded();
        AppSettings next = _current.Clone();
        mutate?.Invoke(next);
        next.Sanitize();
        _current = next;
        Save();
        DebugLogger.Log($"Settings updated: {JsonUtility.ToJson(_current)}");
        Changed?.Invoke(_current.Clone());
    }

    public static void ResetToDefaults()
    {
        Update(s =>
        {
            AppSettings defaults = new();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), s);
        });
    }

    private static void EnsureLoaded()
    {
        if (_current != null) return;

        AppSettings loaded = null;
        if (PlayerPrefs.HasKey(PrefsKey))
        {
            try
            {
                loaded = JsonUtility.FromJson<AppSettings>(PlayerPrefs.GetString(PrefsKey));
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to parse saved settings, using defaults. {ex.Message}");
            }
        }

        if (loaded == null)
        {
            loaded = new AppSettings();
            if (PlayerPrefs.HasKey(LegacyPrefsKeyServerIP))
            {
                loaded.Host = PlayerPrefs.GetString(LegacyPrefsKeyServerIP, AppSettings.DefaultHost);
                loaded.Port = PlayerPrefs.GetInt(LegacyPrefsKeyServerPort, AppSettings.DefaultPort);
                DebugLogger.Log($"Migrated legacy server settings: {loaded.Host}:{loaded.Port}");
            }
        }

        loaded.Sanitize();
        _current = loaded;
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_current));
        PlayerPrefs.Save();
    }
}

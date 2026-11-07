using System;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Threading;

// TODO : fix name typo
public static class RestThruServer
{
    #region Endpoints and payloads
    public const string DefaultServerIP = AppSettings.DefaultHost;
    public const int DefaultServerPort = AppSettings.DefaultPort;

    // Address is owned by SettingsStore; these are read-through accessors.
    public static bool HasSavedConfiguration => SettingsStore.HasSavedSettings;
    public static ServerScheme Scheme => SettingsStore.Current.Scheme;
    public static string ServerIP => SettingsStore.Current.Host;
    public static int ServerPort => SettingsStore.Current.Port;

    public static string AddressDisplay
    {
        get
        {
            AppSettings s = SettingsStore.Current;
            return s.Scheme == ServerScheme.Https ? $"https://{s.Host}:{s.Port}" : $"{s.Host}:{s.Port}";
        }
    }

    public static string BaseUrl
    {
        get
        {
            AppSettings s = SettingsStore.Current;
            return $"{s.SchemePrefix}://{s.Host}:{s.Port}";
        }
    }

    public static string Velocity2Endpoint => $"{BaseUrl}/motion/vel2";
    public static string StopEndpoint => $"{BaseUrl}/motion/stop";
    public static string LimitsEndpoint => $"{BaseUrl}/motion/limits";

    /// <summary>
    /// Maximum wheel speed reported by GET /motion/limits, or null when unknown.
    /// Parsed leniently (see <see cref="TryParseMaxWheelSpeed"/>) because the exact response schema may vary between servers.
    /// </summary>
    public static float? ServerMaxWheelSpeed { get; private set; }

    public static event Action<float?> LimitsUpdated;

    public static void ResetToDefault()
    {
        SettingsStore.Update(s =>
        {
            s.Scheme = ServerScheme.Http;
            s.Host = DefaultServerIP;
            s.Port = DefaultServerPort;
        });
        ClearLimits();
        DebugLogger.Log($"RestThru server reset to default: {BaseUrl}");
    }

    public static bool TrySetServerAddress(string host, string portString, out string errorMessage)
    {
        return TrySetServerAddress(Scheme, host, portString, out errorMessage);
    }

    public static bool TrySetServerAddress(ServerScheme scheme, string host, string portString, out string errorMessage)
    {
        if (!ValidateHost(host, out string trimmedHost, out errorMessage)) return false;

        if (string.IsNullOrWhiteSpace(portString) || !int.TryParse(portString.Trim(), out int parsedPort) || parsedPort < 1 || parsedPort > 65535)
        {
            errorMessage = "Port must be between 1 and 65535.";
            return false;
        }

        SettingsStore.Update(s =>
        {
            s.Scheme = scheme;
            s.Host = trimmedHost;
            s.Port = parsedPort;
        });
        ClearLimits();

        DebugLogger.Log($"RestThru server configured to {BaseUrl}");
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>Accepts a dotted IPv4 address or a DNS hostname.</summary>
    public static bool ValidateHost(string host, out string trimmedHost, out string errorMessage)
    {
        trimmedHost = host?.Trim() ?? string.Empty;
        if (trimmedHost.Length == 0)
        {
            errorMessage = "Host cannot be empty.";
            return false;
        }

        bool looksNumeric = Regex.IsMatch(trimmedHost, @"^[\d.]+$");
        if (looksNumeric)
        {
            if (trimmedHost.Split('.').Length != 4 ||
                !IPAddress.TryParse(trimmedHost, out IPAddress parsedIp) ||
                parsedIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                errorMessage = "Invalid IPv4 format (e.g. 192.168.0.2).";
                return false;
            }
        }
        else if (Uri.CheckHostName(trimmedHost) != UriHostNameType.Dns)
        {
            errorMessage = "Invalid hostname.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static void ClearLimits()
    {
        if (ServerMaxWheelSpeed == null) return;
        ServerMaxWheelSpeed = null;
        LimitsUpdated?.Invoke(null);
    }

    /// <summary>
    /// Extracts a maximum wheel speed from a /motion/limits JSON body: the smallest positive numeric value
    /// whose key mentions "vel" or "speed" (and not "acc"). Returns false when nothing matches.
    /// </summary>
    public static bool TryParseMaxWheelSpeed(string json, out float maxWheelSpeed)
    {
        maxWheelSpeed = 0f;
        if (string.IsNullOrEmpty(json)) return false;

        bool found = false;
        foreach (Match m in Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)"))
        {
            string key = m.Groups[1].Value.ToLowerInvariant();
            if (!(key.Contains("vel") || key.Contains("speed")) || key.Contains("acc")) continue;
            if (!float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)) continue;
            value = Mathf.Abs(value);
            if (value <= 0f) continue;
            if (!found || value < maxWheelSpeed) maxWheelSpeed = value;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// Accepts any TLS certificate (self-signed, untrusted, or with a host/IP mismatch).
    /// The connection is still encrypted, but the server identity is NOT verified:
    /// the user is trusted to type the right server address.
    /// </summary>
    private sealed class AcceptAnyCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }

    /// <summary>Attaches <see cref="AcceptAnyCertificateHandler"/> to HTTPS requests.</summary>
    private static void ApplyTlsPolicy(UnityWebRequest request)
    {
        if (request.uri != null && request.uri.Scheme == Uri.UriSchemeHttps)
        {
            request.certificateHandler = new AcceptAnyCertificateHandler();
            request.disposeCertificateHandlerOnDispose = true;
        }
    }

    public static async Task<bool> CheckServerReachabilityAsync(int timeoutSeconds = 2)
    {
        string url = LimitsEndpoint;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ApplyTlsPolicy(request);
            request.timeout = Mathf.Max(1, timeoutSeconds);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // HTTP 200 from /motion/limits (or any valid HTTP response < 500) confirms the RestThru server is reachable
            bool reachable = request.result == UnityWebRequest.Result.Success ||
                             (request.result != UnityWebRequest.Result.ConnectionError && request.responseCode > 0 && request.responseCode < 500);
            DebugLogger.Log($"Server reachability check ({url}): reachable={reachable} (code={request.responseCode}, result={request.result}, error={request.error})");

            if (request.result == UnityWebRequest.Result.Success && url == LimitsEndpoint)
            {
                string body = request.downloadHandler?.text;
                float? parsed = TryParseMaxWheelSpeed(body, out float maxSpeed) ? maxSpeed : null;
                if (parsed == null) DebugLogger.LogWarning($"Could not find a max speed in /motion/limits response: {body}");
                if (parsed != ServerMaxWheelSpeed)
                {
                    ServerMaxWheelSpeed = parsed;
                    DebugLogger.Log($"Server max wheel speed: {(parsed.HasValue ? parsed.Value.ToString() : "unknown")}");
                    LimitsUpdated?.Invoke(parsed);
                }
            }
            return reachable;
        }
    }

    [System.Serializable]
    public class VelocityData
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
    public class Velocity2Payload
    {
        public VelocityData vel2;

        public Velocity2Payload() { }

        public Velocity2Payload(float r, float l)
        {
            vel2 = new(r, l);
        }
    }

    [System.Serializable]
    public class PoseData
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
    public class PosePayload
    {
        public PoseData pose { get; private set; }

        public PosePayload(float x, float y, float th)
        {
            pose = new(x, y, th);
        }
    }

    public class ServerResponse
    {
        public long StatusCode { get; private set; }
        public string ResponseText { get; private set; }
        public string ErrorMessage { get; private set; }
        public UnityWebRequest.Result Result { get; private set; }

        public ServerResponse(long StatusCode, string ResponseText, string ErrorMessage, UnityWebRequest.Result Result)
        {
            this.StatusCode = StatusCode;
            this.ResponseText = ResponseText;
            this.ErrorMessage = ErrorMessage;
            this.Result = Result;
        }

        public override string ToString()
        {
            return $"StatusCode={StatusCode} ; ResponseText={ResponseText} ; ErrorMessage={ErrorMessage} ; Result={Result}";
        }
    }

    public class ServerConnectionException : System.Exception
    {
        public ServerConnectionException(string message) : base(message) { }
    }

    #endregion
    #region Server Communication Logic
    public static Task<ServerResponse> SendPayloadToServer(string endpoint, string verb)
    {
        return SendPayloadToServer<object>(endpoint, verb, null, CancellationToken.None);
    }

    public static Task<ServerResponse> SendPayloadToServer<T>(string endpoint, string verb, T payload)
    {
        return SendPayloadToServer<object>(endpoint, verb, payload, CancellationToken.None);
    }

    public static async Task<ServerResponse> SendPayloadToServer<T>(string endpoint, string verb, T payload, CancellationToken ct, int timeoutSeconds = 10)
    {
        string json = (payload == null) ? "{}" : JsonUtility.ToJson(payload);
        DebugLogger.Log($"endpoint={endpoint} ; verb={verb} ; json={json}");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new(endpoint, verb))
        {
            ApplyTlsPolicy(request);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(1, timeoutSeconds);

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    DebugLogger.LogWarning("Task cancelled. Aborting web operation.");
                    request.Abort();
                    ct.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                DebugLogger.LogError($"Request error: {request.error}");
                if (request.result == UnityWebRequest.Result.ConnectionError) throw new ServerConnectionException("Unable to connect to server");
            }
            DebugLogger.Log($"Server answer: {request.downloadHandler.text}");

            return new ServerResponse(request.responseCode, request.downloadHandler.text, request.error, request.result);
        }
    }
    #endregion
}

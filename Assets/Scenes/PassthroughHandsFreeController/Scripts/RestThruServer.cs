using System.Net;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Threading;

// TODO : fix name typo
public static class RestThruServer
{
    #region Endpoints and payloads
    public const string DefaultServerIP = "192.168.0.2";
    public const int DefaultServerPort = 8080;

    private const string PrefsKeyServerIP = "RestThru_ServerIP";
    private const string PrefsKeyServerPort = "RestThru_ServerPort";

    private static string _serverIP;
    private static int _serverPort;
    private static bool _initialized;

    public static bool HasSavedConfiguration => PlayerPrefs.HasKey(PrefsKeyServerIP);

    public static string ServerIP
    {
        get
        {
            EnsureInitialized();
            return _serverIP;
        }
    }

    public static int ServerPort
    {
        get
        {
            EnsureInitialized();
            return _serverPort;
        }
    }

    public static string AddressDisplay => $"{ServerIP}:{ServerPort}";
    public static string BaseUrl => $"http://{ServerIP}:{ServerPort}";
    public static string Velocity2Endpoint => $"{BaseUrl}/motion/vel2";
    public static string StopEndpoint => $"{BaseUrl}/motion/stop";
    public static string LimitsEndpoint => $"{BaseUrl}/motion/limits";

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _serverIP = PlayerPrefs.GetString(PrefsKeyServerIP, DefaultServerIP);
        _serverPort = PlayerPrefs.GetInt(PrefsKeyServerPort, DefaultServerPort);
        _initialized = true;
    }

    public static void ResetToDefault()
    {
        _serverIP = DefaultServerIP;
        _serverPort = DefaultServerPort;
        _initialized = true;
        PlayerPrefs.SetString(PrefsKeyServerIP, _serverIP);
        PlayerPrefs.SetInt(PrefsKeyServerPort, _serverPort);
        PlayerPrefs.Save();
        DebugLogger.Log($"RestThru server reset to default: {BaseUrl}");
    }

    public static bool TrySetServerAddress(string ip, string portString, out string errorMessage)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(ip))
        {
            errorMessage = "IP address cannot be empty.";
            return false;
        }

        string trimmedIp = ip.Trim();
        string[] octets = trimmedIp.Split('.');
        if (octets.Length != 4 || !IPAddress.TryParse(trimmedIp, out IPAddress parsedIp) || parsedIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            errorMessage = "Invalid IPv4 format (e.g. 192.168.0.2).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(portString) || !int.TryParse(portString.Trim(), out int parsedPort) || parsedPort < 1 || parsedPort > 65535)
        {
            errorMessage = "Port must be between 1 and 65535.";
            return false;
        }

        _serverIP = trimmedIp;
        _serverPort = parsedPort;
        PlayerPrefs.SetString(PrefsKeyServerIP, _serverIP);
        PlayerPrefs.SetInt(PrefsKeyServerPort, _serverPort);
        PlayerPrefs.Save();

        DebugLogger.Log($"RestThru server configured to {BaseUrl}");
        errorMessage = string.Empty;
        return true;
    }

    public static async Task<bool> CheckServerReachabilityAsync(int timeoutSeconds = 2)
    {
        EnsureInitialized();
        string url = LimitsEndpoint;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // HTTP 200 from /motion/limits (or any valid HTTP response < 500) confirms the RestThru server is reachable
            bool reachable = request.result == UnityWebRequest.Result.Success ||
                             (request.result != UnityWebRequest.Result.ConnectionError && request.responseCode > 0 && request.responseCode < 500);
            DebugLogger.Log($"Server reachability check ({url}): reachable={reachable} (code={request.responseCode}, result={request.result})");
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

    public static async Task<ServerResponse> SendPayloadToServer<T>(string endpoint, string verb, T payload, CancellationToken ct)
    {
        string json = (payload == null) ? "{}" : JsonUtility.ToJson(payload);
        DebugLogger.Log($"endpoint={endpoint} ; verb={verb} ; json={json}");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new(endpoint, verb))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

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
                Debug.LogError($"Request error: {request.error}");
                if (request.result == UnityWebRequest.Result.ConnectionError) throw new ServerConnectionException("Unable to connect to server");
            }
            DebugLogger.Log($"Server answer: {request.downloadHandler.text}");

            return new ServerResponse(request.responseCode, request.downloadHandler.text, request.error, request.result);
        }
    }
    #endregion
}

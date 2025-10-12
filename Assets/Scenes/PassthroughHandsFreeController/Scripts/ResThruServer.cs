using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Unity.VisualScripting;
using System.Threading;


// TODO : fix name typo
public static class ResThruServer
{
    #region Endpoints and payloads
    public const string Velocity2Endpoint = "http://192.168.0.2:8080/motion/vel2";
    public const string StopEndpoint = "http://192.168.0.2:8080/motion/stop";

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
        return  SendPayloadToServer<object>(endpoint, verb, payload, CancellationToken.None);
    }

    public static async Task<ServerResponse> SendPayloadToServer<T>(string endpoint, string verb, T payload, CancellationToken ct)
    {
        string json = (payload == null) ? "" : JsonUtility.ToJson(payload);
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

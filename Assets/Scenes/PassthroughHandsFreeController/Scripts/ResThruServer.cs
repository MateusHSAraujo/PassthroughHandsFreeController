using System.Collections;
using System; 
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Oculus.Interaction.DistanceReticles;
using UnityEngine.UIElements;

public static class ResThruServer
{
    
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

    public static async Task<ServerResponse> SendPayloadToServer(string endpoint, string verb, string json)
    {
        DebugLogger.Log($"endpoint={endpoint} ; verb={verb} ; json={json}");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new(endpoint, verb))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();
            DebugLogger.Log($"Server answer: {request.downloadHandler.text}");
            if (request.result != UnityWebRequest.Result.Success) Debug.LogError($"Request error: {request.error}");

            return new ServerResponse(request.responseCode, request.downloadHandler.text, request.error, request.result);
        }
    }

}

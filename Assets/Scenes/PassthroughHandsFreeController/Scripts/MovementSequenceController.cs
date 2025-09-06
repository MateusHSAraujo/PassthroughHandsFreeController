using System;
using System.Threading.Tasks;
using UnityEngine;
using ZXing.QrCode.Internal;

public class MovementSequenceController : MonoBehaviour
{
    [Tooltip("The QR code scanner on the scene")]
    [SerializeField] private QrCodeManager m_qrCodeManager;

    [Tooltip("Debug prfed to indicate the center of the wheelchair")]
    [SerializeField] private GameObject d_prefab;


    public Action<bool> OnMovementSequenceEnded;

    void Start()
    {
        if (!m_qrCodeManager) DebugLogger.LogError("QrCodeManager not attributed on Unity.");
    }

    public async Task PerformMovementSequence(Vector3 TargetPosition)
    {
        DebugLogger.Log($"Movement sequence controller triggered to move to TargetPosition={TargetPosition}");
        Vector3? WheelchairPosition = await m_qrCodeManager.TriggerWheelchairPositionCalculation();

        if (WheelchairPosition.HasValue)
        {
            DebugLogger.Log($"Wheelchair location identified at: WheelchairPosition={WheelchairPosition.Value}");
            if (d_prefab) Instantiate(d_prefab, WheelchairPosition.Value, Quaternion.identity);
            Vector2 targetPos2 = new(TargetPosition.x, TargetPosition.z);
            Vector2 wheelchairPos2 = new(WheelchairPosition.Value.x, WheelchairPosition.Value.z);
            DebugLogger.Log($"Passing information down to the websocket. targetPos2={targetPos2} ; wheelchairPos2={wheelchairPos2}");
            AbortMovementSequence(true);
        }
        else
        {
            DebugLogger.Log("Movement sequence failed. Aborting");
            AbortMovementSequence(false);
        }

        return;
    }


    private void AbortMovementSequence(bool res)
    {
        OnMovementSequenceEnded?.Invoke(res);
    }
}

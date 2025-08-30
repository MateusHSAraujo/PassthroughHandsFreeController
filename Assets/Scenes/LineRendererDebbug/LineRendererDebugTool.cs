using UnityEngine;

public class LineRendererDebugTool : MonoBehaviour
{
    [SerializeField] private DebugLinePointer up;
    [SerializeField] private DebugLinePointer right;
    [SerializeField] private DebugLinePointer normal;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void UpdateAll(Vector3 pos, Vector3 up_dir, Vector3 right_dir, Vector3 normal_dir)
    {
        transform.position = pos;
        up.UpdateStartPoint(pos);
        right.UpdateStartPoint(pos);
        normal.UpdateStartPoint(pos);
        up.UpdateEndPoint(up_dir);
        right.UpdateEndPoint(right_dir);
        normal.UpdateEndPoint(normal_dir);
    }


}

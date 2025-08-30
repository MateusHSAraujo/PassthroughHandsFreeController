using UnityEngine;

public class DebugLinePointer : MonoBehaviour
{
    [SerializeField] private LineRenderer m_lineRender;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    public void UpdateStartPoint(Vector3 pos)
    {
        m_lineRender.SetPosition(0, pos);
    }

    public void UpdateEndPoint(Vector3 direction)
    {
        Vector3 newEndPoint = m_lineRender.GetPosition(0) + direction.normalized*0.1f;
        m_lineRender.SetPosition(1, newEndPoint);
    }
}

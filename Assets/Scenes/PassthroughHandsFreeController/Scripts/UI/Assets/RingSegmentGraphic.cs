using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural annular sector (donut wedge) drawn around the RectTransform rect center, in local canvas units.
/// Purely visual: it never takes part in raycasting or zone logic.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class RingSegmentGraphic : MaskableGraphic
{
    [Tooltip("Inner radius in local canvas units.")]
    [SerializeField] private float m_InnerRadius = 45f;

    [Tooltip("Outer radius in local canvas units.")]
    [SerializeField] private float m_OuterRadius = 150f;

    [Tooltip("Center angle of the wedge in degrees. 0 = +X (right), 90 = +Y (up), counter-clockwise positive.")]
    [SerializeField] private float m_CenterAngleDeg = 90f;

    [Tooltip("Total angular width of the wedge in degrees.")]
    [SerializeField] private float m_SweepDeg = 84f;

    [Tooltip("Number of quads used to approximate the arc (minimum 2).")]
    [SerializeField] private int m_Segments = 32;

    public float innerRadius
    {
        get => m_InnerRadius;
        set { if (!Mathf.Approximately(m_InnerRadius, value)) { m_InnerRadius = value; SetVerticesDirty(); } }
    }

    public float outerRadius
    {
        get => m_OuterRadius;
        set { if (!Mathf.Approximately(m_OuterRadius, value)) { m_OuterRadius = value; SetVerticesDirty(); } }
    }

    public float centerAngleDeg
    {
        get => m_CenterAngleDeg;
        set { if (!Mathf.Approximately(m_CenterAngleDeg, value)) { m_CenterAngleDeg = value; SetVerticesDirty(); } }
    }

    public float sweepDeg
    {
        get => m_SweepDeg;
        set { if (!Mathf.Approximately(m_SweepDeg, value)) { m_SweepDeg = value; SetVerticesDirty(); } }
    }

    public int segments
    {
        get => m_Segments;
        set { value = Mathf.Max(2, value); if (m_Segments != value) { m_Segments = value; SetVerticesDirty(); } }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float inner = Mathf.Max(0f, m_InnerRadius);
        float outer = Mathf.Max(m_OuterRadius, inner + 0.01f);
        int n = Mathf.Max(2, m_Segments);
        float sweep = Mathf.Clamp(m_SweepDeg, 0f, 360f);
        if (sweep <= 0f) return;

        float start = m_CenterAngleDeg - sweep * 0.5f;
        Vector2 center = rectTransform.rect.center;

        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            float a = (start + sweep * t) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(center + dir * inner, color, new Vector2(t, 0f));
            vh.AddVert(center + dir * outer, color, new Vector2(t, 1f));
        }

        for (int i = 0; i < n; i++)
        {
            int b = i * 2;
            // Vertices: b = inner(i), b+1 = outer(i), b+2 = inner(i+1), b+3 = outer(i+1).
            // Counter-clockwise angle order gives a clockwise winding as seen from the canvas front.
            vh.AddTriangle(b, b + 1, b + 3);
            vh.AddTriangle(b, b + 3, b + 2);
        }
    }

#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();
        raycastTarget = false;
    }

    protected override void OnValidate()
    {
        m_Segments = Mathf.Max(2, m_Segments);
        base.OnValidate();
    }
#endif
}

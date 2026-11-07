using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural isosceles triangle filling the RectTransform, pointing toward local +Y.
/// Rotate the RectTransform to point it elsewhere. Used for arrow heads on the gaze pad (no texture assets).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))] // Graphic itself only requires a RectTransform
public class TriangleGraphic : MaskableGraphic
{
#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();
        raycastTarget = false;
    }
#endif

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = new Vector3(r.xMin, r.yMin);
        vh.AddVert(v);
        v.position = new Vector3(r.center.x, r.yMax);
        vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMin);
        vh.AddVert(v);

        vh.AddTriangle(0, 1, 2);
    }
}

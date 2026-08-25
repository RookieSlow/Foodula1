using UnityEngine;
using UnityEngine.UI;

/// <summary>Asset-free circular uGUI graphic used by the stove-dial controls.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class GearDialGraphic : MaskableGraphic
{
    [SerializeField, Range(16, 64)] private int edgeSegments = 40;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect localRect = GetPixelAdjustedRect();
        Vector2 center = localRect.center;
        float radius = Mathf.Min(localRect.width, localRect.height) * 0.5f;
        if (radius <= 0f)
            return;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        vertex.uv0 = new Vector2(0.5f, 0.5f);
        vertexHelper.AddVert(vertex);

        for (int i = 0; i <= edgeSegments; i++)
        {
            float angle = Mathf.PI * 2f * i / edgeSegments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vertex.position = center + direction * radius;
            vertex.uv0 = direction * 0.5f + Vector2.one * 0.5f;
            vertexHelper.AddVert(vertex);
        }

        for (int i = 0; i < edgeSegments; i++)
            vertexHelper.AddTriangle(0, i + 1, i + 2);
    }
}

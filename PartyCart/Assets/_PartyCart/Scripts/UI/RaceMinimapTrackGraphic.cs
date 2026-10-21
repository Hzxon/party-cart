using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RaceMinimapTrackGraphic : MaskableGraphic
{
    [SerializeField] private Vector3[] worldRoute = new Vector3[0];
    private Vector2 worldCenter;
    private Vector2 worldSize = Vector2.one;

    public Rect MapArea
    {
        get
        {
            Rect r = rectTransform.rect;
            return new Rect(r.x + 22f, r.y + 38f, Mathf.Max(1f, r.width - 44f), Mathf.Max(1f, r.height - 76f));
        }
    }

    public void SetRoute(Vector3[] route)
    {
        worldRoute = route ?? new Vector3[0];
        RecalculateBounds();
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        RecalculateBounds();
    }

    private void RecalculateBounds()
    {
        if (worldRoute.Length == 0) return;
        Vector2 min = new Vector2(worldRoute[0].x, worldRoute[0].z), max = min;
        foreach (Vector3 point in worldRoute)
        {
            Vector2 p = new Vector2(point.x, point.z);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        worldCenter = (min + max) * 0.5f;
        worldSize = Vector2.Max(max - min, Vector2.one);
    }

    public Vector2 WorldToMap(Vector3 point)
    {
        Rect area = MapArea;
        float scale = Mathf.Min((area.width - 40f) / worldSize.x, (area.height - 40f) / worldSize.y);
        return area.center + (new Vector2(point.x, point.z) - worldCenter) * Mathf.Max(scale, 0.001f);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (worldRoute.Length < 3) return;
        var points = new Vector2[worldRoute.Length];
        for (int i = 0; i < points.Length; i++) points[i] = WorldToMap(worldRoute[i]);
        DrawRoute(vh, points, 13f, new Color(0f, 0.015f, 0.04f, 0.8f));
        DrawRoute(vh, points, 8f, new Color(0.92f, 0.97f, 1f, 1f));
        DrawRoute(vh, points, 2f, new Color(0.34f, 0.76f, 0.86f, 1f));
        Vector2 forward = (points[1] - points[0]).normalized;
        Vector2 side = new Vector2(-forward.y, forward.x);
        for (int row = 0; row < 2; row++) for (int column = 0; column < 6; column++)
        {
            Vector2 center = points[0] + side * ((column - 2.5f) * 3f) + forward * ((row - 0.5f) * 3f);
            RaceHUDMesh.Quad(vh, center - side * 1.5f - forward * 1.5f, center - side * 1.5f + forward * 1.5f,
                center + side * 1.5f + forward * 1.5f, center + side * 1.5f - forward * 1.5f,
                (row + column) % 2 == 0 ? Color.white : new Color(0.02f, 0.04f, 0.08f));
        }
    }

    private static void DrawRoute(VertexHelper vh, Vector2[] points, float width, Color tint)
    {
        var offsets = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 before = (points[i] - points[(i - 1 + points.Length) % points.Length]).normalized;
            Vector2 after = (points[(i + 1) % points.Length] - points[i]).normalized;
            Vector2 tangent = (before + after).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float alignment = Mathf.Max(0.5f, Vector2.Dot(normal, new Vector2(-before.y, before.x)));
            offsets[i] = normal * (width * 0.5f / alignment);
        }
        for (int i = 0; i < points.Length; i++)
        {
            int next = (i + 1) % points.Length;
            RaceHUDMesh.Quad(vh, points[i] - offsets[i], points[i] + offsets[i],
                points[next] + offsets[next], points[next] - offsets[next], tint);
        }
    }
}

internal static class RaceHUDMesh
{
    internal static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int first = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
        vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first + 2, first + 3, first);
    }

    internal static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 delta = to - from;
        Vector2 n = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
        Quad(vh, from - n, from + n, to + n, to - n, tint);
    }

    internal static void Ellipse(VertexHelper vh, Vector2 center, Vector2 radius, Color tint, int segments = 28)
    {
        int first = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            vh.AddVert(center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y), tint, Vector2.zero);
            if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
        }
    }

}

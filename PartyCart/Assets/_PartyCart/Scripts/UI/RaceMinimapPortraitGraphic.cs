using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RaceMinimapPortraitGraphic : MaskableGraphic
{
    [SerializeField] private Color paintColor = new Color(1f, 0.27f, 0.4f);
    [SerializeField] private bool playerMarker;
    private Vector2 targetOffset;

    public void Configure(Color paint, bool isPlayer)
    {
        paintColor = paint;
        playerMarker = isPlayer;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void SetTarget(Vector2 actualOffset)
    {
        if ((targetOffset - actualOffset).sqrMagnitude < 0.01f) return;
        targetOffset = actualOffset;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        Color ink = new Color(0.025f, 0.05f, 0.1f);
        if (targetOffset.sqrMagnitude > 16f)
        {
            RaceHUDMesh.Line(vh, targetOffset, Vector2.zero, 4f, ink);
            RaceHUDMesh.Line(vh, targetOffset, Vector2.zero, 2f, paintColor);
            RaceHUDMesh.Ellipse(vh, targetOffset, Vector2.one * 2.5f, paintColor, 12);
        }
        RaceHUDMesh.Ellipse(vh, Vector2.zero, Vector2.one * (r + (playerMarker ? 3f : 2f)), ink);
        RaceHUDMesh.Ellipse(vh, Vector2.zero, Vector2.one * r, playerMarker ? Color.white : paintColor);
        RaceHUDMesh.Ellipse(vh, Vector2.zero, Vector2.one * (r - 2f), paintColor);
        RaceHUDMesh.Ellipse(vh, new Vector2(0f, -0.08f * r), new Vector2(r * 0.7f, r * 0.72f), new Color(1f, 0.94f, 0.81f));
        RaceHUDMesh.Ellipse(vh, new Vector2(0f, r * 0.44f), new Vector2(r * 0.68f, r * 0.34f), paintColor);
        RaceHUDMesh.Quad(vh, new Vector2(-0.10f, 0.32f) * r, new Vector2(-0.10f, 0.75f) * r,
            new Vector2(0.10f, 0.75f) * r, new Vector2(0.10f, 0.32f) * r, Color.white);
        for (int side = -1; side <= 1; side += 2)
        {
            RaceHUDMesh.Ellipse(vh, new Vector2(side * 0.23f, -0.04f) * r, new Vector2(0.085f, 0.13f) * r, ink, 14);
            RaceHUDMesh.Ellipse(vh, new Vector2(side * 0.42f, -0.26f) * r, new Vector2(0.10f, 0.06f) * r, new Color(1f, 0.58f, 0.52f), 12);
        }
        for (int i = 0; i < 8; i++)
        {
            float a = (210f + i * 15f) * Mathf.Deg2Rad, b = a + 15f * Mathf.Deg2Rad;
            RaceHUDMesh.Line(vh, (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.22f + new Vector2(0f, -0.18f)) * r,
                (new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.22f + new Vector2(0f, -0.18f)) * r, Mathf.Max(1f, r * 0.08f), ink);
        }
    }
}

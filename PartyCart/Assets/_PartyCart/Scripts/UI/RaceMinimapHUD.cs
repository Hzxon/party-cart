using System.Collections.Generic;
using UnityEngine;

public sealed class RaceMinimapHUD : MonoBehaviour
{
    [SerializeField] private WaypointManager waypointManager;
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] opponents = new Transform[0];
    [SerializeField] private RaceMinimapTrackGraphic track;
    [SerializeField] private RaceMinimapPortraitGraphic[] portraits = new RaceMinimapPortraitGraphic[0];
    private Transform[] racers;
    private Vector2[] actual;
    private Vector2[] displayed;

    public void Configure(WaypointManager manager, Transform playerCar, Transform[] aiCars,
        RaceMinimapTrackGraphic graphic, RaceMinimapPortraitGraphic[] markers)
    {
        waypointManager = manager; player = playerCar; opponents = aiCars;
        track = graphic; portraits = markers;
        RefreshMap();
    }

    private void Start() => RefreshMap();

    public void RefreshMap()
    {
        if (waypointManager == null || track == null || player == null) return;
        var points = new List<Vector3>();
        foreach (Transform child in waypointManager.transform)
            if (child.GetComponent<Waypoint>() != null) points.Add(child.position);
        track.SetRoute(points.ToArray());
        racers = new Transform[opponents.Length + 1];
        racers[0] = player;
        for (int i = 0; i < opponents.Length; i++) racers[i + 1] = opponents[i];
        actual = new Vector2[racers.Length]; displayed = new Vector2[racers.Length];
        UpdateMarkers();
    }

    private void LateUpdate() => UpdateMarkers();

    private void UpdateMarkers()
    {
        if (racers == null || portraits.Length != racers.Length) return;
        Rect area = track.MapArea;
        Vector2 parentCenter = track.rectTransform.rect.center;
        area.position -= parentCenter;
        for (int i = 0; i < racers.Length; i++)
        {
            if (racers[i] == null) continue;
            actual[i] = track.WorldToMap(racers[i].position) - parentCenter;
            displayed[i] = actual[i];
        }
        // Keep faces readable in a close pack; short stems retain the exact road position.
        // The player's marker always stays at its true map position.
        for (int iteration = 0; iteration < 12; iteration++)
        {
            for (int i = 0; i < racers.Length; i++) for (int j = i + 1; j < racers.Length; j++)
            {
                Vector2 delta = displayed[j] - displayed[i];
                float distance = delta.magnitude;
                float spacing = (portraits[i].rectTransform.sizeDelta.x + portraits[j].rectTransform.sizeDelta.x) * 0.5f + 3f;
                if (distance >= spacing) continue;
                Vector2 direction = distance > 0.01f ? delta / distance : new Vector2(j % 2 == 0 ? 1f : -1f, 0.7f).normalized;
                Vector2 push = direction * (spacing - distance);
                if (i == 0) displayed[j] += push;
                else { displayed[i] -= push * 0.5f; displayed[j] += push * 0.5f; }
            }
            for (int i = 1; i < racers.Length; i++)
            {
                float radius = portraits[i].rectTransform.sizeDelta.x * 0.5f + 2f;
                displayed[i].x = Mathf.Clamp(displayed[i].x, area.xMin + radius, area.xMax - radius);
                displayed[i].y = Mathf.Clamp(displayed[i].y, area.yMin + radius, area.yMax - radius);
            }
        }
        for (int i = 0; i < racers.Length; i++)
        {
            if (racers[i] == null) continue;
            portraits[i].rectTransform.anchoredPosition = displayed[i];
            portraits[i].SetTarget(actual[i] - displayed[i]);
        }
        if (portraits[0].transform.GetSiblingIndex() != portraits[0].transform.parent.childCount - 1)
            portraits[0].transform.SetAsLastSibling();
    }
}

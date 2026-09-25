using UnityEngine;
using System.Collections.Generic;

public class WaypointManager : MonoBehaviour
{
    public static WaypointManager Instance;

    public List<Waypoint> waypoints = new();

    private void Awake()
    {
        Instance = this;
        BuildWaypointList();
    }

    private void BuildWaypointList()
    {
        waypoints.Clear();

        foreach (Transform child in transform)
        {
            Waypoint wp = child.GetComponent<Waypoint>();

            if (wp != null)
                waypoints.Add(wp);
        }

        for (int i = 0; i < waypoints.Count; i++)
        {
            waypoints[i].next = waypoints[(i + 1) % waypoints.Count];
        }

        Debug.Log($"Loaded {waypoints.Count} waypoints.");
    }

    public Waypoint GetWaypoint(int index)
    {
        return waypoints[index % waypoints.Count];
    }
}

using UnityEngine;

public class AIController : MonoBehaviour
{
    public Waypoint currentWaypoint;

    [Header("Movement")]
    public float waypointReachDistance = 2f;

    void Start()
    {
        currentWaypoint = WaypointManager.Instance.GetWaypoint(0);
    }

    void Update()
    {
        CheckWaypointReached();
    }

    private void CheckWaypointReached()
    {
        float distance = Vector3.Distance(transform.position, currentWaypoint.transform.position);

        if (distance <= waypointReachDistance)
        {
            currentWaypoint = currentWaypoint.next;
        }
    }

    private void OnDrawGizmos()
    {
        if (currentWaypoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, currentWaypoint.transform.position);
    }
}

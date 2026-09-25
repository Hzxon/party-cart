using UnityEngine;

[RequireComponent(typeof(CarController))]
public class AIController : MonoBehaviour
{
    private CarController car;

    [Header("Waypoint")]
    public Waypoint currentWaypoint;
    public float reachDistance = 3f;

    [Header("Driving")]
    public float targetSpeed = 18f;
    public float steeringSensitivity = 2f;

    private void Awake()
    {
        car = GetComponent<CarController>();
    }

    void Start()
    {
        currentWaypoint = WaypointManager.Instance.GetWaypoint(0);
    }

    void FixedUpdate()
    {
        DriveToWaypoint();
        CheckWaypointReached();
    }

    void DriveToWaypoint()
    {
        Vector3 localTarget = transform.InverseTransformPoint(currentWaypoint.transform.position);
        float steering = Mathf.Clamp(localTarget.x / localTarget.magnitude, -1f, 1f);

        steering *= steeringSensitivity;
        float speed = car.GetSpeed();
        float throttle = (speed < targetSpeed) ? 1f : 0f;
        float brake = (speed > targetSpeed + 2f) ? 1f : 0f;

        car.Move(steering, throttle, brake);
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

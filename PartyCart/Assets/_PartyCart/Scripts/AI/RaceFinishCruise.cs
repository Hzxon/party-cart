using System.Collections.Generic;
using UnityEngine;

/// <summary>Gentle post-race driving. Never writes lap progress, finish times or rankings.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class RaceFinishCruise : MonoBehaviour
{
    private Rigidbody body;
    private RaceProgressTracker racer;
    private FungsiGerak arcadeCar;
    private CarController wheelCar;
    private WaypointManager route;
    private bool trackerWasEnabled;
    private int nextWaypoint;
    private float speed;
    private readonly List<Behaviour> disabledDrivers = new List<Behaviour>();
    private readonly List<CollisionPair> collisionPairs = new List<CollisionPair>();

    public bool IsCruising { get; private set; }
    public float VisualSteerAngle { get; private set; }

    private struct CollisionPair
    {
        public Collider A;
        public Collider B;
        public bool WasIgnored;
    }

    public void Begin(RaceProgressTracker vehicle)
    {
        if (IsCruising || vehicle == null) return;
        body = GetComponent<Rigidbody>();
        racer = vehicle;
        arcadeCar = GetComponent<FungsiGerak>();
        wheelCar = GetComponent<CarController>();
        route = WaypointManager.Instance;
        trackerWasEnabled = racer.enabled;
        racer.enabled = false;
        foreach (var driver in GetComponents<Behaviour>())
        {
            if (!(driver is FungsiGerak || driver is PlayerInputController || driver is AIController) || !driver.enabled)
                continue;
            disabledDrivers.Add(driver);
            driver.enabled = false;
        }
        speed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity, transform.forward));
        nextWaypoint = FindNextWaypoint();
        IgnoreOtherRacers();
        IsCruising = true;
        enabled = true;
    }

    private int FindNextWaypoint()
    {
        if (route == null || route.waypoints.Count < 2) return 0;
        float nearest = float.PositiveInfinity;
        int next = 0;
        for (int i = 0; i < route.waypoints.Count; i++)
        {
            Vector3 a = route.GetWaypoint(i).transform.position;
            Vector3 segment = route.GetWaypoint(i + 1).transform.position - a;
            float t = Mathf.Clamp01(Vector3.Dot(body.position - a, segment) / Mathf.Max(segment.sqrMagnitude, 0.001f));
            float distance = (body.position - (a + segment * t)).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            next = (i + 1) % route.waypoints.Count;
        }
        return next;
    }

    private void IgnoreOtherRacers()
    {
        var manager = RaceManager.Instance;
        if (manager == null) return;
        var ownColliders = GetComponentsInChildren<Collider>();
        foreach (var other in manager.Racers)
        {
            if (other == null || other == racer) continue;
            foreach (var a in ownColliders)
                foreach (var b in other.GetComponentsInChildren<Collider>())
                {
                    if (a.isTrigger || b.isTrigger) continue;
                    // A second finisher must not own/restore a pair already ghosted by the first.
                    if (Physics.GetIgnoreCollision(a, b)) continue;
                    collisionPairs.Add(new CollisionPair { A = a, B = b, WasIgnored = false });
                    Physics.IgnoreCollision(a, b, true);
                }
        }
        // Ground, ramps and fences keep their normal physical collisions.
    }

    private void FixedUpdate()
    {
        if (!IsCruising || body == null || body.isKinematic) return;
        Vector3 target = GetLookAheadPoint();
        Vector3 local = transform.InverseTransformPoint(target);
        float distanceSquared = Mathf.Max(1f, local.x * local.x + local.z * local.z);
        float curvature = 2f * local.x / distanceSquared;
        float corner = Mathf.Clamp01(Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) / 65f);
        float cruiseSpeed = Mathf.Lerp(9f, 5f, corner);
        speed = Mathf.MoveTowards(speed, cruiseSpeed, 4f * Time.fixedDeltaTime);

        if (arcadeCar != null)
        {
            float yawRate = curvature * Mathf.Max(speed, 1f) * Mathf.Rad2Deg;
            float steering = Mathf.Clamp(yawRate / Mathf.Max(1f, arcadeCar.turnSpeed), -1f, 1f);
            arcadeCar.DriveAutopilot(speed, steering);
            VisualSteerAngle = steering * 28f;
        }
        else if (wheelCar != null)
        {
            float steering = Mathf.Clamp(Mathf.Atan(curvature * wheelCar.Wheelbase) * Mathf.Rad2Deg /
                Mathf.Max(1f, wheelCar.GetSteeringLimit()), -1f, 1f);
            float actualSpeed = wheelCar.GetSpeed();
            wheelCar.Move(steering, Mathf.Clamp01((speed - actualSpeed) / 2f), Mathf.Clamp01((actualSpeed - speed) / 3f));
            VisualSteerAngle = wheelCar.CurrentSteerAngle;
        }
    }

    private Vector3 GetLookAheadPoint()
    {
        float lookAhead = Mathf.Clamp(speed * 0.7f + 4f, 6f, 16f);
        if (route == null || route.waypoints.Count < 2)
            return body.position + transform.forward * lookAhead;
        // Follow a private route cursor; finished race progress remains locked forever.
        for (int i = 0; i < route.waypoints.Count; i++)
        {
            Vector3 a = route.GetWaypoint(nextWaypoint - 1).transform.position;
            Vector3 b = route.GetWaypoint(nextWaypoint).transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(b - a, Vector3.up);
            if (Vector3.Dot(body.position - b, direction) < 0f) break;
            nextWaypoint = (nextWaypoint + 1) % route.waypoints.Count;
        }
        Vector3 from = route.GetWaypoint(nextWaypoint - 1).transform.position;
        Vector3 to = route.GetWaypoint(nextWaypoint).transform.position;
        Vector3 segment = to - from;
        float fraction = Mathf.Clamp01(Vector3.Dot(body.position - from, segment) / Mathf.Max(segment.sqrMagnitude, 0.001f));
        Vector3 point = from + segment * fraction;
        int index = nextWaypoint;
        for (int i = 0; i < route.waypoints.Count; i++)
        {
            Vector3 end = route.GetWaypoint(index).transform.position;
            float distance = Vector3.Distance(point, end);
            if (distance >= lookAhead)
                return Vector3.MoveTowards(point, end, lookAhead);
            lookAhead -= distance;
            point = end;
            index = (index + 1) % route.waypoints.Count;
        }
        return point;
    }

    public void StopCruising()
    {
        if (!IsCruising) return;
        IsCruising = false;
        foreach (var pair in collisionPairs)
            if (pair.A != null && pair.B != null) Physics.IgnoreCollision(pair.A, pair.B, pair.WasIgnored);
        collisionPairs.Clear();
        if (racer != null) racer.enabled = trackerWasEnabled;
        foreach (var driver in disabledDrivers) if (driver != null) driver.enabled = true;
        disabledDrivers.Clear();
    }

    private void OnDisable() => StopCruising();
}

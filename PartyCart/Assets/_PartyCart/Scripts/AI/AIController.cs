using UnityEngine;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(RaceProgressTracker))]
public class AIController : MonoBehaviour
{
    private CarController car;
    private RaceProgressTracker progress;

    private enum AIState
    {
        Driving,
        Recovering
    }

    private AIState state = AIState.Driving;

    private Waypoint targetWaypoint;

    [Header("Driving")]
    public float baseTargetSpeed = 16f;
    public float speedVariance = 1.5f;
    public float lookAheadDistance = 6f;
    public float laneOffset;

    [Header("Steering")]
    public float steeringSensitivity = 1f;
    public float maxSteeringAtHighSpeed = 0.55f;

    [Header("Corner Speed")]
    public float corneringSlowdownAngle = 40f;
    public float minimumCornerSpeedFactor = 0.35f;

    [Header("Recovery")]
    public LayerMask obstacleMask;
    public float sensorDistance = 1.8f;
    public float sensorOffset = 0.45f;
    public float stuckSpeedThreshold = 1f;
    public float stuckTime = 0.7f;
    public float reverseDuration = 1.1f;
    public float reverseThrottle = -0.6f;

    private float personalTargetSpeed;
    private float stuckTimer;
    private float recoveryTimer;
    private float reverseSteering;

    private bool leftBlocked;
    private bool centerBlocked;
    private bool rightBlocked;
    private float avoidanceLane;
    private float lastSteering;
    private readonly RaycastHit[] sensorHits = new RaycastHit[24];
    private readonly RaycastHit[] recoveryGroundHits = new RaycastHit[8];
    private readonly Collider[] recoveryOverlaps = new Collider[64];
    private BoxCollider chassis;
    private int recoveryAttempts;
    private int lastProgressIndex;
    public int RecoveryResets { get; private set; }

    void Awake()
    {
        car = GetComponent<CarController>();
        progress = GetComponent<RaceProgressTracker>();
        chassis = GetComponent<BoxCollider>();
        avoidanceLane = laneOffset;
    }

    void Start()
    {
        personalTargetSpeed = baseTargetSpeed + Random.Range(-speedVariance, speedVariance);
        avoidanceLane = laneOffset;
    }

    void FixedUpdate()
    {
        if (WaypointManager.Instance == null || WaypointManager.Instance.waypoints.Count < 3 ||
            !RaceCountdownController.RaceStarted || progress.HasFinished)
        {
            car.Move(0f, 0f, 1f);
            return;
        }
        if (lastProgressIndex != progress.currentWaypointIndex)
        {
            lastProgressIndex = progress.currentWaypointIndex;
            recoveryAttempts = 0;
        }

        switch (state)
        {
            case AIState.Driving:
                targetWaypoint = GetLookAheadWaypoint();
                DetectObstacle();
                Drive();
                break;

            case AIState.Recovering:
                Recover();
                break;
        }
    }

    // =====================================================
    // LOOK AHEAD WAYPOINT
    // =====================================================

    Waypoint GetLookAheadWaypoint()
    {
        int index = progress.currentWaypointIndex;

        Vector3 lastPosition = transform.position;
        float travelled = 0f;

        Waypoint selected =
            WaypointManager.Instance.GetWaypoint(index);

        int visited = 0;
        float lookAhead = Mathf.Max(lookAheadDistance, car.GetSpeed() * 0.5f + 5f);
        while (travelled < lookAhead && visited++ < WaypointManager.Instance.waypoints.Count)
        {
            Waypoint wp =
                WaypointManager.Instance.GetWaypoint(index);

            if (wp == null) break;

            travelled += Vector3.Distance(
                lastPosition,
                wp.transform.position
            );

            lastPosition = wp.transform.position;
            selected = wp;

            index =
                (index + 1) %
                WaypointManager.Instance.waypoints.Count;
        }

        return selected;
    }

    // =====================================================
    // DRIVING
    // =====================================================

    void Drive()
    {
        if (targetWaypoint == null) return;

        Vector3 tangent = targetWaypoint.next != null
            ? targetWaypoint.next.transform.position - targetWaypoint.transform.position
            : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, tangent.normalized);
        float preferred = ChooseLane(targetWaypoint.transform.position, side);
        avoidanceLane = Mathf.MoveTowards(avoidanceLane, preferred, 5f * Time.fixedDeltaTime);
        Vector3 targetPosition = targetWaypoint.transform.position + side * avoidanceLane;
        Vector3 localTarget = transform.InverseTransformPoint(targetPosition);

        float angle =
            Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        float distanceSquared = Mathf.Max(1f, localTarget.x * localTarget.x + localTarget.z * localTarget.z);
        float requestedAngle = Mathf.Atan2(2f * car.Wheelbase * localTarget.x, distanceSquared) * Mathf.Rad2Deg;
        float steering = Mathf.Clamp(requestedAngle / Mathf.Max(1f, car.GetSteeringLimit()) * steeringSensitivity, -1f, 1f);

        // Kurangi steering kalau speed tinggi.
        float speed = car.GetSpeed(); // m/s

        float steeringLimit =
            Mathf.Lerp(
                1f,
                maxSteeringAtHighSpeed,
                Mathf.Clamp01(speed / 18f)
            );

        steering *= steeringLimit;
        steering = Mathf.Clamp(steering, -1f, 1f);
        lastSteering = steering;

        float targetSpeed = CalculateTargetSpeed(angle);
        if (centerBlocked) targetSpeed = Mathf.Min(targetSpeed, 8f);

        float throttle = 0f;
        float brake = 0f;

        if (speed < targetSpeed)
        {
            throttle =
                Mathf.Clamp01((targetSpeed - speed) / 3f);
        }
        else
        {
            brake =
                Mathf.Clamp01((speed - targetSpeed) / 2f);
        }

        // ========= ANTI SLIDE =========

        float steeringAmount = Mathf.Abs(steering);

        // Belok tajam -> kurangi gas.
        throttle *= Mathf.Lerp(1f, 0.25f, steeringAmount);

        // Belok sangat tajam + masih cepat -> rem sedikit.
        if (steeringAmount > 0.55f && speed > targetSpeed)
        {
            brake = Mathf.Max(brake, steeringAmount * 0.7f);
        }

        car.Move(steering, throttle, brake);
    }

    // =====================================================
    // TARGET SPEED
    // =====================================================

    float CalculateTargetSpeed(float steeringAngle)
    {
        int current = progress.currentWaypointIndex;

        Waypoint wp0 =
            WaypointManager.Instance.GetWaypoint(current);

        Waypoint wp1 =
            WaypointManager.Instance.GetWaypoint(
                (current + 1) %
                WaypointManager.Instance.waypoints.Count
            );

        Waypoint wp2 =
            WaypointManager.Instance.GetWaypoint(
                (current + 2) %
                WaypointManager.Instance.waypoints.Count
            );

        if (wp0 == null || wp1 == null || wp2 == null)
            return personalTargetSpeed;

        Vector3 dir1 =
            (wp1.transform.position - wp0.transform.position).normalized;

        Vector3 dir2 =
            (wp2.transform.position - wp1.transform.position).normalized;

        float cornerAngle =
            Vector3.Angle(dir1, dir2);

        float factor = 1f;

        if (cornerAngle > 70f)
            factor = 0.35f;
        else if (cornerAngle > 50f)
            factor = 0.50f;
        else if (cornerAngle > 30f)
            factor = 0.70f;
        else if (cornerAngle > 15f)
            factor = 0.85f;

        // Kalau mobil masih menghadap jauh dari target, anggap tikungan tajam.
        float steeringFactor =
            Mathf.InverseLerp(10f, 45f, Mathf.Abs(steeringAngle));

        factor *= Mathf.Lerp(1f, 0.55f, steeringFactor);

        return personalTargetSpeed * factor;
    }

    // =====================================================
    // OBSTACLE DETECTION
    // =====================================================

    void DetectObstacle()
    {
        Vector3 origin =
            transform.position + transform.up * 0.2f + transform.forward * 1.5f;

        Vector3 leftOrigin =
            origin - transform.right * sensorOffset;

        Vector3 rightOrigin =
            origin + transform.right * sensorOffset;

        float range = Mathf.Max(sensorDistance, car.GetSpeed() * 0.65f);
        leftBlocked = ClearDistance(leftOrigin, transform.forward, range) < range;
        centerBlocked = ClearDistance(origin, transform.forward, range) < range;
        rightBlocked = ClearDistance(rightOrigin, transform.forward, range) < range;

        bool blocked =
            leftBlocked || centerBlocked || rightBlocked;

        if (car.GetSpeed() > stuckSpeedThreshold)
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.fixedDeltaTime;

        if (stuckTimer >= stuckTime)
        {
            StartRecovery();
        }
    }

    // =====================================================
    // RECOVERY
    // =====================================================

    void StartRecovery()
    {
        recoveryAttempts++;
        // Rejoin the last passed node only after repeated failed reverse manoeuvres.
        // This never advances waypoint or lap progress.
        if (recoveryAttempts >= 3 && (progress.currentWaypointIndex != 0 || progress.lapCount > 0) &&
            TryResetToClearWaypoint())
        {
            recoveryAttempts = 0;
            stuckTimer = 0f;
            lastSteering = 0f;
            state = AIState.Driving;
            RecoveryResets++;
            return;
        }
        state = AIState.Recovering;

        recoveryTimer = reverseDuration;
        stuckTimer = 0f;

        if (Mathf.Abs(lastSteering) > 0.05f)
            reverseSteering = -Mathf.Sign(lastSteering);
        else if (leftBlocked && !rightBlocked)
            reverseSteering = 1f;
        else if (rightBlocked && !leftBlocked)
            reverseSteering = -1f;
        else
            reverseSteering = Random.value > 0.5f ? 1f : -1f;
    }

    bool TryResetToClearWaypoint()
    {
        var manager = WaypointManager.Instance;
        if (manager == null || chassis == null) return false;

        // Only search already-passed nodes, so recovery never awards race progress.
        int backtrack = Mathf.Min(6, manager.waypoints.Count - 1);
        if (progress.lapCount == 0)
            backtrack = Mathf.Min(backtrack, progress.currentWaypointIndex);

        Vector3 scale = transform.lossyScale;
        Vector3 halfExtents = Vector3.Scale(chassis.size * 0.5f,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) +
            new Vector3(0.25f, 0.1f, 0.25f);

        Physics.SyncTransforms();
        for (int back = 1; back <= backtrack; back++)
        {
            var node = manager.GetWaypoint(progress.currentWaypointIndex - back);
            Vector3 forward = Vector3.ProjectOnPlane(node.next.transform.position - node.transform.position, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) continue;
            Quaternion rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            Vector3 right = rotation * Vector3.right;

            for (int lane = 0; lane < 3; lane++)
            {
                float offset = lane == 0 ? Mathf.Clamp(laneOffset, -2.2f, 2.2f) : lane == 1 ? -2.2f : 2.2f;
                Vector3 roadPoint = node.transform.position + right * offset;
                if (!TryGetRecoveryGround(roadPoint, out float groundY)) continue;
                Vector3 position = new Vector3(roadPoint.x, groundY + 1.2f, roadPoint.z);
                Vector3 center = position + rotation * Vector3.Scale(chassis.center, scale);
                int count = Physics.OverlapBoxNonAlloc(center, halfExtents, recoveryOverlaps,
                    rotation, ~0, QueryTriggerInteraction.Ignore);
                // A full buffer is ambiguous; never teleport into unchecked geometry.
                if (count == recoveryOverlaps.Length) continue;
                bool occupied = false;
                for (int i = 0; i < count; i++)
                {
                    if (recoveryOverlaps[i].attachedRigidbody == car.rb) continue;
                    occupied = true;
                    break;
                }
                if (occupied) continue;

                car.ResetAfterRecovery(position, rotation);
                avoidanceLane = offset;
                return true;
            }
        }
        // Continue the normal reverse manoeuvre if every candidate is occupied.
        return false;
    }

    bool TryGetRecoveryGround(Vector3 roadPoint, out float groundY)
    {
        groundY = float.NegativeInfinity;
        int count = Physics.RaycastNonAlloc(roadPoint + Vector3.up * 2f, Vector3.down,
            recoveryGroundHits, 4f, ~0, QueryTriggerInteraction.Ignore);
        if (count == recoveryGroundHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = recoveryGroundHits[i];
            if (hit.rigidbody != null || hit.normal.y < 0.75f || Mathf.Abs(hit.point.y - roadPoint.y) > 0.5f)
                continue;
            groundY = Mathf.Max(groundY, hit.point.y);
        }
        return !float.IsNegativeInfinity(groundY);
    }

    void Recover()
    {
        recoveryTimer -= Time.fixedDeltaTime;

        car.Move(
            reverseSteering,
            reverseThrottle,
            0f
        );

        Vector3 origin =
            transform.position + transform.up * 0.35f;

        bool blocked =
            Physics.Raycast(
                origin,
                transform.forward,
                sensorDistance,
                obstacleMask
            );

        if ((!blocked && recoveryTimer < reverseDuration - 0.45f) || recoveryTimer <= 0f)
        {
            state = AIState.Driving;
        }
    }

    // Score a few lanes on the existing road instead of steering into a blocked racing line.
    float ChooseLane(Vector3 center, Vector3 side)
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f + transform.forward * 1.5f;
        float scan = Mathf.Max(7f, car.GetSpeed() * 0.65f);
        float bestLane = laneOffset;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            float candidate = i == 0 ? laneOffset : i == 1 ? 0f : i == 2 ? -2.2f : 2.2f;
            Vector3 point = center + side * candidate;
            point.y = origin.y;
            Vector3 direction = (point - origin).normalized;
            float clear = ClearDistance(origin, direction, scan);
            float score = clear - Mathf.Abs(candidate - laneOffset) * 0.12f - Mathf.Abs(candidate - avoidanceLane) * 0.04f;
            if (score > bestScore) { bestScore = score; bestLane = candidate; }
        }
        return bestLane;
    }

    float ClearDistance(Vector3 origin, Vector3 direction, float range)
    {
        int count = Physics.SphereCastNonAlloc(origin, 0.65f, direction, sensorHits, range, obstacleMask, QueryTriggerInteraction.Ignore);
        float nearest = range;
        for (int i = 0; i < count; i++)
        {
            if (sensorHits[i].rigidbody == car.rb) continue;
            nearest = Mathf.Min(nearest, sensorHits[i].distance);
        }
        return nearest;
    }

    // =====================================================
    // DEBUG GIZMOS
    // =====================================================

    void OnDrawGizmos()
    {
        Vector3 origin =
            transform.position + transform.up * 0.35f;

        Vector3 leftOrigin =
            origin - transform.right * sensorOffset;

        Vector3 rightOrigin =
            origin + transform.right * sensorOffset;

        Gizmos.color = leftBlocked ? Color.red : Color.cyan;
        Gizmos.DrawLine(
            leftOrigin,
            leftOrigin + transform.forward * sensorDistance
        );

        Gizmos.color = centerBlocked ? Color.red : Color.cyan;
        Gizmos.DrawLine(
            origin,
            origin + transform.forward * sensorDistance
        );

        Gizmos.color = rightBlocked ? Color.red : Color.cyan;
        Gizmos.DrawLine(
            rightOrigin,
            rightOrigin + transform.forward * sensorDistance
        );

        if (targetWaypoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetWaypoint.transform.position, 0.25f);
            Gizmos.DrawLine(
                transform.position,
                targetWaypoint.transform.position
            );
        }

        if (state == AIState.Recovering)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}

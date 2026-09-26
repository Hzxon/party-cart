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

    void Awake()
    {
        car = GetComponent<CarController>();
        progress = GetComponent<RaceProgressTracker>();

        personalTargetSpeed =
            baseTargetSpeed + Random.Range(-speedVariance, speedVariance);
    }

    void FixedUpdate()
    {
        if (WaypointManager.Instance == null) return;

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

        while (travelled < lookAheadDistance)
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

        Vector3 localTarget =
            transform.InverseTransformPoint(targetWaypoint.transform.position);

        float angle =
            Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        float steering =
            Mathf.Clamp(angle / 35f, -1f, 1f);

        // Kurangi steering kalau speed tinggi.
        float speed = car.GetSpeed(); // m/s

        float steeringLimit =
            Mathf.Lerp(
                1f,
                maxSteeringAtHighSpeed,
                Mathf.Clamp01(speed / 18f)
            );

        steering *= steeringSensitivity;
        steering *= steeringLimit;
        steering = Mathf.Clamp(steering, -1f, 1f);

        float targetSpeed = CalculateTargetSpeed(angle);

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
            transform.position + transform.up * 0.35f;

        Vector3 leftOrigin =
            origin - transform.right * sensorOffset;

        Vector3 rightOrigin =
            origin + transform.right * sensorOffset;

        leftBlocked = Physics.Raycast(
            leftOrigin,
            transform.forward,
            sensorDistance,
            obstacleMask
        );

        centerBlocked = Physics.Raycast(
            origin,
            transform.forward,
            sensorDistance,
            obstacleMask
        );

        rightBlocked = Physics.Raycast(
            rightOrigin,
            transform.forward,
            sensorDistance,
            obstacleMask
        );

        bool blocked =
            leftBlocked || centerBlocked || rightBlocked;

        if (!blocked)
        {
            stuckTimer = 0f;
            return;
        }

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
        state = AIState.Recovering;

        recoveryTimer = reverseDuration;
        stuckTimer = 0f;

        if (leftBlocked && !rightBlocked)
            reverseSteering = 1f;
        else if (rightBlocked && !leftBlocked)
            reverseSteering = -1f;
        else
            reverseSteering = Random.value > 0.5f ? 1f : -1f;
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

        if (!blocked || recoveryTimer <= 0f)
        {
            state = AIState.Driving;
        }
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

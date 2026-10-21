using UnityEngine;

/// <summary>Local-route recovery. Never awards waypoints, checkpoints, laps, or finish progress.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(RaceProgressTracker))]
public sealed class PartyCartPlayerRecovery : MonoBehaviour
{
    public const float HoldSeconds = 1f;
    public bool WrongWay { get; private set; }
    public float HoldProgress { get; private set; }
    public string Status { get; private set; }
    public int ResetCount { get; private set; }
    public bool NeedsHelp { get; private set; }
    private Rigidbody body;
    private BoxCollider chassis;
    private RaceProgressTracker tracker;
    private FungsiGerak driver;
    private Vector3 spawnPosition, safePosition;
    private Quaternion spawnRotation, safeRotation;
    private float safeProgress, wrongSeconds, nextSafeCheck, cooldownUntil, statusUntil, stuckSeconds;
    private bool hasSafe, requested;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly Collider[] overlaps = new Collider[64];

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        chassis = GetComponent<BoxCollider>();
        tracker = GetComponent<RaceProgressTracker>();
        driver = GetComponent<FungsiGerak>();
        spawnPosition = body.position;
        spawnRotation = body.rotation;
    }

    private bool CanRecover => RaceCountdownController.RaceStarted && !PartyCartGameOverlay.IsGamePaused &&
        tracker != null && !tracker.HasFinished && body != null && !body.isKinematic && driver != null && driver.enabled;

    private void Update()
    {
        if (!CanRecover) { HoldProgress = 0; WrongWay = false; wrongSeconds = 0; return; }
        if (Time.unscaledTime > statusUntil) Status = null;
        if (Time.time < cooldownUntil || requested) { HoldProgress = 0; return; }
        HoldProgress = Input.GetKey(KeyCode.R) ? Mathf.Clamp01(HoldProgress + Time.deltaTime / HoldSeconds) : 0f;
        if (HoldProgress >= 1f) { requested = true; HoldProgress = 0; }
    }

    private void FixedUpdate()
    {
        if (!CanRecover) return;
        var route = WaypointManager.Instance;
        if (route == null || route.waypoints.Count < 3 || chassis == null) return;
        float progress = tracker.GetProgress();
        TrackPose(route, progress, out var center, out var direction);
        Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        Vector3 roadOffset = Vector3.ProjectOnPlane(body.position - center, Vector3.up);
        stuckSeconds = velocity.sqrMagnitude < .25f && Mathf.Abs(Input.GetAxisRaw("Vertical")) > .5f
            ? stuckSeconds + Time.fixedDeltaTime : 0f;
        NeedsHelp = roadOffset.sqrMagnitude > 4.5f * 4.5f || Vector3.Dot(transform.up, Vector3.up) < .65f ||
            body.position.y < center.y - 3f || stuckSeconds >= 1.5f;
        bool backwards = Vector3.Dot(velocity, direction) < -1.5f;
        wrongSeconds = backwards ? wrongSeconds + Time.fixedDeltaTime : 0f;
        WrongWay = wrongSeconds >= .75f;

        if (requested)
        {
            requested = false;
            TryReset(route, progress);
            return;
        }
        if (Time.time < nextSafeCheck) return;
        nextSafeCheck = Time.time + .25f;
        Vector3 lateral = body.position - center;
        lateral.y = 0;
        if (lateral.sqrMagnitude > 4.5f * 4.5f || Vector3.Dot(transform.up, Vector3.up) < .9f ||
            Vector3.Dot(transform.forward, direction) < .3f || backwards) return;
        // Save a road-centred point behind the actual car, not an anticipatory AI target.
        if (!TryRoadPosition(center, out var position)) return;
        safePosition = position;
        safeRotation = Quaternion.LookRotation(direction, Vector3.up);
        safeProgress = progress;
        hasSafe = true;
    }

    public static void TrackPose(WaypointManager route, float progress, out Vector3 point, out Vector3 forward)
    {
        int step = Mathf.FloorToInt(progress);
        Vector3 a = route.GetWaypoint(step).transform.position;
        Vector3 b = route.GetWaypoint(step + 1).transform.position;
        point = Vector3.Lerp(a, b, progress - step);
        forward = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
    }

    private void TryReset(WaypointManager route, float progress)
    {
        Physics.SyncTransforms();
        int lap = Mathf.FloorToInt(progress / route.waypoints.Count);
        if (hasSafe && Mathf.FloorToInt(safeProgress / route.waypoints.Count) == lap &&
            TryClearLanes(safePosition, safeRotation, out var clear))
        {
            ResetAt(clear, safeRotation);
            return;
        }
        // Before crossing the start, prefer the original grid slot, not a point in front of it.
        if (progress < 0 && TryClearLanes(spawnPosition, spawnRotation, out clear))
        {
            ResetAt(clear, spawnRotation);
            return;
        }
        int step = Mathf.FloorToInt(progress);
        int lapStart = lap * route.waypoints.Count;
        for (int back = 0; back <= 5 && step - back >= lapStart; back++)
        {
            TrackPose(route, step - back, out var center, out var forward);
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            if (!TryClearLanes(center, rotation, out clear)) continue;
            ResetAt(clear, rotation);
            return;
        }
        Status = "Titik aman sedang terhalang. Coba lagi.";
        statusUntil = Time.unscaledTime + 2f;
        cooldownUntil = Time.time + .5f;
    }

    private bool TryClearLanes(Vector3 center, Quaternion rotation, out Vector3 clear)
    {
        Vector3 side = rotation * Vector3.right;
        Vector3 scale = transform.lossyScale;
        Vector3 extents = Vector3.Scale(chassis.size * .5f,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        extents += new Vector3(.12f, .02f, .12f);
        for (int lane = 0; lane < 3; lane++)
        {
            float offset = lane == 0 ? 0 : lane == 1 ? -2.2f : 2.2f;
            if (!TryRoadPosition(center + side * offset, out clear)) continue;
            Vector3 boxCenter = clear + rotation * Vector3.Scale(chassis.center, scale);
            int count = Physics.OverlapBoxNonAlloc(boxCenter, extents, overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) continue;
            bool blocked = false;
            for (int i = 0; i < count; i++)
                if (overlaps[i].attachedRigidbody != body) { blocked = true; break; }
            if (!blocked) return true;
        }
        clear = default;
        return false;
    }

    private bool TryRoadPosition(Vector3 point, out Vector3 position)
    {
        position = default;
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 2f, Vector3.down, groundHits, 5f, ~0, QueryTriggerInteraction.Ignore);
        if (count == groundHits.Length) return false;
        float ground = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.rigidbody != null || hit.normal.y < .75f || Mathf.Abs(hit.point.y - point.y) > 1.5f) continue;
            ground = Mathf.Max(ground, hit.point.y);
        }
        if (float.IsNegativeInfinity(ground)) return false;
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        position = new Vector3(point.x, ground + (chassis.size.y * .5f - chassis.center.y) * scaleY + .16f, point.z);
        return true;
    }

    private void ResetAt(Vector3 position, Quaternion rotation)
    {
        driver.ResetAt(position, rotation);
        var camera = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (camera != null) camera.SnapToTarget();
        wrongSeconds = 0;
        stuckSeconds = 0;
        NeedsHelp = false;
        WrongWay = false;
        cooldownUntil = Time.time + 2f;
        Status = "Kembali ke lintasan!";
        statusUntil = Time.unscaledTime + 1.3f;
        ResetCount++;
    }

    internal void CancelHold() { HoldProgress = 0; requested = false; wrongSeconds = 0; WrongWay = false; }
}

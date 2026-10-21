using UnityEngine;

/// <summary>Frames the actual winner above the horizontal Pit Pass results in its current map.</summary>
[DefaultExecutionOrder(100)]
public sealed class RaceFinishCamera : MonoBehaviour
{
    [SerializeField] private Camera raceCamera;
    private Transform winner;
    private CameraFollow follow;
    private bool followWasEnabled;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 localCenter;
    private Vector3 cameraOffset;
    private Vector3 offsetVelocity;
    private Quaternion aimOffset;
    private float radius;
    private int viewAngle;
    private readonly RaycastHit[] cameraHits = new RaycastHit[32];

    public Transform Target => winner;

    public void Configure(Camera cameraToUse) => raceCamera = cameraToUse;

    public void Present(Transform winningVehicle)
    {
        if (winningVehicle == null || winner == winningVehicle) return;
        if (raceCamera == null) raceCamera = Camera.main;
        if (raceCamera == null) return;
        Restore();
        winner = winningVehicle;
        originalPosition = raceCamera.transform.position;
        originalRotation = raceCamera.transform.rotation;
        follow = raceCamera.GetComponent<CameraFollow>();
        followWasEnabled = follow != null && follow.enabled;
        if (follow != null) follow.enabled = false;
        Bounds bounds = new Bounds(winner.position + Vector3.up, Vector3.one);
        bool foundRenderer = false;
        foreach (var renderer in winner.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
            if (!foundRenderer) { bounds = renderer.bounds; foundRenderer = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        // Stable local framing avoids zoom/bobbing as a moving kart rotates or its wheels spin.
        radius = Mathf.Max(1f, bounds.extents.magnitude);
        localCenter = winner.InverseTransformPoint(bounds.center);
        cameraOffset = raceCamera.transform.position - (bounds.center + Vector3.up * (radius * 0.12f));
        offsetVelocity = Vector3.zero;
        viewAngle = 0;
        aimOffset = Quaternion.Inverse(Quaternion.LookRotation(-cameraOffset, Vector3.up)) * originalRotation;
    }

    private void LateUpdate()
    {
        if (winner == null || raceCamera == null) return;
        float distance = radius / Mathf.Tan(raceCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 2.2f;
        Vector3 center = winner.TransformPoint(localCenter) + Vector3.up * (radius * 0.12f);
        Vector3 direction = ViewDirection(viewAngle);
        float clearance = ClearDistance(center, direction, distance);
        if (clearance < distance * 0.9f)
        {
            // Prefer a higher/road-facing shot over squeezing in against a fence or tree.
            for (int i = 0; i < 5; i++)
            {
                Vector3 candidate = ViewDirection(i);
                float candidateClearance = ClearDistance(center, candidate, distance);
                if (candidateClearance <= clearance + 0.5f) continue;
                viewAngle = i;
                direction = candidate;
                clearance = candidateClearance;
            }
        }
        cameraOffset = Vector3.SmoothDamp(cameraOffset, direction * distance, ref offsetVelocity,
            0.48f, Mathf.Infinity, Time.unscaledDeltaTime);
        Vector3 offset = cameraOffset;
        direction = offset.normalized;
        distance = offset.magnitude;

        // Keep the camera in front of nearby map geometry, ignoring the racers themselves.
        offset = offset.normalized * ClearDistance(center, direction, distance);
        Vector3 desiredPosition = center + offset;
        // Lift the winner clear of the bottom result strip.
        float blend = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
        aimOffset = Quaternion.Slerp(aimOffset, Quaternion.Euler(12f, 3f, 0f), blend);
        // Smooth the composition, not the look-at target: rotation lag would lose a moving kart.
        Quaternion desiredRotation = Quaternion.LookRotation(center - desiredPosition, Vector3.up) * aimOffset;
        raceCamera.transform.SetPositionAndRotation(
            desiredPosition,
            desiredRotation);
    }

    private Vector3 ViewDirection(int index)
    {
        switch (index)
        {
            case 1: return (winner.forward * 0.85f - winner.right * 0.65f + Vector3.up * 0.95f).normalized;
            case 2: return (winner.forward * 1.2f + Vector3.up * 1.25f).normalized;
            case 3: return (-winner.forward * 1.2f + Vector3.up * 1.25f).normalized;
            case 4: return (winner.forward * 0.5f + winner.right * 0.35f + Vector3.up * 1.7f).normalized;
            default: return (winner.forward * 0.85f + winner.right * 0.65f + Vector3.up * 0.85f).normalized;
        }
    }

    private float ClearDistance(Vector3 center, Vector3 direction, float distance)
    {
        int count = Physics.SphereCastNonAlloc(center, 0.25f, direction, cameraHits, distance,
            ~0, QueryTriggerInteraction.Ignore);
        float clear = distance;
        for (int i = 0; i < count; i++)
        {
            if (cameraHits[i].collider.GetComponentInParent<RaceProgressTracker>() != null) continue;
            clear = Mathf.Min(clear, Mathf.Max(0.5f, cameraHits[i].distance - 0.3f));
        }
        return clear;
    }

    public void Restore()
    {
        if (winner == null) return;
        if (raceCamera != null) raceCamera.transform.SetPositionAndRotation(originalPosition, originalRotation);
        if (follow != null) follow.enabled = followWasEnabled;
        winner = null;
    }

    private void OnDisable() => Restore();
}

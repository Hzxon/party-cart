using UnityEngine;

[DefaultExecutionOrder(90)]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Position")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.8f, -5.8f);

    [Header("Camera Settings")]
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float lookHeight = .75f;
    [SerializeField] private float lookAhead = 2.5f;
    private readonly RaycastHit[] cameraHits = new RaycastHit[24];

    private Vector3 LookTarget => target.position + Vector3.up * lookHeight +
        Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized * lookAhead;

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f) return;
        if (!TryGetFollowPose(out var desiredPosition, out var desiredRotation))
        {
            return;
        }

        // Posisi kamera mengikuti arah mobil

        // Perpindahan kamera dibuat halus
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime)
        );

        // Kamera melihat ke arah bagian atas Player
        Vector3 lookTarget = LookTarget;
        desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);

        // Position is already damped. Damping aim again makes the interpolated player
        // wobble on screen when the frame interval changes, especially while cornering.
        transform.rotation = desiredRotation;
    }

    public bool TryGetFollowPose(out Vector3 position, out Quaternion rotation)
    {
        if (target == null)
        {
            position = transform.position;
            rotation = transform.rotation;
            return false;
        }
        position = target.TransformPoint(offset);
        Vector3 aim = LookTarget;
        Vector3 ray = position - aim;
        float length = ray.magnitude;
        if (length > .01f)
        {
            float clear = length;
            int count = Physics.SphereCastNonAlloc(aim, .22f, ray / length, cameraHits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (cameraHits[i].collider.GetComponentInParent<RaceProgressTracker>() != null) continue;
                clear = Mathf.Min(clear, Mathf.Max(2f, cameraHits[i].distance - .25f));
            }
            position = aim + ray / length * clear;
        }
        rotation = Quaternion.LookRotation(aim - position);
        return true;
    }

    public void SnapToTarget()
    {
        if (TryGetFollowPose(out var position, out var rotation)) transform.SetPositionAndRotation(position, rotation);
    }
}

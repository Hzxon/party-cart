using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Position")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -8f);

    [Header("Camera Settings")]
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float lookHeight = 1f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Posisi kamera mengikuti arah mobil
        Vector3 desiredPosition = target.TransformPoint(offset);

        // Perpindahan kamera dibuat halus
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        // Kamera melihat ke arah bagian atas Player
        Vector3 lookTarget = target.position + Vector3.up * lookHeight;

        Quaternion desiredRotation = Quaternion.LookRotation(
            lookTarget - transform.position
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
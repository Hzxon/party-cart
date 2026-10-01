using UnityEngine;

public class PotHeadWheelAnimator : MonoBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private Transform vehicleRoot;

    [Header("Front steering")]
    [SerializeField] private Transform frontLeftSteer;
    [SerializeField] private Transform frontRightSteer;

    [Header("Wheel spin")]
    [SerializeField] private Transform frontLeftSpin;
    [SerializeField] private Transform frontRightSpin;
    [SerializeField] private Transform rearLeftSpin;
    [SerializeField] private Transform rearRightSpin;

    [Header("Visual settings")]
    [SerializeField] private float wheelRadius = 0.375f;
    [SerializeField] private float maxSteerAngle = 28f;
    [SerializeField] private float steeringSmoothness = 10f;
    [SerializeField] private float spinDirection = 1f;

    private Vector3 previousPosition;
    private Quaternion frontLeftBaseRotation;
    private Quaternion frontRightBaseRotation;

    private void Awake()
    {
        if (vehicleRoot == null)
        {
            vehicleRoot = transform;
        }

        previousPosition = vehicleRoot.position;
        if (frontLeftSteer != null) frontLeftBaseRotation = frontLeftSteer.localRotation;
        if (frontRightSteer != null) frontRightBaseRotation = frontRightSteer.localRotation;
    }

    private void LateUpdate()
    {
        Vector3 movement = vehicleRoot.position - previousPosition;
        previousPosition = vehicleRoot.position;

        float steering = Input.GetAxis("Horizontal") * maxSteerAngle;
        float blend = 1f - Mathf.Exp(-steeringSmoothness * Time.deltaTime);
        if (frontLeftSteer != null)
            frontLeftSteer.localRotation = Quaternion.Slerp(
                frontLeftSteer.localRotation,
                frontLeftBaseRotation * Quaternion.Euler(0f, steering, 0f),
                blend);
        if (frontRightSteer != null)
            frontRightSteer.localRotation = Quaternion.Slerp(
                frontRightSteer.localRotation,
                frontRightBaseRotation * Quaternion.Euler(0f, steering, 0f),
                blend);

        // FungsiGerak moves the Transform, so Rigidbody.linearVelocity remains zero.
        // Use actual forward displacement to animate both forward and reverse motion.
        if (movement.sqrMagnitude > 100f) return; // Ignore editor teleports.
        float forwardDistance = Vector3.Dot(movement, vehicleRoot.forward);
        float degrees = forwardDistance / Mathf.Max(wheelRadius, 0.01f) * Mathf.Rad2Deg * spinDirection;
        Spin(frontLeftSpin, degrees);
        Spin(frontRightSpin, degrees);
        Spin(rearLeftSpin, degrees);
        Spin(rearRightSpin, degrees);
    }

    private static void Spin(Transform pivot, float degrees)
    {
        if (pivot != null) pivot.Rotate(degrees, 0f, 0f, Space.Self);
    }
}

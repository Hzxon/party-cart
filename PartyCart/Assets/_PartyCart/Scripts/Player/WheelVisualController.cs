using UnityEngine;

public class WheelVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private CarController aiCar;

    [Header("Steering Pivots")]
    [SerializeField] private Transform frontLeftSteerPivot;
    [SerializeField] private Transform frontRightSteerPivot;

    [Header("Spin Pivots")]
    [SerializeField] private Transform frontLeftSpinPivot;
    [SerializeField] private Transform frontRightSpinPivot;
    [SerializeField] private Transform rearLeftSpinPivot;
    [SerializeField] private Transform rearRightSpinPivot;

    [Header("Visual Settings")]
    [SerializeField] private float wheelRadius = 0.64f;
    [SerializeField] private float maxSteerAngle = 28f;
    [SerializeField] private float steeringSmoothness = 10f;
    [SerializeField] private float spinDirection = -1f;

    private Quaternion frontLeftBaseRotation;
    private Quaternion frontRightBaseRotation;

    private void Awake()
    {
        if (carRigidbody == null)
        {
            carRigidbody = GetComponent<Rigidbody>();
        }

        if (frontLeftSteerPivot != null)
        {
            frontLeftBaseRotation = frontLeftSteerPivot.localRotation;
        }

        if (frontRightSteerPivot != null)
        {
            frontRightBaseRotation = frontRightSteerPivot.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (carRigidbody == null)
        {
            return;
        }

        // A cinematic can temporarily freeze the chassis before wheel physics starts.
        if (aiCar != null && !aiCar.enabled) return;

        if (aiCar != null)
        {
            SyncContactPosition(aiCar.frontLeft, frontLeftSteerPivot);
            SyncContactPosition(aiCar.frontRight, frontRightSteerPivot);
            SyncContactPosition(aiCar.rearLeft, rearLeftSpinPivot);
            SyncContactPosition(aiCar.rearRight, rearRightSpinPivot);
        }

        UpdateSteering();
        UpdateWheelSpin();
    }

    private void UpdateSteering()
    {
        float targetAngle = aiCar != null ? aiCar.CurrentSteerAngle : Input.GetAxis("Horizontal") * maxSteerAngle;

        if (frontLeftSteerPivot != null)
        {
            Quaternion targetRotation =
                frontLeftBaseRotation * Quaternion.Euler(0f, targetAngle, 0f);

            frontLeftSteerPivot.localRotation = Quaternion.Slerp(
                frontLeftSteerPivot.localRotation,
                targetRotation,
                steeringSmoothness * Time.deltaTime
            );
        }

        if (frontRightSteerPivot != null)
        {
            Quaternion targetRotation =
                frontRightBaseRotation * Quaternion.Euler(0f, targetAngle, 0f);

            frontRightSteerPivot.localRotation = Quaternion.Slerp(
                frontRightSteerPivot.localRotation,
                targetRotation,
                steeringSmoothness * Time.deltaTime
            );
        }
    }

    private void UpdateWheelSpin()
    {
        if (aiCar != null)
        {
            SpinFromCollider(aiCar.frontLeft, frontLeftSpinPivot);
            SpinFromCollider(aiCar.frontRight, frontRightSpinPivot);
            SpinFromCollider(aiCar.rearLeft, rearLeftSpinPivot);
            SpinFromCollider(aiCar.rearRight, rearRightSpinPivot);
            return;
        }
        float forwardSpeed = Vector3.Dot(
            carRigidbody.linearVelocity,
            transform.forward
        );

        float safeRadius = Mathf.Max(wheelRadius, 0.01f);
        float spinDegrees =
            (forwardSpeed / safeRadius) *
            Mathf.Rad2Deg *
            Time.deltaTime *
            spinDirection;

        RotateWheel(frontLeftSpinPivot, spinDegrees);
        RotateWheel(frontRightSpinPivot, spinDegrees);
        RotateWheel(rearLeftSpinPivot, spinDegrees);
        RotateWheel(rearRightSpinPivot, spinDegrees);
    }

    private static void SyncContactPosition(WheelCollider wheel, Transform pivot)
    {
        if (wheel == null || pivot == null || !wheel.enabled) return;
        wheel.GetWorldPose(out var position, out _);
        pivot.position = position;
    }

    private void SpinFromCollider(WheelCollider wheel, Transform pivot)
    {
        if (wheel != null) RotateWheel(pivot, wheel.rpm * 6f * Time.deltaTime * spinDirection);
    }

    private static void RotateWheel(Transform wheelPivot, float degrees)
    {
        if (wheelPivot != null)
        {
            wheelPivot.Rotate(degrees, 0f, 0f, Space.Self);
        }
    }
}

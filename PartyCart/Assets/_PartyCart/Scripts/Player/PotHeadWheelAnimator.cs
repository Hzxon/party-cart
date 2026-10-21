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
    private RaceFinishCruise finishCruise;
    private FungsiGerak driver;
    public Transform RearLeftWheel => rearLeftSpin;
    public Transform RearRightWheel => rearRightSpin;

    private void Awake()
    {
        if (vehicleRoot == null)
        {
            vehicleRoot = transform;
        }

        previousPosition = vehicleRoot.position;
        driver = vehicleRoot.GetComponent<FungsiGerak>();
        if (frontLeftSteer != null) frontLeftBaseRotation = frontLeftSteer.localRotation;
        if (frontRightSteer != null) frontRightBaseRotation = frontRightSteer.localRotation;
    }

    private void LateUpdate()
    {
        Vector3 movement = vehicleRoot.position - previousPosition;
        previousPosition = vehicleRoot.position;

        if (finishCruise == null) TryGetComponent(out finishCruise);
        float steering = finishCruise != null && finishCruise.IsCruising ? finishCruise.VisualSteerAngle :
            (RaceCountdownController.RaceStarted ? Input.GetAxis("Horizontal") * maxSteerAngle : 0f);
        if (driver != null && driver.enabled && driver.Drift != null && driver.Drift.IsDrifting)
            steering = driver.Drift.Steering(Input.GetAxis("Horizontal")) * maxSteerAngle;
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

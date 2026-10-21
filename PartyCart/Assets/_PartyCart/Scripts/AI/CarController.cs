using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Settings")]
    public float maxMotorTorque = 1800f;
    public float maxSteeringAngle = 32f;
    public float highSpeedSteeringAngle = 12f;
    public float brakeForce = 3500f;

    [Header("Steering")]
    public float steerSpeed = 180f;
    public float steeringAssist = 0.8f;

    [Header("Physics")]
    public Rigidbody rb;
    public Transform centerOfMass;

    [Header("Stability")]
    public float antiRollForce = 8000f;
    public float downforce = 110f;

    float currentSteer;

    public float CurrentSteerAngle => currentSteer;
    public float Wheelbase => Mathf.Abs(transform.InverseTransformPoint(frontLeft.transform.position).z -
                                        transform.InverseTransformPoint(rearLeft.transform.position).z);

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (centerOfMass != null) rb.centerOfMass = centerOfMass.localPosition;
    }

    void FixedUpdate()
    {
        ApplyAntiRoll(frontLeft, frontRight);
        ApplyAntiRoll(rearLeft, rearRight);

        rb.AddForce(
            -transform.up * downforce * rb.linearVelocity.magnitude,
            ForceMode.Force
        );

        ApplySteeringAssist();
    }

    public void Move(float steering, float throttle, float brake)
    {
        float steeringLimit = GetSteeringLimit();

        float targetSteer = steering * steeringLimit;

        currentSteer = Mathf.MoveTowards(
            currentSteer,
            targetSteer,
            steerSpeed * Time.fixedDeltaTime
        );

        frontLeft.steerAngle = currentSteer;
        frontRight.steerAngle = currentSteer;

        float torque = throttle * maxMotorTorque;

        rearLeft.motorTorque = torque;
        rearRight.motorTorque = torque;

        float brakeTorque = brake * brakeForce;

        if (throttle < 0)
            brakeTorque = 0;

        frontLeft.brakeTorque = brakeTorque;
        frontRight.brakeTorque = brakeTorque;
        rearLeft.brakeTorque = brakeTorque;
        rearRight.brakeTorque = brakeTorque;
    }

    void ApplySteeringAssist()
    {
        if (rb.linearVelocity.sqrMagnitude < 1f || Vector3.Dot(rb.linearVelocity, transform.forward) < 0f)
            return;

        Vector3 velocityDir = rb.linearVelocity.normalized;

        float angle = Vector3.SignedAngle(
            velocityDir,
            transform.forward,
            Vector3.up
        );

        rb.AddTorque(
            Vector3.up * -angle * steeringAssist,
            ForceMode.Acceleration
        );
    }

    public float GetSpeed()
    {
        return Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude;
    }

    public void ResetAfterRecovery(Vector3 position, Quaternion rotation)
    {
        // Recreate wheel contacts at the new location instead of carrying suspension/torque over.
        var wheels = new[] { frontLeft, frontRight, rearLeft, rearRight };
        var wasEnabled = new bool[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;
            wasEnabled[i] = wheels[i].enabled;
            wheels[i].motorTorque = 0f;
            wheels[i].brakeTorque = brakeForce;
            wheels[i].steerAngle = 0f;
            wheels[i].enabled = false;
        }

        var interpolation = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.position = position;
        rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentSteer = 0f;
        Physics.SyncTransforms();

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;
            wheels[i].enabled = wasEnabled[i];
            wheels[i].rotationSpeed = 0f;
        }
        rb.interpolation = interpolation;
        rb.WakeUp();
    }

    public float GetSteeringLimit()
    {
        return Mathf.Lerp(maxSteeringAngle, highSpeedSteeringAngle,
            Mathf.Clamp01(GetSpeed() * 3.6f / 90f));
    }

    void ApplyAntiRoll(WheelCollider left, WheelCollider right)
    {
        WheelHit hit;

        float leftTravel = 1f;
        float rightTravel = 1f;

        bool groundedLeft = left.GetGroundHit(out hit);

        if (groundedLeft)
        {
            Vector3 local =
                left.transform.InverseTransformPoint(hit.point);

            leftTravel =
                (-local.y - left.radius) /
                left.suspensionDistance;
        }

        bool groundedRight = right.GetGroundHit(out hit);

        if (groundedRight)
        {
            Vector3 local =
                right.transform.InverseTransformPoint(hit.point);

            rightTravel =
                (-local.y - right.radius) /
                right.suspensionDistance;
        }

        float force =
            (leftTravel - rightTravel) * antiRollForce;

        if (groundedLeft)
            rb.AddForceAtPosition(
                left.transform.up * -force,
                left.transform.position
            );

        if (groundedRight)
            rb.AddForceAtPosition(
                right.transform.up * force,
                right.transform.position
            );
    }
}

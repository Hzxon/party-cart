using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Settings")]
    public float maxMotorTorque = 1800f;
    public float maxSteeringAngle = 30f;
    public float breakForce = 2500f;

    public Rigidbody rb;

    public void Move(float steering, float throttle, float brake)
    {
        //steering
        float steerAngle = steering * maxSteeringAngle;
        frontLeft.steerAngle = steerAngle;
        frontRight.steerAngle = steerAngle;

        //motor
        float torque = throttle * maxMotorTorque;
        rearLeft.motorTorque = torque;
        rearRight.motorTorque = torque;

        //brake
        rearLeft.brakeTorque = brake * breakForce;
        rearRight.brakeTorque = brake * breakForce;
        frontLeft.brakeTorque = brake * breakForce;
        frontRight.brakeTorque = brake * breakForce;
    }

    public float GetSpeed() {
        return rb.linearVelocity.magnitude;
    }
}

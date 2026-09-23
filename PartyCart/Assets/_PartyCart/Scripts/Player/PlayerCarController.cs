using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCarController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float maxForwardSpeed = 18f;
    [SerializeField] private float maxReverseSpeed = 7f;
    [SerializeField] private float brakeStrength = 10f;

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private float sideFriction = 5f;

    private Rigidbody rb;
    private float throttleInput;
    private float steeringInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Update()
    {
        // W/S atau Arrow Up/Down
        throttleInput = Input.GetAxis("Vertical");

        // A/D atau Arrow Left/Right
        steeringInput = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
        MoveCar();
        TurnCar();
        ReduceSidewaysMovement();
    }

    private void MoveCar()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        // Bergerak maju
        if (throttleInput > 0f && localVelocity.z < maxForwardSpeed)
        {
            rb.AddForce(
                transform.forward * throttleInput * acceleration,
                ForceMode.Acceleration
            );
        }

        // Mundur
        if (throttleInput < 0f && localVelocity.z > -maxReverseSpeed)
        {
            rb.AddForce(
                transform.forward * throttleInput * acceleration,
                ForceMode.Acceleration
            );
        }

        // Rem ketika tidak menekan tombol gas atau mundur
        if (Mathf.Abs(throttleInput) < 0.1f && Mathf.Abs(localVelocity.z) > 0.1f)
        {
            rb.AddForce(
                -transform.forward * Mathf.Sign(localVelocity.z) * brakeStrength,
                ForceMode.Acceleration
            );
        }

        // Membatasi kecepatan maju dan mundur
        localVelocity.z = Mathf.Clamp(
            localVelocity.z,
            -maxReverseSpeed,
            maxForwardSpeed
        );

        rb.linearVelocity = transform.TransformDirection(localVelocity);
    }

    private void TurnCar()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float currentSpeed = Mathf.Abs(localVelocity.z);

        // Mobil hanya bisa berbelok jika sedang bergerak
        if (currentSpeed < 0.1f)
        {
            return;
        }

        float speedFactor = Mathf.Clamp01(currentSpeed / maxForwardSpeed);

        float direction = Mathf.Sign(localVelocity.z);

        float turnAmount =
            steeringInput *
            turnSpeed *
            speedFactor *
            direction *
            Time.fixedDeltaTime;

        Quaternion turnRotation = Quaternion.Euler(
            0f,
            turnAmount,
            0f
        );

        rb.MoveRotation(rb.rotation * turnRotation);
    }

    private void ReduceSidewaysMovement()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        // Mengurangi gerakan menyamping agar mobil terasa seperti kart
        localVelocity.x = Mathf.Lerp(
            localVelocity.x,
            0f,
            sideFriction * Time.fixedDeltaTime
        );

        rb.linearVelocity = transform.TransformDirection(localVelocity);
    }
}
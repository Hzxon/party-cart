using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FungsiGerak : MonoBehaviour
{
    public float speed = 10f;
    [Tooltip("Seberapa cepat mobil mencapai kecepatan yang diminta.")]
    public float acceleration = 10f;
    [Tooltip("Seberapa cepat mobil melambat saat gas dilepas atau arah diganti.")]
    public float deceleration = 14f;
    public float turnSpeed = 100f;
    public PartyCartDriftState Drift { get; private set; }
    public event System.Action DriftCharged;
    public event System.Action MiniBoostStarted;

    private Rigidbody body;
    private BoxCollider bodyCollider;
    private PhysicsMaterial bumperMaterial;
    private PhysicsMaterial originalMaterial;
    private float moveInput;
    private float turnInput;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    private void Awake()
    {
        Drift = new PartyCartDriftState();
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<BoxCollider>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Kart arcade tetap tegak; benturan masih boleh mendorong dan memutar arah mobil.
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.maxDepenetrationVelocity = 2f;
        body.solverIterations = Mathf.Max(body.solverIterations, 10);
        body.solverVelocityIterations = Mathf.Max(body.solverVelocityIterations, 4);

        if (bodyCollider != null)
        {
            originalMaterial = bodyCollider.sharedMaterial;
            bumperMaterial = new PhysicsMaterial("Player Kart Bumper")
            {
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            bodyCollider.sharedMaterial = bumperMaterial;
        }
    }

    private void Update()
    {
        if (PartyCartGameOverlay.IsGamePaused) { ClearInput(); return; }
        moveInput = Input.GetAxis("Vertical");
        turnInput = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
        Drive(moveInput, turnInput, Time.fixedDeltaTime, true, Input.GetKey(KeyCode.Space));
    }

    // The results screen takes over input without freezing the body or bypassing road physics.
    public void DriveAutopilot(float targetSpeed, float steering)
    {
        Drive(Mathf.Clamp01(targetSpeed / Mathf.Max(0.1f, speed)), steering, Time.fixedDeltaTime, false, false);
    }

    private void Drive(float throttle, float steering, float deltaTime, bool playerInput, bool driftHeld)
    {
        // Kemudi dikendalikan oleh MoveRotation. Jangan bawa putaran sisa benturan
        // ke frame berikutnya, termasuk saat berhenti atau belum menyentuh tanah.
        body.angularVelocity = Vector3.zero;
        bool grounded = IsGrounded();

        // Ambil kecepatan hasil simulasi agar tabrakan dapat memperlambat mobil.
        Vector3 forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
        Vector3 velocity = body.linearVelocity;
        float currentSpeed = Vector3.Dot(velocity, forward);
        bool wasReady = Drift.Ready, wasBoosting = Drift.IsBoosting;
        Drift.Step(throttle, steering, driftHeld, currentSpeed, grounded,
            playerInput && RaceCountdownController.RaceStarted && !PartyCartGameOverlay.IsGamePaused, deltaTime);
        if (!wasReady && Drift.Ready) DriftCharged?.Invoke();
        if (!wasBoosting && Drift.IsBoosting) MiniBoostStarted?.Invoke();
        if (!grounded) return;
        steering = Drift.Steering(steering);

        // Naikkan kecepatan secara bertahap agar mobil tidak langsung melesat.
        float targetSpeed = throttle * speed * (Drift.IsBoosting ? PartyCartDriftState.SpeedMultiplier : 1f);
        bool acceleratingInSameDirection =
            Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) &&
            (Mathf.Abs(currentSpeed) < 0.01f || Mathf.Sign(targetSpeed) == Mathf.Sign(currentSpeed));
        float speedChange = acceleratingInSameDirection ? acceleration : deceleration;
        if (Drift.IsBoosting && acceleratingInSameDirection) speedChange *= 3f;
        float nextSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            speedChange * deltaTime
        );

        Vector3 sideways = Vector3.ProjectOnPlane(velocity, Vector3.up) - forward * currentSpeed;
        float grip = 1f - Mathf.Exp(-(Drift.IsDrifting ? 3.6f : 8f) * deltaTime);
        Vector3 sliding = Drift.IsDrifting ? Vector3.ClampMagnitude(sideways, Mathf.Max(2f, Mathf.Abs(currentSpeed) * .5f)) : sideways;
        // Ubah momentum melalui Rigidbody, tanpa memindahkan Transform menembus collider.
        body.AddForce(forward * (nextSpeed - currentSpeed) + sliding * (1f - grip) - sideways, ForceMode.VelocityChange);

        // Tetap bisa belok saat meluncur setelah gas dilepas, sesuai arah gerak mobil.
        if (Mathf.Abs(currentSpeed) > 0.1f && Mathf.Abs(steering) > 0.01f)
        {
            float direction = Mathf.Sign(currentSpeed);
            float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / 3f);
            float driftTurn = Drift.IsDrifting ? 1.1f : 1f;
            body.MoveRotation(body.rotation * Quaternion.Euler(0f, steering * turnSpeed * driftTurn * speedFactor * deltaTime * direction, 0f));
        }
    }

    private bool IsGrounded()
    {
        if (bodyCollider == null) return false;
        Bounds bounds = bodyCollider.bounds;
        // Mulai di dalam bodi (raycast tidak mengenai collider sendiri).
        int count = Physics.RaycastNonAlloc(bounds.center, Vector3.down, groundHits,
            bounds.extents.y + 0.2f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider.attachedRigidbody != body && Vector3.Dot(hit.normal, Vector3.up) > 0.55f)
                return true;
        }
        return false;
    }

    private void OnDisable()
    {
        ClearInput();
        Drift?.CancelAll();
    }

    public void ClearInput()
    {
        moveInput = 0f;
        turnInput = 0f;
        // Pause/input handovers must not release a charged drift into a free boost.
        Drift?.InterruptCharge();
    }

    public void ResetAt(Vector3 position, Quaternion rotation)
    {
        ClearInput();
        Drift?.CancelAll();
        var interpolation = body.interpolation;
        body.interpolation = RigidbodyInterpolation.None;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = position;
        body.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        body.interpolation = interpolation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        RemoveWallLift(collision);
        CancelDriftAtWall(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        RemoveWallLift(collision);
        CancelDriftAtWall(collision);
    }

    private void CancelDriftAtWall(Collision collision)
    {
        if (collision.collider.attachedRigidbody != null || Drift == null) return;
        for (int i = 0; i < collision.contactCount; i++)
            if (Mathf.Abs(collision.GetContact(i).normal.y) < .55f) { Drift.CancelAll(); return; }
    }

    private void RemoveWallLift(Collision collision)
    {
        if (body.linearVelocity.y <= 0f) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            // Jangan ubah respons lantai/ramp; hanya hentikan tendangan ke atas dari sisi bumper/pagar.
            if (Mathf.Abs(collision.GetContact(i).normal.y) >= 0.55f) continue;
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            body.linearVelocity = velocity;
            return;
        }
    }

    private void OnDestroy()
    {
        if (bumperMaterial != null)
        {
            if (bodyCollider != null && bodyCollider.sharedMaterial == bumperMaterial)
                bodyCollider.sharedMaterial = originalMaterial;
            Destroy(bumperMaterial);
        }
    }
}

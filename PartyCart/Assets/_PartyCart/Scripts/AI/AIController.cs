using UnityEngine;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(RaceProgressTracker))]
public class AIController : MonoBehaviour
{
    private CarController car;
    private RaceProgressTracker progress;
    private Waypoint currentWaypoint;

    [Header("Driving (base value, tiap AI akan sedikit acak dari sini)")]
    public float baseTargetSpeed = 18f;
    public float steeringSensitivity = 10f;

    [Header("AI Personality")]
    [Tooltip("Rentang variasi kecepatan tiap AI, biar pack-nya nyebar (gak barengan kayak kereta)")]
    public float speedVariance = 3f;
    [Tooltip("Peluang AI 'sedikit salah' pas belok, biar keliatan manusiawi")]
    [Range(0f, 0.1f)] public float steeringNoiseChance = 0.03f;
    public float steeringNoiseAmount = 0.15f;

    [Header("Cornering (biar AI ngerem dikit sebelum tikungan tajam)")]
    [Tooltip("Sudut (derajat) antara segmen sekarang & berikutnya yang dianggap 'tikungan tajam penuh'")]
    public float corneringSlowdownAngle = 40f;
    [Tooltip("Fraksi minimum dari target speed pas di tikungan paling tajam (0.5 = setengah speed)")]
    [Range(0.1f, 1f)] public float corneringMinSpeedFactor = 0.5f;

    [Header("Rubber Banding")]
    public bool useRubberBanding = true;
    [Tooltip("Jarak progress (dalam satuan waypoint) sebelum efek rubber-band mulai berasa")]
    public float rubberBandRange = 15f;
    [Tooltip("Maksimal tambahan/pengurangan speed dari rubber-band")]
    public float rubberBandMaxBoost = 6f;

    private float personalTargetSpeed;

    private void Awake()
    {
        car = GetComponent<CarController>();
        progress = GetComponent<RaceProgressTracker>();

        // tiap AI punya "karakter" kecepatan sendiri
        personalTargetSpeed = baseTargetSpeed + Random.Range(-speedVariance, speedVariance);
    }

    void FixedUpdate()
    {
        if (WaypointManager.Instance == null) return;

        currentWaypoint = WaypointManager.Instance.GetWaypoint(progress.currentWaypointIndex);
        if (currentWaypoint == null) return;

        DriveToWaypoint();
    }

    void DriveToWaypoint()
    {
        Vector3 localTarget = transform.InverseTransformPoint(currentWaypoint.transform.position);

        // Pakai sudut (atan2), bukan x/magnitude -> tetap stabil walau waypoint
        // agak di belakang/samping mobil
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float steering = Mathf.Clamp(angle / 45f, -1f, 1f) * steeringSensitivity;
        steering = Mathf.Clamp(steering, -1f, 1f);

        // sedikit "human error" biar AI gak kelihatan robotic/sempurna
        if (Random.value < steeringNoiseChance)
        {
            steering += Random.Range(-steeringNoiseAmount, steeringNoiseAmount);
            steering = Mathf.Clamp(steering, -1f, 1f);
        }

        float effectiveTargetSpeed = GetRubberBandedTargetSpeed() * GetCorneringSpeedFactor();

        float speed = car.GetSpeed();
        float throttle = (speed < effectiveTargetSpeed) ? 1f : 0f;
        float brake = (speed > effectiveTargetSpeed + 2f) ? 1f : 0f;

        car.Move(steering, throttle, brake);
    }

    // Cek seberapa tajam tikungan DI DEPAN (bukan yang lagi dilalui sekarang),
    // dengan bandingin arah segmen sekarang vs segmen berikutnya.
    // Ini bikin AI mulai ngurangin speed SEBELUM nyampe apex, bukan pas udah kelewat.
    float GetCorneringSpeedFactor()
    {
        if (WaypointManager.Instance == null || currentWaypoint == null) return 1f;

        int nextIndex = (progress.currentWaypointIndex + 1) % WaypointManager.Instance.waypoints.Count;
        Waypoint nextWaypoint = WaypointManager.Instance.GetWaypoint(nextIndex);
        if (nextWaypoint == null) return 1f;

        Vector3 dirToCurrent = (currentWaypoint.transform.position - transform.position);
        Vector3 dirCurrentToNext = (nextWaypoint.transform.position - currentWaypoint.transform.position);

        if (dirToCurrent.sqrMagnitude < 0.001f || dirCurrentToNext.sqrMagnitude < 0.001f) return 1f;

        float cornerAngle = Vector3.Angle(dirToCurrent.normalized, dirCurrentToNext.normalized);
        float t = Mathf.Clamp01(cornerAngle / corneringSlowdownAngle);

        return Mathf.Lerp(1f, corneringMinSpeedFactor, t);
    }

    float GetRubberBandedTargetSpeed()
    {
        if (!useRubberBanding || RaceManager.Instance == null)
            return personalTargetSpeed;

        float leaderProgress = RaceManager.Instance.GetLeaderProgress();
        float diff = leaderProgress - progress.GetProgress(); // positif = AI ini ketinggalan

        float t = Mathf.Clamp(diff / rubberBandRange, -1f, 1f);
        float boost = t * rubberBandMaxBoost; // ketinggalan -> boost positif, di depan -> boost negatif

        return personalTargetSpeed + boost;
    }

    private void OnDrawGizmos()
    {
        if (currentWaypoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, currentWaypoint.transform.position);
    }
}

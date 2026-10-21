using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Brief native-camera introduction. Countdown and race time begin only after it returns.</summary>
public sealed class RaceIntroCinematic : MonoBehaviour
{
    [SerializeField] private Camera raceCamera;
    [SerializeField, Min(0.1f)] private float playerShotSeconds = 1.3f;
    [SerializeField, Min(0.1f)] private float opponentsShotSeconds = 2.7f;
    [SerializeField, Min(0.1f)] private float returnSeconds = 0.8f;

    public bool IsPlaying { get; private set; }
    public string CurrentShot { get; private set; } = string.Empty;

    private Transform player;
    private CameraFollow follow;
    private bool followWasEnabled;
    private float originalFov;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 gridCenter;
    private Vector3 currentAim;
    private bool captured;
    private readonly List<BodyState> bodies = new List<BodyState>();
    private readonly List<Behaviour> disabledDrivers = new List<Behaviour>();
    private readonly List<Canvas> hiddenHUDs = new List<Canvas>();
    private readonly RaycastHit[] cameraHits = new RaycastHit[32];

    private struct BodyState
    {
        public Rigidbody Body;
        public bool WasKinematic;
    }

    public IEnumerator PlayIntro()
    {
        if (IsPlaying) yield break;
        if (raceCamera == null) raceCamera = Camera.main;
        var laps = FindAnyObjectByType<LapManager>();
        player = laps != null && laps.PlayerCar != null ? laps.PlayerCar.transform : null;
        if (raceCamera == null || player == null) yield break;

        originalPosition = raceCamera.transform.position;
        originalRotation = raceCamera.transform.rotation;
        originalFov = raceCamera.fieldOfView;
        follow = raceCamera.GetComponent<CameraFollow>();
        followWasEnabled = follow != null && follow.enabled;
        captured = true;
        IsPlaying = true;
        if (follow != null) follow.enabled = false;

        var racers = FindObjectsByType<RaceProgressTracker>();
        Array.Sort(racers, (a, b) => string.CompareOrdinal(a.name, b.name));
        var opponents = new List<Transform>();
        gridCenter = Vector3.zero;
        foreach (var racer in racers)
        {
            gridCenter += GetBounds(new[] { racer.transform }).center;
            if (racer.transform != player) opponents.Add(racer.transform);
            var body = racer.GetComponent<Rigidbody>();
            if (body != null)
            {
                bodies.Add(new BodyState { Body = body, WasKinematic = body.isKinematic });
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }
            foreach (var driver in racer.GetComponents<Behaviour>())
            {
                if (!(driver is FungsiGerak || driver is AIController || driver is CarController) || !driver.enabled) continue;
                disabledDrivers.Add(driver);
                driver.enabled = false;
            }
        }
        gridCenter /= Mathf.Max(1, racers.Length);
        foreach (var hud in FindObjectsByType<Canvas>())
        {
            if (!hud.enabled || hud.renderMode == RenderMode.WorldSpace) continue;
            hud.enabled = false;
            hiddenHUDs.Add(hud);
        }
        try
        {
            yield return ShowShot(player, playerShotSeconds, true);
            float opponentSeconds = opponentsShotSeconds / Mathf.Max(1, opponents.Count);
            foreach (var opponent in opponents)
                yield return ShowShot(opponent, opponentSeconds, false);

            CurrentShot = "Return";
            Vector3 fromPosition = raceCamera.transform.position;
            Quaternion fromRotation = raceCamera.transform.rotation;
            float fromFov = raceCamera.fieldOfView;
            for (float elapsed = 0f; elapsed < returnSeconds; elapsed += Time.unscaledDeltaTime)
            {
                FollowPose(out var position, out var rotation);
                float t = Mathf.SmoothStep(0f, 1f, elapsed / returnSeconds);
                raceCamera.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, t), Quaternion.Slerp(fromRotation, rotation, t));
                raceCamera.fieldOfView = Mathf.Lerp(fromFov, originalFov, t);
                yield return null;
            }
            FollowPose(out var finalPosition, out var finalRotation);
            raceCamera.transform.SetPositionAndRotation(finalPosition, finalRotation);
        }
        finally
        {
            Restore(false);
        }
    }

    private IEnumerator ShowShot(Transform subject, float seconds, bool first)
    {
        // State is for diagnostics only; no captions, bars or other intro UI.
        CurrentShot = subject.name;
        Bounds bounds = GetBounds(new[] { subject });
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        Vector3 right = player.right;
        float lateral = Vector3.Dot(bounds.center - gridCenter, right);
        const float fov = 43f;
        float radius = Mathf.Max(1.5f, bounds.extents.magnitude);
        float aspect = Mathf.Max(0.5f, raceCamera.aspect);
        float halfAngle = Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f) * Mathf.Min(1f, aspect));
        float distance = radius / Mathf.Sin(halfAngle) * 1.15f;
        // Dolly along the middle of the grid rather than jumping across the barriers.
        Vector3 direction = (forward * 0.9f - right * Mathf.Clamp(lateral / 4f, -0.7f, 0.7f) + Vector3.up * 0.5f).normalized;
        Vector3 toAim = bounds.center;
        Vector3 toPosition = ClearCameraPosition(toAim, direction, distance);
        if (first)
        {
            Vector3 initialDirection = (direction + right * 0.13f).normalized;
            Vector3 initialPosition = ClearCameraPosition(toAim, initialDirection, distance * 1.05f);
            raceCamera.transform.SetPositionAndRotation(initialPosition, Quaternion.LookRotation(toAim - initialPosition, Vector3.up));
            raceCamera.fieldOfView = fov;
            currentAim = toAim;
        }
        Vector3 fromPosition = raceCamera.transform.position;
        Vector3 fromAim = currentAim;
        float fromFov = raceCamera.fieldOfView;
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            currentAim = Vector3.Lerp(fromAim, toAim, t);
            Vector3 position = Vector3.Lerp(fromPosition, toPosition, t);
            raceCamera.fieldOfView = Mathf.Lerp(fromFov, fov, t);
            raceCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(currentAim - position, Vector3.up));
            yield return null;
        }
        currentAim = toAim;
        raceCamera.fieldOfView = fov;
        raceCamera.transform.SetPositionAndRotation(toPosition, Quaternion.LookRotation(toAim - toPosition, Vector3.up));
    }

    private Vector3 ClearCameraPosition(Vector3 aim, Vector3 direction, float distance)
    {
        int count = Physics.SphereCastNonAlloc(aim, 0.2f, direction, cameraHits, distance, ~0, QueryTriggerInteraction.Ignore);
        float clear = distance;
        for (int i = 0; i < count; i++)
        {
            if (cameraHits[i].collider.GetComponentInParent<RaceProgressTracker>() != null) continue;
            clear = Mathf.Min(clear, Mathf.Max(2f, cameraHits[i].distance - 0.25f));
        }
        return aim + direction * clear;
    }

    private static Bounds GetBounds(Transform[] subjects)
    {
        Bounds bounds = new Bounds(subjects[0].position + Vector3.up, Vector3.one);
        bool first = true;
        foreach (var subject in subjects)
            foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
        return bounds;
    }

    private void FollowPose(out Vector3 position, out Quaternion rotation)
    {
        if (follow != null && follow.TryGetFollowPose(out position, out rotation)) return;
        position = originalPosition;
        rotation = originalRotation;
    }

    private void Restore(bool interrupted)
    {
        if (!captured) return;
        captured = false;
        if (raceCamera != null)
        {
            raceCamera.fieldOfView = originalFov;
            if (interrupted) raceCamera.transform.SetPositionAndRotation(originalPosition, originalRotation);
        }
        if (follow != null) follow.enabled = followWasEnabled;
        foreach (var snapshot in bodies) if (snapshot.Body != null) snapshot.Body.isKinematic = snapshot.WasKinematic;
        foreach (var driver in disabledDrivers) if (driver != null) driver.enabled = true;
        foreach (var hud in hiddenHUDs) if (hud != null) hud.enabled = true;
        bodies.Clear();
        disabledDrivers.Clear();
        hiddenHUDs.Clear();
        IsPlaying = false;
        CurrentShot = string.Empty;
    }

    private void OnDisable() => Restore(true);
}

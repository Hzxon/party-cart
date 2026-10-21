using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small, bounded, texture-free sparks. Never parented to rotating wheel/fender meshes.</summary>
public sealed class PartyCartDriftEffects : MonoBehaviour
{
    private FungsiGerak driver;
    private ParticleSystem left, right, boost;
    private Vector3 lastPosition;
    private int lastReset;
    private PartyCartPlayerRecovery recovery;
    private static readonly Color Charging = new Color(.38f, .78f, .78f);
    private static readonly Color Ready = new Color(.93f, .95f, .35f);

    private void Start()
    {
        driver = GetComponent<FungsiGerak>();
        recovery = GetComponent<PartyCartPlayerRecovery>();
        var wheels = GetComponentInChildren<PotHeadWheelAnimator>();
        var material = Resources.Load<Material>("PartyCart/DriftSpark");
        if (driver == null || wheels == null || material == null) { enabled = false; return; }
        left = Create("Drift Sparks Left", WheelPoint(wheels.RearLeftWheel), material, false);
        right = Create("Drift Sparks Right", WheelPoint(wheels.RearRightWheel), material, false);
        Vector3 rear = (left.transform.localPosition + right.transform.localPosition) * .5f;
        boost = Create("Mini Boost Flecks", rear + Vector3.up * .18f, material, true);
        lastPosition = transform.position;
        lastReset = recovery != null ? recovery.ResetCount : 0;
    }

    private Vector3 WheelPoint(Transform wheel) => wheel != null
        ? transform.InverseTransformPoint(wheel.position) - Vector3.up * .3f
        : new Vector3(0, .1f, -1);

    private ParticleSystem Create(string label, Vector3 position, Material material, bool boosting)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = position;
        child.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var particles = child.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false; main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = false;
        main.maxParticles = boosting ? 24 : 20;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, boosting ? .22f : .18f);
        main.startSize = new ParticleSystem.MinMaxCurve(.035f, boosting ? .10f : .07f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, boosting ? 4f : 2.5f);
        main.startRotation = Mathf.PI * .25f;
        main.startColor = boosting ? Ready : Charging;
        main.gravityModifier = .1f;
        var emission = particles.emission;
        emission.rateOverTime = boosting ? 28 : 14;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = boosting ? 12 : 25; shape.radius = .04f;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return particles;
    }

    private void LateUpdate()
    {
        if (driver == null || left == null) return;
        int reset = recovery != null ? recovery.ResetCount : lastReset;
        bool teleported = (transform.position - lastPosition).sqrMagnitude > 25 || reset != lastReset;
        lastPosition = transform.position; lastReset = reset;
        if (teleported || !driver.enabled || !RaceCountdownController.RaceStarted)
        {
            Stop(left, true); Stop(right, true); Stop(boost, true); return;
        }
        if (PartyCartGameOverlay.IsGamePaused) return; // Scaled particle simulation freezes with the race.
        bool drifting = driver.Drift.IsDrifting;
        Color color = driver.Drift.Ready ? Ready : Charging;
        Set(left, drifting, color); Set(right, drifting, color);
        Set(boost, driver.Drift.IsBoosting, Ready);
    }

    private static void Set(ParticleSystem particles, bool emitting, Color color)
    {
        var main = particles.main; main.startColor = color;
        if (emitting) { if (!particles.isPlaying) particles.Play(); }
        else Stop(particles, false);
    }

    private static void Stop(ParticleSystem particles, bool clear)
    {
        if (particles != null && (particles.isPlaying || (clear && particles.particleCount > 0)))
            particles.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnDisable() { Stop(left, true); Stop(right, true); Stop(boost, true); }
}

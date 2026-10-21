using System;

/// <summary>Deterministic drift state; no input, transforms, audio, or race progress writes.</summary>
public sealed class PartyCartDriftState
{
    public const float MinimumSpeed = 7f;
    public const float ChargeSeconds = .9f;
    public const float BoostSeconds = .85f;
    public const float SpeedMultiplier = 1.22f;
    public bool IsDrifting { get; private set; }
    public bool IsBoosting => BoostRemaining > 0;
    public bool Ready => IsDrifting && charge >= ChargeSeconds;
    public float Charge01 => Math.Min(1f, charge / ChargeSeconds);
    public float BoostRemaining { get; private set; }
    public float Boost01 => BoostRemaining / BoostSeconds;
    public float Direction { get; private set; }
    private float charge, neutralSeconds;
    private bool requiresRelease;

    public void Step(float throttle, float steering, bool held, float forwardSpeed, bool grounded, bool canControl, float deltaTime)
    {
        if (deltaTime <= 0) return;
        if (!held) requiresRelease = false;
        if (!canControl || !grounded || throttle <= .1f || forwardSpeed < MinimumSpeed * .65f)
        {
            CancelAll();
            requiresRelease = held;
            return;
        }
        BoostRemaining = Math.Max(0, BoostRemaining - deltaTime);
        if (IsBoosting)
        {
            if (held) requiresRelease = true;
            return;
        }
        if (IsDrifting)
        {
            if (!held)
            {
                bool earned = Ready;
                ClearCharge();
                if (earned) BoostRemaining = BoostSeconds;
                return;
            }
            // Counter-steer can widen a slide, but straight-line/alternating steering cannot farm charge.
            bool turningIntoDrift = steering * Direction > .15f;
            neutralSeconds = turningIntoDrift ? 0 : neutralSeconds + deltaTime;
            if (neutralSeconds > .25f) { InterruptCharge(); return; }
            if (turningIntoDrift) charge = Math.Min(ChargeSeconds, charge + deltaTime);
            return;
        }
        if (!held || requiresRelease || forwardSpeed < MinimumSpeed || Math.Abs(steering) < .25f) return;
        IsDrifting = true;
        Direction = Math.Sign(steering);
        charge = Math.Min(ChargeSeconds, deltaTime);
        neutralSeconds = 0;
    }

    public float Steering(float input) => IsDrifting ? Direction * .6f + input * .4f : input;

    public void InterruptCharge()
    {
        ClearCharge();
        requiresRelease = true;
    }

    public void CancelAll() { InterruptCharge(); BoostRemaining = 0; }

    private void ClearCharge()
    {
        IsDrifting = false;
        charge = neutralSeconds = Direction = 0;
    }
}

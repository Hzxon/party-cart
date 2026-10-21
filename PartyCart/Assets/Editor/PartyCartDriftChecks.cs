using System;

/// <summary>Fast deterministic edge cases, called by the bounded Play Mode smoke test.</summary>
public static class PartyCartDriftChecks
{
    public static void Run(Action<string, bool> check)
    {
        var state = new PartyCartDriftState();
        state.Step(1, 1, true, 0, true, true, 1);
        check("Drift cannot charge at rest", !state.IsDrifting);
        state.Step(1, 1, true, 12, true, true, 1);
        check("Holding drift before reaching minimum speed requires a fresh press", !state.IsDrifting);
        Release(state); Charge(state, .3f); Release(state);
        check("Early drift release gives no boost", !state.IsBoosting);
        Charge(state, 1.1f);
        check("Sustained turning charges drift", state.Ready && state.Charge01 == 1);
        check("Drift steering retains the slide direction", state.Steering(-1) > 0);
        Release(state);
        check("Charged release starts one 0.85 second mini boost", state.IsBoosting && Math.Abs(state.BoostRemaining - .85f) < .001);
        state.Step(1, 1, true, 12, true, true, .3f);
        check("Holding Space cannot stack an active boost", Math.Abs(state.BoostRemaining - .55f) < .001);
        for (int i = 0; i < 80; i++) state.Step(1, 1, true, 12, true, true, .02f);
        check("Boost expires and cannot auto-farm while Space is held", !state.IsBoosting && !state.IsDrifting);
        Release(state); Charge(state, 1.1f); state.InterruptCharge(); Release(state);
        check("Pause or input handover cancels charge without free boost", !state.IsBoosting && !state.IsDrifting);
        Charge(state, 1.1f); Release(state);
        float remaining = state.BoostRemaining;
        state.InterruptCharge(); state.Step(1, 1, false, 12, true, true, 0);
        check("Paused boost duration does not advance with zero scaled time", state.IsBoosting && state.BoostRemaining == remaining);
        state.CancelAll();
        check("Recovery, finish, or wall contact can clear drift and boost", !state.IsDrifting && !state.IsBoosting);
        Release(state); Charge(state, .2f);
        state.Step(1, 0, true, 12, true, true, .3f); Release(state);
        check("Prolonged straight-line steering cancels charge", !state.IsDrifting && !state.IsBoosting);
        Release(state); Charge(state, .2f);
        state.Step(1, -1, true, 12, true, true, .3f); Release(state);
        check("Alternating opposite steering cannot farm drift charge", !state.IsBoosting);
        foreach (var blocked in new[] { "airborne", "countdown", "reverse", "braking" })
        {
            state = new PartyCartDriftState();
            state.Step(blocked == "braking" ? 0 : 1, 1, true, blocked == "reverse" ? -12 : 12,
                blocked != "airborne", blocked != "countdown", 1);
            check("Drift disabled while " + blocked, !state.IsDrifting && !state.IsBoosting);
        }
        state = new PartyCartDriftState();
        state.Step(1, 0, true, 12, true, true, 1);
        check("Space on a straight cannot start drift", !state.IsDrifting);
        check("Normal steering is unchanged outside a drift", Math.Abs(state.Steering(-.7f) + .7f) < .001);
    }

    private static void Release(PartyCartDriftState state) => state.Step(1, 1, false, 12, true, true, .02f);
    private static void Charge(PartyCartDriftState state, float seconds)
    {
        for (int i = 0; i < (int)Math.Ceiling(seconds / .02f); i++) state.Step(1, 1, true, 12, true, true, .02f);
    }
}

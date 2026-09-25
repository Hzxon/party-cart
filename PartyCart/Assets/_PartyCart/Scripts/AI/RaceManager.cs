using System.Collections.Generic;
using UnityEngine;

// Taruh script ini di satu GameObject kosong di scene (misal "RaceManager"),
// sebelum player & AI di-Start (biasanya otomatis aman karena Awake duluan).
public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    private readonly List<RaceProgressTracker> racers = new List<RaceProgressTracker>();

    private void Awake()
    {
        Instance = this;
    }

    public void Register(RaceProgressTracker racer)
    {
        if (!racers.Contains(racer)) racers.Add(racer);
    }

    public void Unregister(RaceProgressTracker racer)
    {
        racers.Remove(racer);
    }

    // Progress racer paling depan saat ini. Dipakai AIController buat rubber-banding.
    public float GetLeaderProgress()
    {
        float best = float.MinValue;
        foreach (var r in racers)
        {
            if (r.GetProgress() > best) best = r.GetProgress();
        }
        return best == float.MinValue ? 0f : best;
    }

    // Bonus: ranking buat UI posisi (1st, 2nd, 3rd, ...) kalau nanti dibutuhin
    public List<RaceProgressTracker> GetRanking()
    {
        racers.Sort((a, b) => b.GetProgress().CompareTo(a.GetProgress()));
        return racers;
    }
}

using System.Collections.Generic;
using UnityEngine;

// Taruh script ini di satu GameObject kosong di scene (misal "RaceManager"),
// sebelum player & AI di-Start (biasanya otomatis aman karena Awake duluan).
public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    private readonly List<RaceProgressTracker> racers = new List<RaceProgressTracker>();
    private int nextFinishOrder;
    private readonly List<string> opponentNames = new List<string>
    {
        "John", "Doe", "Amber", "Budi", "Nala", "Dimas", "Aya", "Bella",
        "Raka", "Luna", "Rio", "Maya", "Theo", "Sari", "Niko", "Cleo",
        "Arif", "Zara", "Bayu", "Milo", "Tara", "Rani", "Alex", "Dewi"
    };
    private int nextOpponentName;
    public IReadOnlyList<RaceProgressTracker> Racers => racers;

    public RaceProgressTracker Winner
    {
        get
        {
            var ranking = GetRanking();
            return ranking.Count > 0 && ranking[0].FinishRecorded ? ranking[0] : null;
        }
    }

    private void Awake()
    {
        Instance = this;
        // A separate shuffle avoids changing the random sequence used by AI driving.
        var random = new System.Random();
        for (int i = opponentNames.Count - 1; i > 0; i--)
        {
            int swap = random.Next(i + 1);
            string name = opponentNames[i];
            opponentNames[i] = opponentNames[swap];
            opponentNames[swap] = name;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(RaceProgressTracker racer)
    {
        if (racer == null || racers.Contains(racer)) return;
        if (racer.NeedsRaceName) racer.AssignRaceName(TakeOpponentName());
        racers.Add(racer);
    }

    private string TakeOpponentName()
    {
        string candidate;
        do
        {
            int index = nextOpponentName++;
            candidate = opponentNames[index % opponentNames.Count];
            if (index >= opponentNames.Count) candidate += " " + (index / opponentNames.Count + 1);
        }
        while (racers.Exists(r => r != null && r.DisplayName == candidate));
        return candidate;
    }

    public void Unregister(RaceProgressTracker racer)
    {
        racers.Remove(racer);
    }

    internal int RecordFinishOrder(RaceProgressTracker racer)
    {
        Register(racer);
        return ++nextFinishOrder;
    }

    // Progress racer paling depan saat ini. Dipakai AIController buat rubber-banding.
    public float GetLeaderProgress()
    {
        float best = float.MinValue;
        foreach (var r in racers)
        {
            if (r == null) continue;
            if (r.GetProgress() > best) best = r.GetProgress();
        }
        return best == float.MinValue ? 0f : best;
    }

    // Finish timestamps lock the result; a later finisher can never overtake a winner.
    // Return a copy so callers cannot reorder the registration list.
    public List<RaceProgressTracker> GetRanking()
    {
        var ranking = racers.FindAll(r => r != null);
        ranking.Sort((a, b) =>
        {
            if (a.FinishRecorded != b.FinishRecorded) return a.FinishRecorded ? -1 : 1;
            int comparison = a.FinishRecorded
                ? a.FinishedAt.CompareTo(b.FinishedAt)
                : b.GetProgress().CompareTo(a.GetProgress());
            if (comparison == 0 && a.FinishRecorded)
                comparison = a.FinishOrder.CompareTo(b.FinishOrder);
            return comparison != 0 ? comparison : racers.IndexOf(a).CompareTo(racers.IndexOf(b));
        });
        return ranking;
    }
}

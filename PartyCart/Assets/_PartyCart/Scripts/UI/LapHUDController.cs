using TMPro;
using UnityEngine;

public sealed class LapHUDController : MonoBehaviour
{
    private TMP_Text lapCounter;
    private LapManager lapManager;
    private int displayedLap = -1;
    private int displayedTotal = -1;
    private Material outlinedFontMaterial;

    private void Awake()
    {
        lapCounter = GetComponentInChildren<TMP_Text>(true);
        if (lapCounter != null)
        {
            outlinedFontMaterial = new Material(lapCounter.fontSharedMaterial)
            {
                name = "Lap HUD Bangers Outline"
            };
            outlinedFontMaterial.SetColor("_OutlineColor", Color.black);
            outlinedFontMaterial.SetFloat("_OutlineWidth", 0.28f);
            lapCounter.fontMaterial = outlinedFontMaterial;
            lapCounter.extraPadding = true;
        }
    }

    private void Update()
    {
        if (lapCounter == null)
            return;

        if (lapManager == null)
            lapManager = FindAnyObjectByType<LapManager>();

        if (lapManager == null)
            return;

        int currentLap = lapManager.CurrentLap;
        int totalLaps = lapManager.TotalLaps;
        if (currentLap == displayedLap && totalLaps == displayedTotal)
            return;

        lapCounter.text = "<size=46%>LAP</size> <size=130%>" + currentLap +
                          "</size><size=76%>/" + totalLaps + "</size>";
        displayedLap = currentLap;
        displayedTotal = totalLaps;
    }

    private void OnDestroy()
    {
        if (outlinedFontMaterial != null)
            Destroy(outlinedFontMaterial);
    }
}

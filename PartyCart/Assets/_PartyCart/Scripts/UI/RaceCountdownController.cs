using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(RaceIntroCinematic))]
public sealed class RaceCountdownController : MonoBehaviour
{
    public static bool RaceStarted { get; private set; } = true;
    public static double StartedAt { get; private set; } = double.NaN;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRaceState()
    {
        RaceStarted = true;
        StartedAt = double.NaN;
    }
    [SerializeField] private float numberDuration = 1f;
    [SerializeField] private float goDuration = 0.8f;

    private TMP_Text countdownText;
    private FungsiGerak playerController;
    private Material outlinedFontMaterial;

    private void Awake()
    {
        RaceStarted = false;
        StartedAt = double.NaN;
        countdownText = GetComponentInChildren<TMP_Text>(true);
        playerController = FindAnyObjectByType<FungsiGerak>();

        if (playerController != null)
            playerController.enabled = false;

        if (countdownText != null)
        {
            countdownText.text = string.Empty;
            outlinedFontMaterial = RaceHudTypography.Apply(countdownText);
        }
    }

    private IEnumerator Start()
    {
        var intro = GetComponent<RaceIntroCinematic>();
        if (intro != null) yield return intro.PlayIntro();

        yield return new WaitForSecondsRealtime(1f);

        if (countdownText == null)
        {
            StartRace();
            yield break;
        }

        for (int number = 3; number >= 1; number--)
        {
            countdownText.color = RaceHudTypography.Yellow;
            countdownText.text = number.ToString();
            yield return new WaitForSecondsRealtime(numberDuration);
        }

        StartRace();

        countdownText.color = RaceHudTypography.Yellow;
        countdownText.text = "GO!";
        yield return new WaitForSecondsRealtime(goDuration);
        countdownText.text = string.Empty;
    }

    private void StartRace()
    {
        StartedAt = Time.timeAsDouble;
        RaceStarted = true;
        if (playerController != null)
            playerController.enabled = true;
    }

    private void OnDestroy()
    {
        RaceStarted = true;
        StartedAt = double.NaN;
        if (outlinedFontMaterial != null)
            Destroy(outlinedFontMaterial);
    }
}

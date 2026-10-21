using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Portrait-only standings, sharing the existing race canvas and ranking source.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class RaceStandingsHUD : MonoBehaviour
{
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private LapManager lapManager;
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private Texture2D defaultPortrait;
    [SerializeField] private Texture2D cleanRacerPortrait;
    [SerializeField] private Texture2D pitCrewPortrait;

    private static readonly Color Ink = new Color32(21, 63, 71, 255);
    private static readonly Color Paper = new Color32(246, 243, 231, 255);
    private static readonly Color Yellow = new Color32(233, 240, 89, 255);
    private const int MaxRows = 6;
    private const float PortraitSize = 92f;
    private const float RowStep = 110f;
    private readonly Dictionary<RaceProgressTracker, Entry> entries = new Dictionary<RaceProgressTracker, Entry>();
    private readonly List<RaceProgressTracker> displayedOrder = new List<RaceProgressTracker>();
    private RectTransform standings;
    private RectTransform positionCard;
    private TextMeshProUGUI positionNumber;
    private RaceProgressTracker player;
    private Material titleMaterial;
    private float nextRefresh;

    public int PlayerPosition { get; private set; }
    public int RacerCount { get; private set; }
    public IReadOnlyList<RaceProgressTracker> DisplayedOrder => displayedOrder;

    private sealed class Entry
    {
        public RectTransform Root;
        public GameObject PlayerOutline;
        public float TargetY;
        public bool Placed;
    }

    private void Awake()
    {
        if (raceManager == null) raceManager = FindAnyObjectByType<RaceManager>();
        if (lapManager == null) lapManager = FindAnyObjectByType<LapManager>();
        if (titleFont == null)
        {
            Debug.LogError("Race standings require the race display font.", this);
            enabled = false;
            return;
        }
        standings = Rect("Live Standings", transform, new Vector2(PortraitSize, 4 * RowStep - 18),
            new Vector2(0, 0.59f), new Vector2(0, 0.5f), new Vector2(42, 0));
        positionCard = Rect("Player Position", transform, new Vector2(290, 224),
            Vector2.zero, Vector2.zero, new Vector2(34, 30));
        var rankRect = Rect("Player Rank", positionCard, positionCard.sizeDelta,
            Vector2.zero, Vector2.zero, Vector2.zero);
        positionNumber = rankRect.gameObject.AddComponent<TextMeshProUGUI>();
        positionNumber.font = titleFont;
        positionNumber.fontSize = 210;
        positionNumber.alignment = TextAlignmentOptions.BottomLeft;
        positionNumber.textWrappingMode = TextWrappingModes.NoWrap;
        positionNumber.overflowMode = TextOverflowModes.Overflow;
        positionNumber.raycastTarget = false;
        positionNumber.richText = true;
        titleMaterial = RaceHudTypography.Apply(positionNumber);
        standings.gameObject.SetActive(false);
        positionCard.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (standings == null) return;
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.1f;
            RefreshStandings();
        }
        float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        foreach (var entry in entries.Values)
        {
            if (!entry.Root.gameObject.activeSelf) continue;
            Vector2 position = entry.Root.anchoredPosition;
            position.y = Mathf.Lerp(position.y, entry.TargetY, blend);
            entry.Root.anchoredPosition = position;
        }
    }

    public void RefreshStandings()
    {
        if (standings == null) return;
        if (raceManager == null) raceManager = FindAnyObjectByType<RaceManager>();
        if (lapManager == null) lapManager = FindAnyObjectByType<LapManager>();
        if (player == null && lapManager != null && lapManager.PlayerCar != null)
            player = lapManager.PlayerCar.GetComponent<RaceProgressTracker>();
        var ranking = raceManager != null ? raceManager.GetRanking() : null;
        PlayerPosition = ranking != null && player != null ? ranking.IndexOf(player) + 1 : 0;
        RacerCount = ranking != null ? ranking.Count : 0;
        bool ready = PlayerPosition > 0;
        standings.gameObject.SetActive(ready);
        positionCard.gameObject.SetActive(ready);
        if (!ready) return;

        // Only update text on an actual rank change, not every standings refresh.
        if (displayedPosition != PlayerPosition)
        {
            displayedPosition = PlayerPosition;
            positionNumber.text = PlayerPosition + "<voffset=0.65em><size=42%><color=#F6F3E7>" +
                OrdinalSuffix(PlayerPosition) + "</color></size></voffset>";
        }
        foreach (var entry in entries.Values) entry.Root.gameObject.SetActive(false);
        displayedOrder.Clear();
        int count = Mathf.Min(MaxRows, RacerCount);
        standings.sizeDelta = new Vector2(PortraitSize, PortraitSize + (count - 1) * RowStep);
        // Keep the player in view even when a future race has more than six competitors.
        int start = Mathf.Clamp(PlayerPosition - MaxRows, 0, Mathf.Max(0, RacerCount - MaxRows));
        for (int slot = 0; slot < count; slot++)
        {
            int rankIndex = start + slot;
            var racer = ranking[rankIndex];
            if (!entries.TryGetValue(racer, out var entry))
            {
                entry = CreateEntry(racer);
                entries.Add(racer, entry);
            }
            bool isPlayer = racer == player;
            entry.Root.gameObject.SetActive(true);
            entry.PlayerOutline.SetActive(isPlayer);
            entry.TargetY = -slot * RowStep;
            if (!entry.Placed)
            {
                entry.Root.anchoredPosition = new Vector2(0, entry.TargetY);
                entry.Placed = true;
            }
            entry.Root.SetAsLastSibling();
            displayedOrder.Add(racer);
        }
        // The outlined player portrait stays legible while portraits slide past one another.
        if (entries.TryGetValue(player, out var playerEntry)) playerEntry.Root.SetAsLastSibling();
    }

    private Entry CreateEntry(RaceProgressTracker racer)
    {
        var entry = new Entry();
        entry.Root = Rect(racer.name + " Standing", standings, Vector2.one * PortraitSize,
            Vector2.up, Vector2.up, Vector2.zero);
        var outline = Rect("Player Outline", entry.Root, new Vector2(108, 108),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        Fill(outline, Ink);
        var rim = Rect("Yellow Player Rim", outline, new Vector2(102, 102),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        Fill(rim, Yellow);
        entry.PlayerOutline = outline.gameObject;
        var frame = Rect("Portrait Frame", entry.Root, Vector2.one * PortraitSize,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var border = Fill(frame, Ink);
        var shadow = border.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.3f);
        shadow.effectDistance = new Vector2(3, -4);
        var portrait = Rect("Driver Portrait", frame, new Vector2(84, 84),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        Fill(portrait, Paper);
        var photoRect = Rect("Photo", portrait, portrait.sizeDelta,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var photo = photoRect.gameObject.AddComponent<RawImage>();
        photo.texture = PortraitFor(racer);
        photo.color = Color.white;
        photo.raycastTarget = false;
        // Crop a non-square source symmetrically rather than stretching the driver's head.
        if (photo.texture != null)
        {
            float aspect = (float)photo.texture.width / photo.texture.height;
            photo.uvRect = aspect > 1f
                ? new Rect((1f - 1f / aspect) * .5f, 0, 1f / aspect, 1)
                : new Rect(0, (1f - aspect) * .5f, 1, aspect);
        }
        return entry;
    }

    private Texture2D PortraitFor(RaceProgressTracker racer)
    {
        if (racer != player)
        {
            foreach (var renderer in racer.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (material.name.StartsWith("AI Kart 01", System.StringComparison.Ordinal) && cleanRacerPortrait != null)
                        return cleanRacerPortrait;
                    if ((material.name.StartsWith("AI Kart 02", System.StringComparison.Ordinal) ||
                         material.name.StartsWith("AI Kart 03", System.StringComparison.Ordinal)) && pitCrewPortrait != null)
                        return pitCrewPortrait;
                }
            return defaultPortrait;
        }
        var catalog = Resources.Load<PartyCartRaceCatalog>("PartyCart/RaceCatalog");
        if (catalog != null)
            foreach (var character in catalog.characters)
                if (character != null && character.id == PartyCartRaceChoice.CharacterId && character.portrait != null)
                    return character.portrait;
        return defaultPortrait;
    }

    private int displayedPosition = -1;

    internal static string OrdinalSuffix(int position)
    {
        int lastTwo = position % 100;
        if (lastTwo >= 11 && lastTwo <= 13) return "th";
        switch (position % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image Fill(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void OnDestroy()
    {
        if (standings != null) Destroy(standings.gameObject);
        if (positionCard != null) Destroy(positionCard.gameObject);
        if (titleMaterial != null) Destroy(titleMaterial);
    }
}

/// <summary>Shared heavy, slanted race typography for placement and 3-2-1-GO.</summary>
internal static class RaceHudTypography
{
    internal static readonly Color Yellow = new Color32(233, 240, 89, 255);

    // Returns an owned instance; callers destroy it without modifying the font asset.
    internal static Material Apply(TMP_Text text)
    {
        var material = new Material(text.font.material) { name = "Race Display - " + text.font.name };
        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_FaceColor", Color.white);
        material.SetColor("_OutlineColor", new Color32(21, 63, 71, 255));
        material.SetFloat("_OutlineWidth", 0.24f);
        material.SetFloat("_OutlineSoftness", 0f);
        text.fontSharedMaterial = material;
        text.fontStyle = FontStyles.Italic;
        text.color = Yellow;
        text.extraPadding = true;
        text.UpdateMeshPadding();
        return material;
    }
}

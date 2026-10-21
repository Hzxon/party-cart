using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum PartyCartSetupStage { Map, Character }

/// <summary>Native UI Toolkit view for choosing real race content before loading a scene.</summary>
public sealed class PartyCartRaceSetupMenu : IDisposable
{
    private readonly VisualElement host;
    private readonly VisualElement screen;
    private readonly PartyCartMapOption[] maps;
    private readonly PartyCartCharacterOption[] characters;
    private readonly Action returnToMenu;
    private readonly Action<PartyCartMapOption, PartyCartCharacterOption> startRace;
    private readonly List<Button> visibleChoices = new List<Button>();
    private readonly Button continueButton;
    private readonly Button backButton;
    private readonly Button previousPage;
    private readonly Button nextPage;
    private readonly Button seeAll;
    private readonly Button closeOverlay;
    private readonly VisualElement choices;
    private readonly VisualElement allChoices;
    private readonly VisualElement overlay;
    private readonly ScrollView allScroll;
    private readonly Image preview;
    private readonly Image portrait;
    private readonly Label status;
    private readonly RoutePreview route;
    private int mapIndex;
    private int characterIndex;
    private int page;
    private bool loading;
    private bool disposed;

    public PartyCartSetupStage Stage { get; private set; }
    public bool IsVisible => host.style.display.value != DisplayStyle.None;
    public bool IsOverlayVisible => overlay.style.display.value != DisplayStyle.None;
    public int MapCount => maps.Length;
    public int CharacterCount => characters.Length;
    public PartyCartMapOption SelectedMap => maps.Length > 0 ? maps[mapIndex] : null;
    public PartyCartCharacterOption SelectedCharacter => characters.Length > 0 ? characters[characterIndex] : null;

    private int Count => Stage == PartyCartSetupStage.Map ? maps.Length : characters.Length;
    private int SelectedIndex => Stage == PartyCartSetupStage.Map ? mapIndex : characterIndex;
    private int PerPage => Stage == PartyCartSetupStage.Map ? 3 : 6;
    private int PageCount => Mathf.Max(1, Mathf.CeilToInt(Count / (float)PerPage));

    public PartyCartRaceSetupMenu(VisualElement parent, VisualTreeAsset template, PartyCartRaceCatalog catalog,
        Action onReturnToMenu, Action<PartyCartMapOption, PartyCartCharacterOption> onStartRace)
    {
        if (parent == null || template == null || catalog == null) throw new ArgumentNullException("Race setup assets are missing.");
        maps = Array.FindAll(catalog.maps ?? Array.Empty<PartyCartMapOption>(),
            item => item != null && !string.IsNullOrWhiteSpace(item.id) && !string.IsNullOrWhiteSpace(item.sceneName));
        characters = Array.FindAll(catalog.characters ?? Array.Empty<PartyCartCharacterOption>(),
            item => item != null && !string.IsNullOrWhiteSpace(item.id));
        returnToMenu = onReturnToMenu;
        startRace = onStartRace;
        mapIndex = Mathf.Max(0, Array.FindIndex(maps, item => item.id == PartyCartRaceChoice.MapId));
        characterIndex = Mathf.Max(0, Array.FindIndex(characters, item => item.id == PartyCartRaceChoice.CharacterId));

        host = new VisualElement { name = "race-setup-host", pickingMode = PickingMode.Ignore };
        host.style.position = Position.Absolute;
        host.style.left = host.style.right = host.style.top = host.style.bottom = 0;
        host.style.display = DisplayStyle.None;
        template.CloneTree(host);
        parent.Add(host);
        screen = Find<VisualElement>("race-selection");
        continueButton = Find<Button>("setup-continue");
        backButton = Find<Button>("setup-back");
        previousPage = Find<Button>("setup-previous-page");
        nextPage = Find<Button>("setup-next-page");
        seeAll = Find<Button>("setup-see-all");
        closeOverlay = Find<Button>("setup-close-overlay");
        choices = Find<VisualElement>("setup-choices");
        allChoices = Find<VisualElement>("setup-all-choices");
        overlay = Find<VisualElement>("setup-all-overlay");
        allScroll = Find<ScrollView>("setup-all-scroll");
        preview = Find<Image>("setup-preview");
        portrait = Find<Image>("setup-portrait");
        status = Find<Label>("setup-status");
        route = new RoutePreview();
        Find<VisualElement>("setup-route-host").Add(route);
        continueButton.clicked += Continue;
        backButton.clicked += Back;
        previousPage.clicked += PreviousPage;
        nextPage.clicked += NextPage;
        seeAll.clicked += ShowAll;
        closeOverlay.clicked += CloseAll;
        screen.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        screen.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);
        screen.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        overlay.RegisterCallback<NavigationMoveEvent>(OnOverlayNavigation);
        HideElement(overlay);
    }

    public void Show()
    {
        if (disposed) return;
        loading = false;
        screen.SetEnabled(true);
        host.style.display = DisplayStyle.Flex;
        screen.style.display = DisplayStyle.Flex;
        SetStage(PartyCartSetupStage.Map);
    }

    public void Hide()
    {
        CloseAll(false);
        host.style.display = DisplayStyle.None;
    }

    public void SetLoading(bool value)
    {
        loading = value;
        screen.SetEnabled(!value);
        SetStatus(value ? "MEMUAT BALAPAN..." : string.Empty);
    }

    public void SetStatus(string message)
    {
        status.text = message;
        SetVisible(status, !string.IsNullOrEmpty(message));
    }

    private T Find<T>(string name) where T : VisualElement
    {
        return host.Q<T>(name) ?? throw new InvalidOperationException("RaceSelection UXML is missing " + name);
    }

    private void SetStage(PartyCartSetupStage value)
    {
        Stage = value;
        page = SelectedIndex / PerPage;
        CloseAll(false);
        SetStatus(string.Empty);
        Render();
        continueButton.Focus();
    }

    private void Render()
    {
        bool characterStage = Stage == PartyCartSetupStage.Character;
        screen.EnableInClassList("is-character", characterStage);
        screen.EnableInClassList("single-page", Count <= PerPage);
        Find<Label>("setup-title").text = characterStage ? "PILIH\nKARAKTER." : "PILIH MAP.";
        Find<Label>("setup-ticket-label").text = characterStage ? "● KARTU PESERTA" : "● TIKET LINTASAN";
        Find<Label>("setup-counter").text = (Count > 0 ? SelectedIndex + 1 : 0).ToString("00") + " / " + Count.ToString("00");
        SetVisible(Find<Label>("setup-selected-label"), !characterStage);
        Find<Label>("setup-selected-name").text = DisplayName(characterStage ? SelectedCharacter?.displayName : SelectedMap?.displayName);
        SetVisible(portrait, characterStage);
        portrait.image = SelectedCharacter?.portrait ?? SelectedCharacter?.preview;
        portrait.scaleMode = ScaleMode.ScaleToFit;
        preview.image = characterStage ? SelectedCharacter?.preview : SelectedMap?.preview;
        preview.scaleMode = characterStage ? ScaleMode.ScaleToFit : ScaleMode.ScaleAndCrop;
        SetVisible(Find<VisualElement>("setup-map-confirmation"), characterStage);
        Find<Label>("setup-confirmed-map").text = DisplayName(SelectedMap?.displayName) + "  ✓";
        SetVisible(Find<VisualElement>("setup-character-heading"), characterStage);
        SetVisible(Find<Label>("setup-preview-tag"), !characterStage);
        SetVisible(Find<VisualElement>("setup-map-nameplate"), !characterStage);
        Find<Label>("setup-character-id").text = "PESERTA " + (characterIndex + 1).ToString("00") + " / PARTY CART";
        Find<Label>("setup-character-name").text = DisplayName(SelectedCharacter?.displayName);
        Find<Label>("setup-map-name").text = DisplayName(SelectedMap?.displayName);
        Find<Label>("setup-map-id").text = "MAP " + (mapIndex + 1).ToString("00");
        route.SetRoute(SelectedMap?.route);
        SetVisible(Find<VisualElement>("setup-route-host"), SelectedMap?.route != null && SelectedMap.route.Length > 2);
        Find<Label>("setup-list-title").text = characterStage ? "DAFTAR KARAKTER" : "DAFTAR MAP";
        continueButton.text = characterStage ? "MULAI BALAP  ↗" : "LANJUT  ↗";
        backButton.text = characterStage ? "←  KEMBALI KE MAP" : "←  KEMBALI KE MENU";
        continueButton.SetEnabled(Count > 0 && SelectedMap != null && (!characterStage || SelectedCharacter != null));
        SetVisible(seeAll, Count > PerPage);
        SetVisible(previousPage, Count > PerPage);
        SetVisible(nextPage, Count > PerPage);
        BuildPage();
        if (Count == 0) SetStatus(characterStage ? "Karakter belum tersedia." : "Map belum tersedia.");
    }

    private void BuildPage()
    {
        page = Mathf.Clamp(page, 0, PageCount - 1);
        choices.Clear();
        visibleChoices.Clear();
        int first = page * PerPage;
        int end = Mathf.Min(Count, first + PerPage);
        Find<Label>("setup-page-range").text = Count == 0 ? "00 / 00" : Count == 1 ? "01 / 01" :
            (first + 1).ToString("00") + "–" + end.ToString("00") + " / " + Count.ToString("00");
        for (int i = first; i < end; i++)
        {
            var button = CreateChoice(i, false);
            choices.Add(button);
            visibleChoices.Add(button);
        }
        previousPage.SetEnabled(page > 0);
        nextPage.SetEnabled(page < PageCount - 1);
    }

    private Button CreateChoice(int index, bool inOverlay)
    {
        var button = new Button(() => Select(index)) { name = "setup-choice-" + index };
        button.AddToClassList("setup-choice");
        button.style.width = Length.Percent(inOverlay ? (Stage == PartyCartSetupStage.Map ? 31f : 23f) : 100f / PerPage - 1.5f);
        button.EnableInClassList("is-selected", index == SelectedIndex);
        bool characterStage = Stage == PartyCartSetupStage.Character;
        var image = new Image
        {
            image = characterStage ? characters[index].portrait ?? characters[index].preview : maps[index].preview,
            scaleMode = characterStage ? ScaleMode.ScaleToFit : ScaleMode.ScaleAndCrop,
            pickingMode = PickingMode.Ignore
        };
        image.AddToClassList("setup-choice-image");
        button.Add(image);
        var name = new Label(DisplayName(characterStage ? characters[index].displayName : maps[index].displayName))
        { pickingMode = PickingMode.Ignore };
        name.AddToClassList("setup-choice-label");
        name.AddToClassList("pit-title");
        button.Add(name);
        if (index == SelectedIndex)
        {
            var check = new Label("✓") { pickingMode = PickingMode.Ignore };
            check.AddToClassList("setup-choice-check");
            button.Add(check);
        }
        button.tooltip = name.text + (index == SelectedIndex ? " — Terpilih" : string.Empty);
        return button;
    }

    private void Select(int index)
    {
        if (loading || index < 0 || index >= Count) return;
        if (Stage == PartyCartSetupStage.Map) mapIndex = index;
        else characterIndex = index;
        page = index / PerPage;
        CloseAll(false);
        SetStatus(string.Empty);
        Render();
        if (visibleChoices.Count > index % PerPage) visibleChoices[index % PerPage].Focus();
    }

    private void Continue()
    {
        if (loading || SelectedMap == null) return;
        if (Stage == PartyCartSetupStage.Map) SetStage(PartyCartSetupStage.Character);
        else if (SelectedCharacter != null) startRace?.Invoke(SelectedMap, SelectedCharacter);
    }

    private void Back()
    {
        if (loading) return;
        if (IsOverlayVisible) CloseAll();
        else if (Stage == PartyCartSetupStage.Character) SetStage(PartyCartSetupStage.Map);
        else returnToMenu?.Invoke();
    }

    private void PreviousPage() { ChangePage(-1); }
    private void NextPage() { ChangePage(1); }
    private void ChangePage(int delta)
    {
        if (loading) return;
        page = Mathf.Clamp(page + delta, 0, PageCount - 1);
        BuildPage();
    }

    private void ShowAll()
    {
        if (loading || Count == 0) return;
        Find<Label>("setup-overlay-title").text = Stage == PartyCartSetupStage.Map ? "SEMUA MAP" : "SEMUA KARAKTER";
        allChoices.Clear();
        for (int i = 0; i < Count; i++) allChoices.Add(CreateChoice(i, true));
        allScroll.scrollOffset = Vector2.zero;
        overlay.style.display = DisplayStyle.Flex;
        closeOverlay.Focus();
    }

    private void CloseAll() { CloseAll(true); }
    private void CloseAll(bool restoreFocus)
    {
        HideElement(overlay);
        if (restoreFocus && IsVisible) seeAll.Focus();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (!IsVisible || loading) return;
        if (evt.keyCode == KeyCode.Escape)
        {
            Back(); evt.StopPropagation();
        }
        else if (IsOverlayVisible && evt.keyCode == KeyCode.Tab)
        {
            var buttons = overlay.Query<Button>().ToList();
            int current = buttons.FindIndex(button => button == screen.focusController.focusedElement);
            if (buttons.Count > 0)
            {
                buttons[(current + (evt.shiftKey ? buttons.Count - 1 : 1) + buttons.Count) % buttons.Count].Focus();
                screen.focusController.IgnoreEvent(evt);
                evt.StopPropagation();
            }
        }
        else if (!IsOverlayVisible && (evt.keyCode == KeyCode.PageDown || evt.keyCode == KeyCode.PageUp))
        {
            ChangePage(evt.keyCode == KeyCode.PageDown ? 1 : -1); evt.StopPropagation();
        }
    }

    private void OnNavigationCancel(NavigationCancelEvent evt)
    {
        if (IsVisible && !loading) { Back(); evt.StopPropagation(); }
    }

    private void OnOverlayNavigation(NavigationMoveEvent evt)
    {
        // Keep controller navigation inside the catalog overlay.
        if (!IsOverlayVisible) return;
        var focused = screen.focusController.focusedElement as VisualElement;
        if (focused != null && !overlay.Contains(focused)) closeOverlay.Focus();
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        screen.EnableInClassList("compact", evt.newRect.width < 1550f || evt.newRect.height < 900f);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        continueButton.clicked -= Continue;
        backButton.clicked -= Back;
        previousPage.clicked -= PreviousPage;
        nextPage.clicked -= NextPage;
        seeAll.clicked -= ShowAll;
        closeOverlay.clicked -= CloseAll;
        screen.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        screen.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
        screen.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        overlay.UnregisterCallback<NavigationMoveEvent>(OnOverlayNavigation);
        host.RemoveFromHierarchy();
    }

    private static string DisplayName(string value) => string.IsNullOrWhiteSpace(value) ? "BELUM TERSEDIA" : value.ToUpperInvariant();
    private static void SetVisible(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    private static void HideElement(VisualElement element) => SetVisible(element, false);

    private sealed class RoutePreview : VisualElement
    {
        private Vector2[] points;
        public RoutePreview()
        {
            AddToClassList("setup-route-drawing");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        public void SetRoute(Vector2[] value) { points = value; MarkDirtyRepaint(); }

        private void Draw(MeshGenerationContext context)
        {
            if (points == null || points.Length < 3 || contentRect.width < 5f || contentRect.height < 5f) return;
            Vector2 min = points[0], max = points[0];
            foreach (var point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            Vector2 extent = max - min;
            float scale = Mathf.Min((contentRect.width - 14f) / Mathf.Max(1f, extent.x), (contentRect.height - 14f) / Mathf.Max(1f, extent.y));
            Vector2 offset = (contentRect.size - extent * scale) * 0.5f;
            var painter = context.painter2D;
            painter.lineWidth = 4f;
            painter.strokeColor = new Color32(21, 63, 71, 255);
            painter.BeginPath();
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = offset + new Vector2(points[i].x - min.x, max.y - points[i].y) * scale;
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.ClosePath(); painter.Stroke();
            Vector2 start = offset + new Vector2(points[0].x - min.x, max.y - points[0].y) * scale;
            painter.fillColor = new Color32(233, 240, 89, 255);
            painter.BeginPath();
            painter.MoveTo(start + new Vector2(-4, -4)); painter.LineTo(start + new Vector2(4, -4));
            painter.LineTo(start + new Vector2(4, 4)); painter.LineTo(start + new Vector2(-4, 4));
            painter.ClosePath(); painter.Fill();
        }
    }
}

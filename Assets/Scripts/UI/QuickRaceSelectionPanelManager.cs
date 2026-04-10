using System.Collections;
using System.Collections.Generic;
using TMPro;
using Underground.Race;
using Underground.Save;
using Underground.Session;
using Underground.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Underground.UI
{
    public class QuickRaceSelectionPanelManager : MonoBehaviour
    {
        private enum SelectionStep
        {
            Type,
            Map,
            Car,
            Transmission
        }

        private static readonly RaceType[] SupportedTypes =
        {
            RaceType.Sprint,
            RaceType.Circuit,
            RaceType.TimeTrial,
            RaceType.Underground,
            RaceType.Wager,
            RaceType.Drift,
            RaceType.Drag
        };

        [SerializeField] private MainMenuFlowManager flowManager;
        [SerializeField] private MainMenuController mainMenuController;
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private float fadeDuration = 0.2f;

        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private GameObject typePanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject carPanel;
        [SerializeField] private GameObject transmissionPanel;
        [SerializeField] private TMP_Text typeSummaryText;
        [SerializeField] private TMP_Text mapSummaryText;
        [SerializeField] private TMP_Text carSummaryText;
        [SerializeField] private TMP_Text transmissionSummaryText;
        [SerializeField] private Transform typeButtonsRoot;
        [SerializeField] private Transform mapButtonsRoot;
        [SerializeField] private Transform carButtonsRoot;
        [SerializeField] private Transform transmissionButtonsRoot;
        private Coroutine transitionRoutine;
        private SelectionStep currentStep;
        private RaceType selectedRaceType;
        private QuickRaceTrackEntry selectedTrack;
        private PlayerCarDefinition selectedCar;
        private readonly List<Button> currentButtons = new List<Button>();

        public bool IsOpen => overlayRoot != null && overlayRoot.activeSelf;
        public Selectable DefaultSelectable => GetFirstInteractableButton();

        private void Awake()
        {
            flowManager ??= GetComponent<MainMenuFlowManager>();
            mainMenuController ??= FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            progressManager ??= FindFirstObjectByType<PersistentProgressManager>(FindObjectsInactive.Include);
            EnsurePanelsBuilt();
        }

        public void EnsurePanelsBuilt()
        {
            if (overlayRoot != null || TryBindExistingPanels())
            {
                return;
            }

            overlayRoot = CreateObject("QuickRaceSelectionRoot", transform, typeof(RectTransform), typeof(CanvasGroup));
            Stretch(overlayRoot.GetComponent<RectTransform>());
            overlayRoot.SetActive(false);

            typePanel = CreateStepPanel("QuickRace_TypePanel", "Select Type", "Choose a race style based on content unlocked in MyCareer.", out typeSummaryText, out typeButtonsRoot);
            mapPanel = CreateStepPanel("QuickRace_MapPanel", "Select Track", "Only races finished in MyCareer are available here.", out mapSummaryText, out mapButtonsRoot);
            carPanel = CreateStepPanel("QuickRace_CarPanel", "Select Car", "Use one of your owned MyCareer cars.", out carSummaryText, out carButtonsRoot);
            transmissionPanel = CreateStepPanel("QuickRace_TransmissionPanel", "Transmission", "Pick how the event should be driven.", out transmissionSummaryText, out transmissionButtonsRoot);

            typePanel.transform.SetParent(overlayRoot.transform, false);
            mapPanel.transform.SetParent(overlayRoot.transform, false);
            carPanel.transform.SetParent(overlayRoot.transform, false);
            transmissionPanel.transform.SetParent(overlayRoot.transform, false);
        }

        public void OpenSelection()
        {
            EnsurePanelsBuilt();
            progressManager ??= FindFirstObjectByType<PersistentProgressManager>(FindObjectsInactive.Include);

            selectedTrack = default;
            selectedCar = ResolveDefaultCar();
            selectedRaceType = ResolveDefaultType();

            overlayRoot.SetActive(true);
            OpenStep(SelectionStep.Type, true);
        }

        public void StepBackOrClose()
        {
            if (!IsOpen)
            {
                return;
            }

            switch (currentStep)
            {
                case SelectionStep.Type:
                    CloseSelection();
                    break;
                case SelectionStep.Map:
                    OpenStep(SelectionStep.Type);
                    break;
                case SelectionStep.Car:
                    OpenStep(SelectionStep.Map);
                    break;
                case SelectionStep.Transmission:
                    OpenStep(SelectionStep.Car);
                    break;
            }
        }

        public void CloseSelection()
        {
            if (overlayRoot == null)
            {
                return;
            }

            overlayRoot.SetActive(false);
            currentButtons.Clear();

            if (EventSystem.current == null)
            {
                return;
            }

            Selectable fallback = flowManager != null ? flowManager.GetDefaultSelectableForState(MenuState.QuickRace) : null;
            if (fallback != null)
            {
                EventSystem.current.SetSelectedGameObject(fallback.gameObject);
            }
        }

        private void OpenStep(SelectionStep step, bool immediate = false)
        {
            currentStep = step;
            switch (step)
            {
                case SelectionStep.Type:
                    PopulateTypeStep();
                    break;
                case SelectionStep.Map:
                    PopulateMapStep();
                    break;
                case SelectionStep.Car:
                    PopulateCarStep();
                    break;
                case SelectionStep.Transmission:
                    PopulateTransmissionStep();
                    break;
            }

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(TransitionToPanel(GetPanelForStep(step), immediate));
        }

        private IEnumerator TransitionToPanel(GameObject targetPanel, bool immediate)
        {
            GameObject[] panels =
            {
                typePanel,
                mapPanel,
                carPanel,
                transmissionPanel
            };

            float duration = immediate ? 0f : fadeDuration;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                panels[i].SetActive(true);
                CanvasGroup group = panels[i].GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] == null)
                    {
                        continue;
                    }

                    CanvasGroup group = panels[i].GetComponent<CanvasGroup>();
                    float targetAlpha = panels[i] == targetPanel ? 1f : 0f;
                    group.alpha = Mathf.Lerp(group.alpha, targetAlpha, t);
                }

                yield return null;
            }

            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                bool isTarget = panels[i] == targetPanel;
                CanvasGroup group = panels[i].GetComponent<CanvasGroup>();
                group.alpha = isTarget ? 1f : 0f;
                group.interactable = isTarget;
                group.blocksRaycasts = isTarget;
                panels[i].SetActive(isTarget);
            }

            yield return null;
            SelectDefaultButton();
            transitionRoutine = null;
        }

        private void PopulateTypeStep()
        {
            ClearButtons(typeButtonsRoot);
            currentButtons.Clear();

            int unlockedCount = CountUnlockedTracks();
            typeSummaryText.text = unlockedCount > 0
                ? $"Unlocked tracks: {unlockedCount}"
                : "No finished MyCareer races yet.";

            for (int i = 0; i < SupportedTypes.Length; i++)
            {
                RaceType raceType = SupportedTypes[i];
                int availableTracks = CountUnlockedTracksForType(raceType);
                string label = $"{FormatRaceType(raceType)} ({availableTracks})";
                Button button = availableTracks > 0
                    ? CreateActionButton(typeButtonsRoot, label, () =>
                    {
                        selectedRaceType = raceType;
                        OpenStep(SelectionStep.Map);
                    })
                    : CreateActionButton(typeButtonsRoot, label, null, false);
                if (availableTracks == 0)
                {
                    button.GetComponentInChildren<TMP_Text>(true).color = new Color(1f, 1f, 1f, 0.72f);
                }
                currentButtons.Add(button);
            }

            currentButtons.Add(CreateActionButton(typeButtonsRoot, "Back", StepBackOrClose));
            LinkVerticalNavigation(currentButtons);
        }

        private void PopulateMapStep()
        {
            ClearButtons(mapButtonsRoot);
            currentButtons.Clear();

            mapSummaryText.text = $"Type: {FormatRaceType(selectedRaceType)}";

            List<QuickRaceTrackEntry> tracks = GetUnlockedTracksForType(selectedRaceType);
            if (tracks.Count == 0)
            {
                currentButtons.Add(CreateActionButton(mapButtonsRoot, "No Unlocked Tracks", null, false));
                currentButtons.Add(CreateActionButton(mapButtonsRoot, "Back", StepBackOrClose));
                LinkVerticalNavigation(currentButtons);
                return;
            }

            for (int i = 0; i < tracks.Count; i++)
            {
                QuickRaceTrackEntry track = tracks[i];
                currentButtons.Add(CreateActionButton(mapButtonsRoot, track.DisplayName, () =>
                {
                    selectedTrack = track;
                    OpenStep(SelectionStep.Car);
                }));
            }

            currentButtons.Add(CreateActionButton(mapButtonsRoot, "Back", StepBackOrClose));
            LinkVerticalNavigation(currentButtons);
        }

        private void PopulateCarStep()
        {
            ClearButtons(carButtonsRoot);
            currentButtons.Clear();

            carSummaryText.text = $"Track: {selectedTrack.DisplayName}";
            List<PlayerCarDefinition> ownedCars = progressManager != null
                ? PlayerCarCatalog.GetOwnedCars(progressManager)
                : new List<PlayerCarDefinition> { PlayerCarCatalog.GetStarterDefinition() };

            for (int i = 0; i < ownedCars.Count; i++)
            {
                PlayerCarDefinition car = ownedCars[i];
                currentButtons.Add(CreateActionButton(carButtonsRoot, car.DisplayName, () =>
                {
                    selectedCar = car;
                    OpenStep(SelectionStep.Transmission);
                }));
            }

            currentButtons.Add(CreateActionButton(carButtonsRoot, "Back", StepBackOrClose));
            LinkVerticalNavigation(currentButtons);
        }

        private void PopulateTransmissionStep()
        {
            ClearButtons(transmissionButtonsRoot);
            currentButtons.Clear();

            transmissionSummaryText.text = $"{selectedTrack.DisplayName} | {selectedCar.DisplayName}";
            currentButtons.Add(CreateActionButton(transmissionButtonsRoot, "Automatic", () => StartQuickRace(QuickRaceTransmissionMode.Automatic)));
            currentButtons.Add(CreateActionButton(transmissionButtonsRoot, "Manual", () => StartQuickRace(QuickRaceTransmissionMode.Manual)));
            currentButtons.Add(CreateActionButton(transmissionButtonsRoot, "Back", StepBackOrClose));
            LinkVerticalNavigation(currentButtons);
        }

        private void StartQuickRace(QuickRaceTransmissionMode transmissionMode)
        {
            if (mainMenuController == null || string.IsNullOrWhiteSpace(selectedTrack.RaceId) || string.IsNullOrWhiteSpace(selectedCar.CarId))
            {
                return;
            }

            QuickRaceSessionData.Configure(selectedTrack.RaceId, selectedCar.CarId, selectedRaceType, transmissionMode);
            CloseSelection();
            mainMenuController.OpenQuickRace();
        }

        private PlayerCarDefinition ResolveDefaultCar()
        {
            if (progressManager != null && PlayerCarCatalog.TryGetDefinition(progressManager.CurrentOwnedCarId, out PlayerCarDefinition currentCar))
            {
                return currentCar;
            }

            return PlayerCarCatalog.GetStarterDefinition();
        }

        private RaceType ResolveDefaultType()
        {
            for (int i = 0; i < SupportedTypes.Length; i++)
            {
                if (CountUnlockedTracksForType(SupportedTypes[i]) > 0)
                {
                    return SupportedTypes[i];
                }
            }

            return RaceType.Sprint;
        }

        private int CountUnlockedTracks()
        {
            int count = 0;
            IReadOnlyList<QuickRaceTrackEntry> tracks = RaceCatalog.Tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (progressManager == null || progressManager.IsRaceUnlocked(tracks[i].RaceId))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountUnlockedTracksForType(RaceType raceType)
        {
            return GetUnlockedTracksForType(raceType).Count;
        }

        private List<QuickRaceTrackEntry> GetUnlockedTracksForType(RaceType raceType)
        {
            List<QuickRaceTrackEntry> tracks = new List<QuickRaceTrackEntry>();
            IReadOnlyList<QuickRaceTrackEntry> catalogTracks = RaceCatalog.Tracks;
            for (int i = 0; i < catalogTracks.Count; i++)
            {
                if (catalogTracks[i].RaceType != raceType)
                {
                    continue;
                }

                if (progressManager != null && !progressManager.IsRaceUnlocked(catalogTracks[i].RaceId))
                {
                    continue;
                }

                tracks.Add(catalogTracks[i]);
            }

            return tracks;
        }

        private static string FormatRaceType(RaceType raceType)
        {
            return raceType switch
            {
                RaceType.TimeTrial => "Time Trial",
                _ => raceType.ToString()
            };
        }

        private GameObject GetPanelForStep(SelectionStep step)
        {
            return step switch
            {
                SelectionStep.Type => typePanel,
                SelectionStep.Map => mapPanel,
                SelectionStep.Car => carPanel,
                _ => transmissionPanel
            };
        }

        private void SelectDefaultButton()
        {
            Button defaultButton = GetFirstInteractableButton();
            if (EventSystem.current == null || defaultButton == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
        }

        private GameObject CreateStepPanel(string name, string title, string subtitle, out TMP_Text summaryText, out Transform buttonsRoot)
        {
            GameObject panel = CreateObject(name, overlayRoot != null ? overlayRoot.transform : transform, typeof(RectTransform), typeof(CanvasGroup));
            Stretch(panel.GetComponent<RectTransform>());
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject topBand = CreateObject(name + "_TopBand", panel.transform, typeof(RectTransform), typeof(Image));
            ConfigureRect(topBand.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1240f, 72f));
            topBand.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
            CreateText(topBand.transform, "Brand", "FULL THROTTLE", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(520f, 58f), 30f, FontStyles.Bold | FontStyles.Italic, new Color(0.96f, 0.96f, 0.96f, 0.98f), TextAlignmentOptions.Left);
            CreateText(topBand.transform, "Title", title, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(420f, 58f), 30f, FontStyles.Italic, new Color(0.79f, 0.94f, 0.45f, 0.98f), TextAlignmentOptions.Right);

            GameObject divider = CreateObject(name + "_Divider", panel.transform, typeof(RectTransform), typeof(Image));
            ConfigureRect(divider.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1240f, 10f));
            divider.GetComponent<Image>().color = new Color(0.94f, 0.94f, 0.94f, 0.7f);

            summaryText = CreateText(panel.transform, name + "_Summary", subtitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(980f, 42f), 18f, FontStyles.Italic, new Color(0.92f, 0.92f, 0.92f, 0.82f), TextAlignmentOptions.Center);

            GameObject buttonStrip = CreateObject(name + "_Strip", panel.transform, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            ConfigureRect(buttonStrip.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1320f, 360f));
            buttonStrip.GetComponent<Image>().color = new Color(0.82f, 0.82f, 0.82f, 0.18f);
            VerticalLayoutGroup layout = buttonStrip.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 14f;
            layout.padding = new RectOffset(180, 180, 28, 28);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            buttonsRoot = buttonStrip.transform;
            return panel;
        }

        private static void ClearButtons(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Transform child = root.GetChild(i);
                    child.SetParent(null, false);
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(root.GetChild(i).gameObject);
                }
            }
        }

        private Button GetFirstInteractableButton()
        {
            for (int i = 0; i < currentButtons.Count; i++)
            {
                if (currentButtons[i] != null && currentButtons[i].interactable)
                {
                    return currentButtons[i];
                }
            }

            return null;
        }

        private bool TryBindExistingPanels()
        {
            Transform existingOverlay = transform.Find("QuickRaceSelectionRoot");
            if (existingOverlay == null)
            {
                return false;
            }

            overlayRoot = existingOverlay.gameObject;
            typePanel = FindPanel("QuickRace_TypePanel");
            mapPanel = FindPanel("QuickRace_MapPanel");
            carPanel = FindPanel("QuickRace_CarPanel");
            transmissionPanel = FindPanel("QuickRace_TransmissionPanel");

            typeSummaryText = FindText(typePanel, "QuickRace_TypePanel_Summary");
            mapSummaryText = FindText(mapPanel, "QuickRace_MapPanel_Summary");
            carSummaryText = FindText(carPanel, "QuickRace_CarPanel_Summary");
            transmissionSummaryText = FindText(transmissionPanel, "QuickRace_TransmissionPanel_Summary");

            typeButtonsRoot = FindButtonsRoot(typePanel, "QuickRace_TypePanel_Strip");
            mapButtonsRoot = FindButtonsRoot(mapPanel, "QuickRace_MapPanel_Strip");
            carButtonsRoot = FindButtonsRoot(carPanel, "QuickRace_CarPanel_Strip");
            transmissionButtonsRoot = FindButtonsRoot(transmissionPanel, "QuickRace_TransmissionPanel_Strip");

            return overlayRoot != null
                && typePanel != null
                && mapPanel != null
                && carPanel != null
                && transmissionPanel != null
                && typeSummaryText != null
                && mapSummaryText != null
                && carSummaryText != null
                && transmissionSummaryText != null
                && typeButtonsRoot != null
                && mapButtonsRoot != null
                && carButtonsRoot != null
                && transmissionButtonsRoot != null;
        }

        private GameObject FindPanel(string name)
        {
            Transform panel = overlayRoot != null ? overlayRoot.transform.Find(name) : null;
            return panel != null ? panel.gameObject : null;
        }

        private static TMP_Text FindText(GameObject root, string name)
        {
            Transform child = root != null ? root.transform.Find(name) : null;
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindButtonsRoot(GameObject root, string name)
        {
            return root != null ? root.transform.Find(name) : null;
        }

        private static Button CreateActionButton(Transform parent, string label, UnityEngine.Events.UnityAction action, bool enabled = true)
        {
            GameObject buttonObject = CreateObject(label.Replace(" ", string.Empty) + "Button", parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 64f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = enabled ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, 2f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.72f, 0.9f, 0.38f, 0.82f);
            colors.pressedColor = new Color(0.56f, 0.76f, 0.28f, 0.92f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.35f);
            button.colors = colors;
            button.interactable = enabled && action != null;
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            CreateText(buttonObject.transform, "Label", label.ToUpperInvariant(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 42f), 22f, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            return button;
        }

        private static void LinkVerticalNavigation(IReadOnlyList<Button> buttons)
        {
            if (buttons == null || buttons.Count == 0)
            {
                return;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null || !buttons[i].interactable)
                {
                    continue;
                }

                Button up = FindPreviousSelectable(buttons, i);
                Button down = FindNextSelectable(buttons, i);

                Navigation navigation = buttons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = up != null ? up : buttons[i];
                navigation.selectOnDown = down != null ? down : buttons[i];
                navigation.selectOnLeft = buttons[i];
                navigation.selectOnRight = buttons[i];
                buttons[i].navigation = navigation;
            }
        }

        private static Button FindPreviousSelectable(IReadOnlyList<Button> buttons, int currentIndex)
        {
            for (int offset = 1; offset < buttons.Count; offset++)
            {
                int index = (currentIndex - offset + buttons.Count) % buttons.Count;
                if (buttons[index] != null && buttons[index].interactable)
                {
                    return buttons[index];
                }
            }

            return null;
        }

        private static Button FindNextSelectable(IReadOnlyList<Button> buttons, int currentIndex)
        {
            for (int offset = 1; offset < buttons.Count; offset++)
            {
                int index = (currentIndex + offset) % buttons.Count;
                if (buttons[index] != null && buttons[index].interactable)
                {
                    return buttons[index];
                }
            }

            return null;
        }

        private static GameObject CreateObject(string name, Transform parent, params System.Type[] components)
        {
            GameObject gameObject = new GameObject(name, components);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, ResolvePivot(anchorMin, anchorMax), anchoredPosition, sizeDelta);

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = value;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static Vector2 ResolvePivot(Vector2 anchorMin, Vector2 anchorMax)
        {
            return new Vector2(
                Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
                Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        }
    }
}

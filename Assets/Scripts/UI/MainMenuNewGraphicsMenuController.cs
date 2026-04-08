using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Underground.UI
{
    public class MainMenuNewGraphicsMenuController : MonoBehaviour
    {
        private const int MaxVisibleRows = 8;
        private const float RowControlGroupWidth = 250f;
        private const float RowControlGroupRightInset = 22f;
        private const float RowLeftArrowLocalX = -88f;
        private const float RowRightArrowLocalX = 88f;
        private const float RowControlCenterLocalX = 0f;
        private const float RowTrackWidth = 138f;

        [SerializeField] private MainMenuFlowManager flowManager;
        [SerializeField] private Canvas parentCanvas;

        private readonly List<RowView> rowViews = new List<RowView>(MaxVisibleRows);

        private GameObject overlayRoot;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI pageIndicatorText;
        private Image scrollbarThumb;
        private Button displayPageButton;
        private Button customizePageOneButton;
        private Button customizePageTwoButton;
        private Button customizePageThreeButton;
        private Button applyButton;
        private Button backButton;

        private GraphicsPage currentPage = GraphicsPage.Display;
        private GameObject lastSelectedObject;
        private bool isInitialized;

        public bool IsOpen => overlayRoot != null && overlayRoot.activeSelf;

        public Selectable DefaultSelectable
        {
            get
            {
                for (int i = 0; i < rowViews.Count; i++)
                {
                    if (rowViews[i].RootButton.gameObject.activeSelf)
                    {
                        return rowViews[i].RootButton;
                    }
                }

                return backButton;
            }
        }

        private enum GraphicsPage
        {
            Display,
            CustomizeDisplayPage1,
            CustomizeDisplayPage2,
            CustomizeDisplayPage3
        }

        private sealed class GraphicsRowData
        {
            public string Label;
            public Func<string> GetValueText;
            public Func<float?> GetMeterValue;
            public Action OnLeft;
            public Action OnRight;
            public Action OnSubmit;
        }

        private sealed class RowView
        {
            public GameObject Root;
            public Button RootButton;
            public Image Background;
            public RectTransform ControlGroup;
            public TextMeshProUGUI LabelText;
            public TextMeshProUGUI ValueText;
            public Button LeftButton;
            public Button RightButton;
            public Image MeterTrack;
            public Image MeterFill;
            public MainMenuNewGraphicsRowInput Input;
        }

        private void Awake()
        {
            if (flowManager == null)
            {
                flowManager = GetComponent<MainMenuFlowManager>();
            }
        }

        public void EnsureInitialized()
        {
            if (isInitialized)
            {
                return;
            }

            if (flowManager == null)
            {
                flowManager = GetComponent<MainMenuFlowManager>();
            }

            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null)
            {
                parentCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            }

            if (parentCanvas == null)
            {
                return;
            }

            BuildOverlay(parentCanvas.transform);
            overlayRoot.SetActive(false);
            isInitialized = true;
        }

        public void OpenMenu()
        {
            EnsureInitialized();
            if (overlayRoot == null)
            {
                return;
            }

            lastSelectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            currentPage = GraphicsPage.Display;
            RefreshPage();
            overlayRoot.transform.SetAsLastSibling();
            overlayRoot.SetActive(true);
            SelectDefault();
        }

        public void CloseMenu()
        {
            if (!IsOpen)
            {
                return;
            }

            overlayRoot.SetActive(false);

            if (EventSystem.current == null)
            {
                return;
            }

            if (lastSelectedObject != null)
            {
                EventSystem.current.SetSelectedGameObject(lastSelectedObject);
                return;
            }

            Selectable fallback = flowManager != null ? flowManager.GetDefaultSelectableForState(MenuState.Options) : null;
            if (fallback != null)
            {
                EventSystem.current.SetSelectedGameObject(fallback.gameObject);
            }
        }

        public void StepBackOrClose()
        {
            switch (currentPage)
            {
                case GraphicsPage.Display:
                    CloseMenu();
                    break;
                default:
                    ShowPage(GraphicsPage.Display);
                    break;
            }
        }

        private void ShowPage(GraphicsPage page)
        {
            currentPage = page;
            RefreshPage();
            SelectDefault();
        }

        private void SelectDefault()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            Selectable selectable = DefaultSelectable;
            if (selectable != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        private void BuildOverlay(Transform parent)
        {
            overlayRoot = CreateObject("MainMenuNewGraphicsOverlay", parent, typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            overlayRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.04f);
            Canvas overlayCanvas = overlayRoot.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + 500 : 1000;

            GameObject panel = CreateObject("GraphicsPanel", overlayRoot.transform, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(RectMask2D));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 0f);
            panelRect.sizeDelta = new Vector2(1040f, 620f);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.78f, 0.78f, 0.78f, 0.84f);

            Outline panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            GameObject titleBar = CreateObject("TitleBar", panel.transform, typeof(RectTransform));
            RectTransform titleBarRect = titleBar.GetComponent<RectTransform>();
            titleBarRect.anchorMin = new Vector2(0f, 1f);
            titleBarRect.anchorMax = new Vector2(1f, 1f);
            titleBarRect.pivot = new Vector2(0.5f, 1f);
            titleBarRect.offsetMin = new Vector2(18f, -62f);
            titleBarRect.offsetMax = new Vector2(-18f, -20f);

            titleText = CreateText(titleBar.transform, "Title", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(420f, 40f), 21f, FontStyles.Bold, new Color(0.92f, 0.99f, 0.65f, 1f), TextAlignmentOptions.Left, string.Empty);
            pageIndicatorText = CreateText(titleBar.transform, "PageIndicator", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(110f, 34f), 14f, FontStyles.Italic, new Color(0.16f, 0.16f, 0.16f, 0.75f), TextAlignmentOptions.Right, string.Empty);

            GameObject rowsRoot = CreateObject("Rows", panel.transform, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(RectMask2D));
            RectTransform rowsRect = rowsRoot.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0f, 0f);
            rowsRect.anchorMax = new Vector2(1f, 1f);
            rowsRect.offsetMin = new Vector2(18f, 70f);
            rowsRect.offsetMax = new Vector2(-44f, -64f);

            VerticalLayoutGroup rowsLayout = rowsRoot.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 8f;
            rowsLayout.padding = new RectOffset(0, 0, 0, 0);
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            for (int i = 0; i < MaxVisibleRows; i++)
            {
                rowViews.Add(CreateRow(rowsRoot.transform, i));
            }

            GameObject scrollbarTrackObject = CreateObject("ScrollbarTrack", panel.transform, typeof(RectTransform), typeof(Image));
            RectTransform scrollbarTrackRect = scrollbarTrackObject.GetComponent<RectTransform>();
            scrollbarTrackRect.anchorMin = new Vector2(1f, 0f);
            scrollbarTrackRect.anchorMax = new Vector2(1f, 1f);
            scrollbarTrackRect.pivot = new Vector2(1f, 0.5f);
            scrollbarTrackRect.anchoredPosition = new Vector2(-14f, -4f);
            scrollbarTrackRect.sizeDelta = new Vector2(10f, -92f);
            scrollbarTrackObject.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f, 0.35f);

            GameObject scrollbarThumbObject = CreateObject("ScrollbarThumb", scrollbarTrackObject.transform, typeof(RectTransform), typeof(Image));
            RectTransform scrollbarThumbRect = scrollbarThumbObject.GetComponent<RectTransform>();
            scrollbarThumbRect.anchorMin = new Vector2(0.5f, 1f);
            scrollbarThumbRect.anchorMax = new Vector2(0.5f, 1f);
            scrollbarThumbRect.pivot = new Vector2(0.5f, 1f);
            scrollbarThumbRect.anchoredPosition = Vector2.zero;
            scrollbarThumbRect.sizeDelta = new Vector2(8f, 88f);
            scrollbarThumb = scrollbarThumbObject.GetComponent<Image>();
            scrollbarThumb.color = new Color(0.92f, 0.92f, 0.92f, 0.78f);

            displayPageButton = CreateFooterButton(panel.transform, "DisplayPage", new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(76f, 28f), "Display", () => ShowPage(GraphicsPage.Display), useLeftPivot: true);
            customizePageOneButton = CreateFooterButton(panel.transform, "CarPage", new Vector2(0f, 0f), new Vector2(100f, 18f), new Vector2(64f, 28f), "Car", () => ShowPage(GraphicsPage.CustomizeDisplayPage1), useLeftPivot: true);
            customizePageTwoButton = CreateFooterButton(panel.transform, "EffectsPage", new Vector2(0f, 0f), new Vector2(170f, 18f), new Vector2(76f, 28f), "Effects", () => ShowPage(GraphicsPage.CustomizeDisplayPage2), useLeftPivot: true);
            customizePageThreeButton = CreateFooterButton(panel.transform, "AdvancedPage", new Vector2(0f, 0f), new Vector2(252f, 18f), new Vector2(86f, 28f), "Advanced", () => ShowPage(GraphicsPage.CustomizeDisplayPage3), useLeftPivot: true);
            applyButton = CreateFooterButton(panel.transform, "Apply", new Vector2(1f, 0f), new Vector2(-138f, 18f), new Vector2(76f, 28f), "Apply", ApplyChanges);
            backButton = CreateFooterButton(panel.transform, "Back", new Vector2(1f, 0f), new Vector2(-52f, 18f), new Vector2(76f, 28f), "Back", StepBackOrClose);
        }

        private RowView CreateRow(Transform parent, int index)
        {
            RowView row = new RowView();

            row.Root = CreateObject("Row" + index, parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(MainMenuNewGraphicsRowInput));
            RectTransform rootRect = row.Root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.sizeDelta = new Vector2(0f, 32f);

            row.Background = row.Root.GetComponent<Image>();
            row.Background.color = new Color(0f, 0f, 0f, 0f);

            LayoutElement layout = row.Root.GetComponent<LayoutElement>();
            layout.preferredHeight = 32f;
            layout.minHeight = 32f;
            layout.flexibleHeight = 0f;

            row.RootButton = row.Root.GetComponent<Button>();
            row.RootButton.transition = Selectable.Transition.None;

            row.Input = row.Root.GetComponent<MainMenuNewGraphicsRowInput>();
            row.Input.Initialize(row.RootButton, row.Background);

            row.LabelText = CreateText(row.Root.transform, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(430f, 26f), 15f, FontStyles.Italic, new Color(0.18f, 0.18f, 0.18f, 0.72f), TextAlignmentOptions.Left, string.Empty);

            GameObject controlGroupObject = CreateObject("ControlGroup", row.Root.transform, typeof(RectTransform));
            row.ControlGroup = controlGroupObject.GetComponent<RectTransform>();
            row.ControlGroup.anchorMin = new Vector2(1f, 0.5f);
            row.ControlGroup.anchorMax = new Vector2(1f, 0.5f);
            row.ControlGroup.pivot = new Vector2(1f, 0.5f);
            row.ControlGroup.anchoredPosition = new Vector2(-RowControlGroupRightInset, 0f);
            row.ControlGroup.sizeDelta = new Vector2(RowControlGroupWidth, 26f);

            row.ValueText = CreateText(row.ControlGroup, "Value", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(RowControlCenterLocalX, -1f), new Vector2(RowTrackWidth, 20f), 14f, FontStyles.Italic, new Color(0.18f, 0.18f, 0.18f, 0.9f), TextAlignmentOptions.Center, string.Empty);

            row.MeterTrack = CreateImage(row.ControlGroup, "MeterTrack", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(RowControlCenterLocalX, -8f), new Vector2(RowTrackWidth, 4f), new Color(0f, 0f, 0f, 0.22f));
            row.MeterFill = CreateImage(row.MeterTrack.transform, "MeterFill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 4f), new Color(0.43f, 0.84f, 0.16f, 0.92f));
            row.MeterFill.rectTransform.pivot = new Vector2(0f, 0.5f);

            row.LeftButton = CreateArrowButton(row.ControlGroup, "LeftArrow", new Vector2(0.5f, 0.5f), new Vector2(RowLeftArrowLocalX, 0f), () => row.Input.TriggerLeft());
            row.RightButton = CreateArrowButton(row.ControlGroup, "RightArrow", new Vector2(0.5f, 0.5f), new Vector2(RowRightArrowLocalX, 0f), () => row.Input.TriggerRight());
            return row;
        }

        private void RefreshPage()
        {
            GameSettingsManager settings = ResolveSettingsManager();
            List<GraphicsRowData> rows = BuildRows(settings);
            titleText.text = GetTitleForPage();
            pageIndicatorText.text = GetSubtitleForPage();
            RefreshScrollbar();
            RefreshPageButtons();

            for (int i = 0; i < rowViews.Count; i++)
            {
                RowView row = rowViews[i];
                bool isVisible = i < rows.Count;
                row.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                GraphicsRowData rowData = rows[i];
                row.LabelText.text = rowData.Label;
                row.ValueText.text = rowData.GetValueText != null ? rowData.GetValueText() : string.Empty;

                float? meterValue = rowData.GetMeterValue != null ? rowData.GetMeterValue() : null;
                bool showMeter = meterValue.HasValue;
                row.MeterTrack.gameObject.SetActive(showMeter);
                if (showMeter)
                {
                    row.MeterFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, RowTrackWidth * Mathf.Clamp01(meterValue.Value));
                }

                bool isAdjustable = rowData.OnLeft != null || rowData.OnRight != null;
                row.LeftButton.gameObject.SetActive(isAdjustable);
                row.RightButton.gameObject.SetActive(isAdjustable);
                row.ValueText.alignment = isAdjustable ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
                row.ValueText.rectTransform.anchoredPosition = isAdjustable ? new Vector2(RowControlCenterLocalX, -1f) : new Vector2(96f, -1f);
                row.ValueText.rectTransform.sizeDelta = isAdjustable ? new Vector2(RowTrackWidth, 20f) : new Vector2(186f, 20f);
                row.MeterTrack.rectTransform.anchoredPosition = new Vector2(RowControlCenterLocalX, -8f);
                row.MeterTrack.rectTransform.sizeDelta = new Vector2(RowTrackWidth, 4f);

                row.Input.Configure(
                    () => InvokeAndRefresh(rowData.OnLeft),
                    () => InvokeAndRefresh(rowData.OnRight),
                    () => InvokeAndRefresh(rowData.OnSubmit ?? rowData.OnRight));
            }

            LinkRowNavigation(rows.Count);
        }

        private void RefreshPageButtons()
        {
            UpdatePageButtonState(displayPageButton, currentPage == GraphicsPage.Display);
            UpdatePageButtonState(customizePageOneButton, currentPage == GraphicsPage.CustomizeDisplayPage1);
            UpdatePageButtonState(customizePageTwoButton, currentPage == GraphicsPage.CustomizeDisplayPage2);
            UpdatePageButtonState(customizePageThreeButton, currentPage == GraphicsPage.CustomizeDisplayPage3);
        }

        private void RefreshScrollbar()
        {
            if (scrollbarThumb == null)
            {
                return;
            }

            float normalizedPosition = currentPage switch
            {
                GraphicsPage.Display => 0f,
                GraphicsPage.CustomizeDisplayPage1 => 0.25f,
                GraphicsPage.CustomizeDisplayPage2 => 0.58f,
                _ => 0.9f
            };

            scrollbarThumb.rectTransform.anchoredPosition = new Vector2(0f, -normalizedPosition * 220f);
        }

        private void LinkRowNavigation(int visibleRowCount)
        {
            if (visibleRowCount <= 0)
            {
                return;
            }

            for (int i = 0; i < visibleRowCount; i++)
            {
                Navigation navigation = rowViews[i].RootButton.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = i > 0 ? rowViews[i - 1].RootButton : rowViews[visibleRowCount - 1].RootButton;
                navigation.selectOnDown = i < visibleRowCount - 1 ? rowViews[i + 1].RootButton : backButton;
                navigation.selectOnLeft = rowViews[i].RootButton;
                navigation.selectOnRight = rowViews[i].RootButton;
                rowViews[i].RootButton.navigation = navigation;
            }

            Navigation backNavigation = backButton.navigation;
            backNavigation.mode = Navigation.Mode.Explicit;
            backNavigation.selectOnUp = rowViews[visibleRowCount - 1].RootButton;
            backNavigation.selectOnDown = rowViews[0].RootButton;
            backNavigation.selectOnLeft = applyButton != null ? applyButton : backButton;
            backNavigation.selectOnRight = backButton;
            backButton.navigation = backNavigation;

            if (applyButton != null)
            {
                Navigation applyNavigation = applyButton.navigation;
                applyNavigation.mode = Navigation.Mode.Explicit;
                applyNavigation.selectOnUp = rowViews[visibleRowCount - 1].RootButton;
                applyNavigation.selectOnDown = rowViews[0].RootButton;
                applyNavigation.selectOnLeft = customizePageThreeButton != null ? customizePageThreeButton : applyButton;
                applyNavigation.selectOnRight = backButton;
                applyButton.navigation = applyNavigation;
            }

            if (displayPageButton != null)
            {
                LinkFooterButton(displayPageButton, rowViews[visibleRowCount - 1].RootButton, rowViews[0].RootButton, displayPageButton, customizePageOneButton != null ? customizePageOneButton : applyButton);
            }

            if (customizePageOneButton != null)
            {
                LinkFooterButton(customizePageOneButton, rowViews[visibleRowCount - 1].RootButton, rowViews[0].RootButton, displayPageButton, customizePageTwoButton != null ? customizePageTwoButton : applyButton);
            }

            if (customizePageTwoButton != null)
            {
                LinkFooterButton(customizePageTwoButton, rowViews[visibleRowCount - 1].RootButton, rowViews[0].RootButton, customizePageOneButton, customizePageThreeButton != null ? customizePageThreeButton : applyButton);
            }

            if (customizePageThreeButton != null)
            {
                LinkFooterButton(customizePageThreeButton, rowViews[visibleRowCount - 1].RootButton, rowViews[0].RootButton, customizePageTwoButton, applyButton != null ? applyButton : backButton);
            }
        }

        private List<GraphicsRowData> BuildRows(GameSettingsManager settings)
        {
            List<GraphicsRowData> rows = new List<GraphicsRowData>(MaxVisibleRows);
            if (settings == null)
            {
                rows.Add(new GraphicsRowData
                {
                    Label = "Settings Manager Missing",
                    GetValueText = () => "Unavailable"
                });
                return rows;
            }

            switch (currentPage)
            {
                case GraphicsPage.Display:
                    IReadOnlyList<Resolution> resolutions = settings.GetAvailableResolutions();
                    int resolutionCount = resolutions.Count;
                    int qualityCount = Mathf.Max(1, QualitySettings.names.Length);

                    rows.Add(CreateAdjustableRow("Resolution", settings.GetCurrentResolutionLabel, () => NormalizeIndex(settings.ResolutionIndex, resolutionCount), () => settings.CycleResolution(-1), () => settings.CycleResolution(1)));
                    rows.Add(CreateAdjustableRow("Level Of Detail", settings.GetQualityLabel, () => NormalizeIndex(settings.QualityLevel, qualityCount), () => settings.CycleQualityLevel(-1), () => settings.CycleQualityLevel(1)));
                    rows.Add(new GraphicsRowData
                    {
                        Label = "Customize Display Settings",
                        GetValueText = () => "Select",
                        OnSubmit = () => ShowPage(GraphicsPage.CustomizeDisplayPage1)
                    });
                    rows.Add(new GraphicsRowData
                    {
                        Label = "Default Settings",
                        GetValueText = () => "Apply",
                        OnSubmit = ApplyDefaultSettings
                    });
                    break;

                case GraphicsPage.CustomizeDisplayPage1:
                    rows.Add(CreateAdjustableRow("Car Reflection Update Rate", settings.GetCarReflectionUpdateRateLabel, () => NormalizeThreeLevel(settings.CarReflectionUpdateRate), () => settings.CycleCarReflectionUpdateRate(-1), () => settings.CycleCarReflectionUpdateRate(1)));
                    rows.Add(CreateAdjustableRow("Car Reflection Detail", settings.GetCarReflectionDetailLabel, () => NormalizeThreeLevel(settings.CarReflectionDetail), () => settings.CycleCarReflectionDetail(-1), () => settings.CycleCarReflectionDetail(1)));
                    rows.Add(CreateAdjustableRow("Car Shadow / Neon", settings.GetCarShadowDetailLabel, () => NormalizeThreeLevel(settings.CarShadowDetail), () => settings.CycleCarShadowDetail(-1), () => settings.CycleCarShadowDetail(1)));
                    rows.Add(CreateToggleRow("Car Headlight", () => settings.CarHeadlightsEnabled, settings.ToggleCarHeadlights));
                    rows.Add(CreateAdjustableRow("Car Geometry Detail", settings.GetCarGeometryDetailLabel, () => NormalizeThreeLevel(settings.CarGeometryDetail), () => settings.CycleCarGeometryDetail(-1), () => settings.CycleCarGeometryDetail(1)));
                    rows.Add(CreateToggleRow("Crowds", () => settings.CrowdsEnabled, settings.ToggleCrowds));
                    rows.Add(CreateAdjustableRow("World Detail", settings.GetWorldDetailLabel, () => NormalizeThreeLevel(settings.WorldDetail), () => settings.CycleWorldDetail(-1), () => settings.CycleWorldDetail(1)));
                    rows.Add(CreateAdjustableRow("Road Reflection Detail", settings.GetRoadReflectionDetailLabel, () => NormalizeThreeLevel(settings.RoadReflectionDetail), () => settings.CycleRoadReflectionDetail(-1), () => settings.CycleRoadReflectionDetail(1)));
                    break;

                case GraphicsPage.CustomizeDisplayPage2:
                    rows.Add(CreateToggleRow("Light Trails", () => settings.LightTrailsEnabled, settings.ToggleLightTrails));
                    rows.Add(CreateToggleRow("Light Glow", () => settings.LightGlowEnabled, settings.ToggleLightGlow));
                    rows.Add(CreateToggleRow("Particle System", () => settings.ParticleSystemsEnabled, settings.ToggleParticleSystems));
                    rows.Add(CreateToggleRow("Motion Blur", () => settings.MotionBlurEnabled, settings.ToggleMotionBlur));
                    rows.Add(CreateToggleRow("Fog", () => settings.FogEnabled, settings.ToggleFog));
                    rows.Add(CreateToggleRow("Depth Of Field", () => settings.DepthOfFieldEnabled, settings.ToggleDepthOfField));
                    rows.Add(CreateAdjustableRow("Full Screen Anti-Aliasing", settings.GetFullScreenAntiAliasingLabel, () => NormalizeThreeLevel(settings.FullScreenAntiAliasing), () => settings.CycleFullScreenAntiAliasing(-1), () => settings.CycleFullScreenAntiAliasing(1)));
                    rows.Add(CreateToggleRow("Tinting", () => settings.TintingEnabled, settings.ToggleTinting));
                    break;

                case GraphicsPage.CustomizeDisplayPage3:
                    rows.Add(CreateAdjustableRow("Full Screen Anti-Aliasing", settings.GetFullScreenAntiAliasingLabel, () => NormalizeThreeLevel(settings.FullScreenAntiAliasing), () => settings.CycleFullScreenAntiAliasing(-1), () => settings.CycleFullScreenAntiAliasing(1)));
                    rows.Add(CreateToggleRow("Tinting", () => settings.TintingEnabled, settings.ToggleTinting));
                    rows.Add(CreateToggleRow("Horizon Fog", () => settings.HorizonFogEnabled, settings.ToggleHorizonFog));
                    rows.Add(CreateToggleRow("Over Bright", () => settings.OverBrightEnabled, settings.ToggleOverBright));
                    rows.Add(CreateToggleRow("Enhanced Contrast", () => settings.AdvancedContrastEnabled, settings.ToggleAdvancedContrast));
                    rows.Add(CreateToggleRow("Rain Splatter", () => settings.RainSplatterEnabled, settings.ToggleRainSplatter));
                    rows.Add(CreateAdjustableRow("Texture Filtering", settings.GetTextureFilteringLabel, () => NormalizeThreeLevel(settings.TextureFiltering), () => settings.CycleTextureFiltering(-1), () => settings.CycleTextureFiltering(1)));
                    rows.Add(CreateToggleRow("Vsync", () => settings.VSyncEnabled, settings.ToggleVSync));
                    break;
            }

            return rows;
        }

        private static GraphicsRowData CreateAdjustableRow(string label, Func<string> getValue, Func<float?> getMeterValue, Action onLeft, Action onRight)
        {
            return new GraphicsRowData
            {
                Label = label,
                GetValueText = getValue,
                GetMeterValue = getMeterValue,
                OnLeft = onLeft,
                OnRight = onRight
            };
        }

        private static GraphicsRowData CreateToggleRow(string label, Func<bool> getter, Action toggleAction)
        {
            return new GraphicsRowData
            {
                Label = label,
                GetValueText = () => getter() ? "On" : "Off",
                GetMeterValue = () => getter() ? 1f : 0f,
                OnLeft = toggleAction,
                OnRight = toggleAction
            };
        }

        private string GetTitleForPage()
        {
            return currentPage == GraphicsPage.Display ? "Display" : "Customize Display Settings";
        }

        private string GetSubtitleForPage()
        {
            return currentPage switch
            {
                GraphicsPage.Display => "Display",
                GraphicsPage.CustomizeDisplayPage1 => "Car",
                GraphicsPage.CustomizeDisplayPage2 => "Effects",
                _ => "Advanced"
            };
        }

        private void ApplyDefaultSettings()
        {
            GameSettingsManager settings = ResolveSettingsManager();
            if (settings == null)
            {
                return;
            }

            settings.SetQualityLevel(Mathf.Max(0, QualitySettings.names.Length - 1));
            settings.SetShadowQuality(2);
            settings.SetTextureQuality(2);
            settings.SetCarReflectionUpdateRate(2);
            settings.SetCarReflectionDetail(2);
            settings.SetCarShadowDetail(2);
            settings.SetCarHeadlightsEnabled(true);
            settings.SetCarGeometryDetail(2);
            settings.SetCrowdsEnabled(true);
            settings.SetWorldDetail(2);
            settings.SetRoadReflectionDetail(2);
            settings.SetLightTrailsEnabled(true);
            settings.SetLightGlowEnabled(true);
            settings.SetParticleSystemsEnabled(true);
            settings.SetMotionBlurEnabled(true);
            settings.SetFogEnabled(true);
            settings.SetDepthOfFieldEnabled(true);
            settings.SetFullScreenAntiAliasing(2);
            settings.SetTintingEnabled(true);
            settings.SetHorizonFogEnabled(true);
            settings.SetOverBrightEnabled(true);
            settings.SetAdvancedContrastEnabled(true);
            settings.SetRainSplatterEnabled(true);
            settings.SetTextureFiltering(2);
            settings.SetVSync(false);
        }

        private void ApplyChanges()
        {
            GameSettingsManager settings = ResolveSettingsManager();
            settings?.RefreshAll();
            CloseMenu();
        }

        private void InvokeAndRefresh(Action action)
        {
            action?.Invoke();
            RefreshPage();
        }

        private GameSettingsManager ResolveSettingsManager()
        {
            return GameSettingsManager.Instance ?? FindFirstObjectByType<GameSettingsManager>(FindObjectsInactive.Include);
        }

        private static float NormalizeThreeLevel(int value)
        {
            return Mathf.Clamp01(value / 2f);
        }

        private static float NormalizeIndex(int value, int count)
        {
            if (count <= 1)
            {
                return 1f;
            }

            return Mathf.Clamp01(value / (float)(count - 1));
        }

        private static GameObject CreateObject(string name, Transform parent, params Type[] components)
        {
            GameObject gameObject = new GameObject(name, components);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment, string text)
        {
            GameObject textObject = CreateObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = ResolvePivot(anchorMin, anchorMax);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            return tmp;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject imageObject = CreateObject(name, parent, typeof(RectTransform), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = ResolvePivot(anchorMin, anchorMax);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Button CreateArrowButton(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(22f, 22f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.22f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.36f, 0.63f, 0.22f, 0.8f);
            colors.pressedColor = new Color(0.27f, 0.54f, 0.15f, 0.9f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            CreateText(buttonObject.transform, "Glyph", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f), 14f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, name.Contains("Left") ? "<" : ">");
            return button;
        }

        private static Button CreateFooterButton(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string label, UnityEngine.Events.UnityAction action, bool useLeftPivot = false)
        {
            GameObject buttonObject = CreateObject(name + "Button", parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = useLeftPivot ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = name == "Apply"
                ? new Color(0.28f, 0.48f, 0.18f, 0.92f)
                : name == "Back"
                    ? new Color(0.18f, 0.18f, 0.22f, 0.92f)
                    : new Color(0.1f, 0.1f, 0.12f, 0.72f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = name == "Apply"
                ? new Color(0.36f, 0.62f, 0.22f, 0.96f)
                : name == "Back"
                    ? new Color(0.31f, 0.57f, 0.18f, 0.92f)
                    : new Color(0.2f, 0.2f, 0.24f, 0.78f);
            colors.pressedColor = new Color(0.26f, 0.5f, 0.15f, 0.96f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            CreateText(buttonObject.transform, name + "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12f, 8f), 13f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, label);
            return button;
        }

        private static void LinkFooterButton(Button button, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = left ?? button;
            navigation.selectOnRight = right ?? button;
            button.navigation = navigation;
        }

        private static void UpdatePageButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = isActive
                    ? new Color(0.31f, 0.57f, 0.18f, 0.92f)
                    : new Color(0.1f, 0.1f, 0.12f, 0.72f);
            }
        }

        private static Vector2 ResolvePivot(Vector2 anchorMin, Vector2 anchorMax)
        {
            return new Vector2(
                Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
                Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        }
    }

    public sealed class MainMenuNewGraphicsRowInput : MonoBehaviour, ISelectHandler, IDeselectHandler, IMoveHandler
    {
        private static readonly Color UnselectedColor = new Color(0f, 0f, 0f, 0f);
        private static readonly Color SelectedColor = new Color(0.25f, 0.31f, 0.16f, 0.9f);

        private Button rootButton;
        private Image background;
        private Action onLeft;
        private Action onRight;
        private Action onSubmit;

        public void Initialize(Button button, Image targetBackground)
        {
            rootButton = button;
            background = targetBackground;
            ApplySelectionState(false);
            if (rootButton != null)
            {
                rootButton.onClick.AddListener(TriggerSubmit);
            }
        }

        public void Configure(Action leftAction, Action rightAction, Action submitAction)
        {
            onLeft = leftAction;
            onRight = rightAction;
            onSubmit = submitAction;
        }

        public void TriggerLeft()
        {
            onLeft?.Invoke();
        }

        public void TriggerRight()
        {
            onRight?.Invoke();
        }

        public void OnSelect(BaseEventData eventData)
        {
            ApplySelectionState(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            ApplySelectionState(false);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
            {
                onLeft?.Invoke();
                return;
            }

            if (eventData.moveDir == MoveDirection.Right)
            {
                onRight?.Invoke();
            }
        }

        private void TriggerSubmit()
        {
            onSubmit?.Invoke();
        }

        private void ApplySelectionState(bool isSelected)
        {
            if (background != null)
            {
                background.color = isSelected ? SelectedColor : UnselectedColor;
            }
        }
    }
}

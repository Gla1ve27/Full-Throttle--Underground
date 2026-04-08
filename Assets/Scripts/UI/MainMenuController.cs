using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Underground.Core.Architecture;
using Underground.Save;

namespace Underground.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private const string FallbackCanvasName = "MainMenuFallbackCanvas";

        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private string careerSceneName = "Garage";
        [SerializeField] private string quickRaceSceneName = "Garage";
        [SerializeField] private bool allowRuntimeFallbackMenu = true;
        [SerializeField] private bool preferRuntimeStyledMenu = true;

        private Canvas fallbackCanvas;
        private GameObject fallbackSettingsPanel;

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = ServiceResolver.Resolve<IProgressService>(null) as PersistentProgressManager
                    ?? FindFirstObjectByType<PersistentProgressManager>();
            }

            if (saveSystem == null)
            {
                saveSystem = ServiceResolver.Resolve<ISaveService>(null) as SaveSystem
                    ?? FindFirstObjectByType<SaveSystem>();
            }
        }

        private void Start()
        {
            if (!allowRuntimeFallbackMenu)
            {
                return;
            }

            if (preferRuntimeStyledMenu)
            {
                DisableExistingMenuCanvases();
                BuildFallbackMenu();
                return;
            }

            if (HasUsableMenuCanvas())
            {
                return;
            }

            BuildFallbackMenu();
        }

        public void ContinueGame()
        {
            SceneManager.LoadScene(careerSceneName);
        }

        public void StartNewGame()
        {
            saveSystem?.DeleteSave();
            progressManager?.ResetToDefaults();
            progressManager?.SaveNow();
            SceneManager.LoadScene(careerSceneName);
        }

        public void OpenCareer()
        {
            ContinueGame();
        }

        public void LoadGame()
        {
            ContinueGame();
        }

        public void OpenQuickRace()
        {
            SceneManager.LoadScene(quickRaceSceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private bool HasUsableMenuCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (!canvas.isRootCanvas || !canvas.enabled || !canvas.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (canvas.name == FallbackCanvasName)
                {
                    return true;
                }

                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    continue;
                }

                if (canvas.GetComponentInChildren<Button>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildFallbackMenu()
        {
            if (fallbackCanvas != null)
            {
                return;
            }

            EnsureEventSystem();

            GameObject canvasObject = new GameObject(FallbackCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            fallbackCanvas = canvasObject.GetComponent<Canvas>();
            fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fallbackCanvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CreateFullscreenBackdrop(canvasObject.transform);
            CreateTopHeader(canvasObject.transform);
            CreateModeStrip(canvasObject.transform);
            CreateBottomActionStack(canvasObject.transform);

            fallbackSettingsPanel = CreatePanel(canvasObject.transform, "SettingsPanel", new Vector2(560f, 420f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f));
            fallbackSettingsPanel.SetActive(false);

            VerticalLayoutGroup settingsLayout = fallbackSettingsPanel.AddComponent<VerticalLayoutGroup>();
            settingsLayout.childAlignment = TextAnchor.UpperLeft;
            settingsLayout.spacing = 14f;
            settingsLayout.padding = new RectOffset(28, 28, 28, 28);
            settingsLayout.childControlHeight = false;
            settingsLayout.childControlWidth = true;
            settingsLayout.childForceExpandHeight = false;
            settingsLayout.childForceExpandWidth = true;

            CreateLabel(fallbackSettingsPanel.transform, "SETTINGS", 30, FontStyle.Bold, new Color(0.92f, 0.96f, 1f, 1f), TextAnchor.MiddleLeft);
            CreateSpacer(fallbackSettingsPanel.transform, 8f);
            CreateSettingsButton(fallbackSettingsPanel.transform, "Fullscreen", () => FindFirstObjectByType<GameSettingsManager>()?.ToggleFullscreen());
            CreateSettingsButton(fallbackSettingsPanel.transform, "VSync", () => FindFirstObjectByType<GameSettingsManager>()?.ToggleVSync());
            CreateSettingsButton(fallbackSettingsPanel.transform, "Quality", () => FindFirstObjectByType<GameSettingsManager>()?.CycleQualityLevel(1));
            CreateSettingsButton(fallbackSettingsPanel.transform, "Close", ToggleFallbackSettings);
        }

        private void DisableExistingMenuCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (!canvas.isRootCanvas || canvas.name == FallbackCanvasName)
                {
                    continue;
                }

                if (canvas.GetComponentInChildren<Button>(true) == null)
                {
                    continue;
                }

                canvas.gameObject.SetActive(false);
            }
        }

        private void ToggleFallbackSettings()
        {
            if (fallbackSettingsPanel == null)
            {
                return;
            }

            fallbackSettingsPanel.SetActive(!fallbackSettingsPanel.activeSelf);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystemObject);
        }

        private static void CreateFullscreenBackdrop(Transform parent)
        {
            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(parent, false);

            RectTransform rectTransform = backdrop.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = backdrop.GetComponent<Image>();
            image.color = new Color(0.01f, 0.015f, 0.025f, 0.18f);
            image.raycastTarget = false;
        }

        private void CreateTopHeader(Transform parent)
        {
            GameObject topBand = CreatePanel(parent, "TopBand", new Vector2(1240f, 58f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f));
            topBand.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
            CreateText(topBand.transform, "FULL THROTTLE UNDERGROUND", new Vector2(24f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 34, FontStyle.BoldAndItalic, new Color(0.94f, 0.94f, 0.94f, 0.98f), TextAnchor.MiddleLeft);
            CreateText(topBand.transform, "Main Menu", new Vector2(-28f, 0f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), 30, FontStyle.Italic, new Color(0.77f, 0.92f, 0.45f, 0.98f), TextAnchor.MiddleRight);

            GameObject rule = new GameObject("HeaderRule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(parent, false);
            RectTransform ruleRect = rule.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0.5f, 1f);
            ruleRect.anchorMax = new Vector2(0.5f, 1f);
            ruleRect.pivot = new Vector2(0.5f, 1f);
            ruleRect.sizeDelta = new Vector2(1240f, 10f);
            ruleRect.anchoredPosition = new Vector2(0f, -118f);
            rule.GetComponent<Image>().color = new Color(0.92f, 0.92f, 0.92f, 0.72f);
        }

        private void CreateModeStrip(Transform parent)
        {
            GameObject strip = CreatePanel(parent, "ModeStrip", new Vector2(1280f, 168f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -215f));
            strip.GetComponent<Image>().color = new Color(0.74f, 0.74f, 0.74f, 0.26f);

            CreateModeArrow(strip.transform, "LeftArrow", new Vector2(40f, 0f), "<");
            CreateModeArrow(strip.transform, "RightArrow", new Vector2(-40f, 0f), ">");

            CreateModeTile(strip.transform, "Garage", new Vector2(-250f, 0f), new Vector2(220f, 104f), new Color(1f, 1f, 1f, 0.12f), "GARAGE", false);
            CreateModeTile(strip.transform, "QuickRace", new Vector2(0f, 0f), new Vector2(300f, 124f), new Color(0.73f, 0.95f, 0.38f, 0.24f), "QUICK RACE", true);
            CreateModeTile(strip.transform, "WorldMap", new Vector2(250f, 0f), new Vector2(220f, 104f), new Color(1f, 1f, 1f, 0.12f), "WORLD MAP", false);

            CreateText(parent, "Quick Race", new Vector2(0f, -308f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 34, FontStyle.Italic, new Color(0.76f, 0.95f, 0.42f, 0.98f), TextAnchor.MiddleCenter);
        }

        private void CreateBottomActionStack(Transform parent)
        {
            GameObject actionsPanel = new GameObject("ActionStack", typeof(RectTransform));
            actionsPanel.transform.SetParent(parent, false);

            RectTransform rect = actionsPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(380f, 260f);
            rect.anchoredPosition = new Vector2(-60f, 52f);

            VerticalLayoutGroup layout = actionsPanel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateUndergroundActionButton(actionsPanel.transform, "Continue", ContinueGame);
            CreateUndergroundActionButton(actionsPanel.transform, "New Game", StartNewGame);
            CreateUndergroundActionButton(actionsPanel.transform, "Settings", ToggleFallbackSettings);
            CreateUndergroundActionButton(actionsPanel.transform, "Quit Game", QuitGame);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(anchorMax.x, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;

            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.05f, 0.08f, 0.12f, 0.88f);

            return panel;
        }

        private static void CreateText(Transform parent, string text, Vector2 anchoredPosition, Vector2 anchor, Vector2 pivot, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(text, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(420f, fontSize + 24f);
            rect.anchoredPosition = anchoredPosition;

            Text label = textObject.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = alignment;
        }

        private static void CreateModeArrow(Transform parent, string name, Vector2 anchoredPosition, string symbol)
        {
            GameObject arrow = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            arrow.transform.SetParent(parent, false);

            RectTransform rect = arrow.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(name == "LeftArrow" ? 0f : 1f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(64f, 64f);
            rect.anchoredPosition = anchoredPosition;

            Image image = arrow.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.1f);

            CreateText(arrow.transform, symbol, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 34, FontStyle.Bold, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
        }

        private static void CreateModeTile(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color fillColor, string label, bool selected)
        {
            GameObject tile = new GameObject(name, typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(parent, false);

            RectTransform rect = tile.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = tile.GetComponent<Image>();
            image.color = fillColor;

            if (selected)
            {
                Outline outline = tile.AddComponent<Outline>();
                outline.effectColor = new Color(0.72f, 0.94f, 0.36f, 0.95f);
                outline.effectDistance = new Vector2(4f, 4f);
            }

            CreateText(tile.transform, label, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), selected ? 26 : 22, FontStyle.BoldAndItalic, new Color(1f, 1f, 1f, selected ? 0.95f : 0.5f), TextAnchor.MiddleCenter);
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static void CreateLabel(Transform parent, string text, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            GameObject labelObject = new GameObject(text, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            Text label = labelObject.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement layout = labelObject.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize + 20f;
        }

        private static void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 68f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.2f, 0.3f, 0.96f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.18f, 0.32f, 0.46f, 1f);
            colors.pressedColor = new Color(0.08f, 0.14f, 0.22f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(action);

            CreateButtonText(buttonObject.transform, label);
        }

        private static void CreateSettingsButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            CreateButton(parent, label, action);
        }

        private static void CreateUndergroundActionButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 46f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.03f, 0.03f, 0.03f, 0.62f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.42f);
            outline.effectDistance = new Vector2(2f, 2f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.1f, 0.14f, 0.1f, 0.82f);
            colors.pressedColor = new Color(0.12f, 0.18f, 0.08f, 0.9f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(action);

            CreateButtonText(buttonObject.transform, label);
        }

        private static void CreateButtonText(Transform parent, string text)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(20f, 0f);
            rectTransform.offsetMax = new Vector2(-20f, 0f);

            Text label = textObject.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.fontStyle = FontStyle.BoldAndItalic;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.96f, 0.98f, 1f, 1f);
        }
    }
}

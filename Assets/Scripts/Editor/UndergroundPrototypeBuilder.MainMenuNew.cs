using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Underground.UI;
using Object = UnityEngine.Object;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private const string MainMenuNewScenePath = "Assets/Scenes/Menu/MainMenuNew.unity";
        private const string MainMenuNewCanvasName = "Canvas";
        private const string MainMenuNewMarkerName = "MainMenuUI";
        private const string MainMenuNewAutoBuildSessionKey = "Underground.MainMenuNewScene.AutoBuilt";

        [InitializeOnLoadMethod]
        private static void AutoBuildMainMenuNewSceneOnLoad()
        {
            if (SessionState.GetBool(MainMenuNewAutoBuildSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(MainMenuNewAutoBuildSessionKey, true);
            EditorApplication.delayCall += TryAutoBuildMainMenuNewScene;
        }

        [MenuItem("Full Throttle/UI/Rebuild Main Menu New Scene", priority = 43)]
        public static void RebuildMainMenuNewSceneFromTopMenu()
        {
            PreparePrototypeAssets(out GameObject playerCarPrefab, out _, out _, out _, out _);
            CreateMainMenuNewScene(playerCarPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void TryAutoBuildMainMenuNewScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            if (IsMainMenuNewSceneCurrent())
            {
                return;
            }

            PreparePrototypeAssets(out GameObject playerCarPrefab, out _, out _, out _, out _);
            CreateMainMenuNewScene(playerCarPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool IsMainMenuNewSceneCurrent()
        {
            if (!File.Exists(MainMenuNewScenePath))
            {
                return false;
            }

            string sceneText = File.ReadAllText(MainMenuNewScenePath);
            if (HasPreservedMainMenuNewUiSceneContent(sceneText))
            {
                return true;
            }

            return sceneText.Contains("SplashPanel")
                && sceneText.Contains("MainMenuPanel")
                && sceneText.Contains("CareerPanel")
                && sceneText.Contains("QuickRacePanel")
                && sceneText.Contains("CustomizePanel")
                && sceneText.Contains("OptionsPanel")
                && sceneText.Contains(MainMenuNewMarkerName);
        }

        private static void CreateMainMenuNewScene(GameObject playerCarPrefab)
        {
            SceneAsset existingSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuNewScenePath);
            if (existingSceneAsset != null)
            {
                Scene existingScene = EditorSceneManager.OpenScene(MainMenuNewScenePath, OpenSceneMode.Single);
                RemoveMissingScriptsFromScene(existingScene);
                RebuildMainMenuNewScenePreservingExistingUi(existingScene, playerCarPrefab);
                EditorSceneManager.SaveScene(existingScene, MainMenuNewScenePath);
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRuntimeRoot(false);
            EnsureEventSystem();
            ComposeGarageBackdropForMenu(playerCarPrefab);

            MainMenuController menuController = new GameObject("MainMenuController").AddComponent<MainMenuController>();
            SetBoolValue(menuController, "allowRuntimeFallbackMenu", false);
            SetBoolValue(menuController, "preferRuntimeStyledMenu", false);
            SetStringValue(menuController, "quickRaceSceneName", "World");

            Canvas canvas = CreateMainMenuCanvas();
            GameObject mainMenuUI = CreatePanelRoot(canvas.transform, MainMenuNewMarkerName);

            MainMenuFlowManager flowManager = mainMenuUI.AddComponent<MainMenuFlowManager>();
            flowManager.Initialize(menuController);
            SetObjectReference(flowManager, "mainMenuController", menuController);
            if (mainMenuUI.GetComponent<MainMenuNewGraphicsMenuController>() == null)
            {
                mainMenuUI.AddComponent<MainMenuNewGraphicsMenuController>();
            }

            MenuInputHandler inputHandler = mainMenuUI.AddComponent<MenuInputHandler>();
            SetObjectReference(inputHandler, "flowManager", flowManager);

            BuildFreshMainMenuNewUi(mainMenuUI.transform, flowManager);

            EditorSceneManager.SaveScene(scene, MainMenuNewScenePath);
        }

        private static void RebuildMainMenuNewScenePreservingExistingUi(Scene scene, GameObject playerCarPrefab)
        {
            ClearMainMenuNewRootsExceptPreservedUi(scene);

            CreateRuntimeRoot(false);
            EnsureEventSystem();
            ComposeGarageBackdropForMenu(playerCarPrefab);

            MainMenuController menuController = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menuController == null)
            {
                menuController = new GameObject("MainMenuController").AddComponent<MainMenuController>();
            }

            SetBoolValue(menuController, "allowRuntimeFallbackMenu", false);
            SetBoolValue(menuController, "preferRuntimeStyledMenu", false);
            SetStringValue(menuController, "quickRaceSceneName", "World");

            Canvas canvas = FindPreservedMainMenuCanvas();
            GameObject mainMenuUI = FindPreservedMainMenuUi(canvas);
            if (mainMenuUI == null && canvas != null)
            {
                mainMenuUI = CreatePanelRoot(canvas.transform, MainMenuNewMarkerName);
            }

            MainMenuFlowManager flowManager = mainMenuUI != null
                ? mainMenuUI.GetComponent<MainMenuFlowManager>() ?? mainMenuUI.AddComponent<MainMenuFlowManager>()
                : null;
            flowManager?.Initialize(menuController);
            if (flowManager != null)
            {
                SetObjectReference(flowManager, "mainMenuController", menuController);
            }
            if (mainMenuUI != null)
            {
                if (mainMenuUI.GetComponent<MainMenuNewGraphicsMenuController>() == null)
                {
                    mainMenuUI.AddComponent<MainMenuNewGraphicsMenuController>();
                }
                SlimUiMainMenuBinder legacyBinder = mainMenuUI.GetComponent<SlimUiMainMenuBinder>();
                if (legacyBinder != null)
                {
                    Object.DestroyImmediate(legacyBinder);
                }
            }

            if (canvas == null || mainMenuUI == null)
            {
                canvas = CreateMainMenuCanvas();
                mainMenuUI = CreatePanelRoot(canvas.transform, MainMenuNewMarkerName);
                flowManager = mainMenuUI.GetComponent<MainMenuFlowManager>() ?? mainMenuUI.AddComponent<MainMenuFlowManager>();
                flowManager.Initialize(menuController);
                SetObjectReference(flowManager, "mainMenuController", menuController);
                if (mainMenuUI.GetComponent<MainMenuNewGraphicsMenuController>() == null)
                {
                    mainMenuUI.AddComponent<MainMenuNewGraphicsMenuController>();
                }
                BuildFreshMainMenuNewUi(mainMenuUI.transform, flowManager);
            }
            else
            {
                RebindMainMenuNewUi(flowManager, mainMenuUI.transform);
            }

            MenuInputHandler inputHandler = mainMenuUI.GetComponent<MenuInputHandler>() ?? mainMenuUI.AddComponent<MenuInputHandler>();
            SetObjectReference(inputHandler, "flowManager", flowManager);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BuildFreshMainMenuNewUi(Transform mainMenuUiRoot, MainMenuFlowManager flowManager)
        {
            GameObject splashPanel = CreateStatePanel(mainMenuUiRoot, "SplashPanel");
            BuildSplashPanel(splashPanel.transform);

            GameObject mainMenuPanel = CreateStatePanel(mainMenuUiRoot, "MainMenuPanel");
            Button mainMenuDefault = BuildMainMenuPanel(mainMenuPanel.transform, flowManager);

            GameObject careerPanel = CreateStatePanel(mainMenuUiRoot, "CareerPanel");
            Button careerDefault = BuildCareerPanel(careerPanel.transform, flowManager);

            GameObject quickRacePanel = CreateStatePanel(mainMenuUiRoot, "QuickRacePanel");
            Button quickRaceDefault = BuildQuickRacePanel(quickRacePanel.transform, flowManager);

            GameObject customizePanel = CreateStatePanel(mainMenuUiRoot, "CustomizePanel");
            Button customizeDefault = BuildCustomizePanel(customizePanel.transform, flowManager);

            GameObject optionsPanel = CreateStatePanel(mainMenuUiRoot, "OptionsPanel");
            Button optionsDefault = BuildOptionsPanel(optionsPanel.transform, flowManager);

            flowManager.splashPanel = splashPanel;
            flowManager.mainMenuPanel = mainMenuPanel;
            flowManager.careerPanel = careerPanel;
            flowManager.quickRacePanel = quickRacePanel;
            flowManager.customizePanel = customizePanel;
            flowManager.optionsPanel = optionsPanel;
            flowManager.SetDefaultSelectables(mainMenuDefault, careerDefault, quickRaceDefault, customizeDefault, optionsDefault);
        }

        private static void ClearMainMenuNewRootsExceptPreservedUi(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (ShouldPreserveMainMenuNewRoot(roots[i]))
                {
                    continue;
                }

                Object.DestroyImmediate(roots[i]);
            }
        }

        private static bool ShouldPreserveMainMenuNewRoot(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            return string.Equals(root.name, MainMenuNewCanvasName, StringComparison.Ordinal)
                && root.transform.Find(MainMenuNewMarkerName) != null;
        }

        private static Canvas FindPreservedMainMenuCanvas()
        {
            GameObject root = GameObject.Find(MainMenuNewCanvasName);
            if (root == null || root.transform.Find(MainMenuNewMarkerName) == null)
            {
                return null;
            }

            return root.GetComponent<Canvas>();
        }

        private static GameObject FindPreservedMainMenuUi(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            Transform mainMenuUiTransform = canvas.transform.Find(MainMenuNewMarkerName);
            return mainMenuUiTransform != null ? mainMenuUiTransform.gameObject : null;
        }

        private static void RebindMainMenuNewUi(MainMenuFlowManager flowManager, Transform mainMenuUiRoot)
        {
            flowManager.splashPanel = FindChildGameObject(mainMenuUiRoot, "SplashPanel");
            flowManager.mainMenuPanel = FindChildGameObject(mainMenuUiRoot, "MainMenuPanel");
            flowManager.careerPanel = FindChildGameObject(mainMenuUiRoot, "CareerPanel");
            flowManager.quickRacePanel = FindChildGameObject(mainMenuUiRoot, "QuickRacePanel");
            flowManager.customizePanel = FindChildGameObject(mainMenuUiRoot, "CustomizePanel");
            flowManager.optionsPanel = FindChildGameObject(mainMenuUiRoot, "OptionsPanel");

            flowManager.SetDefaultSelectables(
                FindFirstSelectable(flowManager.mainMenuPanel),
                FindFirstSelectable(flowManager.careerPanel),
                FindFirstSelectable(flowManager.quickRacePanel),
                FindFirstSelectable(flowManager.customizePanel),
                FindFirstSelectable(flowManager.optionsPanel));
        }

        private static GameObject FindChildGameObject(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.gameObject : null;
        }

        private static Selectable FindFirstSelectable(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
            return selectables.Length > 0 ? selectables[0] : null;
        }

        private static bool HasPreservedMainMenuNewUiSceneContent(string sceneText)
        {
            return sceneText.Contains(MainMenuNewCanvasName)
                && sceneText.Contains(MainMenuNewMarkerName);
        }

        private static Canvas CreateMainMenuCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static GameObject CreatePanelRoot(Transform parent, string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            StretchToParent(root.GetComponent<RectTransform>());
            return root;
        }

        private static GameObject CreateStatePanel(Transform parent, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            panel.transform.SetParent(parent, false);
            StretchToParent(panel.GetComponent<RectTransform>());
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            panel.SetActive(false);
            return panel;
        }

        private static void CreateGlobalFrame(Transform parent)
        {
            CreateImage(parent, "TopShade", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 200f), new Color(0f, 0f, 0f, 0.36f));
            CreateImage(parent, "BottomShade", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 180f), new Color(0f, 0f, 0f, 0.28f));
            CreateImage(parent, "LeftShade", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(140f, 0f), new Color(0f, 0f, 0f, 0.18f));
            CreateImage(parent, "RightShade", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(140f, 0f), new Color(0f, 0f, 0f, 0.18f));
        }

        private static void BuildSplashPanel(Transform parent)
        {
            CreateGlobalFrame(parent);
            GameObject ambient = CreateImage(parent, "SplashAmbientBar", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 320f), new Color(0.82f, 0.84f, 0.88f, 0.18f));
            ambient.AddComponent<MenuAmbientAnimator>();

            GameObject titleBand = CreateImage(parent, "SplashTitleBand", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1240f, 72f), new Color(0f, 0f, 0f, 0.56f));
            CreateText(titleBand.transform, "SplashLogo", "FULL THROTTLE", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(520f, 58f), 34f, FontStyles.Bold | FontStyles.Italic, new Color(0.96f, 0.96f, 0.96f, 0.98f), TextAlignmentOptions.Left);
            CreateText(titleBand.transform, "SplashTag", "UNDERGROUND", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(420f, 58f), 30f, FontStyles.Italic, new Color(0.79f, 0.94f, 0.45f, 0.98f), TextAlignmentOptions.Right);

            GameObject logoCard = CreateImage(parent, "SplashLogoCard", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(820f, 240f), new Color(0f, 0f, 0f, 0.44f));
            CreateText(logoCard.transform, "SplashHeadline", "FULL THROTTLE", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(760f, 72f), 54f, FontStyles.Bold | FontStyles.Italic, new Color(0.98f, 0.98f, 0.98f, 0.99f), TextAlignmentOptions.Center);
            CreateText(logoCard.transform, "SplashSubhead", "STREET RACING UNDERGROUND", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 42f), new Vector2(760f, 54f), 24f, FontStyles.Italic, new Color(0.78f, 0.94f, 0.46f, 0.96f), TextAlignmentOptions.Center);

            CreateText(parent, "SplashPrompt", "PRESS ANY KEY", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(480f, 48f), 24f, FontStyles.Bold | FontStyles.Italic, new Color(0.94f, 0.94f, 0.94f, 0.92f), TextAlignmentOptions.Center);
        }

        private static Button BuildMainMenuPanel(Transform parent, MainMenuFlowManager flowManager)
        {
            CreateGlobalFrame(parent);

            GameObject topBand = CreateImage(parent, "MainMenuTopBand", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1240f, 72f), new Color(0f, 0f, 0f, 0.62f));
            CreateText(topBand.transform, "BrandText", "FULL THROTTLE", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(520f, 58f), 34f, FontStyles.Bold | FontStyles.Italic, new Color(0.96f, 0.96f, 0.96f, 0.98f), TextAlignmentOptions.Left);
            CreateText(topBand.transform, "TitleText", "Main Menu", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(420f, 58f), 30f, FontStyles.Italic, new Color(0.79f, 0.94f, 0.45f, 0.98f), TextAlignmentOptions.Right);
            CreateImage(parent, "MainMenuDivider", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1240f, 10f), new Color(0.94f, 0.94f, 0.94f, 0.72f));

            GameObject strip = CreateImage(parent, "CarouselStrip", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(1320f, 150f), new Color(0.82f, 0.82f, 0.82f, 0.22f));
            Button leftArrow = CreateCarouselArrow(strip.transform, "LeftArrow", new Vector2(0f, 0.5f), new Vector2(42f, 0f), "<");
            Button rightArrow = CreateCarouselArrow(strip.transform, "RightArrow", new Vector2(1f, 0.5f), new Vector2(-42f, 0f), ">");

            CreateCarouselItem(strip.transform, "CareerTile", new Vector2(-360f, 0f), "C", "CAREER",
                out RectTransform careerRoot, out Image careerBackground, out Outline careerOutline, out TMP_Text careerIcon, out TMP_Text careerLabel, out Button careerButton);
            CreateCarouselItem(strip.transform, "QuickRaceTile", new Vector2(-120f, 0f), "Q", "QUICK RACE",
                out RectTransform quickRaceRoot, out Image quickRaceBackground, out Outline quickRaceOutline, out TMP_Text quickRaceIcon, out TMP_Text quickRaceLabel, out Button quickRaceButton);
            CreateCarouselItem(strip.transform, "CustomizeTile", new Vector2(120f, 0f), "X", "CUSTOMIZE",
                out RectTransform customizeRoot, out Image customizeBackground, out Outline customizeOutline, out TMP_Text customizeIcon, out TMP_Text customizeLabel, out Button customizeButton);
            CreateCarouselItem(strip.transform, "OptionsTile", new Vector2(360f, 0f), "S", "SETTINGS",
                out RectTransform optionsRoot, out Image optionsBackground, out Outline optionsOutline, out TMP_Text optionsIcon, out TMP_Text optionsLabel, out Button optionsButton);

            TMP_Text caption = CreateText(parent, "SelectionCaption", "Quick Race", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(420f, 54f), 30f, FontStyles.Italic, new Color(0.79f, 0.95f, 0.44f, 1f), TextAlignmentOptions.Center);

            CreateFooterHint(parent, "LeftHint", new Vector2(0.18f, 0f), "MOUSE WHEEL");
            CreateFooterHint(parent, "CenterHint", new Vector2(0.5f, 0f), "ENTER SELECT");
            CreateFooterHint(parent, "RightHint", new Vector2(0.82f, 0f), "ARROWS CYCLE");

            MainMenuStateCarouselController carousel = parent.gameObject.AddComponent<MainMenuStateCarouselController>();
            carousel.Configure(
                flowManager,
                leftArrow,
                rightArrow,
                caption,
                careerRoot,
                careerBackground,
                careerOutline,
                careerIcon,
                careerLabel,
                careerButton,
                quickRaceRoot,
                quickRaceBackground,
                quickRaceOutline,
                quickRaceIcon,
                quickRaceLabel,
                quickRaceButton,
                customizeRoot,
                customizeBackground,
                customizeOutline,
                customizeIcon,
                customizeLabel,
                customizeButton,
                optionsRoot,
                optionsBackground,
                optionsOutline,
                optionsIcon,
                optionsLabel,
                optionsButton);

            return quickRaceButton;
        }

        private static Button BuildCareerPanel(Transform parent, MainMenuFlowManager flowManager)
        {
            BuildPanelChrome(parent, "Career", "CONTINUE OR MANAGE PROGRESSION");

            GameObject strip = CreateSubmenuStrip(parent, "CareerActionStrip");
            Button continueButton = CreateSubmenuTile(strip.transform, "Continue", new Vector2(-315f, 0f), new Vector2(210f, 92f), flowManager.ContinueCareer);
            Button newGameButton = CreateSubmenuTile(strip.transform, "New Game", new Vector2(-105f, 0f), new Vector2(210f, 92f), flowManager.StartCareerNewGame);
            Button loadGameButton = CreateSubmenuTile(strip.transform, "Load Game", new Vector2(105f, 0f), new Vector2(210f, 92f), flowManager.LoadCareerGame);
            Button backButton = CreateSubmenuTile(strip.transform, "Back", new Vector2(315f, 0f), new Vector2(210f, 92f), flowManager.Back);

            LinkHorizontalNavigation(continueButton, newGameButton, loadGameButton, backButton);
            CreateSectionCaption(parent, "CareerCaption", "Career", -240f);
            CreateSectionInfo(parent, "CareerInfo", "Continue your free roam progression, begin a new save, or load your current street-racing career.", -286f);
            CreateSubmenuHints(parent);
            return continueButton;
        }

        private static Button BuildQuickRacePanel(Transform parent, MainMenuFlowManager flowManager)
        {
            BuildPanelChrome(parent, "Quick Race", "SELF-CONTAINED RACE SETUP");

            GameObject strip = CreateSubmenuStrip(parent, "QuickRaceActionStrip");
            Button startButton = CreateSubmenuTile(strip.transform, "Start", new Vector2(-120f, 0f), new Vector2(240f, 92f), flowManager.StartQuickRace);
            Button backButton = CreateSubmenuTile(strip.transform, "Back", new Vector2(120f, 0f), new Vector2(240f, 92f), flowManager.Back);

            LinkHorizontalNavigation(startButton, backButton);
            CreateSectionCaption(parent, "QuickRaceCaption", "Quick Race", -240f);
            CreateSectionInfo(parent, "QuickRaceInfo", "Launch a self-contained event using unlocked cars and race content without changing career progress.", -286f);
            CreateSubmenuHints(parent);
            return startButton;
        }

        private static Button BuildCustomizePanel(Transform parent, MainMenuFlowManager flowManager)
        {
            BuildPanelChrome(parent, "Customize", "CAR PREVIEW AND VISUAL HOOKS");

            GameObject strip = CreateSubmenuStrip(parent, "CustomizeActionStrip");
            Button visualHooks = CreateSubmenuTile(strip.transform, "Visual Hooks", new Vector2(-240f, 0f), new Vector2(220f, 92f), flowManager.OpenCustomizeVisualHooks);
            Button previewButton = CreateSubmenuTile(strip.transform, "Car Preview", new Vector2(0f, 0f), new Vector2(220f, 92f), flowManager.OpenCustomizePreview);
            Button backButton = CreateSubmenuTile(strip.transform, "Back", new Vector2(240f, 0f), new Vector2(220f, 92f), flowManager.Back);

            LinkHorizontalNavigation(visualHooks, previewButton, backButton);
            CreateSectionCaption(parent, "CustomizeCaption", "Customize", -240f);
            CreateSectionInfo(parent, "CustomizeInfo", "Preview the car and expand into body kits, paint, vinyls, stance, and lighting customization hooks.", -286f);
            CreateSubmenuHints(parent);
            return visualHooks;
        }

        private static Button BuildOptionsPanel(Transform parent, MainMenuFlowManager flowManager)
        {
            BuildPanelChrome(parent, "Options", "AUDIO CONTROLS GRAPHICS");

            GameObject strip = CreateSubmenuStrip(parent, "OptionsActionStrip");
            Button audio = CreateSubmenuTile(strip.transform, "Audio", new Vector2(-315f, 0f), new Vector2(210f, 92f), flowManager.OpenAudioOptions);
            Button controls = CreateSubmenuTile(strip.transform, "Controls", new Vector2(-105f, 0f), new Vector2(210f, 92f), flowManager.OpenControlsOptions);
            Button graphics = CreateSubmenuTile(strip.transform, "Graphics", new Vector2(105f, 0f), new Vector2(210f, 92f), flowManager.OpenGraphicsOptions);
            Button back = CreateSubmenuTile(strip.transform, "Back", new Vector2(315f, 0f), new Vector2(210f, 92f), flowManager.Back);

            LinkHorizontalNavigation(audio, controls, graphics, back);
            CreateSectionCaption(parent, "OptionsCaption", "Settings", -240f);
            CreateSectionInfo(parent, "OptionsInfo", "Adjust audio, controls, and graphics with the same pacing and interaction style as the rest of the menu.", -286f);
            CreateSubmenuHints(parent);
            return audio;
        }

        private static void BuildPanelChrome(Transform parent, string panelTitle, string panelSubtitle)
        {
            CreateGlobalFrame(parent);
            GameObject topBand = CreateImage(parent, panelTitle + "TopBand", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1240f, 72f), new Color(0f, 0f, 0f, 0.56f));
            CreateText(topBand.transform, panelTitle + "Brand", "FULL THROTTLE", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(520f, 58f), 30f, FontStyles.Bold | FontStyles.Italic, new Color(0.96f, 0.96f, 0.96f, 0.98f), TextAlignmentOptions.Left);
            CreateText(topBand.transform, panelTitle + "Title", panelTitle, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(420f, 58f), 30f, FontStyles.Italic, new Color(0.79f, 0.94f, 0.45f, 0.98f), TextAlignmentOptions.Right);
            CreateImage(parent, panelTitle + "Divider", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1240f, 10f), new Color(0.94f, 0.94f, 0.94f, 0.7f));
            CreateText(parent, panelTitle + "Subtitle", panelSubtitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(800f, 42f), 18f, FontStyles.Italic, new Color(0.92f, 0.92f, 0.92f, 0.68f), TextAlignmentOptions.Center);
        }

        private static GameObject CreateVerticalList(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment)
        {
            GameObject list = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(parent, false);
            RectTransform rect = list.GetComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);

            VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.spacing = 14f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return list;
        }

        private static void CreateInfoCard(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, string body)
        {
            GameObject card = CreateImage(parent, name, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta, new Color(0f, 0f, 0f, 0.34f));
            CreateText(card.transform, name + "Body", body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sizeDelta.x - 60f, sizeDelta.y - 50f), 24f, FontStyles.Italic, new Color(0.95f, 0.95f, 0.95f, 0.94f), TextAlignmentOptions.Center);
        }

        private static Button CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 78f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.05f, 0.07f, 0.1f, 0.84f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, 2f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.49f, 0.66f, 0.27f, 0.92f);
            colors.pressedColor = new Color(0.39f, 0.54f, 0.21f, 0.98f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            button.colors = colors;

            if (action != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, action);
            }

            CreateText(buttonObject.transform, label + "Label", label.ToUpperInvariant(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 44f), 24f, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            return button;
        }

        private static GameObject CreateSubmenuStrip(Transform parent, string name)
        {
            return CreateImage(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(1320f, 150f), new Color(0.82f, 0.82f, 0.82f, 0.2f));
        }

        private static Button CreateSubmenuTile(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject tile = new GameObject(label + "Tile", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            tile.transform.SetParent(parent, false);

            RectTransform rect = tile.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);

            Image image = tile.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);

            Outline outline = tile.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, 2f);

            Button button = tile.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.72f, 0.9f, 0.38f, 0.82f);
            colors.pressedColor = new Color(0.56f, 0.76f, 0.28f, 0.92f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            if (action != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, action);
            }

            CreateText(tile.transform, label + "Label", label.ToUpperInvariant(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(18f, 20f), 22f, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            return button;
        }

        private static void CreateSectionCaption(Transform parent, string name, string text, float y)
        {
            CreateText(parent, name, text, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(420f, 54f), 30f, FontStyles.Italic, new Color(0.79f, 0.95f, 0.44f, 1f), TextAlignmentOptions.Center);
        }

        private static void CreateSectionInfo(Transform parent, string name, string text, float y)
        {
            CreateText(parent, name, text, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(920f, 42f), 16f, FontStyles.Italic, new Color(0.94f, 0.94f, 0.94f, 0.82f), TextAlignmentOptions.Center);
        }

        private static Button CreateCarouselArrow(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, string glyph)
        {
            GameObject arrowObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            arrowObject.transform.SetParent(parent, false);

            RectTransform rect = arrowObject.GetComponent<RectTransform>();
            ConfigureRect(rect, anchor, anchor, new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(64f, 64f));

            Image image = arrowObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);

            Outline outline = arrowObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, 2f);

            Button button = arrowObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.18f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.26f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            CreateText(arrowObject.transform, name + "Glyph", glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f), 32f, FontStyles.Bold, new Color(1f, 1f, 1f, 0.84f), TextAlignmentOptions.Center);
            return button;
        }

        private static void CreateCarouselItem(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            string iconGlyph,
            string labelTextValue,
            out RectTransform root,
            out Image background,
            out Outline outline,
            out TMP_Text icon,
            out TMP_Text label,
            out Button button)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            item.transform.SetParent(parent, false);

            root = item.GetComponent<RectTransform>();
            ConfigureRect(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(190f, 98f));

            background = item.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.08f);

            outline = item.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, 2f);

            button = item.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            icon = CreateText(item.transform, name + "Icon", iconGlyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(80f, 54f), 44f, FontStyles.Bold, new Color(1f, 1f, 1f, 0.82f), TextAlignmentOptions.Center);
            label = CreateText(item.transform, name + "Label", labelTextValue, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(160f, 24f), 16f, FontStyles.Bold | FontStyles.Italic, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Center);
        }

        private static void CreateFooterHint(Transform parent, string name, Vector2 anchor, string text)
        {
            GameObject hint = CreateImage(parent, name, anchor, anchor, new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(220f, 34f), new Color(0f, 0f, 0f, 0.56f));
            Outline outline = hint.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, 2f);
            CreateText(hint.transform, name + "Text", text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 26f), 14f, FontStyles.Italic, new Color(0.94f, 0.94f, 0.94f, 0.9f), TextAlignmentOptions.Center);
        }

        private static void CreateSubmenuHints(Transform parent)
        {
            CreateFooterHint(parent, "BackHint", new Vector2(0.18f, 0f), "ESC BACK");
            CreateFooterHint(parent, "SelectHint", new Vector2(0.5f, 0f), "ENTER SELECT");
            CreateFooterHint(parent, "NavHint", new Vector2(0.82f, 0f), "WASD NAVIGATE");
        }

        private static GameObject CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            imageObject.GetComponent<Image>().color = color;
            return imageObject;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, FontStyles fontStyle, Color color, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = fontStyle;
            tmp.color = color;
            tmp.alignment = alignment;
            return tmp;
        }

        private static void LinkVerticalNavigation(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    continue;
                }

                Navigation navigation = buttons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = i > 0 ? buttons[i - 1] : buttons[buttons.Length - 1];
                navigation.selectOnDown = i < buttons.Length - 1 ? buttons[i + 1] : buttons[0];
                navigation.selectOnLeft = buttons[i];
                navigation.selectOnRight = buttons[i];
                buttons[i].navigation = navigation;
            }
        }

        private static void LinkHorizontalNavigation(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    continue;
                }

                Navigation navigation = buttons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = i > 0 ? buttons[i - 1] : buttons[buttons.Length - 1];
                navigation.selectOnRight = i < buttons.Length - 1 ? buttons[i + 1] : buttons[0];
                navigation.selectOnUp = buttons[i];
                navigation.selectOnDown = buttons[i];
                buttons[i].navigation = navigation;
            }
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

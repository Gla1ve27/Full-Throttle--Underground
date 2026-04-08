using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SlimUI.ModernMenu;

namespace Underground.UI
{
    public class SlimUiMainMenuBinder : MonoBehaviour
    {
        [SerializeField] private MainMenuController menuController;

        private UIMenuManager slimMenu;
        private GameSettingsManager settingsManager;
        private Canvas rootCanvas;
        private Camera menuCamera;
        private GameObject settingsOverlay;
        private Action refreshSettingsView;
        private bool isInitialized;

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (isInitialized)
            {
                return;
            }

            menuController ??= FindFirstObjectByType<MainMenuController>();
            settingsManager ??= FindFirstObjectByType<GameSettingsManager>();
            slimMenu = GetComponentInChildren<UIMenuManager>(true);
            menuCamera = ResolveMenuCamera();
            rootCanvas = ResolveSettingsCanvas();

            if (menuController == null || settingsManager == null || rootCanvas == null)
            {
                return;
            }

            NormalizeMenuCameraRig();

            if (slimMenu != null)
            {
                NormalizeSlimUiInstance();
                DisableDemoSettingsScripts();
                ConfigureMenuState(slimMenu);
                ConfigurePrimaryButtons(slimMenu);
            }

            ConfigureAudioSources();
            BuildSettingsOverlay();
            isInitialized = settingsOverlay != null;
        }

        private void NormalizeSlimUiInstance()
        {
            transform.localScale = Vector3.one;

            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                ConfigureResponsiveCanvas(canvases[i]);
            }

            RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                rectTransforms[i].localScale = Vector3.one;
                if (rectTransforms[i].transform == transform)
                {
                    rectTransforms[i].localRotation = Quaternion.identity;
                }
            }
        }

        private void DisableDemoSettingsScripts()
        {
            UISettingsManager importedSettings = GetComponentInChildren<UISettingsManager>(true);
            if (importedSettings != null)
            {
                importedSettings.enabled = false;
            }

            foreach (CheckMusicVolume musicVolume in GetComponentsInChildren<CheckMusicVolume>(true))
            {
                musicVolume.enabled = false;
            }

            foreach (CheckSFXVolume sfxVolume in GetComponentsInChildren<CheckSFXVolume>(true))
            {
                sfxVolume.enabled = false;
            }
        }

        private void ConfigureResponsiveCanvas(Canvas canvas)
        {
            bool usingGarageBackdrop = IsUsingGarageBackdrop();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            scaler.referenceResolution = settingsManager != null ? settingsManager.ReferenceResolution : new Vector2(1920f, 1080f);

            if (usingGarageBackdrop)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.planeDistance = 100f;
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 10);
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.worldCamera = null;
            }
            else if (menuCamera != null)
            {
                canvas.worldCamera = menuCamera;
            }

            if (canvas.GetComponent<ResponsiveCanvasController>() == null)
            {
                canvas.gameObject.AddComponent<ResponsiveCanvasController>();
            }

            RectTransform rectTransform = canvas.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
                if (usingGarageBackdrop && canvas.isRootCanvas)
                {
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                }
            }
        }

        private void ConfigureMenuState(UIMenuManager menu)
        {
            if (menu.mainMenu != null) menu.mainMenu.SetActive(true);
            if (menu.firstMenu != null) menu.firstMenu.SetActive(true);
            if (menu.playMenu != null) menu.playMenu.SetActive(false);
            if (menu.exitMenu != null) menu.exitMenu.SetActive(false);
            if (menu.extrasMenu != null) menu.extrasMenu.SetActive(false);
            if (menu.loadingMenu != null) menu.loadingMenu.SetActive(false);
        }

        private void ConfigurePrimaryButtons(UIMenuManager menu)
        {
            GameObject menuRoot = menu.firstMenu != null ? menu.firstMenu : menu.mainMenu;
            if (menuRoot == null)
            {
                return;
            }

            List<Button> buttons = menuRoot.GetComponentsInChildren<Button>(true).ToList();
            if (buttons.Count == 0)
            {
                return;
            }

            Button continueButton = FindButton(buttons, "play", "campaign", "continue", "resume");
            Button settingsButton = FindButton(buttons, "setting", "option", "extra");
            Button quitButton = FindButton(buttons, "exit", "quit");

            if (continueButton == null)
            {
                continueButton = buttons[0];
            }

            buttons.Remove(continueButton);

            if (settingsButton != null)
            {
                buttons.Remove(settingsButton);
            }

            if (quitButton != null)
            {
                buttons.Remove(quitButton);
            }

            Button newGameButton = FindButton(buttons, "new", "start");
            if (newGameButton == null && buttons.Count > 0)
            {
                newGameButton = buttons[0];
                buttons.RemoveAt(0);
            }

            if (settingsButton == null && buttons.Count > 0)
            {
                settingsButton = buttons[0];
                buttons.RemoveAt(0);
            }

            if (quitButton == null && buttons.Count > 0)
            {
                quitButton = buttons[0];
                buttons.RemoveAt(0);
            }

            ConfigureButton(continueButton, "Continue", menuController.ContinueGame);
            if (newGameButton != null) ConfigureButton(newGameButton, "New Game", menuController.StartNewGame);
            if (settingsButton != null) ConfigureButton(settingsButton, "Settings", OpenSettings);
            if (quitButton != null) ConfigureButton(quitButton, "Quit", menuController.QuitGame);
        }

        private void ConfigureAudioSources()
        {
            if (settingsManager == null)
            {
                return;
            }

            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                string audioId = $"{source.name} {(source.clip != null ? source.clip.name : string.Empty)}".ToLowerInvariant();
                bool isMusic = source.loop || audioId.Contains("music") || audioId.Contains("theme");
                settingsManager.RouteAudioSource(source, isMusic ? "Music" : "SFX");
            }
        }

        private void BuildSettingsOverlay()
        {
            if (rootCanvas == null || settingsManager == null)
            {
                return;
            }

            Transform existing = rootCanvas.transform.Find("UndergroundSettingsOverlay");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            refreshSettingsView = null;
            settingsOverlay = CreateUiObject("UndergroundSettingsOverlay", rootCanvas.transform, out RectTransform overlayRect, typeof(Image));
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            settingsOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.26f);
            settingsOverlay.SetActive(false);

            GameObject panel = CreateUiObject("Panel", settingsOverlay.transform, out RectTransform panelRect, typeof(Image), typeof(Outline));
            panelRect.anchorMin = new Vector2(0.46f, 0.5f);
            panelRect.anchorMax = new Vector2(0.46f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 470f);
            panel.GetComponent<Image>().color = new Color(0.42f, 0.44f, 0.46f, 0.82f);
            Outline panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 1f, 1f, 0.28f);
            panelOutline.effectDistance = new Vector2(1f, -1f);

            CreateHeader(panel.transform);
            CreateScrollContent(panel.transform, out Transform contentRoot);
            CreateFooter(panel.transform);

            CreateDisplaySection(contentRoot);
            CreateVehicleGraphicsSection(contentRoot);
            CreatePostEffectsSection(contentRoot);
            CreateGraphicsExtrasSection(contentRoot);

            refreshSettingsView?.Invoke();
        }

        private void CreateHeader(Transform parent)
        {
            TMP_Text header = CreateText("Header", parent, "Display", 26f, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0f, 1f);
            headerRect.anchoredPosition = new Vector2(18f, -14f);
            headerRect.sizeDelta = new Vector2(-36f, 32f);
        }

        private void CreateScrollContent(Transform parent, out Transform contentRoot)
        {
            GameObject scrollRoot = CreateUiObject("ScrollView", parent, out RectTransform scrollRectTransform, typeof(Image), typeof(Mask), typeof(ScrollRect));
            scrollRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            scrollRoot.GetComponent<Mask>().showMaskGraphic = false;
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(18f, 64f);
            scrollRectTransform.offsetMax = new Vector2(-34f, -56f);

            GameObject viewport = CreateUiObject("Viewport", scrollRoot.transform, out RectTransform viewportRect, typeof(Image), typeof(Mask));
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = CreateUiObject("Content", viewport.transform, out RectTransform contentRect, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 28f;

            GameObject scrollbarObject = CreateUiObject("Scrollbar", scrollRoot.transform, out RectTransform scrollbarRect, typeof(Image), typeof(Scrollbar));
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 1f);
            scrollbarRect.sizeDelta = new Vector2(14f, 0f);
            scrollbarRect.offsetMin = new Vector2(-14f, 6f);
            scrollbarRect.offsetMax = new Vector2(0f, -6f);
            scrollbarObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);

            GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarObject.transform, out RectTransform slidingAreaRect);
            slidingAreaRect.anchorMin = Vector2.zero;
            slidingAreaRect.anchorMax = Vector2.one;
            slidingAreaRect.offsetMin = new Vector2(2f, 2f);
            slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

            GameObject handle = CreateUiObject("Handle", slidingArea.transform, out RectTransform handleRect, typeof(Image));
            handleRect.anchorMin = new Vector2(0f, 1f);
            handleRect.anchorMax = new Vector2(1f, 1f);
            handleRect.pivot = new Vector2(0.5f, 1f);
            handleRect.sizeDelta = new Vector2(0f, 48f);
            handle.GetComponent<Image>().color = new Color(0.78f, 0.8f, 0.82f, 0.92f);

            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 8f;

            contentRoot = content.transform;
        }

        private void CreateFooter(Transform parent)
        {
            GameObject footer = CreateUiObject("Footer", parent, out RectTransform footerRect, typeof(HorizontalLayoutGroup));
            footerRect.anchorMin = new Vector2(1f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(1f, 0f);
            footerRect.anchoredPosition = new Vector2(-18f, 14f);
            footerRect.sizeDelta = new Vector2(220f, 40f);

            HorizontalLayoutGroup layout = footer.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateActionButton(footer.transform, "Apply", () => settingsManager.RefreshAll(), new Color(0.28f, 0.48f, 0.24f, 0.95f));
            CreateActionButton(footer.transform, "Back", CloseSettings, new Color(0.28f, 0.3f, 0.34f, 0.95f));
        }

        private void CreateDisplaySection(Transform parent)
        {
            Transform section = CreateSection(parent, "Display");
            CreateCycleRow(section, "Resolution", () => settingsManager.CycleResolution(-1), () => settingsManager.CycleResolution(1), () => settingsManager.GetCurrentResolutionLabel());
            CreateCycleRow(section, "Level Of Detail", () => settingsManager.CycleWorldDetail(-1), () => settingsManager.CycleWorldDetail(1), () => settingsManager.GetWorldDetailLabel());
            CreateToggleRow(section, "Fullscreen", settingsManager.ToggleFullscreen, () => settingsManager.Fullscreen ? "On" : "Off");
            CreateToggleRow(section, "VSync", settingsManager.ToggleVSync, () => settingsManager.VSyncEnabled ? "On" : "Off");
            CreateCycleRow(section, "Quality", () => settingsManager.CycleQualityLevel(-1), () => settingsManager.CycleQualityLevel(1), () => settingsManager.GetQualityLabel());
            CreateCycleRow(section, "Shadows", () => settingsManager.CycleShadowQuality(-1), () => settingsManager.CycleShadowQuality(1), () => settingsManager.GetShadowQualityLabel());
            CreateCycleRow(section, "Textures", () => settingsManager.CycleTextureQuality(-1), () => settingsManager.CycleTextureQuality(1), () => settingsManager.GetTextureQualityLabel());
        }

        private void CreateVehicleGraphicsSection(Transform parent)
        {
            Transform section = CreateSection(parent, "Customize Display Settings");
            CreateCycleRow(section, "Car Reflection Update Rate", () => settingsManager.CycleCarReflectionUpdateRate(-1), () => settingsManager.CycleCarReflectionUpdateRate(1), () => settingsManager.GetCarReflectionUpdateRateLabel());
            CreateCycleRow(section, "Car Reflection Detail", () => settingsManager.CycleCarReflectionDetail(-1), () => settingsManager.CycleCarReflectionDetail(1), () => settingsManager.GetCarReflectionDetailLabel());
            CreateCycleRow(section, "Car Shadow / Neon", () => settingsManager.CycleCarShadowDetail(-1), () => settingsManager.CycleCarShadowDetail(1), () => settingsManager.GetCarShadowDetailLabel());
            CreateToggleRow(section, "Car Headlight", settingsManager.ToggleCarHeadlights, () => settingsManager.CarHeadlightsEnabled ? "On" : "Off");
            CreateCycleRow(section, "Car Geometry Detail", () => settingsManager.CycleCarGeometryDetail(-1), () => settingsManager.CycleCarGeometryDetail(1), () => settingsManager.GetCarGeometryDetailLabel());
            CreateToggleRow(section, "Crowds", settingsManager.ToggleCrowds, () => settingsManager.CrowdsEnabled ? "On" : "Off");
            CreateCycleRow(section, "World Detail", () => settingsManager.CycleWorldDetail(-1), () => settingsManager.CycleWorldDetail(1), () => settingsManager.GetWorldDetailLabel());
            CreateCycleRow(section, "Road Reflection Detail", () => settingsManager.CycleRoadReflectionDetail(-1), () => settingsManager.CycleRoadReflectionDetail(1), () => settingsManager.GetRoadReflectionDetailLabel());
        }

        private void CreatePostEffectsSection(Transform parent)
        {
            Transform section = CreateSection(parent, "Post Effects");
            CreateToggleRow(section, "Light Trails", settingsManager.ToggleLightTrails, () => settingsManager.LightTrailsEnabled ? "On" : "Off");
            CreateToggleRow(section, "Light Glow", settingsManager.ToggleLightGlow, () => settingsManager.LightGlowEnabled ? "On" : "Off");
            CreateToggleRow(section, "Particle System", settingsManager.ToggleParticleSystems, () => settingsManager.ParticleSystemsEnabled ? "On" : "Off");
            CreateToggleRow(section, "Motion Blur", settingsManager.ToggleMotionBlur, () => settingsManager.MotionBlurEnabled ? "On" : "Off");
            CreateToggleRow(section, "Fog", settingsManager.ToggleFog, () => settingsManager.FogEnabled ? "On" : "Off");
            CreateToggleRow(section, "Depth Of Field", settingsManager.ToggleDepthOfField, () => settingsManager.DepthOfFieldEnabled ? "On" : "Off");
            CreateCycleRow(section, "Full Screen Anti-Aliasing", () => settingsManager.CycleFullScreenAntiAliasing(-1), () => settingsManager.CycleFullScreenAntiAliasing(1), () => settingsManager.GetFullScreenAntiAliasingLabel());
            CreateToggleRow(section, "Tinting", settingsManager.ToggleTinting, () => settingsManager.TintingEnabled ? "On" : "Off");
        }

        private void CreateGraphicsExtrasSection(Transform parent)
        {
            Transform section = CreateSection(parent, "Advanced Display Settings");
            CreateToggleRow(section, "Horizon Fog", settingsManager.ToggleHorizonFog, () => settingsManager.HorizonFogEnabled ? "On" : "Off");
            CreateToggleRow(section, "Over Bright", settingsManager.ToggleOverBright, () => settingsManager.OverBrightEnabled ? "On" : "Off");
            CreateToggleRow(section, "Advanced Contrast", settingsManager.ToggleAdvancedContrast, () => settingsManager.AdvancedContrastEnabled ? "On" : "Off");
            CreateToggleRow(section, "Rain Splatter", settingsManager.ToggleRainSplatter, () => settingsManager.RainSplatterEnabled ? "On" : "Off");
            CreateCycleRow(section, "Texture Filtering", () => settingsManager.CycleTextureFiltering(-1), () => settingsManager.CycleTextureFiltering(1), () => settingsManager.GetTextureFilteringLabel());
        }

        private void CreateAudioSection(Transform parent)
        {
            Transform section = CreateSection(parent, "Audio");
            CreateSliderRow(section, "Master Volume", settingsManager.MasterVolume, value => settingsManager.SetMasterVolume(value), () => Percent(settingsManager.MasterVolume));
            CreateSliderRow(section, "Music Volume", settingsManager.MusicVolume, value => settingsManager.SetMusicVolume(value), () => Percent(settingsManager.MusicVolume));
            CreateSliderRow(section, "SFX Volume", settingsManager.SfxVolume, value => settingsManager.SetSfxVolume(value), () => Percent(settingsManager.SfxVolume));
        }

        private void CreateCameraSection(Transform parent)
        {
            Transform section = CreateSection(parent, "Camera");
            CreateSliderRow(section, "Field Of View", Mathf.InverseLerp(55f, 85f, settingsManager.CameraFieldOfView), value => settingsManager.SetCameraFieldOfView(Mathf.Lerp(55f, 85f, value)), () => $"{Mathf.RoundToInt(settingsManager.CameraFieldOfView)}");
            CreateToggleRow(section, "Camera Effects", settingsManager.ToggleCameraEffects, () => settingsManager.CameraEffectsEnabled ? "On" : "Off");
        }

        private void CreateControlsSection(Transform parent)
        {
            Transform section = CreateSection(parent, "Controls");
            CreateSliderRow(section, "Steering Response", Mathf.InverseLerp(0.5f, 2f, settingsManager.SteeringSensitivity), value => settingsManager.SetSteeringSensitivity(Mathf.Lerp(0.5f, 2f, value)), () => settingsManager.SteeringSensitivity.ToString("0.00"));
            CreateSliderRow(section, "Pedal Response", Mathf.InverseLerp(0.5f, 2f, settingsManager.PedalSensitivity), value => settingsManager.SetPedalSensitivity(Mathf.Lerp(0.5f, 2f, value)), () => settingsManager.PedalSensitivity.ToString("0.00"));
            CreateSliderRow(section, "Reverse Double-Tap", Mathf.InverseLerp(0.1f, 0.6f, settingsManager.ReverseDoubleTapWindow), value => settingsManager.SetReverseDoubleTapWindow(Mathf.Lerp(0.1f, 0.6f, value)), () => $"{settingsManager.ReverseDoubleTapWindow:0.00}s");
        }

        private void CreateGameplaySection(Transform parent)
        {
            Transform section = CreateSection(parent, "Gameplay");
            CreateToggleRow(section, "HUD", settingsManager.ToggleHud, () => settingsManager.ShowHud ? "On" : "Off");
        }

        private void CreateHelpSection(Transform parent)
        {
            Transform section = CreateSection(parent, "Controls Layout");
            TMP_Text help = CreateText("HelpText", section, "Drive: W/A/S/D or Arrow Keys   Handbrake: Space   Reset: R   Reverse: double-tap S or Down Arrow", 20f, FontStyles.Normal, TextAlignmentOptions.Left);
            LayoutElement element = help.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 38f;
        }

        private Transform CreateSection(Transform parent, string title)
        {
            GameObject section = CreateUiObject(title, parent, out _, typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            section.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            section.GetComponent<LayoutElement>().minHeight = 80f;

            VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = section.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text header = CreateText($"{title}_Header", section.transform, title, 20f, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.Left);
            LayoutElement headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 28f;
            return section.transform;
        }

        private void CreateCycleRow(Transform parent, string label, Action previousAction, Action nextAction, Func<string> valueFactory)
        {
            GameObject row = CreateRow(parent, label, out TMP_Text valueText);
            CreateSmallButton(row.transform, "<", () =>
            {
                previousAction?.Invoke();
                refreshSettingsView?.Invoke();
            });

            CreateSmallButton(row.transform, ">", () =>
            {
                nextAction?.Invoke();
                refreshSettingsView?.Invoke();
            });

            refreshSettingsView += () => valueText.text = valueFactory();
        }

        private void CreateToggleRow(Transform parent, string label, Action toggleAction, Func<string> valueFactory)
        {
            GameObject row = CreateRow(parent, label, out TMP_Text valueText);
            CreateActionButton(row.transform, "Toggle", () =>
            {
                toggleAction?.Invoke();
                refreshSettingsView?.Invoke();
            }, new Color(0.19f, 0.28f, 0.36f, 0.95f), 132f);

            refreshSettingsView += () => valueText.text = valueFactory();
        }

        private void CreateSliderRow(Transform parent, string label, float normalizedValue, Action<float> onValueChanged, Func<string> valueFactory)
        {
            GameObject row = CreateUiObject($"{label}_Row", parent, out _, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            row.GetComponent<LayoutElement>().preferredHeight = 82f;

            VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject headerRow = CreateUiObject($"{label}_Header", row.transform, out _, typeof(HorizontalLayoutGroup));
            HorizontalLayoutGroup headerLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 16f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = false;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            TMP_Text labelText = CreateText($"{label}_Label", headerRow.transform, label, 20f, FontStyles.Normal, TextAlignmentOptions.Left);
            LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 340f;

            TMP_Text valueText = CreateText($"{label}_Value", headerRow.transform, string.Empty, 20f, FontStyles.Bold, TextAlignmentOptions.Right);
            LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1f;

            GameObject sliderObject = CreateUiObject($"{label}_Slider", row.transform, out _, typeof(Slider));
            Slider slider = sliderObject.GetComponent<Slider>();
            BuildSlider(slider);
            slider.SetValueWithoutNotify(normalizedValue);
            slider.onValueChanged.AddListener(value =>
            {
                onValueChanged?.Invoke(value);
                refreshSettingsView?.Invoke();
            });

            refreshSettingsView += () => valueText.text = valueFactory();
        }

        private GameObject CreateRow(Transform parent, string label, out TMP_Text valueText)
        {
            GameObject row = CreateUiObject($"{label}_Row", parent, out _, typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.26f);
            row.GetComponent<LayoutElement>().preferredHeight = 38f;

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 4, 4);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TMP_Text labelText = CreateText($"{label}_Label", row.transform, label, 16f, FontStyles.Italic, TextAlignmentOptions.Left);
            LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 320f;

            valueText = CreateText($"{label}_Value", row.transform, string.Empty, 16f, FontStyles.Bold, TextAlignmentOptions.Right);
            LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1f;
            valueLayout.minWidth = 140f;
            return row;
        }

        private static void BuildSlider(Slider slider)
        {
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            GameObject background = CreateUiObject("Background", slider.transform, out RectTransform backgroundRect, typeof(Image));
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 10f);
            background.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.23f, 1f);

            GameObject fillArea = CreateUiObject("Fill Area", slider.transform, out RectTransform fillAreaRect);
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(10f, 0f);
            fillAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject fill = CreateUiObject("Fill", fillArea.transform, out RectTransform fillRect, typeof(Image));
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, 10f);
            fill.GetComponent<Image>().color = new Color(0.26f, 0.63f, 0.77f, 1f);

            GameObject handleSlideArea = CreateUiObject("Handle Slide Area", slider.transform, out RectTransform handleAreaRect);
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = CreateUiObject("Handle", handleSlideArea.transform, out RectTransform handleRect, typeof(Image));
            handleRect.sizeDelta = new Vector2(18f, 24f);
            handle.GetComponent<Image>().color = new Color(0.93f, 0.95f, 0.98f, 1f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
        }

        private GameObject CreateActionButton(Transform parent, string label, Action action, Color color, float width = 156f)
        {
            GameObject buttonObject = CreateUiObject(label, parent, out _, typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.GetComponent<Image>().color = color;
            buttonObject.GetComponent<LayoutElement>().preferredWidth = width;
            buttonObject.GetComponent<LayoutElement>().preferredHeight = 44f;

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => action?.Invoke());

            TMP_Text text = CreateText("Label", buttonObject.transform, label, 20f, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return buttonObject;
        }

        private void CreateSmallButton(Transform parent, string label, Action action)
        {
            CreateActionButton(parent, label, action, new Color(0.19f, 0.28f, 0.36f, 0.95f), 56f);
        }

        public void OpenSettings()
        {
            EnsureInitialized();
            if (settingsOverlay == null)
            {
                return;
            }

            SetPrimaryMenuVisibility(false);
            AnimateSlimUiCamera(toSettings: true);

            settingsOverlay.transform.SetAsLastSibling();
            settingsOverlay.SetActive(true);
            refreshSettingsView?.Invoke();
        }

        public void CloseSettings()
        {
            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(false);
            }

            AnimateSlimUiCamera(toSettings: false);
            SetPrimaryMenuVisibility(true);
        }

        private void AnimateSlimUiCamera(bool toSettings)
        {
            if (slimMenu == null)
            {
                return;
            }

            Animator animator = slimMenu.GetComponent<Animator>();
            if (animator == null || !animator.enabled)
            {
                return;
            }

            if (toSettings)
            {
                slimMenu.Position2();
            }
            else
            {
                slimMenu.Position1();
            }
        }

        private void SetPrimaryMenuVisibility(bool visible)
        {
            if (slimMenu == null)
            {
                return;
            }

            if (slimMenu.firstMenu != null)
            {
                slimMenu.firstMenu.SetActive(visible);
            }

            if (slimMenu.playMenu != null)
            {
                slimMenu.playMenu.SetActive(false);
            }

            if (slimMenu.exitMenu != null)
            {
                slimMenu.exitMenu.SetActive(false);
            }

            if (slimMenu.extrasMenu != null)
            {
                slimMenu.extrasMenu.SetActive(false);
            }

            if (slimMenu.mainMenu != null)
            {
                slimMenu.mainMenu.SetActive(true);
            }
        }

        private static Button FindButton(IEnumerable<Button> buttons, params string[] terms)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                string name = button.name.ToLowerInvariant();
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                string text = label != null ? label.text.ToLowerInvariant() : string.Empty;

                for (int i = 0; i < terms.Length; i++)
                {
                    if (name.Contains(terms[i]) || text.Contains(terms[i]))
                    {
                        return button;
                    }
                }
            }

            return null;
        }

        private static void ConfigureButton(Button button, string label, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUiObject(name, parent, out RectTransform rectTransform, typeof(TextMeshProUGUI));
            rectTransform.sizeDelta = new Vector2(0f, fontSize + 16f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = alignment;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent, out RectTransform rectTransform, params Type[] extraComponents)
        {
            List<Type> componentTypes = new List<Type> { typeof(RectTransform) };
            if (extraComponents != null)
            {
                componentTypes.AddRange(extraComponents);
            }

            GameObject gameObject = new GameObject(name, componentTypes.ToArray());
            gameObject.transform.SetParent(parent, false);
            rectTransform = gameObject.GetComponent<RectTransform>();
            return gameObject;
        }

        private static string Percent(float value)
        {
            return $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private Canvas ResolveSettingsCanvas()
        {
            Canvas[] canvases = slimMenu != null && slimMenu.mainCanvas != null
                ? slimMenu.mainCanvas.GetComponentsInChildren<Canvas>(true)
                : GetComponentsInChildren<Canvas>(true);

            if (canvases == null || canvases.Length == 0)
            {
                Canvas parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    return parentCanvas.rootCanvas != null ? parentCanvas.rootCanvas : parentCanvas;
                }
            }

            if (canvases == null || canvases.Length == 0)
            {
                return null;
            }

            Canvas bestCanvas = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                int score = canvas.renderMode switch
                {
                    RenderMode.ScreenSpaceOverlay => 3,
                    RenderMode.ScreenSpaceCamera => 2,
                    _ => 1
                };

                if (canvas.transform == transform || canvas.transform.IsChildOf(transform))
                {
                    score += 1;
                }

                if (bestCanvas == null || score > bestScore)
                {
                    bestCanvas = canvas;
                    bestScore = score;
                }
            }

            return bestCanvas;
        }

        private Camera ResolveMenuCamera()
        {
            Camera camera = null;

            if (slimMenu != null)
            {
                camera = slimMenu.GetComponent<Camera>();
                if (camera == null)
                {
                    camera = slimMenu.GetComponentInChildren<Camera>(true);
                }

                if (camera == null)
                {
                    camera = slimMenu.GetComponentInParent<Camera>(true);
                }
            }

            camera ??= Camera.main;
            camera ??= FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            return camera;
        }

        private void NormalizeMenuCameraRig()
        {
            menuCamera ??= ResolveMenuCamera();
            if (menuCamera == null)
            {
                return;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera sceneCamera = cameras[i];
                if (sceneCamera == null)
                {
                    continue;
                }

                sceneCamera.enabled = sceneCamera == menuCamera;
            }

            GameObject cameraObject = menuCamera.gameObject;
            cameraObject.name = "Main Camera";
            cameraObject.tag = "MainCamera";
            menuCamera.enabled = true;
            menuCamera.rect = new Rect(0f, 0f, 1f, 1f);
            menuCamera.targetDisplay = 0;
            menuCamera.targetTexture = null;
            menuCamera.orthographic = false;
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(0.012f, 0.014f, 0.02f, 1f);
            menuCamera.nearClipPlane = 0.1f;
            menuCamera.farClipPlane = 250f;
            menuCamera.cullingMask = ~0;

            if (IsUsingGarageBackdrop())
            {
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2.35f, -9.1f), Quaternion.Euler(9f, 0f, 0f));
                Animator animator = slimMenu != null ? slimMenu.GetComponent<Animator>() : null;
                if (animator != null)
                {
                    animator.enabled = false;
                }
            }

            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] == null)
                {
                    continue;
                }

                listeners[i].enabled = listeners[i].gameObject == cameraObject;
            }
        }

        private static bool IsUsingGarageBackdrop()
        {
            return GameObject.Find("MainMenuGarageBackdrop") != null;
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace FullThrottle.Audio.Editor
{
    public static class RadioSystemSetupWizard
    {
        [MenuItem("Full Throttle/Setup Radio System In Scene")]
        public static void SetupRadioSystem()
        {
            // ── 1. Create RuntimeRadioManager ──────────────────────────
            GameObject managerObj = new GameObject("RuntimeRadioManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create RuntimeRadioManager");

            AudioSource audioSource = managerObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;

            RuntimeRadioManager manager = managerObj.AddComponent<RuntimeRadioManager>();

            // ── 2. Find or create a Canvas ─────────────────────────────
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // ── 3. Create NowPlayingPanel ──────────────────────────────
            GameObject panelObj = new GameObject("NowPlayingPanel");
            Undo.RegisterCreatedObjectUndo(panelObj, "Create NowPlayingPanel");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(560f, 120f);
            panelRect.anchoredPosition = new Vector2(-700f, -40f); // hidden by default

            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            CanvasGroup canvasGroup = panelObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            RadioNowPlayingPopup popup = panelObj.AddComponent<RadioNowPlayingPopup>();

            // ── 4. AlbumArt (child of panel) ───────────────────────────
            GameObject albumArtObj = new GameObject("AlbumArt");
            albumArtObj.transform.SetParent(panelObj.transform, false);

            RectTransform albumRect = albumArtObj.AddComponent<RectTransform>();
            albumRect.anchorMin = new Vector2(0f, 0.5f);
            albumRect.anchorMax = new Vector2(0f, 0.5f);
            albumRect.pivot = new Vector2(0f, 0.5f);
            albumRect.anchoredPosition = new Vector2(14f, 0f);
            albumRect.sizeDelta = new Vector2(88f, 88f);

            Image albumImage = albumArtObj.AddComponent<Image>();
            albumImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            albumImage.enabled = false;

            // ── 5. TitleText (child of panel) ──────────────────────────
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(panelObj.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(116f, -16f);
            titleRect.sizeDelta = new Vector2(-130f, 32f);

            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Track Title";
            titleTMP.fontSize = 22f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = Color.white;
            titleTMP.alignment = TextAlignmentOptions.Left;
            titleTMP.enableWordWrapping = false;
            titleTMP.overflowMode = TextOverflowModes.Ellipsis;

            // ── 6. ArtistText (child of panel) ─────────────────────────
            GameObject artistObj = new GameObject("ArtistText");
            artistObj.transform.SetParent(panelObj.transform, false);

            RectTransform artistRect = artistObj.AddComponent<RectTransform>();
            artistRect.anchorMin = new Vector2(0f, 1f);
            artistRect.anchorMax = new Vector2(1f, 1f);
            artistRect.pivot = new Vector2(0f, 1f);
            artistRect.anchoredPosition = new Vector2(116f, -50f);
            artistRect.sizeDelta = new Vector2(-130f, 24f);

            TextMeshProUGUI artistTMP = artistObj.AddComponent<TextMeshProUGUI>();
            artistTMP.text = "Artist";
            artistTMP.fontSize = 16f;
            artistTMP.color = new Color(0.7f, 0.7f, 0.75f, 1f);
            artistTMP.alignment = TextAlignmentOptions.Left;
            artistTMP.enableWordWrapping = false;
            artistTMP.overflowMode = TextOverflowModes.Ellipsis;

            // ── 7. StationText (child of panel) ────────────────────────
            GameObject stationObj = new GameObject("StationText");
            stationObj.transform.SetParent(panelObj.transform, false);

            RectTransform stationRect = stationObj.AddComponent<RectTransform>();
            stationRect.anchorMin = new Vector2(0f, 0f);
            stationRect.anchorMax = new Vector2(1f, 0f);
            stationRect.pivot = new Vector2(0f, 0f);
            stationRect.anchoredPosition = new Vector2(116f, 12f);
            stationRect.sizeDelta = new Vector2(-130f, 20f);

            TextMeshProUGUI stationTMP = stationObj.AddComponent<TextMeshProUGUI>();
            stationTMP.text = "FULL THROTTLE FM";
            stationTMP.fontSize = 11f;
            stationTMP.fontStyle = FontStyles.UpperCase;
            stationTMP.color = new Color(0.45f, 0.75f, 1f, 0.8f);
            stationTMP.alignment = TextAlignmentOptions.Left;
            stationTMP.characterSpacing = 4f;

            // ── 8. Wire serialized references via SerializedObject ─────

            // Wire RadioNowPlayingPopup references
            SerializedObject popupSO = new SerializedObject(popup);
            popupSO.FindProperty("panel").objectReferenceValue = panelRect;
            popupSO.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            popupSO.FindProperty("titleText").objectReferenceValue = titleTMP;
            popupSO.FindProperty("artistText").objectReferenceValue = artistTMP;
            popupSO.FindProperty("stationText").objectReferenceValue = stationTMP;
            popupSO.FindProperty("albumArtImage").objectReferenceValue = albumImage;
            popupSO.FindProperty("hiddenAnchoredPosition").vector2Value = new Vector2(-700f, -40f);
            popupSO.FindProperty("shownAnchoredPosition").vector2Value = new Vector2(24f, -40f);
            popupSO.FindProperty("fadeInDuration").floatValue = 0.18f;
            popupSO.FindProperty("slideInDuration").floatValue = 0.32f;
            popupSO.FindProperty("visibleDuration").floatValue = 4.25f;
            popupSO.FindProperty("fadeOutDuration").floatValue = 0.2f;
            popupSO.FindProperty("slideOutDuration").floatValue = 0.28f;
            popupSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire RuntimeRadioManager references
            SerializedObject managerSO = new SerializedObject(manager);
            managerSO.FindProperty("musicSource").objectReferenceValue = audioSource;
            managerSO.FindProperty("nowPlayingPopup").objectReferenceValue = popup;
            managerSO.FindProperty("streamingAssetsSubfolder").stringValue = "Radio/MainStation";
            managerSO.FindProperty("radioStationText").stringValue = "FULL THROTTLE FM";
            managerSO.FindProperty("shuffle").boolValue = true;
            managerSO.FindProperty("autoPlayOnStart").boolValue = true;
            managerSO.FindProperty("autoNextTrack").boolValue = true;
            managerSO.ApplyModifiedPropertiesWithoutUndo();

            // ── 9. Select the manager to confirm ──────────────────────
            Selection.activeGameObject = managerObj;
            EditorGUIUtility.PingObject(managerObj);

            Debug.Log("<color=#00ccff>[Full Throttle]</color> Radio System setup complete! RuntimeRadioManager + NowPlayingPopup created and fully wired.");
            Debug.Log("<color=#00ccff>[Full Throttle]</color> Drop your MP3 files into Assets/StreamingAssets/Radio/MainStation/ and press Play.");
        }
    }
}

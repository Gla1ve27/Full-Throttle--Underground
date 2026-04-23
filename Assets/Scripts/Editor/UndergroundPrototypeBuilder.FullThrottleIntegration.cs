using FullThrottle.Audio;
using FullThrottle.SacredCore.Audio;
using FullThrottle.SacredCore.Campaign;
using FullThrottle.SacredCore.EditorTools;
using FullThrottle.SacredCore.Vehicle;
using Underground.Audio;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private const string RuntimeRadioManagerPrefabPath = "Assets/Prefabs/Managers/RuntimeRadioManager.prefab";
        private const string RadioStreamingAssetsFolderPath = "Assets/StreamingAssets/Radio/MainStation";
        private const string AudioModePresetFolderPath = "Assets/ScriptableObjects/FullThrottle/AudioModePresets";
        private const string FullThrottleCampaignPath = "Assets/ScriptableObjects/FullThrottle/Campaign/campaign_full_throttle_gla1ve.asset";
        private const string Car27BankPath = "Assets/ScriptableObjects/Vehicles/Car_27_AudioBank.asset";
        private const string Car27HeroAudioProfilePath = "Assets/ScriptableObjects/FullThrottle/AudioProfiles/car_27_hero_stock.asset";
        private const string Reizan350ZDefinitionPath = "Assets/ScriptableObjects/FullThrottle/Cars/reizan_350z.asset";

        [MenuItem("Full Throttle/Project/Apply Latest FT Core Changes", priority = 4)]
        public static void ApplyLatestFullThrottleCoreChangesFromMenu()
        {
            ApplyLatestFullThrottleCoreChanges(rebuildGeneratedPrefabs: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorUtility.DisplayDialog(
                "FT Core Updated",
                "Applied the latest Full Throttle sacred-core assets, generated prefabs, campaign data, audio profile import, and global radio setup.",
                "OK");
        }

        private static void ApplyLatestFullThrottleCoreChanges(bool rebuildGeneratedPrefabs)
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            ConfigureTagsAndLayers();
            FTSacredCoreSetupWizard.CreateAssetFolders();
            FTPhase5AssetMigrator.GenerateStarterAssets();
            FTCampaignAssetGenerator.GenerateCampaignAssets();
            TryRebuildCar27HeroProfile();
            CreateOrUpdateAudioModePresets();
            EnsureRadioStreamingAssetsFolder();
            CreateOrUpdateRuntimeRadioPrefab();

            if (rebuildGeneratedPrefabs)
            {
                CreateSceneSupportPrefabs(preserveExistingAssets: false);
            }

            Debug.Log("[UndergroundPrototypeBuilder] Latest FT core automation applied: sacred core, campaign, audio identity, generated prefabs, and global radio.");
        }

        private static void CreateOrUpdateAudioModePresets()
        {
            EnsureFolderPath(AudioModePresetFolderPath);

            FTAudioModePreset freeRoam = CreateOrUpdateAudioModePreset("ft_mix_freeroam", FTAudioMode.FreeRoam, 1.00f, 0.90f, 0.90f, 0.88f, 0.95f, 0.92f, 0.82f);
            FTAudioModePreset race = CreateOrUpdateAudioModePreset("ft_mix_race", FTAudioMode.Race, 1.12f, 0.96f, 1.00f, 0.98f, 1.08f, 0.88f, 0.66f);
            FTAudioModePreset drift = CreateOrUpdateAudioModePreset("ft_mix_drift", FTAudioMode.Drift, 1.02f, 1.12f, 1.04f, 0.94f, 1.04f, 1.28f, 0.62f);
            FTAudioModePreset drag = CreateOrUpdateAudioModePreset("ft_mix_drag", FTAudioMode.Drag, 1.18f, 0.82f, 1.02f, 1.12f, 1.14f, 0.72f, 0.58f);
            FTAudioModePreset garage = CreateOrUpdateAudioModePreset("ft_mix_garage", FTAudioMode.Garage, 0.78f, 0.78f, 0.86f, 0.74f, 0.82f, 0.00f, 0.36f);

            string profilesFolder = "Assets/ScriptableObjects/FullThrottle/AudioProfiles";
            if (!AssetDatabase.IsValidFolder(profilesFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:FTVehicleAudioProfile", new[] { profilesFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                FTVehicleAudioProfile profile = AssetDatabase.LoadAssetAtPath<FTVehicleAudioProfile>(assetPath);
                if (profile == null)
                {
                    continue;
                }

                if (profile.modePresets == null)
                {
                    profile.modePresets = new FTAudioModePresetOverrides();
                }
                profile.modePresets.freeRoam = freeRoam;
                profile.modePresets.race = race;
                profile.modePresets.drift = drift;
                profile.modePresets.drag = drag;
                profile.modePresets.garage = garage;
                EditorUtility.SetDirty(profile);
            }
        }

        private static FTAudioModePreset CreateOrUpdateAudioModePreset(
            string assetName,
            FTAudioMode mode,
            float engineCore,
            float engineDecel,
            float sweetener,
            float turbo,
            float shift,
            float skid,
            float worldBed)
        {
            string assetPath = $"{AudioModePresetFolderPath}/{assetName}.asset";
            FTAudioModePreset preset = AssetDatabase.LoadAssetAtPath<FTAudioModePreset>(assetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<FTAudioModePreset>();
                AssetDatabase.CreateAsset(preset, assetPath);
            }

            preset.mode = mode;
            preset.transitionSeconds = mode == FTAudioMode.Garage ? 0.55f : 0.32f;
            preset.engineCore = engineCore;
            preset.engineDecel = engineDecel;
            preset.sweetener = sweetener;
            preset.turbo = turbo;
            preset.shift = shift;
            preset.skid = skid;
            preset.worldBed = worldBed;
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static void TryRebuildCar27HeroProfile()
        {
            NFSU2CarAudioBank bank = AssetDatabase.LoadAssetAtPath<NFSU2CarAudioBank>(Car27BankPath);
            if (bank == null)
            {
                Debug.LogWarning($"[UndergroundPrototypeBuilder] Car 27 audio bank not found at {Car27BankPath}. Existing FT audio profiles were left unchanged.");
                return;
            }

            NFSU2BankToFTProfileConverter.ConvertBank(
                bank,
                VehicleAudioTier.Stock,
                "car_27_hero_stock",
                Car27HeroAudioProfilePath);
        }

        private static void ConfigureKnownFTCarDefinitions(GameObject sharedWorldPrefab)
        {
            FTCarDefinition reizan = AssetDatabase.LoadAssetAtPath<FTCarDefinition>(Reizan350ZDefinitionPath);
            if (reizan == null)
            {
                return;
            }

            reizan.carId = "reizan_350z";
            reizan.displayName = "Reizan 350Z";
            reizan.vehicleClass = "Starter Hero";
            reizan.driveType = "RWD";
            reizan.engineCharacterTag = "turbo_i4_hero";
            reizan.audioProfileId = "car_27_hero_stock";
            reizan.garagePreviewRevStyle = "true-profile-preview";
            reizan.forcedInductionType = "Turbo";
            reizan.audioFamilyTag = "car_27";
            reizan.starterOwned = true;
            reizan.worldPrefab = sharedWorldPrefab;

            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blender3DByBads/350z.fbx");
            if (visualPrefab != null)
            {
                reizan.visualPrefab = visualPrefab;
                reizan.editorVisualPrefabPath = "Assets/Blender3DByBads/350z.fbx";
            }

            reizan.feel.acceleration = 5.2f;
            reizan.feel.topSpeed = 5.0f;
            reizan.feel.handling = 5.2f;
            reizan.feel.driftBias = 0.38f;
            EditorUtility.SetDirty(reizan);
        }

        private static FTCampaignDefinition LoadFullThrottleCampaign()
        {
            return AssetDatabase.LoadAssetAtPath<FTCampaignDefinition>(FullThrottleCampaignPath);
        }

        private static void EnsureRuntimeRadioOnObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            AudioSource source = target.GetComponent<AudioSource>();
            if (source == null)
            {
                source = target.AddComponent<AudioSource>();
            }

            ConfigureRadioAudioSource(source);

            RuntimeRadioManager manager = target.GetComponent<RuntimeRadioManager>();
            if (manager == null)
            {
                manager = target.AddComponent<RuntimeRadioManager>();
            }

            ConfigureRuntimeRadioManager(manager, source, null);
        }

        private static GameObject CreateOrUpdateRuntimeRadioPrefab()
        {
            EnsureRadioStreamingAssetsFolder();

            GameObject root = new GameObject("RuntimeRadioManager");
            AudioSource source = root.AddComponent<AudioSource>();
            ConfigureRadioAudioSource(source);

            RuntimeRadioManager manager = root.AddComponent<RuntimeRadioManager>();
            ConfigureRuntimeRadioManager(manager, source, null);

            PrefabUtility.SaveAsPrefabAsset(root, RuntimeRadioManagerPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRadioManagerPrefabPath);
        }

        private static void EnsureRadioStreamingAssetsFolder()
        {
            EnsureFolderPath(RadioStreamingAssetsFolderPath);
        }

        private static void EnsureFolderPath(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void ConfigureRadioAudioSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0.72f;
        }

        private static void ConfigureRuntimeRadioManager(RuntimeRadioManager manager, AudioSource source, RadioNowPlayingPopup popup)
        {
            if (manager == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(manager);
            AssignObject(serialized, "musicSource", source);
            AssignObject(serialized, "nowPlayingPopup", popup);
            AssignString(serialized, "streamingAssetsSubfolder", "Radio/MainStation");
            AssignString(serialized, "radioStationText", "FULL THROTTLE FM");
            AssignBool(serialized, "useCustomAbsoluteFolder", false);
            AssignBool(serialized, "includeSubfolders", false);
            AssignBool(serialized, "autoPlayOnStart", true);
            AssignBool(serialized, "autoNextTrack", true);
            AssignBool(serialized, "shuffle", true);
            AssignBool(serialized, "avoidImmediateShuffleRepeats", true);
            AssignFloat(serialized, "volume", 0.72f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static RadioNowPlayingPopup EnsureRadioPopupOnCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            RadioNowPlayingPopup existing = canvas.GetComponentInChildren<RadioNowPlayingPopup>(true);
            if (existing != null)
            {
                return existing;
            }

            GameObject panelObject = new GameObject("RadioNowPlayingPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObject.transform.SetParent(canvas.transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(560f, 120f);
            panel.anchoredPosition = new Vector2(-700f, -40f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.02f, 0.025f, 0.035f, 0.82f);

            CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            RadioNowPlayingPopup popup = panelObject.AddComponent<RadioNowPlayingPopup>();

            Image albumArt = CreateRadioImage(panelObject.transform, "AlbumArt", new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(88f, 88f), new Color(0.18f, 0.2f, 0.23f, 1f));
            albumArt.enabled = false;

            TextMeshProUGUI title = CreateRadioText(panelObject.transform, "TitleText", "Track Title", 22f, FontStyles.Bold, Color.white, new Vector2(0f, 1f), new Vector2(116f, -16f), new Vector2(410f, 32f));
            TextMeshProUGUI artist = CreateRadioText(panelObject.transform, "ArtistText", "Artist", 16f, FontStyles.Normal, new Color(0.72f, 0.72f, 0.78f, 1f), new Vector2(0f, 1f), new Vector2(116f, -50f), new Vector2(410f, 24f));
            TextMeshProUGUI station = CreateRadioText(panelObject.transform, "StationText", "FULL THROTTLE FM", 11f, FontStyles.UpperCase, new Color(0.45f, 0.75f, 1f, 0.86f), new Vector2(0f, 0f), new Vector2(116f, 12f), new Vector2(410f, 20f));
            station.characterSpacing = 4f;

            SerializedObject popupSerialized = new SerializedObject(popup);
            AssignObject(popupSerialized, "panel", panel);
            AssignObject(popupSerialized, "canvasGroup", canvasGroup);
            AssignObject(popupSerialized, "titleText", title);
            AssignObject(popupSerialized, "artistText", artist);
            AssignObject(popupSerialized, "stationText", station);
            AssignObject(popupSerialized, "albumArtImage", albumArt);
            AssignVector2(popupSerialized, "hiddenAnchoredPosition", new Vector2(-700f, -40f));
            AssignVector2(popupSerialized, "shownAnchoredPosition", new Vector2(24f, -40f));
            AssignFloat(popupSerialized, "fadeInDuration", 0.18f);
            AssignFloat(popupSerialized, "slideInDuration", 0.32f);
            AssignFloat(popupSerialized, "visibleDuration", 4.25f);
            AssignFloat(popupSerialized, "fadeOutDuration", 0.2f);
            AssignFloat(popupSerialized, "slideOutDuration", 0.28f);
            popupSerialized.ApplyModifiedPropertiesWithoutUndo();

            return popup;
        }

        private static Image CreateRadioImage(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateRadioText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            Color color,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor.y >= 1f ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static void AssignObject(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void AssignString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void AssignBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void AssignFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void AssignVector2(SerializedObject serialized, string propertyName, Vector2 value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.vector2Value = value;
            }
        }
    }
}

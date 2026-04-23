#if UNITY_EDITOR
using FullThrottle.SacredCore.Audio;
using FullThrottle.SacredCore.Campaign;
using FullThrottle.SacredCore.Career;
using FullThrottle.SacredCore.Economy;
using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Progression;
using FullThrottle.SacredCore.Race;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Vehicle;
using FullThrottle.SacredCore.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FullThrottle.SacredCore.EditorTools
{
    public static class FTSacredCoreSetupWizard
    {
        [MenuItem("Full Throttle/Sacred Core/Create Asset Folders")]
        public static void CreateAssetFolders()
        {
            EnsureFolder("Assets", "ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects", "FullThrottle");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "Cars");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "AudioProfiles");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "Races");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "Routes");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "Rivals");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "Outrun");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "WorldTime");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "Campaign");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "Narrative");
            EnsureFolder("Assets/ScriptableObjects/FullThrottle", "StoryActs");
            AssetDatabase.SaveAssets();
            Debug.Log("[SacredCore] Asset folders ready under Assets/ScriptableObjects/FullThrottle.");
        }

        [MenuItem("Full Throttle/Sacred Core/Create Runtime Root In Current Scene")]
        public static void CreateRuntimeRoot()
        {
            GameObject root = FindOrCreate("FT_RuntimeRoot");
            FTBootstrap bootstrap = GetOrAdd<FTBootstrap>(root);
            FTRuntimeRoot runtimeRoot = GetOrAdd<FTRuntimeRoot>(root);
            FTServiceRegistry serviceRegistry = GetOrAdd<FTServiceRegistry>(root);
            FTSaveGateway saveGateway = GetOrAdd<FTSaveGateway>(root);
            FTCarRegistry carRegistry = GetOrAdd<FTCarRegistry>(root);
            FTAudioProfileRegistry audioRegistry = GetOrAdd<FTAudioProfileRegistry>(root);
            FTSelectedCarRuntime selectedCar = GetOrAdd<FTSelectedCarRuntime>(root);
            FTWorldTravelDirector worldTravel = GetOrAdd<FTWorldTravelDirector>(root);
            FTCareerDirector career = GetOrAdd<FTCareerDirector>(root);
            FTRiskEconomyDirector risk = GetOrAdd<FTRiskEconomyDirector>(root);
            FTHeatDirector heat = GetOrAdd<FTHeatDirector>(root);
            FTWagerDirector wager = GetOrAdd<FTWagerDirector>(root);
            FTRivalDirector rival = GetOrAdd<FTRivalDirector>(root);
            FTCampaignDirector campaign = GetOrAdd<FTCampaignDirector>(root);
            FTNarrativeDirector narrative = GetOrAdd<FTNarrativeDirector>(root);
            FTProgressionDirector progression = GetOrAdd<FTProgressionDirector>(root);
            FTAudioIdentityDirector audioIdentity = GetOrAdd<FTAudioIdentityDirector>(root);
            FTAudioRosterValidator audioValidator = GetOrAdd<FTAudioRosterValidator>(root);
            FTSacredCoreHealthCheck healthCheck = GetOrAdd<FTSacredCoreHealthCheck>(root);
            FTGameContext context = GetOrAdd<FTGameContext>(root);

            AssignObject(bootstrap, "saveGateway", saveGateway);
            AssignBehaviourArray(bootstrap, "serviceBehaviours",
                runtimeRoot,
                serviceRegistry,
                saveGateway,
                carRegistry,
                audioRegistry,
                selectedCar,
                worldTravel,
                career,
                risk,
                heat,
                wager,
                rival,
                campaign,
                narrative,
                progression,
                audioIdentity,
                audioValidator,
                healthCheck,
                context);

            MarkSceneDirty();
            Selection.activeObject = root;
            Debug.Log("[SacredCore] Runtime root created. Assign car/audio/story/rival assets in the inspector.");
        }

        [MenuItem("Full Throttle/Sacred Core/Create Garage Directors In Current Scene")]
        public static void CreateGarageDirectors()
        {
            GameObject directors = FindOrCreate("FT_GarageDirectors");
            GameObject anchor = FindOrCreate("FT_ShowroomAnchor");
            GameObject displayParent = FindOrCreate("FT_ShowroomCars");

            FTGarageDirector garage = GetOrAdd<FTGarageDirector>(directors);
            FTGarageShowroomDirector showroom = GetOrAdd<FTGarageShowroomDirector>(directors);
            FullThrottle.SacredCore.Camera.FTGarageCameraDirector cameraDirector =
                GetOrAdd<FullThrottle.SacredCore.Camera.FTGarageCameraDirector>(directors);
            FTGarageAudioPreviewDirector preview = GetOrAdd<FTGarageAudioPreviewDirector>(directors);

            AssignObject(showroom, "displayAnchor", anchor.transform);
            AssignObject(showroom, "displayParent", displayParent.transform);
            AssignObject(cameraDirector, "showroomAnchor", anchor.transform);
            AssignObject(preview, "sourceAnchor", anchor.transform);

            MarkSceneDirty();
            Selection.activeObject = directors;
            Debug.Log("[SacredCore] Garage directors created. Set FTGarageDirector.worldSceneName if your world scene is not named 'World'.");
        }

        [MenuItem("Full Throttle/Sacred Core/Create World Directors In Current Scene")]
        public static void CreateWorldDirectors()
        {
            GameObject directors = FindOrCreate("FT_WorldDirectors");
            GameObject vehicleParent = FindOrCreate("FT_PlayerVehicleRoot");

            FTSpawnPointResolver resolver = GetOrAdd<FTSpawnPointResolver>(directors);
            FTVehicleSpawnDirector spawn = GetOrAdd<FTVehicleSpawnDirector>(directors);
            FTRaceDirector race = GetOrAdd<FTRaceDirector>(directors);
            FTOutrunChallengeDirector outrun = GetOrAdd<FTOutrunChallengeDirector>(directors);
            FTWorldTimePresetDirector timePreset = GetOrAdd<FTWorldTimePresetDirector>(directors);
            FullThrottle.SacredCore.Camera.FTVehicleCameraDirector cameraDirector =
                GetOrAdd<FullThrottle.SacredCore.Camera.FTVehicleCameraDirector>(directors);

            AssignObject(spawn, "vehicleParent", vehicleParent.transform);
            CreateSpawnPoint("FT_Spawn_player_start", "player_start", true, Vector3.zero, Quaternion.identity);
            CreateSpawnPoint("FT_Spawn_garage_exit", "garage_exit", false, new Vector3(0f, 0f, 4f), Quaternion.identity);

            MarkSceneDirty();
            Selection.activeObject = directors;
            Debug.Log("[SacredCore] World directors created. Assign gameplay camera to FTVehicleCameraDirector.");
        }

        private static void CreateSpawnPoint(string objectName, string spawnPointId, bool defaultForScene, Vector3 position, Quaternion rotation)
        {
            GameObject go = FindOrCreate(objectName);
            go.transform.SetPositionAndRotation(position, rotation);
            FTSpawnPoint point = GetOrAdd<FTSpawnPoint>(go);
            point.spawnPointId = spawnPointId;
            point.defaultForScene = defaultForScene;
            EditorUtility.SetDirty(point);
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            return created;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return Undo.AddComponent<T>(go);
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }

        private static void AssignBehaviourArray(Object target, string propertyName, params MonoBehaviour[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        public static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
#endif

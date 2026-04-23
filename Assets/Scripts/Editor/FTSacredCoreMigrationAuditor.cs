#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FullThrottle.SacredCore.EditorTools
{
    public static class FTSacredCoreMigrationAuditor
    {
        private readonly struct LegacyOwner
        {
            public readonly string TypeName;
            public readonly string Replacement;
            public readonly bool Blocking;

            public LegacyOwner(string typeName, string replacement, bool blocking)
            {
                TypeName = typeName;
                Replacement = replacement;
                Blocking = blocking;
            }
        }

        private sealed class AuditReport
        {
            public readonly string Scope;
            public int ObjectsScanned;
            public int ComponentsScanned;
            public int LegacyOwnersFound;
            public int MissingScriptsFound;
            public int BlockingFindings;

            public AuditReport(string scope)
            {
                Scope = scope;
            }
        }

        private static readonly LegacyOwner[] LegacyOwners =
        {
            new LegacyOwner("PersistentProgressManager", "FTSaveGateway + FTProfileData", true),
            new LegacyOwner("GarageManager", "FTGarageDirector", true),
            new LegacyOwner("GarageShowroomController", "FTGarageShowroomDirector", true),
            new LegacyOwner("GarageUIController", "FT-aware garage UI calling FTGarageDirector", false),
            new LegacyOwner("VehicleOwnershipSystem", "FTProfileData.ownedCarIds + FTGarageDirector", true),
            new LegacyOwner("VehicleSetupBridge", "FTVehicleSpawnDirector + FTPlayerVehicleBinder", true),
            new LegacyOwner("VehicleInput", "FTDriverInput", true),
            new LegacyOwner("VehicleInputAdapter", "FTDriverInput", true),
            new LegacyOwner("VehicleDynamicsController", "FTVehicleController + FT handling models", true),
            new LegacyOwner("VehicleControllerV2", "FTVehicleController + FT handling models", true),
            new LegacyOwner("VehicleAudioController", "FTVehicleAudioDirector + FTEngineLoopMixer", true),
            new LegacyOwner("CarEngineAudio", "FTVehicleAudioDirector + FTEngineLoopMixer", true),
            new LegacyOwner("VehicleAudioControllerV2", "FTVehicleAudioDirector + FTEngineLoopMixer", true),
            new LegacyOwner("VehicleAudioTierSelector", "FTAudioIdentityDirector + FTAudioProfileRegistry", true),
            new LegacyOwner("RaceManager", "FTRaceDirector", true),
            new LegacyOwner("RaceStartTrigger", "FT race trigger calling FTRaceDirector", false),
            new LegacyOwner("RaceFinishTrigger", "FT race trigger calling FTRaceDirector", false),
            new LegacyOwner("CarRespawn", "FTRespawnDirector", true),
            new LegacyOwner("Speedometer", "FTSpeedDisplay", false),
            new LegacyOwner("HUDController", "FTHUDDirector + FT displays", false)
        };

        [MenuItem("Full Throttle/Sacred Core/Audit Legacy Migration In Loaded Scenes")]
        public static void AuditLoadedScenes()
        {
            AuditReport report = new AuditReport("loaded scenes");

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    AuditRoot(roots[i], scene.name, report);
                }
            }

            LogSummary(report);
        }

        [MenuItem("Full Throttle/Sacred Core/Audit Legacy Migration In Selected Objects")]
        public static void AuditSelectedObjects()
        {
            AuditReport report = new AuditReport("selected objects");
            Object[] selected = Selection.objects;

            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is GameObject go)
                {
                    AuditRoot(go, AssetDatabase.GetAssetPath(go), report);
                }
            }

            LogSummary(report);
        }

        [MenuItem("Full Throttle/Sacred Core/Audit Legacy Migration In Selected Objects", true)]
        private static bool CanAuditSelectedObjects()
        {
            Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is GameObject)
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem("Full Throttle/Sacred Core/Open Phase 5 Migration Guide")]
        public static void OpenPhase5Guide()
        {
            Object guide = AssetDatabase.LoadAssetAtPath<Object>("Docs/ft_sacred_core_phase5_migration.md");
            if (guide != null)
            {
                Selection.activeObject = guide;
                EditorGUIUtility.PingObject(guide);
                AssetDatabase.OpenAsset(guide);
                return;
            }

            Debug.LogWarning("[SacredCore][Migration] Phase 5 guide was not found at Docs/ft_sacred_core_phase5_migration.md.");
        }

        [MenuItem("Full Throttle/Sacred Core/Audit Player Car Prefab")]
        public static void AuditPlayerCarPrefab()
        {
            string[] prefabPaths =
            {
                "Assets/Prefabs/Generated/PlayerCar.prefab",
                "Assets/Prefabs/PlayerCar.prefab"
            };

            AuditReport report = new AuditReport("player car prefab");
            bool found = false;
            for (int i = 0; i < prefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                if (prefab != null)
                {
                    AuditRoot(prefab, prefabPaths[i], report);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning("[SacredCore][Migration] No player car prefab found at expected paths.");
            }

            LogSummary(report);
        }

        [MenuItem("Full Throttle/Sacred Core/Verify FT Runtime Root Presence")]
        public static void VerifyFTRuntimeRootPresence()
        {
            GameObject ftRoot = GameObject.Find("FT_RuntimeRoot");
            if (ftRoot == null)
            {
                Debug.LogError("[SacredCore][Migration] FT_RuntimeRoot is NOT present in the current scene.");
                return;
            }

            bool hasBootstrap = ftRoot.GetComponent<Runtime.FTBootstrap>() != null;
            bool hasRuntimeRoot = ftRoot.GetComponent<Runtime.FTRuntimeRoot>() != null;
            bool hasSaveGateway = ftRoot.GetComponent<Save.FTSaveGateway>() != null;
            bool hasCarRegistry = ftRoot.GetComponent<Vehicle.FTCarRegistry>() != null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[SacredCore][Migration] FT_RuntimeRoot presence check:");
            sb.AppendLine($"  FTBootstrap: {(hasBootstrap ? "OK" : "MISSING")}");
            sb.AppendLine($"  FTRuntimeRoot: {(hasRuntimeRoot ? "OK" : "MISSING")}");
            sb.AppendLine($"  FTSaveGateway: {(hasSaveGateway ? "OK" : "MISSING")}");
            sb.AppendLine($"  FTCarRegistry: {(hasCarRegistry ? "OK" : "MISSING")}");

            bool allPresent = hasBootstrap && hasRuntimeRoot && hasSaveGateway && hasCarRegistry;
            sb.AppendLine(allPresent ? "  STATUS: PASS" : "  STATUS: FAIL — missing core services");

            // Check for legacy root that should have been removed
            GameObject legacyRoot = GameObject.Find("RuntimeRoot");
            if (legacyRoot != null && legacyRoot != ftRoot)
            {
                sb.AppendLine("  WARNING: Legacy 'RuntimeRoot' object still present alongside FT_RuntimeRoot.");
            }

            if (allPresent)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogError(sb.ToString());
            }
        }

        private static void AuditRoot(GameObject root, string scope, AuditReport report)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            report.ObjectsScanned += transforms.Length;

            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject go = transforms[i].gameObject;
                MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
                report.ComponentsScanned += behaviours.Length;

                for (int componentIndex = 0; componentIndex < behaviours.Length; componentIndex++)
                {
                    MonoBehaviour behaviour = behaviours[componentIndex];
                    if (behaviour == null)
                    {
                        report.MissingScriptsFound++;
                        Debug.LogWarning($"[SacredCore][Migration] Missing script on {BuildPath(go)}. Scope={scope}", go);
                        continue;
                    }

                    string typeName = behaviour.GetType().Name;
                    if (TryGetLegacyOwner(typeName, out LegacyOwner owner))
                    {
                        report.LegacyOwnersFound++;
                        if (owner.Blocking)
                        {
                            report.BlockingFindings++;
                        }

                        string severity = owner.Blocking ? "BLOCKER" : "REVIEW";
                        Debug.LogWarning(
                            $"[SacredCore][Migration] {severity}: {typeName} remains on {BuildPath(go)}. Replace with {owner.Replacement}. Scope={scope}",
                            behaviour);
                    }
                }
            }
        }

        private static bool TryGetLegacyOwner(string typeName, out LegacyOwner owner)
        {
            for (int i = 0; i < LegacyOwners.Length; i++)
            {
                if (LegacyOwners[i].TypeName == typeName)
                {
                    owner = LegacyOwners[i];
                    return true;
                }
            }

            owner = default;
            return false;
        }

        private static void LogSummary(AuditReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[SacredCore][Migration] Audit complete. ");
            builder.Append("scope=").Append(report.Scope);
            builder.Append(", objects=").Append(report.ObjectsScanned);
            builder.Append(", monoBehaviours=").Append(report.ComponentsScanned);
            builder.Append(", legacyOwners=").Append(report.LegacyOwnersFound);
            builder.Append(", blockers=").Append(report.BlockingFindings);
            builder.Append(", missingScripts=").Append(report.MissingScriptsFound);

            if (report.LegacyOwnersFound == 0 && report.MissingScriptsFound == 0)
            {
                Debug.Log("[SacredCore][Migration] PASS. No legacy owner components found.");
                Debug.Log(builder.ToString());
                return;
            }

            if (report.BlockingFindings > 0 || report.MissingScriptsFound > 0)
            {
                Debug.LogError(builder.ToString());
            }
            else
            {
                Debug.LogWarning(builder.ToString());
            }
        }

        private static string BuildPath(GameObject go)
        {
            if (go == null)
            {
                return "<null>";
            }

            string path = go.name;
            Transform current = go.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
#endif

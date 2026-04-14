using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// FastWorld Generator — creates a massive interconnected city by placing
    /// multiple FCG zones in a grid formation.
    ///
    /// Each FCG zone generates its own streets and buildings. When placed
    /// adjacent to each other, the ExitCity/Border blocks connect the
    /// road networks together, creating one huge driveable city.
    ///
    /// This replaces the complex multi-phase pipeline with a dead-simple approach:
    ///   1. Generate FCG city
    ///   2. Clone it to a grid position
    ///   3. Repeat until the grid is full
    ///
    /// Usage: Add to a GameObject, assign the FCG Generator reference,
    ///        then right-click → "Generate FastWorld"
    /// </summary>
    public class FastWorldGenerator : MonoBehaviour
    {
        [Header("World Settings")]
        [Tooltip("Size of the organic city (1=VerySmall 2=Small 3=Medium 4=Large)")]
        [Range(1, 4)]
        [SerializeField] private int citySize = 4;

        [Tooltip("Allow FCG to automatically build organic highway bridges connecting to a secondary satellite city across the water.")]
        [SerializeField] private bool generateSatelliteArchipelago = true;

        [Header("FCG Buildings")]
        [Tooltip("Enable downtown high-rises")]
        [SerializeField] private bool enableDowntown = true;

        [Tooltip("Downtown influence radius")]
        [SerializeField] private float downtownSize = 400f;

        [Header("Optimization & Polish")]
        [Tooltip("Log progress to console")]
        [SerializeField] private bool logProgress = true;

        [Tooltip("Marks all generated meshes as Static. This combines them into fewer draw calls and prevents massive CPU lag.")]
        [SerializeField] private bool optimizeForPerformance = true;

        private GameObject activeWorldRoot;

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("Generate Organic World")]
        public void GenerateFastWorld()
        {
            float startTime = Time.realtimeSinceStartup;

            if (logProgress)
                Debug.Log($"[FastWorld] ═══ Generating Organic FCG World (Size {citySize}) ═══");

            // Find FCG
            FCG.CityGenerator generator = FindFirstObjectByType<FCG.CityGenerator>();
            if (generator == null)
            {
                Debug.LogError("[FastWorld] ❌ No FCG CityGenerator found in scene!");
                return;
            }

            // Clean previous generation
            ClearFastWorld();

            // Create root
            activeWorldRoot = new GameObject("Organic_FCG_World");
            activeWorldRoot.transform.SetParent(transform);

            // Generate organic layout purely through FCG
            generator.transform.position = Vector3.zero;
            generator.GenerateCity(citySize, generateSatelliteArchipelago, false);
            generator.GenerateAllBuildings(enableDowntown, enableDowntown ? downtownSize : 0f);

            // Extract the generated mesh from FCG
            GameObject cityMaker = GameObject.Find("City-Maker");
            if (cityMaker != null)
            {
                GameObject organicWorld = Instantiate(cityMaker);
                organicWorld.name = $"FCG_World_Size{citySize}";
                organicWorld.transform.SetParent(activeWorldRoot.transform);
                organicWorld.transform.position = Vector3.zero;

                // Deactivate original and mark for cleanup
                cityMaker.SetActive(false);
                cityMaker.name = "City-Maker-Garbage";
            }
            else
            {
                Debug.LogWarning("[FastWorld] City-Maker output not found!");
            }

            CleanupFCGArtifacts();
            generator.ClearCity();

            // Populate the highway archipelago bridges to make them alive
            PopulateHighwaysWithNature(activeWorldRoot);

            // Handle Performance Lag
            if (optimizeForPerformance)
            {
                OptimizeWorld(activeWorldRoot);
            }

            float elapsed = Time.realtimeSinceStartup - startTime;

            if (logProgress)
            {
                Debug.Log($"[FastWorld] ═══════════════════════════════════════════");
                Debug.Log($"[FastWorld] ✅ World generated and optimized in {elapsed:F1}s.");
                Debug.Log($"[FastWorld] ═══════════════════════════════════════════");
            }
        }

        private void PopulateHighwaysWithNature(GameObject root)
        {
            if (logProgress) Debug.Log("[FastWorld] Populating highway archipelago with scattered trees...");

            GameObject treePrefab = null;
            MeshRenderer[] allRends = root.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in allRends)
            {
                if (r.gameObject.name.ToLower().Contains("tree"))
                {
                    treePrefab = r.gameObject;
                    break;
                }
            }

            if (treePrefab == null)
            {
                Debug.LogWarning("[FastWorld] No tree found in organic FCG city to clone! Scattering skipped.");
                return;
            }

            Transform[] allTransforms = root.GetComponentsInChildren<Transform>();
            List<GameObject> highways = new List<GameObject>();
            foreach(var t in allTransforms)
            {
                if (t.name.StartsWith("HW-") && t.parent != null && t.parent.name.Contains("FCG_World"))
                {
                    highways.Add(t.gameObject);
                }
            }

            foreach (var hw in highways)
            {
                PopulateChunk(hw, treePrefab);
            }
        }

        private void PopulateChunk(GameObject hw, GameObject treePrefab)
        {
            // 1. Find the explicit "Grass" meshes inside the FCG highway prefab
            List<GameObject> grassObjects = new List<GameObject>();
            Transform[] allChildren = hw.GetComponentsInChildren<Transform>();
            foreach (Transform t in allChildren)
            {
                if (t.name.ToLower().Contains("grass"))
                {
                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        grassObjects.Add(t.gameObject);
                    }
                }
            }

            if (grassObjects.Count == 0) return;

            // 2. Populate each grass mesh footprint perfectly without modifying the Prefab Instance
            foreach (GameObject grassObj in grassObjects)
            {
                MeshRenderer mr = grassObj.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                Bounds grassBounds = mr.bounds; // Reliable rendering bounds, ignores physics init delays

                // Generate an invisible clone just for physics raycasting to prevent 
                // Unity from throwing "UnpackPrefabInstance" errors when editing secure prefabs!
                GameObject tempPhysicsHitbox = new GameObject("Temp_FCG_Physics_Clone");
                tempPhysicsHitbox.transform.position = grassObj.transform.position;
                tempPhysicsHitbox.transform.rotation = grassObj.transform.rotation;
                tempPhysicsHitbox.transform.localScale = grassObj.transform.lossyScale;

                MeshCollider tempMC = tempPhysicsHitbox.AddComponent<MeshCollider>();
                MeshFilter mf = grassObj.GetComponent<MeshFilter>();
                tempMC.sharedMesh = mf.sharedMesh;

                // FORESTRY LEVEL DENSITY
                int targetTrees = Mathf.RoundToInt(grassBounds.size.magnitude * 2.5f);
                int attempts = targetTrees * 6;

                for (int i = 0; i < attempts && targetTrees > 0; i++)
                {
                    float rx = Random.Range(grassBounds.min.x, grassBounds.max.x);
                    float rz = Random.Range(grassBounds.min.z, grassBounds.max.z);
                    Vector3 startPos = new Vector3(rx, grassBounds.max.y + 100f, rz);

                    // MATHEMATICAL PERFECTION: Raycast strictly looking for the detached Grass clone.
                    // This naturally contours to curvy highways without crashing prefabs!
                    if (tempMC.Raycast(new Ray(startPos, Vector3.down), out RaycastHit hit, 200f))
                    {
                        if (Vector3.Angle(hit.normal, Vector3.up) < 35f)
                        {
                            // Spawn main canopy
                            GameObject newTree = Instantiate(treePrefab, hit.point, Quaternion.Euler(0, Random.Range(0, 360), 0), hw.transform);
                            newTree.name = "Forestry_Tree";
                            newTree.transform.localScale = treePrefab.transform.localScale * Random.Range(0.8f, 1.8f);
                            targetTrees--;

                            // Spawn dense micro-clusters naturally clustering on the grass
                            int clusterCount = Random.Range(1, 4);
                            for (int c = 0; c < clusterCount && targetTrees > 0; c++)
                            {
                                Vector3 clusterPos = hit.point + new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
                                if (tempMC.Raycast(new Ray(clusterPos + Vector3.up * 50f, Vector3.down), out RaycastHit cHit, 100f))
                                {
                                    GameObject clusterTree = Instantiate(treePrefab, cHit.point, Quaternion.Euler(0, Random.Range(0, 360), 0), hw.transform);
                                    clusterTree.name = "Forestry_Tree";
                                    clusterTree.transform.localScale = treePrefab.transform.localScale * Random.Range(0.5f, 1.2f);
                                    targetTrees--;
                                }
                            }
                        }
                    }
                }

                // Delete the safe physics clone
                DestroyImmediate(tempPhysicsHitbox);
            }
        }

        private void OptimizeWorld(GameObject root)
        {
            if (logProgress) Debug.Log("[FastWorld] Optimizing for performance (Batching Static only)...");
            
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.gameObject != null)
                {
#if UNITY_EDITOR
                    UnityEditor.GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, UnityEditor.StaticEditorFlags.BatchingStatic);
#else
                    renderer.gameObject.isStatic = true;
#endif
                }
            }
        }

        [ContextMenu("Clear World")]
        public void ClearFastWorld()
        {
            if (activeWorldRoot != null)
                DestroyImmediate(activeWorldRoot);

            Transform existingRoot = transform.Find("Organic_FCG_World");
            if (existingRoot != null)
                DestroyImmediate(existingRoot.gameObject);

            CleanupFCGArtifacts();

            if (logProgress)
                Debug.Log("[FastWorld] Cleared.");
        }

        private void CleanupFCGArtifacts()
        {
            var allObjects = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var toDestroy = new List<GameObject>();

            foreach (var t in allObjects)
            {
                if (t != null && t.gameObject.name.Contains("City-Maker-Garbage"))
                    toDestroy.Add(t.gameObject);
            }

            foreach (var go in toDestroy)
            {
                if (go != null)
                    DestroyImmediate(go);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Underground.Vehicle
{
    /// <summary>
    /// Runtime catalog that discovers and indexes all VehicleDefinition assets.
    /// This is the new data-driven replacement for the hardcoded PlayerCarCatalog arrays.
    /// 
    /// During the transition period, both systems coexist:
    /// - PlayerCarCatalog handles legacy prefab lookup and ownership
    /// - VehicleDefinitionCatalog handles lore-friendly definitions and stats
    /// 
    /// After full migration, PlayerCarCatalog can delegate entirely to this class.
    /// </summary>
    public class VehicleDefinitionCatalog : MonoBehaviour
    {
        private static VehicleDefinitionCatalog _instance;
        public static VehicleDefinitionCatalog Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<VehicleDefinitionCatalog>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("VehicleDefinitionCatalog_AutoInit");
                        _instance = go.AddComponent<VehicleDefinitionCatalog>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Auto-Discovery")]
        [Tooltip("Root folder to search for VehicleDefinition assets.")]
        [SerializeField] private string searchRoot = VehicleConstants.VehiclesRoot;

        [Header("Manual Override")]
        [Tooltip("Optionally drag definitions here instead of auto-discovery.")]
        [SerializeField] private VehicleDefinition[] manualDefinitions;

        private Dictionary<string, VehicleDefinition> _lookup;
        private VehicleDefinition[] _allDefinitions;
        private bool _initialized;

        /// <summary>All discovered VehicleDefinition assets.</summary>
        public IReadOnlyList<VehicleDefinition> AllDefinitions
        {
            get
            {
                EnsureInitialized();
                return _allDefinitions;
            }
        }

        /// <summary>Number of vehicles in the catalog.</summary>
        public int Count
        {
            get
            {
                EnsureInitialized();
                return _allDefinitions.Length;
            }
        }

        private void Awake()
        {
            Instance = this;
            EnsureInitialized();
        }

        /// <summary>
        /// Tries to find a VehicleDefinition by canonical vehicle ID.
        /// Automatically resolves legacy IDs via VehicleRoster.
        /// </summary>
        public bool TryGetDefinition(string vehicleId, out VehicleDefinition definition)
        {
            EnsureInitialized();

            // Direct lookup first
            if (_lookup.TryGetValue(vehicleId, out definition))
            {
                return true;
            }

            // Try legacy migration
            string migrated = VehicleRoster.MigrateLegacyId(vehicleId);
            if (migrated != vehicleId && _lookup.TryGetValue(migrated, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }

        /// <summary>
        /// Returns the VehicleDefinition for the given ID, or null if not found.
        /// </summary>
        public VehicleDefinition GetDefinition(string vehicleId)
        {
            TryGetDefinition(vehicleId, out VehicleDefinition def);
            return def;
        }

        /// <summary>
        /// Returns the hero car definition (Solstice Type-S).
        /// </summary>
        public VehicleDefinition GetHeroDefinition()
        {
            TryGetDefinition(VehicleRoster.HeroCarId, out VehicleDefinition def);
            return def;
        }

        /// <summary>
        /// Returns all definitions matching a given archetype.
        /// </summary>
        public List<VehicleDefinition> GetByArchetype(VehicleArchetype archetype)
        {
            EnsureInitialized();
            List<VehicleDefinition> result = new List<VehicleDefinition>();
            for (int i = 0; i < _allDefinitions.Length; i++)
            {
                if (_allDefinitions[i].archetype == archetype)
                {
                    result.Add(_allDefinitions[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns all definitions matching a given drivetrain.
        /// </summary>
        public List<VehicleDefinition> GetByDrivetrain(DrivetrainType drivetrain)
        {
            EnsureInitialized();
            List<VehicleDefinition> result = new List<VehicleDefinition>();
            for (int i = 0; i < _allDefinitions.Length; i++)
            {
                if (_allDefinitions[i].drivetrain == drivetrain)
                {
                    result.Add(_allDefinitions[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// Forces a re-scan of all definitions. Useful after editor asset generation.
        /// </summary>
        public void Refresh()
        {
            _initialized = false;
            EnsureInitialized();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────────────

        private void EnsureInitialized()
        {
            if (_initialized) return;

            _lookup = new Dictionary<string, VehicleDefinition>();

            if (manualDefinitions != null && manualDefinitions.Length > 0)
            {
                // Use manually assigned definitions
                _allDefinitions = manualDefinitions;
            }
            else
            {
                // Auto-discover from Resources or AssetDatabase
                _allDefinitions = DiscoverDefinitions();
            }

            for (int i = 0; i < _allDefinitions.Length; i++)
            {
                VehicleDefinition def = _allDefinitions[i];
                if (def == null || string.IsNullOrEmpty(def.vehicleId)) continue;

                if (!_lookup.ContainsKey(def.vehicleId))
                {
                    _lookup[def.vehicleId] = def;
                }
                else
                {
                    Debug.LogWarning($"[VehicleDefinitionCatalog] Duplicate vehicleId '{def.vehicleId}' — skipping duplicate.");
                }
            }

            _initialized = true;
            Debug.Log($"[VehicleDefinitionCatalog] Initialized with {_lookup.Count} vehicle(s).");
        }

        private VehicleDefinition[] DiscoverDefinitions()
        {
            // In builds, use Resources.LoadAll or Addressables.
            // For now, load from Resources as a safe runtime path.
            VehicleDefinition[] fromResources = Resources.LoadAll<VehicleDefinition>("");
            if (fromResources != null && fromResources.Length > 0)
            {
                return fromResources;
            }

#if UNITY_EDITOR
            // In editor, scan the asset database from both roots
            string[] searchRoots = new[] { searchRoot, "Assets/Vehicles" };
            List<string> allGuids = new List<string>();
            foreach (string root in searchRoots)
            {
                if (UnityEditor.AssetDatabase.IsValidFolder(root))
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets(
                        "t:VehicleDefinition",
                        new[] { root });
                    allGuids.AddRange(guids);
                }
            }

            // Deduplicate by GUID
            HashSet<string> seen = new HashSet<string>();
            List<VehicleDefinition> results = new List<VehicleDefinition>();
            foreach (string guid in allGuids)
            {
                if (!seen.Add(guid)) continue;
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                VehicleDefinition def = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path);
                if (def != null)
                {
                    results.Add(def);
                }
            }

            return results.ToArray();
#else
            return System.Array.Empty<VehicleDefinition>();
#endif
        }
    }
}

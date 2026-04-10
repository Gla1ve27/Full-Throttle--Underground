using System.Collections.Generic;
using UnityEngine;
using Underground.Progression;
using Underground.Save;

namespace Underground.Vehicle
{
    /// <summary>
    /// Part 5 — Visual Kit Manager
    /// 
    /// Manages visual kit upgrades for any vehicle. Determines which kit (if any)
    /// should override the base visual prefab, applies handling modifiers,
    /// and provides queries for owned/active kits.
    /// 
    /// Works alongside PlayerCarAppearanceController — this component is responsible
    /// for the "what kit?" decision, while the appearance controller handles the
    /// actual visual swap mechanics.
    /// 
    /// Usage:
    ///  1. Attach to the vehicle root (same object as VehicleDynamicsController)
    ///  2. Assign the VehicleDefinition (or auto-resolved from catalog)
    ///  3. Call ResolveActiveKit() to determine which kit to apply
    ///  4. PlayerCarAppearanceController reads ActiveKit to decide the visual prefab
    /// </summary>
    public class VisualKitManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Configuration
        // ─────────────────────────────────────────────────────────────────────

        [Header("Data")]
        [SerializeField] private VehicleDefinition vehicleDefinition;

        [Header("References")]
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private VehicleDynamicsController dynamics;

        // ─────────────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────────────

        private UpgradeDefinition _activeKit;
        private UpgradeDefinition _previousKit;
        private readonly List<UpgradeDefinition> _ownedKits = new List<UpgradeDefinition>();
        private readonly List<UpgradeDefinition> _availableKits = new List<UpgradeDefinition>();

        // ─────────────────────────────────────────────────────────────────────
        //  Public Properties
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Currently active visual kit, or null if base model is active.</summary>
        public UpgradeDefinition ActiveKit => _activeKit;

        /// <summary>The visual prefab path to use (kit override or base).</summary>
        public string ActiveVisualPrefabPath
        {
            get
            {
                if (_activeKit != null && !string.IsNullOrEmpty(_activeKit.visualPrefabOverridePath))
                {
                    return _activeKit.visualPrefabOverridePath;
                }

                return vehicleDefinition != null ? vehicleDefinition.visualPrefabPath : null;
            }
        }

        /// <summary>True if a visual kit is currently overriding the base model.</summary>
        public bool HasActiveKit => _activeKit != null;

        /// <summary>All visual kits available for this vehicle (owned or not).</summary>
        public IReadOnlyList<UpgradeDefinition> AvailableKits => _availableKits;

        /// <summary>Only the visual kits the player currently owns for this vehicle.</summary>
        public IReadOnlyList<UpgradeDefinition> OwnedKits => _ownedKits;

        /// <summary>Fires when the active kit changes.</summary>
        public event System.Action<UpgradeDefinition> KitChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            RefreshAvailableKits();
            ResolveActiveKit();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the vehicle definition and refreshes kits.
        /// Call this when the active car changes.
        /// </summary>
        public void SetVehicleDefinition(VehicleDefinition definition)
        {
            vehicleDefinition = definition;
            RefreshAvailableKits();
            ResolveActiveKit();
        }

        /// <summary>
        /// Re-evaluates which kit should be active based on ownership.
        /// Returns true if the active kit changed.
        /// </summary>
        public bool ResolveActiveKit()
        {
            _previousKit = _activeKit;
            RefreshOwnedKits();

            if (_ownedKits.Count == 0)
            {
                _activeKit = null;
            }
            else
            {
                // Select highest-tier owned kit (by reputation required, then cost)
                _activeKit = _ownedKits[0];
                for (int i = 1; i < _ownedKits.Count; i++)
                {
                    if (IsHigherTier(_ownedKits[i], _activeKit))
                    {
                        _activeKit = _ownedKits[i];
                    }
                }
            }

            bool changed = _activeKit != _previousKit;
            if (changed)
            {
                ApplyHandlingModifier();
                KitChanged?.Invoke(_activeKit);
            }

            return changed;
        }

        /// <summary>
        /// Explicitly selects a specific kit (for future UI support).
        /// Pass null to revert to base model.
        /// </summary>
        public void SelectKit(UpgradeDefinition kit)
        {
            if (kit != null && kit.category != UpgradeCategory.VisualKit)
            {
                Debug.LogWarning($"[VisualKitManager] {kit.upgradeId} is not a VisualKit.");
                return;
            }

            _previousKit = _activeKit;
            _activeKit = kit;

            if (_activeKit != _previousKit)
            {
                ApplyHandlingModifier();
                KitChanged?.Invoke(_activeKit);
            }
        }

        /// <summary>
        /// Checks if a specific kit is owned by the player.
        /// </summary>
        public bool IsKitOwned(UpgradeDefinition kit)
        {
            if (kit == null) return false;
            return _ownedKits.Contains(kit);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Kit Discovery
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Refreshes the list of available visual kits from VehicleDefinition.
        /// </summary>
        public void RefreshAvailableKits()
        {
            _availableKits.Clear();

            if (vehicleDefinition == null || vehicleDefinition.availableUpgrades == null)
            {
                return;
            }

            for (int i = 0; i < vehicleDefinition.availableUpgrades.Length; i++)
            {
                UpgradeDefinition upgrade = vehicleDefinition.availableUpgrades[i];
                if (upgrade != null && upgrade.category == UpgradeCategory.VisualKit)
                {
                    _availableKits.Add(upgrade);
                }
            }
        }

        /// <summary>
        /// Checks which visual kits the player currently owns.
        /// </summary>
        private void RefreshOwnedKits()
        {
            _ownedKits.Clear();

            if (progressManager == null || _availableKits.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _availableKits.Count; i++)
            {
                UpgradeDefinition kit = _availableKits[i];
                if (kit == null) continue;

                // Check ownership through progress manager
                if (IsUpgradeOwned(kit))
                {
                    _ownedKits.Add(kit);
                }
            }
        }

        /// <summary>
        /// Queries the progress manager to check if a specific upgrade is owned.
        /// Uses the existing global purchased upgrade tracking.
        /// Future versions may support per-car upgrade lists.
        /// </summary>
        private bool IsUpgradeOwned(UpgradeDefinition upgrade)
        {
            if (progressManager == null || upgrade == null)
            {
                return false;
            }

            return progressManager.HasPurchasedUpgrade(upgrade.upgradeId);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Handling Modifier
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the handling modifier from the active kit (if any).
        /// This is additive on top of the base stats.
        /// </summary>
        private void ApplyHandlingModifier()
        {
            if (dynamics == null)
            {
                return;
            }

            // Re-initialize from base to clear previous kit modifier
            if (dynamics.BaseStats != null)
            {
                dynamics.Initialize(dynamics.BaseStats);
            }

            // Apply the kit's handling modifier if present
            if (_activeKit != null && _activeKit.handlingModifier != null)
            {
                dynamics.ApplyUpgrade(_activeKit.handlingModifier);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns true if 'candidate' is a higher tier than 'current'.</summary>
        private static bool IsHigherTier(UpgradeDefinition candidate, UpgradeDefinition current)
        {
            if (candidate.reputationRequired != current.reputationRequired)
            {
                return candidate.reputationRequired > current.reputationRequired;
            }

            return candidate.cost > current.cost;
        }

        private void ResolveReferences()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (dynamics == null)
            {
                dynamics = GetComponent<VehicleDynamicsController>();
            }
        }
    }
}

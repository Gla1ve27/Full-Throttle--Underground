using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FullThrottle.SacredCore.Vehicle
{
    /// <summary>
    /// Applies the selected car's visual identity onto a shared drivable vehicle rig.
    /// This keeps FT car truth while still allowing existing roster models to be reused.
    /// </summary>
    public sealed class FTVehicleVisualApplier : MonoBehaviour, IFTVehicleDefinitionReceiver
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private bool clearExistingImportedVisual = true;
        [SerializeField] private string importedVisualName = "ImportedVisual";
        [SerializeField] private bool preferLegacyAppearanceControllerInEditor = true;

        private bool garagePresentationMode;

        public void SetGaragePresentationMode(bool enabled)
        {
            garagePresentationMode = enabled;
        }

        public void ApplyDefinition(FTCarDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (preferLegacyAppearanceControllerInEditor)
            {
                Underground.Vehicle.PlayerCarAppearanceController legacyAppearance =
                    GetComponent<Underground.Vehicle.PlayerCarAppearanceController>();
                bool addedLegacyAppearance = legacyAppearance == null;
                if (legacyAppearance == null)
                {
                    legacyAppearance = gameObject.AddComponent<Underground.Vehicle.PlayerCarAppearanceController>();
                }

                legacyAppearance.SetShowroomPresentationMode(garagePresentationMode);
                if (legacyAppearance.ApplyAppearance(definition.carId))
                {
                    if (addedLegacyAppearance)
                    {
                        legacyAppearance.enabled = false;
                    }

                    Debug.Log($"[SacredCore] Applied legacy roster visual for {definition.carId} on {name}.");
                    return;
                }

                if (addedLegacyAppearance)
                {
                    Destroy(legacyAppearance);
                }
            }
#endif

            GameObject visualPrefab = ResolveVisualPrefab(definition);
            if (visualPrefab == null)
            {
                Debug.LogWarning($"[SacredCore] No visual prefab assigned for car={definition.carId}. Using shared vehicle body.");
                return;
            }

            Transform root = ResolveModelRoot();
            if (root == null)
            {
                return;
            }

            if (clearExistingImportedVisual)
            {
                ClearImportedVisuals(root);
            }

            GameObject visual = Instantiate(visualPrefab, root, false);
            visual.name = importedVisualName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            StripPhysics(visual);
            Debug.Log($"[SacredCore] Applied FT visual prefab for {definition.carId} on {name}.");
        }

        private GameObject ResolveVisualPrefab(FTCarDefinition definition)
        {
            if (definition.visualPrefab != null)
            {
                return definition.visualPrefab;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(definition.editorVisualPrefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(definition.editorVisualPrefabPath);
            }
#endif

            return null;
        }

        private Transform ResolveModelRoot()
        {
            if (modelRoot != null)
            {
                return modelRoot;
            }

            Transform found = transform.Find("ModelRoot");
            if (found != null)
            {
                modelRoot = found;
                return modelRoot;
            }

            modelRoot = transform;
            return modelRoot;
        }

        private void ClearImportedVisuals(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name != importedVisualName && child.name != "FT_ImportedVisual")
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void StripPhysics(GameObject visual)
        {
            Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Destroy(bodies[i]);
            }

            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }
        }
    }
}

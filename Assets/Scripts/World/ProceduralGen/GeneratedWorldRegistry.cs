using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// Tracks all root GameObjects spawned by the procedural generation pipeline.
    /// Enables clean teardown and re-generation without orphaned objects.
    /// </summary>
    public class GeneratedWorldRegistry : MonoBehaviour
    {
        [Header("Registry State")]
        [SerializeField, Tooltip("Read-only: currently tracked spawned roots")]
        private List<GameObject> trackedRoots = new();

        /// <summary>
        /// Read-only view of all tracked roots.
        /// </summary>
        public IReadOnlyList<GameObject> TrackedRoots => trackedRoots;

        /// <summary>
        /// Register a spawned root so it can be cleaned up later.
        /// </summary>
        public void Register(GameObject go)
        {
            if (go != null && !trackedRoots.Contains(go))
            {
                trackedRoots.Add(go);
                Debug.Log($"[Registry] Tracked: {go.name}");
            }
        }

        /// <summary>
        /// Destroy all tracked roots and clear the registry.
        /// Safe to call multiple times.
        /// </summary>
        [ContextMenu("Clear All Generated")]
        public void ClearAll()
        {
            int count = 0;
            for (int i = trackedRoots.Count - 1; i >= 0; i--)
            {
                if (trackedRoots[i] != null)
                {
                    DestroyImmediate(trackedRoots[i]);
                    count++;
                }
            }

            trackedRoots.Clear();
            Debug.Log($"[Registry] Cleared {count} generated roots.");
        }

        /// <summary>
        /// Remove null entries (objects that were destroyed externally).
        /// </summary>
        public void PruneNulls()
        {
            trackedRoots.RemoveAll(go => go == null);
        }
    }
}

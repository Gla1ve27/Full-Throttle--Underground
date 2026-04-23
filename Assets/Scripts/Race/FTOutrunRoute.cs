using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    public sealed class FTOutrunRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private bool autoUseChildren = true;

        public Transform[] Waypoints
        {
            get
            {
                if (autoUseChildren && (waypoints == null || waypoints.Length == 0))
                {
                    RebuildFromChildren();
                }

                return waypoints;
            }
        }

        [ContextMenu("Rebuild From Children")]
        public void RebuildFromChildren()
        {
            waypoints = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                waypoints[i] = transform.GetChild(i);
            }

            Debug.Log($"[SacredCore] Outrun route '{name}' rebuilt with {waypoints.Length} waypoints.");
        }
    }
}

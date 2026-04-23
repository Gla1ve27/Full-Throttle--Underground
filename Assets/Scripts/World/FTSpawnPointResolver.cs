using UnityEngine;

namespace FullThrottle.SacredCore.World
{
    /// <summary>
    /// Resolves deterministic spawn poses from authored scene spawn points.
    /// </summary>
    [DefaultExecutionOrder(-7900)]
    public sealed class FTSpawnPointResolver : MonoBehaviour
    {
        [SerializeField] private string fallbackSpawnPointId = "player_start";
        [SerializeField] private bool preferGroundedDuplicate = true;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundProbeHeight = 80f;
        [SerializeField] private float groundProbeDepth = 240f;
        [SerializeField] private float spawnGroundOffset = 0.65f;

        private void Awake()
        {
            FullThrottle.SacredCore.Runtime.FTServices.Register(this);
        }

        public Pose Resolve(string spawnPointId)
        {
            string requested = string.IsNullOrWhiteSpace(spawnPointId) ? fallbackSpawnPointId : spawnPointId;
            FTSpawnPoint[] points = FindObjectsOfType<FTSpawnPoint>(true);
            FTSpawnPoint fallback = null;
            FTSpawnPoint firstRequested = null;

            for (int i = 0; i < points.Length; i++)
            {
                FTSpawnPoint point = points[i];
                if (point == null) continue;

                if (point.defaultForScene && fallback == null)
                {
                    fallback = point;
                }

                if (point.spawnPointId == requested)
                {
                    firstRequested ??= point;
                    if (!preferGroundedDuplicate)
                    {
                        Pose pose = BuildPose(point);
                        Debug.Log($"[SacredCore] Spawn resolver matched '{requested}' at {pose.position}. ground=unchecked");
                        return pose;
                    }

                    if (TryBuildGroundedPose(point, out Pose groundedPose, out RaycastHit hit))
                    {
                        Debug.Log($"[SacredCore] Spawn resolver matched '{requested}' at {groundedPose.position}. ground={hit.point}");
                        return groundedPose;
                    }
                }
            }

            if (firstRequested != null)
            {
                Pose pose = BuildPose(firstRequested);
                Debug.LogWarning($"[SacredCore] Spawn '{requested}' has no grounded duplicate. Using authored pose {pose.position}.");
                return pose;
            }

            if (fallback != null)
            {
                if (TryBuildGroundedPose(fallback, out Pose groundedFallback, out RaycastHit hit))
                {
                    Debug.LogWarning($"[SacredCore] Spawn '{requested}' missing. Using grounded scene default '{fallback.spawnPointId}' at {groundedFallback.position}. ground={hit.point}");
                    return groundedFallback;
                }

                Debug.LogWarning($"[SacredCore] Spawn '{requested}' missing. Using ungrounded scene default '{fallback.spawnPointId}'.");
                return BuildPose(fallback);
            }

            Debug.LogWarning($"[SacredCore] No FTSpawnPoint found for '{requested}'. Using world origin.");
            return new Pose(Vector3.zero, Quaternion.identity);
        }

        private Pose BuildPose(FTSpawnPoint point)
        {
            return new Pose(point.transform.position, point.transform.rotation);
        }

        private bool TryBuildGroundedPose(FTSpawnPoint point, out Pose pose, out RaycastHit hit)
        {
            Vector3 origin = point.transform.position + Vector3.up * Mathf.Max(1f, groundProbeHeight);
            float distance = Mathf.Max(1f, groundProbeHeight + groundProbeDepth);
            if (Physics.Raycast(origin, Vector3.down, out hit, distance, groundMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 position = point.transform.position;
                position.y = hit.point.y + Mathf.Max(0.05f, spawnGroundOffset);
                pose = new Pose(position, point.transform.rotation);
                return true;
            }

            pose = BuildPose(point);
            return false;
        }
    }
}

using UnityEngine;

namespace FullThrottle.SacredCore.HUD
{
    public sealed class FTMinimapDirector : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private float worldToMapScale = 0.25f;

        private Vector3 origin;

        private void Awake()
        {
            origin = target != null ? target.position : Vector3.zero;
        }

        private void LateUpdate()
        {
            if (target == null || playerMarker == null)
            {
                return;
            }

            Vector3 delta = target.position - origin;
            playerMarker.anchoredPosition = new Vector2(delta.x, delta.z) * worldToMapScale;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, -target.eulerAngles.y);
        }

        public void SetTarget(Transform newTarget)
        {
            if (target == newTarget)
            {
                return;
            }

            target = newTarget;
            if (target != null)
            {
                origin = target.position;
                Debug.Log($"[SacredCore] Minimap target={target.name}.");
            }
        }
    }
}

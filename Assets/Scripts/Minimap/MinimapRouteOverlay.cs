using UnityEngine;

namespace FullThrottle.Minimap
{
    /// <summary>
    /// Draws a route on top of the minimap using a LineRenderer under a second top-down overlay camera,
    /// or in the same minimap world if the overlay is on a visible minimap layer.
    /// Put this on a GameObject with a LineRenderer.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class MinimapRouteOverlay : MonoBehaviour
    {
        [SerializeField] private Transform[] routePoints;
        [SerializeField] private Vector3 lineOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private bool refreshEveryFrame;
        [SerializeField] private bool configureLineRenderer = true;
        [SerializeField] private bool useWorldSpace = true;
        [SerializeField] private float lineWidth = 3f;
        [SerializeField] private Color routeColor = new Color(0.72f, 0.2f, 1f, 1f);
        [SerializeField] private Material routeMaterial;

        private LineRenderer lineRenderer;
        private Vector3[] cachedPositions = System.Array.Empty<Vector3>();

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            ApplyLineRendererSettings();
            Rebuild();
        }

        private void LateUpdate()
        {
            if (refreshEveryFrame)
            {
                Rebuild();
            }
        }

        public void SetRoute(System.Collections.Generic.IReadOnlyList<Transform> points)
        {
            routePoints = new Transform[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                routePoints[i] = points[i];
            }
            Rebuild();
        }

        public void Rebuild()
        {
            if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
            ApplyLineRendererSettings();

            if (routePoints == null || routePoints.Length == 0)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            int validCount = 0;
            for (int i = 0; i < routePoints.Length; i++)
            {
                if (routePoints[i] == null) continue;
                validCount++;
            }

            if (cachedPositions.Length != validCount)
            {
                cachedPositions = new Vector3[validCount];
            }

            int writeIndex = 0;
            for (int i = 0; i < routePoints.Length; i++)
            {
                if (routePoints[i] == null) continue;
                cachedPositions[writeIndex] = routePoints[i].position + lineOffset;
                writeIndex++;
            }

            lineRenderer.positionCount = validCount;
            lineRenderer.SetPositions(cachedPositions);
        }

        private void ApplyLineRendererSettings()
        {
            if (!configureLineRenderer || lineRenderer == null)
            {
                return;
            }

            lineRenderer.useWorldSpace = useWorldSpace;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.startColor = routeColor;
            lineRenderer.endColor = routeColor;
            if (routeMaterial != null)
            {
                lineRenderer.sharedMaterial = routeMaterial;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            lineRenderer = GetComponent<LineRenderer>();
            ApplyLineRendererSettings();
            if (!Application.isPlaying)
            {
                Rebuild();
            }
        }
#endif
    }
}

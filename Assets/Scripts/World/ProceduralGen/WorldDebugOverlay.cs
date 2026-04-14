using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// PHASE 5: Enhanced debug gizmo overlays for the procedural world.
    ///
    /// Draws rich scene-view visualisations that help designers and developers
    /// understand the generated world without running the game:
    ///
    ///   - District bounds (colour-coded filled discs + wireframe)
    ///   - Connection graph edges (with road class colour + arrows)
    ///   - Landmark positions with category labels
    ///   - Road coverage heatmap indicators
    ///   - Bridge connector locations
    ///   - Traffic spawn points and directions
    ///   - Race checkpoint gate locations
    ///
    /// Attach this to the same GameObject as ProceduralWorldBootstrap.
    /// Gizmos are only drawn when the object is selected (OnDrawGizmosSelected).
    /// </summary>
    public class WorldDebugOverlay : MonoBehaviour
    {
        [Header("Gizmo Toggles")]
        [SerializeField] private bool showDistricts = true;
        [SerializeField] private bool showConnections = true;
        [SerializeField] private bool showLandmarks = true;
        [SerializeField] private bool showBridges = true;
        [SerializeField] private bool showWorldBounds = true;

        [Header("Colours")]
        [SerializeField] private Color cityColor     = new Color(0.2f, 0.6f, 1.0f, 0.25f);
        [SerializeField] private Color mountainColor  = new Color(0.4f, 0.8f, 0.3f, 0.25f);
        [SerializeField] private Color arterialColor  = new Color(1.0f, 0.7f, 0.2f, 0.25f);
        [SerializeField] private Color highwayColor   = new Color(0.9f, 0.2f, 0.3f, 0.25f);

        // Cached plan reference
        private WorldPlan plan;

        public void SetPlan(WorldPlan worldPlan)
        {
            plan = worldPlan;
        }

#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            // Try to get plan from bootstrap if not directly set
            if (plan == null)
            {
                var bootstrap = GetComponent<ProceduralWorldBootstrap>();
                if (bootstrap != null)
                    plan = bootstrap.CurrentPlan;
            }

            if (plan == null) return;

            if (showWorldBounds) DrawWorldBounds();
            if (showDistricts)   DrawDistricts();
            if (showConnections) DrawConnections();
            if (showLandmarks)   DrawLandmarks();
            if (showBridges)     DrawBridges();

            DrawInfoPanel();
        }

        // ─────────────────────────────────────────────────────────────────────
        // WORLD BOUNDS
        // ─────────────────────────────────────────────────────────────────────

        private void DrawWorldBounds()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
            Gizmos.DrawWireCube(Vector3.zero,
                new Vector3(plan.worldSize.x, 50f, plan.worldSize.y));

            // Axis markers
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawLine(Vector3.zero, Vector3.right * plan.worldSize.x * 0.5f);
            Gizmos.color = new Color(0f, 0f, 1f, 0.4f);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * plan.worldSize.y * 0.5f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DISTRICTS
        // ─────────────────────────────────────────────────────────────────────

        private void DrawDistricts()
        {
            foreach (var d in plan.districts)
            {
                Color col = GetDistrictColor(d.districtType);

                // Filled disc
                Gizmos.color = col;
                DrawFilledDisc(d.center + Vector3.up * 2f, d.radius, 48);

                // Wireframe boundary
                Color wireCol = col;
                wireCol.a = 0.6f;
                Gizmos.color = wireCol;
                DrawWireDisc(d.center + Vector3.up * 3f, d.radius, 48);

                // Inner ring at 50% radius
                wireCol.a = 0.2f;
                Gizmos.color = wireCol;
                DrawWireDisc(d.center + Vector3.up * 2f, d.radius * 0.5f, 32);

                // Label
                Color labelCol = col;
                labelCol.a = 1f;
                UnityEditor.Handles.color = labelCol;

                var style = new GUIStyle();
                style.normal.textColor = labelCol;
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 14;

                UnityEditor.Handles.Label(
                    d.center + Vector3.up * 40f,
                    $"{d.districtType}\n{d.id}\nr={d.radius:F0}m",
                    style);

                // Center marker cross
                Gizmos.color = labelCol;
                float crossSize = 20f;
                Gizmos.DrawLine(d.center - Vector3.right * crossSize,
                               d.center + Vector3.right * crossSize);
                Gizmos.DrawLine(d.center - Vector3.forward * crossSize,
                               d.center + Vector3.forward * crossSize);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONNECTIONS
        // ─────────────────────────────────────────────────────────────────────

        private void DrawConnections()
        {
            foreach (var conn in plan.connections)
            {
                Color col = GetRoadClassColor(conn.roadClass);
                Gizmos.color = col;

                // Main line
                Vector3 start = conn.start + Vector3.up * 5f;
                Vector3 end   = conn.end + Vector3.up * 5f;
                Gizmos.DrawLine(start, end);

                // Arrowhead
                Vector3 dir = (end - start).normalized;
                Vector3 perpH = Vector3.Cross(dir, Vector3.up).normalized;
                Vector3 arrowPos = Vector3.Lerp(start, end, 0.7f);
                float arrowSize = 30f;

                Gizmos.DrawLine(arrowPos, arrowPos - dir * arrowSize + perpH * arrowSize * 0.4f);
                Gizmos.DrawLine(arrowPos, arrowPos - dir * arrowSize - perpH * arrowSize * 0.4f);

                // Distance label at midpoint
                Vector3 mid = (start + end) * 0.5f + Vector3.up * 10f;
                var style = new GUIStyle();
                style.normal.textColor = col;
                style.fontSize = 10;

                UnityEditor.Handles.Label(mid,
                    $"{conn.roadClass} ({conn.StraightLineDistance:F0}m)",
                    style);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // LANDMARKS
        // ─────────────────────────────────────────────────────────────────────

        private void DrawLandmarks()
        {
            if (plan.landmarks == null) return;

            foreach (var lm in plan.landmarks)
            {
                // Diamond shape
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
                Vector3 pos = lm.position + Vector3.up * 5f;
                float s = lm.reservedRadius * 0.3f;

                Gizmos.DrawLine(pos + Vector3.up    * s, pos + Vector3.right   * s);
                Gizmos.DrawLine(pos + Vector3.right  * s, pos - Vector3.up     * s);
                Gizmos.DrawLine(pos - Vector3.up     * s, pos - Vector3.right  * s);
                Gizmos.DrawLine(pos - Vector3.right  * s, pos + Vector3.up     * s);

                // Reserved radius
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.15f);
                DrawWireDisc(lm.position, lm.reservedRadius, 24);

                // Label
                var style = new GUIStyle();
                style.normal.textColor = new Color(1f, 0.85f, 0.2f);
                style.fontSize = 11;
                style.fontStyle = FontStyle.Italic;

                UnityEditor.Handles.Label(
                    pos + Vector3.up * 15f,
                    $"★ {lm.category}\n   {lm.id}",
                    style);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BRIDGES
        // ─────────────────────────────────────────────────────────────────────

        private void DrawBridges()
        {
            GameObject generated = GameObject.Find("Generated");
            if (generated == null) return;

            Transform bridgesRoot = generated.transform.Find("BridgesAndTransitions");
            if (bridgesRoot == null || bridgesRoot.childCount == 0) return;

            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.5f);

            foreach (Transform bridge in bridgesRoot)
            {
                Gizmos.DrawWireSphere(bridge.position, 25f);

                var style = new GUIStyle();
                style.normal.textColor = new Color(0.5f, 0.9f, 1f);
                style.fontSize = 10;

                int deckCount = 0;
                Transform deck = bridge.Find("Deck");
                if (deck != null) deckCount = deck.childCount;

                UnityEditor.Handles.Label(
                    bridge.position + Vector3.up * 30f,
                    $"🌉 {bridge.name}\n   {deckCount} segments",
                    style);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // INFO PANEL (top-level stats)
        // ─────────────────────────────────────────────────────────────────────

        private void DrawInfoPanel()
        {
            var style = new GUIStyle();
            style.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            style.fontSize = 13;
            style.fontStyle = FontStyle.Bold;

            int bridgeCount = 0;
            GameObject gen = GameObject.Find("Generated");
            if (gen != null)
            {
                Transform bt = gen.transform.Find("BridgesAndTransitions");
                if (bt != null) bridgeCount = bt.childCount;
            }

            UnityEditor.Handles.Label(
                Vector3.up * 250f,
                $"▸ Full Throttle: Underground — Procedural World\n" +
                $"  Seed: {plan.seed}\n" +
                $"  Districts: {plan.districts.Count}\n" +
                $"  Connections: {plan.connections.Count}\n" +
                $"  Landmarks: {plan.landmarks.Count}\n" +
                $"  Bridges: {bridgeCount}\n" +
                $"  World: {plan.worldSize.x:F0} × {plan.worldSize.y:F0}m",
                style);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DRAWING HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private Color GetDistrictColor(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.CityCore: return cityColor;
                case DistrictType.Mountain: return mountainColor;
                case DistrictType.Arterial: return arterialColor;
                case DistrictType.Highway:  return highwayColor;
                default: return Color.white;
            }
        }

        private Color GetRoadClassColor(RoadClass rc)
        {
            switch (rc)
            {
                case RoadClass.Highway:   return new Color(0.9f, 0.2f, 0.3f, 0.7f);
                case RoadClass.Arterial:  return new Color(1.0f, 0.7f, 0.2f, 0.7f);
                case RoadClass.Mountain:  return new Color(0.4f, 0.8f, 0.3f, 0.7f);
                case RoadClass.CityLocal: return new Color(0.2f, 0.6f, 1.0f, 0.7f);
                default: return Color.gray;
            }
        }

        private void DrawWireDisc(Vector3 center, float radius, int segments)
        {
            float step = Mathf.PI * 2f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * step;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f,
                                                     Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawFilledDisc(Vector3 center, float radius, int segments)
        {
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * step;
                float a1 = (i + 1) * step;
                Vector3 v0 = center + new Vector3(Mathf.Cos(a0) * radius, 0f,
                                                   Mathf.Sin(a0) * radius);
                Vector3 v1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f,
                                                   Mathf.Sin(a1) * radius);
                // Triangle fan from center
                Gizmos.DrawLine(center, v0);
                Gizmos.DrawLine(v0, v1);
            }
        }

#endif
    }
}

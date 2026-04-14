using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    public class BridgeAnchorSystem : MonoBehaviour
    {
        public Transform startAnchor;
        public Transform endAnchor;
        public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float peakHeight = 85f; // Raised for massive bridge span
        public List<Vector3> generatedPoints = new List<Vector3>();

        private void Reset()
        {
            AutoCreateAnchors();
        }

        [ContextMenu("Auto-Create Anchors")]
        public void AutoCreateAnchors()
        {
            if (startAnchor == null)
            {
                Transform existingStart = transform.Find("BridgeStart_Anchor");
                if (existingStart != null)
                {
                    startAnchor = existingStart;
                }
                else
                {
                    GameObject startObj = new GameObject("BridgeStart_Anchor");
                    startObj.transform.SetParent(transform);
                    startObj.transform.localPosition = new Vector3(-1000f, 0f, 0f);
                    startAnchor = startObj.transform;
                }
            }

            if (endAnchor == null)
            {
                Transform existingEnd = transform.Find("BridgeEnd_Anchor");
                if (existingEnd != null)
                {
                    endAnchor = existingEnd;
                }
                else
                {
                    GameObject endObj = new GameObject("BridgeEnd_Anchor");
                    endObj.transform.SetParent(transform);
                    endObj.transform.localPosition = new Vector3(100f, 0f, 0f);
                    endAnchor = endObj.transform;
                }
            }
        }

        public void GenerateBridgeSpline(int resolution = 32)
        {
            // Clear old bridge geometry physical manifestation
            Transform oldGeom = transform.Find("BridgeGeometry");
            if (oldGeom) DestroyImmediate(oldGeom.gameObject);

            GameObject geomRoot = new GameObject("BridgeGeometry");
            geomRoot.transform.SetParent(transform);

            generatedPoints.Clear();
            
            if (startAnchor == null || endAnchor == null)
            {
                AutoCreateAnchors();
            }

            // Extrapolate bridge curve over distance
            for (int i = 0; i <= resolution; i++)
            {
                float t = i / (float)resolution;
                Vector3 p = Vector3.Lerp(startAnchor.position, endAnchor.position, t);
                p.y += heightCurve.Evaluate(t) * peakHeight;
                generatedPoints.Add(p);
            }

            // Spawn geometric struts between generated points
            for (int i = 0; i < generatedPoints.Count - 1; i++)
            {
                Vector3 p1 = generatedPoints[i];
                Vector3 p2 = generatedPoints[i + 1];
                
                GameObject span = GameObject.CreatePrimitive(PrimitiveType.Cube);
                span.transform.SetParent(geomRoot.transform);
                
                Vector3 dir = p2 - p1;
                span.transform.position = p1 + dir * 0.5f;
                if (dir.sqrMagnitude > 0.001f) span.transform.forward = dir.normalized;
                
                // Extremely wide physics road deck
                span.transform.localScale = new Vector3(45, 6, dir.magnitude); 
                
                Material mat = new Material(Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Standard"));
                mat.color = new Color(0.8f, 0.2f, 0.1f); // Golden Gate Bridge Red
                span.GetComponent<Renderer>().sharedMaterial = mat;
            }
            
            Debug.Log($"BridgeAnchorSystem: Physical bridge spawned across {Vector3.Distance(startAnchor.position, endAnchor.position)} units.");
        }
    }
}

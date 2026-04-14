using System;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// Classification of procedural world districts.
    /// Separate from the existing ZoneType enum which controls FCG density parameters.
    /// </summary>
    public enum DistrictType
    {
        Mountain,
        CityCore,
        Arterial,
        Highway
    }

    /// <summary>
    /// Runtime data for a single district in the procedural world plan.
    /// </summary>
    [Serializable]
    public class DistrictPlan
    {
        public string id;
        public DistrictType districtType;
        public Vector3 center;
        public float radius;
        public Bounds bounds;

        /// <summary>
        /// Quick check if a world-space point falls inside this district's bounds.
        /// </summary>
        public bool Contains(Vector3 point)
        {
            return bounds.Contains(point);
        }

        public override string ToString()
        {
            return $"[{id}] {districtType} @ {center} r={radius:F0}";
        }
    }
}

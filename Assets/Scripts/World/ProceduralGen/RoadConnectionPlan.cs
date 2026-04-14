using System;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// Road class for procedural world planning.
    /// Separate from the existing RoadRole enum which is used for post-generation classification.
    /// </summary>
    public enum RoadClass
    {
        Highway,
        Arterial,
        CityLocal,
        Mountain
    }

    /// <summary>
    /// A single edge in the world plan's connection graph.
    /// Represents one planned road corridor between two districts.
    /// </summary>
    [Serializable]
    public class RoadConnectionPlan
    {
        public string fromDistrictId;
        public string toDistrictId;
        public RoadClass roadClass;
        public Vector3 start;
        public Vector3 end;

        /// <summary>
        /// Straight-line distance of this connection (actual spline will be longer).
        /// </summary>
        public float StraightLineDistance => Vector3.Distance(start, end);

        public override string ToString()
        {
            return $"[{roadClass}] {fromDistrictId} → {toDistrictId} ({StraightLineDistance:F0}m)";
        }
    }
}

using System;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// A reserved point in the world plan for future landmark placement
    /// (garages, gas stations, race hubs, event zones, scenic overlooks).
    /// </summary>
    [Serializable]
    public class LandmarkPlan
    {
        public string id;
        public Vector3 position;
        public string category; // e.g. "garage", "gas_station", "race_hub", "overlook"
        public float reservedRadius = 50f;

        public override string ToString()
        {
            return $"[Landmark:{category}] {id} @ {position}";
        }
    }
}

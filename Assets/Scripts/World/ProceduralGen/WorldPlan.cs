using System;
using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// The complete procedural world plan produced by ProceduralWorldPlanner.
    /// Contains district layout, road connection graph, and landmark reservations.
    /// This is pure data — no MonoBehaviour, no scene references.
    /// </summary>
    [Serializable]
    public class WorldPlan
    {
        public int seed;
        public Vector2 worldSize;
        public List<DistrictPlan> districts = new();
        public List<RoadConnectionPlan> connections = new();
        public List<LandmarkPlan> landmarks = new();

        // ─────────────────────────────────────────────────────────────────────
        // QUERIES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Find a district by its unique ID.
        /// </summary>
        public DistrictPlan GetDistrict(string id)
        {
            return districts.Find(d => d.id == id);
        }

        /// <summary>
        /// Find the first district of the given type.
        /// </summary>
        public DistrictPlan GetDistrictByType(DistrictType type)
        {
            return districts.Find(d => d.districtType == type);
        }

        /// <summary>
        /// Get all connections that touch a given district.
        /// </summary>
        public List<RoadConnectionPlan> GetConnectionsFor(string districtId)
        {
            return connections.FindAll(c =>
                c.fromDistrictId == districtId || c.toDistrictId == districtId);
        }

        /// <summary>
        /// Get all connections of a specific road class.
        /// </summary>
        public List<RoadConnectionPlan> GetConnectionsByClass(RoadClass roadClass)
        {
            return connections.FindAll(c => c.roadClass == roadClass);
        }

        /// <summary>
        /// Find a landmark by its unique ID.
        /// </summary>
        public LandmarkPlan GetLandmark(string id)
        {
            return landmarks.Find(lm => lm.id == id);
        }

        /// <summary>
        /// Find all landmarks of a given category.
        /// </summary>
        public List<LandmarkPlan> GetLandmarksByCategory(string category)
        {
            return landmarks.FindAll(lm => lm.category == category);
        }

        /// <summary>
        /// Summary string for debug logging.
        /// </summary>
        public string GetSummary()
        {
            return $"WorldPlan [seed={seed}] " +
                   $"size={worldSize} " +
                   $"districts={districts.Count} " +
                   $"connections={connections.Count} " +
                   $"landmarks={landmarks.Count}";
        }
    }
}

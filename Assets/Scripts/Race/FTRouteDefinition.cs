using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Route Definition", fileName = "FT_Route")]
    public sealed class FTRouteDefinition : ScriptableObject
    {
        public string routeId = "route_citycore_entry";
        public string displayName = "City Core Entry";
        public string districtId = "city_core";
        public float targetLengthKm = 2.4f;
        public List<string> checkpointIds = new();
        [TextArea(2, 5)] public string routeIdentity = "Tight neon streets with short straights and bad exits.";
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Underground.Vehicle
{
    [CreateAssetMenu(
        fileName = "NewWheelCatalog",
        menuName = "Full Throttle/Vehicle/Customization/Wheel Catalog")]
    public class WheelCatalog : ScriptableObject
    {
        [SerializeField] private List<WheelDefinition> wheels = new List<WheelDefinition>();

        public IReadOnlyList<WheelDefinition> Wheels => wheels;

        public bool TryGetWheel(string wheelId, out WheelDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(wheelId))
            {
                return false;
            }

            for (int i = 0; i < wheels.Count; i++)
            {
                WheelDefinition candidate = wheels[i];
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.wheelId, wheelId, System.StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        public WheelDefinition GetFirstCompatible(bool frontAxle)
        {
            for (int i = 0; i < wheels.Count; i++)
            {
                WheelDefinition candidate = wheels[i];
                if (candidate != null && candidate.IsCompatible(frontAxle))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}

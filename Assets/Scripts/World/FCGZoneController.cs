using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    public enum ZoneType { Urban, Highway, Drift, Industrial, Bridge }

    [System.Serializable]
    public class DistrictDefinition
    {
        public string id;
        public ZoneType zoneType;
        public Bounds bounds;
        public float density;
        public float curvatureBias;
        public float widthMultiplier;
        public bool allowLandmarks;
    }

    public class FCGZoneController : MonoBehaviour
    {
        public List<DistrictDefinition> districts = new List<DistrictDefinition>();

        private void Reset()
        {
            LoadDefaultDistricts();
        }

        private void LoadDefaultDistricts()
        {
            if (districts.Count == 0)
            {
                districts.Add(new DistrictDefinition { id = "CoreCity", zoneType = ZoneType.Urban });
                districts.Add(new DistrictDefinition { id = "MainHighway", zoneType = ZoneType.Highway });
                districts.Add(new DistrictDefinition { id = "Outskirts", zoneType = ZoneType.Drift });
                districts.Add(new DistrictDefinition { id = "Speedway", zoneType = ZoneType.Industrial });
                districts.Add(new DistrictDefinition { id = "HeroBridge", zoneType = ZoneType.Bridge });
            }
        }

        public void ApplyDistrictRules()
        {
            LoadDefaultDistricts(); // Runtime safeguard

            foreach (var d in districts)
            {
                switch (d.zoneType)
                {
                    case ZoneType.Urban:
                        d.density = 0.62f;
                        d.curvatureBias = 0.25f;
                        d.widthMultiplier = 1.45f;
                        break;
                    case ZoneType.Highway:
                        d.density = 0.18f;
                        d.curvatureBias = 0.05f;
                        d.widthMultiplier = 2.1f;
                        break;
                    case ZoneType.Drift:
                        d.density = 0.28f;
                        d.curvatureBias = 0.75f;
                        d.widthMultiplier = 1.55f;
                        break;
                    case ZoneType.Industrial:
                        d.density = 0.36f;
                        d.curvatureBias = 0.18f;
                        d.widthMultiplier = 1.7f;
                        break;
                    case ZoneType.Bridge:
                        d.density = 0f;
                        d.curvatureBias = 0.1f;
                        d.widthMultiplier = 2.2f;
                        break;
                }
            }
        }
    }
}

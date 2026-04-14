using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    public class HeroRoadDirector : MonoBehaviour
    {
        public void GenerateHeroNetwork()
        {
            // Clear old autobahn
            GameObject oldAutobahn = GameObject.Find("Outer_Ring_Autobahn");
            if (oldAutobahn) DestroyImmediate(oldAutobahn);

            GameObject autobahnRoot = new GameObject("Outer_Ring_Autobahn");

            // Autobahn spans all islands:
            // West Island reaches X=-2000, East Island reaches X=1500
            // South Island reaches Z=-1800, North reaches Z=1000
            
            // South Autobahn (Connecting Port to Coastal Keys over the water)
            CreateHighwayStrut(autobahnRoot.transform, "South_Autobahn", new Vector3(-500, 45, -2000), new Vector3(4000, 5, 45));
            
            // North Autobahn (Connecting Mountains to Downtown North over the water)
            CreateHighwayStrut(autobahnRoot.transform, "North_Autobahn", new Vector3(-500, 45, 1000), new Vector3(4000, 5, 45));
            
            // West Autobahn (Tracing the Mountain Outer Edge)
            CreateHighwayStrut(autobahnRoot.transform, "West_Autobahn", new Vector3(-2500, 45, -500), new Vector3(45, 5, 3000));
            
            // East Autobahn (Tracing the Resort Keys coastline)
            CreateHighwayStrut(autobahnRoot.transform, "East_Autobahn", new Vector3(1500, 45, -500), new Vector3(45, 5, 3000));
            
            Debug.Log("HeroRoadDirector: Massive 4 Kilometers Autobahn Outer Ring instantly generated!");
        }

        private void CreateHighwayStrut(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            GameObject hwy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hwy.name = name;
            hwy.transform.SetParent(parent);
            hwy.transform.position = pos;
            hwy.transform.localScale = scale;
            
            Material mat = new Material(Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Standard"));
            mat.color = new Color(0.12f, 0.12f, 0.12f); // Highway Asphalt
            hwy.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}

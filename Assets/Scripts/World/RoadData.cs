using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    public enum RoadRole
    {
        Highway,
        Bridge,
        Arterial,
        Secondary,
        Local,
        Speedway
    }

    [System.Serializable]
    public class RoadData
    {
        public string id;
        public RoadRole role;
        public List<Vector3> points = new List<Vector3>();
        public float length;
        public float width;
        public int intersections;
        public float averageCurvature;
        public bool isLoop;
        public bool isReserved;
    }
}

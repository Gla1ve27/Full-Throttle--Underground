using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    public class LandmarkReservationSystem : MonoBehaviour
    {
        public List<Bounds> reservedBounds = new List<Bounds>();

        public void BuildReservations()
        {
            // Populate via editor or script
        }

        public bool IsReserved(Vector3 point)
        {
            foreach (var b in reservedBounds)
                if (b.Contains(point)) return true;
            return false;
        }
    }
}

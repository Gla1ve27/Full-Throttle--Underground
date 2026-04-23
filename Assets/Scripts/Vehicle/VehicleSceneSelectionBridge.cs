using UnityEngine;

namespace Underground.Vehicle
{
    /// <summary>
    /// Small static handoff used when the player leaves the garage and enters World.
    /// This avoids scene startup order or stale save-state issues from spawning the wrong car.
    /// </summary>
    public static class VehicleSceneSelectionBridge
    {
        private static string pendingCarId;
        private static int pendingSetFrame = -1;

        public static string PeekPendingCarId()
        {
            return string.IsNullOrEmpty(pendingCarId) ? string.Empty : pendingCarId;
        }

        public static void SetPendingCarId(string carId)
        {
            string resolved = PlayerCarCatalog.MigrateCarId(carId);
            if (string.IsNullOrEmpty(resolved))
            {
                return;
            }

            pendingCarId = resolved;
            pendingSetFrame = Time.frameCount;
            Debug.Log($"[VehicleSceneSelectionBridge] Pending world car set to: {pendingCarId} (frame {pendingSetFrame})");
        }

        public static bool TryConsumePendingCarId(out string carId)
        {
            if (!string.IsNullOrEmpty(pendingCarId))
            {
                carId = pendingCarId;
                pendingCarId = null;
                pendingSetFrame = -1;
                return true;
            }

            carId = string.Empty;
            return false;
        }

        public static void Clear()
        {
            pendingCarId = null;
            pendingSetFrame = -1;
        }
    }
}

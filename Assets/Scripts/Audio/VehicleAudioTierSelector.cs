using UnityEngine;

namespace Underground.Audio
{
    public class VehicleAudioTierSelector : MonoBehaviour
    {
        [SerializeField] private VehicleAudioTier currentTier = VehicleAudioTier.Stock;

        public VehicleAudioTier CurrentTier => currentTier;

        public void SetTier(VehicleAudioTier tier)
        {
            currentTier = tier;
        }

        public void SetTierFromInt(int tier)
        {
            currentTier = (VehicleAudioTier)Mathf.Clamp(tier, 0, 3);
        }
    }
}

using UnityEngine;
using Underground.Session;

namespace Underground.Vehicle
{
    public class VehicleDamageSystem : MonoBehaviour
    {
        [SerializeField] private float maxDamage = 100f;
        [SerializeField] private float damage;
        [SerializeField] private float impactToDamageMultiplier = 0.65f;
        [SerializeField] private float minimumImpactThreshold = 5f;
        [SerializeField] private SessionManager sessionManager;

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<SessionManager>();
            }
        }

        public float DamageNormalized => maxDamage <= 0f ? 0f : Mathf.Clamp01(damage / maxDamage);
        public bool IsTotalled => damage >= maxDamage;
        public float CurrentDamage => damage;

        private void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;
            if (impact < minimumImpactThreshold)
            {
                return;
            }

            damage = Mathf.Min(maxDamage, damage + (impact * impactToDamageMultiplier));

            if (IsTotalled)
            {
                sessionManager?.OnVehicleTotalled();
            }
        }

        public void RepairFully()
        {
            damage = 0f;
        }

        public void SetSessionManager(SessionManager manager)
        {
            sessionManager = manager;
        }
    }
}

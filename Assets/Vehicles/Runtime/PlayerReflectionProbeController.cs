// ============================================================
// PlayerReflectionProbeController.cs
// Place at: Assets/FullThrottle/Vehicles/Runtime/PlayerReflectionProbeController.cs
//
// CRITICAL PRESERVATION RULES (do not modify these):
//   - blendDistance must never exceed 0.09
//   - Probe must not re-render during sky transitions (dawn/dusk)
//   - This script hooks into WorldTimeChangedEvent only
//   - Does NOT modify RenderSettings, skybox, fog, or HDRP volumes
// ============================================================

using UnityEngine;
using UnityEngine.Rendering;
using Underground.Core.Architecture; // WorldTimeChangedEvent + ServiceLocator live here

namespace Underground.Vehicle
{
    [RequireComponent(typeof(ReflectionProbe))]
    public class PlayerReflectionProbeController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Probe Settings — DO NOT CHANGE BLEND DISTANCE ABOVE 0.09")]
        [Tooltip("HARD MAX. Never exceed 0.09 or entire scene turns chrome.")]
        [Range(0.01f, 0.09f)]
        public float maxBlendDistance = 0.09f;

        public float dayMultiplier   = 1.0f;
        public float nightMultiplier = 0.3f;

        [Header("Re-capture Timing")]
        [Tooltip("Seconds between probe re-renders during stable lighting. Runtime captures are disabled by default for stable frame pacing.")]
        public float updateInterval = 30f;

        [Tooltip("Keep disabled during gameplay. Reflection probe renders are visible CPU/GPU spikes.")]
        public bool allowRuntimeCaptures;

        [Tooltip("Fraction of day (0-1) around dawn/dusk where capture is blocked.")]
        [Range(0.02f, 0.15f)]
        public float transitionDeadzone = 0.08f;

        // ── Private ──────────────────────────────────────────────────────────
        private ReflectionProbe _probe;
        private float _timer;
        private float _normalizedTimeOfDay; // 0 = midnight, 0.5 = noon, 1 = midnight
        private bool  _isNight;
        private bool  _receivedTimeEvent;

        // Dawn ≈ 6h (0.25), Dusk ≈ 18h (0.75) on a 0–1 normalized scale
        private const float DawnNormalized = 0.25f;
        private const float DuskNormalized = 0.75f;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            _probe = GetComponent<ReflectionProbe>();
            EnforceSettings();
        }

        private void OnEnable()
        {
            ServiceLocator.EventBus?.Subscribe<WorldTimeChangedEvent>(OnWorldTimeChanged);
        }

        private void OnDisable()
        {
            ServiceLocator.EventBus?.Unsubscribe<WorldTimeChangedEvent>(OnWorldTimeChanged);
        }

        private void Update()
        {
            // Always enforce blend distance — prevents any external code from drifting it
            _probe.blendDistance = maxBlendDistance;

            // Smooth intensity transition
            float targetIntensity = _isNight ? nightMultiplier : dayMultiplier;
            _probe.intensity = Mathf.MoveTowards(
                _probe.intensity, targetIntensity, Time.deltaTime * 0.5f);

            // Periodic re-render, but never during sky transitions
            _timer += Time.deltaTime;
            if (allowRuntimeCaptures && _timer >= updateInterval && !IsMidTransition())
            {
                _probe.RenderProbe();
                _timer = 0f;
            }
        }

        // ── Event Handler ────────────────────────────────────────────────────

        private void OnWorldTimeChanged(WorldTimeChangedEvent e)
        {
            // The DayNight Sun prefab publishes time in 0-24 range.
            _normalizedTimeOfDay = e.TimeOfDay / 24f;
            _isNight = e.IsNight;
            _receivedTimeEvent = true;
        }

        // Called by the active day/night service after transition settles.

        /// <summary>
        /// Call this after a day→night or night→day transition fully completes.
        /// Forces an immediate re-capture at stable lighting.
        /// </summary>
        public void OnDayNightTransitionComplete()
        {
            EnforceSettings();
            if (allowRuntimeCaptures)
            {
                _probe.RenderProbe();
            }
            _timer = 0f;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void EnforceSettings()
        {
            if (_probe == null) return;
            _probe.blendDistance = maxBlendDistance;
            _probe.importance    = 1;
            _probe.mode = ReflectionProbeMode.Baked;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            // Do not force intensity here — let Update() lerp it
        }

        private bool IsMidTransition()
        {
            float t = _normalizedTimeOfDay;
            bool nearDawn = t > (DawnNormalized - transitionDeadzone) &&
                            t < (DawnNormalized + transitionDeadzone);
            bool nearDusk = t > (DuskNormalized - transitionDeadzone) &&
                            t < (DuskNormalized + transitionDeadzone);
            return nearDawn || nearDusk;
        }
    }
}

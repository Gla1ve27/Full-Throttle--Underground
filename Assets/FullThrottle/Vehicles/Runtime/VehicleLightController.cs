// ============================================================
// VehicleLightController.cs
// Part 4 — Vehicle Lighting System
// Place at: Assets/FullThrottle/Vehicles/Runtime/VehicleLightController.cs
//
// PRESERVATION RULE:
//   This script hooks into the existing WorldTimeChangedEvent from
//   DayNightCycleController. It does NOT replace or interfere with
//   the day/night cycle, HDRP volumes, skybox, or reflection probes.
//   It only controls per-vehicle light components and emissive materials.
// ============================================================

using UnityEngine;
using Underground.Core.Architecture;

namespace Underground.Vehicle
{
    public class VehicleLightController : MonoBehaviour
    {
        // ── Light rig references ─────────────────────────────────────────────
        [Header("Light Rigs — match VehicleConstants naming")]
        [Tooltip("Parent of all headlight Light components.")]
        public Transform headlightsRoot;

        [Tooltip("Parent of all tail light renderers (emissive).")]
        public Transform tailLightsRoot;

        [Tooltip("Parent of all brake light renderers (emissive).")]
        public Transform brakeLightsRoot;

        [Tooltip("Parent of all reverse light renderers.")]
        public Transform reverseLightsRoot;

        // ── Emissive settings ────────────────────────────────────────────────
        [Header("Emissive Intensities")]
        public float tailLightBaseIntensity    = 1.2f;
        public float brakeLightActiveIntensity = 4.0f;
        public float brakeLightIdleIntensity   = 0.6f;
        public float headlightIntensity        = 3.5f;
        public float reverseLightIntensity     = 1.8f;

        [Header("Emissive Colors")]
        public Color tailLightColor   = new Color(1f, 0.05f, 0.02f);
        public Color brakeLightColor  = new Color(1f, 0.02f, 0.01f);
        public Color headlightColor   = new Color(0.95f, 0.98f, 1f);
        public Color reverseLightColor= new Color(0.9f, 0.95f, 1f);

        // ── VehicleController reference ──────────────────────────────────────
        [Header("Vehicle Reference")]
        [Tooltip("Injected by spawner. If null, auto-finds on same GameObject.")]
        public VehicleController vehicleController;

        // ── State ────────────────────────────────────────────────────────────
        private bool _headlightsOn;
        private bool _lastBraking;
        private bool _lastReversing;
        private bool _lastHeadlights;

        // Cache renderers to avoid GetComponent every frame
        private Renderer[] _tailRenderers;
        private Renderer[] _brakeRenderers;
        private Renderer[] _headlightRenderers;
        private Renderer[] _reverseRenderers;
        private Light[]    _headlights;

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (vehicleController == null)
                vehicleController = GetComponentInParent<VehicleController>();

            CacheRenderers();
        }

        private void OnEnable()
        {
            // Hook into existing day/night event — does NOT modify the cycle itself
            ServiceLocator.EventBus?.Subscribe<WorldTimeChangedEvent>(OnWorldTimeChanged);
        }

        private void OnDisable()
        {
            ServiceLocator.EventBus?.Unsubscribe<WorldTimeChangedEvent>(OnWorldTimeChanged);
        }

        private void Update()
        {
            if (vehicleController == null) return;

            bool braking  = vehicleController.IsBraking;
            bool reversing = vehicleController.IsReverse;

            // Only update GPU state when something changes — avoids SetPropertyBlock spam
            if (braking  != _lastBraking)  { SetBrakeLights(braking);   _lastBraking  = braking; }
            if (reversing != _lastReversing){ SetReverseLights(reversing); _lastReversing = reversing; }
            if (_headlightsOn != _lastHeadlights)
            {
                SetHeadlights(_headlightsOn);
                SetTailLights(_headlightsOn);
                _lastHeadlights = _headlightsOn;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Toggle headlights manually (e.g. from player input).</summary>
        public void ToggleHeadlights() => SetHeadlightsOn(!_headlightsOn);

        /// <summary>Force headlights on or off.</summary>
        public void SetHeadlightsOn(bool on) => _headlightsOn = on;

        // ── Day/Night hook ───────────────────────────────────────────────────

        private void OnWorldTimeChanged(WorldTimeChangedEvent e)
        {
            // Auto-headlights at night — exactly like Forza Horizon
            bool shouldBeOn = e.IsNight;
            if (shouldBeOn != _headlightsOn)
            {
                _headlightsOn = shouldBeOn;
                // Actual visual update happens next Update() frame
            }
        }

        // ── Light application ────────────────────────────────────────────────

        private void SetHeadlights(bool on)
        {
            if (_headlights != null)
                foreach (var l in _headlights)
                    if (l != null) l.enabled = on;

            if (_headlightRenderers != null)
                foreach (var r in _headlightRenderers)
                    if (r != null) SetEmissive(r, on ? headlightColor * headlightIntensity : Color.black);
        }

        private void SetTailLights(bool on)
        {
            if (_tailRenderers != null)
                foreach (var r in _tailRenderers)
                    if (r != null) SetEmissive(r, on ? tailLightColor * tailLightBaseIntensity : Color.black);
        }

        private void SetBrakeLights(bool braking)
        {
            float intensity = braking ? brakeLightActiveIntensity : brakeLightIdleIntensity;
            if (_brakeRenderers != null)
                foreach (var r in _brakeRenderers)
                    if (r != null) SetEmissive(r, brakeLightColor * intensity);
        }

        private void SetReverseLights(bool on)
        {
            if (_reverseRenderers != null)
                foreach (var r in _reverseRenderers)
                    if (r != null) SetEmissive(r, on ? reverseLightColor * reverseLightIntensity : Color.black);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetEmissive(Renderer r, Color color)
        {
            // Use MaterialPropertyBlock to avoid creating material instances
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(EmissiveColorId, color);
            r.SetPropertyBlock(block);
        }

        private void CacheRenderers()
        {
            _tailRenderers     = GetChildRenderers(tailLightsRoot);
            _brakeRenderers    = GetChildRenderers(brakeLightsRoot);
            _headlightRenderers= GetChildRenderers(headlightsRoot);
            _reverseRenderers  = GetChildRenderers(reverseLightsRoot);
            _headlights        = headlightsRoot != null
                                 ? headlightsRoot.GetComponentsInChildren<Light>(true)
                                 : new Light[0];
        }

        private static Renderer[] GetChildRenderers(Transform root)
        {
            if (root == null) return new Renderer[0];
            return root.GetComponentsInChildren<Renderer>(true);
        }
    }
}

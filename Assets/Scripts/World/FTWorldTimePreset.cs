using UnityEngine;
using UnityEngine.Rendering;

namespace FullThrottle.SacredCore.World
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/World Time Preset", fileName = "FT_WorldTimePreset")]
    public sealed class FTWorldTimePreset : ScriptableObject
    {
        [Header("Identity")]
        public string presetId = "night_free_roam";
        public string displayName = "Night Free Roam";

        [Header("Sky And Volume")]
        public VolumeProfile volumeProfile;
        public Material skyboxMaterial;
        public Color ambientColor = new(0.02f, 0.025f, 0.055f, 1f);
        public bool enableFog = true;
        public Color fogColor = new(0.02f, 0.025f, 0.06f, 1f);
        [Min(0f)] public float fogDensity = 0.012f;

        [Header("Directional Light")]
        public Vector3 sunEuler = new(18f, 166f, 0f);
        public Color sunColor = new(0.42f, 0.48f, 0.72f, 1f);
        [Min(0f)] public float sunIntensity = 0.08f;
        public LightShadows sunShadows = LightShadows.None;

        [Header("Runtime Budget")]
        [Min(0f)] public float shadowDistance = 24f;
        [Range(0, 4)] public int shadowCascades = 0;
        [Header("Gameplay Time")]
        public bool duskNightOnly = true;
        [Range(0f, 24f)] public float gameplayHour = 21f;
        [Tooltip("Use 0 to keep gameplay time frozen. Cinematic scenes can opt into progression later.")]
        [Min(0f)] public float gameplayTimeScale = 0f;
    }
}

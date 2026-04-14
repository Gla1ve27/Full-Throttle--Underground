using System;
using UnityEngine;

namespace Underground.World
{
    [CreateAssetMenu(menuName = "FullThrottle/World Generation Config", fileName = "WorldGenerationConfig")]
    public class WorldGenerationConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        // SEED
        // ─────────────────────────────────────────────────────────────────────

        [Header("Seed")]
        [Tooltip("Deterministic seed for reproducible world layouts")]
        public int seed = 12345;

        [Tooltip("If true, generates a random seed on each run")]
        public bool randomizeSeed = false;

        // ─────────────────────────────────────────────────────────────────────
        // WORLD SIZE — based on NFS Heat measurements
        //
        // NFS Heat Palm City: ~6km × 8km total, ~36 km² drivable
        // Our world: scaled to match FCG city output (~500m across)
        //
        // Scale ratio: FCG city ~500m = NFS downtown ~2500m → 1:5
        // So our 2500m world ≈ NFS Heat's 12.5km coverage
        // ─────────────────────────────────────────────────────────────────────

        [Header("World Size")]
        [Tooltip("Total world extents in X and Z (metres)")]
        public Vector2 worldSize = new Vector2(2500f, 3000f);

        // ─────────────────────────────────────────────────────────────────────
        // DISTRICT SIZES — TIGHT around FCG city
        //
        // FCG Large city output ≈ 500m across → 250m radius
        // Highway should hug the city → 400-500m radius
        // Mountain should be ADJACENT to city → 350m northwest
        // ─────────────────────────────────────────────────────────────────────

        [Header("District Sizes")]
        [Tooltip("Radius matching actual FCG city output size")]
        public float cityCoreRadius = 300f;

        [Tooltip("Radius of mountain / drift district")]
        public float mountainRadius = 400f;

        [Tooltip("Span of the arterial corridor")]
        public float arterialSpan = 500f;

        [Tooltip("How far inward the highway sits from its district radius")]
        public float highwayInset = 50f;

        // ─────────────────────────────────────────────────────────────────────
        // ROAD SHAPE
        // ─────────────────────────────────────────────────────────────────────

        [Header("Road Shape")]
        [Tooltip("Control points for the highway loop")]
        public int highwayControlPointCount = 16;

        [Tooltip("Number of arterial connectors")]
        public int arterialConnectionCount = 4;

        [Tooltip("Number of mountain switchback roads")]
        public int mountainRoadCount = 2;

        [Tooltip("City road density (unused — FCG handles city streets)")]
        public int cityRoadDensity = 0;

        [Header("Road Widths")]
        public float highwayWidth = 30f;
        public float arterialWidth = 18f;
        public float cityRoadWidth = 12f;
        public float mountainRoadWidth = 10f;

        // ─────────────────────────────────────────────────────────────────────
        // VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        [Header("Validation")]
        public bool runValidationAfterGeneration = true;
        public bool clearPreviousGeneration = true;

        // ─────────────────────────────────────────────────────────────────────
        // POPULATION
        // ─────────────────────────────────────────────────────────────────────

        [Header("Population")]
        [Tooltip("Use FCG for city buildings and streets")]
        public bool generateCityWithFCG = true;

        [Tooltip("Scatter crude environment props (disable for cleaner look)")]
        public bool placeEnvironmentProps = false;

        [Tooltip("Place foliage")]
        public bool placeDistrictFoliage = false;

        // ─────────────────────────────────────────────────────────────────────
        // GAMEPLAY
        // ─────────────────────────────────────────────────────────────────────

        [Header("Gameplay")]
        public bool applyGameplayTags = true;
        public bool placeRaceInfrastructure = true;
        public bool placeTrafficSpawnPoints = true;
        public bool attachDebugOverlay = true;

        // ─────────────────────────────────────────────────────────────────────
        // DEBUG
        // ─────────────────────────────────────────────────────────────────────

        [Header("Debug")]
        public bool logGeneration = true;
        public bool drawDebugGizmos = true;
    }
}

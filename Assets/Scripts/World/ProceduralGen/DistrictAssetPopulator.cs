using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// PHASE 3: Procedural district-based environment dressing.
    ///
    /// Scatters props, foliage, lights, and barriers along EasyRoads3D splines
    /// based on the district each road segment belongs to.
    ///
    /// District → Asset Rules (from spec §7, §12):
    ///   Mountain → rocks, cliffs, sparse foliage, guard rails, warning signs
    ///   City     → streetlights, urban props, signs, palms
    ///   Arterial → roadside props, trees, signs, barriers
    ///   Highway  → barriers, signs, lights, tightly-spaced side objects
    ///
    /// Uses primitive geometry for now — future phases will swap in actual
    /// prefab assets from the project's asset folders.
    /// </summary>
    public class DistrictAssetPopulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EasyRoadsManager easyRoadsManager;

        [Header("Density")]
        [Tooltip("Distance between props along highway roads")]
        [SerializeField] private float highwayPropSpacing = 80f;

        [Tooltip("Distance between props along arterial roads")]
        [SerializeField] private float arterialPropSpacing = 100f;

        [Tooltip("Distance between props along city roads")]
        [SerializeField] private float cityPropSpacing = 75f;

        [Tooltip("Distance between props along mountain roads")]
        [SerializeField] private float mountainPropSpacing = 120f;

        [Header("Sizes")]
        [Tooltip("Height of streetlight poles")]
        [SerializeField] private float lightPoleHeight = 10f;

        [Tooltip("Height of highway barrier blocks")]
        [SerializeField] private float barrierHeight = 1.8f;

        // Shared materials (created once, reused)
        private Material metalMat;
        private Material concreteMat;
        private Material foliageMat;
        private Material rockMat;
        private Material lightGlowMat;
        private Material barrierMat;

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Populate all districts with environment props based on their type.
        /// </summary>
        public void PopulateDistricts(WorldPlan plan, WorldGenerationConfig config)
        {
            if (!config.placeEnvironmentProps && !config.placeDistrictFoliage)
            {
                Debug.Log("[DistrictPop] Environment population disabled in config.");
                return;
            }

            InitMaterials();

            // Resolve the Generated hierarchy roots
            Transform generated = transform.parent?.Find("Generated");
            if (generated == null)
            {
                Debug.LogWarning("[DistrictPop] 'Generated' root not found — skipping population.");
                return;
            }

            Transform foliageRoot  = generated.Find("Foliage")  ?? CreateChild(generated, "Foliage");
            Transform lightsRoot   = generated.Find("Lights")   ?? CreateChild(generated, "Lights");
            Transform barriersRoot = generated.Find("Barriers") ?? CreateChild(generated, "Barriers");
            Transform propsRoot    = generated.Find("Props")    ?? CreateChild(generated, "Props");

            if (config.logGeneration)
                Debug.Log("[DistrictPop] ═══ District asset population ═══");

            int totalProps = 0;

            foreach (var district in plan.districts)
            {
                int count = 0;
                switch (district.districtType)
                {
                    case DistrictType.Highway:
                        if (config.placeEnvironmentProps)
                            count += PopulateHighway(district, barriersRoot, lightsRoot);
                        break;

                    case DistrictType.CityCore:
                        if (config.placeEnvironmentProps)
                            count += PopulateCity(district, lightsRoot, propsRoot);
                        if (config.placeDistrictFoliage)
                            count += PopulateCityFoliage(district, foliageRoot);
                        break;

                    case DistrictType.Arterial:
                        if (config.placeEnvironmentProps)
                            count += PopulateArterial(district, barriersRoot, lightsRoot);
                        if (config.placeDistrictFoliage)
                            count += PopulateArterialFoliage(district, foliageRoot);
                        break;

                    case DistrictType.Mountain:
                        if (config.placeEnvironmentProps)
                            count += PopulateMountain(district, barriersRoot, propsRoot);
                        if (config.placeDistrictFoliage)
                            count += PopulateMountainFoliage(district, foliageRoot);
                        break;
                }

                if (config.logGeneration)
                    Debug.Log($"[DistrictPop] {district.districtType}: {count} props placed");

                totalProps += count;
            }

            if (config.logGeneration)
                Debug.Log($"[DistrictPop] ═══ Total: {totalProps} environment props ═══");
        }

        // ─────────────────────────────────────────────────────────────────────
        // HIGHWAY POPULATION
        //
        // Barriers and lights along the highway loop.
        // Tightly spaced for speed sensation (spec §7.4).
        // ─────────────────────────────────────────────────────────────────────

        private int PopulateHighway(DistrictPlan district, Transform barriers, Transform lights)
        {
            int count = 0;
            float radius = district.radius;

            // Sparse barriers — only a few around the perimeter to suggest highway edges
            // NOT every 25m — that created the solid black ring
            int numBarriers = Mathf.Min(30, Mathf.CeilToInt(2f * Mathf.PI * radius / highwayPropSpacing));
            for (int i = 0; i < numBarriers; i++)
            {
                float angle = i * Mathf.PI * 2f / numBarriers;
                float r = radius * 0.92f;

                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * r, barrierHeight * 0.5f,
                    Mathf.Sin(angle) * r);

                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = "Highway_Barrier";
                barrier.transform.SetParent(barriers);
                barrier.transform.position = pos;
                barrier.transform.rotation = Quaternion.LookRotation(tangent);
                barrier.transform.localScale = new Vector3(0.6f, barrierHeight, highwayPropSpacing * 0.4f);
                barrier.GetComponent<Renderer>().sharedMaterial = barrierMat;
                DestroyImmediate(barrier.GetComponent<BoxCollider>());
                count++;
            }

            // Sparse lights — much fewer than barriers
            int numLights = Mathf.Min(16, Mathf.CeilToInt(2f * Mathf.PI * radius / (highwayPropSpacing * 4f)));
            for (int i = 0; i < numLights; i++)
            {
                float angle = i * Mathf.PI * 2f / numLights;
                float r = radius * 0.95f;

                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * r, 0f,
                    Mathf.Sin(angle) * r);

                SpawnStreetLight(pos, lights);
                count++;
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CITY POPULATION
        //
        // Dense lighting, urban props (spec §7.2).
        // ─────────────────────────────────────────────────────────────────────

        private int PopulateCity(DistrictPlan district, Transform lights, Transform props)
        {
            int count = 0;

            // Streetlights in a grid pattern within city bounds
            float gridStep = cityPropSpacing * 2.5f;
            float r = district.radius;

            for (float x = -r; x <= r; x += gridStep)
            {
                for (float z = -r; z <= r; z += gridStep)
                {
                    Vector3 pos = district.center + new Vector3(x, 0f, z);

                    // Only inside the circle
                    if (Vector3.Distance(pos, district.center) > r) continue;

                    SpawnStreetLight(pos, lights);
                    count++;
                }
            }

            return count;
        }

        private int PopulateCityFoliage(DistrictPlan district, Transform foliage)
        {
            int count = 0;
            float r = district.radius;
            int numTrees = Mathf.CeilToInt(r * 0.1f); // sparse in city

            for (int i = 0; i < numTrees; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(r * 0.3f, r * 0.9f);
                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * dist, 0f,
                    Mathf.Sin(angle) * dist);

                SpawnPalmTree(pos, foliage);
                count++;
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ARTERIAL POPULATION
        //
        // Moderate dressing, signs, barriers (spec §7.3).
        // ─────────────────────────────────────────────────────────────────────

        private int PopulateArterial(DistrictPlan district, Transform barriers, Transform lights)
        {
            int count = 0;
            float r = district.radius;

            // Lights along the perimeter
            int numLights = Mathf.CeilToInt(2f * Mathf.PI * r / arterialPropSpacing);
            for (int i = 0; i < numLights; i++)
            {
                float angle = i * Mathf.PI * 2f / numLights;
                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * r * 0.7f, 0f,
                    Mathf.Sin(angle) * r * 0.7f);

                SpawnStreetLight(pos, lights);
                count++;
            }

            return count;
        }

        private int PopulateArterialFoliage(DistrictPlan district, Transform foliage)
        {
            int count = 0;
            float r = district.radius;
            int numTrees = Mathf.CeilToInt(r * 0.06f);

            for (int i = 0; i < numTrees; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(r * 0.4f, r * 0.85f);
                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * dist, 0f,
                    Mathf.Sin(angle) * dist);

                SpawnTree(pos, foliage);
                count++;
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // MOUNTAIN POPULATION
        //
        // Rocks, guard rails, sparse foliage (spec §7.1).
        // ─────────────────────────────────────────────────────────────────────

        private int PopulateMountain(DistrictPlan district, Transform barriers, Transform props)
        {
            int count = 0;
            float r = district.radius;

            // Rocks scattered across mountain area
            int numRocks = Mathf.CeilToInt(r * 0.08f);
            for (int i = 0; i < numRocks; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(r * 0.2f, r * 0.9f);
                float elevation = dist / r * 80f + Random.Range(0f, 30f);

                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * dist, elevation,
                    Mathf.Sin(angle) * dist);

                SpawnRock(pos, props);
                count++;
            }

            // Guard rails along mountain edges
            int numRails = Mathf.CeilToInt(2f * Mathf.PI * r / mountainPropSpacing);
            for (int i = 0; i < numRails; i++)
            {
                float angle = i * Mathf.PI * 2f / numRails;
                float edgeR = r * 0.8f;
                float elevation = edgeR / r * 60f;

                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * edgeR, elevation,
                    Mathf.Sin(angle) * edgeR);

                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                SpawnGuardRail(pos, tangent, barriers);
                count++;
            }

            return count;
        }

        private int PopulateMountainFoliage(DistrictPlan district, Transform foliage)
        {
            int count = 0;
            float r = district.radius;
            int numTrees = Mathf.CeilToInt(r * 0.04f); // sparse — mountain feel

            for (int i = 0; i < numTrees; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(r * 0.15f, r * 0.75f);
                float elevation = dist / r * 50f + Random.Range(0f, 15f);

                Vector3 pos = district.center + new Vector3(
                    Mathf.Cos(angle) * dist, elevation,
                    Mathf.Sin(angle) * dist);

                SpawnTree(pos, foliage);
                count++;
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // PROP SPAWNERS (primitive geometry — Phase 5 will swap with prefabs)
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnStreetLight(Vector3 pos, Transform parent)
        {
            // Pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "StreetLight_Pole";
            pole.transform.SetParent(parent);
            pole.transform.position = pos + Vector3.up * (lightPoleHeight * 0.5f);
            pole.transform.localScale = new Vector3(0.25f, lightPoleHeight * 0.5f, 0.25f);
            pole.GetComponent<Renderer>().sharedMaterial = metalMat;
            DestroyImmediate(pole.GetComponent<CapsuleCollider>());

            // Lamp head
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "StreetLight_Lamp";
            lamp.transform.SetParent(parent);
            lamp.transform.position = pos + Vector3.up * lightPoleHeight;
            lamp.transform.localScale = new Vector3(1f, 0.4f, 1f);
            lamp.GetComponent<Renderer>().sharedMaterial = lightGlowMat;
            DestroyImmediate(lamp.GetComponent<SphereCollider>());

            // HDRP light
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.95f, 0.85f); // warm white
            light.intensity = 1500f;
            light.range = 50f;
            light.spotAngle = 100f;
            light.transform.rotation = Quaternion.LookRotation(Vector3.down);
        }

        private void SpawnPalmTree(Vector3 pos, Transform parent)
        {
            // Trunk
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "PalmTree";
            trunk.transform.SetParent(parent);
            float height = Random.Range(8f, 14f);
            trunk.transform.position = pos + Vector3.up * (height * 0.5f);
            trunk.transform.localScale = new Vector3(0.4f, height * 0.5f, 0.4f);
            trunk.GetComponent<Renderer>().sharedMaterial = rockMat; // brown-ish
            DestroyImmediate(trunk.GetComponent<CapsuleCollider>());

            // Canopy
            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "PalmTree_Canopy";
            canopy.transform.SetParent(parent);
            canopy.transform.position = pos + Vector3.up * height;
            canopy.transform.localScale = new Vector3(4f, 2.5f, 4f);
            canopy.GetComponent<Renderer>().sharedMaterial = foliageMat;
            DestroyImmediate(canopy.GetComponent<SphereCollider>());
        }

        private void SpawnTree(Vector3 pos, Transform parent)
        {
            // Trunk
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree";
            trunk.transform.SetParent(parent);
            float height = Random.Range(6f, 12f);
            trunk.transform.position = pos + Vector3.up * (height * 0.5f);
            trunk.transform.localScale = new Vector3(0.3f, height * 0.5f, 0.3f);
            trunk.GetComponent<Renderer>().sharedMaterial = rockMat;
            DestroyImmediate(trunk.GetComponent<CapsuleCollider>());

            // Canopy (cone for evergreen)
            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Tree_Canopy";
            canopy.transform.SetParent(parent);
            canopy.transform.position = pos + Vector3.up * (height * 0.8f);
            float canopySize = Random.Range(3f, 5f);
            canopy.transform.localScale = new Vector3(canopySize, canopySize * 1.2f, canopySize);
            canopy.GetComponent<Renderer>().sharedMaterial = foliageMat;
            DestroyImmediate(canopy.GetComponent<SphereCollider>());
        }

        private void SpawnRock(Vector3 pos, Transform parent)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(parent);
            rock.transform.position = pos;
            float size = Random.Range(1.5f, 6f);
            rock.transform.localScale = new Vector3(
                size * Random.Range(0.7f, 1.3f),
                size * Random.Range(0.5f, 0.9f),
                size * Random.Range(0.7f, 1.3f));
            rock.transform.rotation = Random.rotation;
            rock.GetComponent<Renderer>().sharedMaterial = rockMat;
            DestroyImmediate(rock.GetComponent<SphereCollider>());
        }

        private void SpawnGuardRail(Vector3 pos, Vector3 forward, Transform parent)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "GuardRail";
            rail.transform.SetParent(parent);
            rail.transform.position = pos + Vector3.up * 0.5f;
            rail.transform.rotation = Quaternion.LookRotation(forward);
            rail.transform.localScale = new Vector3(0.15f, 1f, mountainPropSpacing * 0.8f);
            rail.GetComponent<Renderer>().sharedMaterial = metalMat;
            DestroyImmediate(rail.GetComponent<BoxCollider>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // MATERIAL FACTORY
        // ─────────────────────────────────────────────────────────────────────

        private void InitMaterials()
        {
            metalMat    = CreateHDRP(new Color(0.5f, 0.5f, 0.55f), 0.6f);
            concreteMat = CreateHDRP(new Color(0.6f, 0.58f, 0.55f), 0.2f);
            foliageMat  = CreateHDRP(new Color(0.15f, 0.45f, 0.12f), 0.1f);
            rockMat     = CreateHDRP(new Color(0.35f, 0.3f, 0.25f), 0.15f);
            barrierMat  = CreateHDRP(new Color(0.7f, 0.7f, 0.72f), 0.3f);

            lightGlowMat = CreateHDRP(new Color(1f, 0.95f, 0.85f), 0.1f);
            if (lightGlowMat.HasProperty("_EmissiveColor"))
            {
                lightGlowMat.EnableKeyword("_EMISSION");
                lightGlowMat.SetColor("_EmissiveColor", new Color(1f, 0.95f, 0.85f) * 3f);
            }
        }

        private Material CreateHDRP(Color color, float smoothness)
        {
            Material mat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private Transform CreateChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }
    }
}

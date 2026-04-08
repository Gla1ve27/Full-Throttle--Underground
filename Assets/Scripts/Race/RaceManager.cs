using System;
using Underground.Core.Architecture;
using Underground.Session;
using Underground.TimeSystem;
using UnityEngine;

namespace Underground.Race
{
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager ActiveRace { get; private set; }

        public static event Action<RaceManager> RaceStarted;
        public static event Action<RaceManager> RaceEnded;

        [Header("Race Setup")]
        [SerializeField] private RaceDefinition activeRace;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private RiskSystem riskSystem;
        [SerializeField] private DayNightCycleController dayNightCycle;

        [Header("Track Limiter")]
        [SerializeField] private float raceLength = 240f;
        [SerializeField] private float halfTrackWidth = 16f;
        [SerializeField] private float limiterSpacing = 20f;
        [SerializeField] private float limiterHeight = 2.4f;
        [SerializeField] private float finishTriggerDepth = 14f;

        private Transform activeCourseRoot;
        private RaceFinishTrigger finishTrigger;
        private Renderer[] markerRenderers;

        public bool IsRaceActive { get; private set; }
        public RaceDefinition ActiveDefinition => activeRace;
        public string DisplayName => activeRace != null && !string.IsNullOrWhiteSpace(activeRace.displayName) ? activeRace.displayName : gameObject.name;
        public string ActiveObjectiveText => IsRaceActive ? $"{DisplayName} - Reach the finish gate" : string.Empty;

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = ServiceResolver.Resolve<ISessionService>(null) as SessionManager
                    ?? FindFirstObjectByType<SessionManager>();
            }

            if (riskSystem == null)
            {
                riskSystem = ServiceResolver.Resolve<IRiskService>(null) as RiskSystem
                    ?? FindFirstObjectByType<RiskSystem>();
            }

            if (dayNightCycle == null)
            {
                dayNightCycle = ServiceResolver.Resolve<ITimeOfDayService>(null) as DayNightCycleController
                    ?? FindFirstObjectByType<DayNightCycleController>();
            }

            markerRenderers = GetComponentsInChildren<Renderer>(true);
        }

        public bool CanStartRace()
        {
            if (activeRace == null || IsRaceActive || (ActiveRace != null && ActiveRace != this))
            {
                return false;
            }

            if (activeRace.nightOnly && (dayNightCycle == null || !dayNightCycle.IsNight))
            {
                return false;
            }

            return true;
        }

        public string GetStartPrompt()
        {
            if (activeRace == null)
            {
                return string.Empty;
            }

            if (IsRaceActive)
            {
                return ActiveObjectiveText;
            }

            if (activeRace.nightOnly && (dayNightCycle == null || !dayNightCycle.IsNight))
            {
                return $"{DisplayName} - Night only";
            }

            return $"Press F or Enter to start {DisplayName}";
        }

        public bool TryStartRace()
        {
            if (!CanStartRace())
            {
                return false;
            }

            IsRaceActive = true;
            ActiveRace = this;
            ToggleMarker(false);
            BuildRaceCourse();
            RaceStarted?.Invoke(this);
            return true;
        }

        public void HandlePlayerReachedFinish(Collider other)
        {
            if (!IsRaceActive || other == null || !other.CompareTag("Player"))
            {
                return;
            }

            CompleteAndCloseRace(true);
        }

        public void CancelRace()
        {
            if (!IsRaceActive)
            {
                return;
            }

            CompleteAndCloseRace(false);
        }

        public void CompleteRace(bool playerWon)
        {
            if (activeRace == null || !playerWon)
            {
                return;
            }

            bool isNight = dayNightCycle != null && dayNightCycle.IsNight;
            float multiplier = riskSystem != null ? riskSystem.GetRewardMultiplier(isNight) : 1f;

            int money = Mathf.RoundToInt(activeRace.rewardMoney * multiplier);
            int rep = Mathf.RoundToInt(activeRace.rewardReputation * multiplier);

            sessionManager?.AddMoney(money);
            sessionManager?.AddReputation(rep);
        }

        private void CompleteAndCloseRace(bool playerWon)
        {
            if (playerWon)
            {
                CompleteRace(true);
            }

            CleanupRaceCourse();
            IsRaceActive = false;

            if (ActiveRace == this)
            {
                ActiveRace = null;
            }

            ToggleMarker(true);
            RaceEnded?.Invoke(this);
        }

        private void BuildRaceCourse()
        {
            CleanupRaceCourse();

            activeCourseRoot = new GameObject("ActiveRaceCourse").transform;
            activeCourseRoot.SetParent(transform, false);
            activeCourseRoot.localPosition = Vector3.zero;
            activeCourseRoot.localRotation = Quaternion.identity;

            Material limiterMaterial = CreateLimiterMaterial();
            Material finishMaterial = CreateFinishMaterial();

            Vector3 startPosition = transform.position;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 leftEdge = -right * halfTrackWidth;
            Vector3 rightEdge = right * halfTrackWidth;

            for (float distance = 18f; distance <= raceLength; distance += limiterSpacing)
            {
                Vector3 center = startPosition + forward * distance;
                CreateChevronSign(activeCourseRoot, "LimiterLeft", center + leftEdge + Vector3.up * limiterHeight, Quaternion.LookRotation(right, Vector3.up), limiterMaterial, false);
                CreateChevronSign(activeCourseRoot, "LimiterRight", center + rightEdge + Vector3.up * limiterHeight, Quaternion.LookRotation(-right, Vector3.up), limiterMaterial, true);
            }

            Vector3 finishCenter = startPosition + forward * raceLength;
            GameObject finishRoot = new GameObject("FinishGate");
            finishRoot.transform.SetParent(activeCourseRoot, false);
            finishRoot.transform.position = finishCenter;
            finishRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            CreateFinishPylon(finishRoot.transform, "LeftPylon", -halfTrackWidth, finishMaterial);
            CreateFinishPylon(finishRoot.transform, "RightPylon", halfTrackWidth, finishMaterial);

            GameObject finishTriggerObject = new GameObject("FinishTrigger");
            finishTriggerObject.transform.SetParent(finishRoot.transform, false);
            finishTriggerObject.transform.localPosition = Vector3.up * 1.75f;
            BoxCollider finishCollider = finishTriggerObject.AddComponent<BoxCollider>();
            finishCollider.isTrigger = true;
            finishCollider.size = new Vector3(halfTrackWidth * 2f, 3.5f, finishTriggerDepth);

            finishTrigger = finishTriggerObject.AddComponent<RaceFinishTrigger>();
            finishTrigger.Configure(this);
        }

        private void CleanupRaceCourse()
        {
            finishTrigger = null;
            if (activeCourseRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(activeCourseRoot.gameObject);
            }
            else
            {
                DestroyImmediate(activeCourseRoot.gameObject);
            }

            activeCourseRoot = null;
        }

        private void ToggleMarker(bool visible)
        {
            if (markerRenderers == null || markerRenderers.Length == 0)
            {
                markerRenderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < markerRenderers.Length; i++)
            {
                Renderer renderer = markerRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = visible;
            }
        }

        private static void CreateChevronSign(Transform parent, string name, Vector3 position, Quaternion rotation, Material material, bool mirrored)
        {
            GameObject signRoot = new GameObject(name);
            signRoot.transform.SetParent(parent, false);
            signRoot.transform.position = position;
            signRoot.transform.rotation = rotation;
            signRoot.transform.localScale = Vector3.one;

            float direction = mirrored ? -1f : 1f;
            for (int i = 0; i < 3; i++)
            {
                float offset = i * 1.28f;
                CreateChevronBar(signRoot.transform, $"Upper_{i}", new Vector3(offset * direction, 0.44f, 0f), 45f * direction, material);
                CreateChevronBar(signRoot.transform, $"Lower_{i}", new Vector3(offset * direction, -0.44f, 0f), -45f * direction, material);
            }
        }

        private static void CreateChevronBar(Transform parent, string name, Vector3 localPosition, float zRotation, Material material)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            bar.transform.localScale = new Vector3(1.35f, 0.22f, 0.18f);

            Collider collider = bar.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            Renderer renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void CreateFinishPylon(Transform parent, string name, float lateralOffset, Material material)
        {
            GameObject pylon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pylon.name = name;
            pylon.transform.SetParent(parent, false);
            pylon.transform.localPosition = new Vector3(lateralOffset, 1.9f, 0f);
            pylon.transform.localScale = new Vector3(0.55f, 3.8f, 0.55f);
            RemoveCollider(pylon);

            Renderer renderer = pylon.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private static Material CreateLimiterMaterial()
        {
            Shader shader = Shader.Find("HDRP/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = "RaceLimiterRuntime"
            };

            Color baseColor = new Color(1f, 0.39f, 0.12f, 1f);
            SetColorIfPresent(material, "_BaseColor", baseColor);
            SetColorIfPresent(material, "_Color", baseColor);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.86f);

            Color emissiveColor = baseColor * 7f;
            SetColorIfPresent(material, "_EmissiveColor", emissiveColor);
            SetColorIfPresent(material, "_EmissionColor", emissiveColor);
            material.EnableKeyword("_EMISSION");
            return material;
        }

        private static Material CreateFinishMaterial()
        {
            Shader shader = Shader.Find("HDRP/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = "RaceFinishRuntime"
            };

            Color baseColor = new Color(1f, 0.1f, 0.46f, 1f);
            SetColorIfPresent(material, "_BaseColor", baseColor);
            SetColorIfPresent(material, "_Color", baseColor);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.78f);

            Color emissiveColor = baseColor * 6f;
            SetColorIfPresent(material, "_EmissiveColor", emissiveColor);
            SetColorIfPresent(material, "_EmissionColor", emissiveColor);
            material.EnableKeyword("_EMISSION");
            return material;
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}

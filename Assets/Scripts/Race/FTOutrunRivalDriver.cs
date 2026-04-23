using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [DefaultExecutionOrder(-210)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FTOutrunRivalDriver : MonoBehaviour
    {
        [SerializeField] private FTOutrunRivalDefinition definition;
        [SerializeField] private FTOutrunRoute route;
        [SerializeField] private Transform[] routeWaypoints;
        [SerializeField] private FTDriverInput driverInput;
        [SerializeField] private FTVehicleTelemetry telemetry;
        [SerializeField] private Rigidbody body;
        [SerializeField] private bool roamingEnabled = true;
        [SerializeField] private bool loopRoute = true;
        [SerializeField] private bool allowOpenRoamWithoutRoute = true;
        [SerializeField] private float waypointRadius = 9f;
        [SerializeField] private float lookAheadSteerAngle = 52f;
        [SerializeField] private float brakingAngle = 42f;
        [SerializeField] private float hardBrakeAngle = 68f;
        [SerializeField] private float handbrakeAngle = 78f;
        [SerializeField] private float minimumRouteSpeedKph = 42f;
        [SerializeField] private float chaseLeadMeters = 18f;
        [SerializeField] private float openRoamRetargetSeconds = 3.5f;
        [SerializeField] private float openRoamForwardDistance = 130f;
        [SerializeField] private float openRoamSideDistance = 42f;
        [SerializeField] private float routeLookupInterval = 4f;
        [SerializeField] private LayerMask groundMask = ~0;

        private int waypointIndex;
        private float mistakeTimer;
        private float currentMistake;
        private Transform outrunChaseTarget;
        private Vector3 openRoamTarget;
        private bool hasOpenRoamTarget;
        private float nextOpenRoamTargetTime;
        private float nextRouteLookupTime;

        public FTOutrunRivalDefinition Definition => definition;
        public FTVehicleTelemetry Telemetry => telemetry;
        public bool RoamingEnabled
        {
            get => roamingEnabled;
            set
            {
                roamingEnabled = value;
                if (!roamingEnabled && driverInput != null)
                {
                    driverInput.SetManual(0f, 0f, 1f, false, false);
                }
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyDefinitionIfAvailable();
        }

        private void Update()
        {
            ResolveReferences();
            if (!roamingEnabled || driverInput == null)
            {
                return;
            }

            if (!TryResolveDriveTarget(out Vector3 targetPosition))
            {
                driverInput.SetManual(0f, 0f, 0.2f, false, false);
                return;
            }

            Vector3 delta = targetPosition - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (outrunChaseTarget == null && routeWaypoints != null && routeWaypoints.Length > 0 && distance <= waypointRadius)
            {
                AdvanceWaypoint();
                if (!TryResolveDriveTarget(out targetPosition))
                {
                    return;
                }

                delta = targetPosition - transform.position;
                delta.y = 0f;
            }

            float signedAngle = Vector3.SignedAngle(transform.forward, delta.normalized, Vector3.up);
            float speedKph = telemetry != null ? telemetry.SpeedKph : (body != null ? body.linearVelocity.magnitude * 3.6f : 0f);
            float aggression = definition != null ? definition.aggression : 0.72f;
            float targetSpeed = definition != null ? definition.roamTargetSpeedKph : 112f;
            float angle01 = Mathf.InverseLerp(0f, hardBrakeAngle, Mathf.Abs(signedAngle));
            float cornerSpeed = Mathf.Lerp(targetSpeed, Mathf.Max(minimumRouteSpeedKph, targetSpeed * 0.45f), angle01);
            float speedError = cornerSpeed - speedKph;

            UpdateMistake(aggression);

            float steer = Mathf.Clamp(signedAngle / Mathf.Max(1f, lookAheadSteerAngle), -1f, 1f);
            steer = Mathf.Clamp(steer + currentMistake, -1f, 1f);

            float throttle = speedError > 0f ? Mathf.Lerp(0.35f, 1f, aggression) : 0f;
            float brake = speedError < -7f || Mathf.Abs(signedAngle) > brakingAngle
                ? Mathf.InverseLerp(0f, 45f, -speedError) * Mathf.Lerp(0.35f, 0.82f, angle01)
                : 0f;
            bool handbrake = Mathf.Abs(signedAngle) > handbrakeAngle && speedKph > 55f && aggression > 0.62f;

            driverInput.SetManual(steer, throttle, Mathf.Clamp01(brake), handbrake, false);
        }

        public void SetDefinition(FTOutrunRivalDefinition newDefinition)
        {
            definition = newDefinition;
            ApplyDefinitionIfAvailable();
        }

        public void SetRoute(Transform[] waypoints)
        {
            routeWaypoints = waypoints;
            waypointIndex = 0;
        }

        public void SetRoute(FTOutrunRoute newRoute)
        {
            route = newRoute;
            routeWaypoints = route != null ? route.Waypoints : null;
            waypointIndex = 0;
        }

        public void SetOutrunChaseTarget(Transform target)
        {
            outrunChaseTarget = target;
        }

        private void ResolveReferences()
        {
            if (driverInput == null) driverInput = GetComponent<FTDriverInput>();
            if (driverInput == null) driverInput = gameObject.AddComponent<FTDriverInput>();
            if (telemetry == null) telemetry = GetComponent<FTVehicleTelemetry>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        private void TryResolveRouteWaypoints()
        {
            if (routeWaypoints != null && routeWaypoints.Length > 0)
            {
                return;
            }

            if (route == null)
            {
                if (Time.unscaledTime < nextRouteLookupTime)
                {
                    return;
                }

                nextRouteLookupTime = Time.unscaledTime + Mathf.Max(0.5f, routeLookupInterval);
                route = FindFirstObjectByType<FTOutrunRoute>();
            }

            if (route != null)
            {
                routeWaypoints = route.Waypoints;
            }
        }

        private bool TryResolveDriveTarget(out Vector3 targetPosition)
        {
            if (outrunChaseTarget != null)
            {
                Vector3 chaseVelocity = Vector3.zero;
                if (outrunChaseTarget.TryGetComponent(out Rigidbody targetBody))
                {
                    chaseVelocity = targetBody.linearVelocity;
                }

                Vector3 chaseForward = chaseVelocity.sqrMagnitude > 9f
                    ? chaseVelocity.normalized
                    : outrunChaseTarget.forward;
                targetPosition = outrunChaseTarget.position + chaseForward * chaseLeadMeters;
                return true;
            }

            TryResolveRouteWaypoints();
            Transform waypoint = ResolveCurrentWaypoint();
            if (waypoint != null)
            {
                targetPosition = waypoint.position;
                return true;
            }

            if (allowOpenRoamWithoutRoute)
            {
                targetPosition = ResolveOpenRoamTarget();
                return true;
            }

            targetPosition = transform.position;
            return false;
        }

        private Vector3 ResolveOpenRoamTarget()
        {
            if (!hasOpenRoamTarget || Time.time >= nextOpenRoamTargetTime || Vector3.Distance(transform.position, openRoamTarget) <= waypointRadius)
            {
                nextOpenRoamTargetTime = Time.time + Mathf.Max(0.5f, openRoamRetargetSeconds);
                float forwardDistance = Random.Range(openRoamForwardDistance * 0.65f, openRoamForwardDistance * 1.25f);
                float sideDistance = Random.Range(-openRoamSideDistance, openRoamSideDistance);
                Vector3 candidate = transform.position + transform.forward * forwardDistance + transform.right * sideDistance;

                if (Physics.Raycast(candidate + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 180f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    candidate = hit.point;
                }
                else
                {
                    candidate.y = transform.position.y;
                }

                openRoamTarget = candidate;
                hasOpenRoamTarget = true;
            }

            return openRoamTarget;
        }

        private void ApplyDefinitionIfAvailable()
        {
            if (definition == null || definition.carDefinition == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IFTVehicleDefinitionReceiver receiver)
                {
                    receiver.ApplyDefinition(definition.carDefinition);
                }
            }

            Debug.Log($"[SacredCore] Outrun rival '{definition.displayName}' bound to car={definition.carDefinition.carId}, targetSpeed={definition.roamTargetSpeedKph:0}.");
        }

        private Transform ResolveCurrentWaypoint()
        {
            if (routeWaypoints == null || routeWaypoints.Length == 0)
            {
                return null;
            }

            waypointIndex = Mathf.Clamp(waypointIndex, 0, routeWaypoints.Length - 1);
            return routeWaypoints[waypointIndex];
        }

        private void AdvanceWaypoint()
        {
            if (routeWaypoints == null || routeWaypoints.Length == 0)
            {
                return;
            }

            waypointIndex++;
            if (waypointIndex >= routeWaypoints.Length)
            {
                waypointIndex = loopRoute ? 0 : routeWaypoints.Length - 1;
            }
        }

        private void UpdateMistake(float aggression)
        {
            if (definition == null || definition.mistakeChance <= 0f)
            {
                currentMistake = Mathf.MoveTowards(currentMistake, 0f, Time.deltaTime * 0.8f);
                return;
            }

            mistakeTimer -= Time.deltaTime;
            if (mistakeTimer <= 0f)
            {
                mistakeTimer = Random.Range(2.5f, 6.5f);
                float chance = definition.mistakeChance * Mathf.Lerp(1.2f, 0.45f, aggression);
                currentMistake = Random.value < chance ? Random.Range(-0.18f, 0.18f) : 0f;
            }

            currentMistake = Mathf.MoveTowards(currentMistake, 0f, Time.deltaTime * 0.18f);
        }
    }
}

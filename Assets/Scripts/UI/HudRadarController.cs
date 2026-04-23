using System.Collections.Generic;
using FullThrottle.SacredCore.Race;
using UnityEngine;
using UnityEngine.UI;
using Underground.Vehicle;

namespace Underground.UI
{
    public class HudRadarController : MonoBehaviour
    {
        private static Sprite fallbackSprite;
        private static Sprite orangeArrowSprite;

        [SerializeField] private Underground.Vehicle.V2.VehicleControllerV2 vehicle;
        [SerializeField] private FullThrottle.SacredCore.Vehicle.FTVehicleTelemetry sacredVehicle;
        [SerializeField] private RectTransform radarBounds;
        [SerializeField] private RectTransform markerLayer;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private float worldRange = 220f;
        [SerializeField] private float edgePadding = 16f;
        [SerializeField] private Color raceMarkerColor = new Color(1f, 0.33f, 0.75f, 0.95f);
        [SerializeField] private Color garageMarkerColor = new Color(0.25f, 0.88f, 1f, 0.95f);
        [SerializeField] private Color respawnMarkerColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color outrunRivalMarkerColor = new Color(1f, 0.48f, 0.05f, 0.98f);
        [SerializeField] private bool periodicallyRefreshMarkers = true;
        [SerializeField] private float markerRefreshInterval = 2f;

        private readonly List<RadarMarker> markers = new List<RadarMarker>();
        private float nextRefreshTime;

        public void BindView(RectTransform bounds, RectTransform markerParent, RectTransform playerIcon)
        {
            radarBounds = bounds;
            markerLayer = markerParent;
            playerMarker = playerIcon;
            RebuildMarkers();
        }

        private void Awake()
        {
            if (vehicle == null)
            {
                vehicle = FindFirstObjectByType<Underground.Vehicle.V2.VehicleControllerV2>();
            }

            if (sacredVehicle == null)
            {
                sacredVehicle = FindSacredVehicleTarget();
            }
        }

        private void LateUpdate()
        {
            if (vehicle == null && sacredVehicle == null)
            {
                vehicle = FindFirstObjectByType<Underground.Vehicle.V2.VehicleControllerV2>();
                sacredVehicle = FindSacredVehicleTarget();
            }

            Transform radarTarget = ResolveRadarTarget();
            if (radarTarget == null || radarBounds == null || markerLayer == null)
            {
                return;
            }

            if (periodicallyRefreshMarkers && Time.unscaledTime >= nextRefreshTime)
            {
                RebuildMarkers();
                nextRefreshTime = Time.unscaledTime + Mathf.Max(1f, markerRefreshInterval);
            }

            UpdatePlayerMarker();
            UpdateWorldMarkers();
        }

        private void RebuildMarkers()
        {
            if (markerLayer == null)
            {
                return;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].icon != null)
                {
                    Destroy(markers[i].icon.gameObject);
                }
            }

            markers.Clear();
            RegisterMarkersWithTag("RaceStart", raceMarkerColor, new Vector2(10f, 10f));
            RegisterMarkersWithTag("Garage", garageMarkerColor, new Vector2(12f, 12f));
            RegisterMarkersWithTag("RespawnPoint", respawnMarkerColor, new Vector2(8f, 8f));
            RegisterOutrunRivalMarkers();
        }

        private void RegisterMarkersWithTag(string tag, Color color, Vector2 size)
        {
            GameObject[] taggedObjects;
            try
            {
                taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                return;
            }

            Sprite sprite = CreateFallbackSprite();

            for (int i = 0; i < taggedObjects.Length; i++)
            {
                GameObject taggedObject = taggedObjects[i];
                Transform radarTarget = ResolveRadarTarget();
                if (taggedObject == null || (radarTarget != null && taggedObject == radarTarget.gameObject))
                {
                    continue;
                }

                GameObject iconObject = new GameObject($"{tag}_RadarMarker", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(markerLayer, false);
                RectTransform rectTransform = iconObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = size;

                Image image = iconObject.GetComponent<Image>();
                image.sprite = sprite;
                image.color = color;

                markers.Add(new RadarMarker
                {
                    target = taggedObject.transform,
                    icon = rectTransform,
                    rotateWithTarget = false
                });
            }
        }

        private void RegisterOutrunRivalMarkers()
        {
            FTOutrunRivalDriver[] rivals = FindObjectsByType<FTOutrunRivalDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Sprite sprite = CreateOrangeArrowSprite();

            for (int i = 0; i < rivals.Length; i++)
            {
                FTOutrunRivalDriver rival = rivals[i];
                if (rival == null || markerLayer == null)
                {
                    continue;
                }

                GameObject iconObject = new GameObject("OutrunRival_RadarMarker", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(markerLayer, false);
                RectTransform rectTransform = iconObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(18f, 18f);

                Image image = iconObject.GetComponent<Image>();
                image.sprite = sprite;
                image.color = outrunRivalMarkerColor;

                markers.Add(new RadarMarker
                {
                    target = rival.transform,
                    icon = rectTransform,
                    rotateWithTarget = true
                });
            }
        }

        private static Sprite CreateFallbackSprite()
        {
            if (fallbackSprite == null)
            {
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;
                fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                fallbackSprite.name = "GeneratedRadarSprite";
            }

            return fallbackSprite;
        }

        private static Sprite CreateOrangeArrowSprite()
        {
            if (orangeArrowSprite != null)
            {
                return orangeArrowSprite;
            }

            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            for (int y = 3; y < size - 3; y++)
            {
                float halfWidth = Mathf.Lerp(3f, 14f, y / (float)(size - 1));
                float center = (size - 1) * 0.5f;
                for (int x = 0; x < size; x++)
                {
                    if (Mathf.Abs(x - center) <= halfWidth)
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            orangeArrowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            orangeArrowSprite.name = "GeneratedOutrunRadarArrow";
            return orangeArrowSprite;
        }

        private void UpdatePlayerMarker()
        {
            if (playerMarker == null || ResolveRadarTarget() == null)
            {
                return;
            }

            playerMarker.localRotation = Quaternion.identity;
        }

        private void UpdateWorldMarkers()
        {
            float radarRadius = Mathf.Max(8f, (Mathf.Min(radarBounds.rect.width, radarBounds.rect.height) * 0.5f) - edgePadding);
            Transform radarTarget = ResolveRadarTarget();
            if (radarTarget == null)
            {
                return;
            }

            for (int i = markers.Count - 1; i >= 0; i--)
            {
                RadarMarker marker = markers[i];
                if (marker.target == null || marker.icon == null)
                {
                    if (marker.icon != null)
                    {
                        Destroy(marker.icon.gameObject);
                    }

                    markers.RemoveAt(i);
                    continue;
                }

                Vector3 worldOffset = marker.target.position - radarTarget.position;
                Vector3 localOffset = radarTarget.InverseTransformDirection(worldOffset);
                Vector2 radarOffset = new Vector2(localOffset.x, localOffset.z);
                float normalizedDistance = radarOffset.magnitude / Mathf.Max(1f, worldRange);

                if (normalizedDistance > 1.35f)
                {
                    marker.icon.gameObject.SetActive(false);
                    continue;
                }

                marker.icon.gameObject.SetActive(true);
                Vector2 clampedOffset = Vector2.ClampMagnitude(radarOffset / Mathf.Max(1f, worldRange) * radarRadius, radarRadius);
                marker.icon.anchoredPosition = clampedOffset;
                marker.icon.localRotation = marker.rotateWithTarget
                    ? Quaternion.Euler(0f, 0f, radarTarget.eulerAngles.y - marker.target.eulerAngles.y)
                    : Quaternion.identity;
            }
        }

        private Transform ResolveRadarTarget()
        {
            if (vehicle != null)
            {
                return vehicle.transform;
            }

            return sacredVehicle != null ? sacredVehicle.transform : null;
        }

        private static FullThrottle.SacredCore.Vehicle.FTVehicleTelemetry FindSacredVehicleTarget()
        {
            FullThrottle.SacredCore.Vehicle.FTVehicleTelemetry[] vehicles =
                FindObjectsByType<FullThrottle.SacredCore.Vehicle.FTVehicleTelemetry>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            FullThrottle.SacredCore.Vehicle.FTVehicleTelemetry fallback = null;
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] == null)
                {
                    continue;
                }

                fallback ??= vehicles[i];
                if (vehicles[i].CompareTag("Player") || vehicles[i].name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return vehicles[i];
                }
            }

            return fallback;
        }

        private struct RadarMarker
        {
            public Transform target;
            public RectTransform icon;
            public bool rotateWithTarget;
        }
    }
}


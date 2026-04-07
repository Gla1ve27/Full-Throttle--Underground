using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Underground.Vehicle;

namespace Underground.UI
{
    public class HudRadarController : MonoBehaviour
    {
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private RectTransform radarBounds;
        [SerializeField] private RectTransform markerLayer;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private float worldRange = 220f;
        [SerializeField] private float edgePadding = 16f;
        [SerializeField] private Color raceMarkerColor = new Color(1f, 0.33f, 0.75f, 0.95f);
        [SerializeField] private Color garageMarkerColor = new Color(0.25f, 0.88f, 1f, 0.95f);
        [SerializeField] private Color respawnMarkerColor = new Color(1f, 1f, 1f, 0.95f);

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
                vehicle = FindFirstObjectByType<VehicleDynamicsController>();
            }
        }

        private void LateUpdate()
        {
            if (vehicle == null)
            {
                vehicle = FindFirstObjectByType<VehicleDynamicsController>();
            }

            if (vehicle == null || radarBounds == null || markerLayer == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                RebuildMarkers();
                nextRefreshTime = Time.unscaledTime + 1.5f;
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
        }

        private void RegisterMarkersWithTag(string tag, Color color, Vector2 size)
        {
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            Sprite sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

            for (int i = 0; i < taggedObjects.Length; i++)
            {
                GameObject taggedObject = taggedObjects[i];
                if (taggedObject == null || taggedObject == vehicle.gameObject)
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
                    icon = rectTransform
                });
            }
        }

        private void UpdatePlayerMarker()
        {
            if (playerMarker == null || vehicle == null)
            {
                return;
            }

            playerMarker.localRotation = Quaternion.identity;
        }

        private void UpdateWorldMarkers()
        {
            float radarRadius = Mathf.Max(8f, (Mathf.Min(radarBounds.rect.width, radarBounds.rect.height) * 0.5f) - edgePadding);

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

                Vector3 worldOffset = marker.target.position - vehicle.transform.position;
                Vector3 localOffset = vehicle.transform.InverseTransformDirection(worldOffset);
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
            }
        }

        private struct RadarMarker
        {
            public Transform target;
            public RectTransform icon;
        }
    }
}

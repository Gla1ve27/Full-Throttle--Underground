using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FullThrottle.Minimap
{
    /// <summary>
    /// Core minimap UI controller.
    /// Keeps the player arrow centered, places marker icons, and supports map rotation.
    /// Attach this to your MinimapRoot UI object.
    /// </summary>
    public class MinimapSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform mapViewport;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform iconContainer;
        [SerializeField] private RectTransform playerArrow;
        [SerializeField] private MinimapCameraController minimapCamera;
        [SerializeField] private Transform playerTarget;

        [Header("Presentation")]
        [SerializeField] private bool rotateMapWithPlayer = true;
        [SerializeField] private bool rotatePlayerArrowWhenNorthUp = true;
        [SerializeField] private float worldUnitsVisibleRadius = 70f;
        [SerializeField] private float iconEdgePadding = 10f;

        private readonly List<MinimapMarker> markers = new();
        private float ViewRadiusPixels => Mathf.Max(1f, Mathf.Min(mapViewport.rect.width, mapViewport.rect.height) * 0.5f - iconEdgePadding);

        public Transform PlayerTarget => playerTarget;
        public bool RotateMapWithPlayer => rotateMapWithPlayer;

        private void Awake()
        {
            if (playerTarget == null && minimapCamera != null)
            {
                playerTarget = minimapCamera.Target;
            }
        }

        private void OnEnable()
        {
            MinimapMarker[] sceneMarkers = FindObjectsByType<MinimapMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sceneMarkers.Length; i++)
            {
                RegisterMarker(sceneMarkers[i]);
            }
        }

        private void LateUpdate()
        {
            if (playerTarget == null || mapViewport == null) return;

            UpdateMapRotation();
            UpdatePlayerArrow();
            UpdateMarkers();
        }

        public void RegisterMarker(MinimapMarker marker)
        {
            if (marker == null || markers.Contains(marker)) return;
            markers.Add(marker);
            marker.Bind(this);
        }

        public void UnregisterMarker(MinimapMarker marker)
        {
            if (marker == null) return;
            markers.Remove(marker);
        }

        public Vector2 WorldToMapPosition(Vector3 worldPosition)
        {
            return WorldToMapPosition(worldPosition, true, out _);
        }

        public Vector2 WorldToMapPosition(Vector3 worldPosition, bool clampToEdge, out bool inside)
        {
            Vector3 delta = worldPosition - playerTarget.position;
            Vector2 planar = new Vector2(delta.x, delta.z);

            if (rotateMapWithPlayer)
            {
                float angle = -playerTarget.eulerAngles.y * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                planar = new Vector2(
                    planar.x * cos - planar.y * sin,
                    planar.x * sin + planar.y * cos
                );
            }

            float scale = ViewRadiusPixels / Mathf.Max(1f, worldUnitsVisibleRadius);
            Vector2 uiPos = planar * scale;

            float maxRadius = ViewRadiusPixels;
            inside = uiPos.sqrMagnitude <= maxRadius * maxRadius;
            if (uiPos.magnitude > maxRadius)
            {
                uiPos = clampToEdge ? uiPos.normalized * maxRadius : uiPos;
            }

            return uiPos;
        }

        private void UpdateMapRotation()
        {
            if (mapImage == null) return;

            mapImage.rectTransform.localRotation = Quaternion.identity;
            if (iconContainer != null)
            {
                iconContainer.localRotation = Quaternion.identity;
            }

            if (minimapCamera != null)
            {
                minimapCamera.SetRotateWithTarget(rotateMapWithPlayer);
            }
        }

        private void UpdatePlayerArrow()
        {
            if (playerArrow == null) return;
            playerArrow.anchoredPosition = Vector2.zero;

            if (rotateMapWithPlayer)
            {
                playerArrow.localRotation = Quaternion.identity;
            }
            else if (rotatePlayerArrowWhenNorthUp)
            {
                playerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerTarget.eulerAngles.y);
            }
        }

        private void UpdateMarkers()
        {
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                if (markers[i] == null)
                {
                    markers.RemoveAt(i);
                    continue;
                }

                markers[i].Refresh();
            }
        }
    }
}

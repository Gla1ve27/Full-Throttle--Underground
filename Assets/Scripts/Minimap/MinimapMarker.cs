using UnityEngine;
using UnityEngine.UI;

namespace FullThrottle.Minimap
{
    /// <summary>
    /// Register this on a world object that should show an icon in the minimap.
    /// </summary>
    public class MinimapMarker : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform iconRect;
        [SerializeField] private Image iconImage;
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Color iconColor = Color.white;

        [Header("Behavior")]
        [SerializeField] private bool clampToEdge = true;
        [SerializeField] private bool rotateWithWorldObject = false;
        [SerializeField] private Vector3 worldOffset = Vector3.zero;
        [SerializeField] private bool hideWhenVeryClose = false;
        [SerializeField] private float hideDistance = 4f;

        private MinimapSystem minimap;

        public void Bind(MinimapSystem system)
        {
            minimap = system;
            if (iconImage != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.color = iconColor;
            }
        }

        private void OnEnable()
        {
            if (minimap != null)
            {
                minimap.RegisterMarker(this);
            }
        }

        private void Start()
        {
            if (minimap == null)
            {
                minimap = FindFirstObjectByType<MinimapSystem>();
                if (minimap != null)
                {
                    minimap.RegisterMarker(this);
                }
            }
        }

        private void OnDisable()
        {
            if (minimap != null)
            {
                minimap.UnregisterMarker(this);
            }
        }

        public void Refresh()
        {
            if (minimap == null || iconRect == null || minimap.PlayerTarget == null) return;

            Vector3 markerWorldPosition = transform.position + worldOffset;
            Vector2 pos = minimap.WorldToMapPosition(markerWorldPosition, clampToEdge, out bool inside);

            if (!clampToEdge)
            {
                if (iconRect.gameObject.activeSelf != inside)
                {
                    iconRect.gameObject.SetActive(inside);
                }

                if (!inside)
                {
                    return;
                }
            }
            else if (!iconRect.gameObject.activeSelf)
            {
                iconRect.gameObject.SetActive(true);
            }

            if (hideWhenVeryClose)
            {
                Vector3 delta = markerWorldPosition - minimap.PlayerTarget.position;
                float distance = new Vector2(delta.x, delta.z).magnitude;
                bool visible = distance > hideDistance;
                if (iconRect.gameObject.activeSelf != visible)
                {
                    iconRect.gameObject.SetActive(visible);
                }
                if (!visible) return;
            }

            iconRect.anchoredPosition = pos;

            if (rotateWithWorldObject)
            {
                float zRotation = minimap.RotateMapWithPlayer ? (transform.eulerAngles.y - minimap.PlayerTarget.eulerAngles.y) : -transform.eulerAngles.y;
                iconRect.localRotation = Quaternion.Euler(0f, 0f, -zRotation);
            }
            else
            {
                iconRect.localRotation = Quaternion.identity;
            }
        }
    }
}

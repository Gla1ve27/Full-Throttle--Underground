using UnityEngine;
using UnityEngine.UI;

namespace Underground.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public class ResponsiveCanvasController : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;

        private void Awake()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Apply();
        }
#endif

        private void Apply()
        {
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }
    }
}

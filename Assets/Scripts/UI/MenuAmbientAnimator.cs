using UnityEngine;
using UnityEngine.UI;

namespace Underground.UI
{
    public class MenuAmbientAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform targetTransform;
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Vector2 movementAmplitude = new Vector2(0f, 14f);
        [SerializeField] private float movementSpeed = 0.75f;
        [SerializeField] private float baseAlpha = 0.28f;
        [SerializeField] private float alphaAmplitude = 0.12f;

        private Vector2 origin;
        private Color graphicColor;

        private void Awake()
        {
            targetTransform ??= transform as RectTransform;
            targetGraphic ??= GetComponent<Graphic>();

            if (targetTransform != null)
            {
                origin = targetTransform.anchoredPosition;
            }

            if (targetGraphic != null)
            {
                graphicColor = targetGraphic.color;
            }
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.unscaledTime * movementSpeed);

            if (targetTransform != null)
            {
                targetTransform.anchoredPosition = origin + (movementAmplitude * wave);
            }

            if (targetGraphic != null)
            {
                Color animatedColor = graphicColor;
                animatedColor.a = Mathf.Clamp01(baseAlpha + (wave * alphaAmplitude));
                targetGraphic.color = animatedColor;
            }
        }
    }
}

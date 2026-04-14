using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FullThrottle.Audio
{
    public class RadioNowPlayingPopup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text artistText;
        [SerializeField] private TMP_Text stationText;
        [SerializeField] private Image albumArtImage;
        [SerializeField] private Sprite fallbackAlbumArt;

        [Header("Animation")]
        [SerializeField] private Vector2 hiddenAnchoredPosition = new Vector2(-700f, -40f);
        [SerializeField] private Vector2 shownAnchoredPosition = new Vector2(24f, -40f);
        [SerializeField] private float fadeInDuration = 0.18f;
        [SerializeField] private float slideInDuration = 0.32f;
        [SerializeField] private float visibleDuration = 4.25f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float slideOutDuration = 0.28f;
        [SerializeField] private AnimationCurve moveCurve = null;
        [SerializeField] private AnimationCurve fadeCurve = null;

        private Coroutine popupRoutine;

        private void Reset()
        {
            panel = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            if (panel == null)
                panel = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (moveCurve == null || moveCurve.length == 0)
                moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            if (fadeCurve == null || fadeCurve.length == 0)
                fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            HideImmediately();
        }

        public void Show(RuntimeRadioTrack track, string stationLabel = null)
        {
            if (track == null || panel == null || canvasGroup == null)
                return;

            if (titleText != null)
                titleText.text = track.DisplayTitle;

            if (artistText != null)
                artistText.text = track.DisplayArtist;

            if (stationText != null)
            {
                bool hasStation = !string.IsNullOrWhiteSpace(stationLabel);
                stationText.gameObject.SetActive(hasStation);
                if (hasStation)
                    stationText.text = stationLabel;
            }

            if (albumArtImage != null)
            {
                Sprite targetSprite = track.albumArt != null ? track.albumArt : fallbackAlbumArt;
                albumArtImage.sprite = targetSprite;
                albumArtImage.enabled = targetSprite != null;
            }

            if (popupRoutine != null)
                StopCoroutine(popupRoutine);

            popupRoutine = StartCoroutine(ShowRoutine());
        }

        public void HideImmediately()
        {
            if (panel != null)
                panel.anchoredPosition = hiddenAnchoredPosition;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private IEnumerator ShowRoutine()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            yield return Animate(hiddenAnchoredPosition, shownAnchoredPosition, 0f, 1f, Mathf.Max(fadeInDuration, slideInDuration));
            yield return new WaitForSecondsRealtime(visibleDuration);
            yield return Animate(shownAnchoredPosition, hiddenAnchoredPosition, 1f, 0f, Mathf.Max(fadeOutDuration, slideOutDuration));

            popupRoutine = null;
        }

        private IEnumerator Animate(Vector2 fromPosition, Vector2 toPosition, float fromAlpha, float toAlpha, float duration)
        {
            float time = 0f;
            duration = Mathf.Max(0.0001f, duration);

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);

                float moveT = moveCurve != null ? moveCurve.Evaluate(t) : t;
                float fadeT = fadeCurve != null ? fadeCurve.Evaluate(t) : t;

                if (panel != null)
                    panel.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, moveT);

                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, fadeT);

                yield return null;
            }

            if (panel != null)
                panel.anchoredPosition = toPosition;

            if (canvasGroup != null)
                canvasGroup.alpha = toAlpha;
        }
    }
}

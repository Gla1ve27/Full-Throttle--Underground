using System.Collections;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using TMPro;
using UnityEngine;

namespace FullThrottle.SacredCore.Campaign
{
    [DefaultExecutionOrder(-9450)]
    public sealed class FTNarrativeDirector : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private bool useFallbackOnGui = true;
        [SerializeField] private float fallbackLineSeconds = 3.5f;

        private FTSaveGateway saveGateway;
        private FTSignalBus bus;
        private Coroutine playbackRoutine;
        private string fallbackTitle = string.Empty;
        private string fallbackBody = string.Empty;
        private float fallbackVisibleUntil;

        private void Awake()
        {
            FTServices.Register(this);
            FTServices.TryGet(out saveGateway);
            FTServices.TryGet(out bus);
            HideOverlay();
        }

        private void OnGUI()
        {
            if (!useFallbackOnGui || overlayGroup != null || Time.unscaledTime > fallbackVisibleUntil || string.IsNullOrWhiteSpace(fallbackBody))
            {
                return;
            }

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1f, 0.35f, 0.72f, 1f);

            GUIStyle bodyStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            bodyStyle.normal.textColor = Color.white;

            float width = Mathf.Min(860f, Screen.width - 80f);
            Rect titleRect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.68f, width, 30f);
            Rect bodyRect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, 72f);
            GUI.Label(titleRect, fallbackTitle, titleStyle);
            GUI.Box(bodyRect, fallbackBody, bodyStyle);
        }

        public void PlayBeat(FTNarrativeBeatDefinition beat)
        {
            if (beat == null)
            {
                return;
            }

            if (saveGateway != null && !saveGateway.Profile.seenNarrativeBeatIds.Contains(beat.beatId))
            {
                saveGateway.Profile.seenNarrativeBeatIds.Add(beat.beatId);
                saveGateway.Save();
            }

            bus?.Raise(new FTNarrativeBeatTriggeredSignal(beat.beatId, beat.chapterId, beat.beatType));

            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
            }

            playbackRoutine = StartCoroutine(PlayRoutine(beat));
            Debug.Log($"[SacredCore] Narrative beat: {beat.beatId}, type={beat.beatType}, chapter={beat.chapterId}, title={beat.title}.");
        }

        private IEnumerator PlayRoutine(FTNarrativeBeatDefinition beat)
        {
            ShowOverlay();
            string firstBody = ResolveFirstBody(beat);
            SetText(beat.title, firstBody);

            if (beat.dialogue == null || beat.dialogue.Count == 0)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(beat.minimumDuration, fallbackLineSeconds));
            }
            else
            {
                for (int i = 0; i < beat.dialogue.Count; i++)
                {
                    FTDialogueLine line = beat.dialogue[i];
                    string speaker = string.IsNullOrWhiteSpace(line.displayName) ? line.characterId : line.displayName;
                    SetText(beat.title, $"{speaker}: {line.line}");
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, fallbackLineSeconds + line.delayAfter));
                }
            }

            HideOverlay();
            playbackRoutine = null;
        }

        private string ResolveFirstBody(FTNarrativeBeatDefinition beat)
        {
            if (!string.IsNullOrWhiteSpace(beat.radioCopy))
            {
                return beat.radioCopy;
            }

            if (beat.dialogue != null && beat.dialogue.Count > 0)
            {
                FTDialogueLine line = beat.dialogue[0];
                string speaker = string.IsNullOrWhiteSpace(line.displayName) ? line.characterId : line.displayName;
                return $"{speaker}: {line.line}";
            }

            return beat.cinematicDirection;
        }

        private void SetText(string title, string body)
        {
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (bodyText != null)
            {
                bodyText.text = body;
            }

            fallbackTitle = title;
            fallbackBody = body;
            fallbackVisibleUntil = Time.unscaledTime + fallbackLineSeconds;
        }

        private void ShowOverlay()
        {
            if (overlayGroup == null)
            {
                return;
            }

            overlayGroup.alpha = 1f;
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = false;
        }

        private void HideOverlay()
        {
            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
                overlayGroup.blocksRaycasts = false;
            }

            fallbackVisibleUntil = 0f;
        }
    }
}

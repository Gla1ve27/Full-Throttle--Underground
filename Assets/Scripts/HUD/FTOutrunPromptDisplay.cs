using FullThrottle.SacredCore.Race;
using TMPro;
using UnityEngine;

namespace FullThrottle.SacredCore.HUD
{
    public sealed class FTOutrunPromptDisplay : MonoBehaviour
    {
        [SerializeField] private FTOutrunChallengeDirector director;
        [SerializeField] private TMP_Text promptText;

        private void Awake()
        {
            if (director == null)
            {
                director = FindFirstObjectByType<FTOutrunChallengeDirector>();
            }

            if (promptText == null)
            {
                promptText = GetComponentInChildren<TMP_Text>(true);
            }

            if (director != null && promptText != null)
            {
                director.BindPromptText(promptText);
            }
        }
    }
}

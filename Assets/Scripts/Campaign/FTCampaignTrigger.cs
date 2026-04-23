using FullThrottle.SacredCore.Runtime;
using UnityEngine;

namespace FullThrottle.SacredCore.Campaign
{
    public enum FTCampaignTriggerAction
    {
        StartChapter,
        CompleteChapter,
        PlayNarrativeBeat,
        PlayCurrentGarageBeat,
        PlayCurrentRadioFollowUp
    }

    public sealed class FTCampaignTrigger : MonoBehaviour
    {
        [SerializeField] private FTCampaignTriggerAction action = FTCampaignTriggerAction.PlayNarrativeBeat;
        [SerializeField] private string chapterId;
        [SerializeField] private FTNarrativeBeatDefinition beat;
        [SerializeField] private bool playerOnly = true;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private KeyCode interactionKey = KeyCode.None;

        private FTCampaignDirector campaignDirector;
        private FTNarrativeDirector narrativeDirector;
        private bool playerInside;
        private bool fired;

        private void Awake()
        {
            FTServices.TryGet(out campaignDirector);
            FTServices.TryGet(out narrativeDirector);
        }

        private void Update()
        {
            if (interactionKey == KeyCode.None || !playerInside || fired && triggerOnce)
            {
                return;
            }

            if (Input.GetKeyDown(interactionKey))
            {
                Fire();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (playerOnly && !IsPlayer(other))
            {
                return;
            }

            playerInside = true;
            if (interactionKey == KeyCode.None)
            {
                Fire();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!playerOnly || IsPlayer(other))
            {
                playerInside = false;
            }
        }

        public void Fire()
        {
            if (fired && triggerOnce)
            {
                return;
            }

            if (campaignDirector == null)
            {
                FTServices.TryGet(out campaignDirector);
            }

            if (narrativeDirector == null)
            {
                FTServices.TryGet(out narrativeDirector);
            }

            switch (action)
            {
                case FTCampaignTriggerAction.StartChapter:
                    campaignDirector?.TryStartChapter(chapterId);
                    break;
                case FTCampaignTriggerAction.CompleteChapter:
                    campaignDirector?.CompleteChapter(chapterId);
                    break;
                case FTCampaignTriggerAction.PlayNarrativeBeat:
                    narrativeDirector?.PlayBeat(beat);
                    break;
                case FTCampaignTriggerAction.PlayCurrentGarageBeat:
                    campaignDirector?.TriggerGarageBeatForCurrentChapter();
                    break;
                case FTCampaignTriggerAction.PlayCurrentRadioFollowUp:
                    campaignDirector?.TriggerRadioFollowUpForCurrentChapter();
                    break;
            }

            fired = true;
            Debug.Log($"[SacredCore] Campaign trigger fired: {name}, action={action}, chapter={chapterId}, beat={(beat != null ? beat.beatId : "none")}.");
        }

        private static bool IsPlayer(Collider other)
        {
            return other != null
                && (other.CompareTag("Player")
                    || other.GetComponentInParent<FullThrottle.SacredCore.Vehicle.FTVehicleTelemetry>() != null
                    && other.GetComponentInParent<FullThrottle.SacredCore.Vehicle.FTVehicleTelemetry>().CompareTag("Player"));
        }
    }
}

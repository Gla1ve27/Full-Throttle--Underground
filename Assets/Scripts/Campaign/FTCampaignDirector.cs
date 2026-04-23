using System.Collections.Generic;
using FullThrottle.SacredCore.Race;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using UnityEngine;

namespace FullThrottle.SacredCore.Campaign
{
    [DefaultExecutionOrder(-9460)]
    public sealed class FTCampaignDirector : MonoBehaviour
    {
        [SerializeField] private FTCampaignDefinition campaign;
        [SerializeField] private bool autoStartFirstChapter = true;
        [SerializeField] private bool autoCompleteChapterWhenGatesMet = true;

        private readonly Dictionary<string, FTCampaignChapterDefinition> chapterMap = new();
        private FTSaveGateway saveGateway;
        private FTSignalBus bus;
        private FTNarrativeDirector narrativeDirector;

        public FTCampaignDefinition Campaign => campaign;
        public FTCampaignChapterDefinition CurrentChapter => ResolveChapter(saveGateway != null ? saveGateway.Profile.currentChapterId : "");

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            FTServices.TryGet(out bus);
            FTServices.TryGet(out narrativeDirector);
            Rebuild();
            bus?.Subscribe<FTRaceResolvedSignal>(OnRaceResolved);
        }

        private void Start()
        {
            if (autoStartFirstChapter && campaign != null && campaign.chapters.Count > 0 && string.IsNullOrWhiteSpace(saveGateway.Profile.currentChapterId))
            {
                TryStartChapter(campaign.chapters[0].chapterId);
            }

            RefreshCampaign();
        }

        private void OnDestroy()
        {
            bus?.Unsubscribe<FTRaceResolvedSignal>(OnRaceResolved);
        }

        public void Rebuild()
        {
            chapterMap.Clear();
            if (campaign == null)
            {
                return;
            }

            for (int i = 0; i < campaign.chapters.Count; i++)
            {
                FTCampaignChapterDefinition chapter = campaign.chapters[i];
                if (chapter != null && !string.IsNullOrWhiteSpace(chapter.chapterId))
                {
                    chapterMap[chapter.chapterId] = chapter;
                }
            }

            Debug.Log($"[SacredCore] Campaign indexed {chapterMap.Count} chapters for '{(campaign != null ? campaign.title : "none")}'.");
        }

        public bool TryStartChapter(string chapterId)
        {
            FTCampaignChapterDefinition chapter = ResolveChapter(chapterId);
            if (chapter == null)
            {
                Debug.LogWarning($"[SacredCore] Missing campaign chapter '{chapterId}'.");
                return false;
            }

            if (!AreChapterGatesMet(chapter, out string reason))
            {
                Debug.LogWarning($"[SacredCore] Chapter locked: {chapter.chapterId}. reason={reason}");
                return false;
            }

            FTProfileData profile = saveGateway.Profile;
            profile.currentChapterId = chapter.chapterId;
            profile.currentActId = chapter.actId;
            profile.currentDistrictId = chapter.districtId;
            Unlock(profile.unlockedChapterIds, chapter.chapterId);
            saveGateway.Save();

            bus?.Raise(new FTCampaignChapterStartedSignal(chapter.chapterId, chapter.title));
            Debug.Log($"[SacredCore] Campaign chapter started: {chapter.chapterNumber}. {chapter.title} ({chapter.chapterId}).");
            ResolveNarrativeDirector()?.PlayBeat(chapter.introBeat);
            return true;
        }

        public void CompleteChapter(string chapterId)
        {
            FTCampaignChapterDefinition chapter = ResolveChapter(chapterId);
            if (chapter == null)
            {
                return;
            }

            FTProfileData profile = saveGateway.Profile;
            Unlock(profile.completedChapterIds, chapter.chapterId);

            if (chapter.moneyReward > 0)
            {
                profile.bankMoney += chapter.moneyReward;
                bus?.Raise(new FTMoneyChangedSignal(profile.bankMoney));
            }

            if (chapter.reputationReward > 0)
            {
                profile.reputation += chapter.reputationReward;
                bus?.Raise(new FTRepChangedSignal(profile.reputation));
            }

            for (int i = 0; i < chapter.unlockDistrictIds.Count; i++)
            {
                Unlock(profile.unlockedDistrictIds, chapter.unlockDistrictIds[i]);
            }

            for (int i = 0; i < chapter.unlockRewardIds.Count; i++)
            {
                Unlock(profile.unlockedRewardIds, chapter.unlockRewardIds[i]);
            }

            for (int i = 0; i < chapter.unlockChapterIds.Count; i++)
            {
                Unlock(profile.unlockedChapterIds, chapter.unlockChapterIds[i]);
            }

            saveGateway.Save();
            bus?.Raise(new FTCampaignChapterCompletedSignal(chapter.chapterId, chapter.title));
            Debug.Log($"[SacredCore] Campaign chapter completed: {chapter.chapterNumber}. {chapter.title}. money+={chapter.moneyReward}, rep+={chapter.reputationReward}.");

            ResolveNarrativeDirector()?.PlayBeat(chapter.postRaceBeat);
            RefreshCampaign();
        }

        public void TriggerGarageBeatForCurrentChapter()
        {
            FTCampaignChapterDefinition chapter = CurrentChapter;
            if (chapter != null)
            {
                ResolveNarrativeDirector()?.PlayBeat(chapter.garageBeat);
            }
        }

        public void TriggerRadioFollowUpForCurrentChapter()
        {
            FTCampaignChapterDefinition chapter = CurrentChapter;
            if (chapter != null)
            {
                ResolveNarrativeDirector()?.PlayBeat(chapter.radioFollowUpBeat);
            }
        }

        public void RefreshCampaign()
        {
            if (campaign == null || saveGateway == null)
            {
                return;
            }

            FTProfileData profile = saveGateway.Profile;
            for (int i = 0; i < campaign.chapters.Count; i++)
            {
                FTCampaignChapterDefinition chapter = campaign.chapters[i];
                if (chapter == null || profile.completedChapterIds.Contains(chapter.chapterId))
                {
                    continue;
                }

                if (!AreChapterGatesMet(chapter, out _))
                {
                    continue;
                }

                Unlock(profile.unlockedChapterIds, chapter.chapterId);
                if (string.IsNullOrWhiteSpace(profile.currentChapterId)
                    || profile.completedChapterIds.Contains(profile.currentChapterId)
                    || ResolveChapter(profile.currentChapterId) == null)
                {
                    TryStartChapter(chapter.chapterId);
                }

                if (autoCompleteChapterWhenGatesMet && IsCompletionReady(chapter))
                {
                    CompleteChapter(chapter.chapterId);
                }

                break;
            }

            saveGateway.Save();
        }

        public bool AreChapterGatesMet(FTCampaignChapterDefinition chapter, out string reason)
        {
            reason = string.Empty;
            if (chapter == null || saveGateway == null)
            {
                reason = "missing chapter or save";
                return false;
            }

            FTProfileData profile = saveGateway.Profile;
            if (profile.reputation < chapter.requiredReputation)
            {
                reason = $"rep {profile.reputation}/{chapter.requiredReputation}";
                return false;
            }

            for (int i = 0; i < chapter.requiredChapterIds.Count; i++)
            {
                if (!profile.completedChapterIds.Contains(chapter.requiredChapterIds[i]))
                {
                    reason = $"chapter '{chapter.requiredChapterIds[i]}' not complete";
                    return false;
                }
            }

            for (int i = 0; i < chapter.requiredRaceWins.Count; i++)
            {
                if (!profile.completedRaceIds.Contains(chapter.requiredRaceWins[i]))
                {
                    reason = $"race '{chapter.requiredRaceWins[i]}' not won";
                    return false;
                }
            }

            for (int i = 0; i < chapter.requiredRivalWins.Count; i++)
            {
                if (!profile.beatenRivalIds.Contains(chapter.requiredRivalWins[i]))
                {
                    reason = $"rival '{chapter.requiredRivalWins[i]}' not beaten";
                    return false;
                }
            }

            return true;
        }

        private bool IsCompletionReady(FTCampaignChapterDefinition chapter)
        {
            if (chapter == null || saveGateway == null)
            {
                return false;
            }

            FTProfileData profile = saveGateway.Profile;
            for (int i = 0; i < chapter.requiredRaces.Count; i++)
            {
                FTRaceDefinition race = chapter.requiredRaces[i];
                if (race != null && !profile.completedRaceIds.Contains(race.raceId))
                {
                    return false;
                }
            }

            for (int i = 0; i < chapter.requiredRivalWins.Count; i++)
            {
                if (!profile.beatenRivalIds.Contains(chapter.requiredRivalWins[i]))
                {
                    return false;
                }
            }

            return chapter.requiredRaces.Count > 0 || chapter.requiredRivalWins.Count > 0;
        }

        private void OnRaceResolved(FTRaceResolvedSignal signal)
        {
            if (!signal.Won || signal.Race == null || saveGateway == null)
            {
                return;
            }

            Unlock(saveGateway.Profile.completedRaceIds, signal.Race.raceId);
            if (!string.IsNullOrWhiteSpace(signal.Race.rivalId))
            {
                Unlock(saveGateway.Profile.beatenRivalIds, signal.Race.rivalId);
            }

            saveGateway.Save();
            RefreshCampaign();
        }

        private FTCampaignChapterDefinition ResolveChapter(string chapterId)
        {
            if (!string.IsNullOrWhiteSpace(chapterId) && chapterMap.TryGetValue(chapterId, out FTCampaignChapterDefinition chapter))
            {
                return chapter;
            }

            return null;
        }

        private FTNarrativeDirector ResolveNarrativeDirector()
        {
            if (narrativeDirector == null)
            {
                FTServices.TryGet(out narrativeDirector);
            }

            return narrativeDirector;
        }

        private static void Unlock(List<string> list, string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && !list.Contains(id))
            {
                list.Add(id);
            }
        }
    }
}

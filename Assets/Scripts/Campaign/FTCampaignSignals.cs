using FullThrottle.SacredCore.Runtime;

namespace FullThrottle.SacredCore.Campaign
{
    public readonly struct FTCampaignChapterStartedSignal : IFTSignal
    {
        public readonly string ChapterId;
        public readonly string Title;

        public FTCampaignChapterStartedSignal(string chapterId, string title)
        {
            ChapterId = chapterId;
            Title = title;
        }
    }

    public readonly struct FTCampaignChapterCompletedSignal : IFTSignal
    {
        public readonly string ChapterId;
        public readonly string Title;

        public FTCampaignChapterCompletedSignal(string chapterId, string title)
        {
            ChapterId = chapterId;
            Title = title;
        }
    }

    public readonly struct FTNarrativeBeatTriggeredSignal : IFTSignal
    {
        public readonly string BeatId;
        public readonly string ChapterId;
        public readonly FTNarrativeBeatType BeatType;

        public FTNarrativeBeatTriggeredSignal(string beatId, string chapterId, FTNarrativeBeatType beatType)
        {
            BeatId = beatId;
            ChapterId = chapterId;
            BeatType = beatType;
        }
    }
}

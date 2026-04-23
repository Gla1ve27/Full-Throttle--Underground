using System;
using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Campaign
{
    public enum FTNarrativeBeatType
    {
        IntroCinematic,
        PreRace,
        PostRace,
        GarageScene,
        Radio,
        Message,
        Montage,
        DistrictTakeover,
        Epilogue
    }

    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Narrative Beat", fileName = "FT_NarrativeBeat")]
    public sealed class FTNarrativeBeatDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string beatId = "beat_city_intro";
        public string chapterId = "chapter_00_name_before_city";
        public string title = "The Name Before The City";
        public FTNarrativeBeatType beatType = FTNarrativeBeatType.IntroCinematic;

        [Header("Playback")]
        public bool blocksGameplay = true;
        public string cameraRigTag = "";
        public AudioClip musicSting;
        public AudioClip voiceOver;
        public float minimumDuration = 3f;

        [Header("Content")]
        [TextArea(2, 6)] public string cinematicDirection;
        public List<FTDialogueLine> dialogue = new();
        [TextArea(2, 6)] public string radioCopy;
    }

    [Serializable]
    public sealed class FTDialogueLine
    {
        public string characterId = "marc_badua";
        public string displayName = "Marc";
        [TextArea(1, 4)] public string line;
        public float delayAfter = 0.6f;
    }
}

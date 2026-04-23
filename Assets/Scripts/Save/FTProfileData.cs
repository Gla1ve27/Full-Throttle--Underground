using System;
using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Save
{
    [Serializable]
    public sealed class FTProfileData
    {
        public string playerAlias = "Gla1ve";
        public string currentActId = "act_01_unknown_name";
        public string currentChapterId = "chapter_00_name_before_city";
        public string currentDistrictId = "city_core";
        public string currentCarId = "";
        public int bankMoney = 5000;
        public int reputation = 0;
        public int heat = 0;
        public List<string> ownedCarIds = new();
        public List<string> beatenRivalIds = new();
        public List<string> completedRaceIds = new();
        public List<string> completedChapterIds = new();
        public List<string> unlockedChapterIds = new() { "chapter_00_name_before_city" };
        public List<string> seenNarrativeBeatIds = new();
        public List<string> unlockedRewardIds = new();
        public List<string> unlockedActIds = new() { "act_01_unknown_name" };
        public List<string> unlockedDistrictIds = new() { "city_core" };
        public FTSessionState session = new();

        public void EnsureDefaults(string fallbackCarId)
        {
            if (string.IsNullOrWhiteSpace(currentCarId))
            {
                currentCarId = fallbackCarId;
            }

            if (!ownedCarIds.Contains(currentCarId))
            {
                ownedCarIds.Add(currentCarId);
            }
        }

        public bool OwnsCar(string carId) => ownedCarIds.Contains(carId);

        public void OwnCar(string carId)
        {
            if (!ownedCarIds.Contains(carId))
            {
                ownedCarIds.Add(carId);
            }
        }
    }

    [Serializable]
    public sealed class FTSessionState
    {
        public int sessionMoney = 0;
        public int sessionReputation = 0;
        public int repairDebt = 0;
        public int wagerExposure = 0;
        public bool raceInProgress = false;
        public string activeRaceId = "";
    }

    [Serializable]
    internal sealed class FTProfileEnvelope
    {
        public FTProfileData profile = new();
    }
}

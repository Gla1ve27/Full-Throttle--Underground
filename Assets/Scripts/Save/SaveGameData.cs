using System;
using System.Collections.Generic;

namespace Underground.Save
{
    [Serializable]
    public class SaveGameData
    {
        public int savedMoney;
        public int savedReputation;
        public string currentOwnedCarId;
        public List<string> ownedCarIds = new List<string>();
        public List<string> purchasedUpgradeIds = new List<string>();
        public List<string> completedRaceIds = new List<string>();
        public float worldTimeOfDay = 21f;
        public string lastGarageScene = "Garage";
    }
}

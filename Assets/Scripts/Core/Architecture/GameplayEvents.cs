namespace Underground.Core.Architecture
{
    public enum TimeWindow
    {
        Day,
        Sunset,
        Night,
        LateNight
    }
    
    public readonly struct SessionStartedEvent
    {
        public SessionStartedEvent(string sceneName)
        {
            SceneName = sceneName;
        }

        public string SceneName { get; }
    }
    
    public readonly struct RaceStartedEvent
    {
        public RaceStartedEvent(string raceId)
        {
            RaceId = raceId;
        }

        public string RaceId { get; }
    }

    public readonly struct RaceFinishedEvent
    {
        public RaceFinishedEvent(string raceId, bool playerWon, int moneyEarned, int reputationEarned)
        {
            RaceId = raceId;
            PlayerWon = playerWon;
            MoneyEarned = moneyEarned;
            ReputationEarned = reputationEarned;
        }

        public string RaceId { get; }
        public bool PlayerWon { get; }
        public int MoneyEarned { get; }
        public int ReputationEarned { get; }
    }

    public readonly struct RaceRewardsBankedEvent
    {
        public RaceRewardsBankedEvent(int money, int reputation)
        {
            Money = money;
            Reputation = reputation;
        }

        public int Money { get; }
        public int Reputation { get; }
    }

    public readonly struct TimeWindowChangedEvent
    {
        public TimeWindowChangedEvent(TimeWindow previous, TimeWindow current)
        {
            Previous = previous;
            Current = current;
        }

        public TimeWindow Previous { get; }
        public TimeWindow Current { get; }
    }

    public readonly struct SessionBankedEvent
    {
        public SessionBankedEvent(int moneyBanked, int reputationBanked, float worldTime)
        {
            MoneyBanked = moneyBanked;
            ReputationBanked = reputationBanked;
            WorldTime = worldTime;
        }

        public int MoneyBanked { get; }
        public int ReputationBanked { get; }
        public float WorldTime { get; }
    }

    public readonly struct SessionFailedEvent
    {
        public SessionFailedEvent(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }



    public readonly struct ProgressSavedEvent
    {
        public ProgressSavedEvent(float worldTime, string targetScene)
        {
            WorldTime = worldTime;
            TargetScene = targetScene;
        }

        public float WorldTime { get; }
        public string TargetScene { get; }
    }

    public readonly struct MoneyChangedEvent
    {
        public MoneyChangedEvent(int bankedMoney)
        {
            BankedMoney = bankedMoney;
        }

        public int BankedMoney { get; }
    }

    public readonly struct ReputationChangedEvent
    {
        public ReputationChangedEvent(int reputation)
        {
            Reputation = reputation;
        }

        public int Reputation { get; }
    }

    public readonly struct CurrentCarChangedEvent
    {
        public CurrentCarChangedEvent(string carId)
        {
            CarId = carId;
        }

        public string CarId { get; }
    }

    public readonly struct WorldTimeChangedEvent
    {
        public WorldTimeChangedEvent(float timeOfDay, bool isNight)
        {
            TimeOfDay = timeOfDay;
            IsNight = isNight;
        }

        public float TimeOfDay { get; }
        public bool IsNight { get; }
    }
}

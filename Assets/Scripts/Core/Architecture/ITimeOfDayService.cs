namespace Underground.Core.Architecture
{
    public interface ITimeOfDayService
    {
        float TimeOfDay { get; }
        bool IsNight { get; }
        TimeWindow CurrentWindow { get; }
        void SetTime(float timeOfDay);
    }
}

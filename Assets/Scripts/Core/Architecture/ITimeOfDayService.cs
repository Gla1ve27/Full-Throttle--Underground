namespace Underground.Core.Architecture
{
    public interface ITimeOfDayService
    {
        float TimeOfDay { get; }
        bool IsNight { get; }
        void SetTime(float timeOfDay);
    }
}

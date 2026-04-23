using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [System.Serializable]
    public sealed class FTGearbox
    {
        public float finalDrive = 3.7f;
        public float shiftUpRpm01 = 0.88f;
        public float shiftDownRpm01 = 0.32f;
        public float shiftDelay = 0.16f;
        public float reverseRatio = -3.1f;
        public float[] forwardRatios = { 3.1f, 2.1f, 1.52f, 1.16f, 0.93f, 0.78f };

        public int CurrentGear { get; private set; } = 1;
        public bool IsShifting => shiftTimer > 0f;
        public float CurrentRatio => CurrentGear < 0 ? reverseRatio : forwardRatios[Mathf.Clamp(CurrentGear - 1, 0, forwardRatios.Length - 1)];

        private float shiftTimer;

        public void Reset()
        {
            CurrentGear = 1;
            shiftTimer = 0f;
        }

        public void UpdateAutomatic(float rpm01, float throttle, bool reverseHeld, float speedKph, float dt)
        {
            shiftTimer = Mathf.Max(0f, shiftTimer - dt);
            if (shiftTimer > 0f)
            {
                return;
            }

            if (reverseHeld && speedKph < 8f && throttle <= 0.05f)
            {
                CurrentGear = -1;
                return;
            }

            if (CurrentGear < 1)
            {
                CurrentGear = 1;
                shiftTimer = shiftDelay;
                return;
            }

            if (rpm01 > shiftUpRpm01 && CurrentGear < forwardRatios.Length)
            {
                CurrentGear++;
                shiftTimer = shiftDelay;
                return;
            }

            if (rpm01 < shiftDownRpm01 && CurrentGear > 1 && throttle > 0.15f)
            {
                CurrentGear--;
                shiftTimer = shiftDelay;
            }
        }
    }
}

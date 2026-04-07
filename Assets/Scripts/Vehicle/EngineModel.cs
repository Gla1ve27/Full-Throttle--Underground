using UnityEngine;

namespace Underground.Vehicle
{
    public class EngineModel : MonoBehaviour
    {
        [SerializeField] private AnimationCurve normalizedTorqueCurve = new AnimationCurve(
            new Keyframe(0f, 0.24f),
            new Keyframe(0.2f, 0.42f),
            new Keyframe(0.45f, 0.62f),
            new Keyframe(0.68f, 0.74f),
            new Keyframe(0.86f, 0.72f),
            new Keyframe(1f, 0.56f));

        public float EvaluateNormalizedTorque(float normalizedRpm)
        {
            return normalizedTorqueCurve.Evaluate(Mathf.Clamp01(normalizedRpm));
        }
    }
}
